using UnityEngine;

namespace Archer
{
    /// <summary>화살 적중 섬광. 풀에서 꺼내 Play → 커지며 사라진 뒤 스스로 풀로 돌아간다.</summary>
    public class HitSpark : MonoBehaviour
    {
        public Renderer flash;
        public float life = 0.2f;
        public float size = 1.1f;
        [Tooltip("파티클 색도 Play 색으로 바꿀지 (끄면 프리팹에 정한 색 그대로)")]
        public bool tintParticles = true;
        [Tooltip("켜면 바닥에 눕혀 아무 방향으로 돌림 (적중 섬광). 끄면 꺼낸 방향 그대로 (벽 충돌 조각이 튕겨 나갈 방향)")]
        public bool faceUp = true;

        static readonly int TintId = Shader.PropertyToID("_TintColor");
        MaterialPropertyBlock mpb;
        Color color;
        float t;

        public void Play(Color c)
        {
            color = c;
            t = 0f;
            mpb ??= new MaterialPropertyBlock();
            if (faceUp) transform.rotation = Quaternion.Euler(90f, Random.Range(0f, 360f), 0f);
            foreach (var ps in GetComponentsInChildren<ParticleSystem>())
            {
                if (tintParticles)
                {
                    var m = ps.main;
                    m.startColor = c;
                }
                ps.Clear(false);
                ps.Play(false);
            }
            Apply();
        }

        void Update()
        {
            t += Time.deltaTime;
            if (t >= life + 0.35f) { Pool.Despawn(gameObject); return; }
            Apply();
        }

        void Apply()
        {
            if (!flash) return;
            float k = Mathf.Clamp01(t / life);
            flash.enabled = k < 1f;
            // 빠르게 터졌다가 줄며 사라짐
            float s = size * (k < 0.25f ? Mathf.Lerp(0.4f, 1.15f, k / 0.25f) : Mathf.Lerp(1.15f, 0.8f, (k - 0.25f) / 0.75f));
            flash.transform.localScale = Vector3.one * s;
            float fade = 1f - k * k;
            var c = color * fade; // 더하기 합성이라 색 자체를 어둡게 해서 사라지게
            c.a = fade;
            mpb.SetColor(TintId, c);
            flash.SetPropertyBlock(mpb);
        }
    }
}
