using System.Collections.Generic;
using Unity.Burst;
using Unity.Mathematics;
using UnityEngine;

public class UnitManager
{
    public List<LineSegment>[] unitLineSegments;
    
    private ComputeBuffer unitDataBuffer;
    private GraphicsBuffer commandBuf;
    private GraphicsBuffer.IndirectDrawIndexedArgs[] commandData;

    private int maxAmountUnits;
    private Material unitMaterial;
    private RenderParams renderParams;
    private int unitCount;

    public UnitManager(int maxAmountUnits, Material unitMaterial)
    {
        this.maxAmountUnits = maxAmountUnits;
        this.unitMaterial = unitMaterial;

        unitLineSegments = new List<LineSegment>[3];
        for (int i = 0; i < unitLineSegments.Length; i++)
        {
            unitLineSegments[i] = new List<LineSegment>();
        }

        unitDataBuffer = new ComputeBuffer(maxAmountUnits, sizeof(float) * 5);
        commandBuf = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1, GraphicsBuffer.IndirectDrawIndexedArgs.size);
        commandData = new GraphicsBuffer.IndirectDrawIndexedArgs[1];
        
        commandData[0].indexCountPerInstance = GameManager.quadMesh.GetIndexCount(0);
        commandData[0].instanceCount = (uint)maxAmountUnits;
        commandBuf.SetData(commandData);
        
        renderParams = new RenderParams(unitMaterial);
    }

    public void Dispose()
    {
        unitDataBuffer?.Dispose();
        commandBuf?.Dispose();
    }

    public void SpawnUnits(int amount, float angle0, float angle1, float radius, int layer)
    {
        List<Unit> tempUnits = new List<Unit>();
        for (int i = 0; i < amount; i++)
        {
            float angle = math.radians(math.lerp(angle0, angle1, (i + 1) / (float)amount));
            Unit unit = new Unit(angle, layer + 1, 1, 1, layer);
            tempUnits.Add(unit);
            unitCount++;
        }
        LineSegment lineSegment = new LineSegment(angle0, angle1, layer, tempUnits);
        unitLineSegments[layer].Add(lineSegment);
        
        renderParams.material.SetFloat("_UnitCount", unitCount);
    }
    
    public void SelectUnits(float angle0, float angle1, int layer)
    {
        for (int i = 0; i < unitLineSegments[layer].Count; i++)
        {
            LineSegment lineSegment = unitLineSegments[layer][i];
            if (!(angle0 < lineSegment.angle1) && !(angle1 > lineSegment.angle0))
                continue;

            for (int j = 0; j < lineSegment.units.Count; j++)
            {
                Unit unit = lineSegment.units[j];
                if (!IsAngleBetween(unit.angle, angle0, angle1))
                {
                    unit.tiredness = 1;
                    lineSegment.units[j] = unit;
                    continue;
                }

                unit.tiredness = 0;
                lineSegment.units[j] = unit;
            }
        }
    }
    
    public void Update()
    {
        if (unitCount >= maxAmountUnits)
        {
            Debug.LogError($"{unitCount} too many units");
            return;
        }

        int startIndex = 0;
        for (int i = 0; i < unitLineSegments.Length; i++)
        {
            for (int j = 0; j < unitLineSegments[i].Count; j++)
            {
                LineSegment lineSegment = unitLineSegments[i][j];
                unitDataBuffer.SetData(lineSegment.units, 0, startIndex, lineSegment.units.Count);
                startIndex += lineSegment.units.Count;
            }
        }
        unitMaterial.SetBuffer("UnitDataBuffer", unitDataBuffer);
        Graphics.RenderMeshIndirect(renderParams, GameManager.quadMesh, commandBuf);
    }

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

    
    public struct LineSegment
    {
        public float angle0;
        public float angle1;
        public int layer;
        public List<Unit> units;

        public LineSegment(float angle0, float angle1, int layer, List<Unit> units)
        {
            this.angle0 = angle0;
            this.angle1 = angle1;
            this.layer = layer;
            this.units = units;
        }
    }
    
    public struct Unit
    {
        public float angle;
        public float radius;
        public float tiredness;
        public float health;
        public int layer;

        public Unit(float angle, float radius, float tiredness, float health, int layer)
        {
            this.angle = angle;
            this.radius = radius;
            this.tiredness = tiredness;
            this.health = health;
            this.layer = layer;
        }
    }
}
