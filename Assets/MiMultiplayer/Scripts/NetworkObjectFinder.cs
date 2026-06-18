using Unity.Netcode;
using UnityEngine;

public class NetworkObjectFinder : MonoBehaviour
{
    [ContextMenu("Find Broken NetworkObjects")]
    void Start()
    {
        // Find all NetworkObjects, including inactive ones
        NetworkObject[] allNetObjs = FindObjectsByType<NetworkObject>(FindObjectsSortMode.None);

        if (allNetObjs.Length == 0)
        {
            Debug.Log("No NetworkObjects found in the scene.");
            return;
        }

        foreach (var netObj in allNetObjs)
        {
            // In modern Netcode, we check the PrefabIdHash
            // If it's 0, it means the object hasn't been properly indexed by the NetworkManager
            if (netObj.PrefabIdHash == 0)
            {
                Debug.LogError($"!!! BROKEN OBJECT !!! Name: {netObj.name}. This object is missing its Network ID and will crash your game on join.", netObj);
            }
            else
            {
                Debug.Log($"Healthy Object: {netObj.name} (Hash: {netObj.PrefabIdHash})");
            }
        }
    }
}