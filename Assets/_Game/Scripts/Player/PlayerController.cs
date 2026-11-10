using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Archer
{
    /// <summary>움직이면 이동만, 멈추면 가장 가까운 적을 자동 사격 (궁수의 전설 1 규칙).</summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("기본 능력치")]
        public float moveSpeed = 5f;
        public float baseMaxHp = 600f;
        public float baseAttack = 30f;
        [Tooltip("공격 간격(초)")]
        public float attackInterval = 0.75f;
        public float arrowSpeed = 18f;

        [Header("표시")]
        public Transform model;
        public float hpBarOffset = 2.1f;

        public float MaxHp { get; private set; }
        public float Hp { get; private set; }
        public int Level { get; private set; } = 1;
        public int Exp { get; private set; }
        public int ExpToNext => 4 + Level * 4;
        public bool IsDead { get; private set; }
        public bool IsMoving { get; private set; }
        public float Attack => baseAttack * atkMul;
        public readonly Dictionary<SkillId, int> Skills = new();
        /// <summary>테스트용 무적 (체력이 줄지 않음)</summary>
        public bool GodMode { get; set; }

        // 스킬로 바뀌는 값들
        float atkMul = 1f, aspdMul = 1f, shotDmgMul = 1f, speedMul = 1f, damageTakenMul = 1f;
        float critChance = 0.05f, critMult = 2f, killHealRatio;
        int multishot, frontArrows, diagonalArrows, sideArrows, rearArrows;
        int pierce, ricochet, wallBounce, orbCount;
        bool fireArrow, poisonArrow, frostArrow, lightningArrow, windArrow, rockArrow;
        const float WindArrowSpeedMul = 1.4f;

        CharacterController cc;
        VirtualJoystick joystick;
        HpBar hpBar;
        Enemy target;
        float attackCd, invuln, retargetTimer, flashTimer;
        Renderer[] renderers;
        MaterialPropertyBlock mpb;
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int LegacyColor = Shader.PropertyToID("_Color"); // WAF 캐릭터 셰이더
        CharacterAnim anim;

        readonly List<Transform> orbs = new();
        readonly Dictionary<Enemy, float> orbNextHit = new();
        static readonly Collider[] orbBuf = new Collider[8];
        float orbAngle;

        public void Init(VirtualJoystick joy)
        {
            joystick = joy;
            cc = GetComponent<CharacterController>();
            if (model == null) model = transform;
            renderers = model.GetComponentsInChildren<Renderer>();
            anim = model.GetComponentInChildren<CharacterAnim>();
            mpb = new MaterialPropertyBlock();
            MaxHp = baseMaxHp;
            Hp = MaxHp;
            hpBar = GameManager.I.ui.CreateHpBar(transform, hpBarOffset, 130f, new Color(0.3f, 0.9f, 0.35f), true);
            RefreshHpBar();
        }

        void Update()
        {
            if (IsDead || cc == null) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            if (invuln > 0f) invuln -= dt;
            UpdateFlash(dt);

            bool canAct = GameManager.I.State == GameState.Playing;
            Vector2 input = canAct ? ReadInput() : Vector2.zero;
            IsMoving = input.sqrMagnitude > 0.0001f;
            if (anim) anim.SetMoving(IsMoving);
            attackCd -= dt;

            if (IsMoving)
            {
                var dir = new Vector3(input.x, 0f, input.y);
                if (dir.sqrMagnitude > 1f) dir.Normalize();
                cc.Move(dir * (moveSpeed * speedMul * dt));
                // 몬스터 충돌체를 타고 올라가지 않게 바닥에 붙인다
                var pos = transform.position;
                if (Mathf.Abs(pos.y) > 0.001f) { pos.y = 0f; transform.position = pos; }
                Face(dir, dt);
            }
            else if (canAct)
            {
                retargetTimer -= dt;
                if (retargetTimer <= 0f || target == null || !target.IsActive)
                {
                    target = FindTarget();
                    retargetTimer = 0.25f;
                }
                if (target != null)
                {
                    Face(Combat.Flat(target.transform.position - transform.position), dt);
                    if (attackCd <= 0f)
                    {
                        attackCd = attackInterval / aspdMul;
                        StartCoroutine(Volley());
                    }
                }
            }

            UpdateOrbs(dt);
        }

        Vector2 ReadInput()
        {
            Vector2 v = joystick != null ? joystick.Direction : Vector2.zero;
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null)
            {
                var k = Vector2.zero;
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) k.y += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) k.y -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) k.x += 1f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) k.x -= 1f;
                if (k != Vector2.zero) v = k.normalized;
            }
