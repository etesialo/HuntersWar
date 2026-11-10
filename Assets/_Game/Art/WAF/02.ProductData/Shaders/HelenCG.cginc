#include "UnityCG.cginc"

////////////////////////////////
float _CurvatureX;
float _WorldRadius;
float _IsSphere;
float _IsCylinder;

////////////////////////////////
sampler2D _FogNoiseTex;
float _FlowMainFog;
float _FogSpeedX;
float _FogSpeedY;
float _FogScale;
float _FogOffset;
fixed _FogYZWeight;
float _FogStart;
float _FogEnd;
fixed4 _FogColor;
float _FogIntencity;
float _FogVStart;
float _FogVEnd;
float _FogDistortionSpeedX;
float _FogDistortionSpeedY;
float _FogDistortionScale;
float _FogDistortionWeight;

fixed4 _SubFogColor;
float _SubFogIntencity;
float _SubFogDistortionSpeedX;
float _SubFogDistortionSpeedY;
float _SubFogDistortionScale;
float _SubFogDistortionWeight;

fixed4 _CloudColor;
float _CloudIntencity;
float _CloudScale;
float _CloudSpeedX;
float _CloudSpeedY;
float _CloudDistortionSpeedX;
float _CloudDistortionSpeedY;
float _CloudDistortionScale;
float _CloudDistortionWeight;
float _CloudFadeOutWeight;
////////////////////////////////////////////////////////////////
sampler2D _LightMask;
float _LightMaskWeight;
sampler2D _AddLightMask;
float _AddLightMaskWeight;
float _MoveAddLightMaskX;
float _MoveAddLightMaskY;
float _LightMaskOffsetX;
float _LightMaskOffsetY;
////////////////////////////////////////////////////////////////
uniform sampler2D _MatCapTex;
float _RotateMatCap;
float _UseMatCap;
fixed _Brightness;
fixed _OnlyMatCap;
fixed _OnlyNormal;
fixed _CalcMatCapType;

sampler2D _GlobalFlowMap;
float _GlobalFlowIntencity;
float _GlobalFlowSpeed;

#if CUSTOMLIGHT
float3 _CLightPos;

fixed3 _CLightColorR;
fixed3 _CLightColorG;
fixed3 _CLightColorB;

fixed _CLightIntensity;
fixed _CLightLinear;
fixed _CLightQuadratic;

uniform sampler2D _CLightMASKMAP;
#endif

// [CA] 월드 커브는 이 게임에서 안 씀 → 원래 위치 그대로 반환
inline float4 CurvePosition(float4 pos)
{
	return pos;
}

inline float4 CurvePosition_Unused(float4 pos)
{
	// y curve
    float is_sphere = 1 - _IsSphere;
    is_sphere = is_sphere * 100000;
    float _Radius = _WorldRadius + is_sphere;
	float3 world_pos = mul(unity_ObjectToWorld, pos);
    float3 cam_pos = _WorldSpaceCameraPos.xyz;
	cam_pos.y = 0;
	float3 new_world_pos = world_pos;
	new_world_pos.x = lerp(world_pos.x, cam_pos.x, _IsCylinder);
	float dist = min(distance(new_world_pos, cam_pos), _Radius);
	float cos_theta = dist / _Radius;
	float theta = acos(cos_theta);
	float sin_theta = sin(theta);
	float offset = _Radius * sin_theta;
	offset = _Radius - offset;
	world_pos.y -= offset;

	// x curve
	_CurvatureX = _CurvatureX * 0.001;

	float dist_z = new_world_pos.z - _WorldSpaceCameraPos.z;
	float add_x = dist_z * dist_z;
	world_pos.x += add_x * _CurvatureX;

    float4 ret = mul(unity_WorldToObject, float4(world_pos, 1));
	return ret;
}

