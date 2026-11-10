using UnityEngine;

namespace Archer
{
    /// <summary>멧돼지: 다가오다가 경로를 예고한 뒤 일직선 돌진.</summary>
    public class EnemyCharger : Enemy
    {
        public float windup = 0.7f;
        public float dashSpeed = 13f;
        public float dashTime = 0.55f;
        public float rest = 0.8f;

        enum State { Approach, Windup, Dash, Rest }
        State state;
        float timer;
        Vector3 dashDir;

        protected override void OnActivated()
        {
            state = State.Approach;
            timer = Random.Range(0.8f, 1.6f);
        }

        protected override void Tick(float dt)
        {
            timer -= dt;
            switch (state)
            {
                case State.Approach:
                {
                    var d = ToPlayer.normalized;
                    Face(d);
                    Move(d, moveSpeed);
                    if (timer <= 0f) { state = State.Windup; timer = windup; }
                    break;
                }
                case State.Windup:
                {
                    dashDir = ToPlayer.normalized;
                    Face(dashDir, 1080f);
                    ShowAimLine(dashDir, dashSpeed * dashTime, bodyRadius * 2f);
                    var shake = Random.insideUnitSphere * 0.05f;
                    shake.y = 0f;
                    model.localPosition = shake;
                    if (timer <= 0f)
                    {
                        HideAimLine();
                        model.localPosition = Vector3.zero;
                        Sound.Play(Sfx.EnemyCharge);
                        state = State.Dash;
                        timer = dashTime;
                    }
                    break;
                }
                case State.Dash:
                {
                    var flags = Move(dashDir, dashSpeed);
                    if ((flags & CollisionFlags.Sides) != 0 || timer <= 0f) { state = State.Rest; timer = rest; }
                    break;
                }
                case State.Rest:
                    if (timer <= 0f) { state = State.Approach; timer = Random.Range(1f, 1.8f); }
                    break;
            }
        }
    }
}
