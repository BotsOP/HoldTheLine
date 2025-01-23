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

        float2 startPos = MathExtensions.RotateVector(new float2(0, 1), inputManager.angle0);
        float2 endPos = MathExtensions.RotateVector(new float2(0, 1), inputManager.angle1);
        Gizmos.DrawSphere(new Vector3(startPos.x, startPos.y, -1), 0.1f);
        Gizmos.DrawSphere(new Vector3(endPos.x, endPos.y, -1), 0.1f);
    }

    private void Awake()
    {
        quadMesh = mesh;
        unitManager = new UnitManager(maxAmountUnits, unitMaterial);
        inputManager = new InputManager(Camera.main, 3);
        unitManager.SpawnUnits(30, 0,  360, 1, 0);
        unitManager.SpawnUnits(10, 0,  360, 2, 1);
    }

    private void OnDisable()
    {
        unitManager.Dispose();
    }

    private void Update()
    {
        inputManager.GetLayerInput();
        if (inputManager.GetSelectionInput())
        {
            unitManager.SelectUnits(inputManager.angle0, inputManager.angle1, inputManager.layer);
        }
        unitManager.Update();
    }
    
    
}