inline fixed4 ApplyFogAndCloud(fixed4 origin, float3 world_pos, fixed cloud_intensity, fixed fog_intensity)
{
	float mask_scale = 0.01;
	float2 mask_coord = float2(world_pos.x*mask_scale + 0.5, (world_pos.z + world_pos.y) * mask_scale);

	fixed4 ret = origin;

	fixed4 flow_col = tex2D(_GlobalFlowMap, mask_coord);
	float flow_mask = 1 - flow_col.b;
	flow_col = (flow_col - 0.5) * 2 * _GlobalFlowIntencity;
	float flow_speed = _Time.y * _GlobalFlowSpeed;

	// //////////////////////////////////////////////////////////////
	// Fog Dist
	float value = clamp(world_pos.z - _FogStart, 0, _FogEnd);
	float ratio = clamp(value / (_FogEnd - _FogStart), 0.0, 1.0);

	value = world_pos.y - _FogVStart;
	ratio = ratio * (1- clamp(value / (_FogVEnd - _FogVStart), 0.0, 1.0)) * 0.5;
	// //////////////////////////////////////////////////////////////
	// // Sub Fog
	float sub_fog_scale = _SubFogDistortionScale * 0.0001;
	float2 sub_fog_coord = 
		float2(world_pos.x*sub_fog_scale, 
		(world_pos.z*sub_fog_scale*_FogYZWeight + world_pos.y*sub_fog_scale*(1 - _FogYZWeight))*0.5);
	//sub_fog_coord.x += _FogOffset;
	float2 sub_distortion_move = float2(_Time.x * _SubFogDistortionSpeedX, _Time.x * _SubFogDistortionSpeedY);
	half4 sub_distortion_noise = tex2D(_FogNoiseTex, sub_fog_coord + sub_distortion_move);

	sub_fog_scale = _FogScale * 0.0001;
	sub_fog_coord = 
		float2(world_pos.x*sub_fog_scale, 
		(world_pos.z*sub_fog_scale*_FogYZWeight + world_pos.y*sub_fog_scale*(1 - _FogYZWeight))*0.5);
	sub_fog_coord.x += _FogOffset;
	float2 sub_move = float2(_Time.x * _FogSpeedX, _Time.x * _FogSpeedY);
	sub_fog_coord = sub_fog_coord + sub_move + ((sub_distortion_noise.x + sub_distortion_noise.z) * _SubFogDistortionWeight);

	fixed4 ret1 = tex2D(_FogNoiseTex, sub_fog_coord + flow_col.rg * frac(flow_speed)) * flow_mask;
	fixed4 ret2 = tex2D(_FogNoiseTex, sub_fog_coord + flow_col.rg * frac(flow_speed + 0.5)) * flow_mask;
	float lerp_value = abs(frac(flow_speed) * 2 - 1);
	half4 sub_fog_noise = lerp(ret1, ret2, lerp_value);

	float fog_mask_b = sub_fog_noise.z * _SubFogIntencity;
	_SubFogColor = lerp(ret, _SubFogColor, fog_mask_b);
	ret = lerp(ret, _SubFogColor, ratio * fog_intensity);
	// //////////////////////////////////////////////////////////
	// fog
	float fog_scale = _FogDistortionScale * 0.0001;
	float2 fog_coord = 
		float2(world_pos.x*fog_scale, 
		(world_pos.z*fog_scale*_FogYZWeight + world_pos.y*fog_scale*(1 - _FogYZWeight))*0.5);
	//fog_coord.x += _FogOffset;
	float2 distortion_move = float2(_Time.x * _FogDistortionSpeedX, _Time.x * _FogDistortionSpeedY);
	half4 distortion_noise = tex2D(_FogNoiseTex, fog_coord + distortion_move);

	fog_scale = _FogScale * 0.0001;
	fog_coord =
		 float2(world_pos.x*fog_scale, 
		 (world_pos.z*fog_scale*_FogYZWeight + world_pos.y*fog_scale*(1 - _FogYZWeight))*0.5);
	fog_coord.x += _FogOffset;
	float2 move = float2(_Time.x * _FogSpeedX, _Time.x * _FogSpeedY);
	fog_coord = fog_coord + move + ((distortion_noise.x + distortion_noise.z) * _FogDistortionWeight);
	ret1 = tex2D(_FogNoiseTex, fog_coord + flow_col.rg * frac(flow_speed) * _FlowMainFog) * flow_mask;
	ret2 = tex2D(_FogNoiseTex, fog_coord + flow_col.rg * frac(flow_speed + 0.5)* _FlowMainFog) * flow_mask;
	lerp_value = abs(frac(flow_speed) * 2 - 1);
	half4 fog_noise = lerp(ret1, ret2, lerp_value);

	//half4 fog_noise = tex2D(_FogNoiseTex, fog_coord);

	float fog_mask_r = fog_noise.x * _FogIntencity;
	_FogColor = lerp(ret, _FogColor, fog_mask_r);
	ret = lerp(ret, _FogColor, ratio * fog_intensity);
	// //////////////////////////////////////////////////////////////////
	// Cloud
	float cloud_scale = _CloudDistortionScale * 0.0001;
	float2 cloud_coord = float2(world_pos.x*cloud_scale, (world_pos.z*cloud_scale + world_pos.y*cloud_scale )*0.5);
	distortion_move = float2(_Time.x * _CloudDistortionSpeedX, _Time.x * _CloudDistortionSpeedY);
	distortion_noise = tex2D(_FogNoiseTex, cloud_coord + distortion_move);

	cloud_scale = _CloudScale * 0.0001;	
	cloud_coord = float2(world_pos.x*cloud_scale, (world_pos.z*cloud_scale + world_pos.y*cloud_scale)*0.5);

	move = float2(_Time.x * _CloudSpeedX, _Time.x * _CloudSpeedY);
	half4 cloud_noise = tex2D(_FogNoiseTex, cloud_coord + move + (distortion_noise.y * _CloudDistortionWeight));
	float cloud_mask = cloud_noise.y * _CloudIntencity;
	float3 cam_pos = _WorldSpaceCameraPos.xyz;
	float dist = saturate(pow(distance(world_pos, cam_pos), 2) * _CloudFadeOutWeight * 0.001);
	cloud_mask =  cloud_mask * (1 - dist);
	ret = lerp(ret, _CloudColor, cloud_mask * cloud_intensity);

	return ret;
}

