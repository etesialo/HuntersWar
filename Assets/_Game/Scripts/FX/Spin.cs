using UnityEngine;

namespace Archer
{
    /// <summary>제자리에서 계속 돈다 (바람 화살 소용돌이 등). 축은 로컬 기준</summary>
    public class Spin : MonoBehaviour
    {
        [Tooltip("초당 회전 각도")] public float speed = 720f;
        public Vector3 axis = Vector3.forward;
        [Tooltip("크기가 살짝 커졌다 작아지는 정도 (0 = 없음)")] public float pulse = 0.08f;
        public float pulseSpeed = 18f;

        Vector3 baseScale;
        float phase;

        void Awake()
        {
            baseScale = transform.localScale;
            phase = Random.Range(0f, 6.28f);
        }

        void OnEnable() => transform.Rotate(axis, Random.Range(0f, 360f), Space.Self);

        void LateUpdate()
        {
            transform.Rotate(axis, speed * Time.deltaTime, Space.Self);
            if (pulse > 0f)
                transform.localScale = baseScale * (1f + Mathf.Sin(Time.time * pulseSpeed + phase) * pulse);
        }
    }
}
