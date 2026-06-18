using Unity.FPS.Core;
using Unity.FPS.UI;
using Unity.Netcode;
using UnityEngine;

public class MatchNotificationHandler : NetworkBehaviour
{
    private NotificationHUDManager m_NotificationManager;

    // Hex Color codes matching Team UI styles
    private const string HexBlue = "#3498db";
    private const string HexRed = "#e74c3c";

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback += OnServerDetectedDisconnect;
            FlagCarrierTracker.OnFlagGrabbedServer += OnServerDetectedFlagGrab;
            FlagCarrierTracker.OnFlagDroppedNotificationServer += OnServerDetectedFlagDrop; // <-- Subscribing here
        }

        NetworkManager.Singleton.OnClientDisconnectCallback += OnLocalDisconnectFallback;
    }

    public override void OnNetworkDespawn()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnServerDetectedDisconnect;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnLocalDisconnectFallback;
        }

        if (IsServer)
        {
            FlagCarrierTracker.OnFlagGrabbedServer -= OnServerDetectedFlagGrab;
            FlagCarrierTracker.OnFlagDroppedNotificationServer -= OnServerDetectedFlagDrop; // <-- Unsubscribing here
        }
    }

    void Start()
    {
        m_NotificationManager = FindAnyObjectByType<NotificationHUDManager>();

        if (m_NotificationManager == null)
        {
            Debug.LogWarning("MatchNotificationHandler: Could not find NotificationHUDManager in the scene hierarchy.");
        }
    }

    // --- SERVER FLAG GRAB DETECTOR ---
    private void OnServerDetectedFlagGrab(NetworkObject carrierNetObj)
    {
        if (!IsServer || carrierNetObj == null) return;

        if (carrierNetObj.TryGetComponent<PlayerSettings>(out var settings))
        {
            string rawPlayerName = "A player";
            if (LobbyManager.Instance != null)
            {
                rawPlayerName = LobbyManager.Instance.GetPlayerNameFromClientId(carrierNetObj.OwnerClientId);
            }

            string playerTeamHex = (settings.TeamIndex == 0) ? HexBlue : HexRed;
            string coloredPlayerName = $"<color={playerTeamHex}>{rawPlayerName}</color>";

            string flagTeamHex = (settings.TeamIndex == 0) ? HexRed : HexBlue;
            string flagName = (settings.TeamIndex == 0) ? "Red Flag" : "Blue Flag";
            string coloredFlagText = $"<color={flagTeamHex}>{flagName}</color>";

            string complexMessage = $"{coloredPlayerName} has taken the {coloredFlagText}!";

            NotifyClientsClientRpc(complexMessage);
        }
    }

    // --- SERVER FLAG DROP DETECTOR ---
    private void OnServerDetectedFlagDrop(NetworkObject carrierNetObj, int droppedFlagTeamIndex)
    {
        if (!IsServer || carrierNetObj == null) return;

        if (carrierNetObj.TryGetComponent<PlayerSettings>(out var settings))
        {
            // 1. Resolve dropping player's text identity
            string rawPlayerName = "A player";
            if (LobbyManager.Instance != null)
            {
                rawPlayerName = LobbyManager.Instance.GetPlayerNameFromClientId(carrierNetObj.OwnerClientId);
            }

            string playerTeamHex = (settings.TeamIndex == 0) ? HexBlue : HexRed;
            string coloredPlayerName = $"<color={playerTeamHex}>{rawPlayerName}</color>";

            // 2. Resolve dropped flag details (0 = Blue Flag, 1 = Red Flag)
            string flagTeamHex = (droppedFlagTeamIndex == 0) ? HexBlue : HexRed;
            string flagName = (droppedFlagTeamIndex == 0) ? "Blue Flag" : "Red Flag";
            string coloredFlagText = $"<color={flagTeamHex}>{flagName}</color>";

            // 3. Assemble complete dropped layout string format
            string complexMessage = $"{coloredPlayerName} has dropped the {coloredFlagText}!";

            // 4. Distribute announcement package across clients
            NotifyClientsClientRpc(complexMessage);
        }
    }

    // Executed ONLY on the Server/Host machine
    private void OnServerDetectedDisconnect(ulong clientId)
    {
        if (clientId == NetworkManager.ServerClientId) return;

        string disconnectedPlayerName = "A player";
        if (LobbyManager.Instance != null)
        {
            disconnectedPlayerName = LobbyManager.Instance.GetPlayerNameFromClientId(clientId);
        }

        string noticeMessage = $"{disconnectedPlayerName} has left the game";
        Debug.Log($"[SERVER] Mid-match disconnect verified for client {clientId}. Dispatching announcement...");

        NotifyClientsClientRpc(noticeMessage);
    }

    private void OnLocalDisconnectFallback(ulong myClientId)
    {
        if (IsServer || !NetworkManager.Singleton.IsConnectedClient) return;

        if (myClientId == NetworkManager.Singleton.LocalClientId)
        {
            Debug.LogWarning("[MATCH] Connection to host lost! Stashing notice and preparing exit sequence...");

            if (LobbyManager.Instance != null)
            {
                LobbyManager.Instance.SetDisconnectFeedback("Host has disconnected. Returning to Main Menu.");
                LobbyManager.Instance.LeaveLobbyAfterStarting();
            }
        }
    }

    [Rpc(SendTo.Everyone)]
    private void NotifyClientsClientRpc(string noticeMessage)
    {
        Debug.Log($"[NOTIFICATION RECV] {noticeMessage}");

        if (m_NotificationManager != null)
        {
            m_NotificationManager.CreateNotification(noticeMessage);
        }
        else
        {
            var manager = FindFirstObjectByType<NotificationHUDManager>();
            if (manager != null) manager.CreateNotification(noticeMessage);
        }
    }
}