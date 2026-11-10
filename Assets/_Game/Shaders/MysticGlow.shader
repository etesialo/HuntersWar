// 영험한 빛: 둥근 링 여러 겹이 실제로 층을 이뤄, 가운데에서 생겨나 방사형으로 퍼지며 위로 올라가 사라진다.
// 메시는 MysticRings 가 만든다: 링 한 겹 = 쿼드 하나 (오브젝트 XZ 평면에 눕힘).
//   uv0 = 쿼드 모서리(-0.5~0.5), uv1.x = 이 링의 시작 시점(0~1), uv1.y = 1 이면 가운데 빛(움직이지 않음)
// 링은 오브젝트 +Y 로 올라간다 → 오브젝트를 돌리면 그 방향으로 퍼지며 뻗어 나감.
// 크기·높이는 오브젝트 단위 (스케일을 따라감).
Shader "Archer/MysticGlow"
{
    Properties
    {
        [HDR] _Color ("Ring Color", Color) = (0.35, 1, 0.8, 1)
        [HDR] _CoreColor ("Core Color (가운데 빛)", Color) = (0.7, 1, 0.9, 1)
        _Intensity ("Intensity", Range(0, 4)) = 1

        [Header(Rings)]
        _Speed ("Speed (초당 링 생성 주기)", Range(0, 3)) = 0.35
        _StartSize ("Start Size (생겨나는 지름)", Range(0, 3)) = 0.2
        _EndSize ("End Size (사라질 때 지름)", Range(0.1, 6)) = 1.6
        _Rise ("Rise (올라가는 높이)", Range(0, 5)) = 0.8
        _RiseCurve ("Rise Curve (1 고르게, 작을수록 처음에 빨리 올라감)", Range(0.3, 3)) = 0.7
        _RingWidth ("Ring Width", Range(0.01, 0.5)) = 0.08
        _RingSoft ("Ring Softness (0 또렷, 1 흐릿)", Range(0, 1)) = 0.8
        _InnerFill ("Inner Fill (링 안쪽 은은하게 채움)", Range(0, 1)) = 0.15
        _FadeIn ("Fade In (생겨날 때 서서히, 0~1)", Range(0.01, 1)) = 0.15
        _FadeOut ("Fade Out (퍼지며 사라지는 곡선)", Range(0.2, 6)) = 1.5

        [Header(Core)]
        _CoreStrength ("Core Strength", Range(0, 2)) = 0.4
        _CoreSize ("Core Size (지름)", Range(0.05, 3)) = 0.6
        _Pulse ("Core Pulse", Range(0, 1)) = 0.3

        _DepthPull ("Depth Pull (m, 벽에 묻히지 않게)", Range(0, 3)) = 0.3
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }

        Pass
        {
            Name "Rings"
            Tags { "LightMode"="UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _CoreColor;
                half _Intensity;
                half _Speed;
                float _StartSize;
                float _EndSize;
                float _Rise;
                half _RiseCurve;
                half _RingWidth;
                half _RingSoft;
                half _InnerFill;
                half _FadeIn;
                half _FadeOut;
                half _CoreStrength;
                float _CoreSize;
                half _Pulse;
                float _DepthPull;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 corner : TEXCOORD0; float2 layer : TEXCOORD1; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 corner : TEXCOORD0; half2 data : TEXCOORD1; UNITY_VERTEX_INPUT_INSTANCE_ID };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                bool isCore = v.layer.y > 0.5;
                // 링마다 시작 시점이 고르게 어긋나 있어서 하나씩 계속 생겨남
                float phase = isCore ? 0 : frac(_Time.y * _Speed + v.layer.x);
                float size = isCore ? _CoreSize : lerp(_StartSize, _EndSize, phase);
                float height = isCore ? 0 : pow(phase, _RiseCurve) * _Rise;
                float3 pos = float3(v.corner.x * size, height, v.corner.y * size);

                // 카메라 쪽으로 당김: 화면 위치는 그대로, 깊이만 앞으로
                float3 posVS = TransformWorldToView(TransformObjectToWorld(pos));
                posVS *= max(0.01, 1.0 - _DepthPull / max(length(posVS), 0.01));
                o.positionCS = TransformWViewToHClip(posVS);
                o.corner = v.corner;
                half fade = smoothstep(0.0h, _FadeIn, phase) * pow(1.0h - phase, _FadeOut);
                o.data = half2(isCore ? 1.0h : fade, isCore ? 1.0h : 0.0h);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                half r = length(i.corner) * 2.0h;   // 가운데 0, 쿼드 가장자리 1
                if (i.data.y > 0.5h)
                {
                    // 가운데 빛 (숨 쉬듯)
                    half core = saturate(1.0h - r); core *= core;
                    core *= _CoreStrength * (1.0h + sin(_Time.y * 2.0) * _Pulse);
                    return half4(_CoreColor.rgb * core * _Intensity, 0);
                }
                // 링: 쿼드 가장자리 안쪽에 둥근 띠 + 안쪽 은은한 채움
                half ringR = 1.0h - _RingWidth;
                half d = abs(r - ringR);
                half ring = 1.0h - smoothstep(_RingWidth * (1.0h - _RingSoft), _RingWidth, d);
                half fill = saturate(r / ringR) * step(r, ringR) * _InnerFill;
                return half4(_Color.rgb * (ring + fill * fill) * i.data.x * _Intensity, 0);
            }
            ENDHLSL
        }
    }
}
