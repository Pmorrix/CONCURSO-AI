using System.Collections.Generic;
using Unity.FPS.Core;
using Unity.Netcode;
using Unity.Services.Lobbies.Models;
using UnityEngine;

namespace Unity.FPS.Gameplay
{
    public class PlayerSpawner : MonoBehaviour
    {
        public static PlayerSpawner Instance { get; private set; }

        [Header("Default Human Setup")]
        [Tooltip("Assign your default player prefab here (used when choice is 'HumanPlayer').")]
        [SerializeField] private GameObject defaultHumanPrefab;

        [Header("Team Spawn Lists")]
        [SerializeField] private List<Transform> redTeamSpawnPoints = new List<Transform>();
        [SerializeField] private List<Transform> blueTeamSpawnPoints = new List<Transform>();

        [Header("Gizmo Configuration")]
        [SerializeField] private Color redTeamGizmoColor = Color.red;
        [SerializeField] private Color blueTeamGizmoColor = Color.cyan;
        [SerializeField] private Vector3 playerSizeGizmo = new Vector3(1, 2, 1);

        private int redIndexOffset = 0;
        private int blueIndexOffset = 0;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            Debug.Log("[SPAWNER] PlayerSpawner initialized in the battle scene.");

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            {
                Debug.Log("[SPAWNER] Server detected on Start! Spawning currently ready clients (Host).");
                ExecuteMatchRegistrationAndSpawn();

                // Keep this! It catches remote clients that finish scene loading late over Relay.
                NetworkManager.Singleton.SceneManager.OnSceneEvent += HandleSceneEvent;
            }
            else
            {
                if (LobbyManager.Instance != null)
                {
                    LobbyManager.Instance.OnServerAllClientsLoadedScene += ExecuteMatchRegistrationAndSpawn;
                    Debug.Log("[SPAWNER] Client detected. Listening to LobbyManager action fallback hook.");
                }
            }
        }

