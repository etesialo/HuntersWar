using UnityEngine;

namespace Archer
{
    /// <summary>모든 적의 공통 처리: 체력, 등장 연출, 상태이상, 접촉 피해, 드랍.</summary>
    public abstract class Enemy : MonoBehaviour
    {
        [Header("능력치")]
        public string displayName = "몬스터";
        public float maxHp = 60f;
        public float moveSpeed = 2f;
        public float contactDamage = 50f;
        public float attackDamage = 70f;
        public float bodyRadius = 0.5f;
        [Tooltip("맞았을 때 밀려나는 정도 (0 이면 안 밀림, 보스는 항상 안 밀림)")]
        public float knockback = 1f;
        [Tooltip("맞을 때 하얗게 번쩍이는 밝기 배율 (원래 밝은 몬스터는 낮게)")]
        public float hitFlash = 1.4f;
        public int expValue = 2;
        public int coinValue = 1;
        public bool isBoss;

        [Header("표시")]
        public Transform model;
        public float hpBarOffset = 1.4f;
        public float hpBarWidth = 90f;

        public float Hp { get; protected set; }
        public bool IsDead { get; private set; }
        public bool IsActive => !IsDead && spawnTimer <= 0f;

        protected PlayerController player;
        protected StageManager stage;
        protected GameConfig config;
        protected CharacterController cc;
        protected float dmgMul = 1f;
        protected Vector3 ModelScale { get; private set; }

        const float SpawnDuration = 0.6f;
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int LegacyColor = Shader.PropertyToID("_Color"); // WAF 캐릭터 셰이더
        protected CharacterAnim anim;
        Vector3 lastPos;

        float spawnTimer = SpawnDuration;
        Renderer[] rends;
        MaterialPropertyBlock mpb;
        float flashTimer;
        int tintState = -1;
        HpBar bar;
        float burnTimer, burnDps, poisonDps, freezeTimer, slowTimer, dotTimer, stunTimer;
        bool animHeld; // 빙결/기절로 애니메이션을 멈춰둔 상태
        const float WindKnockMul = 2.5f, RockStunChance = 0.25f;
        GameObject aimLine;

        protected virtual void Awake()
        {
            cc = GetComponent<CharacterController>();
            if (model == null) model = transform;
            rends = model.GetComponentsInChildren<Renderer>();
            anim = model.GetComponentInChildren<CharacterAnim>();
            mpb = new MaterialPropertyBlock();
            ModelScale = model.localScale;
        }

        public void Init(StageManager owner, PlayerController target, float hpMul, float damageMul)
        {
            stage = owner;
            player = target;
            dmgMul = damageMul;
            config = GameManager.I.config;
            maxHp *= hpMul;
            Hp = maxHp;
            spawnTimer = SpawnDuration;
            lastPos = transform.position;
            model.localScale = ModelScale * 0.05f;
            Sound.Play(Sfx.EnemySpawn, 0.7f);
            if (config.enemySpawnFx)
            {
                var fx = Pool.Spawn(config.enemySpawnFx, transform.position + Vector3.up * 0.05f, Quaternion.identity);
                fx.transform.localScale = Vector3.one * Mathf.Max(1f, bodyRadius * 2.2f);
                fx.Play(Color.white);
            }

            var ui = GameManager.I.ui;
            if (isBoss) ui.ShowBossBar(displayName);
            else bar = ui.CreateHpBar(transform, hpBarOffset, hpBarWidth, new Color(0.95f, 0.25f, 0.25f), false);
        }

        protected abstract void Tick(float dt);
        protected virtual void OnActivated() { }
        protected virtual void OnDeath() { }

