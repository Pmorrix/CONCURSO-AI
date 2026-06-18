using System;
using TMPro;
using System.Collections;
using Unity.Services.Authentication;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.UI;

namespace Unity.FPS.Core
{
    public class LobbyRoomUI : MonoBehaviour
    {
        public static LobbyRoomUI Instance { get; private set; }

        [Header("Panel Containers")]
        [SerializeField] private GameObject lobbyMainContentPanel;
        [SerializeField] private GameObject kickedPanel;

        [Header("Table References")]
        [SerializeField] private Transform container;
        [SerializeField] private Transform playerSingleTemplate;

        [Header("Lobby Labels")]
        [SerializeField] private TextMeshProUGUI lobbyCodeText;
        [SerializeField] private TextMeshProUGUI playerCountText;
        [SerializeField] private TextMeshProUGUI mapNameText;

        [Header("Room Actions")]
        [SerializeField] private Button leaveLobbyButton;
        [SerializeField] private Button startGameButton;

        [Header("Kick Feedback UI")]
        [SerializeField] private float kickScreenDuration = 2.0f; // Time in seconds to show the panel

        private bool isTransitioningForGameStart = false;

        private void Awake()
        {
            Instance = this;
            playerSingleTemplate.gameObject.SetActive(false);

            if (kickedPanel != null) kickedPanel.SetActive(false);

            // Start completely hidden out of sight
            Hide();
        }

        private void Start()
        {
            if (LobbyManager.Instance != null)
            {
                leaveLobbyButton.onClick.AddListener(() =>
                {
                    leaveLobbyButton.interactable = false;
                    LobbyManager.Instance.LeaveLobby();
                });

                // 1. HOST HANDLER: Fade out first, then launch the Netcode server system
                startGameButton.onClick.AddListener(() =>
                {
                    startGameButton.interactable = false;
                    leaveLobbyButton.interactable = false;
                    isTransitioningForGameStart = true;

                    if (MenuEffectsManager.Instance != null)
                    {
                        MenuEffectsManager.Instance.StartMatchTransitions(() =>
                        {
                            LobbyManager.Instance.StartGameSystem();
                        });
                    }
                    else
                    {
                        LobbyManager.Instance.StartGameSystem();
                    }
                });

                if (startGameButton != null) startGameButton.gameObject.SetActive(false);

                LobbyManager.Instance.OnJoinedLobby += OnLobbyUpdated;

                // 2. CLIENT HANDLER INTERCEPTION: Listen to the lobby updates to catch the start flag early
                LobbyManager.Instance.OnJoinedLobbyUpdate += (sender, e) =>
                {
                    // If we are a client, look for the host's start command flag
                    if (!LobbyManager.Instance.IsLobbyHost() && e.lobby.Data.ContainsKey(LobbyManager.KEY_START_GAME) && e.lobby.Data[LobbyManager.KEY_START_GAME].Value == "1")
                    {
                        if (!isTransitioningForGameStart)
                        {
                            isTransitioningForGameStart = true;

                            // Prevent any more button interactions
                            if (leaveLobbyButton != null) leaveLobbyButton.interactable = false;

                            if (MenuEffectsManager.Instance != null)
                            {
                                MenuEffectsManager.Instance.StartMatchTransitions(() =>
                                {
                                    // Trigger the actual Netcode connection method manually after fading to black
                                    LobbyManager.Instance.JoinRelayAndMap();
                                });
                            }
                            else
                            {
                                LobbyManager.Instance.JoinRelayAndMap();
                            }
                        }
                    }
                    else
                    {
                        // Run normal room visual updates if the game hasn't started yet
                        OnLobbyUpdated(sender, e);
                    }
                };

                LobbyManager.Instance.OnLeftLobby += OnPlayerLeftLobby;
                LobbyManager.Instance.OnKickedFromLobby += OnPlayerKicked;

                if (LobbyManager.Instance.ShouldShowDisconnectFeedback)
                {
                    string incomingMessage = LobbyManager.Instance.DisconnectFeedbackMessage;
                    LobbyManager.Instance.ClearDisconnectFeedback();
                    StartCoroutine(ShowCustomNoticeSequence(incomingMessage));
                }
            }
        }

        private void OnDestroy()
        {
            if (LobbyManager.Instance != null)
            {
                LobbyManager.Instance.OnJoinedLobby -= OnLobbyUpdated;
                LobbyManager.Instance.OnJoinedLobbyUpdate -= OnLobbyUpdated;
                LobbyManager.Instance.OnLeftLobby -= OnPlayerLeftLobby;
                LobbyManager.Instance.OnKickedFromLobby -= OnPlayerKicked;

                Debug.Log("LobbyRoomUIController: OnDestroy has occurred");
            }
        }

        private void OnPlayerLeftLobby(object sender, EventArgs e)
        {
            if (this == null) return;
            if (leaveLobbyButton != null) leaveLobbyButton.interactable = true;
            if (LobbyMenuUI.Instance != null) LobbyMenuUI.Instance.ShowMainMenu();
        }

        private void OnPlayerKicked(object sender, LobbyManager.LobbyEventArgs e)
        {
            if (this == null) return;
            if (leaveLobbyButton != null) leaveLobbyButton.interactable = true;

            string myId = AuthenticationService.Instance.PlayerId;
            if (e.playerId == myId)
            {
                // Start the sequence to flash the screen and route back to the menu
                StartCoroutine(ShowKickedSequence());
            }
            else
            {
                // If it wasn't me who got kicked, just refresh the display normally
                UpdateRoomDisplay(e.lobby);
            }
        }