        private void OnDestroy()
        {
            if (LobbyManager.Instance != null)
            {
                LobbyManager.Instance.OnServerAllClientsLoadedScene -= ExecuteMatchRegistrationAndSpawn;
            }

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.SceneManager != null && NetworkManager.Singleton.IsServer)
            {
                NetworkManager.Singleton.SceneManager.OnSceneEvent -= HandleSceneEvent;
            }
        }

        private void HandleSceneEvent(SceneEvent sceneEvent)
        {
            if (sceneEvent.SceneEventType == SceneEventType.LoadComplete)
            {
                ulong clientThatLoaded = sceneEvent.ClientId;
                Debug.Log($"[SPAWNER] [SceneEvent] Client {clientThatLoaded} finished loading scene: {sceneEvent.SceneName}");

                if (clientThatLoaded == NetworkManager.ServerClientId) return;

                Debug.Log($"[SPAWNER] [SceneEvent] Triggering dynamic late-spawn for Client {clientThatLoaded}");
                SpawnSingleConnectedPlayer(clientThatLoaded);
            }
        }

        public void ExecuteMatchRegistrationAndSpawn()
        {
            if (!NetworkManager.Singleton.IsServer)
            {
                Debug.Log("[SPAWNER] I am a Client. Aborting execution because only the Server can spawn objects.");
                return;
            }

            Debug.Log($"[SPAWNER] Server event received! Total connected clients to iterate: {NetworkManager.Singleton.ConnectedClientsIds.Count}");

            foreach (ulong clientNetworkId in NetworkManager.Singleton.ConnectedClientsIds)
            {
                SpawnSingleConnectedPlayer(clientNetworkId);
            }
        }

        private void SpawnSingleConnectedPlayer(ulong networkClientId)
        {
            Lobby activeLobby = LobbyManager.Instance.GetJoinedLobby();
            if (activeLobby == null)
            {
                Debug.LogError("[SPAWN] activeLobby is NULL! Cannot parse team data.");
                return;
            }

            string ugsPlayerId = "";
            if (networkClientId == NetworkManager.ServerClientId)
            {
                ugsPlayerId = Unity.Services.Authentication.AuthenticationService.Instance.PlayerId;
                Debug.Log($"[SPAWN] Client NetID: {networkClientId} is the Host. Using local UGS ID: '{ugsPlayerId}'");
            }
            else
            {
                ugsPlayerId = LobbyManager.Instance.GetUgsIdFromClientId(networkClientId);
                Debug.Log($"[SPAWN] Client NetID: {networkClientId} maps to remote UGS ID: '{ugsPlayerId}'");
            }

            if (string.IsNullOrEmpty(ugsPlayerId))
            {
                Debug.LogWarning($"[SPAWN] No explicit UGS mapping found for Client: {networkClientId}. Aborting.");
                return;
            }

            Player targetLobbyPlayerData = null;
            foreach (Player lobbyPlayer in activeLobby.Players)
            {
                if (lobbyPlayer.Id == ugsPlayerId)
                {
                    targetLobbyPlayerData = lobbyPlayer;
                    break;
                }
            }

            if (targetLobbyPlayerData == null)
            {
                Debug.LogWarning($"[SPAWN] UGS ID {ugsPlayerId} could not be found in active Lobby records.");
                return;
            }

            string playerTeam = targetLobbyPlayerData.Data.ContainsKey(LobbyManager.KEY_PLAYER_TEAM)
                ? targetLobbyPlayerData.Data[LobbyManager.KEY_PLAYER_TEAM].Value : "Unassigned";

            // Default target option shifts to "HumanPlayer" string format
            string prefabChoice = targetLobbyPlayerData.Data.ContainsKey(LobbyManager.KEY_PLAYER_PREFAB)
                ? targetLobbyPlayerData.Data[LobbyManager.KEY_PLAYER_PREFAB].Value : "HumanPlayer";

            Debug.Log($"[SPAWN] Spawning Client {networkClientId}: Team={playerTeam}, PrefabChoice={prefabChoice}");

            Transform spawnPoint = DetermineSpawnTransform(playerTeam);
            Vector3 position = spawnPoint != null ? spawnPoint.position : Vector3.zero;
            Quaternion rotation = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;

            GameObject spawnedObjectPrefab = null;

            // Evaluate data configurations 
            if (prefabChoice == "HumanPlayer" || prefabChoice == "You")
            {
                spawnedObjectPrefab = defaultHumanPrefab;
            }
            else
            {
                GameObject botResource = Resources.Load<GameObject>($"BotPrefabs/{prefabChoice}");
                if (botResource != null)
                {
                    spawnedObjectPrefab = botResource;
                }
                else
                {
                    Debug.LogWarning($"[SPAWN] 'BotPrefabs/{prefabChoice}' missing from Resources! Reverting to human fallback.");
                    spawnedObjectPrefab = defaultHumanPrefab;
                }
            }

            if (spawnedObjectPrefab == null)
            {
                Debug.LogError($"[SPAWN] Cannot spawn! Target prefabs are completely unassigned.");
                return;
            }

            Debug.Log($"[SPAWN] Instantiating prefab '{spawnedObjectPrefab.name}' locally on server at position {position}.");
            GameObject runtimeInstance = Instantiate(spawnedObjectPrefab, position, rotation);
            NetworkObject networkObjectComponent = runtimeInstance.GetComponent<NetworkObject>();

            if (networkObjectComponent != null)
            {
                networkObjectComponent.SpawnAsPlayerObject(networkClientId, true);
                Debug.Log($"[SPAWN] Success spawning for Client {networkClientId}!");

                // --- LOG MATCH START WHEN HOST INSTANTIATES ---
                if (networkClientId == NetworkManager.ServerClientId && MatchVotingDataLogger.Instance != null)
                {
                    MatchVotingDataLogger.Instance.RecordMatchStart(activeLobby);
                }
            }
            else
            {
                Debug.LogError($"[SPAWN] Prefab missing critical NetworkObject component! {spawnedObjectPrefab.name}");
                Destroy(runtimeInstance);
            }
        }

        private Transform DetermineSpawnTransform(string playerTeam)
        {
            if (playerTeam == "Red")
            {
                if (redTeamSpawnPoints.Count == 0) return GetFallbackSpawnPoint();
                Transform spot = redTeamSpawnPoints[redIndexOffset % redTeamSpawnPoints.Count];
                redIndexOffset++;
                return spot;
            }
            else if (playerTeam == "Blue")
            {
                if (blueTeamSpawnPoints.Count == 0) return GetFallbackSpawnPoint();
                Transform spot = blueTeamSpawnPoints[blueIndexOffset % blueTeamSpawnPoints.Count];
                blueIndexOffset++;
                return spot;
            }
            return GetFallbackSpawnPoint();
        }

        private Transform GetFallbackSpawnPoint()
        {
            if (blueTeamSpawnPoints.Count > 0) return blueTeamSpawnPoints[0];
            if (redTeamSpawnPoints.Count > 0) return redTeamSpawnPoints[0];
            return null;
        }

        private void OnDrawGizmos()
        {
            DrawTeamGizmos(redTeamSpawnPoints, redTeamGizmoColor);
            DrawTeamGizmos(blueTeamSpawnPoints, blueTeamGizmoColor);
        }

        private void DrawTeamGizmos(List<Transform> points, Color teamColor)
        {
            if (points == null) return;
            Gizmos.color = teamColor;

            foreach (Transform t in points)
            {
                if (t != null)
                {
                    Matrix4x4 rotationMatrix = Matrix4x4.TRS(t.position, t.rotation, t.localScale);
                    Gizmos.matrix = rotationMatrix;
                    Gizmos.DrawWireCube(new Vector3(0, playerSizeGizmo.y / 2, 0), playerSizeGizmo);
                    Gizmos.DrawLine(new Vector3(0, playerSizeGizmo.y / 2, 0), new Vector3(0, playerSizeGizmo.y / 2, 0.75f));
                }
            }
        }
    }
}