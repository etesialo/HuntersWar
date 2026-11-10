using System.Collections.Generic;
using UnityEngine;

namespace Archer
{
    /// <summary>
    /// 지나간 자리에 파티클(연기 등)을 남긴다. 파티클은 화살이 아니라 씬에 하나만 있는 공용 시스템에서 나오므로
    /// 화살이 벽에 맞아 사라져도 연기는 남아서 천천히 흩어진다.
    /// </summary>
    public class TrailEmitter : MonoBehaviour
    {
        [Tooltip("공용으로 쓸 파티클 프리팹 (발사 0, World 공간)")]
        public ParticleSystem prefab;
        public float perMeter = 9f;
        public float perSecond = 10f;

        static readonly Dictionary<ParticleSystem, ParticleSystem> shared = new();

        ParticleSystem sys;
        Vector3 last;
        float acc;

        static ParticleSystem Shared(ParticleSystem prefab)
        {
            if (shared.TryGetValue(prefab, out var s) && s) return s;
            s = Instantiate(prefab);
            s.name = prefab.name + " (공용)";
            s.Play();
            shared[prefab] = s;
            return s;
        }

        /// <summary>화살을 새로 쏠 때: 이전 위치에서 연기가 길게 이어지지 않게</summary>
        public void ResetAt()
        {
            last = transform.position;
            acc = 0f;
        }

        void OnEnable() => ResetAt();

        void LateUpdate()
        {
            if (!prefab) return;
            if (!sys) sys = Shared(prefab);
            var p = transform.position;
            acc += (p - last).magnitude * perMeter + Time.deltaTime * perSecond;
            int n = (int)acc;
            acc -= n;
            var ep = new ParticleSystem.EmitParams { applyShapeToPosition = true };
            for (int i = 0; i < n; i++)
            {
                ep.position = Vector3.Lerp(last, p, (i + 1f) / n);
                sys.Emit(ep, 1);
            }
            last = p;
        }
    }
}
