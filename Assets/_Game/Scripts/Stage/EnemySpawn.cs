using UnityEngine;

namespace Archer
{
    /// <summary>배치도 글자와 같은 값을 쓴다 (GameConfig.GetEnemy).</summary>
    public enum EnemyKind
    {
        Slime = 'S',
        Bat = 'B',
        Archer = 'A',
        Charger = 'C',
        Turret = 'T',
        MiniBoss = 'M',
        Boss = 'X',
    }

    /// <summary>방 프리팹 안의 적 스폰 위치. 게임 화면에는 안 보이고 씬 뷰에만 표시된다.</summary>
    public class EnemySpawn : MonoBehaviour
    {
        public EnemyKind kind = EnemyKind.Slime;

        void OnDrawGizmos()
        {
            Gizmos.color = kind switch
            {
                EnemyKind.Boss or EnemyKind.MiniBoss => new Color(1f, 0.2f, 0.2f, 0.8f),
                EnemyKind.Archer or EnemyKind.Turret => new Color(1f, 0.8f, 0.2f, 0.8f),
                _ => new Color(0.3f, 1f, 0.4f, 0.8f),
            };
            float r = kind is EnemyKind.Boss or EnemyKind.MiniBoss ? 0.8f : 0.4f;
            Gizmos.DrawSphere(transform.position + Vector3.up * r, r);
            Gizmos.DrawWireCube(transform.position + Vector3.up * 0.01f, new Vector3(1f, 0f, 1f));
        }
    }
}