        void Update()
        {
            if (IsDead || player == null) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            if (spawnTimer > 0f)
            {
                spawnTimer -= dt;
                float t = 1f - Mathf.Clamp01(spawnTimer / SpawnDuration);
                model.localScale = ModelScale * Mathf.LerpUnclamped(0.05f, 1f, Combat.EaseOutBack(t));
                if (spawnTimer <= 0f)
                {
                    model.localScale = ModelScale;
                    OnActivated();
                }
                return;
            }

            TickStatus(dt);
            if (IsDead) return;
            UpdatePoisonFx();

            if (!player.IsDead)
            {
                UpdatePunch(dt);
                if (freezeTimer > 0f) freezeTimer -= dt;
                else if (stunTimer > 0f) stunTimer -= dt;
                else Tick(dt);
                UpdateFreezeFx();
                UpdateAnimHold();
                UpdateKnock(dt);

                if (anim)
                {
                    // 실제로 움직였는지로 이동/대기 모션을 고른다
                    var delta = transform.position - lastPos;
                    delta.y = 0f;
                    anim.SetMoving(delta.sqrMagnitude > (0.3f * dt) * (0.3f * dt));
                }

                if (contactDamage > 0f &&
                    Combat.Flat(player.transform.position - transform.position).magnitude < bodyRadius + 0.4f)
                {
                    if (player.TakeDamage(contactDamage * dmgMul) && config.playerContactHit)
                        Pool.Spawn(config.playerContactHit, player.transform.position + Vector3.up * 0.8f, Quaternion.identity).Play(new Color(1f, 0.4f, 0.35f));
                }
            }
            UpdateTint(dt);
            lastPos = transform.position;
        }

        // ───────── 피해 ─────────

        public void ApplyHit(in ShotData s, Vector3 hitDir = default)
        {
            if (IsDead) return;
            bool crit = Random.value < s.critChance;
            float dmg = s.damage * (crit ? s.critMult : 1f);

            // 넉백: 화살이 날아온 방향으로 살짝 밀림 (치명타는 더 세게 + 히트스톱)
            hitDir.y = 0f;
            if (!isBoss && knockback > 0f && hitDir.sqrMagnitude > 0.0001f)
                knockVel = hitDir.normalized * ((crit ? 5.5f : 3.2f) * knockback * (s.wind ? WindKnockMul : 1f));
            if (crit) HitStop.Do(0.035f);

            if (s.fire)
            {
                burnTimer = 2f;
                burnDps = Mathf.Max(burnDps, s.damage * 0.35f);
            }
            if (s.poison) poisonDps = Mathf.Min(poisonDps + s.damage * 0.12f, s.damage * 0.6f);
            if (s.frost)
            {
                slowTimer = 1.5f;
                if (Random.value < 0.2f) freezeTimer = isBoss ? 0.4f : 1.2f;
            }
            if (s.rock && Random.value < RockStunChance)
            {
                if (stunTimer <= 0f) Sound.Play(Sfx.Stun);
                stunTimer = Mathf.Max(stunTimer, isBoss ? 0.3f : 0.8f);
            }

            TakeDamage(dmg, crit, crit ? DamageColors.Crit : DamageColors.Normal);
        }

        public void TakeDamage(float amount, bool crit, Color color)
        {
            if (IsDead) return;
            Hp -= amount;
            flashTimer = 0.06f;
            punch = PunchTime;

            var ui = GameManager.I.ui;
            ui.ShowDamage(transform.position + Vector3.up * (hpBarOffset * 0.75f), amount, crit, color);
            if (isBoss) ui.SetBossHp(Mathf.Max(0f, Hp) / maxHp);
            else if (bar) bar.Set(Hp / maxHp);

            if (Hp <= 0f) Die();
        }

        // ───────── 타격감 (넉백 / 찌그러짐) ─────────

        Vector3 knockVel;
        float punch;
        bool punching;
        const float PunchTime = 0.12f;

        void UpdateKnock(float dt)
        {
            if (knockVel.sqrMagnitude < 0.01f) return;
            var delta = knockVel * dt;
            if (cc != null && cc.enabled) cc.Move(delta);
            else transform.position = StageManager.ClampToRoom(transform.position + delta, bodyRadius);
            KeepOnGround();
            knockVel = Vector3.Lerp(knockVel, Vector3.zero, 14f * dt);
        }

        /// <summary>맞는 순간 몸이 옆으로 퍼지고 위아래로 눌렸다 돌아옴</summary>
        void UpdatePunch(float dt)
        {
            if (punch <= 0f)
            {
                if (punching) { model.localScale = ModelScale; punching = false; }
                return;
            }
            punch -= dt;
            punching = true;
            float k = Mathf.Clamp01(punch / PunchTime);
            k = k * k;
            model.localScale = Vector3.Scale(ModelScale, new Vector3(1f + 0.2f * k, 1f - 0.18f * k, 1f + 0.2f * k));
        }