        // Coroutine sequence handling the screen flash timing safely
        private IEnumerator ShowKickedSequence()
        {
            // 1. Turn on the kicked panel overlay and forcefully restore its text
            if (kickedPanel != null)
            {
                TextMeshProUGUI panelText = kickedPanel.GetComponentInChildren<TextMeshProUGUI>();
                if (panelText != null)
                {
                    panelText.text = "You have been kicked from this lobby";
                }

                kickedPanel.SetActive(true);
            }

            // 2. Hide the actual lobby room details behind it
            Hide();

            // 3. Pause execution right here for your designated duration
            yield return new WaitForSeconds(kickScreenDuration);

            // 4. Turn off the overlay panel 
            if (kickedPanel != null)
            {
                kickedPanel.SetActive(false);
            }

            // 5. Reveal the underlying main menu panel cleanly
            if (LobbyMenuUI.Instance != null)
            {
                LobbyMenuUI.Instance.ShowMainMenu();
            }
        }

        public void Show()
        {
            if (lobbyMainContentPanel != null) lobbyMainContentPanel.SetActive(true);
        }
        public void Hide()
        {
            if (lobbyMainContentPanel != null) lobbyMainContentPanel.SetActive(false);
        }

        // Allows LobbyMenuUI to manually kick-start the display if event execution timing varies
        public void RefreshFromMenu()
        {
            if (LobbyManager.Instance != null)
            {
                Lobby lobby = LobbyManager.Instance.GetJoinedLobby();
                if (lobby != null)
                {
                    UpdateRoomDisplay(lobby);
                }
            }
        }

        private void OnLobbyUpdated(object sender, LobbyManager.LobbyEventArgs e)
        {
            if (e.lobby == null || LobbyManager.Instance.GetJoinedLobby() == null)
            {
                return;
            }

            UpdateRoomDisplay(e.lobby);
        }

        private void UpdateRoomDisplay(Lobby lobby)
        {
            ClearTable();

            string myId = AuthenticationService.Instance.PlayerId;

            // Validation trackers
            int redTeamCount = 0;
            int blueTeamCount = 0;

            // First Pass: Calculate team sizes and unassigned statuses
            foreach (Player player in lobby.Players)
            {
                string assignedTeam = player.Data.ContainsKey(LobbyManager.KEY_PLAYER_TEAM)
                    ? player.Data[LobbyManager.KEY_PLAYER_TEAM].Value : "Unassigned";

                if (assignedTeam == "Red") redTeamCount++;
                else if (assignedTeam == "Blue") blueTeamCount++;
            }

            // Second Pass: Build our single-table visual list items
            foreach (Player player in lobby.Players)
            {
                if (container != null)
                {
                    Transform row = Instantiate(playerSingleTemplate, container);
                    row.gameObject.SetActive(true);

                    LobbyPlayerSingleUI rowUI = row.GetComponent<LobbyPlayerSingleUI>();

                    // =========================================================================
                    // EVALUATE HOST MATCH PER ROW
                    // =========================================================================
                    // Safely compares this specific row's player ID against the master Lobby Host ID field
                    bool isThisPlayerTheHost = (player.Id == lobby.HostId);

                    // Pass the calculated state right into the single view component 
                    rowUI.UpdatePlayer(player, isThisPlayerTheHost);

                    // Host can kick anyone except themselves
                    rowUI.SetKickButtonVisibility(
                        LobbyManager.Instance.IsLobbyHost() && player.Id != myId
                    );
                }
            }

            // =========================================================================
            // HOST ONLY GAME START BUTTON VISIBILITY
            // =========================================================================
            if (startGameButton != null)
            {
                // This activates the entire GameObject only if the local player is the host
                startGameButton.gameObject.SetActive(LobbyManager.Instance.IsLobbyHost());
            }

            // Header Labels
            if (lobbyCodeText != null && lobby.Data.ContainsKey(LobbyManager.KEY_CUSTOM_JOIN_CODE))
                lobbyCodeText.text = $"ROOM CODE: {lobby.Data[LobbyManager.KEY_CUSTOM_JOIN_CODE].Value}";

            if (playerCountText != null)
                playerCountText.text = $"PLAYERS: {lobby.Players.Count} / {lobby.MaxPlayers}";

            if (mapNameText != null && lobby.Data.ContainsKey(LobbyManager.KEY_MAP_NAME))
                mapNameText.text = $"MAP: {lobby.Data[LobbyManager.KEY_MAP_NAME].Value}";

            Show();
        }

        private void ClearTable()
        {
            if (container != null)
            {
                foreach (Transform child in container)
                {
                    if (child == playerSingleTemplate) continue;
                    Destroy(child.gameObject);
                }
            }
        }

        // Coroutine sequence handling dynamic message assignments safely
        private IEnumerator ShowCustomNoticeSequence(string messageToShow)
        {
            // 1. Find the TextMeshPro component inside the kickedPanel to swap the text dynamically
            if (kickedPanel != null)
            {
                TextMeshProUGUI panelText = kickedPanel.GetComponentInChildren<TextMeshProUGUI>();
                if (panelText != null)
                {
                    panelText.text = messageToShow;
                }

                // 2. Turn on the panel overlay
                kickedPanel.SetActive(true);
            }

            // 3. Keep the underlying canvas elements hidden out of view
            Hide();

            // 4. Pause execution on-screen for your designated duration variable
            yield return new WaitForSeconds(kickScreenDuration);

            // 5. Turn off the overlay panel safely
            if (kickedPanel != null)
            {
                kickedPanel.SetActive(false);
            }

            // 6. Reset UI display focus back to the default clear Main Menu layout state
            if (LobbyMenuUI.Instance != null)
            {
                LobbyMenuUI.Instance.ShowMainMenu();
            }
        }
    }
}