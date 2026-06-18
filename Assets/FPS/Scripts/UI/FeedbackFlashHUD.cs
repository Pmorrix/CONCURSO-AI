using System.Collections;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace Unity.FPS.UI
{
    public class FeedbackFlashHUD : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Image component of the flash")]
        public Image FlashImage;

        [Tooltip("CanvasGroup to fade the damage flash, used when recieving damage end healing")]
        public CanvasGroup FlashCanvasGroup;

        [Tooltip("CanvasGroup to fade the critical health vignette")]
        public CanvasGroup VignetteCanvasGroup;

        [Header("Damage")]
        [Tooltip("Color of the damage flash")]
        public Color DamageFlashColor;

        [Tooltip("Duration of the damage flash")]
        public float DamageFlashDuration;

        [Tooltip("Max alpha of the damage flash")]
        public float DamageFlashMaxAlpha = 1f;

        [Header("Critical health")]
        [Tooltip("Max alpha of the critical vignette")]
        public float CriticaHealthVignetteMaxAlpha = .8f;

        [Tooltip("Frequency at which the vignette will pulse when at critical health")]
        public float PulsatingVignetteFrequency = 4f;

        [Header("Heal")]
        [Tooltip("Color of the heal flash")]
        public Color HealFlashColor;

        [Tooltip("Duration of the heal flash")]
        public float HealFlashDuration;

        [Tooltip("Max alpha of the heal flash")]
        public float HealFlashMaxAlpha = 1f;

        bool m_FlashActive;
        bool m_IsPlayerDead; // Track local living status
        float m_LastTimeFlashStarted = Mathf.NegativeInfinity;
        Health m_PlayerHealth;
        GameFlowManager m_GameFlowManager;
        PlayerCharacterController playerCharacterController;

        IEnumerator Start()
        {
            m_GameFlowManager = FindFirstObjectByType<GameFlowManager>();


            // 1. Wait until the game's NetworkManager is active and has assigned our local player object
            while (NetworkManager.Singleton == null ||
                   NetworkManager.Singleton.LocalClient == null ||
                   NetworkManager.Singleton.LocalClient.PlayerObject == null)
            {
                yield return null; // Wait for the network identity to hydrate
            }

            // 2. Safely grab the PlayerObject assigned directly to our machine's client connection
            GameObject localPlayerRoot = NetworkManager.Singleton.LocalClient.PlayerObject.gameObject;

            // 3. Extract the components from our local player object exclusively
            m_PlayerHealth = localPlayerRoot.GetComponent<Health>();

            // Double-check fallback in case the Health component sits on a child object
            if (m_PlayerHealth == null)
            {
                m_PlayerHealth = localPlayerRoot.GetComponentInChildren<Health>();
            }

            // 4. Safely hook up local UI replication listeners
            if (m_PlayerHealth != null)
            {
                m_PlayerHealth.OnDamaged += OnTakeDamage;
                m_PlayerHealth.OnHealed += OnHealed;
                m_PlayerHealth.OnDie += OnPlayerDie;
                m_PlayerHealth.OnRevive += OnPlayerRevive;
            }
            else
            {
                Debug.LogError("[HUD Error] Found Local Player Object, but it is missing a Health component!", this);
            }
        }

        void Update()
        {
            if (m_PlayerHealth == null) return;

            // MODIFICATION: Only process critical vignette states if the player is alive
            if (m_PlayerHealth.IsCritical() && !m_IsPlayerDead)
            {
                VignetteCanvasGroup.gameObject.SetActive(true);
                float vignetteAlpha =
                    (1 - (m_PlayerHealth.CurrentHealth.Value / m_PlayerHealth.MaxHealth /
                          m_PlayerHealth.CriticalHealthRatio)) * CriticaHealthVignetteMaxAlpha;

                if (m_GameFlowManager.GameIsEnding)
                    VignetteCanvasGroup.alpha = vignetteAlpha;
                else
                    VignetteCanvasGroup.alpha =
                        ((Mathf.Sin(Time.time * PulsatingVignetteFrequency) / 2) + 0.5f) * vignetteAlpha;
            }
            else
            {
                // Forces the vignette away when healthy OR dead
                VignetteCanvasGroup.gameObject.SetActive(false);
            }

            if (m_FlashActive)
            {
                float normalizedTimeSinceDamage = (Time.time - m_LastTimeFlashStarted) / DamageFlashDuration;

                if (normalizedTimeSinceDamage < 1f)
                {
                    float flashAmount = DamageFlashMaxAlpha * (1f - normalizedTimeSinceDamage);
                    FlashCanvasGroup.alpha = flashAmount;
                }
                else
                {
                    FlashCanvasGroup.gameObject.SetActive(false);
                    m_FlashActive = false;
                }
            }
        }

        void ResetFlash()
        {
            m_LastTimeFlashStarted = Time.time;
            m_FlashActive = true;
            FlashCanvasGroup.alpha = 0f;
            FlashCanvasGroup.gameObject.SetActive(true);
        }

        void OnTakeDamage(float dmg, GameObject damageSource)
        {
            ResetFlash();
            FlashImage.color = DamageFlashColor;
        }

        void OnHealed(float amount)
        {
            // If we are healed out of a down/death state via external logic, wake up automatically
            if (m_IsPlayerDead && m_PlayerHealth.CurrentHealth.Value > 0f)
            {
                OnPlayerRevive();
            }

            ResetFlash();
            FlashImage.color = HealFlashColor;
        }

        // =========================================================
        // LIFECYCLE CONTROLLERS
        // =========================================================

        private void OnPlayerDie()
        {
            m_IsPlayerDead = true;
            VignetteCanvasGroup.alpha = 0f;
            VignetteCanvasGroup.gameObject.SetActive(false);
        }

        /// Public utility method. Call this from your custom Revive sequence script,
        /// or allow the OnHealed sequence above to automatically catch it.
        public void OnPlayerRevive()
        {
            m_IsPlayerDead = false;
        }

        private void OnDestroy()
        {
            if (m_PlayerHealth != null)
            {
                m_PlayerHealth.OnDamaged -= OnTakeDamage;
                m_PlayerHealth.OnHealed -= OnHealed;
                m_PlayerHealth.OnDie -= OnPlayerDie;
            }
        }
    }
}