        // ───────── 중독 연출 ─────────

        GameObject poisonFx;

        /// <summary>중독되면 몸 주변에 독 기포/독기/독방울 (몸에 붙어 따라다님)</summary>
        void UpdatePoisonFx()
        {
            if (poisonFx || poisonDps <= 0f || !config.poisonedFx) return;
            var b = BodyBounds();
            poisonFx = Instantiate(config.poisonedFx, transform);
            poisonFx.transform.position = b.center;
            poisonFx.transform.localScale = Vector3.one * Mathf.Clamp(Mathf.Max(b.size.x, b.size.y), 0.8f, 3f);
        }

        /// <summary>죽을 때: 독 연출을 떼어내 새로 안 뿜고 남은 건 서서히 사라지게</summary>
        void ReleasePoisonFx()
        {
            if (!poisonFx) return;
            poisonFx.transform.SetParent(null, true);
            foreach (var ps in poisonFx.GetComponentsInChildren<ParticleSystem>())
                ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            Destroy(poisonFx, 2f);
            poisonFx = null;
        }

        // ───────── 빙결 연출 ─────────

        IceBlock ice;
        bool frozenFx;

        /// <summary>얼면: 동작 정지 + 얼음덩어리 씌움. 풀리면: 얼음이 산산조각</summary>
        void UpdateFreezeFx()
        {
            bool f = freezeTimer > 0f && !IsDead;
            if (f == frozenFx) return;
            frozenFx = f;
            if (f)
            {
                Sound.Play(Sfx.Freeze);
                if (!config.iceBlock) return;
                var b = BodyBounds();
                ice = Pool.Spawn(config.iceBlock, b.center, Quaternion.identity);
                ice.Show(b.center, Mathf.Clamp(Mathf.Max(b.size.x, b.size.y) * 1.3f, 1f, 4f));
            }
            else ReleaseIce(true);
        }

        /// <summary>빙결이나 기절 중에는 애니메이션을 멈춘다 (둘이 겹쳐도 다 풀릴 때만 다시 움직임)</summary>
        void UpdateAnimHold()
        {
            bool held = (freezeTimer > 0f || stunTimer > 0f) && !IsDead;
            if (held == animHeld) return;
            animHeld = held;
            if (anim) anim.SetFrozen(held);
        }

        void ReleaseIce(bool shatter)
        {
            if (!ice) return;
            if (shatter) Sound.Play(Sfx.IceBreak);
            if (shatter && config.iceShatter)
            {
                var s = Pool.Spawn(config.iceShatter, ice.transform.position, Quaternion.LookRotation(Vector3.up));
                s.transform.localScale = Vector3.one * Mathf.Max(1f, ice.transform.localScale.x * 0.8f);
                s.Play(Color.white);
            }
            Pool.Despawn(ice.gameObject);
            ice = null;
        }

        Bounds BodyBounds()
        {
            var b = new Bounds(transform.position + Vector3.up * 0.5f, Vector3.one * bodyRadius * 2f);
            bool first = true;
            foreach (var r in rends)
            {
                if (!r || !r.enabled || r is ParticleSystemRenderer) continue;
                if (first) { b = r.bounds; first = false; }
                else b.Encapsulate(r.bounds);
            }
            return b;
        }

        void TickStatus(float dt)
        {
            if (slowTimer > 0f) slowTimer -= dt;
            if (burnTimer > 0f) burnTimer -= dt;
            dotTimer -= dt;
            if (dotTimer > 0f) return;
            dotTimer = 0.5f;

            float d = 0f;
            if (burnTimer > 0f) d += burnDps * 0.5f;
            if (poisonDps > 0f) d += poisonDps * 0.5f;
            if (d > 0f) TakeDamage(d, false, burnTimer > 0f ? DamageColors.Fire : DamageColors.Poison);
        }

