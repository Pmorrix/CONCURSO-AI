using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unity.FPS.Core
{
    public class LobbyManager : MonoBehaviour
    {
        public static LobbyManager Instance { get; private set; }
        public static string RelayJoinCode { get; private set; }

        // Synchronized string keys
        public const string KEY_PLAYER_NAME = "PlayerName";
        public const string KEY_PLAYER_PREFAB = "PlayerPrefab";
        public const string KEY_PLAYER_TEAM = "PlayerTeam";
        public const string KEY_CUSTOM_JOIN_CODE = "CustomJoinCode";
        public const string KEY_MAP_NAME = "MapName";
        public const string KEY_START_GAME = "StartGame";
        public const string KEY_RELAY_JOIN_CODE = "RelayJoinCode";

        // Events for UI communication
        public event EventHandler OnLeftLobby;
        public event EventHandler<LobbyEventArgs> OnJoinedLobby;
        public event EventHandler<LobbyEventArgs> OnJoinedLobbyUpdate;
        public event EventHandler<LobbyEventArgs> OnKickedFromLobby;

        public event Action OnServerAllClientsLoadedScene;

        public class LobbyEventArgs : EventArgs
        {
            public Lobby lobby;
            public string playerId;
        }

        private float heartbeatTimer;
        private float lobbyPollTimer;
        private Lobby joinedLobby;
        private string playerName;
        private string selectedPrefabName = "You";
        private string selectedInitialTeam = "Unassigned\"";
        private bool alreadyStartedGame;

        private Dictionary<ulong, string> clientIdToUgsIdMap = new Dictionary<ulong, string>();
        private Dictionary<ulong, string> clientIdToPlayerNameMap = new Dictionary<ulong, string>();
        public string LocalMatchResultStatus { get; set; } = "Match Draw";

        public bool ShouldShowDisconnectFeedback { get; private set; } = false;
        public string DisconnectFeedbackMessage { get; private set; } = "";




        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            HandleLobbyHeartbeat();
            HandleLobbyPolling();
        }

        public async Task Authenticate(string pName, string pPrefab)
        {
            playerName = pName.Replace(" ", "_");
            selectedPrefabName = pPrefab; // Cache the dropdown selection text

            if (UnityServices.State == ServicesInitializationState.Uninitialized)
            {
                InitializationOptions initializationOptions = new InitializationOptions();
                initializationOptions.SetProfile(playerName);
                await UnityServices.InitializeAsync(initializationOptions);
            }

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
                Debug.Log($"Signed in anonymously as: {AuthenticationService.Instance.PlayerId}");
            }
        }

        private async void HandleLobbyHeartbeat()
        {
            if (IsLobbyHost() && joinedLobby != null)
            {
                heartbeatTimer -= Time.deltaTime;
                if (heartbeatTimer < 0f)
                {
                    float heartbeatTimerMax = 15f;
                    heartbeatTimer = heartbeatTimerMax;
                    try
                    {
                        await LobbyService.Instance.SendHeartbeatPingAsync(joinedLobby.Id);
                    }
                    catch (LobbyServiceException e)
                    {
                        Debug.LogWarning("Heartbeat failed: " + e.Message);
                    }
                }
            }
        }

        private async void HandleLobbyPolling()
        {
            if (joinedLobby == null || string.IsNullOrEmpty(joinedLobby.Id)) return;

            lobbyPollTimer -= Time.deltaTime;
            if (lobbyPollTimer < 0f)
            {
                float lobbyPollTimerMax = 1.1f;
                lobbyPollTimer = lobbyPollTimerMax;

                try
                {
                    string targetLobbyId = joinedLobby.Id;
                    if (string.IsNullOrEmpty(targetLobbyId)) return;

                    Lobby freshlyPolledLobby = await LobbyService.Instance.GetLobbyAsync(targetLobbyId);
                    if (joinedLobby == null) return;

                    joinedLobby = freshlyPolledLobby;
                    OnJoinedLobbyUpdate?.Invoke(this, new LobbyEventArgs { lobby = joinedLobby });

                    if (!IsLobbyHost())
                    {
                        if (joinedLobby.Data != null && joinedLobby.Data.ContainsKey(KEY_START_GAME) && joinedLobby.Data[KEY_START_GAME].Value == "1")
                        {
                            if (!alreadyStartedGame)
                            {
                                alreadyStartedGame = true;
                            }
                        }
                    }

                    if (!IsPlayerInLobby())
                    {
                        Debug.Log("Kicked from Lobby!");
                        string myKickedId = AuthenticationService.Instance.PlayerId;
                        OnKickedFromLobby?.Invoke(this, new LobbyEventArgs { lobby = joinedLobby, playerId = myKickedId });
                        joinedLobby = null;
                    }
                }
                catch (LobbyServiceException e)
                {
                    Debug.LogWarning("Lobby API Polling network error: " + e.Message);
                }
                catch (NullReferenceException e)
                {
                    Debug.LogWarning("Handled a minor race-condition mismatch during lobby poll clearance." + e.Message);
                }
            }
        }

        public bool IsLobbyHost()
        {
            return joinedLobby != null && joinedLobby.HostId == AuthenticationService.Instance.PlayerId;
        }

        private bool IsPlayerInLobby()
        {
            if (joinedLobby?.Players == null) return false;
            foreach (Player player in joinedLobby.Players)
            {
                if (player.Id == AuthenticationService.Instance.PlayerId) return true;
            }
            return false;
        }

        private Player CreatePlayerObject()
        {
            // If "You" was chosen, save it as "HumanPlayer", otherwise save the string matching your prefab name
            string prefabValueToSave = selectedPrefabName == "You" ? "HumanPlayer" : selectedPrefabName;

            Debug.Log($"[LOBBY] Packaging Player UGS Data profile: Name={playerName}, Team={selectedInitialTeam}, Prefab={prefabValueToSave}");

            return new Player(AuthenticationService.Instance.PlayerId, null, new Dictionary<string, PlayerDataObject> {
                { KEY_PLAYER_NAME, new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, playerName) },
                { KEY_PLAYER_TEAM, new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, selectedInitialTeam) },
                { KEY_PLAYER_PREFAB, new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, prefabValueToSave) }
            });
        }

        // --- HOST METHOD ---
        public async Task CreateCustomLobby(string customJoinCode, int maxPlayers, string mapName, string hostTeam)
        {
            selectedInitialTeam = hostTeam;
            Player player = CreatePlayerObject();

            CreateLobbyOptions options = new CreateLobbyOptions
            {
                Player = player,
                IsPrivate = false,
                Data = new Dictionary<string, DataObject> {
                    { KEY_CUSTOM_JOIN_CODE, new DataObject(DataObject.VisibilityOptions.Public, customJoinCode, DataObject.IndexOptions.S1) },
                    { KEY_MAP_NAME, new DataObject(DataObject.VisibilityOptions.Public, mapName) },
                    { KEY_START_GAME, new DataObject(DataObject.VisibilityOptions.Public, "0") },
                    { KEY_RELAY_JOIN_CODE, new DataObject(DataObject.VisibilityOptions.Member, "") }
                }
            };

            Lobby lobby = await LobbyService.Instance.CreateLobbyAsync(customJoinCode, maxPlayers, options);
            joinedLobby = lobby;

            OnJoinedLobby?.Invoke(this, new LobbyEventArgs { lobby = lobby });
            Debug.Log($"Successfully created Lobby room with custom code: {customJoinCode}");
        }

        // --- CLIENT METHOD ---
        public async Task JoinLobbyByCustomCode(string customJoinCode)
        {
            selectedInitialTeam = "Unassigned";

            try
            {
                QueryLobbiesOptions queryOptions = new QueryLobbiesOptions
                {
                    Count = 1,
                    Filters = new List<QueryFilter> { new QueryFilter(QueryFilter.FieldOptions.S1, customJoinCode, QueryFilter.OpOptions.EQ) }
                };

                QueryResponse response = await LobbyService.Instance.QueryLobbiesAsync(queryOptions);

                if (response.Results.Count == 0)
                {
                    throw new Exception("Lobby could not be found!");
                }

                Lobby targetedLobby = response.Results[0];

                if (targetedLobby.Data != null && targetedLobby.Data.ContainsKey(KEY_START_GAME) && targetedLobby.Data[KEY_START_GAME].Value == "1")
                {
                    throw new Exception("This lobby's game has already started!");
                }

                if (targetedLobby.AvailableSlots == 0)
                {
                    throw new Exception("This lobby is full!");
                }

                if (targetedLobby.Players != null)
                {
                    foreach (var existingPlayer in targetedLobby.Players)
                    {
                        if (existingPlayer.Data != null && existingPlayer.Data.ContainsKey(KEY_PLAYER_NAME))
                        {
                            string existingName = existingPlayer.Data[KEY_PLAYER_NAME].Value;
                            if (string.Equals(existingName, playerName, StringComparison.OrdinalIgnoreCase))
                            {
                                throw new DuplicateNameException("Someone in this lobby already has that name!");
                            }
                        }
                    }
                }

                int redCount = 0;
                int blueCount = 0;
                int maxTeamSize = targetedLobby.MaxPlayers / 2;

                if (targetedLobby.Players != null)
                {
                    foreach (var p in targetedLobby.Players)
                    {
                        if (p.Data != null && p.Data.ContainsKey(KEY_PLAYER_TEAM))
                        {
                            string team = p.Data[KEY_PLAYER_TEAM].Value;
                            if (team == "Red") redCount++;
                            else if (team == "Blue") blueCount++;
                        }
                    }
                }

                List<string> availableTeams = new List<string>();
                if (redCount < maxTeamSize) availableTeams.Add("Red");
                if (blueCount < maxTeamSize) availableTeams.Add("Blue");

                if (availableTeams.Count == 0)
                {
                    selectedInitialTeam = redCount <= blueCount ? "Red" : "Blue";
                }
                else
                {
                    int randomIndex = UnityEngine.Random.Range(0, availableTeams.Count);
                    selectedInitialTeam = availableTeams[randomIndex];
                }

                Player player = CreatePlayerObject();
                JoinLobbyByIdOptions joinOptions = new JoinLobbyByIdOptions { Player = player };

                joinedLobby = await LobbyService.Instance.JoinLobbyByIdAsync(targetedLobby.Id, joinOptions);
                OnJoinedLobby?.Invoke(this, new LobbyEventArgs { lobby = joinedLobby });
            }
            catch (LobbyServiceException e)
            {
                throw new Exception($"Network failure: {e.Reason}");
            }
        }

        public async Task UpdatePlayerTeam(string targetTeam)
        {
            if (joinedLobby == null) return;
            try
            {
                UpdatePlayerOptions options = new UpdatePlayerOptions
                {
                    Data = new Dictionary<string, PlayerDataObject> {
                        { KEY_PLAYER_TEAM, new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, targetTeam) }
                    }
                };

                joinedLobby = await LobbyService.Instance.UpdatePlayerAsync(joinedLobby.Id, AuthenticationService.Instance.PlayerId, options);
                OnJoinedLobbyUpdate?.Invoke(this, new LobbyEventArgs { lobby = joinedLobby });
            }
            catch (LobbyServiceException e)
            {
                Debug.LogError($"Failed updating team: {e.Message}");
            }
        }

        // --- HOST START GAME SYSTEM ---J
        public async void StartGameSystem()
        {
            if (!IsLobbyHost()) return;

            if (joinedLobby == null)
            {
                Debug.LogError("[START GAME] Cannot start match because joinedLobby is null!");
                return;
            }

            try
            {
                alreadyStartedGame = true;
                clientIdToUgsIdMap.Clear();

                // Secure the connection slot allocation math so it never requests <= 0 slots
                int allocationSlots = joinedLobby.MaxPlayers - 1;
                if (allocationSlots < 1) allocationSlots = 1;

                Allocation allocation = await RelayService.Instance.CreateAllocationAsync(allocationSlots);

                string relayJoinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

                RelayServerData relayServerData = AllocationUtils.ToRelayServerData(allocation, "dtls");

                if (NetworkManager.Singleton == null)
                {
                    Debug.LogError("[START GAME] NetworkManager.Singleton is missing from your scene!");
                    return;
                }
                NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(relayServerData);

                NetworkManager.Singleton.ConnectionApprovalCallback = ApprovalCheck;

                // =========================================================================
                //  Spin up Netcode to initialize managers
                // =========================================================================
                NetworkManager.Singleton.StartHost();

                // Now that StartHost() has completed, the SceneManager is safely instantiated
                NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += OnSceneLoadEventCompleted;

                Lobby lobby = await LobbyService.Instance.UpdateLobbyAsync(joinedLobby.Id, new UpdateLobbyOptions
                {
                    Data = new Dictionary<string, DataObject> {
                        { KEY_START_GAME, new DataObject(DataObject.VisibilityOptions.Public, "1") },
                        { KEY_RELAY_JOIN_CODE, new DataObject(DataObject.VisibilityOptions.Member, relayJoinCode) }
                    }
                });
                joinedLobby = lobby;

                string targetMap = joinedLobby.Data[KEY_MAP_NAME].Value;
                NetworkManager.Singleton.SceneManager.LoadScene(targetMap, LoadSceneMode.Single);
            }
            catch (Exception e)
            {
                Debug.LogError("Host failed starting match via Relay: " + e.Message);
                alreadyStartedGame = false;
            }
        }


        public void JoinRelayAndMap()
        {
            alreadyStartedGame = true;
            try
            {
                string relayJoinCode = joinedLobby.Data[KEY_RELAY_JOIN_CODE].Value;
                var task = ConnectClientAsync(relayJoinCode);
            }
            catch (Exception e)
            {
                Debug.LogError("Client failed joining match via Relay: " + e.Message);
                alreadyStartedGame = false;
            }
        }

        private async Task ConnectClientAsync(string relayJoinCode)
        {
            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(relayJoinCode);
            RelayServerData relayServerData = AllocationUtils.ToRelayServerData(joinAllocation, "dtls");

            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(relayServerData);

            // Send local UGS Player ID converted to bytes as the connection payload
            string ugsPlayerId = AuthenticationService.Instance.PlayerId;
            NetworkManager.Singleton.NetworkConfig.ConnectionData = System.Text.Encoding.UTF8.GetBytes(ugsPlayerId);

            NetworkManager.Singleton.StartClient();
        }

        private void ApprovalCheck(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            response.Approved = true;
            response.CreatePlayerObject = false; // Disable automatic default prefab spawning

            // Extract the payload sent from incoming user
            string ugsPlayerId = System.Text.Encoding.UTF8.GetString(request.Payload);

            if (request.ClientNetworkId == NetworkManager.ServerClientId)
            {
                ugsPlayerId = AuthenticationService.Instance.PlayerId;
            }

            // Map Netcode Client ID to UGS Player ID
            clientIdToUgsIdMap[request.ClientNetworkId] = ugsPlayerId;

            // Map the ClientId to their actual Player Name from the Lobby payload ---
            string discoveredName = "Unknown Player";
            if (joinedLobby != null && joinedLobby.Players != null)
            {
                foreach (var player in joinedLobby.Players)
                {
                    if (player.Id == ugsPlayerId && player.Data.ContainsKey(KEY_PLAYER_NAME))
                    {
                        discoveredName = player.Data[KEY_PLAYER_NAME].Value;
                        break;
                    }
                }
            }
            clientIdToPlayerNameMap[request.ClientNetworkId] = discoveredName;
        }

        private void OnSceneLoadEventCompleted(string sceneName, LoadSceneMode loadSceneMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
        {
            if (!NetworkManager.Singleton.IsServer) return;

            // Use the action event to cross the asmdef boundary cleanly
            OnServerAllClientsLoadedScene?.Invoke();

            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= OnSceneLoadEventCompleted;
        }

        // Public helper for the Spawner to safely request who a client is via their UGS Profile.
        public string GetUgsIdFromClientId(ulong clientId)
        {
            if (clientIdToUgsIdMap.TryGetValue(clientId, out string ugsId))
            {
                return ugsId;
            }
            return null;
        }

        // Public helper so that notifications can get the player names
        public string GetPlayerNameFromClientId(ulong clientId)
        {
            if (clientIdToPlayerNameMap.TryGetValue(clientId, out string pName))
            {
                return pName;
            }
            return "A player";
        }


        public async void LeaveLobby()
        {
            if (NetworkManager.Singleton != null)
            {
                if (NetworkManager.Singleton.SceneManager != null)
                {
                    NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= OnSceneLoadEventCompleted;
                }

                NetworkManager.Singleton.Shutdown();
            }

            if (joinedLobby != null)
            {
                try
                {
                    await LobbyService.Instance.RemovePlayerAsync(joinedLobby.Id, AuthenticationService.Instance.PlayerId);
                }
                catch (LobbyServiceException e)
                {
                    Debug.LogError("Cloud removal failed: " + e);
                }
            }

            joinedLobby = null;
            alreadyStartedGame = false;
            clientIdToUgsIdMap.Clear();
            OnLeftLobby?.Invoke(this, EventArgs.Empty);
        }

        // For post-match cleanup (deletes the lobby and purges the old game code completely)
        public async void LeaveLobbyAfterStarting()
        {
            if (NetworkManager.Singleton != null)
            {
                if (NetworkManager.Singleton.SceneManager != null)
                {
                    NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= OnSceneLoadEventCompleted;
                }

                NetworkManager.Singleton.Shutdown();
            }

            if (joinedLobby != null)
            {
                try
                {
                    if (IsLobbyHost())
                    {
                        Debug.Log($"[LOBBY] Match ended. Host is deleting room: {joinedLobby.Id}");
                        await LobbyService.Instance.DeleteLobbyAsync(joinedLobby.Id);
                    }
                    else
                    {
                        Debug.Log($"[LOBBY] Match ended. Client is removing self from room: {joinedLobby.Id}");
                        await LobbyService.Instance.RemovePlayerAsync(joinedLobby.Id, AuthenticationService.Instance.PlayerId);
                    }
                }
                catch (LobbyServiceException e)
                {
                    // Catch race condition gracefully ---
                    if (e.Reason == LobbyExceptionReason.LobbyNotFound || e.Message.Contains("404"))
                    {
                        Debug.Log("[LOBBY] Notice: Lobby was already cleared out by the Host. Proceeding to menu cleanly.");
                    }
                    else
                    {
                        Debug.LogError("Cloud cleanup following match failed: " + e);
                    }
                }
            }

            // Fully reset local variables to a clean state
            joinedLobby = null;
            alreadyStartedGame = false;
            clientIdToUgsIdMap.Clear();

            OnLeftLobby?.Invoke(this, EventArgs.Empty);
        }


        public async void KickPlayer(string playerId)
        {
            if (IsLobbyHost() && joinedLobby != null)
            {
                try
                {
                    await LobbyService.Instance.RemovePlayerAsync(joinedLobby.Id, playerId);
                }
                catch (LobbyServiceException e)
                {
                    Debug.Log(e);
                }
            }
        }

        public Lobby GetJoinedLobby() => joinedLobby;

        public void ClearJoinedLobbyReference()
        {
            joinedLobby = null;
            alreadyStartedGame = false;
        }

        public void SetDisconnectFeedback(string message)
        {
            ShouldShowDisconnectFeedback = true;
            DisconnectFeedbackMessage = message;
        }

        public void ClearDisconnectFeedback()
        {
            ShouldShowDisconnectFeedback = false;
            DisconnectFeedbackMessage = "";
        }

    }

    public class DuplicateNameException : Exception
    {
        public DuplicateNameException(string message) : base(message) { }
    }
}