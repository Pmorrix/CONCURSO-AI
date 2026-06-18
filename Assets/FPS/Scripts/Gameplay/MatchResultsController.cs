using UnityEngine;
using TMPro;
using Unity.Netcode;
using Unity.Collections;
using UnityEngine.SceneManagement;
using Unity.FPS.Core;

namespace Unity.FPS.Gameplay
{
    public class MatchResultsController : NetworkBehaviour
    {
        [Header("UI Element Outlets")]
        [SerializeField] private TextMeshProUGUI winStatusText;
        [SerializeField] private TextMeshProUGUI countdownTimerText;

        [Header("Configuration")]
        [SerializeField] private float returnDelayDuration = 10f;
        [SerializeField] private string mainMenuSceneName = "MainMenu";

        private NetworkVariable<float> remainingTime = new NetworkVariable<float>(10f);
        private NetworkVariable<FixedString128Bytes> networkWinStatus = new NetworkVariable<FixedString128Bytes>("Match Over!");

        private void Start()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                remainingTime.Value = returnDelayDuration;

                if (LobbyManager.Instance != null && !string.IsNullOrEmpty(LobbyManager.Instance.LocalMatchResultStatus))
                {
                    networkWinStatus.Value = LobbyManager.Instance.LocalMatchResultStatus;
                }
            }

            remainingTime.OnValueChanged += UpdateTimerText;
            networkWinStatus.OnValueChanged += UpdateWinStatusText;

            UpdateTimerText(0, remainingTime.Value);
            UpdateWinStatusText("", networkWinStatus.Value);

            NetworkManager.Singleton.OnClientDisconnectCallback += OnDisconnectOrShutdown;
        }

        public override void OnNetworkDespawn()
        {
            remainingTime.OnValueChanged -= UpdateTimerText;
            networkWinStatus.OnValueChanged -= UpdateWinStatusText;

            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnDisconnectOrShutdown;
            }
        }

        private void Update()
        {
            if (!IsServer) return;

            if (remainingTime.Value > 0)
            {
                remainingTime.Value -= Time.deltaTime;

                if (remainingTime.Value <= 0)
                {
                    remainingTime.Value = 0;
                    DisconnectAndExitMatch();
                }
            }
        }

        private void UpdateWinStatusText(FixedString128Bytes previousValue, FixedString128Bytes newValue)
        {
            winStatusText.text = newValue.ToString();
            if (winStatusText.text == "Blue Team Wins!") winStatusText.color = Color.blue;
            else if (winStatusText.text == "Red Team Wins!") winStatusText.color = Color.red;
        }

        private void UpdateTimerText(float previousValue, float newValue)
        {
            if (newValue > 0)
            {
                countdownTimerText.text = $"Returning to Menu in: {Mathf.CeilToInt(newValue)}s";
            }
            else
            {
                countdownTimerText.text = "Disconnecting...";
            }
        }

        private void DisconnectAndExitMatch()
        {
            if (!IsServer) return;

            Debug.Log("[SERVER] Timer complete. Purging old match session completely...");

            if (LobbyManager.Instance != null)
            {
                // Listen for when cleanup finishes, then switch scenes ---
                LobbyManager.Instance.OnLeftLobby += HandleLobbyCleanupComplete;
                LobbyManager.Instance.LeaveLobbyAfterStarting();
            }
            else
            {
                NetworkManager.Singleton.Shutdown();
                LoadMainMenuLocal();
            }
        }

        private void OnDisconnectOrShutdown(ulong clientId)
        {
            if (clientId == NetworkManager.Singleton.LocalClientId)
            {
                Debug.Log("[CLIENT] Disconnected from server. Cleaning up post-match references...");

                if (LobbyManager.Instance != null)
                {
                    // --- FIX: Listen for when cleanup finishes, then switch scenes ---
                    LobbyManager.Instance.OnLeftLobby += HandleLobbyCleanupComplete;
                    LobbyManager.Instance.LeaveLobbyAfterStarting();
                }
                else
                {
                    if (NetworkManager.Singleton.IsConnectedClient || NetworkManager.Singleton.IsListening)
                    {
                        NetworkManager.Singleton.Shutdown();
                    }
                    LoadMainMenuLocal();
                }
            }
        }

        private void HandleLobbyCleanupComplete(object sender, System.EventArgs e)
        {
            if (LobbyManager.Instance != null)
            {
                LobbyManager.Instance.OnLeftLobby -= HandleLobbyCleanupComplete;
            }

            Debug.Log("[MATCH SYSTEM] Lobby data references cleared cleanly. Loading local Main Menu scene...");
            LoadMainMenuLocal();
        }

        private void LoadMainMenuLocal()
        {
            SceneManager.LoadScene(mainMenuSceneName, LoadSceneMode.Single);
        }
    }
}