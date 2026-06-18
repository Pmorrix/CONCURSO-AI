using UnityEngine;

public class RotateSkybox : MonoBehaviour
{
    [Tooltip("Speed of the skybox rotation.")]
    public float rotationSpeed = 1.5f;

    private Material skyboxMaterial;

    private void Start()
    {
        // Grab the active skybox material currently used in lighting settings
        skyboxMaterial = RenderSettings.skybox;

        if (skyboxMaterial == null)
        {
            Debug.LogWarning("No skybox material found in RenderSettings! Please assign one in the Lighting window.");
            enabled = false;
        }
    }

    private void Update()
    {
        // Get the current rotation value from the shader
        float currentRotation = skyboxMaterial.GetFloat("_Rotation");

        // Increase it over time
        currentRotation += rotationSpeed * Time.deltaTime;

        // Keep the value clean between 0 and 360 degrees
        currentRotation %= 360f;

        // Apply it back to the shader
        skyboxMaterial.SetFloat("_Rotation", currentRotation);
    }
}