using System.Collections.Generic;
using UnityEngine;

namespace Archer
{
    /// <summary>
    /// 플레이어 화살 / 적 탄환 공용. 물리 없이 직접 이동하며 OverlapSphere로 충돌 판정.
    /// Wall 컴포넌트가 붙은 것만 막힘 (물 웅덩이는 통과).
    /// </summary>
    public class Projectile : MonoBehaviour
    {
        public float radius = 0.2f;
        public float lifetime = 3f;
        [Header("속성 화살 연출 (자식). 여러 속성이면 불 > 얼음 > 독 > 번개 > 바람 > 바위 순으로 하나만 보인다")]
        public ArrowTrailFx fireFx;
        public ArrowTrailFx frostFx;
        public ArrowTrailFx poisonFx;
        [Tooltip("비면 화살 몸통만 속성 색으로 칠한다")]
        public ArrowTrailFx lightningFx, windFx, rockFx;
        [Tooltip("속성이 없을 때 항상 켜는 연출 (적 탄환의 마법 꼬리 등)")]
        public ArrowTrailFx baseFx;
        [Tooltip("적에게 맞았을 때 섬광 (비면 없음)")]
        public HitSpark hitSpark;
        [Tooltip("속성 화살이 적에게 맞았을 때 (비면 hitSpark 를 색만 바꿔 씀)")]
        public HitSpark hitFire, hitFrost, hitPoison;
        public HitSpark hitLightning, hitWind, hitRock;
        public Color hitColor = new(1f, 0.85f, 0.45f);

        // 새 속성 색: 연출 프리팹이 비었을 때 화살 몸통/섬광/파편을 이 색으로
        static readonly Color LightningColor = new(0.7f, 0.85f, 1f);
        static readonly Color WindColor = new(0.6f, 1f, 0.85f);
        static readonly Color RockColor = new(0.72f, 0.58f, 0.42f);

        [Header("벽/장애물에 닿을 때 (속성별, 비면 일반)")]
        public HitSpark wallImpact;
        public Color wallImpactColor = Color.white;
        public HitSpark fireImpact;
        public HitSpark frostImpact;
        public HitSpark poisonImpact;
        public HitSpark lightningImpact, windImpact, rockImpact;

        Renderer[] body;
        MaterialPropertyBlock tintMpb;
        ArrowTrailFx activeFx;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        public static readonly List<Projectile> Active = new();

        ShotData data;
        Vector3 dir;
        float speed, life;
        float dying;                    // >0: 벽에 부딪혀 꼬리가 접혀 들어가는 중
        const float CollapseTime = 0.15f;
        readonly List<Enemy> hitEnemies = new();
        static readonly Collider[] buf = new Collider[16];

        public bool FromPlayer => data.fromPlayer;

        public void Launch(Vector3 direction, float spd, ShotData shot)
        {
            direction.y = 0f;
            dir = direction.normalized;
            speed = spd;
            data = shot;
            life = lifetime;
            hitEnemies.Clear();
            dying = 0f;
            foreach (var r in Body()) r.enabled = true;
            transform.rotation = Quaternion.LookRotation(dir);
            if (!Active.Contains(this)) Active.Add(this);
            var el = Elem();
            ArrowTrailFx want = TrailFor(el);
            if (!want) want = baseFx;
            // 연출 프리팹이 없는 새 속성은 화살 몸통만 속성 색으로
            Color? tint = want ? want.tint : el >= El.Lightning ? ColorFor(el) : (Color?)null;
            SetElement(want, tint);
            if (want && want.hideBody)
                foreach (var r in Body()) r.enabled = false;
        }

        // ───────── 속성 ─────────

        /// <summary>보이는 속성 하나 (우선순위: 불 > 얼음 > 독 > 번개 > 바람 > 바위). 적 탄환은 None</summary>
        enum El { None, Fire, Frost, Poison, Lightning, Wind, Rock }

        El Elem()
        {
            if (!data.fromPlayer) return El.None;
            return data.fire ? El.Fire : data.frost ? El.Frost : data.poison ? El.Poison
                : data.lightning ? El.Lightning : data.wind ? El.Wind : data.rock ? El.Rock : El.None;
        }

        ArrowTrailFx TrailFor(El e) => e switch
        {
            El.Fire => fireFx, El.Frost => frostFx, El.Poison => poisonFx,
            El.Lightning => lightningFx, El.Wind => windFx, El.Rock => rockFx, _ => null,
        };

        HitSpark HitFor(El e) => e switch
        {
            El.Fire => hitFire, El.Frost => hitFrost, El.Poison => hitPoison,
            El.Lightning => hitLightning, El.Wind => hitWind, El.Rock => hitRock, _ => null,
        };

