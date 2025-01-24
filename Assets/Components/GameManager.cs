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

        MathExtensions.RotateVector(new float2(0, 1), inputManager.angle0, out float2 startPos);
        MathExtensions.RotateVector(new float2(0, 1), inputManager.angle1, out float2 endPos);
        Gizmos.DrawSphere(new Vector3(startPos.x, startPos.y, -1), 0.1f);
        Gizmos.DrawSphere(new Vector3(endPos.x, endPos.y, -1), 0.1f);
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
