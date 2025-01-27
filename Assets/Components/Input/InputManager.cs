using Unity.Burst;
using Unity.Mathematics;
using UnityEngine;

public class InputManager
{
    public int layer;
    public float angle0;
    public float angle1;
    public bool shiftedSides;
    public bool flipped;
    
    private Camera camera;
    private int maxLayer;
    private float cachedTemp;

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
    
    public void GetSelectionInput()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Vector3 mousePos = camera.ScreenToWorldPoint(Input.mousePosition);
            float2 startPos = math.normalize(new float2(mousePos.x, mousePos.y)) * (layer + 1);
            angle0 = MathExtensions.AngleBetween(startPos);
            cachedTemp = angle0;
            flipped = false;
            shiftedSides = false;
        }
        if (Input.GetMouseButton(0))
        {
            Vector3 mousePos = camera.ScreenToWorldPoint(Input.mousePosition);
            float2 endPos = math.normalize(new float2(mousePos.x, mousePos.y)) * (layer + 1);
            float temp = MathExtensions.AngleBetween(endPos);
            
            if (math.abs(temp - cachedTemp) > math.PI + math.PIHALF)
            {
                flipped = !flipped;
                Debug.Log($"flipped");
            }
            
            if (temp < angle0 && !shiftedSides)
            {
                shiftedSides = true;
                Debug.Log($"shifted");
            }
            if (temp > angle0 && shiftedSides)
            {
                shiftedSides = false;
                Debug.Log($"unshifted");
            }
            
            angle1 = temp;
            cachedTemp = temp;
        }
        if (Input.GetMouseButtonUp(0))
        {
            
        }
    }

    
}
