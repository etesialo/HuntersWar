using UnityEngine;

namespace Archer
{
    /// <summary>
    /// 라이트 굽는 동안만 붙는 표식. 바닥 메시를 '구멍 뚫린 임시 메시'로 바꿔둘 때 원래 메시를 기억한다.
    /// 굽기가 끝나면(또는 에디터를 다시 켜면) HoleWallBaker 가 원래 메시로 되돌리고 이 컴포넌트를 지운다.
    /// </summary>
    [AddComponentMenu("")]
    public class BakeFloorBackup : MonoBehaviour
    {
        public Mesh original;
        // 굽는 동안만 그림자를 드리우게 켠다 (구멍 안쪽이 바닥 그림자를 받게).
        // 평소엔 꺼둬야 함: 켜두면 평평한 바닥이 자기 그림자를 받아 바닥 전체가 그림자 속이 된다
        public UnityEngine.Rendering.ShadowCastingMode cast;
    }
}
