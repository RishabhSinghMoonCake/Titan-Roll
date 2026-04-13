using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraLayerCulling : MonoBehaviour
{
    [Header("Layer Setup")]
    [Tooltip("The exact index number of your Foliage layer (Look in the Layers menu)")]
    public int foliageLayerIndex = 8;

    [Tooltip("How close the camera needs to be to render the flowers (Meters)")]
    public float foliageCullDistance = 50f;

    void Start()
    {
        Camera cam = GetComponent<Camera>();

        // Unity has exactly 32 layers. We create an array to hold distance limits for all of them.
        float[] cullDistances = new float[32];

        // Assign our aggressive 50m limit ONLY to the foliage layer
        cullDistances[foliageLayerIndex] = foliageCullDistance;

        // Apply the new limits to the camera
        cam.layerCullDistances = cullDistances;

        // Makes the culling a perfect curve instead of a flat plane, preventing pop-in at the edges of the screen
        cam.layerCullSpherical = true;
    }
}