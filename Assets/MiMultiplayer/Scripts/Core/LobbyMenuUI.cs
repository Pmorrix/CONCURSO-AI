using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Unity.FPS.Core
{
    public class LobbyMenuUI : MonoBehaviour
    {
        public static LobbyMenuUI Instance { get; private set; }

        [Header("Panels")]
        [SerializeField] private GameObject mainMenuPanel;
        [SerializeField] private GameObject infoPanel;
        [SerializeField] private GameObject audioSettingsPanel;
        [SerializeField] private GameObject controlsPanel;
        [SerializeField] private GameObject gameSetupPanel;
        [SerializeField] private GameObject joinPanel;
        [SerializeField] private GameObject lobbyRoomPanel;

        [Header("Main Menu Buttons")]
        [SerializeField] private Button openInfoPanelButton;
        [SerializeField] private Button openSetupPanelButton;
        [SerializeField] private Button openJoinPanelButton;
        [SerializeField] private Button openControlsPanelButton;
        [SerializeField] private Button quitButton;

        [Header("Game Setup Inputs (Host)")]
        [SerializeField] private TMP_InputField hostPlayerNameInput;
        [SerializeField] private TMP_Dropdown hostPlayerPrefabDropdown;
        [SerializeField] private TMP_InputField lobbyCodeInput;
        [SerializeField] private TMP_Dropdown maxPlayersDropdown;
        [SerializeField] private TMP_Dropdown mapDropdown;
        [SerializeField] private Button finalizeCreateLobbyButton;

        [Header("Join Inputs (Client)")]
        [SerializeField] private TMP_InputField clientPlayerNameInput;
        [SerializeField] private TMP_Dropdown clientPlayerPrefabDropdown;
        [SerializeField] private TMP_InputField joinCodeInput;
        [SerializeField] private Button finalizeJoinLobbyButton;

        [Header("Error Handling")]
        [SerializeField] private TextMeshProUGUI createErrorText;
        [SerializeField] private TextMeshProUGUI joinErrorText;

        [Header("Panel Animation Settings")]
        [SerializeField] private float slideDuration = 0.35f;
        [SerializeField] private AnimationCurve slideCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        private GameObject currentlyActivePanel;
        private System.Collections.IEnumerator panelTransitionCoroutine;

        private void Awake()
        {
            Instance = this;

            ReleaseMouseCursor();

            openInfoPanelButton.onClick.AddListener(() => SwitchPanel(infoPanel));
            openSetupPanelButton.onClick.AddListener(() => SwitchPanel(gameSetupPanel));
            openJoinPanelButton.onClick.AddListener(() => SwitchPanel(joinPanel));
            openControlsPanelButton.onClick.AddListener(() => SwitchPanel(controlsPanel));
            quitButton.onClick.AddListener(() => QuitGame());

            finalizeCreateLobbyButton.onClick.AddListener(OnCreateLobbyClicked);
            finalizeJoinLobbyButton.onClick.AddListener(OnJoinLobbyClicked);

            hostPlayerNameInput.onValueChanged.AddListener((val) => ValidateCreateInputs());
            lobbyCodeInput.onValueChanged.AddListener((val) => ValidateCreateInputs());

            clientPlayerNameInput.onValueChanged.AddListener((val) => ValidateJoinInputs());
            joinCodeInput.onValueChanged.AddListener((val) => ValidateJoinInputs());

            PopulatePrefabDropdowns();

            if (LobbyManager.Instance != null && LobbyManager.Instance.GetJoinedLobby() != null)
            {
                LobbyManager.Instance.ClearJoinedLobbyReference();
            }

            InitializePanelPositions();

            mainMenuPanel.SetActive(true);
            currentlyActivePanel = mainMenuPanel;

            ShowMainMenu();
        }

        private void OnEnable()
        {
            ReleaseMouseCursor();
        }

        private void ReleaseMouseCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void PopulatePrefabDropdowns()
        {
            List<string> dropdownOptions = new List<string> { "You" };
            GameObject[] botPrefabs = Resources.LoadAll<GameObject>("BotPrefabs");

            foreach (GameObject bot in botPrefabs)
            {
                if (bot != null)
                {
                    dropdownOptions.Add(bot.name);
                }
            }

            hostPlayerPrefabDropdown.ClearOptions();
            clientPlayerPrefabDropdown.ClearOptions();

            hostPlayerPrefabDropdown.AddOptions(dropdownOptions);
            clientPlayerPrefabDropdown.AddOptions(dropdownOptions);
        }

        private void InitializePanelPositions()
        {
            SnapPanelToCenter(mainMenuPanel);
            SnapPanelToCenter(infoPanel);
            SnapPanelToCenter(audioSettingsPanel);
            SnapPanelToCenter(controlsPanel);
            SnapPanelToCenter(gameSetupPanel);
            SnapPanelToCenter(joinPanel);
            SnapPanelToCenter(lobbyRoomPanel);

            mainMenuPanel.SetActive(false);
            infoPanel.SetActive(false);
            audioSettingsPanel.SetActive(false);
            controlsPanel.SetActive(false);
            gameSetupPanel.SetActive(false);
            joinPanel.SetActive(false);
            lobbyRoomPanel.SetActive(false);
        }

        private void SnapPanelToCenter(GameObject panel)
        {
            if (panel != null)
            {
                RectTransform rect = panel.GetComponent<RectTransform>();
                if (rect != null) rect.anchoredPosition = new Vector2(0f, rect.anchoredPosition.y);
            }
        }

        public void ShowMainMenu()
        {
            SwitchPanel(mainMenuPanel);
            HideError();

            hostPlayerNameInput.text = null;
            hostPlayerPrefabDropdown.value = 0;
            lobbyCodeInput.text = null;
            maxPlayersDropdown.value = 0;
            mapDropdown.value = 0;

            clientPlayerNameInput.text = null;
            joinCodeInput.text = null;
            clientPlayerPrefabDropdown.value = 0;

            ValidateCreateInputs();
            ValidateJoinInputs();
        }

        public void SwitchPanel(GameObject targetPanel)
        {
            if (targetPanel == currentlyActivePanel) return;

            if (panelTransitionCoroutine != null)
            {
                StopCoroutine(panelTransitionCoroutine);
            }

            // Prepare the target panel's layout position immediately before any scripts awaken it 
            if (targetPanel != null && targetPanel != mainMenuPanel)
            {
                RectTransform targetRect = targetPanel.GetComponent<RectTransform>();
                if (targetRect != null)
                {
                    // Position it off-screen left safely before it can blink into view
                    targetRect.anchoredPosition = new Vector2(-Screen.width, targetRect.anchoredPosition.y);
                }
            }

            panelTransitionCoroutine = TransitionPanelsSequence(currentlyActivePanel, targetPanel);
            StartCoroutine(panelTransitionCoroutine);
            currentlyActivePanel = targetPanel;
        }

        private System.Collections.IEnumerator TransitionPanelsSequence(GameObject oldPanel, GameObject newPanel)
        {
            float screenWidth = Screen.width;

            if (oldPanel != null)
            {
                RectTransform oldRect = oldPanel.GetComponent<RectTransform>();
                if (oldRect != null)
                {
                    Vector2 startPos = oldRect.anchoredPosition;
                    Vector2 endPos = new Vector2(-screenWidth, startPos.y);
                    float elapsedTime = 0f;

                    while (elapsedTime < slideDuration)
                    {
                        elapsedTime += Time.unscaledDeltaTime;
                        float t = slideCurve.Evaluate(elapsedTime / slideDuration);
                        oldRect.anchoredPosition = Vector2.Lerp(startPos, endPos, t);
                        yield return null;
                    }
                    oldRect.anchoredPosition = endPos;
                }
                oldPanel.SetActive(false);
            }

            if (newPanel != null)
            {
                newPanel.SetActive(true);
                RectTransform newRect = newPanel.GetComponent<RectTransform>();
                if (newRect != null)
                {
                    Vector2 offscreenLeft = new Vector2(-screenWidth, newRect.anchoredPosition.y);
                    Vector2 centerPos = new Vector2(0f, newRect.anchoredPosition.y);
                    newRect.anchoredPosition = offscreenLeft;

                    float elapsedTime = 0f;
                    while (elapsedTime < slideDuration)
                    {
                        elapsedTime += Time.unscaledDeltaTime;
                        float t = slideCurve.Evaluate(elapsedTime / slideDuration);
                        newRect.anchoredPosition = Vector2.Lerp(offscreenLeft, centerPos, t);
                        yield return null;
                    }
                    newRect.anchoredPosition = centerPos;
                }
            }
        }

        private void ValidateCreateInputs()
        {
            bool isValid = !string.IsNullOrWhiteSpace(hostPlayerNameInput.text) &&
                           !string.IsNullOrWhiteSpace(lobbyCodeInput.text);
            finalizeCreateLobbyButton.interactable = isValid;
        }

        private void ValidateJoinInputs()
        {
            bool isValid = !string.IsNullOrWhiteSpace(clientPlayerNameInput.text) &&
                           !string.IsNullOrWhiteSpace(joinCodeInput.text);
            finalizeJoinLobbyButton.interactable = isValid;
        }

        public void ShowJoinError(string message)
        {
            joinErrorText.text = message;
            joinErrorText.gameObject.SetActive(true);
        }

        public void ShowCreateError(string message)
        {
            createErrorText.text = message;
            createErrorText.gameObject.SetActive(true);
        }

        public void HideError()
        {
            createErrorText.text = "";
            createErrorText.gameObject.SetActive(false);

            joinErrorText.text = "";
            joinErrorText.gameObject.SetActive(false);
        }

        private async void OnCreateLobbyClicked()
        {
            string pName = hostPlayerNameInput.text.Trim();
            string selectedPrefab = hostPlayerPrefabDropdown.options[hostPlayerPrefabDropdown.value].text;
            string lCode = lobbyCodeInput.text.Trim().ToUpper();
            int maxPlayers = int.Parse(maxPlayersDropdown.options[maxPlayersDropdown.value].text);
            string selectedMap = mapDropdown.options[mapDropdown.value].text;

            UnityEngine.Random.InitState((int)System.DateTime.Now.Ticks);
            string initialTeam = (UnityEngine.Random.value > 0.5f) ? "Blue" : "Red";

            HideError();
            finalizeCreateLobbyButton.interactable = false;

            try
            {
                await LobbyManager.Instance.Authenticate(pName, selectedPrefab);
                await LobbyManager.Instance.CreateCustomLobby(lCode, maxPlayers, selectedMap, initialTeam);

                // Success! Safe to open room now
                SwitchPanel(lobbyRoomPanel);

                if (LobbyRoomUI.Instance != null)
                {
                    LobbyRoomUI.Instance.RefreshFromMenu();
                }
            }
            catch (Exception ex)
            {
                // Stay on setup panel or force shift back if an immediate race occurs
                SwitchPanel(gameSetupPanel);
                ShowCreateError($"Lobby creation failed: {ex.Message}");
                ValidateCreateInputs();
            }
        }

        private async void OnJoinLobbyClicked()
        {
            string pName = clientPlayerNameInput.text.Trim();
            string selectedPrefab = clientPlayerPrefabDropdown.options[clientPlayerPrefabDropdown.value].text;
            string lCode = joinCodeInput.text.Trim().ToUpper();

            HideError();
            finalizeJoinLobbyButton.interactable = false;

            try
            {
                // 1. Authenticate & format local variables safely first
                await LobbyManager.Instance.Authenticate(pName, selectedPrefab);

                // 2. Perform the server queries and logic checks
                await LobbyManager.Instance.JoinLobbyByCustomCode(lCode);

                // 3. SUCCESS PATH: Transition UI panel ONLY when both tasks are clear!
                SwitchPanel(lobbyRoomPanel);

                if (LobbyRoomUI.Instance != null)
                {
                    LobbyRoomUI.Instance.RefreshFromMenu();
                }
            }
            catch (DuplicateNameException nameEx)
            {
                // Return to join panel layout and cleanly drop unauthorized token profiles
                SwitchPanel(joinPanel);
                ShowJoinError(nameEx.Message);
                ValidateJoinInputs();

                if (Unity.Services.Authentication.AuthenticationService.Instance.IsSignedIn)
                {
                    Unity.Services.Authentication.AuthenticationService.Instance.SignOut(true);
                }
            }
            catch (Exception ex)
            {
                // Ensure user bounces out of transition back to registration context
                SwitchPanel(joinPanel);
                ShowJoinError(ex.Message);
                ValidateJoinInputs();
            }
        }

        private void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}