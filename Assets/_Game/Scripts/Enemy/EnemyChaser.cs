using UnityEngine;

namespace Archer
{
    /// <summary>슬라임: 통통 튀며 플레이어에게 돌진, 몸통 박치기.</summary>
    public class EnemyChaser : Enemy
    {
        public float hopHeight = 0.15f;
        float t;

        protected override void Tick(float dt)
        {
            var d = ToPlayer;
            if (d.sqrMagnitude > 0.01f)
            {
                d.Normalize();
                Face(d);
                Move(d, moveSpeed);
            }
            t += dt * 8f;
            model.localPosition = new Vector3(0f, Mathf.Abs(Mathf.Sin(t)) * hopHeight, 0f);
        }
    }
}
