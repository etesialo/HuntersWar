using UnityEngine;

namespace Archer
{
    /// <summary>
    /// 방 꾸미기 테마. 판정(충돌·화살 막기)은 RoomBuilder가 칸 단위로 따로 붙이고,
    /// 여기 프리팹들은 겉모습만 맡는다.
    /// </summary>
    [CreateAssetMenu(menuName = "Archer/Room Theme")]
    public class RoomTheme : ScriptableObject
    {
        [Tooltip("배치도 '#' 칸에 랜덤으로 놓는 장애물. 한 칸 크기에 맞게 줄여서 놓는다.")]
        public GameObject[] obstacles;

        [Tooltip("문 양옆 기둥")]
        public GameObject doorPillar;

        [Tooltip("위쪽 벽 뒤 배경 장식 (왼쪽부터 순서대로)")]
        public GameObject[] backdrop;

        [Header("상단 글로우 (블룸 느낌 알파 판)")]
        public GameObject topGlow;
        [Tooltip("비면 프리팹 머티리얼 그대로. 색은 머티리얼의 _TintColor 로 조정")]
        public Material topGlowMaterial;
        public Vector3 topGlowPos = new(0f, 2.5f, 10.11f);
        public float topGlowScale = 31.81f;
    }
}
