// 바닥 구멍용 깊이 마스크. 색은 안 그리고 깊이만 써서, 뒤에 그려지는 바닥을 가린다.
// 순서: 구멍 벽·물(Geometry-2) → 이 마스크(Geometry-1) → 바닥(Geometry)
Shader "Archer/DepthMask"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry-1" }
        Pass
        {
            Name "DepthMask"
            Tags { "LightMode"="UniversalForward" }
            ColorMask 0
            ZWrite On
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };

            float4 vert(Attributes v) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(v);
                return TransformObjectToHClip(v.positionOS.xyz);
            }
            half4 frag() : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
