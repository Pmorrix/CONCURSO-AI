using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class NetworkDisconnectHandler : MonoBehaviour
{
    void Start()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback += OnDisconnect;
        }
    }

    void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnDisconnect;
        }
    }

    private void OnDisconnect(ulong clientId)
    {
        // If WE are the local client, and we were forced out (e.g., Host closed server)
        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            Debug.Log("Disconnected from server or Host shut down the game. Returning to Main Menu.");

            // Clean up cursor state
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // Kick us back to safety
            SceneManager.LoadScene(0);
        }
    }
}