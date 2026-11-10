// 바닥 위로 천천히 지나가는 구름 그림자. 바닥 바로 위 판에 곱하기 합성.
// 캐릭터/벽은 판보다 앞(깊이)에 있어 그림자가 안 묻고, 바닥에만 드리운다.
Shader "Archer/CloudShadow"
{
    Properties
    {
        _MainTex ("Cloud Shadow (white = 밝음)", 2D) = "white" {}
        _Strength ("Strength", Range(0, 1)) = 0.35
        _ShadowColor ("Shadow Color", Color) = (0.55, 0.5, 0.65, 1)
        _Tiling ("Tiling (월드 m 당)", Float) = 0.035
        _Scroll ("Scroll (xy)", Vector) = (0.012, 0.006, 0, 0)
        _EdgeFade ("Edge Fade", Range(0.01, 0.5)) = 0.15
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-50" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Blend DstColor Zero
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half _Strength;
                half4 _ShadowColor;
                float _Tiling;
                float4 _Scroll;
                half _EdgeFade;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float2 world : TEXCOORD1; };

            Varyings vert (Attributes v)
            {
                Varyings o;
                float3 w = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(w);
                o.uv = v.uv;
                o.world = w.xz;
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                half c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.world * _Tiling + _Time.y * _Scroll.xy).r;
                float2 e = min(i.uv, 1 - i.uv) / _EdgeFade;
                half edge = saturate(min(e.x, e.y));
                half k = (1 - c) * _Strength * edge;
                // 1 이면 그대로, 그림자 부분은 그림자색 쪽으로 곱함
                return half4(lerp(half3(1, 1, 1), _ShadowColor.rgb, k), 1);
            }
            ENDHLSL
        }
    }
}
