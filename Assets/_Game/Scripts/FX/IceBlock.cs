using UnityEngine;

namespace Archer
{
    /// <summary>빙결된 몬스터를 감싸는 얼음덩어리. 나타날 때 "쩍" 하고 커지며 씌워진다 (그림은 자식의 Billboard 가 카메라를 봄).</summary>
    public class IceBlock : MonoBehaviour
    {
        public float popTime = 0.18f;

        float t;
        Vector3 target;

        public void Show(Vector3 pos, float size)
        {
            transform.position = pos;
            target = Vector3.one * size;
            t = 0f;
            transform.localScale = target * 0.3f;
            foreach (var ps in GetComponentsInChildren<ParticleSystem>())
            {
                ps.Clear(true);
                ps.Play(true);
            }
        }

        void Update()
        {
            if (t >= popTime) return;
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / popTime);
            transform.localScale = target * Mathf.LerpUnclamped(0.3f, 1f, Combat.EaseOutBack(k));
        }
    }
}
