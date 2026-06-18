using Unity.FPS.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Unity.FPS.UI
{
    public class FlagHUD : MonoBehaviour // Correct choice: Pure MonoBehaviour
    {
        [Header("UI Display Components")]
        [Tooltip("The Image component on your HUD that represents the flag icon.")]
        [SerializeField] private GameObject flagIcon;

        private FlagCarrierTracker m_LocalFlagTracker;

        private void Start()
        {
            // Initial attempt to bind the local player
            FindLocalPlayerTracker();
        }

        private void Update()
        {
            // Fallback safety hook in case the local player spawns delayed over the network
            if (m_LocalFlagTracker == null)
            {
                FindLocalPlayerTracker();
            }
        }

        private void FindLocalPlayerTracker()
        {
            // Look for all instances of players spawned in the client's local scene hierarchy
            FlagCarrierTracker[] trackers = FindObjectsByType<FlagCarrierTracker>(FindObjectsSortMode.None);

            foreach (var tracker in trackers)
            {
                // Ensure this character controller is owned by this specific machine
                if (tracker.IsOwner)
                {
                    if (m_LocalFlagTracker != tracker)
                    {
                        // Clean up previous registration loops if any exist
                        UnbindTracker();

                        m_LocalFlagTracker = tracker;

                        // 1. Subscribe to changes on the underlying NetworkVariable state
                        m_LocalFlagTracker.IsCarryingFlag.OnValueChanged += OnFlagStateChanged;

                        // 2. Initialize the visual layout based on their current loadout state immediately
                        UpdateVisualIcon(m_LocalFlagTracker.IsCarryingFlag.Value);
                    }
                    return;
                }
            }
        }

        private void OnFlagStateChanged(bool oldState, bool newState)
        {
            // Fired automatically when the server updates our NetworkVariable tracking state
            UpdateVisualIcon(newState);
        }

        private void UpdateVisualIcon(bool holdingFlag)
        {
            if (flagIcon != null)
            {
                // Toggles the actual icon on or off cleanly based on the state variable
                flagIcon.SetActive(holdingFlag);
            }
        }

        private void UnbindTracker()
        {
            if (m_LocalFlagTracker != null)
            {
                m_LocalFlagTracker.IsCarryingFlag.OnValueChanged -= OnFlagStateChanged;
            }
        }

        private void OnDestroy()
        {
            // Essential memory garbage management pass
            UnbindTracker();
        }
    }
}