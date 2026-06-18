using Unity.FPS.Game;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using Unity.FPS.Core;

namespace Unity.FPS.AI
{
    [RequireComponent(typeof(EnemyController))]
    public class EnemyTurret : NetworkBehaviour
    {
        public enum AIState
        {
            Idle,
            Attack,
        }

        public Transform TurretPivot;
        public Transform TurretAimPoint;
        public NetworkAnimator NetAnimator;
        public float AimRotationSharpness = 5f;
        public float LookAtRotationSharpness = 2.5f;
        public float DetectionFireDelay = 1f;
        public float AimingTransitionBlendTime = 1f;

        [Tooltip("The random hit damage effects")]
        public ParticleSystem[] RandomHitSparks;

        public ParticleSystem[] OnDetectVfx;
        public AudioClip OnDetectSfx;

        [Tooltip("The time in seconds it takes for the turret to fully pop up and be ready to fire.")]
        public float DeploymentDuration = 0.7f;

        // public AIState AiState { get; private set; }
        public NetworkVariable<AIState> NetAiState = new NetworkVariable<AIState>(AIState.Idle);

        EnemyController m_EnemyController;
        Health m_Health;
        Quaternion m_RotationWeaponForwardToPivot;
        float m_TimeStartedDetection;
        float m_TimeLostDetection;
        Quaternion m_PreviousPivotAimingRotation;

        // Quaternion m_PivotAimingRotation;
        public NetworkVariable<Quaternion> m_NetPivotAimingRotation = new NetworkVariable<Quaternion>();

        const string k_AnimOnDamagedParameter = "OnDamaged";
        const string k_AnimIsActiveParameter = "IsActive";

        [Header("Custom Height Clamping")]
        [Tooltip("Check this if you want to forcefully clamp the pop-up extension height.")]
        public bool ClampExtensionHeight = true;
        [Tooltip("The maximum local Y position the TurretPivot can reach.")]
        public float MaxLocalHeight = 0.5f;

        [Header("Damage Flash Settings")]
        [SerializeField] private FlashController flashController;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            m_Health = GetComponent<Health>();
            DebugUtility.HandleErrorIfNullGetComponent<Health, EnemyTurret>(m_Health, this, gameObject);
            m_Health.OnDamaged += OnDamaged;

            m_EnemyController = GetComponent<EnemyController>();
            DebugUtility.HandleErrorIfNullGetComponent<EnemyController, EnemyTurret>(m_EnemyController, this,
                gameObject);

            m_EnemyController.onDetectedTarget += OnDetectedTarget;
            m_EnemyController.onLostTarget += OnLostTarget;

            // Remember the rotation offset between the pivot's forward and the weapon's forward
            m_RotationWeaponForwardToPivot =
                Quaternion.Inverse(m_EnemyController.GetCurrentWeapon().WeaponMuzzle.rotation) * TurretPivot.rotation;

            m_TimeStartedDetection = Mathf.NegativeInfinity;
            m_PreviousPivotAimingRotation = TurretPivot.rotation;
        }

        void Update()
        {
            if (!IsSpawned) return;
            UpdateCurrentAiState();
        }

        void LateUpdate()
        {
            if (!IsSpawned) return;
            UpdateTurretAiming();

            if (ClampExtensionHeight && TurretPivot != null)
            {
                Vector3 localPos = TurretPivot.localPosition;
                if (localPos.y > MaxLocalHeight)
                {
                    localPos.y = MaxLocalHeight;
                    TurretPivot.localPosition = localPos;
                }
            }
        }

