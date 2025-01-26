using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

public class UnitManager
{
    private const float UNIT_WDITH = 0.03f;
    private const float FLOAT_TOLERANCE = 0.0001f;
    
    public NativeArray<NativeList<LineSegment>> unitLineSegments;
    
    private ComputeBuffer unitDataBuffer;
    private GraphicsBuffer commandBuf;
    private GraphicsBuffer.IndirectDrawIndexedArgs[] commandData;

    private int maxAmountUnits;
    private Material unitMaterial;
    private RenderParams renderParams;
    private int unitCount;

    private NativeList<UnitSelection> unitSelections0;
    private NativeList<UnitSelection> unitSelections1;
    private int amountSelectedUnits;
    private int startSelectedIndex;
    private int cachedUnitSelectionIndex;
    private UnitSelection selectedUnitSelection;
    private int selectionIndex = -1;
    private int2 cachedLineSegmentIndex = new int2(-1, -1);

    public UnitManager(int maxAmountUnits, Material unitMaterial)
    {
        this.maxAmountUnits = maxAmountUnits;
        this.unitMaterial = unitMaterial;

        unitLineSegments = new NativeArray<NativeList<LineSegment>>(3, Allocator.Persistent);
        for (int i = 0; i < unitLineSegments.Length; i++)
        {
            unitLineSegments[i] = new NativeList<LineSegment>(1, Allocator.Persistent);
        }
        
        unitSelections0 = new NativeList<UnitSelection>(Allocator.Persistent);

        unitDataBuffer = new ComputeBuffer(maxAmountUnits, sizeof(float) * 8);
        commandBuf = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1, GraphicsBuffer.IndirectDrawIndexedArgs.size);
        commandData = new GraphicsBuffer.IndirectDrawIndexedArgs[1];
        
        commandData[0].indexCountPerInstance = GameManager.quadMesh.GetIndexCount(0);
        commandData[0].instanceCount = (uint)maxAmountUnits;
        commandBuf.SetData(commandData);
        
