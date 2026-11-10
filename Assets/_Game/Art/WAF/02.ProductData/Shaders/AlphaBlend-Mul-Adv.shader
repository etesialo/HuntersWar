// Unity built-in shader source. Copyright (c) 2016 Unity Technologies. MIT license (see license.txt)

// Simplified Additive Particle shader. Differences from regular Additive Particle one:
// - no Smooth particle support
// - no AlphaTest
// - no ColorMask

Shader "Helen/Alpha/AlphaBlend-Mul-Adv"
{

	Properties
	{
		//[Enum(Off,0,On,1)]_UseGlobalRate("Use Global Rate", Float) = 1
		[HDR]_TintColor("Tint Color", Color) = (1,1,1,1)
		_MainTex("Particle Texture", 2D) = "white" {}
		_DistortionTex("Distortion Texture", 2D) = "black" {}
		_ColorStrength("Color strength", Range(1,5)) = 1.0
		_ColorMask("Color Mask", Float) = 15
		[Toggle]_ModifyAlpha("Modify Alpha", Float) = 0
		[IntRange] _Stencil("Stencil ID", Range(0, 255)) = 0
		[Enum(UnityEngine.Rendering.CompareFunction)] _StencilComp("Stencil Comparison", Float) = 0
		[Enum(UnityEngine.Rendering.StencilOp)] _StencilOp("Stencil Operation", Float) = 0
		_StencilWriteMask("Stencil Write Mask", Float) = 255
		_StencilReadMask("Stencil Read Mask", Float) = 255

		_MainTexMoveU("Move MainTex U", Float) = 0
		_MainTexMoveV("Move MainTex V", Float) = 0
		_MainTexRotUV("Rot MainTex", Float) = 0
		_MainTexRotAnchorU("Rot MainTex AnchorU", Range(0.0, 1.0)) = 0.5
		_MainTexRotAnchorV("Rot MainTex AnchorV", Range(0.0, 1.0)) = 0.5

		_DistortionTexMoveU("Move DistortionTex U", Float) = 0
		_DistortionTexMoveV("Move DistortionTex V", Float) = 0
		_DistortionTexRotUV("Rot DistortionTex", Float) = 0
		_DistortionRotAnchorU("Rot DistortionTex AnchorU", Range(0.0, 1.0)) = 0.5
		_DistortionRotAnchorV("Rot DistortionTex AnchorV", Range(0.0, 1.0)) = 0.5
		_DistortionStrength("Distortion Strength", Range(-1, 1)) = 0
		[Toggle]_UseDistortionAnchor("Use Distortion Anchor", Float) = 0

		[Header(DISSOLVE)]
		_DissolveProgress("Dissolve Progress",Range(0,1)) = 1
		[Toggle(DISSOLVE_EDGE)] _DissolveEdge("Edge Color", Float) = 0
		_Edge("Edge Range",Range(0.01,0.5)) = 0.12
		_EdgeAround("Edge Color Range",Range(0,0.5)) = 0.05
		_EdgeAroundPower("Edge Color Power",Range(0.1,5)) = 1.9
		_EdgeAroundHDR("Edge Color HDR",Range(1,3)) = 1.45
		_DissolveTex("Dissolve Texture", 2D) = "white" {}
		[NoScaleOffset] _EdgeAroundRamp("Edge Ramp", 2D) = "gray" {}

		//[Header(VERTEX DISPLACEMENT)]
  //      _DisplacementTexture("VertexDisplacementTexture", 2D) = "black" {}
  //      _DisplacementWeight("VertexDisplacement Weight", Range(0, 10)) = 0
  //      _MovementX("MovementX", Range(0,1)) = 0
  //      _MovementY("MovementY", Range(0,1)) = 0
		//[Toggle]_OnlyY("Only Y", float) = 0

		//[Header(FLOW MAP)]
		//_FlowTex("Flow Texture", 2D) = "black" {}
		//_FlowIntensity("Flow Intensity", Range(0, 5)) = 0
		//_FlowSpeed("Flow Speed", Range(0,5)) = 1

		[Header(CUSTOM DATA)]
		[Toggle] _UseCustomData("Use Custom Data", Float) = 0

		[Header(CULL MODE)]
		[Enum(None,0,Front,1,Back,2)] _CullMode("Cull Mode", Float) = 0
		[Header(ZWRITE)]
		[Toggle] _ZWriteMode("ZWrite", Float) = 0
		[Header(ZTEST)]
		[Enum(UnityEngine.Rendering.CompareFunction)] _ZTestMode("ZTest", Float) = 4
		[Header(BLEND MODE)]
		[Enum(UnityEngine.Rendering.BlendMode)] _Blend1("Blend mode", Float) = 5
		[Enum(UnityEngine.Rendering.BlendMode)] _Blend2("Blend mode subset", Float) = 10
		//[Header(ENV)]
		//_EnvWeight("Fog and Cloud Weight", Range(0,1)) = 0
		//[Header(LightMask)]
		//_EnalbeLightMask("Enable LightMask", Range(0,2)) = 0

		//[Header(DISABLE_ENV)]
		//[Toggle(DISABLE_ENV)] _DisableEnv("Disable env", float) = 0

		//[Header(CAMERA WEIGHT)]
		//[Toggle]_EnableCamWeight("Enable Camera Weight", Float) = 0
		//_CamWeight("Camera Weight", Range(1,100)) = 5
		//_CamDistMax("Max Cam Distance", Range(1, 500)) = 100
		
		[Header(Emission)]
        [Toggle]_UseEmission("UseEmission", Float) = 0
        [HDR]_EmissionColor("EmissionColor", Color) = (0,0,0)
        _EmissionMulByBaseColor("EmissionMulByBaseColor", Range(0,1)) = 0
        [NoScaleOffset]_EmissionMap("EmissionMap", 2D) = "white" {}
        //_EmissionMapChannelMask("EmissionMapChannelMask", Vector) = (1,1,1,0)
	}

	Category
	{
		Tags{ "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" }
		Blend[_Blend1][_Blend2]
		Cull[_CullMode] 
		ZWrite[_ZWriteMode]
		ZTest[_ZTestMode]
		Lighting Off 

	SubShader
	{
	Stencil
	{
		Ref[_Stencil]
		Comp[_StencilComp]
		Pass[_StencilOp]
		ReadMask[_StencilReadMask]
		WriteMask[_StencilWriteMask]
	}
	ColorMask[_ColorMask]
	Pass
	{
		CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#include "UnityCG.cginc"
#include "HelenCG.cginc"
	#pragma multi_compile _ DISSOLVE_EDGE
	#pragma multi_compile _ DISABLE_ENV

	sampler2D _MainTex;
	sampler2D _DistortionTex;
	half4 _TintColor;
	half _ColorStrength;
	half _ModifyAlpha;

	float _MainTexMoveU;
	float _MainTexMoveV;
	float _MainTexRotUV;
	half _MainTexRotAnchorU;
	half _MainTexRotAnchorV;

	float _DistortionTexMoveU;
	float _DistortionTexMoveV;
	float _DistortionTexRotUV;
	half _DistortionRotAnchorU;
	half _DistortionRotAnchorV;
	float _DistortionStrength;
	float _UseDistortionAnchor;

	uniform sampler2D _DissolveTex;
	uniform float4 _DissolveTex_ST;
	half _DissolveProgress;
	half _Edge;
	uniform sampler2D _EdgeAroundRamp;
	half _EdgeAround;
	float _EdgeAroundPower;
	float _EdgeAroundHDR;

	float _UseCustomData;

	//fixed _UseGlobalRate;
	half4 _BackGroundColorRate;

	//uniform sampler2D _DisplacementTexture;
	//float _DisplacementWeight;
    //float _MovementX;
    //float _MovementY;
	//float _OnlyY;

	//sampler2D _FlowTex;
	//float _FlowIntensity;
	//float _FlowSpeed;

	//float _EnvWeight;

	//float _EnalbeLightMask;

	//float _EnableCamWeight;
	//float _CamWeight;
	//float _CamDistMax;
		
	half   _UseEmission;
	uniform sampler2D _EmissionMap;
	half3   _EmissionColor;
	half    _EmissionMulByBaseColor;
	//half3   _EmissionMapChannelMask;
		

	struct appdata_t {
		float4 vertex : POSITION;
		half3 normal : NORMAL;
		half4 color : COLOR;
		float4 texcoord : TEXCOORD0;
		float4 customdata1: TEXCOORD1;
	};

	struct v2f {
		float4 vertex : SV_POSITION;
		half4 color : COLOR;
		float2 texcoord : TEXCOORD0;
		float2 texcoord2 : TEXCOORD1;
		half2 dessolve_uv : TEXCOORD2;
		float4 customdata1 : TEXCOORD3;
		float3 world_pos: TEXCOORD4;
	};

	float4 _MainTex_ST;
	float4 _DistortionTex_ST;
	//float4 _DisplacementTexture_ST;

	/*float rand(float3 co)
 	{
    	return frac(sin( dot(co.xyz ,float3(12.9898,78.233,45.5432) )) * 43758.5453);
 	}*/

	v2f vert(appdata_t v)
	{	
#ifndef DISABLE_ENV
		v.vertex = CurvePosition(v.vertex);
#endif
		v2f o;
		o.color = v.color;
		o.texcoord = TRANSFORM_TEX(v.texcoord.xy,_MainTex);
		o.texcoord2 = TRANSFORM_TEX(v.texcoord.xy, _DistortionTex);
		o.dessolve_uv = TRANSFORM_TEX(v.texcoord.xy, _DissolveTex);
		o.customdata1 = v.customdata1;

		//half2 uv = TRANSFORM_TEX(v.texcoord.xy, _DisplacementTexture) + (float2(_MovementX, _MovementY) * _Time.y);
        //half4 noise = tex2Dlod(_DisplacementTexture, float4(uv, 0.0f, 0.0f));
//o.color = noise;
		//half3 normal = v.normal;
		//normal.x = normal.x * (1 - _OnlyY);
		//normal.z = normal.z * (1 - _OnlyY);
        //float3 offset = normal * (noise * _DisplacementWeight);
        //v.vertex.xyz += offset;

		o.vertex = UnityObjectToClipPos(v.vertex);
		o.world_pos = mul(unity_ObjectToWorld, v.vertex);

		return o;
	}

	half4 frag(v2f i) : SV_Target
	{
		half2 temp_uv = i.texcoord2;
		temp_uv -= half2(_DistortionRotAnchorU, _DistortionRotAnchorV);
		float theta = (_DistortionTexRotUV * _Time.y) % (3.141592 * 2);
		half2 noise_uv = half2(temp_uv.x * cos(theta) - temp_uv.y * sin(theta),
								 temp_uv.x * sin(theta) + temp_uv.y * cos(theta));

		noise_uv += half2(_DistortionRotAnchorU, _DistortionRotAnchorV);
		noise_uv.x = noise_uv.x + _DistortionTexMoveU * _Time.y % 2;
		noise_uv.y = noise_uv.y + _DistortionTexMoveV * _Time.y % 2;

		half4 noise = tex2D(_DistortionTex, noise_uv);

		temp_uv = i.texcoord2;
		temp_uv -= half2(_DistortionRotAnchorU, _DistortionRotAnchorV);
		theta = (_DistortionTexRotUV * _Time.x);
		noise_uv = half2(temp_uv.x * cos(theta) - temp_uv.y * sin(theta),
								 temp_uv.x * sin(theta) + temp_uv.y * cos(theta));
		noise_uv += half2(_DistortionRotAnchorU, _DistortionRotAnchorV);
		noise += lerp(half4(0,0,0,0), tex2D(_DistortionTex, noise_uv.yx), _UseDistortionAnchor);

		noise = noise * 2 - 1;
		noise *= _DistortionStrength;
		noise_uv = i.texcoord2 - half2(_DistortionRotAnchorU, _DistortionRotAnchorV);
		noise_uv *= noise.r;

		temp_uv = lerp(half2(noise.r, noise.r), noise_uv, _UseDistortionAnchor);
		temp_uv += i.texcoord;
		temp_uv -= half2(_MainTexRotAnchorU, _MainTexRotAnchorV);
		theta = (_MainTexRotUV * _Time.y) % (3.141592 * 2);
		half2 maintex_uv = half2(temp_uv.x * cos(theta) - temp_uv.y * sin(theta),
								   temp_uv.x * sin(theta) + temp_uv.y * cos(theta));


		maintex_uv += half2(_MainTexRotAnchorU, _MainTexRotAnchorV);
		maintex_uv.x = maintex_uv.x + _MainTexMoveU * _Time.y % 2;
		maintex_uv.y = maintex_uv.y + _MainTexMoveV * _Time.y % 2;
		maintex_uv.x = maintex_uv.x + (i.customdata1.y * _UseCustomData);
		maintex_uv.y = maintex_uv.y + (i.customdata1.z * _UseCustomData);

		//fixed4 flow_col = tex2D(_FlowTex, maintex_uv);
		//flow_col = (flow_col - 0.5) * 2 * _FlowIntensity;
		//float flow_speed = _Time.y * _FlowSpeed;

		//fixed4 ret1 = tex2D(_MainTex, maintex_uv + flow_col.rg * frac(flow_speed));
		//fixed4 ret2 = tex2D(_MainTex, maintex_uv + flow_col.rg * frac(flow_speed + 0.5));
		//float lerp_value = abs(frac(flow_speed) * 2 - 1);
		//fixed4 ret = i.color * lerp(ret1, ret2, lerp_value);
		half4 ret = i.color * tex2D(_MainTex, maintex_uv);


		half4 emission = half4(0,0,0,0);
        if(_UseEmission)
		{
        	//half3 emission1 = half3(0,0,0);
        	//half3 emission2 = half3(0,0,0);
			//emission1 = tex2D(_EmissionMap, maintex_uv + flow_col.rg * frac(flow_speed)).rgb * _EmissionMapChannelMask * _EmissionColor.rgb;
			//emission2 = tex2D(_EmissionMap, maintex_uv + flow_col.rg * frac(flow_speed + 0.5)).rgb * _EmissionMapChannelMask * _EmissionColor.rgb;
			//emission1 = lerp(emission1, emission1 * ret.rgb, _EmissionMulByBaseColor);
			//emission2 = lerp(emission2, emission2 * ret.rgb, _EmissionMulByBaseColor);
			//emission.rgb = lerp(emission1, emission2, lerp_value);

			emission.rgb = tex2D(_EmissionMap, maintex_uv).rgb * _EmissionColor.rgb;
        	emission.rgb = lerp(emission, emission * ret.rgb, _EmissionMulByBaseColor);
			emission.rgb = emission;
		}
		
		ret.a = lerp(ret.a, i.color.a * ret.a, _ModifyAlpha);
		ret = ret * _TintColor * _ColorStrength + emission;
		
		////////////////////////////////////////////////
		// Dessolve
		half4 col = tex2D(_DissolveTex, i.dessolve_uv);
		half x = col.r;
		half progress = _DissolveProgress + (i.customdata1.x * _UseCustomData);
		//Edge
		half edge = lerp(x + _Edge, x - _Edge, progress);
		half alpha = min(smoothstep(progress + _Edge, progress - _Edge, edge), ret.a);
#if DISSOLVE_EDGE
		//Edge Around Factor
		half edgearound = lerp(x + _EdgeAround, x - _EdgeAround, progress);
		edgearound = smoothstep(progress + _EdgeAround, progress - _EdgeAround, edgearound);
		edgearound = pow(edgearound, _EdgeAroundPower);
		//Edge Around Color
		half3 ca = tex2D(_EdgeAroundRamp, half2(1 - edgearound, 0)).rgb;
		ca = (ret.rgb + ca)*ca*_EdgeAroundHDR;
		ret.rgb = lerp(ca, ret.rgb, edgearound);
#endif

		//////////////////////////////////////////////////////

		//ret = ApplyFogAndCloud(ret, i.world_pos, _EnvWeight, _EnvWeight);
		//ret = ApplyLightMask(ret, i.world_pos, _EnalbeLightMask);
		
		ret.a = alpha;
		//float cam_weight = saturate(1 - (i.world_pos.z - _WorldSpaceCameraPos.z)/_CamDistMax) * _CamWeight;
		//fixed4 temp = ret * cam_weight;
		//ret = lerp(ret, temp, _EnableCamWeight);
		
		//if(_UseGlobalRate)
		//	ret = ret * _BackGroundColorRate;
		return ret;
	}
		ENDCG
	}
	}
	}
}