        void UpdateCurrentAiState()
        {
            if (!IsServer) return;

            switch (NetAiState.Value)
            {
                case AIState.Attack:
                    if (m_EnemyController.KnownDetectedTarget == null) return;

                    bool mustShoot = Time.time > m_TimeStartedDetection + DetectionFireDelay;

                    // Get the active weapon muzzle
                    Transform currentMuzzle = m_EnemyController.GetCurrentWeapon().WeaponMuzzle;
                    Vector3 firingOrigin = currentMuzzle != null ? currentMuzzle.position : TurretAimPoint.position;

                    // Target position (safely fallback if tracking object root vs sub-transform)
                    Vector3 targetPosition = m_EnemyController.KnownDetectedTarget.transform.position;

                    // --- BALANCING CLIENT LATENCY ---
                    // Calculate direction safely
                    Vector3 directionToTarget = (targetPosition - firingOrigin).normalized;
                    Quaternion targetRotation = Quaternion.LookRotation(directionToTarget);

                    // Convert to target pivot space and apply design offset
                    Quaternion offsettedTargetRotation = targetRotation * m_RotationWeaponForwardToPivot;

                    // Smoothly rotate toward the target
                    Quaternion finalRotation = Quaternion.Slerp(m_PreviousPivotAimingRotation, offsettedTargetRotation,
                        (mustShoot ? AimRotationSharpness : LookAtRotationSharpness) * Time.deltaTime);

                    // --- VERTICAL ANGLE PROTECTION ---
                    // Prevents the turret mesh from breaking structure if targets get right underneath it
                    Vector3 eulerAngles = finalRotation.eulerAngles;
                    float pitch = eulerAngles.x > 180 ? eulerAngles.x - 360 : eulerAngles.x;
                    pitch = Mathf.Clamp(pitch, -30f, 30f); // Clamp tilt to 30 degrees up or down max
                    eulerAngles.x = pitch;

                    m_NetPivotAimingRotation.Value = Quaternion.Euler(eulerAngles);

                    // Shoot logic execution
                    if (mustShoot)
                    {
                        Vector3 correctedDirectionToTarget =
                            (m_NetPivotAimingRotation.Value * Quaternion.Inverse(m_RotationWeaponForwardToPivot)) *
                            Vector3.forward;

                        // Force spawn trajectory forward cleanly matching the new pitch calculations
                        m_EnemyController.TryAtack(firingOrigin + correctedDirectionToTarget);
                    }

                    break;
            }
        }
        void UpdateTurretAiming()
        {
            switch (NetAiState.Value)
            {
                case AIState.Attack:
                    TurretPivot.rotation = m_NetPivotAimingRotation.Value;
                    break;
                default:
                    // Use the turret rotation of the animation
                    TurretPivot.rotation = Quaternion.Slerp(m_NetPivotAimingRotation.Value, TurretPivot.rotation,
                        (Time.time - m_TimeLostDetection) / AimingTransitionBlendTime);
                    break;
            }

            m_PreviousPivotAimingRotation = TurretPivot.rotation;
        }

        void OnDamaged(float dmg, GameObject source)
        {
            if (!IsSpawned || !IsServer) return;

            if (NetAnimator != null && NetAnimator.IsSpawned)
            {
                NetAnimator.SetTrigger(k_AnimOnDamagedParameter);
            }

            PlayDamageVfxRpc();
        }

        [Rpc(SendTo.Everyone)]
        void PlayDamageVfxRpc()
        {
            if (RandomHitSparks.Length > 0)
            {
                int n = Random.Range(0, RandomHitSparks.Length - 1);
                RandomHitSparks[n].Play();
            }

            if (flashController != null)
            {
                Debug.Log("EnemyTurret: flashWhite!");
                flashController.FlashWhite();
            }
            else
            {
                Debug.Log("EnemyTurret: flashController is null!");
            }
        }

        void OnDetectedTarget()
        {
            if (!IsSpawned || !IsServer) return;

            if (NetAiState.Value == AIState.Idle)
            {
                NetAiState.Value = AIState.Attack;
            }

            if (NetAnimator != null && NetAnimator.IsSpawned)
            {
                NetAnimator.Animator.SetBool(k_AnimIsActiveParameter, true);
            }

            PlayDetectionEffectsRpc(true);

            m_TimeStartedDetection = Time.time;
        }

        void OnLostTarget()
        {
            if (!IsSpawned || !IsServer) return;

            Debug.Log(name + " has lost it's target");

            if (NetAiState.Value == AIState.Attack)
            {
                NetAiState.Value = AIState.Idle;
            }

            if (NetAnimator != null && NetAnimator.IsSpawned)
            {
                NetAnimator.Animator.SetBool(k_AnimIsActiveParameter, false);
            }

            PlayDetectionEffectsRpc(false);

            m_TimeLostDetection = Time.time;
        }

        [Rpc(SendTo.Everyone)]
        void PlayDetectionEffectsRpc(bool isDetected)
        {
            // Handle Particle Systems
            for (int i = 0; i < OnDetectVfx.Length; i++)
            {
                if (isDetected) OnDetectVfx[i].Play();
                else OnDetectVfx[i].Stop();
            }

            // Handle Audio (only on detection)
            if (isDetected && OnDetectSfx)
            {
                AudioUtility.CreateSFX(OnDetectSfx, transform.position, AudioUtility.AudioGroups.EnemyDetection, 1f);
            }
        }
    }
}