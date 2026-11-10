using System.Collections;
using UnityEngine;

namespace Archer
{
    /// <summary>
    /// 보스: 걷기 → [원형 탄막 / 부채꼴 조준탄 / 돌진 2회 / 소환] 순서로 반복.
    /// 체력 50% 이하에서 격노해 패턴이 빨라진다.
    /// </summary>
    public class EnemyBoss : Enemy
    {
        public float chargeSpeed = 14f;

        bool walking;

        protected override void OnActivated()
        {
            StartCoroutine(Brain());
        }

        protected override void Tick(float dt)
        {
            if (!walking) return;
            var d = ToPlayer.normalized;
            Face(d);
            Move(d, moveSpeed);
        }

        IEnumerator Brain()
        {
            yield return new WaitForSeconds(0.8f);
            int pattern = 0;
            while (!IsDead)
            {
                bool enraged = Hp < maxHp * 0.5f;

                walking = true;
                yield return new WaitForSeconds(enraged ? 1f : 1.6f);
                walking = false;

                switch (pattern++ % 4)
                {
                    case 0:
                        for (int i = 0; i < (enraged ? 4 : 3); i++)
                        {
                            FireRadial(enraged ? 20 : 16, 5.5f, i * 7f);
                            yield return new WaitForSeconds(0.55f);
                        }
                        break;

                    case 1:
                        for (int i = 0; i < (enraged ? 5 : 4); i++)
                        {
                            transform.rotation = Quaternion.LookRotation(ToPlayer.normalized);
                            FireFan(ToPlayer.normalized, 5, 50f, 8.5f);
                            yield return new WaitForSeconds(0.4f);
                        }
                        break;

                    case 2:
                        for (int i = 0; i < 2; i++) yield return Charge();
                        break;

                    case 3:
                        if (stage.Enemies.Count < 6)
                        {
                            stage.SpawnEnemy(config.bat, transform.position + transform.right * 2f);
                            stage.SpawnEnemy(config.bat, transform.position - transform.right * 2f);
                            stage.SpawnEnemy(config.slime, transform.position + transform.forward * 2f);
                        }
                        else
                        {
                            FireRadial(24, 4.5f);
                        }
                        yield return new WaitForSeconds(0.8f);
                        break;
                }
                yield return new WaitForSeconds(0.4f);
            }
        }

        IEnumerator Charge()
        {
            var dir = ToPlayer.normalized;
            float t = 0f;
            while (t < 0.8f)
            {
                var to = ToPlayer;
                if (to.sqrMagnitude > 0.01f) dir = to.normalized;
                transform.rotation = Quaternion.LookRotation(dir);
                ShowAimLine(dir, 10f, bodyRadius * 2f);
                t += Time.deltaTime;
                yield return null;
            }
            HideAimLine();

            t = 0f;
            while (t < 0.7f)
            {
                var flags = Move(dir, chargeSpeed);
                t += Time.deltaTime;
                if ((flags & CollisionFlags.Sides) != 0) break;
                yield return null;
            }
            GameManager.I.cam.Shake(0.3f, 0.25f);
            yield return new WaitForSeconds(0.4f);
        }
    }
}
