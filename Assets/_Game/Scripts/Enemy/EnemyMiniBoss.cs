using UnityEngine;

namespace Archer
{
    /// <summary>킹 슬라임: 쫓아오다 몸을 눌렀다 펴며 원형 탄 발사. 죽으면 슬라임으로 분열.</summary>
    public class EnemyMiniBoss : Enemy
    {
        public float slamInterval = 3.2f;
        public float slamWindup = 0.6f;
        public int splitCount = 3;

        float timer;
        bool slamming;
        float slamT;

        protected override void OnActivated()
        {
            timer = 2f;
        }

        protected override void Tick(float dt)
        {
            if (slamming)
            {
                slamT += dt;
                float s = Mathf.Clamp01(slamT / slamWindup);
                model.localScale = new Vector3(ModelScale.x * (1f + 0.25f * s), ModelScale.y * (1f - 0.3f * s), ModelScale.z * (1f + 0.25f * s));
                if (slamT < slamWindup) return;

                model.localScale = ModelScale;
                FireRadial(14, 6f, Random.Range(0f, 30f));
                FireFan(ToPlayer.normalized, 3, 30f, 8f);
                GameManager.I.cam.Shake(0.2f, 0.2f);
                slamming = false;
                timer = slamInterval;
                return;
            }

            var d = ToPlayer.normalized;
            Face(d);
            Move(d, moveSpeed);
            timer -= dt;
            if (timer <= 0f)
            {
                slamming = true;
                slamT = 0f;
            }
        }

        protected override void OnDeath()
        {
            for (int i = 0; i < splitCount; i++)
            {
                var offset = Quaternion.Euler(0f, 360f * i / splitCount, 0f) * Vector3.forward * 1.2f;
                stage.SpawnEnemy(config.slime, transform.position + offset);
            }
        }
    }
}
