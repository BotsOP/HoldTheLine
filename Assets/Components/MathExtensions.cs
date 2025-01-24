using Unity.Burst;
using Unity.Mathematics;
using UnityEngine;

[BurstCompile]
public static class MathExtensions
{
    [BurstCompile]
    public static bool IsAngleBetween(float angle, float startAngle, float endAngle)
    {
        // Normalize all angles to the range [0, 2π)
        angle = math.fmod(angle + math.PI * 2, math.PI * 2);
        startAngle = math.fmod(startAngle + math.PI * 2, math.PI * 2);
        endAngle = math.fmod(endAngle + math.PI * 2, math.PI * 2);

        // If the range crosses the 0°/360° boundary
        if (startAngle > endAngle)
        {
            return angle >= startAngle || angle <= endAngle;
        }
        // Normal range
        else
        {
            return angle >= startAngle && angle <= endAngle;
        }
    }
    
    [BurstCompile]
    public static float AngleBetween(in float2 vector)
    {
        float2 referenceVector = new float2(0, 1);

        float2 normalizedVector = math.normalize(vector);

        float dotProduct = math.dot(normalizedVector, referenceVector);
        dotProduct = math.clamp(dotProduct, -1.0f, 1.0f);

        float angle = math.acos(dotProduct);

        float crossZ = normalizedVector.x * referenceVector.y - normalizedVector.y * referenceVector.x;
        if (crossZ < 0)
        {
            angle = -angle;
        }

        return angle;
    }
    
    [BurstCompile]
    public static void RotateVector(in float2 vector, float angleInRadians, out float2 output)
    {
        float cosAngle = math.cos(angleInRadians);
        float sinAngle = math.sin(angleInRadians);

        float2x2 rotationMatrix = new float2x2(
            new float2(cosAngle, -sinAngle),
            new float2(sinAngle, cosAngle)
        );

        output = math.mul(rotationMatrix, vector);
    }
    
    [BurstCompile]
    public static float ClampAngle(float angle)
    {
        if (angle > math.PI)
        {
            angle -= math.PI * 2;
        }
        if (angle < -math.PI)
        {
            angle += math.PI * 2;
        }
        return angle;
    }
}
