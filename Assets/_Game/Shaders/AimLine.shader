// 적 공격 예고선: 바닥에 눕힌 쿼드 (uv.x = 폭, uv.y = 길이 방향).
// 가운데가 진하고 양옆으로 부드럽게 흐려지는 반투명 빨간 페더 줄. 빛이 발사 방향으로 흐른다.
// _Progress(0~1, 스크립트가 MaterialPropertyBlock 으로 넣음): 조준이 끝나갈수록 진하고 좁아진다.
Shader "Archer/AimLine"
{
    Properties
    {
        _Color ("Color", Color) = (1, 0.12, 0.08, 0.55)
        _CoreColor ("Core Color", Color) = (1, 0.75, 0.6, 1)
        _CoreAmount ("Core Amount", Range(0, 1)) = 0.35
        _Feather ("Feather Softness", Range(0.5, 10)) = 3
        _EndFade ("End Fade", Range(0, 1)) = 0.7
        _FlowSpeed ("Flow Speed", Float) = 6
        _FlowSpacing ("Flow Spacing", Float) = 1.6
        _FlowStrength ("Flow Strength", Range(0, 1)) = 0.35
        _Progress ("Progress", Range(0, 1)) = 0
        _Length ("Length", Float) = 10
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

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _CoreColor;
                half _CoreAmount;
                half _Feather;
                half _EndFade;
                half _FlowSpeed;
                half _FlowSpacing;
                half _FlowStrength;
                half _Progress;
                half _Length;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert (Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                half p = saturate(_Progress);
                // 폭 방향: 선명한 띠 (가장자리는 계단만 안 지게 아주 살짝). 조준이 끝나갈수록 좁아짐
                half x = abs(i.uv.x * 2 - 1) / lerp(1.0, 0.6, p);
                half aa = max(fwidth(x) * 1.5, 0.02);
                half soft = 1 - smoothstep(1 - aa, 1, x);
                half core = 1 - smoothstep(0.25 - aa, 0.25, x); // 가운데 밝은 심선

                // 길이 방향: 시작은 살짝 페이드 인, 끝으로 갈수록 옅어지고 끝은 부드럽게 닫힘
                half y = i.uv.y;
                half along = smoothstep(0.0, 0.05, y) * (1 - _EndFade * y) * smoothstep(1.0, 0.85, y);

                // 발사 방향으로 흐르는 빛 띠 (월드 길이 기준이라 줄 길이와 무관하게 간격 일정)
                half d = y * _Length / max(_FlowSpacing, 0.01) - _Time.y * _FlowSpeed / max(_FlowSpacing, 0.01);
                half flow = 1 + _FlowStrength * (sin(d * 6.2831853) * 0.5 + 0.5) - _FlowStrength * 0.5;

                half intensity = lerp(0.6, 1.25, p);
                half a = saturate(_Color.a * soft * along * flow * intensity);
                half3 col = lerp(_Color.rgb, _CoreColor.rgb, core * _CoreAmount * (0.5 + p));
                return half4(col, a);
            }
            ENDHLSL
        }
    }
}
