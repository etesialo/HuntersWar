// 흘러가는 옅은 안개 판 (바닥 안개 / 위쪽 안개구름).
// 월드 XZ 좌표로 안개 무늬 두 겹을 서로 다른 방향·속도로 흘려 겹치고, 판 가장자리는 부드럽게 사라진다.
Shader "Archer/FogScroll"
{
    Properties
    {
        _MainTex ("Mist (grayscale, tileable)", 2D) = "black" {}
        _Color ("Tint (alpha = 진하기)", Color) = (1, 0.96, 0.9, 0.2)
        _Tiling ("Tiling (월드 m 당)", Float) = 0.08
        _Scroll1 ("Scroll 1 (xy)", Vector) = (0.02, 0.008, 0, 0)
        _Scroll2 ("Scroll 2 (xy)", Vector) = (-0.012, 0.015, 0, 0)
        _Contrast ("Contrast", Range(0.5, 4)) = 1.6
        _EdgeFade ("Edge Fade", Range(0.01, 0.5)) = 0.2
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                float _Tiling;
                float4 _Scroll1;
                float4 _Scroll2;
                half _Contrast;
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
                float2 p = i.world * _Tiling;
                half a1 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, p + _Time.y * _Scroll1.xy).r;
                half a2 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, p * 1.37 + float2(0.31, 0.57) + _Time.y * _Scroll2.xy).r;
                half m = saturate(pow(saturate(a1 * 0.6 + a2 * 0.6), _Contrast) * 1.4);
                // 판 가장자리 페이드 (경계가 안 보이게)
                float2 e = min(i.uv, 1 - i.uv) / _EdgeFade;
                half edge = saturate(min(e.x, e.y));
                edge = edge * edge * (3 - 2 * edge);
                return half4(_Color.rgb, m * _Color.a * edge);
            }
            ENDHLSL
        }
    }
}