        renderParams = new RenderParams(unitMaterial);
    }

    public void Dispose()
    {
        foreach (NativeList<LineSegment> lineSegments in unitLineSegments)
        {
            foreach (LineSegment lineSegment in lineSegments)
            {
                lineSegment.Dispose();
            }
            lineSegments.Dispose();
        }
        for (int i = 0; i < unitSelections0.Length; i++)
        {
            unitSelections0[i].unitIndices.Dispose();
        }
        
        unitDataBuffer?.Dispose();
        commandBuf?.Dispose();
        unitSelections0.Dispose();
        unitLineSegments.Dispose();
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
        LineSegment lineSegment = new LineSegment(math.radians(angle0), math.radians(angle1), layer, tempUnits);
        unitLineSegments[layer].Add(lineSegment);
        
        renderParams.material.SetFloat("_UnitCount", unitCount);
    }

    public void SelectUnitsDown(float angle, int layer)
    {
        startSelectedIndex = -1;
        amountSelectedUnits = 0;
        selectionIndex = -1;

        for (int i = 0; i < unitLineSegments[layer].Length; i++)
        {
            LineSegment lineSegment = unitLineSegments[layer][i];
            if (angle < lineSegment.angle0 || angle > lineSegment.angle1 || lineSegment.state != 0)
                continue;

            int2 lineSegmentIndex = new int2(layer, i);
            cachedLineSegmentIndex = lineSegmentIndex;
            Debug.Log(cachedLineSegmentIndex);
            
            //Check if there is already an existing unit selection
            for (int j = 0; j < unitSelections0.Length; j++)
            {
                if (math.all(unitSelections0[j].lineSegmentIndex == lineSegmentIndex))
                {
                    selectionIndex = j;
                    break;
                }
            }
        }
    }

    public void SelectUnits(float angle0, float angle1, int layer)
    {
        for (int i = 0; i < unitLineSegments[layer].Length; i++)
        {
            LineSegment lineSegment = unitLineSegments[layer][i];
            if (!(angle1 > lineSegment.angle0 && angle1 < lineSegment.angle1) || lineSegment.state != 0)
                continue;

            int2 lineSegmentIndex = new int2(layer, i);
            if (cachedLineSegmentIndex.y != lineSegmentIndex.y)
            {
                Debug.Log($"selected new segment");
                SelectUnitsUp();
                SelectUnitsDown(angle1, layer);
                return;
            }
            //Check if there is already an existing unit selection
            
            for (int j = 0; j < lineSegment.units.Length; j++)
            {
                Unit unit = lineSegment.units[j];
                if (!MathExtensions.IsAngleBetween(unit.angle, angle0, angle1) || unit.selected == 1)
                {
                    continue;
                }

                amountSelectedUnits++;

                if (startSelectedIndex == -1 || j < startSelectedIndex)
                {
                    startSelectedIndex = j;
                }
                
                unit.selected = 1;
                lineSegment.units[j] = unit;
            }
        }
    }

    public void SelectUnitsUp()
    {
        if (startSelectedIndex == -1)
            return;
        
        if (selectionIndex == -1)
        {
            NativeList<int2> selectedUnits = new NativeList<int2>(Allocator.Persistent);
            selectedUnitSelection = new UnitSelection(cachedLineSegmentIndex, selectedUnits);
            selectedUnitSelection.unitIndices.Add(new int2(startSelectedIndex, amountSelectedUnits));
            unitSelections0.Add(selectedUnitSelection);
            Debug.Log($"2: {selectedUnitSelection.unitIndices.Length}");
            return;
        }
        
        unitSelections0[selectionIndex].unitIndices.Add(new int2(startSelectedIndex, amountSelectedUnits));
        Debug.Log($"1: {unitSelections0[selectionIndex].unitIndices.Length}");
    }

    public void MergeUnits()
    {
        LineSegment unitLineSegment = unitLineSegments[0][0];
        NativeList<Unit> unitsLeft = new NativeList<Unit>(Allocator.Temp);
        NativeList<Unit> unitsRight = new NativeList<Unit>(Allocator.Temp);
        float desiredAngle = unitLineSegment.angle0 + (unitLineSegment.angle1 - unitLineSegment.angle0) / 2;

        int count = 0;
        for (int i = 0; i < unitSelections0.Length; i++)
        {
            UnitSelection unitSelection = unitSelections0[i];
            for (int j = 0; j < unitSelection.unitIndices.Length; j++)
            {
                count += unitSelection.unitIndices[j].y;
            }
            
            unitSelection.unitIndices.Sort(new SortIndices());
        }
        NativeList<Unit> newUnits = new NativeList<Unit>(count, Allocator.Persistent);
        float leftAngle = desiredAngle - UNIT_WDITH * (count / 2f);
        float rightAngle = desiredAngle + UNIT_WDITH * (count / 2f);
        LineSegment newLineSegment = new LineSegment(leftAngle, rightAngle, 1, newUnits);
        for (int i = 0; i < unitSelections0.Length; i++)
        {
            UnitSelection unitSelection = unitSelections0[i];
            int2 lineSegmentIndex = unitSelection.lineSegmentIndex;
            for (int j = 0; j < unitSelection.unitIndices.Length; j++)
            {
                int2 unitIndex = unitSelection.unitIndices[j];
                for (int k = unitIndex.x + unitIndex.y - 1; k >= unitIndex.x; k--)
                {
                    Unit unit = unitLineSegments[lineSegmentIndex.x][lineSegmentIndex.y].units[k];
                    
                    if (unit.angle < leftAngle)
                        unitsLeft.Add(unit);
                    else
                        unitsRight.Add(unit);
                    
                    unitLineSegments[lineSegmentIndex.x][lineSegmentIndex.y].units.RemoveAtSwapBack(k);
                }
            }
        }
        unitsLeft.Sort(new SortUnits(leftAngle, true));
        unitsRight.Sort(new SortUnits(leftAngle, false));
        newUnits.AddRange(unitsLeft);
        newUnits.AddRange(unitsRight);
        for (int i = 0; i < newUnits.Length; i++)
        {
            Unit unit = newUnits[i];
            float angle = math.lerp(leftAngle, rightAngle, (i + 1) / (float)newUnits.Length);
            unit.selected = 0;
            unit.desiredAngle = angle;
            newUnits[i] = unit;
        }

        for (int i = 0; i < unitSelections0.Length; i++)
        {
            UnitSelection unitSelection = unitSelections0[i];
            int2 lineSegmentIndex = unitSelection.lineSegmentIndex;
            NativeList<LineSegment> lineSegments = unitLineSegments[lineSegmentIndex.x];
            lineSegments[lineSegmentIndex.y] = ResizeLineSegment(lineSegments[lineSegmentIndex.y]);
        }
        
        newLineSegment.state = 1;
        unitLineSegments[unitSelections0[0].lineSegmentIndex.x].Add(newLineSegment);
        unitSelections0.Clear();
        unitsLeft.Dispose();
        unitsRight.Dispose();
    }

    public LineSegment ResizeLineSegment(LineSegment lineSegment)
    {
        for (int i = 0; i < lineSegment.units.Length; i++)
        {
            Unit unit = lineSegment.units[i];
            float angle = math.lerp(lineSegment.angle0, lineSegment.angle1, (i + 1) / (float)lineSegment.units.Length);
            unit.desiredAngle = angle;
            lineSegment.units[i] = unit;
        }

        lineSegment.state = 1;
        return lineSegment;
    }

    public void MoveUnits()
    {
        for (int i = 0; i < unitLineSegments.Length; i++)
        {
            NativeList<LineSegment> lineSegments = unitLineSegments[i];
            for (int j = 0; j < lineSegments.Length; j++)
            {
                LineSegment lineSegment = lineSegments[j];
                
                if(lineSegment.state == 0)
                    continue;
                
                int count = 0;
                for (int k = 0; k < lineSegment.units.Length; k++)
                {
                    Unit unit = lineSegment.units[k];
                    
                    if(math.abs(unit.angle - unit.desiredAngle) < math.radians(UNIT_WDITH))
                        continue;
                    
                    if (unit.angle < unit.desiredAngle)
                    {
                        unit.angle += math.radians(Time.deltaTime * unit.speed);
                    }
                    if (unit.angle > unit.desiredAngle)
                    {
                        unit.angle -= math.radians(Time.deltaTime * unit.speed);
                    }
                    lineSegment.units[k] = unit;
                    count++;
                }
                
                if (count == 0)
                {
                    lineSegment.state = 0;
                    lineSegments[j] = lineSegment;
                    Debug.Log($"everyone stopped moving");
                }
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

        MoveUnits();

        int startIndex = 0;
        for (int i = 0; i < unitLineSegments.Length; i++)
        {
            for (int j = 0; j < unitLineSegments[i].Length; j++)
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
        public int state;
        public NativeList<Unit> units;

        public LineSegment(float angle0, float angle1, int layer, NativeList<Unit> units)
        {
            this.angle0 = angle0;
            this.angle1 = angle1;
            this.layer = layer;
            this.units = units;
            state = 0;
        }

        public void Dispose()
        {
            units.Dispose();
        }
    }

    public struct UnitSelection
    {
        public int2 lineSegmentIndex; //x = layer, y = lineSegment
        public NativeList<int2> unitIndices; //x = start, y = amount

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
        public float desiredAngle;
        public float speed;
        public float radius;
        public float tiredness;
        public float health;
        public int selected;
        public int layer;

        public Unit(float angle, float radius, float tiredness, float health, int layer)
        {
            this.angle = angle;
            this.radius = radius;
            this.tiredness = tiredness;
            this.health = health;
            this.layer = layer;
            desiredAngle = angle;
            speed = 10;
            selected = 0;
        }
    }

    public struct UnitWithReference
    {
        public Unit unit;
        public int3 index; //x = layer, y = lineSegment, z = unitIndex

        public UnitWithReference(Unit unit, int3 index)
        {
            this.unit = unit;
            this.index = index;
        }
    }

    public struct SortUnits : IComparer<Unit>
    {
        public float desiredAngle;
        public int reverse;
        public SortUnits(float desiredAngle, bool reverse)
        {
            this.desiredAngle = desiredAngle;
            this.reverse = reverse ? 1 : -1;
        }
        public int Compare(Unit x, Unit y)
        {
            if (Math.Abs(x.angle - y.angle) < FLOAT_TOLERANCE)
            {
                return 0;
            }
            if (math.abs(x.angle - desiredAngle) * x.speed < math.abs(y.angle - desiredAngle) * x.speed)
            {
                return reverse;
            }
            return reverse * -1;
        }
    }
    
    public struct SortIndices : IComparer<int2>
    {
        public int Compare(int2 x, int2 y)
        {
            if (x.x == y.x)
            {
                return 0;
            }
            if (x.x > y.x)
            {
                return -1;
            }
            return 1;
        }
    }
}
