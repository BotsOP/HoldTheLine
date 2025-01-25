using Unity.Burst;
using Unity.Mathematics;
using UnityEngine;

public class InputManager
{
    public int layer;
    public float angle0;
    public float angle1;
    
    private Camera camera;
    private bool shiftedSides;
    private int maxLayer;

    public InputManager(Camera camera, int maxLayer)
    {
        this.camera = camera;
        this.maxLayer = maxLayer - 1;
    }

    public void GetLayerInput()
    {
        layer = layer + (int)Input.mouseScrollDelta.y;
        layer = math.clamp(layer, 0, maxLayer);
    }
    
    public bool GetSelectionInput()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Vector3 mousePos = camera.ScreenToWorldPoint(Input.mousePosition);
            float2 startPos = math.normalize(new float2(mousePos.x, mousePos.y)) * (layer + 1);
            angle0 = MathExtensions.AngleBetween(startPos);
        }
        if (Input.GetMouseButton(0))
        {
            Vector3 mousePos = camera.ScreenToWorldPoint(Input.mousePosition);
            float2 endPos = math.normalize(new float2(mousePos.x, mousePos.y)) * (layer + 1);
            float temp = MathExtensions.AngleBetween(endPos);

            if (temp < angle0 && !shiftedSides)
                shiftedSides = true;
            if (temp > angle1 && shiftedSides)
                shiftedSides = false;
            
            if (shiftedSides)
                angle0 = temp;
            else
                angle1 = temp;
            
            return true;
        }
        if (Input.GetMouseButtonUp(0))
        {
            shiftedSides = false;
            // Debug.Log($"angles {angle0.ToString("#.##")} {angle1.ToString("#.##")}");
        }
        return false;
    }

    
}
