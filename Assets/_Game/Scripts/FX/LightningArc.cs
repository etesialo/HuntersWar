using System.Collections.Generic;
using UnityEngine;

namespace Archer
{
    /// <summary>
    /// 번개 화살: 맞은 적에서 주변 적에게 번개가 튀는 처리 + 지그재그 번개 줄기 연출.
    /// 연출은 코드로 LineRenderer 를 만들어 쓰고 끝나면 숨겨 두었다가 다시 쓴다 (프리팹 필요 없음).
    /// 재질은 GameConfig.lightningMat, 비어 있으면 Sprites/Default.
    /// </summary>
    public class LightningArc : MonoBehaviour
    {
        public const int ChainCount = 2;
        public const float ChainRange = 3.5f;
        public const float ChainDamageRatio = 0.4f;

        const float Life = 0.18f;
        const float JitterInterval = 0.03f;
        const int Segments = 8;

        static readonly List<LightningArc> free = new();
        static Material fallbackMat;
        static readonly List<Enemy> picked = new();

        LineRenderer line;
        Vector3 from, to;
        float life, jitter;

        /// <summary>
        /// from 에게 맞은 화살에서 주변 적(이미 맞은 적 제외) 가까운 순으로 번개를 튀긴다.
        /// 튄 번개는 속성 효과 없이 피해만 준다 (연쇄의 연쇄는 없음).
        /// </summary>
        public static void Chain(Enemy from, float hitDamage, IList<Enemy> exclude)
        {
            if (from == null || GameManager.I == null || GameManager.I.stage == null) return;
            picked.Clear();
            var origin = from.transform.position;
            for (int k = 0; k < ChainCount; k++)
            {
                Enemy best = null;
                float bd = ChainRange * ChainRange;
                foreach (var e in GameManager.I.stage.Enemies)
                {
                    if (e == null || e == from || !e.IsActive || picked.Contains(e)) continue;
                    if (exclude != null && exclude.Contains(e)) continue;
                    float d = Combat.Flat(e.transform.position - origin).sqrMagnitude;
                    if (d < bd) { bd = d; best = e; }
                }
                if (best == null) break;
                picked.Add(best);
            }
            if (picked.Count == 0) return;

            Sound.Play(Sfx.Zap);
            var a = origin + Vector3.up * 0.7f;
            foreach (var e in picked)
            {
                var b = e.transform.position + Vector3.up * 0.7f;
                Spawn(a, b);
                e.TakeDamage(hitDamage * ChainDamageRatio, false, DamageColors.Lightning);
            }
        }

        public static void Spawn(Vector3 a, Vector3 b)
        {
            LightningArc arc = null;
            while (free.Count > 0 && arc == null)
            {
                arc = free[free.Count - 1];
                free.RemoveAt(free.Count - 1);
            }
            if (arc == null) arc = Create();
            arc.from = a;
            arc.to = b;
            arc.life = Life;
            arc.jitter = 0f;
            arc.gameObject.SetActive(true);
            arc.Rebuild();
        }

        static LightningArc Create()
        {
            var go = new GameObject("LightningArc");
            var arc = go.AddComponent<LightningArc>();
            var lr = go.AddComponent<LineRenderer>();
            lr.positionCount = Segments + 1;
            lr.useWorldSpace = true;
            lr.numCapVertices = 2;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.textureMode = LineTextureMode.Stretch;
            var cfg = GameManager.I ? GameManager.I.config : null;
            Material mat = cfg ? cfg.lightningMat : null;
            if (!mat)
            {
                if (!fallbackMat) fallbackMat = new Material(Shader.Find("Sprites/Default")) { name = "LightningArc (auto)" };
                mat = fallbackMat;
            }
            lr.sharedMaterial = mat;
            arc.line = lr;
            return arc;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            life -= dt;
            if (life <= 0f)
            {
                gameObject.SetActive(false);
                free.Add(this);
                return;
            }
            jitter -= dt;
            if (jitter <= 0f) Rebuild();

            // 처음엔 하얗게 굵다가 푸르게 가늘어지며 사라짐
            float k = life / Life;
            var c = Color.Lerp(new Color(0.45f, 0.7f, 1f, 0f), new Color(0.95f, 0.98f, 1f, 1f), k);
            line.startColor = line.endColor = c;
            line.widthMultiplier = Mathf.Lerp(0.04f, 0.14f, k);
        }

        /// <summary>양 끝은 고정, 가운데 점들만 옆으로 흔들어 지그재그</summary>
        void Rebuild()
        {
            jitter = JitterInterval;
            var d = to - from;
            var side = Vector3.Cross(d, Vector3.up).normalized;
            if (side == Vector3.zero) side = Vector3.right;
            float amp = Mathf.Clamp(d.magnitude * 0.12f, 0.1f, 0.45f);
            for (int i = 0; i <= Segments; i++)
            {
                float t = (float)i / Segments;
                var p = Vector3.Lerp(from, to, t);
                if (i > 0 && i < Segments)
                {
                    float env = Mathf.Sin(t * Mathf.PI); // 가운데가 가장 크게 흔들림
                    p += side * (Random.Range(-1f, 1f) * amp * env) + Vector3.up * (Random.Range(-0.5f, 0.5f) * amp * env);
                }
                line.SetPosition(i, p);
            }
        }

        void OnDestroy() => free.Remove(this);
    }
}
