using System.Collections;
using UnityEngine;

namespace Archer
{
    /// <summary>
    /// 몬스터 사망 연출 (떼어낸 모델에 붙는다):
    /// 사망 모션을 틀면서 몸을 바닥에 붙여두고(뜨지 않게), 모션이 끝나기 전에 땅속으로 다 파묻어 지운다.
    /// → 모션이 끝나 멈춘 자세는 보이지 않는다.
    /// 몸 위치는 스킨 메시의 범위로 잰다 (WAF 모델은 뼈가 숨겨져 있어 뼈 위치를 못 읽음)
    /// </summary>
    public class DeathSink : MonoBehaviour
    {
        const float SinkStart = 0.15f;  // 모션 길이 대비: 이때부터 가라앉기 시작
        const float SinkEnd = 0.85f;    // 모션 길이 대비: 이때까지 다 파묻힘 (모션 끝나기 전)
        const float FallTime = 0.2f;    // 떠 있던 몬스터가 바닥까지 떨어지는 시간

        float ground;
        float sink;                     // 일부러 가라앉힌 양 (바닥 맞춤이 되돌리지 않게 따로 둔다)
        Renderer[] rends;

        public static void Begin(Transform model, float groundY, float animLength)
        {
            var d = model.gameObject.AddComponent<DeathSink>();
            d.ground = groundY;
            d.rends = model.GetComponentsInChildren<Renderer>();
            // 스킨 메시 범위가 애니메이션을 따라 매 프레임 갱신되게
            foreach (var r in d.rends)
                if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = true;
            d.StartCoroutine(d.Run(Mathf.Clamp(animLength, 0.4f, 1.5f)));
        }

        IEnumerator Run(float animLength)
        {
            // 죽는 순간의 몸 높이 (모션이 몸을 날리기 전)
            Measure(out float low0, out float high0);
            float height = Mathf.Clamp(high0 - low0, 0.3f, 3f);

            // 박쥐처럼 떠 있던 몬스터는 순간이동하지 않게 짧게 떨어뜨린다
            float hover = Mathf.Max(0f, low0 - ground);

            float start = animLength * SinkStart;
            float end = animLength * SinkEnd;
            for (float t = 0f; t < end; t += Time.deltaTime)
            {
                yield return null; // 애니메이터가 이번 프레임 자세를 만든 뒤에 맞춘다
                float f = Mathf.Clamp01(t / FallTime);
                Ground(hover * (1f - f * f));
                float k = Mathf.Clamp01((t - start) / (end - start));
                Shift(-height * 1.2f * k * k);
            }
            Destroy(gameObject);
        }

        /// <summary>몸 아랫면을 바닥에 딱 맞춘다 — 모션이 띄워도 누르고, 파고들면 올린다 (일부러 가라앉힌 양은 빼고 본다)</summary>
        void Ground(float lift)
        {
            Measure(out float low, out _);
            low -= sink;
            Move(ground + lift - low);
        }

        void Shift(float amount)
        {
            Move(amount - sink);
            sink = amount;
        }

        void Move(float dy) => transform.position += Vector3.up * dy;

        void Measure(out float low, out float high)
        {
            low = float.MaxValue;
            high = float.MinValue;
            foreach (var r in rends)
            {
                if (!r || !r.enabled || r is ParticleSystemRenderer) continue;
                var b = r.bounds;
                low = Mathf.Min(low, b.min.y);
                high = Mathf.Max(high, b.max.y);
            }
            if (low == float.MaxValue) { low = ground + sink; high = low + 1f; }
        }
    }
}
