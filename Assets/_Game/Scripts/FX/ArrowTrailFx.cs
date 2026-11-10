using UnityEngine;

namespace Archer
{
    /// <summary>
    /// 속성 화살(불/독/얼음) 연출 묶음. 화살 자식으로 붙고 Projectile 이 켜고 끈다.
    /// streak: 화살 뒤로 길게 끌리는 꼬리 그림(바닥에 눕힌 쿼드). 쏜 직후엔 짧게 시작해 금방 다 자란다
    /// (풀에서 꺼낸 화살이 처음부터 긴 꼬리를 달고 플레이어 몸을 덮지 않게).
    /// </summary>
    public class ArrowTrailFx : MonoBehaviour
    {
        public Transform streak;
        [Tooltip("꼬리가 다 자라는 시간")] public float growTime = 0.12f;
        [Tooltip("화살 몸통 색")] public Color tint = Color.white;
        [Tooltip("켜면 원래 화살 몸통을 숨김 (얼음 결정처럼 연출 그림이 화살을 대신할 때)")] public bool hideBody;

        float len, tipZ, age;
        Vector3 baseScale;
        bool ready;
        float collapseFrom = -1f, collapseT, collapseTime;

        void Init()
        {
            if (ready || !streak) return;
            ready = true;
            baseScale = streak.localScale;
            len = baseScale.x;
            tipZ = streak.localPosition.z + len * 0.5f;
        }

        public void Restart()
        {
            Init();
            age = 0f;
            collapseFrom = -1f;
            foreach (var e in GetComponentsInChildren<TrailEmitter>(true))
            {
                e.enabled = true;
                e.ResetAt();
            }
            foreach (var t in GetComponentsInChildren<TrailRenderer>()) t.Clear();
            foreach (var e in GetComponentsInChildren<ElectricTrail>()) e.ResetTrail();
            foreach (var ps in GetComponentsInChildren<ParticleSystem>())
            {
                ps.Clear(false);
                ps.Play(false);
            }
            Apply();
        }

        /// <summary>벽에 부딪힘: 꼬리가 머리(부딪힌 자리)쪽으로 빨려 들어가며 접히고, 파티클은 새로 안 뿜는다</summary>
        public void BeginCollapse(float time)
        {
            Init();
            collapseFrom = streak ? streak.localScale.x : 0f;
            collapseT = 0f;
            collapseTime = Mathf.Max(time, 0.01f);
            foreach (var ps in GetComponentsInChildren<ParticleSystem>())
                ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            foreach (var e in GetComponentsInChildren<TrailEmitter>()) e.enabled = false;
        }

        void Update()
        {
            age += Time.deltaTime;
            if (collapseFrom >= 0f) collapseT += Time.deltaTime;
            Apply();
        }

        void Apply()
        {
            if (!streak) return;
            Init();
            float l = Mathf.Max(0.05f, len * Mathf.Clamp01(age / Mathf.Max(growTime, 0.01f)));
            if (collapseFrom >= 0f)
            {
                float k = Mathf.Clamp01(collapseT / collapseTime);
                l = Mathf.Max(0.01f, collapseFrom * (1f - k) * (1f - k));
            }
            streak.localScale = new Vector3(l, baseScale.y, baseScale.z);
            var p = streak.localPosition;
            p.z = tipZ - l * 0.5f;
            streak.localPosition = p;
        }
    }
}
