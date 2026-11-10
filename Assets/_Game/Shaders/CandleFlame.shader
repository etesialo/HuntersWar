// 촛불 불꽃: 물방울 모양 하나 (노란 속불 + 주황 겉불 + 은은한 빛무리). 텍스처 없이 그린다.
// 쿼드(Unity 기본 Quad)를 카메라를 향해 세우고, 오브젝트 위치 = 불꽃 밑동(심지 끝).
// 끝이 살랑살랑 흔들리고 높이가 미세하게 깜빡인다. 촛불마다 위치로 박자가 다름.
Shader "Archer/CandleFlame"
{
    Properties
    {
        _InnerColor ("Inner Color (속불)", Color) = (1, 0.97, 0.75, 1)
        _OuterColor ("Outer Color (겉불)", Color) = (1, 0.55, 0.12, 1)
        _GlowColor ("Glow Color (빛무리)", Color) = (1, 0.6, 0.2, 1)
        _Brightness ("Brightness", Range(0, 3)) = 1.3
        _Size ("Size (m, 불꽃 높이)", Range(0.02, 1)) = 0.16
        _Width ("Width (불꽃 굵기)", Range(0.1, 0.8)) = 0.38
        _Core ("Core Size (속불 크기)", Range(0, 1)) = 0.55
        _Glow ("Glow Strength", Range(0, 1)) = 0.35
        _GlowSize ("Glow Size", Range(0.5, 3)) = 1.6
        _Sway ("Sway (흔들림)", Range(0, 0.5)) = 0.12
        _SwaySpeed ("Sway Speed", Range(0, 10)) = 3
        _Flicker ("Flicker (깜빡임)", Range(0, 0.5)) = 0.1
        _Feather ("Feather (가장자리 흐림)", Range(0.05, 1)) = 0.6
        _Wobble ("Wobble (불꽃 결 일렁임)", Range(0, 0.4)) = 0.08
        _Opacity ("Opacity", Range(0, 1)) = 0.9
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }

        Pass
        {
            Name "Flame"
            Tags { "LightMode"="UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _InnerColor;
                half4 _OuterColor;
                half4 _GlowColor;
                half _Brightness;
                half _Size;
                half _Width;
                half _Core;
                half _Glow;
                half _GlowSize;
                half _Sway;
                half _SwaySpeed;
                half _Flicker;
                half _Feather;
                half _Wobble;
                half _Opacity;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 anim : TEXCOORD1; UNITY_VERTEX_INPUT_INSTANCE_ID };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                float3 pivot = TransformObjectToWorld(float3(0, 0, 0));
                float scale = length(float3(unity_ObjectToWorld._m00, unity_ObjectToWorld._m10, unity_ObjectToWorld._m20));
                float seed = frac(sin(dot(pivot.xz, float2(12.9898, 78.233))) * 43758.5453) * 6.28;
                float t = _Time.y * _SwaySpeed + seed;
                float sway = sin(t) * 0.6 + sin(t * 2.3 + 1.7) * 0.4;           // -1~1 불규칙한 흔들림
                float flicker = 1.0 + (sin(t * 3.1) * 0.5 + sin(t * 5.7 + 0.5) * 0.5) * _Flicker;

                // 불꽃 단위(1 = 불꽃 높이)로 쿼드를 빛무리 크기(G)만큼 넓게: x -G/2~G/2, y -m~G-m (밑동 = 0)
                float S = _Size * scale;
                float G = _GlowSize, m = (G - 1.0) * 0.35;
                float3 right = UNITY_MATRIX_V[0].xyz;
                float3 up = UNITY_MATRIX_V[1].xyz;
                float2 q = v.positionOS.xy;                         // -0.5 ~ 0.5
                float3 w = pivot + right * (q.x * G * S) + up * (((q.y + 0.5) * G - m) * S);
                o.positionCS = TransformWorldToHClip(w);
                o.uv = v.uv;
                o.anim = float3(sway * _Sway, flicker, _Time.y + seed);   // z: 불꽃 결 일렁임용 시간
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                // 쿼드 uv → 불꽃 좌표 (밑동 y=0, 끝 y=1, 가운데 x=0). vert 와 같은 G, m
                float G = _GlowSize, m = (G - 1.0) * 0.35;
                float fy = (i.uv.y * G - m) / i.anim.y;              // 깜빡임: 높이가 살짝 늘었다 줄었다
                float fx = (i.uv.x - 0.5) * G - i.anim.x * fy * fy;  // 끝으로 갈수록 더 흔들림

                // 불꽃 결이 위로 흐르며 좌우로 일렁임 (위로 갈수록 크게)
                float t = i.anim.z;
                fx += (sin(fy * 7.0 - t * 9.0) * 0.5 + sin(fy * 13.0 - t * 14.0 + 1.3) * 0.3) * _Wobble * saturate(fy);

                // 불꽃 폭: 아래는 둥글고 위는 가늘게 (y 0~1). 경계는 Feather 만큼 안팎으로 흐리게
                float tY = saturate(fy);
                float halfW = _Width * 0.5 * sin(PI * pow(tY, 0.55)) + 1e-3;
                float d = abs(fx) / halfW;
                half side = 1.0h - smoothstep(1.0h - _Feather, 1.0h + _Feather * 0.5h, d);
                // 밑동은 살짝, 끝은 길게 흐려지며 사라짐
                half vert = smoothstep(-0.08h, 0.15h, fy) * (1.0h - smoothstep(0.45h, 1.05h, fy));
                half body = side * vert;

                // 속불: 아래쪽에 작게, 역시 흐리게
                float cy = fy / max(_Core, 0.05);
                float cHalfW = _Width * 0.5 * _Core * sin(PI * pow(saturate(cy), 0.6)) + 1e-3;
                half inner = (1.0h - smoothstep(1.0h - _Feather, 1.0h + _Feather * 0.5h, abs(fx) / cHalfW))
                           * smoothstep(-0.05h, 0.12h, cy) * (1.0h - smoothstep(0.4h, 1.0h, cy));

                // 빛무리: 불꽃 가운데쯤을 중심으로 둥글게
                float2 g = float2(fx, fy - 0.35) / (0.5 * G);
                half glow = saturate(1.0 - length(g));
                glow = glow * glow * _Glow;

                half3 flame = lerp(_OuterColor.rgb, _InnerColor.rgb, inner) * _Brightness;
                half alpha = body * _Opacity;
                // 미리 곱한 알파: 불꽃은 반투명하게 덮고, 빛무리는 더하기만
                return half4(flame * alpha + _GlowColor.rgb * glow, alpha * 0.85h);
            }
            ENDHLSL
        }
    }
}
