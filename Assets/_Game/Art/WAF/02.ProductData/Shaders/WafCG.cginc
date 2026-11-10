//#include "UnityCG.cginc"

////////////////////////////////
float _CurvatureZ_float;

// [CA] 월드 커브는 이 게임에서 안 씀 → 원래 위치 그대로 반환
inline void CurvePositionZ_float(float3 world_pos, float3 object_pos, out float4 _out)
{
    _out = float4(object_pos, 1.0);
}

inline void CurvePositionZ_float_Unused(float3 world_pos, float3 object_pos, out float4 _out)
{
    float3 dist = world_pos - _WorldSpaceCameraPos;
    
    float3 pow_result = pow(dist, 2); // Power ( I, 2)
    
    float3 splitX = _CurvatureZ_float * 0.001 * pow_result;
    
    float4 _Vector4_Out = float4(0, splitX.x, 0, 0);

    float4 mul_result = mul(unity_WorldToObject, _Vector4_Out);
    
    _out = mul_result + float4(object_pos, 1.0);
}