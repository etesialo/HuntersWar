using UnityEngine;

namespace Archer
{
    /// <summary>
    /// 방 프리팹의 루트. 문/포털 참조와 방 종류를 들고 있다.
    /// 적은 자식의 EnemySpawn 위치에서 나온다.
    /// </summary>
    public class RoomRoot : MonoBehaviour
    {
        public RoomType type;
        [Tooltip("방을 깨면 꺼지는 문 (판정 포함)")]
        public GameObject door;
        [Tooltip("방을 깨면 켜지는 포털")]
        public GameObject portal;
        [Tooltip("입구 (벽 줄 위, 2칸 폭). 플레이어는 바로 안쪽에서 시작")]
        public Transform entry;
        [Tooltip("출구 (벽 줄 위, 2칸 폭). 방을 깨고 여기로 가면 다음 방")]
        public Transform exit;

        public EnemySpawn[] Spawns => GetComponentsInChildren<EnemySpawn>();

        /// <summary>편집할 때 18 x 28 그리드와 벽 자리(바깥 한 줄)를 씬 뷰에 그린다</summary>
        void OnDrawGizmos()
        {
            if (Application.isPlaying) return;
            Gizmos.matrix = transform.localToWorldMatrix;
            float hw = RoomGrid.Width * 0.5f, hl = RoomGrid.Length * 0.5f, c = RoomGrid.Cell;
            const float y = 0.02f;

            Gizmos.color = new Color(1f, 1f, 1f, 0.18f);
            for (int i = 0; i <= RoomGrid.Cols; i++)
                Gizmos.DrawLine(new Vector3(-hw + i * c, y, -hl), new Vector3(-hw + i * c, y, hl));
            for (int j = 0; j <= RoomGrid.Rows; j++)
                Gizmos.DrawLine(new Vector3(-hw, y, -hl + j * c), new Vector3(hw, y, -hl + j * c));

            // 바깥 한 줄 = 벽 자리 (장애물 금지)
            Gizmos.color = new Color(1f, 0.25f, 0.25f, 0.6f);
            Gizmos.DrawWireCube(new Vector3(0f, y, 0f), new Vector3(RoomGrid.Width - c * 2f, 0f, RoomGrid.Length - c * 2f));
            Gizmos.DrawWireCube(new Vector3(0f, y, 0f), new Vector3(RoomGrid.Width, 0f, RoomGrid.Length));
        }
    }
}
