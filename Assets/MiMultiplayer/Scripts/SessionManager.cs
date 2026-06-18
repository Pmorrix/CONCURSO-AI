using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

public class SessionManager : Singleton<SessionManager>
{

    public ISession ActiveSession { get; private set; }

    // Constants for global custom session attributes
    public const string MAP_INDEX_KEY = "SelectedMapIndex";

    public const string TEAM_KEY = "PlayerTeam";

    public const string playerNamePropertyKey = "playerName";

    // Expose structural properties cleanly to UI layer interfaces
    
    public bool IsHost => ActiveSession != null && ActiveSession.IsHost;
    public string CurrentRoomCode => ActiveSession != null ? ActiveSession.Code : string.Empty;

    private async void Start()
    {
        await InitializeUnityServicesAsync();
    }

    async Task<Dictionary<string, PlayerProperty>> GetPlayerProperties(string teamName)
    {
        var playerName = await AuthenticationService.Instance.GetPlayerNameAsync();
        var playerNameProperty = new PlayerProperty(playerName, VisibilityPropertyOptions.Member);
        var teamProperty = new PlayerProperty(teamName, VisibilityPropertyOptions.Member);

        return new Dictionary<string, PlayerProperty>
        {
            { playerNamePropertyKey, playerNameProperty },
            { TEAM_KEY, teamProperty }
        };
    }


    private async Task InitializeUnityServicesAsync()
    {
        try
        {
            if (UnityServices.State == ServicesInitializationState.Uninitialized)
            {
                var options = new InitializationOptions();
                #if UNITY_EDITOR
                // Keeps local multiplayer testing profiles isolated on the same physical desktop
                options.SetProfile(UnityEngine.Random.Range(0, 1000).ToString());
                #endif
                await UnityServices.InitializeAsync(options);
            }

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
                Debug.Log($"SessionManager: Authenticated as player ID: {AuthenticationService.Instance.PlayerId}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"SessionManager: Critical Platform Initialization Failure: {e.Message}");
        }
    }

    /// Dictates network initialization logic to host an online matchmaking session instance.
    public async Task<bool> StartSessionAsHost(string sessionName, int maxPlayers, int mapIndex)
    {
        if (NetworkManager.Singleton == null) return false;

        try
        {
            var customProperties = new Dictionary<string, SessionProperty>
            {
                { MAP_INDEX_KEY, new SessionProperty(mapIndex.ToString(), VisibilityPropertyOptions.Public) }
            };

            var options = new SessionOptions
            {
                Name = sessionName,
                MaxPlayers = maxPlayers,
                IsPrivate = false,
                SessionProperties = customProperties
            };

            // Allocate native routing structures under the hood automatically
            options.WithRelayNetwork();

            Debug.Log($"SessionManager: Provisioning cloud server space for '{sessionName}'...");
            ActiveSession = await MultiplayerService.Instance.CreateSessionAsync(options);

            if (NetworkManager.Singleton.StartHost())
            {
                Debug.Log($"SessionManager: Host started successfully. Room code allocated: {ActiveSession.Code}");
                return true;
            }

            Debug.LogError("SessionManager: Netcode failed to assign internal local Host initialization paths.");
            return false;
        }
        catch (SessionException e)
        {
            Debug.LogError($"SessionManager: Host generation call rejected: {e.Message} ");
            return false;
        }
    }

    /// Connects to a matching background session instance container using an explicit 6-digit alphanumerical room code string.
    public async Task<bool> JoinSessionByCode(string joinCode)
    {
        if (NetworkManager.Singleton == null) return false;

        try
        {
            var joinOptions = new JoinSessionOptions();

            Debug.Log($"SessionManager: Dialing background cloud address space connection code: {joinCode}...");
            ActiveSession = await MultiplayerService.Instance.JoinSessionByCodeAsync(joinCode, joinOptions);

            if (NetworkManager.Singleton.StartClient())
            {
                Debug.Log("SessionManager: Local client tracking hook successfully mounted to Relay transport.");
                return true;
            }

            Debug.LogError("SessionManager: Netcode network pipeline assembly encountered an error mapping local client loops.");
            return false;
        }
        catch (SessionException e)
        {
            Debug.LogError($"SessionManager: Handshake connection step aborted by remote service: {e.Message} ");
            return false;
        }
    }

    /// Explicitly closes network communication handles and resets background cloud session registration maps.
    public async Task LeaveSession()
    {
        if (ActiveSession == null) return;

        try
        {
            Debug.Log($"SessionManager: Severing membership registration connections with session reference ID: {ActiveSession.Id}");

            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.Shutdown();
            }

            await ActiveSession.LeaveAsync();
        }
        catch (SessionException e)
        {
            Debug.LogWarning($"SessionManager: Issues flagged during cleanup tracking execution calls: {e.Message} ");
        }
        finally
        {
            ActiveSession = null;
        }
    }

    /// Utility parsing mechanism to securely fetch custom key string data profiles written inside an active session.
    public string GetSessionPropertyValue(string propertyKey)
    {
        if (ActiveSession != null && ActiveSession.Properties.TryGetValue(propertyKey, out var valueNode))
        {
            return valueNode.Value;
        }
        return string.Empty;
    }


    /// Updates the local player's custom team property inside the active session.
    public async Task UpdateLocalPlayerTeam(string teamName)
    {
        if (ActiveSession == null) return;

        try
        {
            var localPlayer = ActiveSession.CurrentPlayer;
            if (localPlayer != null)
            {
                // 1. Build your updated player properties dictionary container
                var updatedProperties = await GetPlayerProperties(teamName);

                // 2. Assign the modifications locally to the player entity first
                localPlayer.SetProperties(updatedProperties);

                // 3. Fire the save task with no arguments to sync everything up
                await ActiveSession.SaveCurrentPlayerDataAsync();

                Debug.Log($"SessionManager: Local player team updated to {teamName}");
            }
        }
        catch (SessionException e)
        {
            Debug.LogError($"SessionManager: Failed to update player team properties: {e.Message}");
        }
    }
}