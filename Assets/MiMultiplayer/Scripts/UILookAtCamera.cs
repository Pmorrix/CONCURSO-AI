using UnityEngine;

public class UILookAtCamera : MonoBehaviour
{

    void LateUpdate()
    {
        Camera targetCamera = Camera.main;

        // If the main player camera is disabled (e.g., player is dead), 
        // fallback to looking at whatever camera is currently active in the scene.
        if (targetCamera == null)
        {
            targetCamera = FindFirstObjectByType<Camera>();
        }

        // Safety check to ensure we found ANY camera before applying rotation
        if (targetCamera != null)
        {
            transform.LookAt(transform.position + targetCamera.transform.forward);
        }
    }
}
