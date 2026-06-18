using Unity.FPS.Gameplay;
using UnityEngine;
using TMPro;
using Unity.Netcode;

namespace Unity.FPS.UI
{
    public class RespawnMenu : MonoBehaviour
    {
        [Tooltip("The text component that will display the countdown")]
        public TextMeshProUGUI TimerText;

        [Tooltip("The root object of the respawn UI (to hide/show the whole panel)")]
        public GameObject RespawnContainer;

        [Tooltip("Reference to UI to hide while dead.")]
        public GameObject BottomLeftCorner;
        public GameObject WeaponHUDManager;
        public GameObject NotificationsRect;

        PlayerCharacterController m_LocalPlayer;
        float m_RespawnTimer;

        void Start()
        {
            // Start with the UI hidden
            if (RespawnContainer != null)
                RespawnContainer.SetActive(false);
        }

        void Update()
        {
            // 1. Find the local player if we haven't already
            if (m_LocalPlayer == null)
            {
                var players = FindObjectsByType<PlayerCharacterController>(FindObjectsSortMode.None);
                foreach (var p in players)
                {
                    if (p.IsOwner)
                    {
                        m_LocalPlayer = p;
                        // Subscribe to the death state change if you added that event, 
                        // otherwise we'll just poll the IsDead status below.
                        break;
                    }
                }

                if (m_LocalPlayer == null) return;
            }

            // 2. Handle the UI Visibility and Timer
            if (m_LocalPlayer.IsDead.Value)
            {
                BottomLeftCorner.SetActive(false);
                WeaponHUDManager.SetActive(false);
                NotificationsRect.SetActive(false);

                if (RespawnContainer != null && !RespawnContainer.activeSelf)
                {
                    RespawnContainer.SetActive(true);
                    // Reset our local countdown based on the player's delay
                    m_RespawnTimer = m_LocalPlayer.RespawnDelay;
                }

                // Tick the timer down locally for the UI
                if (m_RespawnTimer > 0)
                {
                    m_RespawnTimer -= Time.deltaTime;

                    // Display the time (Ceil makes it look better: 5, 4, 3...)
                    if (TimerText != null)
                    {
                        TimerText.text = $"Respawning in: {Mathf.CeilToInt(m_RespawnTimer)}";
                    }
                }
            }
            else
            {
                // Hide the UI when the player is no longer dead
                if (RespawnContainer != null && RespawnContainer.activeSelf)
                {
                    RespawnContainer.SetActive(false);
                    BottomLeftCorner.SetActive(true);
                    WeaponHUDManager.SetActive(true);
                    NotificationsRect.SetActive(true);
                }
            }
        }
    }
}