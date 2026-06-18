using UnityEngine;
using UnityEngine.Events;
using Unity.Netcode;
using Unity.FPS.Core;

namespace Unity.FPS.Game
{
    public class Health : NetworkBehaviour
    {
        [Tooltip("Maximum amount of health")] public float MaxHealth = 10f;

        [Tooltip("Health ratio at which the critical health vignette starts appearing")]
        public float CriticalHealthRatio = 0.3f;

        public UnityAction<float, GameObject> OnDamaged;
        public UnityAction<float> OnHealed;
        public UnityAction OnDie;
        public UnityAction OnRevive;

        public NetworkVariable<float> CurrentHealth = new NetworkVariable<float>(100f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public bool Invincible { get; set; }
        public bool CanPickup() => CurrentHealth.Value < MaxHealth;

        public float GetRatio() => CurrentHealth.Value / MaxHealth;
        public bool IsCritical() => GetRatio() <= CriticalHealthRatio;

        private Collider m_PlayerCollider;
        private CharacterController m_CharacterController;

        private void Awake()
        {
            m_PlayerCollider = GetComponent<Collider>();
            m_CharacterController = GetComponent<CharacterController>();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (IsServer)
            {
                CurrentHealth.Value = MaxHealth;
            }

            CurrentHealth.OnValueChanged += (oldValue, newValue) =>
            {
                float diff = newValue - oldValue;

                if (diff < 0)
                {
                    OnDamaged?.Invoke(-diff, null);
                }

                if (newValue <= 0f)
                {
                    if (!IsServer)
                    {
                        OnDie?.Invoke();
                    }
                }
                else if (diff > 0)
                {
                    OnHealed?.Invoke(diff);
                }
            };
        }

        public void Heal(float healAmount)
        {
            if (!IsServer) { HealServerRpc(healAmount); return; }
            CurrentHealth.Value = Mathf.Clamp(CurrentHealth.Value + healAmount, 0f, MaxHealth);
        }

        public void TakeDamage(float damage, GameObject damageSource)
        {
            if (Invincible) return;

            if (IsServer)
            {
                ApplyDamageLogic(damage, damageSource);
            }
            else
            {
                NetworkObjectReference sourceRef = default;
                if (damageSource != null && damageSource.TryGetComponent<NetworkObject>(out var netObj))
                {
                    sourceRef = netObj;
                }
                TakeDamageServerRpc(damage, sourceRef);

            }
        }

        [Rpc(SendTo.Server)]
        void TakeDamageServerRpc(float damage, NetworkObjectReference sourceRef)
        {
            GameObject sourceObject = null;
            if (sourceRef.TryGet(out NetworkObject netObj))
            {
                sourceObject = netObj.gameObject;
            }

            ApplyDamageLogic(damage, sourceObject);
    
        }

        void ApplyDamageLogic(float damage, GameObject damageSource)
        {
            if (!IsServer) return;
            if (CurrentHealth.Value <= 0f) return;

            float healthBefore = CurrentHealth.Value;
            CurrentHealth.Value = Mathf.Clamp(CurrentHealth.Value - damage, 0f, MaxHealth);

            float trueDamageAmount = healthBefore - CurrentHealth.Value;
            if (trueDamageAmount > 0f)
            {
                OnDamaged?.Invoke(trueDamageAmount, damageSource);
                FlashMeshClientRpc();
            }

            if (CurrentHealth.Value <= 0f)
            {
                HandleDeath();
            }
        }


        [Rpc(SendTo.Everyone)]
        void FlashMeshClientRpc()
        {
            // Try to find the PlayerSettings component on this player object
            if (TryGetComponent<PlayerSettings>(out var playerSettings))
            {
                playerSettings.FlashWhite();
            }
        }

        [Rpc(SendTo.Server)]
        void HealServerRpc(float amount) => Heal(amount);

        void HandleDeath()
        {
            if (IsServer)
            {
                Debug.Log($"[CTF Server] Player with ID {OwnerClientId} died.");

                // 2. SERVER-SIDE PHYSICS REMOVAL:
                // Instantly disables collisions so the dead body cannot trigger an accidental instant pickup re-calculation.
                if (m_PlayerCollider != null) m_PlayerCollider.enabled = false;
                if (m_CharacterController != null) m_CharacterController.enabled = false;
            }

            OnDie?.Invoke();
        }

        public void Revive()
        {
            if (IsServer)
            {
                CurrentHealth.Value = MaxHealth;

                // Re-enable collisions on respawn now that they've been relocated
                if (m_PlayerCollider != null) m_PlayerCollider.enabled = true;
                if (m_CharacterController != null) m_CharacterController.enabled = true;
            }

            OnRevive?.Invoke();
        }
    }
}