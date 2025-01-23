using System.Collections.Generic;
using System.Linq;
using Unity.Burst;
using Unity.Collections;
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

    private NativeList<UnitSelection> unitSelections0;

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
        
        foreach (LineSegment lineSegment in unitLineSegments.SelectMany(unitLineSegment => unitLineSegment).ToList())
        {
            lineSegment.Dispose();
        }
    }

    public void SpawnUnits(int amount, float angle0, float angle1, float radius, int layer)
    {
        NativeList<Unit> tempUnits = new NativeList<Unit>(Allocator.Persistent);
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

            int2 lineSegmentIndex = new int2(layer, i);
            int unitSelectionIndex = -1;
            for (int j = 0; j < unitSelections0.Length; j++)
            {
                if (math.all(unitSelections0[j].lineSegmentIndex == lineSegmentIndex))
                {
                    unitSelectionIndex = j;
                    break;
                }
            }

            UnitSelection unitSelection;
            if (unitSelectionIndex == -1)
            {
                NativeList<int2> selectedUnits = new NativeList<int2>(Allocator.Persistent);
                unitSelection = new UnitSelection(lineSegmentIndex, selectedUnits);
            }
            else
            {
                unitSelection = unitSelections0[unitSelectionIndex];
            }
            int startIndex = -1;
            int amountSelectedUnits = 0;

            for (int j = 0; j < lineSegment.units.Length; j++)
            {
                Unit unit = lineSegment.units[j];
                if (!MathExtensions.IsAngleBetween(unit.angle, angle0, angle1))
                {
                    unit.tiredness = 1;
                    lineSegment.units[j] = unit;
                    amountSelectedUnits++;
                    
                    if (startIndex == -1)
                        startIndex = j;
                    
                    continue;
                }

                unit.tiredness = 0;
                lineSegment.units[j] = unit;
            }
            
            if (startIndex != -1)
                unitSelection.unitIndices.Add(new int2(startIndex, amountSelectedUnits));
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
                unitDataBuffer.SetData(lineSegment.units.AsArray(), 0, startIndex, lineSegment.units.Length);
                startIndex += lineSegment.units.Length;
            }
        }
        unitMaterial.SetBuffer("UnitDataBuffer", unitDataBuffer);
        Graphics.RenderMeshIndirect(renderParams, GameManager.quadMesh, commandBuf);
    }
    
    public struct LineSegment
    {
        public float angle0;
        public float angle1;
        public int layer;
        public NativeList<Unit> units;

        public LineSegment(float angle0, float angle1, int layer, NativeList<Unit> units)
        {
            this.angle0 = angle0;
            this.angle1 = angle1;
            this.layer = layer;
            this.units = units;
        }

        public void Dispose()
        {
            units.Dispose();
        }
    }

    public struct UnitSelection
    {
        public int2 lineSegmentIndex;
        public NativeList<int2> unitIndices;

        public UnitSelection(int2 lineSegmentIndex, NativeList<int2> unitIndices)
        {
            this.lineSegmentIndex = lineSegmentIndex;
            this.unitIndices = unitIndices;
        }

        public void Dispose()
        {
            unitIndices.Dispose();
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
