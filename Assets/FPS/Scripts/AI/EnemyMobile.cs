using Unity.FPS.Core;
using Unity.FPS.Game;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace Unity.FPS.AI
{
    [RequireComponent(typeof(EnemyController))]
    public class EnemyMobile : NetworkBehaviour
    {
        public enum AIState
        {
            Patrol,
            Follow,
            Attack,
        }

        public NetworkAnimator NetAnimator;

        [Tooltip("Fraction of the enemy's attack range at which it will stop moving towards target while attacking")]
        [Range(0f, 1f)]
        public float AttackStopDistanceRatio = 0.5f;

        [Tooltip("The random hit damage effects")]
        public ParticleSystem[] RandomHitSparks;

        public ParticleSystem[] OnDetectVfx;
        public AudioClip OnDetectSfx;

        [Header("Sound")] public AudioClip MovementSound;
        public MinMaxFloat PitchDistortionMovementSpeed;

        public AIState AiState { get; private set; }
        EnemyController m_EnemyController;
        AudioSource m_AudioSource;

        //const string k_AnimMoveSpeedParameter = "MoveSpeed";
        //const string k_AnimAttackParameter = "Attack";
        //const string k_AnimAlertedParameter = "Alerted";
        //const string k_AnimOnDamagedParameter = "OnDamaged";

        [Header("Damage Flash Settings")]
        [SerializeField] private FlashController flashController;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            m_EnemyController = GetComponent<EnemyController>();
            DebugUtility.HandleErrorIfNullGetComponent<EnemyController, EnemyMobile>(m_EnemyController, this, gameObject);

            m_EnemyController.onAttack += OnAttack;
            m_EnemyController.onDetectedTarget += OnDetectedTarget;
            m_EnemyController.onLostTarget += OnLostTarget;
            m_EnemyController.SetPathDestinationToClosestNode();
            m_EnemyController.onDamaged += OnDamaged;

            PlayerDeathBridge.OnAnyPlayerDied += HandlePlayerDeath;

            AiState = AIState.Patrol;

            m_AudioSource = GetComponent<AudioSource>();
            DebugUtility.HandleErrorIfNullGetComponent<AudioSource, EnemyMobile>(m_AudioSource, this, gameObject);
            m_AudioSource.clip = MovementSound;
            m_AudioSource.Play();
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            Unity.FPS.Game.PlayerDeathBridge.OnAnyPlayerDied -= HandlePlayerDeath;
        }

        void Update()
        {
            if (!IsServer || !IsSpawned) return;

            UpdateAiStateTransitions();
            UpdateCurrentAiState();

            float moveSpeed = m_EnemyController.NavMeshAgent.velocity.magnitude;

            /*
            if (NetAnimator != null && NetAnimator.Animator != null)
            {
                NetAnimator.Animator.SetFloat(k_AnimMoveSpeedParameter, moveSpeed);
            }
            */

            m_AudioSource.pitch = Mathf.Lerp(PitchDistortionMovementSpeed.Min, PitchDistortionMovementSpeed.Max,
                moveSpeed / m_EnemyController.NavMeshAgent.speed);
        }

        void UpdateAiStateTransitions()
        {
            switch (AiState)
            {
                case AIState.Patrol:
                    // Fallback safety: If the controller somehow found a target but the event missed
                    if (m_EnemyController.KnownDetectedTarget != null)
                    {
                        AiState = AIState.Follow;
                    }
                    break;

                case AIState.Follow:
                    // Transition to Attack if target is close enough and visible
                    if (m_EnemyController.IsSeeingTarget && m_EnemyController.IsTargetInAttackRange)
                    {
                        AiState = AIState.Attack;
                        m_EnemyController.SetNavDestination(transform.position);
                    }
                    // Fallback safety: If target completely vanished, go back to patrol
                    else if (m_EnemyController.KnownDetectedTarget == null)
                    {
                        AiState = AIState.Patrol;
                    }
                    break;

                case AIState.Attack:
                    // If target gets too far away, drop back down to Follow to chase them
                    if (!m_EnemyController.IsTargetInAttackRange)
                    {
                        AiState = AIState.Follow;
                    }
                    // If target disappears entirely, go back to patrol
                    else if (m_EnemyController.KnownDetectedTarget == null)
                    {
                        AiState = AIState.Patrol;
                    }
                    break;
            }
        }

        void UpdateCurrentAiState()
        {
            if (!IsServer) return;

            switch (AiState)
            {
                case AIState.Patrol:
                    m_EnemyController.UpdatePathDestination();
                    m_EnemyController.SetNavDestination(m_EnemyController.GetDestinationOnPath());
                    break;
                case AIState.Follow:
                    if (m_EnemyController.KnownDetectedTarget == null) return;
                    m_EnemyController.SetNavDestination(m_EnemyController.KnownDetectedTarget.transform.position);
                    m_EnemyController.OrientTowards(m_EnemyController.KnownDetectedTarget.transform.position);
                    m_EnemyController.OrientWeaponsTowards(m_EnemyController.KnownDetectedTarget.transform.position);
                    break;
                case AIState.Attack:
                    if (m_EnemyController.KnownDetectedTarget == null) return;
                    if (Vector3.Distance(m_EnemyController.KnownDetectedTarget.transform.position,
                            m_EnemyController.DetectionModule.DetectionSourcePoint.position)
                        >= (AttackStopDistanceRatio * m_EnemyController.DetectionModule.AttackRange))
                    {
                        m_EnemyController.SetNavDestination(m_EnemyController.KnownDetectedTarget.transform.position);
                    }
                    else
                    {
                        m_EnemyController.SetNavDestination(transform.position);
                    }

                    m_EnemyController.OrientTowards(m_EnemyController.KnownDetectedTarget.transform.position);
                    m_EnemyController.TryAtack(m_EnemyController.KnownDetectedTarget.transform.position);
                    break;
            }
        }

        void OnAttack()
        {
            if (!IsSpawned || !IsServer) return;

            
            /*
            if (NetAnimator != null && NetAnimator.IsSpawned)
            {
               NetAnimator.SetTrigger(k_AnimAttackParameter);
            }
            */
            
        }

        void OnDamaged()
        {
            if (!IsSpawned || !IsServer) return;

            /*
            if (NetAnimator != null && NetAnimator.IsSpawned)
            {
                NetAnimator.SetTrigger(k_AnimOnDamagedParameter);
            }
            */

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
                Debug.Log("EnemyMobile: flashWhite!");
                flashController.FlashWhite();
            }
            else
            {
                Debug.Log("EnemyMobile: flashController is null!");
            }
        }

        void OnDetectedTarget()
        {
            if (!IsServer) return;

            if (AiState == AIState.Patrol)
            {
                AiState = AIState.Follow;
            }

            //NetAnimator.Animator.SetBool(k_AnimAlertedParameter, true);
            PlayDetectionEffectsRpc(true);
        }
        void HandlePlayerDeath(GameObject deadPlayer)
        {
            if (!IsServer) return;

            bool forceWipe = false;

            if (m_EnemyController.KnownDetectedTarget != null)
            {
                GameObject targetRoot = m_EnemyController.KnownDetectedTarget.transform.root.gameObject;
                if (targetRoot == deadPlayer)
                {
                    forceWipe = true;
                }
            }
            else if (AiState == AIState.Follow || AiState == AIState.Attack)
            {
                forceWipe = true;
            }

            if (forceWipe)
            {
                Debug.Log($"[AI] Wiping memory of dead player {deadPlayer.name} on {name}. Resetting modules.");

                // 1. Manually break the hidden variables inside DetectionModule
                if (m_EnemyController.DetectionModule != null)
                {
                    m_EnemyController.DetectionModule.ClearTargetManually();
                }

                // 2. Drop the core state machine flags back to Patrol patterns safely
                OnLostTarget();

                // 3. Prevent structural pathing execution errors down the line
                m_EnemyController.SetPathDestinationToClosestNode();
            }
        }

        void OnLostTarget()
        {
            if (!IsServer) return;

            Debug.Log(name + " has lost its target");

            if (AiState == AIState.Follow || AiState == AIState.Attack)
            {
                AiState = AIState.Patrol;
            }

            //NetAnimator.Animator.SetBool(k_AnimAlertedParameter, false);
            PlayDetectionEffectsRpc(false);
        }

        [Rpc(SendTo.Everyone)]
        void PlayDetectionEffectsRpc(bool isDetected)
        {
            for (int i = 0; i < OnDetectVfx.Length; i++)
            {
                if (isDetected) OnDetectVfx[i].Play();
                else OnDetectVfx[i].Stop();
            }

            if (isDetected && OnDetectSfx)
            {
                AudioUtility.CreateSFX(OnDetectSfx, transform.position, AudioUtility.AudioGroups.EnemyDetection, 1f);
            }
        }
    }
}