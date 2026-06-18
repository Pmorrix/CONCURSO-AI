using Unity.FPS.Game;
using Unity.Netcode; // Add this
using UnityEngine;
using UnityEngine.Events;

namespace Unity.FPS.Gameplay
{
    [RequireComponent(typeof(AudioSource))]
    public class Jetpack : NetworkBehaviour // Changed to NetworkBehaviour
    {
        [Header("References")]
        public AudioSource AudioSource;
        public ParticleSystem[] JetpackVfx;

        [Header("Parameters")]
        public bool IsJetpackUnlockedAtStart = false;
        public float JetpackAcceleration = 7f;
        public float JetpackDownwardVelocityCancelingFactor = 1f;

        [Header("Durations")]
        public float ConsumeDuration = 1.5f;
        public float RefillDurationGrounded = 2f;
        public float RefillDurationInTheAir = 5f;
        public float RefillDelay = 1f;

        [Header("Audio")]
        public AudioClip JetpackSfx;

        public NetworkVariable<bool> IsJetpackUnlocked = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public NetworkVariable<float> CurrentFillRatio = new NetworkVariable<float>(1f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public NetworkVariable<bool> IsJetpackFiring = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        bool m_CanUseJetpack;
        PlayerCharacterController m_PlayerCharacterController;
        PlayerInputHandler m_InputHandler;
        float m_LastTimeOfUse;

        public UnityAction<bool> OnUnlockJetpack;

        public override void OnNetworkSpawn()
        {
            if (IsOwner)
            {
                m_PlayerCharacterController = GetComponent<PlayerCharacterController>();
                m_InputHandler = GetComponent<PlayerInputHandler>();
                AudioSource.clip = JetpackSfx;
                AudioSource.loop = true;
                IsJetpackUnlocked.Value = IsJetpackUnlockedAtStart;
                CurrentFillRatio.Value = 1f;
            }
        }

        void Update()
        {

            HandleVfxAndAudio(IsJetpackFiring.Value);

            if (!IsOwner) return;

            if (m_PlayerCharacterController.IsGrounded)
                m_CanUseJetpack = false;
            else if (!m_PlayerCharacterController.HasJumpedThisFrame && m_InputHandler.GetJumpInputDown())
                m_CanUseJetpack = true;

            bool jetpackIsInUse = m_CanUseJetpack && IsJetpackUnlocked.Value && CurrentFillRatio.Value > 0f && m_InputHandler.GetJumpInputHeld();

            IsJetpackFiring.Value = jetpackIsInUse;

            if (jetpackIsInUse)
            {
                m_LastTimeOfUse = Time.time;

                // Movement logic (calculated by Owner for smoothness and Server for authority)
                float totalAcceleration = JetpackAcceleration + m_PlayerCharacterController.GravityDownForce;
                if (m_PlayerCharacterController.CharacterVelocity.y < 0f)
                {
                    totalAcceleration += ((-m_PlayerCharacterController.CharacterVelocity.y / Time.deltaTime) * JetpackDownwardVelocityCancelingFactor);
                }
                m_PlayerCharacterController.CharacterVelocity += Vector3.up * totalAcceleration * Time.deltaTime;

                CurrentFillRatio.Value -= (Time.deltaTime / ConsumeDuration);

                HandleVfxAndAudio(true);
            }
            else
            {

                if (IsJetpackUnlocked.Value && Time.time - m_LastTimeOfUse >= RefillDelay)
                {
                    float refillRate = 1 / (m_PlayerCharacterController.IsGrounded ? RefillDurationGrounded : RefillDurationInTheAir);
                    CurrentFillRatio.Value = Mathf.Clamp01(CurrentFillRatio.Value + Time.deltaTime * refillRate);
                }

                HandleVfxAndAudio(false);
            }
        }

        void HandleVfxAndAudio(bool active)
        {
            // Visuals/Audio run locally for everyone
            for (int i = 0; i < JetpackVfx.Length; i++)
            {
                var emission = JetpackVfx[i].emission;
                emission.enabled = active;
            }

            if (active && !AudioSource.isPlaying) AudioSource.Play();
            else if (!active && AudioSource.isPlaying) AudioSource.Stop();
        }

        public bool TryUnlock()
        {
            if (!IsOwner) return false;

            if (IsJetpackUnlocked.Value) return false;

            IsJetpackUnlocked.Value = true;
            m_LastTimeOfUse = Time.time;
            OnUnlockJetpack?.Invoke(true);
            return true;
        }

        public void LoseJetpack()
        {
            if (IsOwner)
            {
                IsJetpackUnlocked.Value = false;
                IsJetpackFiring.Value = false;
                CurrentFillRatio.Value = 1f;

                // Ensure VFX/SFX stop immediately
                HandleVfxAndAudio(false);
            }
        }
    }
}