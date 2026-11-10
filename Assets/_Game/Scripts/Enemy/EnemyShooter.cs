using UnityEngine;

namespace Archer
{
    /// <summary>해골 궁수: 돌아다니다가 멈춰서 조준선을 보여준 뒤 한 발 발사.</summary>
    public class EnemyShooter : Enemy
    {
        public float aimTime = 0.8f;
        public float bulletSpeed = 8f;
        public Vector2 moveTimeRange = new(1f, 2f);
        public float keepDistance = 4f;

        enum State { Move, Aim }
        State state;
        float timer;
        Vector3 wander;

        protected override void OnActivated()
        {
            state = State.Move;
            timer = Random.Range(0.3f, 1f);
            PickWander();
        }

        void PickWander()
        {
            var to = ToPlayer;
            var rnd = Random.insideUnitSphere;
            rnd.y = 0f;
            wander = to.magnitude < keepDistance ? (-to.normalized + rnd * 0.6f).normalized : rnd.normalized;
        }

        protected override void Tick(float dt)
        {
            timer -= dt;
            if (state == State.Move)
            {
                if (wander != Vector3.zero)
                {
                    Face(wander);
                    Move(wander, moveSpeed);
                }
                if (timer <= 0f)
                {
                    state = State.Aim;
                    timer = aimTime;
                }
            }
            else
            {
                var to = ToPlayer.normalized;
                Face(to, 1080f);
                ShowAimLine(to, 12f, 0.08f, 1f - Mathf.Clamp01(timer / aimTime));
                if (timer <= 0f)
                {
                    HideAimLine();
                    FireBullet(to, bulletSpeed);
                    state = State.Move;
                    timer = Random.Range(moveTimeRange.x, moveTimeRange.y);
                    PickWander();
                }
            }
        }
    }
}
