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
    public bool flippedLeft;
    
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
        if ((int)Input.mouseScrollDelta.y != 0)
        {
            layer += (int)Input.mouseScrollDelta.y;
            layer = math.clamp(layer, 0, maxLayer);
            SetupMouseMoving();
        }
    }
    
    public void GetSelectionInput()
    {
        if (Input.GetMouseButtonDown(0))
        {
            SetupMouseMoving();
        }
        if (Input.GetMouseButton(0))
        {
            Vector3 mousePos = camera.ScreenToWorldPoint(Input.mousePosition);
            float2 endPos = math.normalize(new float2(mousePos.x, mousePos.y)) * (layer + 1);
            float temp = MathExtensions.AngleBetween(endPos);
            
            if (math.abs(temp - cachedTemp) > math.PI + math.PIHALF)
            {
                flipped = !flipped;

                if (flipped)
                {
                    flippedLeft = cachedTemp > 0;
                    Debug.Log($"flipped {flipped} {Time.frameCount}");
                }
            }
            
            if (temp < angle0 && !shiftedSides)
            {
                Debug.Log($"shifted {Time.frameCount}");
                shiftedSides = true;
            }
            if (temp > angle1 && shiftedSides && !flipped)
            {
                Debug.Log($"unshifted {Time.frameCount}");
                shiftedSides = false;
            }
            
            
            if (flipped && flippedLeft)
            {
                angle1 = temp;
                cachedTemp = temp;
                return;
            }
            if (flipped && !flippedLeft)
            {
                angle0 = temp;
                cachedTemp = temp;
                return;
            }
            
            if (!shiftedSides)
            {
                angle1 = temp;
            }
            else
            {
                angle0 = temp;
            }
            cachedTemp = temp;
        }
    }
    private void SetupMouseMoving()
    {
        Vector3 mousePos = camera.ScreenToWorldPoint(Input.mousePosition);
        float2 startPos = math.normalize(new float2(mousePos.x, mousePos.y)) * (layer + 1);
        angle0 = MathExtensions.AngleBetween(startPos);
        cachedTemp = angle0;
        flipped = false;
        shiftedSides = false;
    }


}
