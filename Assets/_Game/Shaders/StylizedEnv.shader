// 궁수의전설2 배경 느낌의 부드러운 스타일라이즈드 셰이더 (URP).
// - 빛/어두운 면 경계를 부드러운 램프로 (Half Lambert + smoothstep)
// - 어두운 면 / 드리운 그림자 색을 따로 (검정이 아니라 색으로 물든다)
// - 앰비언트는 하늘/땅 두 색, 구운 방은 라이트맵(구운 AO 포함)이 대신함
// - 덩어리감: 위를 향한 면 밝게, 바닥 쪽 그라데이션, 림, 넓고 약한 하이라이트
Shader "Archer/StylizedEnv"
{
    Properties
    {
        [Header(Base)]
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Color", Color) = (1,1,1,1)
        [Toggle(_NORMALMAP)] _UseNormal ("Use Normal Map", Float) = 0
        [Normal] _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Strength", Range(0, 3)) = 1
        _BumpShade ("Normal Shading (램프와 별개로 굴곡 명암)", Range(0, 3)) = 1

        [Header(Cast Shadow)]
        _CastShadowColor ("Cast Shadow Color (그림자 안 = 텍스처 x 이 색, 빛 설정과 무관)", Color) = (0.6, 0.59, 0.65, 1)
        _CastShadowStrength ("Cast Shadow Strength (1=그림자색 그대로, 1 넘으면 더 진하게)", Range(0, 4)) = 1
        _ReceiveShadowSoft ("Cast Shadow Lighten (그림자 옅게)", Range(0, 1)) = 0.35

        [Header(Light)]
        _RampThreshold ("Light/Dark Border", Range(0, 1)) = 0.45
        _RampSmooth ("Border Softness", Range(0.001, 1)) = 0.35
        _LightIntensity ("Lit Brightness", Range(0, 2)) = 1
        _ShadowColor ("Dark Side Color (빛을 등진 면)", Color) = (0.55, 0.5, 0.78, 1)
        _ShadowStrength ("Dark Side Strength", Range(0, 1)) = 0.75

        [Header(Ambient and Baked Light)]
        _SkyColor ("Ambient Top (sky)", Color) = (0.82, 0.86, 1, 1)
        _GroundColor ("Ambient Bottom (ground)", Color) = (0.55, 0.5, 0.62, 1)
        _AmbientStrength ("Ambient Strength", Range(0, 1.5)) = 0.55
        _BakedGIStrength ("Baked Light Strength (라이트맵 세기)", Range(0, 3)) = 1
        _BakedAmbientMix ("Keep Ambient In Baked Room (구운 방에서도 하늘/땅 색 유지)", Range(0, 1)) = 0.2
        [Toggle(_VERTEX_GI)] _VertexGI ("Baked Light In Vertex Color (구멍 단면용)", Float) = 0

        [Header(Shape)]
        _SoftNormal ("Soft Normals (0 원래, 1 완전 부드럽게)", Range(0, 1)) = 0
        _TopLight ("Up-facing Brighten", Range(0, 1)) = 0.15
        _GradientColor ("Bottom Darken Color", Color) = (0.45, 0.4, 0.6, 1)
        _GradientHeight ("Bottom Darken Height (m)", Range(0.01, 5)) = 0.8
        _GradientStrength ("Bottom Darken Strength", Range(0, 1)) = 0

        [Header(Rim and Highlight)]
        _RimColor ("Rim Color", Color) = (1, 0.95, 0.9, 1)
        _RimPower ("Rim Width", Range(0.5, 8)) = 3
        _RimStrength ("Rim Strength", Range(0, 1)) = 0.2
        _SpecColor ("Highlight Color", Color) = (1, 1, 1, 1)
        _Gloss ("Highlight Size", Range(0.01, 1)) = 0.25
        _SpecStrength ("Highlight Strength", Range(0, 1)) = 0.08

        [Header(Floor)]
        [Toggle(_FLOOR_HOLES)] _FloorHoles ("Cut Floor Holes (바닥 전용)", Float) = 0

        [Header(Render)]
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        LOD 300

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _BumpScale;
            half _BumpShade;
            half _RampThreshold;
            half _RampSmooth;
            half _LightIntensity;
            half4 _ShadowColor;
            half _ShadowStrength;
            half4 _CastShadowColor;
            half _CastShadowStrength;
            half _ReceiveShadowSoft;
            half4 _SkyColor;
            half4 _GroundColor;
            half _AmbientStrength;
            half _BakedGIStrength;
            half _BakedAmbientMix;
            half _SoftNormal;
            half _TopLight;
            half4 _GradientColor;
            half _GradientHeight;
            half _GradientStrength;
            half4 _RimColor;
            half _RimPower;
            half _RimStrength;
            half4 _SpecColor;
            half _Gloss;
            half _SpecStrength;
        CBUFFER_END

        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);

        // 방 구멍 마스크 (FloorHoles 가 전역으로 넣어줌). rect = (minX, minZ, 1/width, 1/length)
        TEXTURE2D(_RoomHoleMask); SAMPLER(sampler_RoomHoleMask);
        float4 _RoomHoleRect;
        // 메인 라이트(디렉셔널)를 배경에 안 비추기 (ShadowOnlyLight 가 전역으로 넣어줌). 0 = 평소, 1 = 그림자만 받음
        half _EnvMainLightOff;

        void HoleClip(float3 positionWS)
        {
            #if defined(_FLOOR_HOLES)
            float2 uv = (positionWS.xz - _RoomHoleRect.xy) * _RoomHoleRect.zw;
            half m = SAMPLE_TEXTURE2D(_RoomHoleMask, sampler_RoomHoleMask, uv).r;
            clip(0.5h - m);
            #endif
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            #pragma shader_feature_local _FLOOR_HOLES
            #pragma shader_feature_local _VERTEX_GI
            #pragma shader_feature_local _NORMALMAP
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP _FORWARD_PLUS
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma multi_compile _ LIGHTMAP_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
                float2 lightmapUV : TEXCOORD1;
                float4 smoothN    : TEXCOORD3; // xyz 부드러운 노멀 (SmoothNormalBaker 가 구움)
                half4  color      : COLOR;     // 구멍 단면: 구운 빛 (HoleWallBaker)
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3  normalWS   : TEXCOORD2;
                half   fogFactor  : TEXCOORD3;
                float  objectY    : TEXCOORD4; // 오브젝트 바닥 기준 높이 (그라데이션용)
                #if defined(LIGHTMAP_ON)
                float2 lightmapUV : TEXCOORD5;
                #endif
                #if defined(_VERTEX_GI)
                half3 vertexGI    : TEXCOORD6;
                #endif
                #if defined(_NORMALMAP)
                half4 tangentWS   : TEXCOORD7; // w: 바이탄젠트 방향
                #endif
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                VertexPositionInputs pos = GetVertexPositionInputs(v.positionOS.xyz);
                // 꺾인 면을 부드럽게: 같은 자리 꼭짓점들의 평균 노멀. 구워둔 값이 없으면 원래 노멀
                float3 target = dot(v.smoothN.xyz, v.smoothN.xyz) > 0.01 ? normalize(v.smoothN.xyz) : v.normalOS;
                float3 softN = normalize(lerp(v.normalOS, target, _SoftNormal));
                o.positionCS = pos.positionCS;
                o.positionWS = pos.positionWS;
                o.normalWS = TransformObjectToWorldNormal(softN);
                #if defined(_NORMALMAP)
                o.tangentWS = half4(TransformObjectToWorldDir(v.tangentOS.xyz), v.tangentOS.w * GetOddNegativeScale());
                #endif
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.fogFactor = ComputeFogFactor(pos.positionCS.z);
                // 월드 높이에서 오브젝트 원점 높이를 빼서 '바닥에서 얼마나 위인지'
                o.objectY = pos.positionWS.y - TransformObjectToWorld(float3(0, 0, 0)).y;
                #if defined(LIGHTMAP_ON)
                o.lightmapUV = v.lightmapUV * unity_LightmapST.xy + unity_LightmapST.zw;
                #endif
                #if defined(_VERTEX_GI)
                o.vertexGI = v.color.rgb;
                #endif
                return o;
            }

            // 부드러운 램프: 경계 위치(threshold)와 폭(smooth)
            half Ramp(half x)
            {
                return smoothstep(_RampThreshold - _RampSmooth * 0.5h, _RampThreshold + _RampSmooth * 0.5h, x);
            }

            half3 StylizedLight(Light light, half3 albedo, half3 n, half3 viewDir, bool isMain)
            {
                half ndl = dot(n, light.direction);
                // 보조 광원(포인트 등): 라이트맵 굽기와 같은 물리 계산 (램프 없이 N·L x 거리 감쇠 한 번)
                // → 굽기 전 씬 미리보기 밝기가 구운 결과와 비슷하게 (반사광만 빠짐)
                if (!isMain) return albedo * light.color * (saturate(ndl) * light.distanceAttenuation * light.shadowAttenuation);
                half halfLambert = ndl * 0.5h + 0.5h;
                // 면 방향만으로 밝은 쪽/어두운 쪽 (Dark Side Color 는 여기에만 쓰임)
                half faceLit = Ramp(halfLambert) * light.distanceAttenuation;
                // 받는 그림자 (Lighten 만큼 옅게)
                half shadow = lerp(light.shadowAttenuation, 1.0h, _ReceiveShadowSoft * (1.0h - light.shadowAttenuation) * 0.5h);
                half lit = faceLit * shadow;

                half3 darkTint = lerp(half3(1, 1, 1), _ShadowColor.rgb, _ShadowStrength);
                half3 diffuse = albedo * lerp(darkTint, half3(1, 1, 1) * _LightIntensity, faceLit) * light.color;

                // 넓고 약한 하이라이트
                half3 h = SafeNormalize(light.direction + viewDir);
                half spec = pow(saturate(dot(n, h)), lerp(8.0h, 128.0h, 1.0h - _Gloss));
                spec = smoothstep(0.4h, 0.6h, spec) * _SpecStrength * lit;
                half3 specular = spec * _SpecColor.rgb * light.color;

                return diffuse + specular;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                HoleClip(i.positionWS);
                half3 albedo = (SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor).rgb;
                half3 n = normalize(i.normalWS);
                half3 nGeom = n;   // 노말맵 적용 전 (굴곡 명암 비교용)
                #if defined(_NORMALMAP)
                half3 nTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, i.uv), _BumpScale);
                half3 bitangent = i.tangentWS.w * cross(n, i.tangentWS.xyz);
                n = normalize(TransformTangentToWorld(nTS, half3x3(i.tangentWS.xyz, bitangent, n)));
                #endif
                half3 viewDir = GetWorldSpaceNormalizeViewDir(i.positionWS);

                Light mainLight = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half3 color = StylizedLight(mainLight, albedo, n, viewDir, true) * (1.0h - _EnvMainLightOff);

                // 보조 광원 (Forward / Forward+ 모두)
                #if defined(_ADDITIONAL_LIGHTS)
                InputData inputData = (InputData)0;
                inputData.positionWS = i.positionWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                uint pixelLightCount = GetAdditionalLightsCount();
                #if USE_CLUSTER_LIGHT_LOOP || USE_FORWARD_PLUS
                for (uint li = 0; li < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); li++)
                {
                    Light l = GetAdditionalLight(li, i.positionWS, half4(1, 1, 1, 1));
                    color += StylizedLight(l, albedo, n, viewDir, false);
                }
                #endif
                LIGHT_LOOP_BEGIN(pixelLightCount)
                    Light l = GetAdditionalLight(lightIndex, i.positionWS, half4(1, 1, 1, 1));
                    color += StylizedLight(l, albedo, n, viewDir, false);
                LIGHT_LOOP_END
                #endif

                // 앰비언트: 하늘/땅 두 색 (위를 향할수록 하늘색)
                half3 ambient = lerp(_GroundColor.rgb, _SkyColor.rgb, n.y * 0.5h + 0.5h) * _AmbientStrength;
                #if defined(LIGHTMAP_ON)
                // 구운 방: 라이트맵(구운 포인트 라이트·간접광·AO)이 앰비언트를 대신함
                ambient = SampleLightmap(i.lightmapUV, float2(0, 0), n) * _BakedGIStrength + ambient * _BakedAmbientMix;
                #elif defined(_VERTEX_GI)
                // 굽기 후 꼭짓점 색에 옮겨 담은 구운 빛 (부드럽게 보간돼서 얼룩 없음)
                ambient = i.vertexGI * _BakedGIStrength + ambient * _BakedAmbientMix;
                #endif
                color += albedo * ambient;

                // 드리운 그림자: 빛 설정(밝기·램프·앰비언트·라이트맵)과 무관하게 '텍스처 x 그림자색' 으로 바꿔 칠한다
                // Strength 0~1 = 섞는 양, 1 넘으면 그림자색을 거듭 곱해 더 진하게
                // 빛을 등진 면은 원래 어두우므로 빛 쪽을 향한 면에만 (램프 설정과 무관한 고정 기준)
                half att = mainLight.shadowAttenuation;
                half towardLight = saturate(dot(n, mainLight.direction) * 4.0h);
                half castMask = (1.0h - lerp(att, 1.0h, _ReceiveShadowSoft * (1.0h - att) * 0.5h)) * towardLight;
                half3 shadowed = albedo * pow(max(_CastShadowColor.rgb, 1e-3h), max(_CastShadowStrength, 1.0h));
                color = lerp(color, shadowed, castMask * saturate(_CastShadowStrength));

                #if defined(_NORMALMAP)
                // 굴곡 명암: 램프가 딱 끊겨도 보이게, 노말맵이 빛 쪽/반대쪽으로 기울인 만큼 직접 밝게/어둡게
                // 빛이 거의 수직이면 기울기 차이가 안 나서, 화면 왼쪽 위(-X, +Z)에서 비추는 성분을 섞음
                half3 bumpDir = normalize(mainLight.direction + half3(-0.6h, 0.0h, 0.6h));
                half bumpLight = dot(n, bumpDir) - dot(nGeom, bumpDir);
                color *= max(0.0h, 1.0h + bumpLight * _BumpShade);
                #endif

                // 덩어리감: 위를 향한 면 밝게 + 바닥 쪽 어둡게
                color *= 1.0h + saturate(n.y) * _TopLight;
                half g = 1.0h - saturate(i.objectY / _GradientHeight);
                color = lerp(color, color * _GradientColor.rgb, g * _GradientStrength);

                // 림
                half rim = pow(1.0h - saturate(dot(n, viewDir)), _RimPower) * _RimStrength;
                color += rim * _RimColor.rgb * (0.5h + 0.5h * mainLight.shadowAttenuation);

                color = MixFog(color, i.fogFactor);
                return half4(color, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vertShadow
            #pragma fragment fragShadow
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma shader_feature_local _FLOOR_HOLES

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct VShadow { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; };

            VShadow vertShadow(Attributes v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(v.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDir = normalize(_LightPosition - positionWS);
                #else
                float3 lightDir = _LightDirection;
                #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDir));
                #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                VShadow o; o.positionCS = positionCS; o.positionWS = positionWS;
                return o;
            }

            half4 fragShadow(VShadow i) : SV_Target { HoleClip(i.positionWS); return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vertDepth
            #pragma fragment fragDepth
            #pragma multi_compile_instancing
            #pragma shader_feature_local _FLOOR_HOLES

            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct VDepth { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; };

            VDepth vertDepth(Attributes v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                VDepth o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                return o;
            }
            half fragDepth(VDepth i) : SV_Target { HoleClip(i.positionWS); return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vertDN
            #pragma fragment fragDN
            #pragma multi_compile_instancing
            #pragma shader_feature_local _FLOOR_HOLES

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; half3 normalWS : TEXCOORD0; float3 positionWS : TEXCOORD1; };

            Varyings vertDN(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }
            half4 fragDN(Varyings i) : SV_Target { HoleClip(i.positionWS); return half4(NormalizeNormalPerPixel(i.normalWS), 0); }
            ENDHLSL
        }

        // 라이트 굽기용: 표면 색(albedo)을 라이트매퍼에 알려준다
        Pass
        {
            Name "Meta"
            Tags { "LightMode"="Meta" }
            Cull Off

            HLSLPROGRAM
            #pragma vertex vertMeta
            #pragma fragment fragMeta
            #pragma shader_feature EDITOR_VISUALIZATION
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MetaInput.hlsl"

            struct AttributesMeta
            {
                float4 positionOS : POSITION;
                float2 uv0 : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
                float2 uv2 : TEXCOORD2;
            };
            struct VMeta
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                #ifdef EDITOR_VISUALIZATION
                float2 VizUV : TEXCOORD1;
                float4 LightCoord : TEXCOORD2;
                #endif
            };

            VMeta vertMeta(AttributesMeta v)
            {
                VMeta o = (VMeta)0;
                o.positionCS = UnityMetaVertexPosition(v.positionOS.xyz, v.uv1, v.uv2);
                o.uv = TRANSFORM_TEX(v.uv0, _BaseMap);
                #ifdef EDITOR_VISUALIZATION
                UnityEditorVizData(v.positionOS.xyz, v.uv0, v.uv1, v.uv2, o.VizUV, o.LightCoord);
                #endif
                return o;
            }

            half4 fragMeta(VMeta i) : SV_Target
            {
                MetaInput meta = (MetaInput)0;
                meta.Albedo = (SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor).rgb;
                #ifdef EDITOR_VISUALIZATION
                meta.VizUV = i.VizUV;
                meta.LightCoord = i.LightCoord;
                #endif
                return UnityMetaFragment(meta);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
