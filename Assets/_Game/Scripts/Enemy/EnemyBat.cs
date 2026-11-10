using UnityEngine;

namespace Archer
{
    /// <summary>박쥐: 장애물 위를 날아다니며 지그재그로 접근.</summary>
    public class EnemyBat : Enemy
    {
        Vector3 dir;
        float changeTimer, t;

        protected override void Tick(float dt)
        {
            changeTimer -= dt;
            if (changeTimer <= 0f)
            {
                changeTimer = Random.Range(0.35f, 0.8f);
                var rnd = Random.insideUnitSphere;
                rnd.y = 0f;
                dir = (ToPlayer.normalized + rnd * 1.1f).normalized;
            }
            Face(dir, 900f);
            Move(dir, moveSpeed);
            t += dt * 12f;
            model.localPosition = new Vector3(0f, Mathf.Sin(t) * 0.1f, 0f);
        }
    }
}