#endif
            return v;
        }

        void Face(Vector3 dir, float dt)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return;
            model.rotation = Quaternion.RotateTowards(model.rotation, Quaternion.LookRotation(dir), 1080f * dt);
        }

        // ───────── 공격 ─────────

        Enemy FindTarget()
        {
            Enemy best = null, bestVisible = null;
            float bd = float.MaxValue, bvd = float.MaxValue;
            foreach (var e in GameManager.I.stage.Enemies)
            {
                if (e == null || !e.IsActive) continue;
                float d = (e.transform.position - transform.position).sqrMagnitude;
                if (d < bd) { bd = d; best = e; }
                if (d < bvd && Combat.HasLineOfSight(transform.position, e.transform.position)) { bvd = d; bestVisible = e; }
            }
            // 벽에 안 가린 적을 우선
            return bestVisible != null ? bestVisible : best;
        }

        IEnumerator Volley()
        {
            for (int i = 0; i <= multishot; i++)
            {
                if (IsDead || IsMoving) yield break;
                if (target == null || !target.IsActive)
                {
                    target = FindTarget();
                    if (target == null) yield break;
                }
                var dir = Combat.Flat(target.transform.position - transform.position).normalized;
                if (dir == Vector3.zero) dir = model.forward;
                if (anim) anim.Attack();
                Sound.Play(fireArrow ? Sfx.ShootFire : frostArrow ? Sfx.ShootFrost : poisonArrow ? Sfx.ShootPoison : Sfx.Shoot);
                FireSpread(dir);
                if (i < multishot) yield return new WaitForSeconds(0.12f);
            }
        }

        void FireSpread(Vector3 fwd)
        {
            var data = new ShotData
            {
                damage = Attack * shotDmgMul,
                critChance = critChance,
                critMult = critMult,
                pierce = pierce,
                ricochet = ricochet,
                wallBounce = wallBounce,
                fire = fireArrow,
                poison = poisonArrow,
                frost = frostArrow,
                lightning = lightningArrow,
                wind = windArrow,
                rock = rockArrow,
                fromPlayer = true,
            };
            var origin = transform.position + Vector3.up * 0.6f;
            var right = Vector3.Cross(Vector3.up, fwd);

            int n = 1 + frontArrows;
            for (int i = 0; i < n; i++)
                Shoot(origin + right * ((i - (n - 1) * 0.5f) * 0.3f), fwd, data);

            for (int i = 0; i < diagonalArrows; i++)
            {
                float a = 30f + i * 15f;
                Shoot(origin, Quaternion.Euler(0f, a, 0f) * fwd, data);
                Shoot(origin, Quaternion.Euler(0f, -a, 0f) * fwd, data);
            }

            if (sideArrows > 0)
            {
                Shoot(origin, right, data);
                Shoot(origin, -right, data);
            }

            for (int i = 0; i < rearArrows; i++)
                Shoot(origin + right * ((i - (rearArrows - 1) * 0.5f) * 0.3f), -fwd, data);
        }

        void Shoot(Vector3 origin, Vector3 dir, ShotData data)
        {
            var p = Pool.Spawn(GameManager.I.config.arrow, origin + dir * 0.4f, Quaternion.LookRotation(dir));
            p.Launch(dir, arrowSpeed * (data.wind ? WindArrowSpeedMul : 1f), data);
        }

        // ───────── 화염 구체 ─────────

        void RebuildOrbs()
        {
            foreach (var o in orbs) if (o) Destroy(o.gameObject);
            orbs.Clear();
            var fx = GameManager.I.config.fireOrbFx;
            for (int i = 0; i < orbCount; i++)
            {
                GameObject o;
                if (fx) o = Instantiate(fx);
                else
                {
                    o = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    DestroyImmediate(o.GetComponent<Collider>());
                    o.transform.localScale = Vector3.one * 0.45f;
                    o.GetComponent<Renderer>().sharedMaterial = GameManager.I.config.orb;
                }
                o.name = "FireOrb";
                orbs.Add(o.transform);
            }
        }

        void UpdateOrbs(float dt)
        {
            if (orbs.Count == 0) return;
            orbAngle += 200f * dt;
            for (int i = 0; i < orbs.Count; i++)
            {
                float a = orbAngle + 360f * i / orbs.Count;
                var p = transform.position + Quaternion.Euler(0f, a, 0f) * Vector3.forward * 1.7f + Vector3.up * 0.7f;
                orbs[i].position = p;
                orbs[i].rotation = Quaternion.Euler(0f, -orbAngle * 2.5f, 0f); // 불덩이 그림이 소용돌이치듯 돈다

                int n = Physics.OverlapSphereNonAlloc(p, 0.45f, orbBuf, ~0, QueryTriggerInteraction.Collide);
                for (int j = 0; j < n; j++)
                {
                    var e = orbBuf[j].GetComponentInParent<Enemy>();
                    if (e == null || !e.IsActive) continue;
                    if (orbNextHit.TryGetValue(e, out float t) && Time.time < t) continue;
                    orbNextHit[e] = Time.time + 0.5f;
                    e.TakeDamage(Attack * 0.6f, false, DamageColors.Fire);
                }
            }
        }

        // ───────── 체력 / 경험치 ─────────

        /// <returns>실제로 맞았으면 true (무적 시간 중이면 false)</returns>
        public bool TakeDamage(float amount)
        {
            if (IsDead || invuln > 0f) return false;
            amount *= damageTakenMul;
            if (!GodMode) Hp -= amount; // 테스트 무적: 맞는 연출만 하고 체력은 그대로
            invuln = 0.6f;
            var gm0 = GameManager.I;
            Sound.Play(Sfx.PlayerHurt);
            gm0.ui.HurtFlash();
            Haptics.Light();
            HitStop.Do(0.06f, 0.1f);
            Flash(new Color(1f, 0.35f, 0.35f));
            var gm = GameManager.I;
            gm.ui.ShowDamage(transform.position + Vector3.up * 1.6f, amount, false, DamageColors.Hurt);
            gm.cam.Shake(0.15f, 0.15f);
            if (Hp <= 0f)
            {
                Hp = 0f;
                Die();
            }
            RefreshHpBar();
            return true;
        }

        public void Heal(float amount)
        {
            if (IsDead || amount <= 0f) return;
            float before = Hp;
            Hp = Mathf.Min(MaxHp, Hp + amount);
            if (Hp - before >= 1f)
            {
                Sound.Play(Sfx.Heal, 0.6f);
                GameManager.I.ui.ShowText(transform.position + Vector3.up * 1.6f, "+" + Mathf.RoundToInt(Hp - before), DamageColors.Heal, 1f);
            }
            RefreshHpBar();
        }

        public void AddExp(int v)
        {
            Exp += v;
            while (Exp >= ExpToNext)
            {
                Exp -= ExpToNext;
                Level++;
                GameManager.I.OnLevelUp();
            }
            GameManager.I.ui.SetLevel(Level, Exp, ExpToNext);
        }

        public void OnKill()
        {
            if (killHealRatio > 0f) Heal(MaxHp * killHealRatio);
        }

        void Die()
        {
            IsDead = true;
            StopAllCoroutines();
            if (anim) anim.Die();
            else model.rotation = Quaternion.Euler(-90f, model.eulerAngles.y, 0f);
            GameManager.I.OnPlayerDied();
        }

        public void Revive()
        {
            IsDead = false;
            Hp = MaxHp;
            invuln = 2.5f;
            model.rotation = Quaternion.identity;
            if (anim) anim.ResetState();
            RefreshHpBar();
        }

        public void Teleport(Vector3 pos)
        {
            cc.enabled = false;
            transform.position = pos;
            cc.enabled = true;
            model.rotation = Quaternion.identity;
            target = null;
            orbNextHit.Clear();
        }

        void RefreshHpBar()
        {
            if (hpBar) hpBar.Set(Hp / MaxHp, Mathf.CeilToInt(Hp).ToString());
            if (GameManager.I) GameManager.I.ui.SetDanger(!IsDead && Hp < MaxHp * 0.3f);
        }

        void Flash(Color c)
        {
            flashTimer = 0.12f;
            mpb.SetColor(BaseColor, c);
            mpb.SetColor(LegacyColor, c);
            foreach (var r in renderers) r.SetPropertyBlock(mpb);
        }

        void UpdateFlash(float dt)
        {
            if (flashTimer <= 0f) return;
            flashTimer -= dt;
            if (flashTimer <= 0f)
                foreach (var r in renderers) r.SetPropertyBlock(null);
        }

        // ───────── 스킬 ─────────

        public void ApplySkill(SkillId id)
        {
            Skills.TryGetValue(id, out int lv);
            SetSkillLevel(id, lv + 1);
            if (id == SkillId.Heal) Heal(MaxHp * 0.4f);
        }

        /// <summary>스킬 레벨을 직접 정한다 (0 = 없앰). 능력치는 가진 스킬 전체로 처음부터 다시 계산한다.</summary>
        public void SetSkillLevel(SkillId id, int level)
        {
            if (level <= 0) Skills.Remove(id);
            else Skills[id] = level;
            RecalcStats();
        }

        int Lv(SkillId id) => Skills.TryGetValue(id, out int v) ? v : 0;

        void RecalcStats()
        {
            int ms = Lv(SkillId.Multishot), fa = Lv(SkillId.FrontArrow);
            multishot = ms;
            frontArrows = fa;
            shotDmgMul = Mathf.Pow(0.9f, ms) * Mathf.Pow(0.85f, fa);
            diagonalArrows = Lv(SkillId.DiagonalArrow);
            sideArrows = Lv(SkillId.SideArrow);
            rearArrows = Lv(SkillId.RearArrow);
            pierce = Lv(SkillId.Pierce);
            ricochet = Lv(SkillId.Ricochet) * 2;
            wallBounce = Lv(SkillId.BouncyWall) * 2;
            atkMul = 1f + 0.25f * Lv(SkillId.AttackBoost);
            aspdMul = 1f + 0.2f * Lv(SkillId.AttackSpeed);
            critChance = 0.05f + 0.1f * Lv(SkillId.CritMaster);
            critMult = 2f + 0.4f * Lv(SkillId.CritMaster);
            killHealRatio = 0.02f * Lv(SkillId.Bloodthirst);
            fireArrow = Lv(SkillId.FireArrow) > 0;
            poisonArrow = Lv(SkillId.PoisonArrow) > 0;
            frostArrow = Lv(SkillId.FrostArrow) > 0;
            lightningArrow = Lv(SkillId.LightningArrow) > 0;
            windArrow = Lv(SkillId.WindArrow) > 0;
            rockArrow = Lv(SkillId.RockArrow) > 0;
            speedMul = 1f + 0.15f * Lv(SkillId.Agility);
            damageTakenMul = Mathf.Pow(0.85f, Lv(SkillId.IronSkin));

            // 최대 체력: 레벨마다 +20%. 늘어난 만큼 현재 체력도 늘고, 줄면 최대치에 맞춘다
            float newMax = baseMaxHp * Mathf.Pow(1.2f, Lv(SkillId.HpBoost));
            if (newMax > MaxHp) Hp += newMax - MaxHp;
            MaxHp = newMax;
            Hp = Mathf.Min(Hp, MaxHp);

            int orbsWanted = 2 * Lv(SkillId.FireOrb);
            if (orbsWanted != orbCount)
            {
                orbCount = orbsWanted;
                RebuildOrbs();
            }
            RefreshHpBar();
        }
    }
}