        HitSpark ImpactFor(El e) => e switch
        {
            El.Fire => fireImpact, El.Frost => frostImpact, El.Poison => poisonImpact,
            El.Lightning => lightningImpact, El.Wind => windImpact, El.Rock => rockImpact, _ => null,
        };

        Color ColorFor(El e) => e switch
        {
            El.Fire => new Color(1f, 0.55f, 0.15f), El.Frost => new Color(0.45f, 0.85f, 1f),
            El.Poison => new Color(0.55f, 1f, 0.3f), El.Lightning => LightningColor,
            El.Wind => WindColor, El.Rock => RockColor, _ => hitColor,
        };

        // 새 속성은 아직 전용 효과음이 없어서 일반 소리를 쓴다
        static Sfx HitSfx(El e) => e switch { El.Fire => Sfx.HitFire, El.Frost => Sfx.HitFrost, El.Poison => Sfx.HitPoison, _ => Sfx.Hit };
        static Sfx WallSfx(El e) => e switch { El.Fire => Sfx.WallFire, El.Frost => Sfx.WallFrost, El.Poison => Sfx.WallPoison, _ => Sfx.Wall };

        ArrowTrailFx[] AllFx() => new[] { fireFx, frostFx, poisonFx, lightningFx, windFx, rockFx, baseFx };

        Color? activeTint;

        /// <summary>속성 연출 하나만 켠다. 풀에서 재사용되므로 꼬리/파티클은 매번 비우고 새로 시작</summary>
        void SetElement(ArrowTrailFx want, Color? tint)
        {
            foreach (var fx in AllFx())
                if (fx && fx != want && fx.gameObject.activeSelf) fx.gameObject.SetActive(false);
            if (want)
            {
                if (!want.gameObject.activeSelf) want.gameObject.SetActive(true);
                want.Restart();
            }
            activeFx = want;
            if (tint == activeTint) return;
            activeTint = tint;
            Body();
            if (tint.HasValue) tintMpb.SetColor(BaseColorId, tint.Value);
            foreach (var r in body) r.SetPropertyBlock(tint.HasValue ? tintMpb : null);
        }

        Renderer[] Body()
        {
            if (body != null) return body;
            var list = new List<Renderer>();
            foreach (var r in GetComponentsInChildren<MeshRenderer>(true))
                if (!IsFx(r.transform)) list.Add(r);
            body = list.ToArray();
            tintMpb = new MaterialPropertyBlock();
            return body;
        }

        /// <summary>
        /// 벽에 닿은 자리(막히기 직전 위치)에서 속성별로 산산이 흩어지는 조각 연출.
        /// 조각은 벽에서 튕겨 나오는 쪽(들어온 방향의 반대)으로 퍼진다. scale: 튕길 때는 작게
        /// </summary>
        void WallImpact(Vector3 at, float scale)
        {
            var el = Elem();
            HitSpark fx = ImpactFor(el);
            var color = fx ? Color.white : el >= El.Lightning ? ColorFor(el) : wallImpactColor;
            if (!fx) fx = wallImpact;
            Sound.Play(!data.fromPlayer ? Sfx.EnemyBulletHit : WallSfx(el), scale);
            if (!fx) return;
            var s = Pool.Spawn(fx, at, Quaternion.LookRotation(-dir));
            s.transform.localScale = Vector3.one * scale;
            s.Play(color);
        }

        /// <summary>벽에 막힘: 몸통은 숨기고 그 자리에 멈춘 채 꼬리가 접혀 들어간 뒤 사라진다</summary>
        void HitWall(Vector3 at)
        {
            WallImpact(at, 1f);
            if (!activeFx)
            {
                Kill();
                return;
            }
            transform.position = at;
            foreach (var r in Body()) r.enabled = false;
            activeFx.BeginCollapse(CollapseTime);
            dying = CollapseTime;
        }

        bool IsFx(Transform t)
        {
            foreach (var fx in AllFx())
                if (fx && t.IsChildOf(fx.transform)) return true;
            return false;
        }

        void OnDisable() => Active.Remove(this);

        public static void DespawnAll(bool enemyOnly)
        {
            foreach (var p in Active.ToArray())
                if (p != null && (!enemyOnly || !p.data.fromPlayer)) p.Kill();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            if (dying > 0f)
            {
                dying -= dt;
                if (dying <= 0f) Kill();
                return;
            }
            life -= dt;
            if (life <= 0f) { Kill(); return; }

            // 빠른 화살이 얇은 벽을 뚫지 않도록 나눠서 이동
            float total = speed * dt;
            int steps = Mathf.Max(1, Mathf.CeilToInt(total / (radius * 1.5f)));
            float step = total / steps;
            for (int s = 0; s < steps; s++)
            {
                var prev = transform.position;
                transform.position = prev + dir * step;
                if (!CheckHits(prev)) return;
            }
        }

