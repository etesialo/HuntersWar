using UnityEngine;

namespace Archer
{
    /// <summary>
    /// 카메라를 바라보는 그림 (드랍 아이템, 적 탄환 구슬).
    /// flipSpeed: 가로로 납작해졌다 펴지며 동전이 도는 것처럼. bob: 위아래로 둥실.
    /// </summary>
    public class Billboard : MonoBehaviour
    {
        public float flipSpeed;
        public float bobHeight;
        public float bobSpeed = 3f;
        public float spin;          // 화면 기준 회전 (도/초)

        Vector3 baseScale, basePos;
        float phase, angle;
        Transform cam;

        void Awake()
        {
            baseScale = transform.localScale;
            basePos = transform.localPosition;
            phase = Random.Range(0f, 10f);
        }

        void LateUpdate()
        {
            if (!cam)
            {
                var c = Camera.main;
                if (!c) return;
                cam = c.transform;
            }
            float t = Time.time + phase;
            angle += spin * Time.deltaTime;
            transform.rotation = cam.rotation * Quaternion.Euler(0f, 0f, angle);
            if (flipSpeed > 0f)
                transform.localScale = new Vector3(baseScale.x * Mathf.Max(0.12f, Mathf.Abs(Mathf.Cos(t * flipSpeed))), baseScale.y, baseScale.z);
            if (bobHeight > 0f)
                transform.localPosition = basePos + Vector3.up * (Mathf.Sin(t * bobSpeed) * bobHeight);
        }
    }
}
