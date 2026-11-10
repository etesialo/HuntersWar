// 물속 작은 물고기: 위에서 내려다본 물고기 실루엣이 영역 안을 유영한다.
// 메시는 FishSchool 이 만든다: 물고기 하나 = 쿼드 하나 (오브젝트 XZ 평면).
//   uv0 = 쿼드 모서리(x: 폭 -0.5~0.5, y: 꼬리 -0.5 ~ 머리 0.5), uv1 = 물고기별 난수 4개
// 경로는 리사주 곡선(가로·세로 다른 박자의 사인)이라 영역 밖으로 안 나가고, 머리는 가는 방향을 향한다.
// 물(Transparent-10)은 깊은 곳을 거의 불투명하게 칠해서 그 아래는 안 보인다.
// 그래서 물 다음에 그리되, 옅은 물속 그림자처럼 (낮은 불투명도 + 물빛 어두운 색) 그린다.
// 값은 FishSchool 이 MaterialPropertyBlock 으로 넣는다 (오브젝트마다 다르게)
Shader "Archer/FishSchool"
{
    Properties
    {
        _Color ("Color", Color) = (0.1, 0.25, 0.3, 1)
        _Opacity ("Opacity", Float) = 0.35
        _Size ("Size (m, 몸 길이)", Float) = 0.35
        _SizeVar ("Size Variation", Float) = 0.3
        _AreaX ("Area X (m)", Float) = 2
        _AreaZ ("Area Z (m)", Float) = 2
        _Speed ("Speed", Float) = 0.4
        _Wiggle ("Wiggle (꼬리 흔들림)", Float) = 0.25
        _WiggleSpeed ("Wiggle Speed", Float) = 8
        _Feather ("Edge Feather", Float) = 0.3
        _Depth ("Depth (m, 오브젝트보다 아래)", Float) = 0.15
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent-5" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }

        Pass
        {
            Name "Fish"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Opacity;
                float _Size;
                float _SizeVar;
                float _AreaX;
                float _AreaZ;
                float _Speed;
                half _Wiggle;
                half _WiggleSpeed;
                half _Feather;
                float _Depth;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 corner : TEXCOORD0; float4 rnd : TEXCOORD1; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 corner : TEXCOORD0; half wig : TEXCOORD1; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float4 r = v.rnd;
                float t = _Time.y * _Speed;
                // 리사주 경로: 가로·세로 박자가 달라 이리저리 돎. 반지름은 영역 안쪽 85%
                float2 R = float2(_AreaX, _AreaZ) * 0.5 * 0.85;
                float wa = (0.6 + r.x * 0.8) / max(R.x, 0.05);
                float wb = (0.6 + r.y * 0.8) / max(R.y, 0.05);
                float pa = r.z * 6.2831, pb = r.w * 6.2831;
                // 가끔 빨라졌다 느려졌다 (같은 경로를 따라 시간만 늘였다 줄임)
                float tt = t + sin(t * (0.7 + r.x) + pa) * 0.25;
                float2 p = float2(sin(tt * wa + pa) * R.x, sin(tt * wb + pb) * R.y);
                float2 vel = float2(cos(tt * wa + pa) * R.x * wa, cos(tt * wb + pb) * R.y * wb);
                float2 fwd = normalize(vel + float2(1e-4, 0));
                float2 side = float2(fwd.y, -fwd.x);

                float size = _Size * (1.0 + (r.y * 2.0 - 1.0) * _SizeVar);
                // 쿼드: 길이 = size, 폭 = size * 0.5
                float2 local = p + side * (v.corner.x * size * 0.5) + fwd * (v.corner.y * size);
                float3 pos = float3(local.x, -_Depth, local.y);
                o.positionCS = TransformObjectToHClip(pos);
                o.corner = v.corner;
                o.wig = sin(_Time.y * _WiggleSpeed * (0.8 + r.z * 0.4) + pa);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                // 물고기 좌표: x 폭(-0.5~0.5, 폭은 길이의 절반이라 2배해서 비율 맞춤), y 꼬리(-0.5)~머리(0.5)
                float y = i.corner.y;
                // 꼬리 쪽일수록 좌우로 휘어짐
                float bend = i.wig * _Wiggle * saturate(0.25 - y) * saturate(0.25 - y);
                float x = i.corner.x * 0.5 - bend;

                // 몸통: 머리 쪽이 둥근 타원
                float2 b = float2(x / 0.16, (y - 0.08) / 0.34);
                half body = 1.0h - smoothstep(1.0h - _Feather, 1.0h, length(b));
                // 꼬리: 몸통 뒤 삼각형 (뒤로 갈수록 넓어짐)
                float ty = saturate((-0.22 - y) / 0.26);                 // 꼬리 시작 0 → 끝 1
                float tw = 0.03 + ty * 0.15;
                half tail = (1.0h - smoothstep(tw * (1.0h - _Feather), tw, abs(x)))
                          * step(y, -0.2) * (1.0h - smoothstep(0.85h, 1.0h, ty));
                half a = saturate(max(body, tail)) * _Opacity;
                return half4(_Color.rgb, a);
            }
            ENDHLSL
        }
    }
}
