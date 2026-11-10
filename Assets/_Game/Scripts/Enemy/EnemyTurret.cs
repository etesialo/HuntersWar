using UnityEngine;

namespace Archer
{
    /// <summary>식인 꽃: 제자리에서 3갈래 조준탄과 8방향 탄을 번갈아 발사.</summary>
    public class EnemyTurret : Enemy
    {
        public float interval = 2.4f;
        public float bulletSpeed = 6.5f;

        float timer;
        int pattern;

        protected override void OnActivated()
        {
            timer = Random.Range(0.8f, interval);
        }

        protected override void Tick(float dt)
        {
            Face(ToPlayer, 360f);
            timer -= dt;

            // 쏘기 직전 부풀어 오르는 예고
            float pulse = timer < 0.4f ? 1f + (0.4f - timer) * 0.6f : 1f;
            model.localScale = ModelScale * pulse;

            if (timer > 0f) return;
            timer = interval;
            model.localScale = ModelScale;
            if (pattern++ % 2 == 0) FireFan(ToPlayer.normalized, 3, 40f, bulletSpeed);
            else FireRadial(8, bulletSpeed * 0.85f, Random.Range(0f, 45f));
        }
    }
}
