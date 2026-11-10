using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Archer
{
    /// <summary>
    /// 디렉셔널 라이트를 '캐릭터·몬스터 그림자 전용'으로 쓴다.
    /// - 그림자: 렌더링 레이어 characterLayer 가 켜진 렌더러(캐릭터·몬스터)만 그림자를 만든다. 벽·배경은 안 만듦
    /// - 빛: 배경(StylizedEnv)에는 빛을 안 비춘다 (environmentLight 만큼만). 배경은 라이트맵·앰비언트로 밝음
    /// 캐릭터 렌더러의 레이어는 프리팹에 들어 있다 (CharacterModels 가 붙임).
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Light))]
    public class ShadowOnlyLight : MonoBehaviour
    {
        /// <summary>캐릭터·몬스터 렌더러가 쓰는 렌더링 레이어 번호 (Light Layer 1)</summary>
        public const int CharacterLayer = 1;
        public static uint CharacterMask => 1u << CharacterLayer;

        [Tooltip("배경에 비추는 빛의 양 (0 = 그림자만, 1 = 평소처럼 비춤)")]
        [Range(0f, 1f)] public float environmentLight = 0f;

        static readonly int EnvMainLightOffId = Shader.PropertyToID("_EnvMainLightOff");

        void OnEnable() => Apply();
        void OnValidate() => Apply();

        void OnDisable()
        {
            // 다른 씬(굽기 씬 등)에 영향이 남지 않게 되돌림
            Shader.SetGlobalFloat(EnvMainLightOffId, 0f);
            if (TryGetComponent<UniversalAdditionalLightData>(out var data)) data.customShadowLayers = false;
        }

        void Apply()
        {
            if (!isActiveAndEnabled) return;
            Shader.SetGlobalFloat(EnvMainLightOffId, 1f - environmentLight);
            if (!TryGetComponent<UniversalAdditionalLightData>(out var data))
                data = gameObject.AddComponent<UniversalAdditionalLightData>();
            data.customShadowLayers = true;
            data.shadowRenderingLayers = CharacterMask;
        }
    }
}
