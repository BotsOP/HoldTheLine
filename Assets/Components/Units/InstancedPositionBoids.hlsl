#ifndef SHADER_GRAPH_SUPPORT_H
#define SHADER_GRAPH_SUPPORT_H

struct Unit
{
    float angle;
    float radius;
    float tiredness;
    float health;
    int layer;
};

StructuredBuffer<Unit> UnitDataBuffer;

float4x4 m_scale(float4x4 m, float3 v)
{
    float x = v.x, y = v.y, z = v.z;

    m[0][0] *= x; m[1][0] *= y; m[2][0] *= z;
    m[0][1] *= x; m[1][1] *= y; m[2][1] *= z;
    m[0][2] *= x; m[1][2] *= y; m[2][2] *= z;
    m[0][3] *= x; m[1][3] *= y; m[2][3] *= z;

    return m;
}

float4x4 quaternion_to_matrix(float4 quat)
{
    float4x4 m = float4x4(float4(0, 0, 0, 0), float4(0, 0, 0, 0), float4(0, 0, 0, 0), float4(0, 0, 0, 0));

    float x = quat.x, y = quat.y, z = quat.z, w = quat.w;
    float x2 = x + x, y2 = y + y, z2 = z + z;
    float xx = x * x2, xy = x * y2, xz = x * z2;
    float yy = y * y2, yz = y * z2, zz = z * z2;
    float wx = w * x2, wy = w * y2, wz = w * z2;

    m[0][0] = 1.0 - (yy + zz);
    m[0][1] = xy - wz;
    m[0][2] = xz + wy;

    m[1][0] = xy + wz;
    m[1][1] = 1.0 - (xx + zz);
    m[1][2] = yz - wx;

    m[2][0] = xz - wy;
    m[2][1] = yz + wx;
    m[2][2] = 1.0 - (xx + yy);

    m[3][3] = 1.0;

    return m;
}

float4x4 m_translate(float4x4 m, float3 v)
{
    float x = v.x, y = v.y, z = v.z;
    m[0][3] += x;
    m[1][3] += y;
    m[2][3] += z;
    return m;
}

float4x4 compose(float3 position, float4 quat, float3 scale)
{
    float4x4 m = quaternion_to_matrix(quat);
    m = m_scale(m, scale);
    m = m_translate(m, position);
    return m;
}

float4 EulerToQuaternion(float3 euler)
{
    float3 halfAngles = 0.5f * euler;
    float3 sinAngles = sin(halfAngles);
    float3 cosAngles = cos(halfAngles);

    float4 q;
    q.x = sinAngles.x * cosAngles.y * cosAngles.z - cosAngles.x * sinAngles.y * sinAngles.z;
    q.y = cosAngles.x * sinAngles.y * cosAngles.z + sinAngles.x * cosAngles.y * sinAngles.z;
    q.z = cosAngles.x * cosAngles.y * sinAngles.z - sinAngles.x * sinAngles.y * cosAngles.z;
    q.w = cosAngles.x * cosAngles.y * cosAngles.z + sinAngles.x * sinAngles.y * sinAngles.z;

    return q;
}

inline void SetUnityMatrices(uint instanceID, inout float4x4 objectToWorld, inout float4x4 worldToObject)
{
// #if UNITY_ANY_INSTANCING_ENABLED
    float4x4 worldMatrix;
    float angle = UnitDataBuffer[instanceID].angle;
    float cosAngle = cos(angle);
    float sinAngle = sin(angle);

    float2x2 rotationMatrix = float2x2(
        float2(cosAngle, -sinAngle),
        float2(sinAngle, cosAngle)
    );

    float2 pos = mul(rotationMatrix, float2(0, 1)) * UnitDataBuffer[instanceID].radius;
    pos = float2(-pos.x, pos.y);
    worldMatrix = compose(float3(pos, -1), float4(0, 0, 0, 1), float3(0.03, 0.03, 0.03));

    objectToWorld = worldMatrix;
    
    float3x3 w2oRotation;
    w2oRotation[0] = objectToWorld[1].yzx * objectToWorld[2].zxy - objectToWorld[1].zxy * objectToWorld[2].yzx;
    w2oRotation[1] = objectToWorld[0].zxy * objectToWorld[2].yzx - objectToWorld[0].yzx * objectToWorld[2].zxy;
    w2oRotation[2] = objectToWorld[0].yzx * objectToWorld[1].zxy - objectToWorld[0].zxy * objectToWorld[1].yzx;
    
    float det = dot(objectToWorld[0].xyz, w2oRotation[0]);
    w2oRotation = transpose(w2oRotation);
    w2oRotation *= rcp(det);
    float3 w2oPosition = mul(w2oRotation, -objectToWorld._14_24_34);
    
    worldToObject._11_21_31_41 = float4(w2oRotation._11_21_31, 0.0f);
    worldToObject._12_22_32_42 = float4(w2oRotation._12_22_32, 0.0f);
    worldToObject._13_23_33_43 = float4(w2oRotation._13_23_33, 0.0f);
    worldToObject._14_24_34_44 = float4(w2oPosition, 1.0f);
// #endif
}

void passthroughVec3_float(in float3 In, out float3 Out)
{
    Out = In;
}

void setup()
{
#if UNITY_ANY_INSTANCING_ENABLED
    SetUnityMatrices(unity_InstanceID, unity_ObjectToWorld, unity_WorldToObject);
#endif
}




#endif