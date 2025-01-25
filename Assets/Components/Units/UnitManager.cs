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
    
    public List<LineSegment>[] unitLineSegments;
    
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

        unitLineSegments = new List<LineSegment>[3];
        for (int i = 0; i < unitLineSegments.Length; i++)
        {
            unitLineSegments[i] = new List<LineSegment>();
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
        foreach (LineSegment lineSegment in unitLineSegments.SelectMany(unitLineSegment => unitLineSegment).ToList())
        {
            lineSegment.Dispose();
        }
        for (int i = 0; i < unitSelections0.Length; i++)
        {
            unitSelections0[i].unitIndices.Dispose();
        }
        
        unitDataBuffer?.Dispose();
        commandBuf?.Dispose();
        unitSelections0.Dispose();
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

        for (int i = 0; i < unitLineSegments[layer].Count; i++)
        {
            LineSegment lineSegment = unitLineSegments[layer][i];
            if (angle < lineSegment.angle0 || angle > lineSegment.angle1)
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
        for (int i = 0; i < unitLineSegments[layer].Count; i++)
        {
            LineSegment lineSegment = unitLineSegments[layer][i];
            if (!(angle1 > lineSegment.angle0 && angle1 < lineSegment.angle1))
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
        /*
         * Make new lineSegment by adding all of the selected units
         * Remove all of the old selected units by sorting the selected lineSegment indices.x
         * Calculate new lineSegment angles
         * Sort left and right new units based on angle0
         * Reset and clear everything
         */
        
        LineSegment unitLineSegment = unitLineSegments[0][0];
        NativeList<UnitWithReference> unitsLeft = new NativeList<UnitWithReference>(Allocator.Temp);
        NativeList<UnitWithReference> unitsRight = new NativeList<UnitWithReference>(Allocator.Temp);
        float desiredAngle = unitLineSegment.angle0 + (unitLineSegment.angle1 - unitLineSegment.angle0) / 2;
        

        for (int i = 0; i < unitSelections0.Length; i++)
        {
            UnitSelection unitSelection = unitSelections0[i];
            LineSegment tempLineSegment = unitLineSegments[unitSelection.lineSegmentIndex.x][unitSelection.lineSegmentIndex.y];
            if (tempLineSegment.angle1 < desiredAngle)
            {
                for (int j = 0; j < unitSelection.unitIndices.Length; j++)
                {
                    for (int k = unitSelection.unitIndices[j].x; k < unitSelection.unitIndices[j].x + unitSelection.unitIndices[j].y; k++)
                    {
                        Unit unit = tempLineSegment.units[k];
                        int3 index = new int3(unitSelection.lineSegmentIndex.x, unitSelection.lineSegmentIndex.y, k);
                        unitsLeft.Add(new UnitWithReference(unit, index));
                    }
                }
            }
            else if (tempLineSegment.angle0 > desiredAngle)
            {
                for (int j = 0; j < unitSelection.unitIndices.Length; j++)
                {
                    for (int k = unitSelection.unitIndices[j].x; k < unitSelection.unitIndices[j].x + unitSelection.unitIndices[j].y; k++)
                    {
                        Unit unit = tempLineSegment.units[k];
                        int3 index = new int3(unitSelection.lineSegmentIndex.x, unitSelection.lineSegmentIndex.y, k);
                        unitsRight.Add(new UnitWithReference(unit, index));
                    }
                }
            }
        }
        
        unitsLeft.Sort(new SortUnits(unitLineSegment.angle0));
        unitsRight.Sort(new SortUnits(unitLineSegment.angle1));
        int unitsLeftCount = unitsLeft.Length;
        int unitsRightCount = unitsRight.Length;
        for (int i = 0; i < unitsLeftCount; i++)
        {
            UnitWithReference unitWithReference = unitsLeft[i];
            unitLineSegments[unitWithReference.index.x][unitWithReference.index.y].units.RemoveAtSwapBack(unitWithReference.index.z);
        }
        for (int i = unitsRightCount - 1; i >= 0; i--)
        {
            UnitWithReference unitWithReference = unitsRight[i];
            unitLineSegments[unitWithReference.index.x][unitWithReference.index.y].units.RemoveAtSwapBack(unitWithReference.index.z);
        }
        if (unitsLeftCount > unitsRightCount)
        {
            int amount = (unitsLeftCount - unitsRightCount) / 2;
            for (int i = 0; i < amount; i++)
            {
                unitsRight.Add(unitsLeft[i]);
            }
            unitsLeft.RemoveRange(0, amount);
        }
        else if (unitsRightCount > unitsLeftCount)
        {
            int amount = (unitsRightCount - unitsLeftCount) / 2;
            for (int i = 0; i < amount; i++)
            {
                unitsLeft.Add(unitsRight[i]);
            }
            unitsRight.RemoveRange(0, amount);
        }
        unitsLeft.Sort(new SortUnits(desiredAngle));
        unitsRight.Sort(new SortUnits(desiredAngle));
        
        float leftAngle = desiredAngle;
        for (int i = 0; i < unitsLeft.Length; i++)
        {
            UnitWithReference unitWithReference = unitsLeft[i];
            Unit unit = unitsLeft[i].unit;
            unit.desiredAngle = leftAngle;
            
            LineSegment lineSegment = unitLineSegments[unitWithReference.index.x][unitWithReference.index.y];
            lineSegment.units[unitWithReference.index.z] = unit;
            unitLineSegments[unitWithReference.index.x][unitWithReference.index.y] = lineSegment;
            
            leftAngle -= UNIT_WDITH;
            leftAngle = MathExtensions.ClampAngle(leftAngle);
        }
        
        float rightAngle = desiredAngle;
        for (int i = 0; i < unitsRight.Length; i++)
        {
            UnitWithReference unitWithReference = unitsRight[i];
            Unit unit = unitsRight[i].unit;
            unit.desiredAngle = rightAngle;
            
            LineSegment lineSegment = unitLineSegments[unitWithReference.index.x][unitWithReference.index.y];
            lineSegment.units[unitWithReference.index.z] = unit;
            unitLineSegments[unitWithReference.index.x][unitWithReference.index.y] = lineSegment;
            
            rightAngle += UNIT_WDITH;
            rightAngle = MathExtensions.ClampAngle(rightAngle);
        }

        for (int i = 0; i < unitSelections0.Length; i++)
        {
            UnitSelection unitSelection = unitSelections0[i];
            LineSegment tempLineSegment = unitLineSegments[unitSelection.lineSegmentIndex.x][unitSelection.lineSegmentIndex.y];
            ResizeLineSegmentAndClearSelection(tempLineSegment);
        }

        int layer = unitSelections0[0].lineSegmentIndex.x;
        NativeList<Unit> newUnits = new NativeList<Unit>(unitsLeftCount + unitsRightCount, Allocator.Persistent);
        for (int i = 0; i < unitsLeftCount; i++)
        {
            newUnits.Add(unitsLeft[i].unit);
        }
        for (int i = 0; i < unitsRightCount; i++)
        {
            newUnits.Add(unitsRight[i].unit);
        }
        LineSegment newLineSegment = new LineSegment(leftAngle, rightAngle, layer, newUnits);
        unitLineSegments[layer].Add(newLineSegment);

        unitSelections0.Clear();
        unitsLeft.Dispose();
        unitsRight.Dispose();
    }

    public void ResizeLineSegment(LineSegment lineSegment)
    {
        for (int i = 0; i < lineSegment.units.Length; i++)
        {
            Unit unit = lineSegment.units[i];
            float angle = math.radians(math.lerp(lineSegment.angle0, lineSegment.angle1, (i + 1) / (float)lineSegment.units.Length));
            unit.angle = angle;
            unit.desiredAngle = angle;
            lineSegment.units[i] = unit;
        }
    }
    
    public void ResizeLineSegmentAndClearSelection(LineSegment lineSegment)
    {
        for (int i = 0; i < lineSegment.units.Length; i++)
        {
            Unit unit = lineSegment.units[i];
            float angle = math.radians(math.lerp(lineSegment.angle0, lineSegment.angle1, (i + 1) / (float)lineSegment.units.Length));
            unit.selected = 0;
            unit.angle = angle;
            unit.desiredAngle = angle;
            lineSegment.units[i] = unit;
        }
    }

    public void MoveUnits()
    {
        for (int i = 0; i < unitLineSegments.Length; i++)
        {
            for (int j = 0; j < unitLineSegments[i].Count; j++)
            {
                LineSegment lineSegment = unitLineSegments[i][j];
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

    public struct SortUnits : IComparer<UnitWithReference>
    {
        public float desiredAngle;
        public SortUnits(float desiredAngle)
        {
            this.desiredAngle = desiredAngle;
        }
        public int Compare(UnitWithReference x, UnitWithReference y)
        {
            if (math.all(x.index == y.index))
            {
                return 0;
            }
            if (math.abs(x.unit.angle - desiredAngle) * x.unit.speed < math.abs(y.unit.angle - desiredAngle) * x.unit.speed)
            {
                return -1;
            }
            return 1;
        }
    }
}