inline fixed4 ApplyLightMask(fixed4 origin, float3 world_pos, fixed obj_weight)
{
	float mask_scale = 0.01;
	float2 mask_coord = float2(world_pos.x*mask_scale + 0.5, (world_pos.z + world_pos.y) * mask_scale);
	//float2 mask_coord = float2(world_pos.x*mask_scale + 0.5, (world_pos.z) * mask_scale);
	fixed4 mask = tex2D(_LightMask, mask_coord);
	fixed4 ret = origin * mask;
	ret = lerp(origin, ret, _LightMaskWeight * obj_weight);
	return ret;
}

inline fixed4 ApplyAddLightMask(fixed4 origin, float3 world_pos, float obj_weight)
{
	float mask_scale = 0.01;
	float2 mask_coord = float2(world_pos.x*mask_scale + 0.5, (world_pos.z + world_pos.y) * mask_scale);
	mask_coord.x += (_Time.x * _MoveAddLightMaskX);
	mask_coord.x +=_LightMaskOffsetX;
	mask_coord.y += (_Time.y * _MoveAddLightMaskY);
	mask_coord.y +=_LightMaskOffsetY;
	mask_coord = frac(mask_coord);

	fixed4 mask = tex2D(_AddLightMask, mask_coord);
	fixed4 ret = origin + mask;
	ret = lerp(origin, ret, _AddLightMaskWeight * obj_weight);
	return ret;
}

inline fixed4 ApplyEnvMatcap(fixed4 origin, fixed2 uv)
{
	fixed4 ret = origin;

	fixed2 center = fixed2(0.5, 0.5);
	fixed2 temp_uv = uv - center;

	fixed theta = _RotateMatCap * 3.141592 / 180;
	fixed2 rot_uv;
	rot_uv.x = temp_uv.x * cos(theta) - temp_uv.y * sin(theta);
	rot_uv.y = temp_uv.x * sin(theta) + temp_uv.y * cos(theta);
	rot_uv += center;
	half4 mc = tex2D(_MatCapTex, rot_uv);

	if (_UseMatCap > 0)
	{
		if (_CalcMatCapType > 0)
		{
			mc = mc * _Brightness + (1.0 - _Brightness);
			ret = ret * mc;
		}
		else
		{
			mc = mc * _Brightness;
			ret = ret + mc;
		}
	}

	return ret;
}

inline fixed2 GetUVFromNormal(float3 v)
{
	float3 vAbs = abs(v);
	float ma;
	float2 uv;
	if (vAbs.z >= vAbs.x && vAbs.z >= vAbs.y)
	{
		ma = 0.5 / vAbs.z;
		uv = float2(v.z > 0.0 ? -v.x : -v.x , v.z > 0.0 ? v.y : -v.y );
	}
	else if (vAbs.y >= vAbs.x)
	{
		ma = 0.5 / vAbs.y;
		uv = float2(v.y < 0.0 ? v.x : -v.x, v.y < 0.0 ? -v.z : v.z);
	}
	else
	{
		ma = 0.5 / vAbs.x;
		uv = float2( v.x > 0.0 ? -v.y : v.y, v.x > 0.0 ? v.z : v.z);
	}
	return uv * ma + 0.5;
}

#if CUSTOMLIGHT
inline fixed3 GetCustomLight(float3 worldPos, float3 worldNor)
{
	fixed3 PTL = _CLightPos - worldPos;
	fixed LDist = length(PTL);
	fixed3 NorPTL = normalize(PTL);

	fixed LDN = max(dot(NorPTL, worldNor), 0);
	fixed att = 1 / (1 + (LDist * _CLightLinear) + (LDist * LDist * _CLightQuadratic));

	fixed2 lightMaskUV = GetUVFromNormal(NorPTL);
	fixed3 lightmask = tex2D(_CLightMASKMAP, lightMaskUV);

	fixed3 lightColor1 = lightmask.r * _CLightColorR;
	fixed3 lightColor2 = lightmask.g * _CLightColorG;
	fixed3 lightColor3 = lightmask.b * _CLightColorB;

	fixed3 lightColor = (lightColor1 + lightColor2 + lightColor3) * LDN * att * _CLightIntensity;
	return lightColor;
}
#endif