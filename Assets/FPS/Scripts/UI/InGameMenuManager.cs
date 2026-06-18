using System;
using System.Collections;
using Unity.FPS.Core;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Unity.FPS.UI
{
    public class InGameMenuManager : MonoBehaviour
    {
        [Tooltip("Root GameObject of the menu used to toggle its activation")]
        public GameObject MenuRoot;

        [Header("Panels")]
        [Tooltip("GameObject for the main menu options panel (Includes Audio Sliders now)")]
        public GameObject MenuPanel;

        [Tooltip("GameObject for the new standalone controls view panel")]
        public GameObject ControlsPanel;

        [Tooltip("Master volume when menu is open")]
        [Range(0.001f, 1f)]
        public float VolumeWhenMenuOpen = 0.5f;

        [Header("Audio Settings (Now inside Main Panel Layout)")]
        public AudioMixer MainMixer;
        public Slider MasterVolumeSlider;
        public Slider MusicVolumeSlider;
        public Slider SFXVolumeSlider;

        [Tooltip("Slider component for look sensitivity")]
        public Slider LookSensitivitySlider;

        [Tooltip("Toggle component for framerate display")]
        public Toggle FramerateToggle;

        PlayerInputHandler m_PlayerInputsHandler;
        FramerateCounter m_FramerateCounter;

        private InputAction m_SubmitAction;
        private InputAction m_CancelAction;
        private InputAction m_NavigateAction;
        private InputAction m_MenuAction;

        private GameObject m_CurrentlyActivePanel;

        private void Awake()
        {
            MenuPanel.SetActive(false);
            ControlsPanel.SetActive(false);
        }

        IEnumerator Start()
        {
            // Wait for the PlayerInputHandler to exist in the scene
            m_PlayerInputsHandler = null;
            while (m_PlayerInputsHandler == null)
            {
                var allPlayers = FindObjectsByType<PlayerInputHandler>(FindObjectsSortMode.None);
                foreach (var player in allPlayers)
                {
                    if (player.GetComponent<NetworkObject>().IsLocalPlayer)
                    {
                        m_PlayerInputsHandler = player;
                        break;
                    }
                }
                yield return null;
            }

            m_FramerateCounter = FindFirstObjectByType<FramerateCounter>();

            InitializePanels();

            MenuRoot.SetActive(false);

            // Initialize UI values
            LookSensitivitySlider.value = m_PlayerInputsHandler.LookSensitivity;
            LookSensitivitySlider.onValueChanged.AddListener(OnMouseSensitivityChanged);

            // Load and map persistent audio levels
            float savedMaster = PlayerPrefs.GetFloat("MasterVolume", 1.0f);
            float savedMusic = PlayerPrefs.GetFloat("MusicVolume", 1.0f);
            float savedSFX = PlayerPrefs.GetFloat("SFXVolume", 1.0f);

            MasterVolumeSlider.value = savedMaster;
            MusicVolumeSlider.value = savedMusic;
            SFXVolumeSlider.value = savedSFX;

            SetMixerVolume("MasterVolume", savedMaster);
            SetMixerVolume("MusicVolume", savedMusic);
            SetMixerVolume("SFXVolume", savedSFX);

            MasterVolumeSlider.onValueChanged.AddListener((val) => OnInGameVolumeChanged("MasterVolume", val));
            MusicVolumeSlider.onValueChanged.AddListener((val) => OnInGameVolumeChanged("MusicVolume", val));
            SFXVolumeSlider.onValueChanged.AddListener((val) => OnInGameVolumeChanged("SFXVolume", val));

            if (m_FramerateCounter != null)
            {
                FramerateToggle.isOn = m_FramerateCounter.UIText.gameObject.activeSelf;
                FramerateToggle.onValueChanged.AddListener(OnFramerateCounterChanged);
            }

            m_SubmitAction = InputSystem.actions.FindAction("UI/Submit");
            m_CancelAction = InputSystem.actions.FindAction("UI/Cancel");
            m_NavigateAction = InputSystem.actions.FindAction("UI/Navigate");
            m_MenuAction = InputSystem.actions.FindAction("UI/Menu");

            m_SubmitAction.Enable();
            m_CancelAction.Enable();
            m_NavigateAction.Enable();
            m_MenuAction.Enable();
        }

        private void InitializePanels()
        {
            MenuPanel.SetActive(false);
            if (ControlsPanel != null) ControlsPanel.SetActive(false);
        }

        void Update()
        {
            if (m_PlayerInputsHandler == null || m_MenuAction == null) return;

            m_PlayerInputsHandler.enabled = !MenuRoot.activeSelf;

            if (!MenuRoot.activeSelf && Mouse.current.leftButton.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            if (m_MenuAction.WasPressedThisFrame() || (MenuRoot.activeSelf && m_CancelAction.WasPressedThisFrame()))
            {
                if (ControlsPanel != null && ControlsPanel.activeSelf)
                {
                    // Instant reverse fallback back into Main options dashboard
                    OnShowControlsButtonClicked(false);
                }
                else
                {
                    SetPauseMenuActivation(!MenuRoot.activeSelf);
                }
            }

            if (m_NavigateAction.ReadValue<Vector2>().y != 0)
            {
                if (EventSystem.current.currentSelectedGameObject == null)
                {
                    EventSystem.current.SetSelectedGameObject(null);
                    LookSensitivitySlider.Select();
                }
            }
        }

        private void OnInGameVolumeChanged(string parameterKey, float value)
        {
            PlayerPrefs.SetFloat(parameterKey, value);
            PlayerPrefs.Save();
            SetMixerVolume(parameterKey, value);
        }

        public void SetMasterVolume(float value) => OnInGameVolumeChanged("MasterVolume", value);
        public void SetMusicVolume(float value) => OnInGameVolumeChanged("MusicVolume", value);
        public void SetSFXVolume(float value) => OnInGameVolumeChanged("SFXVolume", value);

        private void SetMixerVolume(string parameter, float value)
        {
            float dB = Mathf.Log10(Mathf.Max(0.0001f, value)) * 20f;
            MainMixer.SetFloat(parameter, dB);
        }

        public void ClosePauseMenu()
        {
            SetPauseMenuActivation(false);
        }

        void SetPauseMenuActivation(bool active)
        {
            if (active)
            {
                MenuRoot.SetActive(true);

                MenuPanel.SetActive(true);
                if (ControlsPanel != null) ControlsPanel.SetActive(false);

                m_CurrentlyActivePanel = MenuPanel;

                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;

                EventSystem.current.SetSelectedGameObject(null);
            }
            else
            {
                MenuRoot.SetActive(false);
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        // =========================================================================
        // INSTANT PANELS SWITCHER METHODS (WITHOUT COROUTINE SLIDES)
        // =========================================================================
        public void OnShowControlsButtonClicked(bool show)
        {
            GameObject target = show ? ControlsPanel : MenuPanel;
            SwitchPanel(target);
        }

        public void SwitchPanel(GameObject targetPanel)
        {
            if (targetPanel == m_CurrentlyActivePanel || targetPanel == null) return;

            // Turn off old panel instantly
            if (m_CurrentlyActivePanel != null)
            {
                m_CurrentlyActivePanel.SetActive(false);
            }

            // Snap and activate new panel instantly
            targetPanel.SetActive(true);

            m_CurrentlyActivePanel = targetPanel;
        }

        void OnMouseSensitivityChanged(float newValue)
        {
            m_PlayerInputsHandler.LookSensitivity = newValue;
        }

        void OnFramerateCounterChanged(bool newValue)
        {
            m_FramerateCounter.UIText.gameObject.SetActive(newValue);
        }

        public void MainMenu()
        {
            if (LobbyManager.Instance != null)
            {
                Debug.Log("[PAUSE MENU] Directing exit sequence through LobbyManager for clean cloud removal...");
                LobbyManager.Instance.OnLeftLobby += HandleAbandonMatchComplete;
                LobbyManager.Instance.LeaveLobbyAfterStarting();
            }
            else
            {
                Debug.LogWarning("[PAUSE MENU] LobbyManager missing. Defaulting to standalone offline fallback...");
                ShutdownNetworkAndThen(() =>
                {
                    SceneManager.LoadScene("MainMenu");
                });
            }
        }

        private void HandleAbandonMatchComplete(object sender, System.EventArgs e)
        {
            if (LobbyManager.Instance != null)
            {
                LobbyManager.Instance.OnLeftLobby -= HandleAbandonMatchComplete;
            }

            Debug.Log("[PAUSE MENU] Cloud session purged. Returning local machine to MainMenu scene.");
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            SceneManager.LoadScene("MainMenu");
        }

        private void ShutdownNetworkAndThen(System.Action onComplete)
        {
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.Shutdown();
                StartCoroutine(WaitForShutdownCoroutine(onComplete));
            }
            else
            {
                onComplete?.Invoke();
            }
        }

        private IEnumerator WaitForShutdownCoroutine(System.Action onComplete)
        {
            while (NetworkManager.Singleton != null && NetworkManager.Singleton.ShutdownInProgress)
            {
                yield return null;
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            onComplete?.Invoke();
        }
    }
}