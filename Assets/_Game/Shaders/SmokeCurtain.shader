// 출구 연기 장막: 바닥에서 피어오르는 보라색 연기 커튼 (방을 깨기 전까지 출구를 막음).
// 텍스처 없이 노이즈를 위로 흘려서 만든다.
// 두 번 그린다:
//   1) Tint  : 뒤 배경에 보라색을 곱한다 (2배 곱하기 → 1 이하 = 곱하기로 어둡게, 1 넘으면 오버레이처럼 밝게). 뒤가 비쳐 보임
//   2) Glow  : 바닥 쪽 자홍 빛과 연기 결을 더한다 (빛나는 부분)
// 쿼드 UV: x = 가로(0~1), y = 바닥(0) → 위(1)
Shader "Archer/SmokeCurtain"
{
    Properties
    {
        [Header(Tint Multiply)]
        _TintColor ("Tint Color (1 이하 곱하기, 1 넘으면 밝게)", Color) = (0.55, 0.3, 0.85, 1)
        _TintStrength ("Tint Strength", Range(0, 2)) = 1

        [Header(Glow Add)]
        _BottomColor ("Bottom Glow Color (바닥 쪽 빛)", Color) = (1, 0.25, 0.85, 1)
        _Intensity ("Glow Intensity", Range(0, 4)) = 1.3
        _GlowWidth ("Glow Width (좌우 영역, 1=전체 폭)", Range(0, 1)) = 1
        _GlowSideSoft ("Glow Side Softness (좌우 경계 부드럽게)", Range(0.01, 1)) = 0.3
        _GlowHeight ("Glow Height (아래에서 얼마나 올라오나)", Range(0.05, 1)) = 0.3
        _SmokeColor ("Smoke Glow Color (연기 결 빛)", Color) = (0.45, 0.15, 0.7, 1)
        _SmokeGlow ("Smoke Glow", Range(0, 2)) = 0.3

        [Header(Shape)]
        _Opacity ("Opacity (1 넘으면 속이 꽉 참)", Range(0, 2)) = 1.4
        _Height ("Fade Height (위로 사라지는 정도)", Range(0.2, 3)) = 1.1
        _EdgeFade ("Side Fade (좌우 가장자리)", Range(0.01, 0.5)) = 0.15
        _NoiseScale ("Smoke Scale (연기 덩어리 크기)", Range(0.5, 10)) = 3
        _Stretch ("Smoke Stretch (세로로 길게)", Range(0.2, 4)) = 1.8
        _Speed ("Rise Speed", Range(0, 3)) = 0.6
        _Wisp ("Wisp Amount (연기 들쭉날쭉)", Range(0, 1)) = 0.6
        _DepthPull ("Depth Pull (m, 뒤 계단·바닥에 묻히지 않게 화면 위치는 그대로 두고 앞으로)", Range(0, 5)) = 1.5
        _Fade ("Fade (스크립트가 0으로 걷음)", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _TintColor;
            half _TintStrength;
            half4 _BottomColor;
            half _Intensity;
            half _GlowWidth;
            half _GlowSideSoft;
            half _GlowHeight;
            half4 _SmokeColor;
            half _SmokeGlow;
            half _Opacity;
            half _Height;
            half _EdgeFade;
            half _NoiseScale;
            half _Stretch;
            half _Speed;
            half _Wisp;
            half _DepthPull;
            half _Fade;
        CBUFFER_END

        struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
        struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float2 seed : TEXCOORD1; UNITY_VERTEX_INPUT_INSTANCE_ID };

        Varyings vert(Attributes v)
        {
            Varyings o;
            UNITY_SETUP_INSTANCE_ID(v);
            UNITY_TRANSFER_INSTANCE_ID(v, o);
            // 카메라 쪽으로 당김: 시선 방향으로만 줄여서 화면 위치는 그대로
            float3 posVS = TransformWorldToView(TransformObjectToWorld(v.positionOS.xyz));
            posVS *= max(0.01, 1.0 - _DepthPull / max(length(posVS), 0.01));
            o.positionCS = TransformWViewToHClip(posVS);
            o.uv = v.uv;
            // 장막마다 무늬가 다르게 (월드 위치로)
            float3 w = TransformObjectToWorld(float3(0, 0, 0));
            o.seed = w.xz * 0.37;
            return o;
        }

        float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
        float Noise(float2 p)
        {
            float2 i = floor(p), f = frac(p);
            f = f * f * (3.0 - 2.0 * f);
            return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), f.x),
                        lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), f.x), f.y);
        }

        // 두 패스가 같이 쓰는 모양: 연기 무늬(smoke), 장막 농도(alpha), 바닥 글로우 영역(glow)
        void Shape(Varyings i, out half smoke, out half alpha, out half glow)
        {
            float t = _Time.y * _Speed;
            // 세로로 늘인 좌표에서 노이즈를 위로 흘림 (두 겹 + 서로 비틀기)
            float2 p = float2(i.uv.x * _NoiseScale, i.uv.y * _NoiseScale / _Stretch) + i.seed;
            float n1 = Noise(p + float2(0, -t));
            float n2 = Noise(p * 2.1 + float2(n1 * 0.8, -t * 1.7));
            smoke = saturate(n1 * 0.6 + n2 * 0.5);

            // 위쪽에서만 사라짐 (아래는 꽉 찬 장막). 연기 무늬만큼 윗선이 들쭉날쭉
            half height = i.uv.y / _Height + (smoke - 0.5h) * _Wisp;
            half top = 1.0h - smoothstep(0.35h, 1.0h, height);
            // 좌우 가장자리 부드럽게
            half side = smoothstep(0.0h, _EdgeFade, i.uv.x) * smoothstep(0.0h, _EdgeFade, 1.0h - i.uv.x);
            alpha = saturate(top * side * lerp(0.8h, 1.2h, smoke) * _Opacity) * _Fade;

            // 글로우 영역: 아래에서 GlowHeight 까지, 가운데에서 좌우 GlowWidth 만큼
            half glowV = 1.0h - smoothstep(0.0h, _GlowHeight, i.uv.y);
            half halfW = _GlowWidth * 0.5h;
            half glowH = 1.0h - smoothstep(halfW - _GlowSideSoft * halfW, halfW + 1e-3h, abs(i.uv.x - 0.5h));
            glow = glowV * glowH;
        }
        ENDHLSL

        // 1) 뒤 배경에 곱하기. 결과 = 2 x 출력 x 배경 → 출력 0.5 = 그대로, 0.5 x TintColor = TintColor 곱하기
        Pass
        {
            Name "Tint"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend DstColor SrcColor
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment fragTint
            #pragma multi_compile_instancing

            half4 fragTint(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                half smoke, alpha, glow;
                Shape(i, smoke, alpha, glow);
                half3 tint = _TintColor.rgb * lerp(0.85h, 1.15h, smoke);
                half3 m = lerp(half3(1, 1, 1), tint, saturate(alpha * _TintStrength));
                return half4(m * 0.5h, 1);
            }
            ENDHLSL
        }

        // 2) 빛 더하기: 바닥 쪽 자홍 + 연기 결
        Pass
        {
            Name "Glow"
            Tags { "LightMode"="UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment fragGlow
            #pragma multi_compile_instancing

            half4 fragGlow(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                half smoke, alpha, glow;
                Shape(i, smoke, alpha, glow);
                half3 col = _BottomColor.rgb * _Intensity * glow * lerp(0.75h, 1.25h, smoke)
                          + _SmokeColor.rgb * _SmokeGlow * smoke * smoke;
                return half4(col * alpha, 0);
            }
            ENDHLSL
        }
    }
}