        void Die()
        {
            if (IsDead) return;
            IsDead = true;
            HideAimLine();
            ReleasePoisonFx();
            ReleaseIce(true);
            Sound.Play(Sfx.EnemyDie);
            HitStop.Do(isBoss ? 0.2f : 0.045f);
            GameManager.I.cam.Shake(isBoss ? 0.45f : 0.1f, isBoss ? 0.4f : 0.12f);
            model.localScale = ModelScale;
            DropLoot();
            OnDeath();

            var gm = GameManager.I;
            if (isBoss) gm.ui.HideBossBar();
            if (bar) Destroy(bar.gameObject);
            if (anim)
            {
                // 모델만 떼어내서 사망 모션 → 바닥에 붙인 채 쓰러졌다가 땅속으로 가라앉으며 사라짐 (DeathSink)
                foreach (var r in rends) if (r) r.SetPropertyBlock(null);
                model.SetParent(null, true);
                anim.Die();
                DeathSink.Begin(model, transform.position.y, anim.DieLength);
            }
            gm.Kills++;
            player.OnKill();
            stage.OnEnemyDied(this);
            Destroy(gameObject);
        }

        void DropLoot()
        {
            int gems = Mathf.Clamp(expValue, 1, 6);
            for (int i = 0; i < gems; i++)
            {
                int v = expValue / gems + (i < expValue % gems ? 1 : 0);
                if (v > 0) SpawnPickup(config.expGem, v);
            }
            int coins = Mathf.Clamp(coinValue, 0, 6);
            for (int i = 0; i < coins; i++)
            {
                int v = coinValue / coins + (i < coinValue % coins ? 1 : 0);
                if (v > 0) SpawnPickup(config.coin, v);
            }
        }

        void SpawnPickup(Pickup prefab, int value)
        {
            var rnd = Random.insideUnitSphere;
            rnd.y = 0f;
            var p = Pool.Spawn(prefab, transform.position + Vector3.up * 0.3f, Quaternion.identity);
            p.Init(value, rnd.normalized * Random.Range(2f, 5f));
        }

        void OnDestroy()
        {
            if (bar) Destroy(bar.gameObject);
            if (aimLine) Destroy(aimLine);
            ReleaseIce(false);
        }

        // ───────── 색 (피격 번쩍임 / 빙결) ─────────

        void UpdateTint(float dt)
        {
            if (flashTimer > 0f) flashTimer -= dt;
            int s = flashTimer > 0f ? 1 : freezeTimer > 0f ? 2 : stunTimer > 0f ? 4 : poisonDps > 0f ? 3 : 0;
            if (s == 3)
            {
                // 중독: 초록빛이 꿀렁꿀렁 맥박 (매 프레임 갱신)
                tintState = 3;
                float k = 0.5f + 0.5f * Mathf.Sin(Time.time * 6f);
                var tox = Color.Lerp(new Color(0.85f, 1.15f, 0.7f, 1f), new Color(0.6f, 1.45f, 0.45f, 1f), k);
                mpb.SetColor(BaseColor, tox);
                mpb.SetColor(LegacyColor, tox);
                foreach (var r in rends) r.SetPropertyBlock(mpb);
                return;
            }
            if (s == tintState) return;
            tintState = s;
            if (s == 0)
            {
                foreach (var r in rends) r.SetPropertyBlock(null);
                return;
            }
            // 빙결: 어둡게 누르지 않고 밝은 얼음빛으로 (색을 곱하는 셰이더라 1보다 크게 줘서 밝힘)
            var frost = new Color(1.1f, 1.3f, 1.6f, 1f);
            // 기절(바위 화살): 돌처럼 누런 회색으로 굳은 느낌
            var stone = new Color(0.78f, 0.72f, 0.62f, 1f);
            var tint = s == 1 ? Color.white : s == 4 ? stone : frost;
            mpb.SetColor(BaseColor, tint);
            // WAF 셰이더는 _Color 를 곱하므로 흰색 번쩍임은 밝기를 올려서 표현
            mpb.SetColor(LegacyColor, s == 1 ? new Color(hitFlash, hitFlash, hitFlash, 1f) : tint);
            foreach (var r in rends) r.SetPropertyBlock(mpb);
        }

        // ───────── 하위 클래스용 도우미 ─────────

        protected Vector3 ToPlayer => Combat.Flat(player.transform.position - transform.position);

