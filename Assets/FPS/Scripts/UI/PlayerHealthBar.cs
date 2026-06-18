using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;

namespace Unity.FPS.UI
{
    public class PlayerHealthBar : MonoBehaviour
    {
        [Tooltip("Image component displaying current health")]
        public Image HealthFillImage;

        Health m_PlayerHealth;

        void Update()
        {
            if (m_PlayerHealth == null)
            {
                var players = FindObjectsByType<PlayerCharacterController>(FindObjectsSortMode.None);
                foreach (var p in players)
                {
                    if (p.TryGetComponent<NetworkObject>(out var netObj) && netObj.IsOwner)
                    {
                        m_PlayerHealth = p.GetComponent<Health>();
                        break;
                    }
                }

                if (m_PlayerHealth == null) return;
            }

            if (HealthFillImage != null)
            {
                HealthFillImage.fillAmount = m_PlayerHealth.CurrentHealth.Value / m_PlayerHealth.MaxHealth;
            }
        }
    }
}