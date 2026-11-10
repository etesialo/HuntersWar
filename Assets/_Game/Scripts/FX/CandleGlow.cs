using UnityEngine;

namespace Archer
{
    /// <summary>촛불 빛무리: 불꽃처럼 불규칙하게 커졌다 작아지고 밝아졌다 어두워짐</summary>
    public class CandleGlow : MonoBehaviour
    {
        public Renderer glow;
        public Color color = new(1f, 0.62f, 0.25f, 1f);
        public float size = 1.6f;
        public float speed = 7f;

        static readonly int TintId = Shader.PropertyToID("_TintColor");
        MaterialPropertyBlock mpb;
        float seed;

        void Awake()
        {
            mpb = new MaterialPropertyBlock();
            seed = Random.Range(0f, 100f);
        }

        void LateUpdate()
        {
            if (!glow) return;
            float t = Time.time * speed;
            // 느린 흔들림 + 빠른 떨림
            float n = Mathf.PerlinNoise(seed, t * 0.35f) * 0.7f + Mathf.PerlinNoise(seed + 13f, t) * 0.3f;
            glow.transform.localScale = Vector3.one * (size * (0.82f + 0.36f * n));
            mpb.SetColor(TintId, color * (0.55f + 0.6f * n));
            glow.SetPropertyBlock(mpb);
        }
    }
}
