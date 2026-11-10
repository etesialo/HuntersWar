using UnityEngine;

namespace Archer
{
    /// <summary>
    /// 방 분위기 묶음 (안개, 구름 그림자, 낙엽, 햇살). StageManager 가 방에 들어갈 때 방 밑에 하나 만든다.
    /// 방 프리팹은 건드리지 않고, 방 안의 촛불(이름에 "CandleFlame")마다 일렁이는 빛무리를 붙인다.
    /// </summary>
    public class Ambience : MonoBehaviour
    {
        [Tooltip("촛불마다 붙일 빛무리 (CandleGlow)")]
        public CandleGlow candleGlow;

        void Start()
        {
            var room = transform.parent;
            if (!room || !candleGlow) return;
            foreach (var t in room.GetComponentsInChildren<Transform>(true))
            {
                if (!t.name.StartsWith("FX_CandleFlame")) continue;
                var g = Instantiate(candleGlow, t);
                g.transform.localPosition = Vector3.up * 0.15f;
            }
        }
    }
}
