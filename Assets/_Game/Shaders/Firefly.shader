// 반딧불: 동그랗게 빛나는 글로우들이 아래에서 위로 천천히 올라가며 사라진다.
// 글로우마다 속도·크기·흔들림 박자가 조금씩 다름. 움직임은 전부 셰이더에서 (CPU 부담 없음).
// 메시는 FireflyField 가 만든다: 글로우 하나 = 쿼드 하나.
//   positionOS.xz = 바닥 자리, uv0 = 쿼드 모서리(-0.5~0.5), uv1 = 글로우별 난수 3개
// 값은 FireflyField 가 MaterialPropertyBlock 으로 넣는다 (인스턴스마다 다르게)
Shader "Archer/Firefly"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (1, 0.85, 0.4, 1)
        _Intensity ("Intensity", Float) = 1.5
        _Speed ("Rise Speed (m/s)", Float) = 0.4
        _SpeedVar ("Speed Variation", Float) = 0.4
        _Size ("Size (m)", Float) = 0.15
        _SizeVar ("Size Variation", Float) = 0.3
        _Height ("Height (m)", Float) = 3
        _FadeBottom ("Fade In Range (아래, 0~1)", Float) = 0.2
        _FadeTop ("Fade Out Range (위, 0~1)", Float) = 0.35
        _Sway ("Sway (m)", Float) = 0.1
        _Twinkle ("Twinkle", Float) = 0.3
        _Feather ("Edge Feather", Float) = 0.6
        _CenterBright ("Center Brightness", Float) = 0.3
        _HaloSize ("Halo Size (원 크기의 몇 배까지 번지나)", Float) = 2.5
        _HaloStrength ("Halo Strength", Float) = 0.25
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }

        Pass
        {
            Name "Firefly"
            Tags { "LightMode"="UniversalForward" }
            Blend One One   // 더하기: 겹치면 더 밝게
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Intensity;
                float _Speed;
                float _SpeedVar;
                float _Size;
                float _SizeVar;
                float _Height;
                half _FadeBottom;
                half _FadeTop;
                float _Sway;
                half _Twinkle;
                half _Feather;
                half _CenterBright;
                float _HaloSize;
                half _HaloStrength;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 corner : TEXCOORD0; float4 rnd : TEXCOORD1; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 corner : TEXCOORD0; half alpha : TEXCOORD1; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float seed = v.rnd.x, r1 = v.rnd.y, r2 = v.rnd.z;
                float t = _Time.y;
                // 글로우마다 속도 다르게. 높이를 넘으면 바닥에서 다시 (frac)
                float speed = _Speed * (1.0 + (r1 * 2.0 - 1.0) * _SpeedVar);
                float h = max(_Height, 0.01);
                float y01 = frac(seed + t * speed / h);
                // 살랑살랑 좌우로
                float ph = seed * 6.2831;
                float3 center = float3(v.positionOS.x + sin(t * (0.6 + r2) + ph) * _Sway,
                                       y01 * h,
                                       v.positionOS.z + cos(t * (0.5 + r1) + ph) * _Sway * 0.5);
                float3 centerWS = TransformObjectToWorld(center);

                // 아래에서 서서히 나타나고 위에서 서서히 사라짐
                half fade = smoothstep(0.0h, max(_FadeBottom, 1e-3h), y01) * (1.0h - smoothstep(1.0h - max(_FadeTop, 1e-3h), 1.0h, y01));
                half twinkle = lerp(1.0h, 0.55h + 0.45h * sin(t * (2.0 + r2 * 3.0) + ph * 3.0), _Twinkle);

                // 쿼드는 빛무리까지 담게 원보다 HaloSize 배 크게
                float size = _Size * (1.0 + (r2 * 2.0 - 1.0) * _SizeVar) * max(_HaloSize, 1.0);
                float3 right = UNITY_MATRIX_V[0].xyz, up = UNITY_MATRIX_V[1].xyz;
                float3 w = centerWS + (right * v.corner.x + up * v.corner.y) * size;
                o.positionCS = TransformWorldToHClip(w);
                o.corner = v.corner;
                o.alpha = fade * twinkle;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half q = length(i.corner) * 2.0h;               // 쿼드 가운데 0, 가장자리 1
                half d = q * max(_HaloSize, 1.0h);              // 원 기준: 원 가장자리 1
                // 꽉 찬 원 + 가장자리만 부드럽게 (Feather: 0 = 또렷한 원, 1 = 가운데부터 흐려짐)
                half circle = 1.0h - smoothstep(1.0h - max(_Feather, 0.02h), 1.0h, d);
                // 가운데를 살짝 더 밝게
                half center = 1.0h + saturate(1.0h - d) * _CenterBright;
                // 바깥으로 은은하게 번지는 빛무리 (쿼드 가장자리에서 0)
                half halo = saturate(1.0h - q); halo = pow(halo, 1.6h) * _HaloStrength;
                half3 col = _Color.rgb * _Intensity * (circle * center + halo);
                return half4(col * i.alpha, 0);
            }
            ENDHLSL
        }
    }
}
