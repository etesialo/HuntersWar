using UnityEngine;

namespace Archer
{
    /// <summary>말랑한 덩어리처럼 출렁임: 앞뒤로 늘어나면 옆으로 줄고, 반대로도 (부피 유지 느낌)</summary>
    public class Wobble : MonoBehaviour
    {
        public float amount = 0.12f;
        public float speed = 14f;

        Vector3 baseScale;
        float phase;

        void Awake()
        {
            baseScale = transform.localScale;
            phase = Random.Range(0f, 6.28f);
        }

        void LateUpdate()
        {
            float s = Mathf.Sin(Time.time * speed + phase) * amount;
            float s2 = Mathf.Sin(Time.time * speed * 1.7f + phase) * amount * 0.4f;
            transform.localScale = new Vector3(baseScale.x * (1f + s + s2), baseScale.y * (1f - s * 0.8f), baseScale.z);
        }
    }
}
