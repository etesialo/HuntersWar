using System.Collections.Generic;
using UnityEngine;

namespace Archer
{
    public enum PickupType { Exp, Coin }

    /// <summary>경험치 보석 / 코인. 방을 클리어하면 플레이어에게 빨려 들어간다.</summary>
    public class Pickup : MonoBehaviour
    {
        public PickupType type;
        [Tooltip("주웠을 때 반짝임 (비면 없음)")]
        public HitSpark collectFx;
        public Color collectColor = Color.white;
        [Tooltip("켜면 몸통이 스스로 돌지 않음 (그림이 Billboard 로 도는 경우)")]
        public bool noSpin;

        public static readonly List<Pickup> Active = new();

        int value;
        Vector3 vel;
        bool magnet;
        float speed;

        public void Init(int amount, Vector3 velocity)
        {
            value = amount;
            vel = velocity;
            magnet = false;
            speed = 0f;
            if (!Active.Contains(this)) Active.Add(this);
        }

        void OnDisable() => Active.Remove(this);

        public void StartMagnet()
        {
            if (magnet) return;
            magnet = true;
            speed = 2f;
        }

        public static void DespawnAll()
        {
            foreach (var p in Active.ToArray())
                if (p != null) Pool.Despawn(p.gameObject);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            if (!noSpin) transform.Rotate(0f, 180f * dt, 0f, Space.World);

            var gm = GameManager.I;
            var player = gm != null ? gm.Player : null;
            if (player == null) return;

            if (magnet)
            {
                speed += 25f * dt;
                var target = player.transform.position + Vector3.up * 0.6f;
                transform.position = Vector3.MoveTowards(transform.position, target, speed * dt);
                if ((transform.position - target).sqrMagnitude < 0.09f)
                {
                    gm.Collect(type, value);
                    Sound.Play(type == PickupType.Coin ? Sfx.Coin : Sfx.Gem, 0.7f);
                    if (collectFx)
                    {
                        var fx = Pool.Spawn(collectFx, target, Quaternion.identity);
                        fx.transform.localScale = Vector3.one * 0.6f;
                        fx.Play(collectColor);
                    }
                    Pool.Despawn(gameObject);
                }
            }
            else if (vel.sqrMagnitude > 0.001f)
            {
                transform.position = StageManager.ClampToRoom(transform.position + vel * dt, 0.3f);
                vel = Vector3.Lerp(vel, Vector3.zero, 6f * dt);
            }
        }
    }
}