        protected CollisionFlags Move(Vector3 dir, float speed)
        {
            float mul = slowTimer > 0f ? 0.5f : 1f;
            var delta = dir * (speed * mul * Time.deltaTime);
            if (cc != null && cc.enabled)
            {
                var flags = cc.Move(delta);
                KeepOnGround();
                return flags;
            }
            transform.position = StageManager.ClampToRoom(transform.position + delta, bodyRadius);
            KeepOnGround();
            return CollisionFlags.None;
        }

        /// <summary>
        /// 몸을 바닥(y=0)에 붙인다. 중력이 없어서 다른 몬스터/플레이어 충돌체를 타고 올라가면
        /// 뜬 채로 다녀 화살(높이 0.6)이 아래로 지나가던 버그 방지
        /// </summary>
        void KeepOnGround()
        {
            var p = transform.position;
            if (Mathf.Abs(p.y) < 0.001f) return;
            p.y = 0f;
            transform.position = p;
        }

        protected void Face(Vector3 dir, float degPerSec = 720f)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), degPerSec * Time.deltaTime);
        }

        protected void FireBullet(Vector3 dir, float speed = 7f, float dmgScale = 1f)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return;
            if (anim) anim.Attack();
            Sound.Play(Sfx.EnemyShoot, 0.6f);
            dir.Normalize();
            var data = new ShotData { damage = attackDamage * dmgMul * dmgScale };
            var origin = transform.position + Vector3.up * 0.6f + dir * (bodyRadius + 0.1f);
            Pool.Spawn(config.enemyBullet, origin, Quaternion.LookRotation(dir)).Launch(dir, speed, data);
        }

        protected void FireRadial(int count, float speed, float offsetDeg = 0f)
        {
            for (int i = 0; i < count; i++)
                FireBullet(Quaternion.Euler(0f, offsetDeg + 360f * i / count, 0f) * Vector3.forward, speed);
        }

        protected void FireFan(Vector3 dir, int count, float spread, float speed)
        {
            for (int i = 0; i < count; i++)
            {
                float a = count == 1 ? 0f : -spread * 0.5f + spread * i / (count - 1);
                FireBullet(Quaternion.Euler(0f, a, 0f) * dir, speed);
            }
        }

        static readonly int ProgressId = Shader.PropertyToID("_Progress");
        static readonly int LengthId = Shader.PropertyToID("_Length");
        Renderer aimRenderer;
        MaterialPropertyBlock aimMpb;

        /// <summary>
        /// 공격 예고선. 바닥에 눕힌 반투명 페더 줄 (config.aimLine).
        /// progress(0~1): 조준이 얼마나 진행됐는지 — 끝나갈수록 진하고 좁아진다.
        /// </summary>
        protected void ShowAimLine(Vector3 dir, float length, float width = 0.12f, float progress = 0.5f)
        {
            bool soft = config.aimLine != null;
            if (aimLine == null)
            {
                aimLine = GameObject.CreatePrimitive(soft ? PrimitiveType.Quad : PrimitiveType.Cube);
                aimLine.name = "AimLine";
                DestroyImmediate(aimLine.GetComponent<Collider>());
                aimRenderer = aimLine.GetComponent<Renderer>();
                aimRenderer.sharedMaterial = soft ? config.aimLine : config.telegraph;
                aimRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                aimRenderer.receiveShadows = false;
                aimMpb = new MaterialPropertyBlock();
            }
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return;
            dir.Normalize();
            aimLine.SetActive(true);
            var pos = transform.position + dir * (length * 0.5f) + Vector3.up * 0.04f;
            if (soft)
            {
                // 쿼드(XY 평면)를 바닥에 눕혀 길이(Y)가 발사 방향을 보게
                aimLine.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(dir) * Quaternion.Euler(90f, 0f, 0f));
                aimLine.transform.localScale = new Vector3(Mathf.Max(width, 0.25f), length, 1f);
                aimMpb.SetFloat(ProgressId, Mathf.Clamp01(progress));
                aimMpb.SetFloat(LengthId, length);
                aimRenderer.SetPropertyBlock(aimMpb);
            }
            else
            {
                aimLine.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(dir));
                aimLine.transform.localScale = new Vector3(width, 0.02f, length);
            }
        }

        protected void HideAimLine()
        {
            if (aimLine) aimLine.SetActive(false);
        }
    }
}
