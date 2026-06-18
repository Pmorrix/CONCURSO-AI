using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace Unity.FPS.UI
{
    public class StanceHUD : MonoBehaviour // Keep as MonoBehaviour - NOT NetworkBehaviour
    {
        [Tooltip("Image component for the stance sprites")]
        public Image StanceImage;

        [Tooltip("Sprite to display when standing")]
        public Sprite StandingSprite;

        [Tooltip("Sprite to display when crouching")]
        public Sprite CrouchingSprite;

        private PlayerCharacterController m_LocalPlayerCharacterController;

        void Start()
        {
            FindLocalPlayer();
        }

        void Update()
        {
            if (m_LocalPlayerCharacterController == null)
            {
                FindLocalPlayer();
            }
        }

        private void FindLocalPlayer()
        {

            PlayerCharacterController[] players = FindObjectsByType<PlayerCharacterController>(FindObjectsSortMode.None);

            foreach (var player in players)
            {
                if (player.IsOwner)
                {
                    if (m_LocalPlayerCharacterController != player)
                    {
                        if (m_LocalPlayerCharacterController != null)
                        {
                            m_LocalPlayerCharacterController.OnStanceChanged -= OnStanceChanged;
                        }

                        m_LocalPlayerCharacterController = player;
                        m_LocalPlayerCharacterController.OnStanceChanged += OnStanceChanged;

                        OnStanceChanged(m_LocalPlayerCharacterController.IsCrouching);
                    }
                    return;
                }
            }
        }

        void OnStanceChanged(bool crouched)
        {
            StanceImage.sprite = crouched ? CrouchingSprite : StandingSprite;
        }

        void OnDestroy()
        {
            if (m_LocalPlayerCharacterController != null)
            {
                m_LocalPlayerCharacterController.OnStanceChanged -= OnStanceChanged;
            }
        }
    }
}