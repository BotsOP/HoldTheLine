using System;
using Unity.Burst;
using Unity.Mathematics;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static Mesh quadMesh;

    [SerializeField] private Mesh mesh;
    [SerializeField] private int maxAmountUnits;
    [SerializeField] private Material unitMaterial;
    
    private UnitManager unitManager;
    private InputManager inputManager;

    private void OnDrawGizmos()
    {
        if(inputManager == null)
            return;


        float angle0 = inputManager.shiftedSides ? inputManager.angle1 : inputManager.angle0;
        float angle1 = inputManager.shiftedSides ? inputManager.angle0 : inputManager.angle1;
        MathExtensions.RotateVector(new float2(0, 1), angle0, out float2 startPos);
        MathExtensions.RotateVector(new float2(0, 1), angle1, out float2 endPos);

        float middleAngle = (angle0 + angle1) / 2;
        if (inputManager.flipped)
        {
            float dist0 = math.abs(-math.PI - angle0);
            float dist1 = math.PI - angle1;
            float dist = dist0 + dist1;
            dist /= 2;
            // Debug.Log($"{dist0} + {dist1} = {dist}");
            middleAngle = MathExtensions.ClampAngle(angle1 + dist);
            Debug.Log($"{middleAngle}");
        }
        MathExtensions.RotateVector(new float2(0, 1), middleAngle, out float2 middlePos);
        
        Gizmos.color = Color.green;
        Gizmos.DrawSphere(new Vector3(startPos.x, startPos.y, -1), 0.1f);
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(new Vector3(endPos.x, endPos.y, -1), 0.1f);
        Gizmos.color = Color.gray;
        Gizmos.DrawSphere(new Vector3(middlePos.x, middlePos.y, -1), 0.1f);
    }

    private void Awake()
    {
        quadMesh = mesh;
        unitManager = new UnitManager(maxAmountUnits, unitMaterial);
        inputManager = new InputManager(Camera.main, 3);
        unitManager.SpawnUnits(30, 0,  30, 2, 1);
        unitManager.SpawnUnits(30, 110,  140, 2, 1);
        unitManager.SpawnUnits(10, 45,  90, 1, 0);
    }

    private void OnDisable()
    {
        unitManager.Dispose();
    }

    private void Update()
    {
        inputManager.GetLayerInput();
        inputManager.GetSelectionInput();
        
        if (Input.GetMouseButtonDown(0))
        {
            unitManager.SelectUnitsDown(inputManager.angle0, inputManager.layer);
        }
        if (Input.GetMouseButton(0))
        {
            unitManager.SelectUnits(inputManager.angle0, inputManager.angle1, inputManager.layer);
        }
        if (Input.GetMouseButtonUp(0))
        {
            unitManager.SelectUnitsUp();
        }
        
        if (Input.GetMouseButtonDown(1))
        {
            unitManager.MergeUnits();
        }
        unitManager.Update();
    }
    
    
}
