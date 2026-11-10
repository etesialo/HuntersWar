// 빛 콘(팡파레): 꼭짓점에서 원뿔 모양으로 빛이 은은하게 퍼진다.
// 메시는 LightCone 이 만든다: uv.x = 둘레(0~1), uv.y = 밑동(0)~끝(1). 모양(높이·반지름)은 여기서 정해서 재질로 조절.
// 오브젝트 +Y 로 넓어짐. 둘레를 따라 빛줄기 결이 천천히 돌고, 끝으로 갈수록 사라짐.
Shader "Archer/LightCone"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (0.35, 1, 0.8, 1)
        _Intensity ("Intensity", Range(0, 4)) = 0.6

        [Header(Shape)]
        _Height ("Height", Range(0.1, 10)) = 1.6
        _BottomRadius ("Bottom Radius (밑동)", Range(0, 3)) = 0.12
        _TopRadius ("Top Radius (끝)", Range(0.05, 10)) = 1.1

        [Header(Rays)]
        _RayCount ("Ray Count", Range(1, 40)) = 14
        _RaySharp ("Ray Sharpness", Range(1, 32)) = 4
        _RayContrast ("Ray Contrast (0 고르게, 1 빛줄기만)", Range(0, 1)) = 0.7
        _RaySpeed ("Ray Rotate Speed", Range(-2, 2)) = 0.1

        [Header(Fade)]
        _FadeBottom ("Fade In Bottom (밑동에서 서서히)", Range(0.01, 1)) = 0.15
        _FadeTop ("Fade Out Top (끝으로 사라지는 곡선)", Range(0.2, 6)) = 1.5
        _EdgeSoft ("Edge Softness (비스듬한 면 흐리게)", Range(0, 4)) = 1
        _Pulse ("Pulse (숨 쉬는 정도)", Range(0, 1)) = 0.25
        _PulseSpeed ("Pulse Speed", Range(0, 5)) = 1.2

        _DepthPull ("Depth Pull (m, 벽에 묻히지 않게)", Range(0, 3)) = 0.3
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }

        Pass
        {
            Name "Cone"
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
                half _Intensity;
                float _Height;
                float _BottomRadius;
                float _TopRadius;
                half _RayCount;
                half _RaySharp;
                half _RayContrast;
                half _RaySpeed;
                half _FadeBottom;
                half _FadeTop;
                half _EdgeSoft;
                half _Pulse;
                half _PulseSpeed;
                float _DepthPull;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half facing : TEXCOORD1; UNITY_VERTEX_INPUT_INSTANCE_ID };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                float a = v.uv.x * TWO_PI;
                float2 dir = float2(cos(a), sin(a));
                float radius = lerp(_BottomRadius, _TopRadius, v.uv.y);
                float3 pos = float3(dir.x * radius, v.uv.y * _Height, dir.y * radius);
                // 옆면 노멀 (기울기 반영) → 비스듬히 보이는 면을 흐리게
                float3 n = normalize(float3(dir.x * _Height, -(_TopRadius - _BottomRadius), dir.y * _Height));
                float3 posWS = TransformObjectToWorld(pos);
                float3 nWS = TransformObjectToWorldNormal(n);
                o.facing = abs(dot(nWS, GetWorldSpaceNormalizeViewDir(posWS)));

                // 카메라 쪽으로 당김: 화면 위치는 그대로, 깊이만 앞으로
                float3 posVS = TransformWorldToView(posWS);
                posVS *= max(0.01, 1.0 - _DepthPull / max(length(posVS), 0.01));
                o.positionCS = TransformWViewToHClip(posVS);
                o.uv = v.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float t = _Time.y;
                // 빛줄기 두 겹 (조금 다른 수·속도로 겹쳐 일렁임)
                float u = i.uv.x * PI;
                half ray1 = pow(abs(sin(u * _RayCount + t * _RaySpeed * 3.0)), _RaySharp);
                half ray2 = pow(abs(sin(u * (_RayCount + 3.0) - t * _RaySpeed * 2.0 + 1.3)), _RaySharp);
                half rays = lerp(1.0h, ray1 * 0.6h + ray2 * 0.4h, _RayContrast);

                // 밑동에서 서서히 나타나고 끝으로 갈수록 사라짐
                half v = i.uv.y;
                half fade = smoothstep(0.0h, _FadeBottom, v) * pow(saturate(1.0h - v), _FadeTop);
                half edge = pow(saturate(i.facing), _EdgeSoft);
                half pulse = 1.0h + sin(t * _PulseSpeed) * _Pulse;

                return half4(_Color.rgb * rays * fade * edge * pulse * _Intensity, 0);
            }
            ENDHLSL
        }
    }
}
