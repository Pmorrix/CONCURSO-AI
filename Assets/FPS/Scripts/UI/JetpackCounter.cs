using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace Unity.FPS.UI
{
    public class JetpackCounter : MonoBehaviour
    {
        [Tooltip("Image component representing jetpack fuel")]
        public Image JetpackFillImage;

        [Tooltip("Canvas group that contains the whole UI for the jetpack")]
        public CanvasGroup MainCanvasGroup;

        [Tooltip("Component to animate the color when empty or full")]
        public FillBarColorChange FillBarColorChange;

        Jetpack m_Jetpack;

        void Start()
        {
            // Start looking for the local player's jetpack
            FindLocalJetpack();
        }

        void Update()
        {
            // Keep looking for the local player's jetpack until we find it
            if (m_Jetpack == null)
            {
                FindLocalJetpack();
                return;
            }

            // Update jetpack UI
            MainCanvasGroup.gameObject.SetActive(m_Jetpack.IsJetpackUnlocked.Value);

            if (m_Jetpack.IsJetpackUnlocked.Value)
            {
                JetpackFillImage.fillAmount = m_Jetpack.CurrentFillRatio.Value;
                FillBarColorChange.UpdateVisual(m_Jetpack.CurrentFillRatio.Value);
            }
        }

        private void FindLocalJetpack()
        {
            // Find the local player first
            PlayerCharacterController[] players = FindObjectsByType<PlayerCharacterController>(FindObjectsSortMode.None);

            foreach (var player in players)
            {
                // Check if this is the local player (assuming PlayerCharacterController is a NetworkBehaviour)
                if (player.IsOwner)
                {
                    // Get the Jetpack component from the local player
                    Jetpack jetpack = player.GetComponent<Jetpack>();
                    if (jetpack != null && m_Jetpack != jetpack)
                    {
                        SetUpJetpack(jetpack);
                    }
                    return;
                }
            }
        }

        private void SetUpJetpack(Jetpack jetpack)
        {
            m_Jetpack = jetpack;
            FillBarColorChange.Initialize(1f, 0f);

            // Initialize UI with current state
            MainCanvasGroup.gameObject.SetActive(m_Jetpack.IsJetpackUnlocked.Value);
            if (m_Jetpack.IsJetpackUnlocked.Value)
            {
                JetpackFillImage.fillAmount = m_Jetpack.CurrentFillRatio.Value;
                FillBarColorChange.UpdateVisual(m_Jetpack.CurrentFillRatio.Value);
            }
        }
    }
}