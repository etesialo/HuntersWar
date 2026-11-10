// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'

// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'

// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'


Shader "WAF/WAF_Character"
{
    Properties
    {
        [Enum(Off,0,On,1)]_UseEffectMapcap("Use EffectMapcap", Float) = 0
        _Color ("Color", Color) = (1,1,1,1)
        _SubColor("Sub Color", Color) = (0.953,0.953,0.953,1)
        [Toggle] _UseCorrection("Use Correction", Range(0,1)) = 0
        _Contrast ("Contrast", Range(1,2)) = 1.13
        _Saturation2 ("Saturation", Range(1,2)) = 1.2
        _Temperature("Temperature", Range(-100, 100)) = -6
        _Tint("Tint", Range(-100, 100)) = 4
        _MainTex("Albedo (RGB)", 2D) = "white" {}
        _MultPower("Multiply Power", Range(1.0, 5.0)) = 2.0

        [Header(MAT CAP)]
        _MatCapWeight("MatCap Weight", range(0.0, 1.0)) = 1
        [NoScaleOffset]_MatCapMask("MatCap Mask", 2D) = "white" {}
        [NoScaleOffset]_MatCapWeightMask("MatCap Weight Mask", 2D) = "white" {}

        _MatCapRColor("MatCap R Color", Color) = (1, 1, 1)
        [NoScaleOffset]_MatCapR("MatCap R", 2D) = "black" {}

        _MatCapGColor("MatCap G Color", Color) = (1, 1, 1)
        [NoScaleOffset]_MatCapG("MatCap G", 2D) = "black" {}

        _MatCapBColor("MatCap B Color", Color) = (1, 1, 1)
        [NoScaleOffset]_MatCapB("MatCap B", 2D) = "black" {}

        _MatCapAColor("MatCap A Color", Color) = (1, 1, 1)
        [NoScaleOffset]_MatCapA("MatCap A", 2D) = "black" {}

        _MatCapEffect("Effect MatCap ", 2D) = "white" {}
        _MatCapEffectColor("Effect MatCap Color", Color) = (1, 1, 1)
        _matcapEffectMask("Effect MatCap Mask", 2D) = "white"{}
        _MatCapEffectIntensity("Effect Mapcap Intensity", Range(1, 10)) = 1
        _MatCapEffectRotate("Effect MapCap  Rot", range(-1, 1)) = 0.0
        _MatCapEffectMoveX("Effect MapCap Move X", Range(-1, 1)) = 0.0
        _MatCapEffectMoveY("Effect MapCap Move Y", Range(-1, 1)) = 0.0
        _MatCapEffectWeight("MatCap Effect Weight", Range(0,1)) = 1

        [Header(RIM LIGHT)]
        _RimColor("Rim Color", Color) = (1, 1, 1)
        _RimPower("Rim Power", Range(0.0,1.0)) = 1.0
        [NoScaleOffset]_RimTex("Rim Tex", 2D) = "black" {}

        [Header(OUTLINE)]
        _OutLineColor("OutLine Color", Color) = (1, 1, 1)
        _OutLineWeight("OutLine Weight", range(0.0, 0.05)) = 0

        [Header(BLEND MODE)]
        [Enum(UnityEngine.Rendering.BlendMode)] _Blend("Blend mode", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _Blend2("Blend mode subset", Float) = 0

        [Header(DISSOLVE)]
        [Toggle(DISSOLVE)] _Dissolve("Dissolve", Float) = 0
        _DissolveProgress("Dissolve Progress",Range(0,1)) = 1
        _Edge("Edge Range",Range(0.01,0.5)) = 0.12

        [Header(COLOR MASK)]
        _ColorMask("Color Mask", 2D) = "black" {}
        _RColor("R Color", Color) = (1,1,1)
        _GColor("G Color", Color) = (1,1,1)
        _BColor("B Color", Color) = (1,1,1)
        
        [Header(MIPMAP BIAS)]
        _MipmapBias("Mipmap bias", range(-1, 1)) = 0

        [Header(DISABLE_ENV)]
        [Toggle(DISABLE_ENV)] _DisableEnv("Disable env", float) = 0
        
    	[Header(Emission)]
        [Toggle]_UseEmission("UseEmission", Float) = 0
        [HDR]_EmissionColor("EmissionColor", Color) = (0,0,0)
        _EmissionMulByBaseColor("EmissionMulByBaseColor", Range(0,1)) = 0
        [NoScaleOffset]_EmissionMap("EmissionMap", 2D) = "white" {}
        _EmissionMapChannelMask("EmissionMapChannelMask", Vector) = (1,1,1,0)
    	
    	//[Header(Curve)]
        //[Toggle]_UseCurve("UseCurve", Float) = 0
    	//_WRadius("WorldRadius", Float) = 1000
    }

    Subshader
    {
	    Pass
        {
        	    	Tags
            {
                "RenderType" = "Opaque" "Queue" = "Geometry+1"
            }
            Blend[_Blend][_Blend2]

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma fragmentoption ARB_precision_hint_fastest
            #pragma multi_compile_fog

            #pragma multi_compile _ _TEAMCOLOR_NONE _TEAMCOLOR_MYTEAM _TEAMCOLOR_ENEMYTEAM
            #pragma multi_compile _ DISSOLVE
            #pragma multi_compile _ DISABLE_ENV
            #pragma multi_compile _ SIMPLESHADER
            #pragma multi_compile _ CUSTOMLIGHT

            #include "UnityCG.cginc"
            #include "HelenCG.cginc"
            #include "WafCG.cginc"

            struct VertexOutput
            {
                float4 pos : SV_POSITION;
                fixed3 normalDir: NORMAL;
                fixed4 col : COLOR;
                fixed2 uv : TEXCOORD0;
                fixed2 cap : TEXCOORD1;
                float3 posWorld : TEXCOORD2;
                float3 normal : TEXCOORD3;
                fixed2 effect : TexCOORD4;
                #if DISSOLVE
					fixed2 dessolve_uv : TEXCOORD5;
                #endif
                UNITY_FOG_COORDS(6)
            };

            uniform sampler2D _MainTex;
            uniform float4 _MainTex_ST;

            uniform sampler2D _MatCapMask;
            uniform sampler2D _MatCapWeightMask;
            uniform sampler2D _MatCapR;
            uniform sampler2D _MatCapG;
            uniform sampler2D _MatCapB;
            uniform sampler2D _MatCapA;
            uniform sampler2D _MatCapEffect;
            uniform float4 _MatCapEffect_ST;
            uniform sampler2D _RimTex;
            uniform sampler2D _matcapEffectMask;

            fixed4 _MatCapRColor;
            fixed4 _MatCapGColor;
            fixed4 _MatCapBColor;
            fixed4 _MatCapAColor;
            float _RimBlend;
            float _RimPower;
            float _MultPower;
            fixed4 _Color;
            fixed4 _SubColor;
            fixed4 _RimColor;
            fixed _MatCapWeight;

            fixed _UseEffectMapcap;
            fixed _UseRotCorrection;
            fixed4 _MatCapEffectColor;
            fixed _MatCapEffectWeight;
            float _MatCapEffectRotate;
            float _MatCapEffectMoveX;
            float _MatCapEffectMoveY;
            float _MatCapEffectIntensity;

            fixed4 _OutLineColor;
            float _OutLinePower;
            float _OutLineWeight;

            float _CloudCharacterIntensicy;

            #if _TEAMCOLOR_MYTEAM
				fixed4 _MyTeamColorRate;
            #elif _TEAMCOLOR_ENEMYTEAM
				fixed4 _EnemyTeamColorRate;
            #elif _TEAMCOLOR_NONE

            #else
            fixed4 _BackGroundColorRate;
            #endif

            #if DISSOLVE
				uniform sampler2D _CharacterDissolveTex;
				uniform float4 _CharacterDissolveTex_ST;
				fixed _DissolveProgress;
				fixed _Edge;
            #endif


            ///////////////////////////////////////////////
            uniform sampler2D _ColorMask;
            fixed4 _RColor;
            fixed4 _GColor;
            fixed4 _BColor;
            ////////////////////////////////////////////////
            fixed _Contrast;
            fixed _Saturation2;
            half _Temperature;
            half _Tint;
            fixed _UseCorrection;

            float _MipmapBias;
            
            fixed   _UseEmission;
            uniform sampler2D _EmissionMap;
		    half3   _EmissionColor;
		    half    _EmissionMulByBaseColor;
			half3   _EmissionMapChannelMask;

            fixed _UseCurve;
            float _WRadius;
            fixed4 SetRimLight(VertexOutput i, fixed4 mc)
            {
                fixed3 normalDirection = i.normalDir;
                fixed3 viewDirection = normalize(_WorldSpaceCameraPos.xyz - i.posWorld.xyz);
                fixed rim = 1 - saturate(dot(viewDirection, normalDirection));
                fixed val = pow(rim, _RimPower);
                fixed4 rim_final = _RimColor * val;
                fixed4 ret = mc + (rim_final * _RimBlend);
                return ret;
            }

            float4 multQuat(float4 q1, float4 q2)
            {
                return float4(
                    q1.w * q2.x + q1.x * q2.w + q1.z * q2.y - q1.y * q2.z,
                    q1.w * q2.y + q1.y * q2.w + q1.x * q2.z - q1.z * q2.x,
                    q1.w * q2.z + q1.z * q2.w + q1.y * q2.x - q1.x * q2.y,
                    q1.w * q2.w - q1.x * q2.x - q1.y * q2.y - q1.z * q2.z
                );
            }

            float4 _curvedOut;

            VertexOutput vert(appdata_full v)
            {
                VertexOutput o;
                UNITY_INITIALIZE_OUTPUT(VertexOutput, o);
                o.posWorld = mul(unity_ObjectToWorld, v.vertex);
            	if(_UseCurve)
            	{
                    CurvePositionZ_float(o.posWorld,  v.vertex, _curvedOut);
                    v.vertex = _curvedOut;
            	}

                o.pos = UnityObjectToClipPos(v.vertex);
                o.col = v.color;

                o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
                float3 worldNorm = normalize(unity_WorldToObject[0].xyz * v.normal.x +
                    unity_WorldToObject[1].xyz * v.normal.y +
                    unity_WorldToObject[2].xyz * v.normal.z);
                o.normal = worldNorm;
                //////////////////////////////////////////////////////////////////

                worldNorm = mul((float4x4)UNITY_MATRIX_V, float4(worldNorm, 0.0)).xyz;
                o.normalDir = worldNorm;

                o.cap.xy = worldNorm.xy;

                o.effect = TRANSFORM_TEX(o.cap, _MatCapEffect);
                #if DISSOLVE
					o.dessolve_uv = TRANSFORM_TEX(v.texcoord, _CharacterDissolveTex);
                #endif
                UNITY_TRANSFER_FOG(o, o.pos);
                ////////////////////////////////////////////////////////
                return o;
            }

            float3 Unity_WhiteBalance_float(float3 In, float Temperature, float Tint)
            {
                // Range ~[-1.67;1.67] works best
                float t1 = Temperature * 10 / 6;
                float t2 = Tint * 10 / 6;

                // Get the CIE xy chromaticity of the reference white point.
                // Note: 0.31271 = x value on the D65 white point
                float x = 0.31271 - t1 * (t1 < 0 ? 0.1 : 0.05);
                float standardIlluminantY = 2.87 * x - 3 * x * x - 0.27509507;
                float y = standardIlluminantY + t2 * 0.05;

                // Calculate the coefficients in the LMS space.
                float3 w1 = float3(0.949237, 1.03542, 1.08728); // D65 white point

                // CIExyToLMS
                float Y = 1;
                float X = Y * x / y;
                float Z = Y * (1 - x - y) / y;
                float L = 0.7328 * X + 0.4296 * Y - 0.1624 * Z;
                float M = -0.7036 * X + 1.6975 * Y + 0.0061 * Z;
                float S = 0.0030 * X + 0.0136 * Y + 0.9834 * Z;
                float3 w2 = float3(L, M, S);

                float3 balance = float3(w1.x / w2.x, w1.y / w2.y, w1.z / w2.z);

                float3x3 LIN_2_LMS_MAT = {
                    3.90405e-1, 5.49941e-1, 8.92632e-3,
                    7.08416e-2, 9.63172e-1, 1.35775e-3,
                    2.31082e-2, 1.28021e-1, 9.36245e-1
                };

                float3x3 LMS_2_LIN_MAT = {
                    2.85847e+0, -1.62879e+0, -2.48910e-2,
                    -2.10182e-1, 1.15820e+0, 3.24281e-4,
                    -4.18120e-2, -1.18169e-1, 1.06867e+0
                };

                float3 lms = mul(LIN_2_LMS_MAT, In);
                lms *= balance;
                return mul(LMS_2_LIN_MAT, lms);
            }
            
            fixed4 frag(VertexOutput i) : COLOR
            {
                #if SIMPLESHADER
					fixed4 simpleColor = tex2D(_MainTex, i.uv);
					return simpleColor;
                #endif

                fixed4 mask = tex2D(_MatCapMask, i.uv);

                //fixed4 tex = tex2D(_MainTex, i.uv);
                half4 tex = tex2Dbias(_MainTex, float4(i.uv, 0, _MipmapBias));

                //WhiteBalance
                float temper = _Temperature * _UseCorrection;
                float tint = _Tint * _UseCorrection;
                tex.rgb = Unity_WhiteBalance_float(tex.rgb, temper / 200, tint / 200);
                // Saturation
                float3 intensity = dot(tex.rgb, float3(0.299, 0.587, 0.114));
                float satu = max(1, _Saturation2 * _UseCorrection);
                tex.rgb = lerp(intensity, tex.rgb, satu);
                // Contrast
                float cont = max(1, _Contrast * _UseCorrection);
                tex = (tex - 0.4135884) * cont + 0.4135884;
                tex = tex * lerp(fixed4(1, 1, 1, 1), _SubColor, _UseCorrection);

                fixed4 weight_mask = tex2D(_MatCapWeightMask, i.uv);
                float matcap_weight = saturate(weight_mask.x + weight_mask.y + weight_mask.z);
                i.cap = (i.cap * 0.5 * matcap_weight) + 0.5;

                fixed4 mcR = tex2D(_MatCapR, i.cap) * mask.x * _MatCapRColor;
                fixed4 mcG = tex2D(_MatCapG, i.cap) * mask.y * _MatCapGColor;
                fixed4 mcB = tex2D(_MatCapB, i.cap) * mask.z * _MatCapBColor;
                fixed4 mcA = tex2D(_MatCapA, i.cap) * mask.w * _MatCapAColor;
                fixed4 mc = (mcR + mcG + mcB + mcA);

                mc = mc * _MatCapWeight + (1.0 - _MatCapWeight);

                //return mc;
                fixed4 rim = tex2D(_RimTex, i.cap);
                rim = rim * _RimColor * _RimPower;

                fixed4 ret = tex;

                ////////////////////////////////////////////////////////////////////
                // Color Mask
                fixed4 color_mask = tex2D(_ColorMask, i.uv);
 				ret *= lerp(fixed4(1, 1, 1, 1), _RColor, color_mask.r);
                ret *= lerp(fixed4(1, 1, 1, 1), _GColor, color_mask.g);
                ret *= lerp(fixed4(1, 1, 1, 1), _BColor, color_mask.b);
            	ret.a = 1;
                ////////////////////////////////////////////////////////////////////

		    	half4 emission = half4(0,0,0,1);
            	if(_UseEmission)
				{
            		emission.rgb = tex2D(_EmissionMap, i.uv).rgb * _EmissionMapChannelMask * _EmissionColor.rgb;
            		emission.rgb = lerp(emission, emission * ret.rgb, _EmissionMulByBaseColor);
				}
            	
                if (_UseEffectMapcap > 0)
                {
                    i.effect = (i.effect * 0.5) + 0.5;
                    fixed2 center = fixed2(0.5, 0.5);
                    fixed2 temp_uv = i.effect - center;
                    fixed2 theta = _Time.x * _MatCapEffectRotate * 100;
                    fixed2 rot_uv;
                    rot_uv.x = temp_uv.x * cos(theta) - temp_uv.y * sin(theta);
                    rot_uv.y = temp_uv.x * sin(theta) + temp_uv.y * cos(theta);
                    rot_uv += center;

                    rot_uv = rot_uv + float2(_Time.x * _MatCapEffectMoveX * 100, _Time.x * _MatCapEffectMoveY * 100);

                    // 임시로 텍스쳐 대신 화이트 칼라로
                    fixed4 effect = tex2D(_MatCapEffect, rot_uv) * _MatCapEffectColor * _MatCapEffectIntensity;
                    //fixed4 effect = fixed4(1, 1, 1, 1) * _MatCapEffectIntensity;

                    fixed4 matCapEffectMask = tex2D(_matcapEffectMask, i.uv);
                    ret = lerp(ret, effect, _MatCapEffectWeight * matCapEffectMask.r);
                }
            	ret = (ret * _MultPower * mc * _Color) + rim + emission;
                //ret += (tex * albedo_mask);

            	//이미션 맵 추가를 위해서 임시로 막아놓음
                //#if DISABLE_ENV
                //#else
                //ret = ApplyLightMask(ret, i.posWorld, 1);
                //ret = ApplyAddLightMask(ret, i.posWorld, 1);
                //ret = ApplyFogAndCloud(ret, i.posWorld, _CloudCharacterIntensicy, 1);
                //#endif

                UNITY_APPLY_FOG(i.fogCoord, ret);
                // #if _TEAMCOLOR_MYTEAM
                // 					ret = ret * _MyTeamColorRate;
                // #elif _TEAMCOLOR_ENEMYTEAM
                // 					ret = ret * _EnemyTeamColorRate;
                // #elif _TEAMCOLOR_NONE
                // #else
                // 					ret = ret * _BackGroundColorRate;
                // #endif

                #if CUSTOMLIGHT
					fixed3 lightColor = GetCustomLight(i.posWorld, i.normal);
					ret.xyz = ret.xyz + lightColor;
                #endif

                #if DISSOLVE
					fixed4 col = tex2D(_CharacterDissolveTex, i.dessolve_uv);
					fixed edge = lerp(col.r + _Edge, col.r - _Edge, _DissolveProgress);
					fixed alpha = smoothstep(_DissolveProgress + _Edge, _DissolveProgress - _Edge, edge);
					ret.a = alpha;
                #endif
                ret.a = saturate(ret.a * _Color.a);

                return ret;
            }
            ENDCG
        }
        
        Blend[_Blend][_Blend2]
		Pass
		{
			Tags {"RenderType" = "Opaque" "Queue" = "Geometry+1" "LightMode" = "OutlineAddPass" }

			Cull Front

			CGPROGRAM

			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile _ DISSOLVE
			#pragma multi_compile _ DISABLE_ENV
			#pragma multi_compile_fog

			#include "UnityCG.cginc"
			#include "HelenCG.cginc"
            #include "WafCG.cginc"

CBUFFER_START(UnitPerMaterial)
			fixed4 _Color;
			fixed4 _OutLineColor;
			float _OutLineWeight;
			float _CloudCharacterIntensicy;
			uniform sampler2D _MainTex;
            uniform float4 _MainTex_ST;
			fixed _UseCurve;
			float _WRadius;
#if DISSOLVE
			uniform sampler2D _CharacterDissolveTex;
			uniform float4 _CharacterDissolveTex_ST;
			fixed _DissolveProgress;
			fixed _Edge;
#endif
CBUFFER_END

			struct vertexOutput
			{
				float4 pos : POSITION;
				float4 color : COLOR;
				float3 posWorld : TEXCOORD0;
#if DISSOLVE
				fixed2 dessolve_uv : TEXCOORD1;
#endif
				fixed2 uv : TEXCOORD2;
				UNITY_FOG_COORDS(3)
			};

            float4 _curvedOut;

			vertexOutput vert(appdata_full v) 
			{
				vertexOutput o;
				UNITY_INITIALIZE_OUTPUT(vertexOutput, o);
				o.posWorld = mul(unity_ObjectToWorld, v.vertex);

				if(_UseCurve)
				{
                    CurvePositionZ_float(o.posWorld, v.vertex, _curvedOut);
                    v.vertex = _curvedOut;
				}				
				//float dist = distance(_WorldSpaceCameraPos, o.posWorld);
				//dist = clamp(dist, 0, 20) * 0.2;

				float3 pos = v.vertex + (v.tangent * _OutLineWeight * v.color.x);
				o.pos = UnityObjectToClipPos(pos);
#if DISSOLVE
				o.dessolve_uv = TRANSFORM_TEX(v.texcoord, _CharacterDissolveTex);
#endif

				o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
				UNITY_TRANSFER_FOG(o, o.pos);

				return o;
			}

			fixed4 frag(vertexOutput i) : COLOR
			{
				fixed4 ret = _OutLineColor * tex2D(_MainTex, i.uv);;
#if DISABLE_ENV
#else
				ret = ApplyLightMask(ret, i.posWorld, 1);
				ret = ApplyFogAndCloud(ret, i.posWorld, _CloudCharacterIntensicy, 1);
#endif
				UNITY_APPLY_FOG(i.fogCoord, ret);

#if DISSOLVE
				fixed4 col = tex2D(_CharacterDissolveTex, i.dessolve_uv);
				fixed edge = lerp(col.r + _Edge, col.r - _Edge, _DissolveProgress);
				fixed alpha = smoothstep(_DissolveProgress + _Edge, _DissolveProgress - _Edge, edge);
				ret.a = alpha;
#endif
				ret.a = saturate(ret.a * _Color.a);
				
				return ret;
			}
			ENDCG
		}
    	Pass
        {
            Tags
            {
                "LightMode" = "ShadowCaster"
            }
            CGPROGRAM
            #pragma vertex VSMain
            #pragma fragment PSMain

            #include "WafCG.cginc"
            
            float4 _curvedOut;
			fixed _UseCurve;
            
            float4 VSMain(float4 vertex:POSITION) : SV_POSITION
            {
            	float3 posWorld = mul(unity_ObjectToWorld, vertex);
				if(_UseCurve)
				{
                    CurvePositionZ_float(posWorld, vertex, _curvedOut);
                    vertex = _curvedOut;
				}
                return UnityObjectToClipPos(vertex);
            }

            float4 PSMain(float4 vertex:SV_POSITION) : SV_TARGET
            {
                return 0;
            }
            ENDCG
        }
    }

    Fallback "Unlit/Texture"
}