using System.Collections;
using UnityEngine;

namespace Archer
{
    /// <summary>천사: 다가가면 체력 회복 / 스킬 획득 중 하나를 고르게 한다.</summary>
    public class Angel : MonoBehaviour
    {
        public float triggerRadius = 1.8f;

        bool used;
        float t;
        Vector3 basePos;

        void Start() => basePos = transform.position;

        void Update()
        {
            t += Time.deltaTime;
            transform.position = basePos + Vector3.up * (0.25f + Mathf.Sin(t * 2f) * 0.15f);
            if (used) return;

            var p = GameManager.I.Player;
            if (p != null && !p.IsDead && Combat.Flat(p.transform.position - basePos).magnitude < triggerRadius)
            {
                used = true;
                GameManager.I.OnAngelTouched(this);
            }
        }

        public void Vanish() => StartCoroutine(VanishRoutine());

        IEnumerator VanishRoutine()
        {
            var s = transform.localScale;
            float k = 0f;
            while (k < 1f)
            {
                k += Time.deltaTime / 0.4f;
                transform.localScale = s * (1f - Mathf.Clamp01(k));
                yield return null;
            }
            Destroy(gameObject);
        }
    }
}