        /// <returns>계속 날아가면 true</returns>
        bool CheckHits(Vector3 prev)
        {
            int n = Physics.OverlapSphereNonAlloc(transform.position, radius, buf, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < n; i++)
            {
                var col = buf[i];
                if (col.GetComponent<Wall>() != null)
                {
                    if (data.wallBounce > 0)
                    {
                        data.wallBounce--;
                        WallImpact(prev, 0.6f);
                        Reflect(col.bounds, prev);
                        return true;
                    }
                    HitWall(prev);
                    return false;
                }

                if (data.fromPlayer)
                {
                    var e = col.GetComponentInParent<Enemy>();
                    if (e == null || !e.IsActive || hitEnemies.Contains(e)) continue;
                    hitEnemies.Add(e);
                    e.ApplyHit(data, dir);
                    if (data.lightning) LightningArc.Chain(e, data.damage, hitEnemies);
                    var el = Elem();
                    Sound.Play(HitSfx(el));
                    var elemFx = HitFor(el);
                    if (elemFx || hitSpark)
                    {
                        var c = elemFx ? Color.white : ColorFor(el);
                        var at = transform.position;
                        at.y = Mathf.Max(at.y, 0.6f);
                        Pool.Spawn(elemFx ? elemFx : hitSpark, at, Quaternion.identity).Play(c);
                    }

                    if (data.ricochet > 0 && TryRicochet(e))
                    {
                        data.ricochet--;
                        data.damage *= 0.8f;
                        return true;
                    }
                    if (data.pierce > 0)
                    {
                        data.pierce--;
                        data.damage *= 0.67f;
                        continue;
                    }
                    Kill();
                    return false;
                }
                else
                {
                    var p = col.GetComponentInParent<PlayerController>();
                    if (p == null || p.IsDead) continue;
                    if (p.TakeDamage(data.damage) && hitSpark)
                        Pool.Spawn(hitSpark, p.transform.position + Vector3.up * 0.8f, Quaternion.identity).Play(Color.white);
                    Kill();
                    return false;
                }
            }
            return true;
        }

        void Reflect(Bounds b, Vector3 prev)
        {
            // 벽/바위는 모두 축 정렬 박스라서 들어오기 직전 위치로 면 방향을 판단
            var d = prev - b.center;
            float nx = d.x / Mathf.Max(0.01f, b.extents.x);
            float nz = d.z / Mathf.Max(0.01f, b.extents.z);
            var nX = new Vector3(Mathf.Sign(nx), 0f, 0f);
            var nZ = new Vector3(0f, 0f, Mathf.Sign(nz));
            bool useX = Mathf.Abs(nx) > Mathf.Abs(nz);
            // 칸이 이어진 벽(장애물 영역)은 칸 사이 이음매에 맞으면 안쪽 면을 고를 수 있다.
            // 고른 면 바로 바깥이 또 벽이면 그 면은 막힌 면이므로 다른 축 면으로 튕긴다
            if (useX ? WallAt(b.center + nX * b.size.x) : WallAt(b.center + nZ * b.size.z)) useX = !useX;
            var normal = useX ? nX : nZ;
            dir = Vector3.Reflect(dir, normal);
            dir.y = 0f;
            dir.Normalize();
            transform.position = prev;
            transform.rotation = Quaternion.LookRotation(dir);
        }

        static readonly Collider[] probeBuf = new Collider[8];

        /// <summary>그 자리에 화살을 막는 벽이 있는지</summary>
        static bool WallAt(Vector3 p)
        {
            int n = Physics.OverlapSphereNonAlloc(p, 0.05f, probeBuf, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < n; i++)
                if (probeBuf[i].GetComponent<Wall>() != null) return true;
            return false;
        }

        bool TryRicochet(Enemy from)
        {
            Enemy best = null;
            float bd = 7f * 7f;
            foreach (var e in GameManager.I.stage.Enemies)
            {
                if (e == null || !e.IsActive || hitEnemies.Contains(e)) continue;
                float d = (e.transform.position - from.transform.position).sqrMagnitude;
                if (d < bd) { bd = d; best = e; }
            }
            if (best == null) return false;
            var to = Combat.Flat(best.transform.position - transform.position);
            if (to.sqrMagnitude < 0.0001f) return false;
            dir = to.normalized;
            transform.rotation = Quaternion.LookRotation(dir);
            return true;
        }

        void Kill() => Pool.Despawn(gameObject);
    }
}
