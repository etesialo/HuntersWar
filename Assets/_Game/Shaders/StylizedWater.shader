// 바닥 구멍 아래 물 (궁수의전설2 느낌).
// - 밝은 청록 + 잔잔한 물결로 색이 부드럽게 흔들림
// - 자잘한 흰 반짝임(깜빡임)
// - 은은한 코스틱(물빛 그물 무늬)
// - 구멍 테두리/벽 그림자를 색 있는 그림자로 받아서 가장자리에 진한 띠
// - 다른 메시와 맞닿는 접합면에 물 경계선 (카메라 깊이 텍스처 사용 → 불투명 다음에 그림)
Shader "Archer/StylizedWater"
{
    Properties
    {
        [Header(Color)]
        _ShallowColor ("Bright Color", Color) = (0.30, 0.98, 0.78, 1)
        _DeepColor ("Dark Color (멀티플라이: 물결 어두운 곳에 곱함)", Color) = (0.2, 0.63, 0.7, 1)
        _ShadowColor ("Shadow Color (멀티플라이: 그림자에 곱함)", Color) = (0.07, 0.31, 0.44, 1)
        _ShadowStrength ("Shadow Strength", Range(0, 1)) = 0.85
        _Brightness ("Brightness", Range(0.5, 2)) = 1.05

        [Header(Waves)]
        [Toggle(_WAVES)] _UseWaves ("Use Waves (끄면 Bright Color 단색)", Float) = 1
        _WaveScale ("Wave Scale", Range(0.1, 4)) = 0.9
        _WaveSpeed ("Wave Speed", Range(0, 2)) = 0.25
        _WaveContrast ("Wave Contrast", Range(0, 1)) = 0.55

        [Header(Caustics)]
        [Toggle(_CAUSTICS)] _UseCaustics ("Use Caustics (물빛 그물 무늬)", Float) = 1
        _CausticColor ("Caustic Color", Color) = (0.75, 1, 0.92, 1)
        _CausticScale ("Caustic Scale", Range(0.5, 8)) = 2.6
        _CausticSpeed ("Caustic Speed", Range(0, 2)) = 0.3
        _CausticStrength ("Caustic Strength", Range(0, 1)) = 0.22
        _CausticWidth ("Caustic Line Width", Range(0.01, 0.5)) = 0.12

        [Header(Sparkles)]
        _SparkleColor ("Sparkle Color", Color) = (1, 1, 1, 1)
        _SparkleDensity ("Sparkle Density (per m)", Range(0.5, 20)) = 4
        _SparkleSize ("Sparkle Radius (m)", Range(0.005, 0.25)) = 0.035
        _SparkleAmount ("Sparkle Amount", Range(0, 1)) = 0.35
        _SparkleSpeed ("Sparkle Twinkle Speed", Range(0, 6)) = 2.2
        _SparkleStrength ("Sparkle Strength", Range(0, 3)) = 1.2

        [Header(Shoreline)]
        _EdgeColor ("Edge Line Color", Color) = (0.85, 1, 0.95, 1)
        _EdgeWidth ("Edge Line Width (m)", Range(0.005, 0.5)) = 0.08
        _EdgeStrength ("Edge Line Strength", Range(0, 2)) = 1
        _EdgeWobble ("Edge Line Wobble", Range(0, 1)) = 0.4
        _EdgeBandColor ("Edge Band Color", Color) = (0.03, 0.42, 0.42, 1)
        _EdgeBandWidth ("Edge Band Width (m)", Range(0.01, 2)) = 0.45
        _EdgeBandStrength ("Edge Band Strength", Range(0, 1)) = 0.55

        [Header(Clarity)]
        _SubmergedColor ("Submerged Color (얕게 잠긴 곳이 빛나는 색)", Color) = (0.55, 1, 0.85, 1)
        _SubmergedBrightness ("Submerged Strength (오버레이 세기)", Range(0, 2)) = 1
        _Clarity ("Clarity (원래 색이 비치는 정도, 0=안 비침)", Range(0, 1)) = 0.15
        _VisibleDepth ("Visible Depth (m, 이 깊이까지 비쳐 보임)", Range(0.05, 3)) = 0.6
    }
    SubShader
    {
        // 깊이 텍스처를 읽어야 해서 불투명 물체가 다 그려진 뒤에 그린다 (출력은 불투명)
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-10" }
        Pass
        {
            Name "Water"
            Tags { "LightMode"="UniversalForward" }
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma shader_feature_local _WAVES
            #pragma shader_feature_local _CAUSTICS
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor, _DeepColor, _ShadowColor;
                half _ShadowStrength, _Brightness;
                half _WaveScale, _WaveSpeed, _WaveContrast;
                half4 _CausticColor;
                half _CausticScale, _CausticSpeed, _CausticStrength, _CausticWidth;
                half4 _SparkleColor;
                half _SparkleDensity, _SparkleSize, _SparkleAmount, _SparkleSpeed, _SparkleStrength;
                half4 _EdgeColor, _EdgeBandColor;
                half _EdgeWidth, _EdgeStrength, _EdgeWobble, _EdgeBandWidth, _EdgeBandStrength;
                half _Clarity, _VisibleDepth;
                half4 _SubmergedColor;
                half _SubmergedBrightness;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; half fog : TEXCOORD1; };

            float2 Hash2(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
                return frac(sin(p) * 43758.5453);
            }

            // 부드러운 값 노이즈
            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = Hash2(i).x, b = Hash2(i + float2(1, 0)).x, c = Hash2(i + float2(0, 1)).x, d = Hash2(i + float2(1, 1)).x;
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            // 셀(보로노이) 경계까지 거리 → 코스틱 그물
            float VoronoiEdge(float2 p, float t)
            {
                float2 i = floor(p), f = frac(p);
                float d1 = 8.0, d2 = 8.0;
                [unroll] for (int y = -1; y <= 1; y++)
                [unroll] for (int x = -1; x <= 1; x++)
                {
                    float2 g = float2(x, y);
                    float2 o = Hash2(i + g);
                    o = 0.5 + 0.45 * sin(t + 6.2831 * o);
                    float d = length(g + o - f);
                    if (d < d1) { d2 = d1; d1 = d; } else if (d < d2) { d2 = d; }
                }
                return d2 - d1;
            }

            Varyings vert(Attributes v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 p = i.positionWS.xz;
                float t = _Time.y;

                // 물결: 두 겹 노이즈를 서로 다른 방향으로 흘림
                float2 wp = p * _WaveScale;
                #if defined(_WAVES)
                float n1 = Noise(wp + float2(t, t * 0.6) * _WaveSpeed);
                float n2 = Noise(wp * 1.7 - float2(t * 0.7, -t) * _WaveSpeed + 3.1);
                half wave = saturate(lerp(0.5, (n1 + n2) * 0.5, 1.0 + _WaveContrast));
                #else
                half wave = 1.0h; // 물결 없음: Bright Color 단색
                #endif
                // Dark Color 는 멀티플라이: Bright Color 에 곱해서 어두운 물결을 만든다
                half3 col = lerp(_ShallowColor.rgb * _DeepColor.rgb, _ShallowColor.rgb, wave);

                #if defined(_CAUSTICS)
                // 코스틱 그물 (부드러운 밝은 선)
                float edge = VoronoiEdge(p * _CausticScale, t * _CausticSpeed * 3.0);
                half caustic = 1.0 - smoothstep(0.0, _CausticWidth, edge);
                col = lerp(col, _CausticColor.rgb, caustic * _CausticStrength * (0.6 + 0.4 * wave));
                #endif

                // 반짝임: 격자(Density = 1m 당 칸 수)마다 점 하나, Amount 비율만 켜짐.
                // Size 는 점 하나의 반지름(m). 이웃 칸 점까지 봐서 커져도 칸 경계에 잘리지 않는다.
                float2 sp = p * _SparkleDensity;
                float2 baseCell = floor(sp);
                half sparkle = 0;
                [unroll] for (int sy = -1; sy <= 1; sy++)
                [unroll] for (int sx = -1; sx <= 1; sx++)
                {
                    float2 cell = baseCell + float2(sx, sy);
                    float2 rnd = Hash2(cell + 17.0);
                    float2 center = cell + 0.15 + rnd * 0.7;
                    float distM = length(sp - center) / _SparkleDensity; // 미터
                    half twinkle = pow(saturate(sin(t * _SparkleSpeed + rnd.x * 6.2831) * 0.5 + 0.5), 6.0);
                    half on = step(1.0 - _SparkleAmount, rnd.y);
                    half spot = 1.0 - smoothstep(_SparkleSize * 0.35, _SparkleSize, distM);
                    sparkle = max(sparkle, spot * twinkle * on);
                }

                // 그림자 (구멍 벽·테두리) → 색 있는 진한 띠
                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half sh = mainLight.shadowAttenuation;
                // Shadow Color 는 멀티플라이: 그림자 진 곳의 물색에 그대로 곱한다
                col = lerp(col, col * _ShadowColor.rgb, (1.0 - sh) * _ShadowStrength);
                sparkle *= sh; // 그늘에선 반짝임 없음

                // 접합면: 물 표면과 그 뒤 물체 사이 거리(m)가 가까울수록 경계
                float2 suv = GetNormalizedScreenSpaceUV(i.positionCS);
                float sceneEye = LinearEyeDepth(SampleSceneDepth(suv), _ZBufferParams);
                float surfEye = LinearEyeDepth(i.positionCS.z, _ZBufferParams);
                float gap = max(0.0, sceneEye - surfEye);
                // 시선이 비스듬하니 거리 차를 수직 거리에 가깝게 보정
                float3 viewDir = normalize(GetWorldSpaceViewDir(i.positionWS));
                gap *= saturate(abs(viewDir.y)) + 0.05;
                half wob = (Noise(p * 6.0 + t * 0.8) - 0.5) * _EdgeWobble * _EdgeWidth * 2.0;
                half band = 1.0 - smoothstep(_EdgeWidth, _EdgeWidth + _EdgeBandWidth, gap);
                half edgeLine = 1.0 - smoothstep(_EdgeWidth * 0.5 + wob, _EdgeWidth + wob, gap);
                col = lerp(col, _EdgeBandColor.rgb, band * _EdgeBandStrength);
                sparkle *= 1.0 - band;

                col = col * _Brightness + _SparkleColor.rgb * sparkle * _SparkleStrength;
                col = lerp(col, _EdgeColor.rgb * 1.1, saturate(edgeLine * _EdgeStrength));
                col = MixFog(col, i.fog);
                // 얕게 잠긴 곳: 물이 밝은 민트색으로 빛나 보임 (잠긴 메시 모양대로 밝아짐). 깊을수록 원래 물색
                half shallow = 1.0h - saturate(gap / _VisibleDepth);
                shallow = shallow * shallow * (3.0h - 2.0h * shallow); // 부드럽게
                // 오버레이 합성: 물결·무늬는 살리고 그 위에 밝은 색을 얹음 (포토샵 Overlay)
                half3 o = _SubmergedColor.rgb;
                half3 overlay = col < 0.5h ? 2.0h * col * o : 1.0h - 2.0h * (1.0h - col) * (1.0h - o);
                col = lerp(col, overlay, saturate(shallow * _SubmergedBrightness) * (1.0h - saturate(edgeLine * _EdgeStrength)));
                // 원래 색은 Clarity 만큼만 살짝 비침 (물 아래 아무것도 없으면 불투명)
                half alpha = lerp(1.0h - _Clarity, 1.0h, 1.0h - shallow);
                alpha = max(alpha, saturate(edgeLine * _EdgeStrength)); // 경계선은 또렷하게
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
}
