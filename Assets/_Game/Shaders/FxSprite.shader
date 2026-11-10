// 검은 배경 이펙트 그림(힉스 생성 스프라이트)용 셰이더.
// 밝기를 알파로 쓰고 색은 밝기로 나눠 되살린다 → 밝은 바닥 위에서도 색이 바래지 않는 일반 반투명 합성.
// 꼬리 그림은 _Wobble 로 꼬리 쪽(uv.x 작은 쪽)이 더 크게 일렁인다. 파티클은 _UseVertexColor 를 켠다.
Shader "Archer/FxSprite"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "black" {}
        [HDR] _TintColor ("Tint", Color) = (1, 1, 1, 1)
        _Intensity ("Intensity", Range(0, 4)) = 1.3
        _AlphaBoost ("Alpha Boost", Range(0.5, 6)) = 2.2
        _Glow ("Glow Add", Range(0, 1)) = 0.25
        [Toggle] _UseVertexColor ("Use Vertex Color (particles)", Float) = 0
        _Wobble ("Wobble", Range(0, 0.2)) = 0
        _WobbleFreq ("Wobble Freq", Float) = 9
        _WobbleSpeed ("Wobble Speed", Float) = 14
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _TintColor;
                half _Intensity;
                half _AlphaBoost;
                half _Glow;
                half _UseVertexColor;
                half _Wobble;
                half _WobbleFreq;
                half _WobbleSpeed;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };

            Varyings vert (Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = lerp(half4(1, 1, 1, 1), v.color, _UseVertexColor);
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                float2 uv = i.uv;
                // 꼬리 쪽일수록 크게 일렁임 (머리 uv.x=1 은 고정)
                half tail = 1 - uv.x;
                uv.y += sin(uv.x * _WobbleFreq - _Time.y * _WobbleSpeed) * _Wobble * tail;
                half3 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).rgb;
                half lum = max(c.r, max(c.g, c.b));
                half a = saturate(lum * _AlphaBoost) * _TintColor.a * i.color.a;
                half3 col = c / max(lum, 1e-3) * _TintColor.rgb * i.color.rgb * _Intensity;
                // 프리멀티플라이 합성 + 밝은 부분은 살짝 더해서 빛나는 느낌
                half3 outRgb = col * a + c * _Glow * i.color.rgb * i.color.a * _TintColor.rgb;
                return half4(outRgb, a);
            }
            ENDHLSL
        }
    }
}
