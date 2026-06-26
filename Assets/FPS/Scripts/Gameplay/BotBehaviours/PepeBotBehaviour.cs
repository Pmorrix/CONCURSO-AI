using System.Collections.Generic;
using Unity.FPS.Core;
using Unity.FPS.Game;
using Unity.FPS.AI;
using UnityEngine;
using UnityEngine.AI;

namespace Unity.FPS.Gameplay
{
    public class PepeBotBehaviour : BotBehaviour
    {
        [Header("Sensing")]
        [SerializeField] private float sightRadius = 35f;
        [SerializeField] private float sightConeAngle = 150f;
        [SerializeField] private float senseInterval = 0.18f;
        [SerializeField] private bool ignoreSameAffiliation = false;
        [SerializeField] private LayerMask sightMask = ~0;

        [Header("Movement")]
        [SerializeField] private float decisionIntervalMin = 0.7f;
        [SerializeField] private float decisionIntervalMax = 1.8f;
        [SerializeField] private float pauseChance = 0.30f;
        [SerializeField] private float initialNoPauseDuration = 3f;
        [SerializeField] private float sprintChance = 0.1f;
        [SerializeField] private float jumpChance = 0.12f;
        [SerializeField] private float obstacleCheckDistance = 2.2f;

        [Header("Obstacle Handling")]
        [SerializeField] private float wallAvoidanceDistance = 2.4f;
        [SerializeField] private float lowObstacleHeight = 0.45f;
        [SerializeField] private float highObstacleHeight = 1.35f;
        [SerializeField] private float obstacleJumpDistance = 1.5f;
        [SerializeField] private float obstacleJumpCooldown = 0.8f;
        [SerializeField] private float sideProbeAngle = 35f;

        [Header("Stuck Recovery")]
        [SerializeField] private float moveInputSharpness = 5f;
        [SerializeField] private float stuckCheckInterval = 0.6f;
        [SerializeField] private float stuckMinMoveDistance = 0.35f;
        [SerializeField] private float stuckRecoveryDuration = 1.4f;
        [SerializeField] private float stuckRecoveryCooldown = 0.35f;
        [SerializeField] private float stuckBackInput = -0.55f;
        [SerializeField] private float stuckStrafeInput = 0.85f;

        [Header("Combat")]
        [SerializeField] private float reactionTimeMin = 0.25f;
        [SerializeField] private float reactionTimeMax = 0.85f;
        [SerializeField] private float comfortableFireDistance = 26f;
        [SerializeField] private float aimAngleToFire = 18f;
        [SerializeField] private float aimAngleToAim = 38f;
        [SerializeField] private float aimNoise = 0.75f;
        [SerializeField] private float focusedAimNoiseMultiplier = 0.45f;
        [SerializeField] private float enemyBurstMin = 0.28f;
        [SerializeField] private float enemyBurstMax = 0.75f;
        [SerializeField] private float enemyBurstPauseMin = 0.18f;
        [SerializeField] private float enemyBurstPauseMax = 0.55f;
        [SerializeField] private float closeEnemyFireAngleBonus = 10f;

        [Header("Rear Threat")]
        [SerializeField] private float rearThreatRadius = 12f;
        [SerializeField] private float rearThreatAngle = 130f;
        [SerializeField] private float rearThreatDetectionChance = 0.18f;
        [SerializeField] private float rearThreatReactionTime = 0.4f;
        [SerializeField] private float rearThreatAimAngleToFire = 12f;
        [SerializeField] private float rearThreatMaxLookInputPerFrame = 0.09f;
        [SerializeField] private float rearThreatLookInputSharpness = 20f;
        [SerializeField] private float rearThreatAimNoiseMultiplier = 0.15f;

        [Header("Healing")]
        [SerializeField] private float lowHealthSeekRatio = 0.30f;
        [SerializeField] private float postCombatHealRatio = 0.45f;
        [SerializeField] private float postCombatHealDuration = 8f;
        [SerializeField] private float healthPickupScanInterval = 1f;
        [SerializeField] private float healthPickupDiscoveryRadius = 20f;
        [SerializeField] private float healthPickupDiscoveryAngle = 110f;
        [SerializeField] private float healthPickupNearbyAwarenessDistance = 3f;
        [SerializeField] private float healthPickupReachDistance = 0.6f;
        [SerializeField] private float duplicateHealthMemoryDistance = 1.5f;
        [SerializeField] private float healthMemorySkipChance = 0.12f;
        [SerializeField] private float healthPickupSelectionNoise = 2.5f;

        [Header("CTF Objective")]
        [SerializeField] private bool pursueCtfObjective = true;
        [SerializeField] private bool sprintToCtfObjective = true;
        [SerializeField] private float objectiveScanInterval = 0.5f;
        [SerializeField] private float objectiveReachDistance = 1.2f;
        [SerializeField] private float objectiveSprintChance = 0.55f;
        [SerializeField] private float objectiveSideLookChance = 0.45f;
        [SerializeField] private float objectiveSideLookIntervalMin = 2.0f;
        [SerializeField] private float objectiveSideLookIntervalMax = 4.5f;
        [SerializeField] private float objectiveSideLookDurationMin = 0.35f;
        [SerializeField] private float objectiveSideLookDurationMax = 0.8f;
        [SerializeField] private float objectiveSideLookAngleMin = 35f;
        [SerializeField] private float objectiveSideLookAngleMax = 75f;
        [SerializeField] private float objectiveHesitationChance = 0.22f;
        [SerializeField] private float objectiveHesitationIntervalMin = 4.0f;
        [SerializeField] private float objectiveHesitationIntervalMax = 8.0f;
        [SerializeField] private float objectiveHesitationDurationMin = 0.18f;
        [SerializeField] private float objectiveHesitationDurationMax = 0.45f;

        [Header("Path Steering")]
        [SerializeField] private float pathRepathInterval = 0.45f;
        [SerializeField] private float pathCornerReachDistance = 1.1f;
        [SerializeField] private float navMeshSampleDistance = 3f;
        [SerializeField] private float wanderPointMinDistance = 6f;
        [SerializeField] private float wanderPointMaxDistance = 16f;
        [SerializeField] private float wanderDestinationReachDistance = 2f;

        [Header("Traversal Humanization")]
        [SerializeField] private float traversalStrafeChance = 0.7f;
        [SerializeField] private float traversalStrafeInputMin = 0.08f;
        [SerializeField] private float traversalStrafeInputMax = 0.22f;
        [SerializeField] private float traversalStrafeIntervalMin = 1.2f;
        [SerializeField] private float traversalStrafeIntervalMax = 3f;
        [SerializeField] private float traversalStrafeDurationMin = 0.45f;
        [SerializeField] private float traversalStrafeDurationMax = 1.25f;
        [SerializeField] private float traversalWallBiasChance = 0.22f;
        [SerializeField] private float sprintBurstDurationMin = 1.4f;
        [SerializeField] private float sprintBurstDurationMax = 3.2f;
        [SerializeField] private float impatientJumpCooldownMin = 4.5f;
        [SerializeField] private float impatientJumpCooldownMax = 9f;

        [Header("Turret Tactics")]
        [SerializeField] private float turretReactionTime = 0.22f;
        [SerializeField] private float turretFirstDetectionTimeMin = 0.45f;
        [SerializeField] private float turretFirstDetectionTimeMax = 0.85f;
        [SerializeField] private float turretOpeningFireTime = 3.0f;
        [SerializeField] private float turretFireDistance = 38f;
        [SerializeField] private float turretAimAngleToFire = 10f;
        [SerializeField] private float turretAimNoiseMultiplier = 0.12f;
        [SerializeField] private float turretSafeDistance = 32f;
        [SerializeField] private float turretPreferredDistance = 24f;
        [SerializeField] private float turretRetreatDuration = 1.6f;
        [SerializeField] private int turretDamagePressureHitCount = 2;
        [SerializeField] private float turretDamagePressureWindow = 1.6f;
        [SerializeField] private float turretDamagePressureRetreatDuration = 1.25f;
        [SerializeField] private float turretDamagePressureLowHealthRatio = 0.42f;
        [SerializeField] private float turretPriorityBias = 12f;
        [SerializeField] private float turretMaxLookInputPerFrame = 0.018f;
        [SerializeField] private float turretLookInputSharpness = 10f;
        [SerializeField] private float turretReloadAmmoRatio = 0.18f;
        [SerializeField] private float turretResumeFireAmmoRatio = 0.55f;
        [SerializeField] private float turretReloadRetreatDuration = 1.8f;

        [Header("Look")]
        [SerializeField] private float maxLookInputPerFrame = 0.012f;
        [SerializeField] private float lookInputSharpness = 5f;

        [Header("Humanization")]
        [SerializeField] private float lookOvershootChance = 0.45f;
        [SerializeField] private float lookOvershootAngleMin = 3f;
        [SerializeField] private float lookOvershootAngleMax = 9f;
        [SerializeField] private float lookOvershootDurationMin = 0.16f;
        [SerializeField] private float lookOvershootDurationMax = 0.38f;
        [SerializeField] private float damageFlinchDuration = 0.16f;
        [SerializeField] private float damageFlinchYaw = 5f;
        [SerializeField] private float damageFlinchPitch = 2.5f;

        [Header("Damage Awareness")]
        [SerializeField] private float damageSourceLookDuration = 1.1f;
        [SerializeField] private float damageSourceTargetDuration = 2.2f;
        [SerializeField] private float damageSourceReactionTime = 0.12f;
        [SerializeField] private float damageSourceMaxDistance = 45f;
        [SerializeField] private float damageSourceMaxLookInputPerFrame = 0.06f;
        [SerializeField] private float damageSourceLookInputSharpness = 14f;
        [SerializeField] private float damageSourceAimNoiseMultiplier = 0.25f;

        [Header("Combat Variation")]
        [SerializeField] private float combatEngageMovementChance = 0.7f;
        [SerializeField] private float combatPreferredDistanceVariation = 4f;
        [SerializeField] private float combatCrouchChance = 0.08f;
        [SerializeField] private float combatCrouchDurationMin = 0.35f;
        [SerializeField] private float combatCrouchDurationMax = 0.8f;

        [Header("Match Variation")]
        [SerializeField] private bool varyPersonalityPerMatch = true;
        [SerializeField] private float matchVariationStrength = 0.18f;
        [SerializeField] private bool logMatchStyleToConsole = true;

        private PlayerCharacterController m_Controller;
        private Actor m_SelfActor;
        private Health m_Health;
        private PlayerSettings m_PlayerSettings;
        private FlagCarrierTracker m_FlagCarrierTracker;
        private PlayerWeaponsManager m_WeaponsManager;
        private Transform m_Eye;

        private Actor m_Target;
        private Actor m_PreviousTarget;
        private Actor m_DamageSourceThreat;
        private bool m_TargetIsTurret;
        private bool m_TargetIsRearThreat;
        private bool m_TargetIsDamageSourceThreat;
        private bool m_DamageSourceWasRear;
        private float m_TargetVisibleSince;
        private float m_CurrentReactionTime;
        private float m_NextSenseTime;

        private Vector3 m_WanderWorldDirection;
        private Vector3 m_WanderDestination;
        private Vector3 m_MoveInput;
        private Vector3 m_OutputMoveInput;
        private Vector2 m_LookInput;
        private Vector3 m_AimOffset;

        private float m_NextDecisionTime;
        private float m_StateUntil;
        private float m_InitializedTime;
        private float m_NextAimNoiseTime;
        private float m_JumpHeldUntil;
        private float m_NextImpatientJumpTime;
        private float m_NextSprintDecisionTime;
        private float m_TraversalStrafe;
        private float m_TraversalStrafeUntil;
        private float m_NextTraversalStrafeTime;
        private float m_FireBurstUntil;
        private float m_NextFireBurstTime;
        private float m_CombatStrafe;
        private float m_CombatDistanceOffset;
        private float m_CrouchReleaseTime;
        private float m_TurretRetreatUntil;
        private float m_TurretReloadRetreatUntil;
        private float m_TurretDamagePressureWindowUntil;
        private float m_TurretDamagePressureRetreatUntil;
        private float m_TurretAwareAt;
        private int m_TurretDamagePressureHits;
        private float m_NextObstacleJumpTime;
        private float m_AvoidanceSide;
        private float m_AvoidanceSideUntil;
        private float m_NextHealthPickupScanTime;
        private float m_NextObjectiveScanTime;
        private float m_NextObjectiveSideLookTime;
        private float m_ObjectiveSideLookUntil;
        private float m_ObjectiveSideLookAngle;
        private float m_NextObjectiveHesitationTime;
        private float m_ObjectiveHesitationUntil;
        private Vector3 m_ObjectiveHesitationInput;
        private float m_LookOvershootUntil;
        private float m_LookOvershootAngle;
        private float m_DamageFlinchUntil;
        private float m_DamageFlinchYaw;
        private float m_DamageFlinchPitch;
        private float m_DamageSourceLookUntil;
        private float m_DamageSourceThreatUntil;
        private Vector3 m_DamageSourceLookDirection;
        private float m_ReactionTimeMultiplier = 1f;
        private float m_AimNoiseMultiplier = 1f;
        private float m_BurstDurationMultiplier = 1f;
        private float m_BurstPauseMultiplier = 1f;
        private float m_HealthMemoryMultiplier = 1f;
        private float m_HealingThresholdMultiplier = 1f;
        private float m_ObjectiveSideLookChanceMultiplier = 1f;
        private float m_ObjectiveSideLookIntervalMultiplier = 1f;
        private float m_ObjectiveHesitationChanceMultiplier = 1f;
        private float m_ObjectiveHesitationDurationMultiplier = 1f;
        private float m_TurretAggressionMultiplier = 1f;
        private float m_PostCombatHealUntil;
        private Vector3 m_HealingDestination;
        private Vector3 m_ObjectiveDestination;
        private Vector3 m_LastStuckCheckPosition;
        private readonly List<Vector3> m_KnownHealthPickups = new List<Vector3>();
        private readonly HashSet<int> m_SeenTurretIds = new HashSet<int>();
        private float m_NextStuckCheckTime;
        private float m_StuckRecoveryUntil;
        private float m_NextStuckRecoveryAllowed;
        private float m_StuckRecoverySide = 1f;
        private float m_NextPathUpdateTime;
        private Vector3 m_LastPathDestination;
        private Vector3 m_CurrentPathSteeringPoint;
        private bool m_HasPathSteeringPoint;

        private bool m_IsPaused;
        private bool m_SprintHeld;
        private bool m_JumpDown;
        private bool m_CrouchDown;
        private bool m_CrouchReleased;
        private bool m_IsBotCrouching;
        private bool m_EngageTargetMovement;
        private bool m_FireHeld;
        private bool m_FireHeldPrevious;
        private bool m_FireDown;
        private bool m_FireReleased;
        private bool m_ReloadDown;
        private bool m_AimHeld;
        private bool m_IsSeekingHealth;
        private bool m_HasObjectiveDestination;
        private bool m_HasWanderDestination;
        private bool m_HasInitialized;
        private bool m_LastCarryingFlagState;

        public override void Initialize(PlayerCharacterController controller)
        {
            m_HasInitialized = true;
            m_Controller = controller;
            m_SelfActor = GetComponent<Actor>();
            m_Health = GetComponent<Health>();
            m_PlayerSettings = GetComponent<PlayerSettings>();
            m_FlagCarrierTracker = GetComponent<FlagCarrierTracker>();
            m_WeaponsManager = GetComponent<PlayerWeaponsManager>();
            if (m_Health != null)
                m_Health.OnDamaged += OnDamaged;

            m_Eye = controller != null && controller.PlayerCamera != null
                ? controller.PlayerCamera.transform
                : transform;

            RandomizeMatchPersonality();
            m_CurrentReactionTime = GetRandomReactionTime();
            m_LastStuckCheckPosition = transform.position;
            m_InitializedTime = Time.time;
            m_LastCarryingFlagState = IsCarryingFlag();
            PickNewWanderState();
        }

        public override void OnBotUpdate()
        {
            EnsureInitializedFallback();
            ResetFramePulses();

            if (Time.time >= m_NextSenseTime)
            {
                m_NextSenseTime = Time.time + senseInterval;
                UpdateTarget();
            }

            if (Time.time >= m_NextHealthPickupScanTime)
            {
                m_NextHealthPickupScanTime = Time.time + healthPickupScanInterval;
                RememberVisibleHealthPickups();
            }

            UpdateHealingIntent();

            if (Time.time >= m_NextObjectiveScanTime)
            {
                m_NextObjectiveScanTime = Time.time + objectiveScanInterval;
                UpdateCtfObjective();
            }

            UpdateWeaponTactics();
            UpdateFlagStateHumanReaction();
            UpdateObjectiveHesitation();

            if (Time.time >= m_NextDecisionTime || Time.time >= m_StateUntil)
            {
                PickNewWanderState();
            }

            UpdateCrouchState();
            UpdateAimNoise();
            UpdateLookInput();
            UpdateMoveInput();
            UpdateStuckRecovery();
            SmoothMoveInput();
            UpdateFireInput();
        }

        private void ResetFramePulses()
        {
            m_JumpDown = false;
            m_CrouchDown = false;
            m_CrouchReleased = false;
            m_FireDown = false;
            m_FireReleased = false;
            m_ReloadDown = false;
        }

        private void EnsureInitializedFallback()
        {
            if (m_HasInitialized)
                return;

            m_HasInitialized = true;
            m_Controller = GetComponent<PlayerCharacterController>();
            m_SelfActor = GetComponent<Actor>();
            m_Health = GetComponent<Health>();
            m_PlayerSettings = GetComponent<PlayerSettings>();
            m_FlagCarrierTracker = GetComponent<FlagCarrierTracker>();
            m_WeaponsManager = GetComponent<PlayerWeaponsManager>();
            if (m_Health != null)
                m_Health.OnDamaged += OnDamaged;

            m_Eye = m_Controller != null && m_Controller.PlayerCamera != null
                ? m_Controller.PlayerCamera.transform
                : transform;

            RandomizeMatchPersonality();
            m_CurrentReactionTime = GetRandomReactionTime();
            m_LastStuckCheckPosition = transform.position;
            m_InitializedTime = Time.time;
            m_LastCarryingFlagState = IsCarryingFlag();
            PickNewWanderState();
        }

        private void OnDestroy()
        {
            if (m_Health != null)
                m_Health.OnDamaged -= OnDamaged;
        }

        private void OnDamaged(float damage, GameObject source)
        {
            m_PostCombatHealUntil = Time.time + postCombatHealDuration;
            m_DamageFlinchUntil = Time.time + damageFlinchDuration;
            m_DamageFlinchYaw = Random.Range(-damageFlinchYaw, damageFlinchYaw);
            m_DamageFlinchPitch = Random.Range(-damageFlinchPitch, damageFlinchPitch);
            RegisterDamageSource(source);
            RegisterTurretDamagePressure(source);
        }

        private void RegisterTurretDamagePressure(GameObject source)
        {
            if (source == null || !IsTurretActor(source.GetComponentInParent<Actor>()))
                return;

            if (Time.time >= m_TurretDamagePressureWindowUntil)
                m_TurretDamagePressureHits = 0;

            m_TurretDamagePressureHits++;
            m_TurretDamagePressureWindowUntil = Time.time + turretDamagePressureWindow;

            bool pressureByHits = m_TurretDamagePressureHits >= Mathf.Max(1, turretDamagePressureHitCount);
            bool pressureByHealth = m_Health != null &&
                                    m_Health.GetRatio() <= Mathf.Clamp01(turretDamagePressureLowHealthRatio);

            if (!pressureByHits && !pressureByHealth)
                return;

            m_TurretDamagePressureHits = 0;
            if (Mathf.Abs(m_CombatStrafe) < 0.35f)
                m_CombatStrafe = Random.value < 0.5f ? -0.65f : 0.65f;

            m_TurretDamagePressureRetreatUntil = Mathf.Max(
                m_TurretDamagePressureRetreatUntil,
                Time.time + turretDamagePressureRetreatDuration);
        }

        private void RegisterDamageSource(GameObject source)
        {
            if (source == null || m_Eye == null)
                return;

            Actor sourceActor = source.GetComponentInParent<Actor>();
            if (sourceActor != null && (sourceActor == m_SelfActor || IsFriendlyActor(sourceActor)))
                return;

            Vector3 sourcePosition = sourceActor != null
                ? GetTargetPoint(sourceActor)
                : source.transform.position;

            Vector3 toSource = sourcePosition - m_Eye.position;
            float distance = toSource.magnitude;
            if (toSource.sqrMagnitude < 0.01f || distance > damageSourceMaxDistance)
                return;

            m_DamageSourceLookDirection = toSource.normalized;
            m_DamageSourceLookUntil = Time.time + damageSourceLookDuration;
            m_DamageSourceWasRear = IsDirectionBehind(toSource);

            if (sourceActor == null)
                return;

            if (m_DamageSourceWasRear &&
                (distance > rearThreatRadius || Random.value > rearThreatDetectionChance))
            {
                m_DamageSourceThreat = null;
                m_DamageSourceThreatUntil = 0f;
                return;
            }

            m_DamageSourceThreat = sourceActor;
            m_DamageSourceThreatUntil = Time.time + damageSourceTargetDuration;
            m_NextSenseTime = 0f;
        }

        private void RandomizeMatchPersonality()
        {
            m_ReactionTimeMultiplier = 1f;
            m_AimNoiseMultiplier = 1f;
            m_BurstDurationMultiplier = 1f;
            m_BurstPauseMultiplier = 1f;
            m_HealthMemoryMultiplier = 1f;
            m_HealingThresholdMultiplier = 1f;
            m_ObjectiveSideLookChanceMultiplier = 1f;
            m_ObjectiveSideLookIntervalMultiplier = 1f;
            m_ObjectiveHesitationChanceMultiplier = 1f;
            m_ObjectiveHesitationDurationMultiplier = 1f;
            m_TurretAggressionMultiplier = 1f;

            if (!varyPersonalityPerMatch)
            {
                LogMatchStyle("Estable");
                return;
            }

            float strength = Mathf.Clamp(matchVariationStrength, 0f, 0.35f);
            float aggression = Random.Range(-1f, 1f);
            float focus = Random.Range(-1f, 1f);
            float caution = Random.Range(-1f, 1f);
            float distractibility = Random.Range(-1f, 1f);

            m_ReactionTimeMultiplier = ClampPersonalityMultiplier(1f - focus * strength * 0.7f + distractibility * strength * 0.45f);
            m_AimNoiseMultiplier = ClampPersonalityMultiplier(1f - focus * strength * 0.8f + distractibility * strength * 0.8f);
            m_BurstDurationMultiplier = ClampPersonalityMultiplier(1f + aggression * strength);
            m_BurstPauseMultiplier = ClampPersonalityMultiplier(1f - aggression * strength * 0.6f + caution * strength * 0.4f);
            m_HealthMemoryMultiplier = ClampPersonalityMultiplier(1f + distractibility * strength + caution * strength * 0.2f);
            m_HealingThresholdMultiplier = ClampPersonalityMultiplier(1f + caution * strength * 0.7f - aggression * strength * 0.5f);
            m_ObjectiveSideLookChanceMultiplier = ClampPersonalityMultiplier(1f + distractibility * strength * 0.8f + caution * strength * 0.3f);
            m_ObjectiveSideLookIntervalMultiplier = ClampPersonalityMultiplier(1f - distractibility * strength * 0.5f);
            m_ObjectiveHesitationChanceMultiplier = ClampPersonalityMultiplier(1f + distractibility * strength + caution * strength * 0.4f - aggression * strength * 0.5f);
            m_ObjectiveHesitationDurationMultiplier = ClampPersonalityMultiplier(1f + distractibility * strength * 0.7f);
            m_TurretAggressionMultiplier = ClampPersonalityMultiplier(1f + aggression * strength - caution * strength * 0.4f);

            LogMatchStyle(GetMatchStyleLabel(aggression, focus, caution, distractibility));
        }

        private float ClampPersonalityMultiplier(float value)
        {
            return Mathf.Clamp(value, 0.72f, 1.32f);
        }

        private string GetMatchStyleLabel(float aggression, float focus, float caution, float distractibility)
        {
            const float strongTraitThreshold = 0.35f;

            string primaryStyle = "";
            float primaryScore = strongTraitThreshold;

            ConsiderStyle("Agresivo", aggression, ref primaryStyle, ref primaryScore);
            ConsiderStyle("Prudente", caution, ref primaryStyle, ref primaryScore);
            ConsiderStyle("Concentrado", focus, ref primaryStyle, ref primaryScore);
            ConsiderStyle("Distraido", distractibility, ref primaryStyle, ref primaryScore);

            return string.IsNullOrEmpty(primaryStyle) ? "Equilibrado" : primaryStyle;
        }

        private void ConsiderStyle(string style, float score, ref string primaryStyle, ref float primaryScore)
        {
            if (score <= primaryScore)
                return;

            primaryStyle = style;
            primaryScore = score;
        }

        private void LogMatchStyle(string style)
        {
            if (!logMatchStyleToConsole)
                return;

            Debug.Log($"[PepeBot] Match style: {style}", this);
        }

        private float GetRandomReactionTime()
        {
            return Mathf.Max(0.05f, Random.Range(reactionTimeMin, reactionTimeMax) * m_ReactionTimeMultiplier);
        }

        private void UpdateTarget()
        {
            Actor damageThreat = FindDamageSourceThreat();
            m_Target = damageThreat != null ? damageThreat : FindVisibleTarget();
            m_TargetIsRearThreat = damageThreat != null && m_DamageSourceWasRear && m_Target == damageThreat;
            m_TargetIsDamageSourceThreat = damageThreat != null && m_Target == damageThreat;

            if (m_Target != m_PreviousTarget)
            {
                if (m_PreviousTarget != null && m_Target == null)
                    m_PostCombatHealUntil = Time.time + postCombatHealDuration;

                m_TargetVisibleSince = m_Target != null ? Time.time : 0f;
                m_CurrentReactionTime = m_TargetIsRearThreat
                    ? rearThreatReactionTime
                    : m_TargetIsDamageSourceThreat
                        ? damageSourceReactionTime
                        : GetRandomReactionTime();
                m_PreviousTarget = m_Target;
                m_TargetIsTurret = IsTurretActor(m_Target);
                StartLookOvershootForNewTarget();

                if (m_TargetIsTurret)
                {
                    bool firstDetection = m_SeenTurretIds.Add(m_Target.GetInstanceID());
                    float awarenessDelay = firstDetection
                        ? Random.Range(turretFirstDetectionTimeMin, turretFirstDetectionTimeMax)
                        : GetTurretReactionTime();
                    m_TurretAwareAt = Time.time + awarenessDelay;
                    m_TurretRetreatUntil = 0f;
                }
            }
        }

        private Actor FindDamageSourceThreat()
        {
            if (Time.time >= m_DamageSourceThreatUntil || m_DamageSourceThreat == null || IsFriendlyActor(m_DamageSourceThreat))
                return null;

            Vector3 targetPoint = GetTargetPoint(m_DamageSourceThreat);
            float distance = Vector3.Distance(m_Eye.position, targetPoint);
            if (distance > damageSourceMaxDistance)
                return null;

            if (!HasLineOfSight(m_DamageSourceThreat, targetPoint, distance))
                return null;

            return m_DamageSourceThreat;
        }

        private void StartLookOvershootForNewTarget()
        {
            if (m_Target == null || m_TargetIsTurret || Random.value > lookOvershootChance)
                return;

            float duration = Random.Range(lookOvershootDurationMin, lookOvershootDurationMax);
            float angle = Random.Range(lookOvershootAngleMin, lookOvershootAngleMax) * RandomSign();

            if (m_TargetIsRearThreat)
            {
                duration *= 0.6f;
                angle *= 0.5f;
            }

            m_LookOvershootUntil = Time.time + duration;
            m_LookOvershootAngle = angle;
        }

        private bool IsDirectionBehind(Vector3 worldDirection)
        {
            Vector3 flatDirection = Vector3.ProjectOnPlane(worldDirection, Vector3.up);
            if (flatDirection.sqrMagnitude <= 0.01f)
                return false;

            float rearAngle = Vector3.Angle(-transform.forward, flatDirection.normalized);
            return rearAngle <= rearThreatAngle * 0.5f;
        }

        private Actor FindVisibleTarget()
        {
            Collider[] nearbyColliders = Physics.OverlapSphere(transform.position, sightRadius, sightMask, QueryTriggerInteraction.Ignore);
            Actor bestTarget = null;
            float bestScore = float.MaxValue;

            foreach (Collider nearbyCollider in nearbyColliders)
            {
                Actor candidate = nearbyCollider.GetComponentInParent<Actor>();
                if (candidate == null || candidate == m_SelfActor)
                    continue;

                if (IsFriendlyActor(candidate))
                    continue;

                Vector3 targetPoint = GetTargetPoint(candidate);
                Vector3 toCandidate = targetPoint - m_Eye.position;
                float distance = toCandidate.magnitude;
                if (distance <= 0.01f)
                    continue;

                float angle = Vector3.Angle(transform.forward, toCandidate);
                if (angle > sightConeAngle * 0.5f)
                    continue;

                if (!HasLineOfSight(candidate, targetPoint, distance))
                    continue;

                float score = distance + angle * 0.08f;
                if (IsTurretActor(candidate))
                    score -= turretPriorityBias;

                if (score < bestScore)
                {
                    bestScore = score;
                    bestTarget = candidate;
                }
            }

            return bestTarget;
        }

        private bool IsFriendlyActor(Actor candidate)
        {
            if (candidate == null || candidate == m_SelfActor)
                return true;

            PlayerSettings candidateSettings = candidate.GetComponent<PlayerSettings>();
            if (m_PlayerSettings != null &&
                candidateSettings != null &&
                m_PlayerSettings.TeamIndex >= 0 &&
                candidateSettings.TeamIndex >= 0)
            {
                return candidateSettings.TeamIndex == m_PlayerSettings.TeamIndex;
            }

            return ignoreSameAffiliation &&
                   m_SelfActor != null &&
                   candidate.Affiliation == m_SelfActor.Affiliation;
        }

        private bool HasLineOfSight(Actor candidate, Vector3 targetPoint, float distance)
        {
            Vector3 origin = m_Eye.position;
            Vector3 direction = (targetPoint - origin).normalized;
            RaycastHit[] hits = Physics.RaycastAll(origin, direction, distance, sightMask, QueryTriggerInteraction.Ignore);

            RaycastHit closestRelevantHit = default;
            bool hasRelevantHit = false;
            float closestDistance = float.MaxValue;

            foreach (RaycastHit hit in hits)
            {
                Actor hitActor = hit.collider.GetComponentInParent<Actor>();
                if (hitActor != null)
                    continue;

                if (hit.distance < closestDistance)
                {
                    closestDistance = hit.distance;
                    closestRelevantHit = hit;
                    hasRelevantHit = true;
                }
            }

            if (!hasRelevantHit)
                return true;

            Actor closestActor = closestRelevantHit.collider.GetComponentInParent<Actor>();
            return closestActor == candidate || closestRelevantHit.collider.transform.IsChildOf(candidate.transform);
        }

        private void PickNewWanderState()
        {
            m_NextDecisionTime = Time.time + Random.Range(decisionIntervalMin, decisionIntervalMax);
            m_StateUntil = m_NextDecisionTime;

            if (m_Target != null)
            {
                if (m_TargetIsTurret)
                    UpdateTurretRetreatState();

                m_NextSprintDecisionTime = Time.time;
                m_IsPaused = Random.value < 0.08f;
                m_SprintHeld = IsRetreatingFromTurret() || Random.value < sprintChance * 0.5f;
                m_CombatStrafe = Random.Range(-0.65f, 0.65f);
                m_CombatDistanceOffset = Random.Range(-combatPreferredDistanceVariation, combatPreferredDistanceVariation);
                m_EngageTargetMovement = Random.value < combatEngageMovementChance;
                TryStartCombatCrouch();
                return;
            }

            bool canPause = Time.time >= m_InitializedTime + initialNoPauseDuration;
            m_IsPaused = canPause && Random.value < pauseChance;
            float currentSprintChance = m_HasObjectiveDestination && sprintToCtfObjective
                ? objectiveSprintChance
                : sprintChance;
            UpdateNavigationSprint(currentSprintChance);

            float turnAngle = IsObstacleAhead() ? Random.Range(75f, 135f) * RandomSign() : Random.Range(-85f, 85f);
            m_WanderWorldDirection = Quaternion.Euler(0f, turnAngle, 0f) * transform.forward;
            m_WanderWorldDirection.y = 0f;
            m_WanderWorldDirection.Normalize();
            PickWanderDestination();

            TryStartImpatientJump();

        }

        private void UpdateNavigationSprint(float chance)
        {
            if (m_IsPaused)
            {
                m_SprintHeld = false;
                m_NextSprintDecisionTime = Time.time;
                return;
            }

            if (Time.time < m_NextSprintDecisionTime)
                return;

            m_SprintHeld = Random.value < chance;
            m_NextSprintDecisionTime = Time.time + (m_SprintHeld
                ? Random.Range(sprintBurstDurationMin, sprintBurstDurationMax)
                : Random.Range(decisionIntervalMin, decisionIntervalMax));
        }

        private void TryStartImpatientJump()
        {
            if (m_IsPaused || Time.time < m_NextImpatientJumpTime || Random.value >= jumpChance)
                return;

            if (m_Controller == null || !m_Controller.IsGrounded || m_OutputMoveInput.sqrMagnitude < 0.2f)
                return;

            Vector3 moveDirection = transform.TransformDirection(m_OutputMoveInput);
            moveDirection.y = 0f;
            if (moveDirection.sqrMagnitude < 0.01f ||
                HasObstacle(moveDirection.normalized, lowObstacleHeight, obstacleJumpDistance, out _))
                return;

            m_JumpDown = true;
            m_JumpHeldUntil = Time.time + Random.Range(0.08f, 0.18f);
            m_NextImpatientJumpTime = Time.time + Random.Range(impatientJumpCooldownMin, impatientJumpCooldownMax);
        }

        private void TryStartCombatCrouch()
        {
            if (m_Target == null || m_TargetIsTurret || m_IsBotCrouching || Random.value > combatCrouchChance)
                return;

            m_IsBotCrouching = true;
            m_CrouchDown = true;
            m_CrouchReleaseTime = Time.time + Random.Range(combatCrouchDurationMin, combatCrouchDurationMax);
            m_SprintHeld = false;
        }

        private void UpdateCrouchState()
        {
            if (!m_IsBotCrouching || Time.time < m_CrouchReleaseTime)
                return;

            m_IsBotCrouching = false;
            m_CrouchReleased = true;
        }

        private bool IsObstacleAhead()
        {
            Vector3 origin = transform.position + Vector3.up * 0.75f;
            RaycastHit[] hits = Physics.RaycastAll(origin, transform.forward, obstacleCheckDistance, sightMask, QueryTriggerInteraction.Ignore);

            foreach (RaycastHit hit in hits)
            {
                Actor hitActor = hit.collider.GetComponentInParent<Actor>();
                if (hitActor != null)
                    continue;

                return true;
            }

            return false;
        }

        private void UpdateAimNoise()
        {
            if (Time.time < m_NextAimNoiseTime)
                return;

            m_NextAimNoiseTime = Time.time + Random.Range(0.25f, 0.7f);
            float personalityAimNoise = aimNoise * m_AimNoiseMultiplier;
            m_AimOffset = new Vector3(
                Random.Range(-personalityAimNoise, personalityAimNoise),
                Random.Range(-personalityAimNoise * 0.45f, personalityAimNoise * 0.45f),
                Random.Range(-personalityAimNoise, personalityAimNoise));
        }

        private void UpdateLookInput()
        {
            Vector3 desiredDirection;

            if (m_Target != null && (!m_TargetIsTurret || IsTurretAware()))
            {
                Vector3 targetNoise = GetTargetAimNoise();
                desiredDirection = GetTargetPoint(m_Target) + targetNoise - m_Eye.position;
            }
            else if (Time.time < m_DamageSourceLookUntil && m_DamageSourceLookDirection.sqrMagnitude > 0.01f)
            {
                desiredDirection = m_DamageSourceLookDirection;
            }
            else if (m_IsSeekingHealth)
            {
                desiredDirection = GetNavigationLookDirection(m_HealingDestination);
            }
            else if (m_HasObjectiveDestination)
            {
                desiredDirection = GetObjectiveLookDirection();
            }
            else if (m_WanderWorldDirection.sqrMagnitude > 0.01f)
            {
                desiredDirection = m_WanderWorldDirection + Vector3.up * Random.Range(-0.03f, 0.04f);
            }
            else
            {
                desiredDirection = transform.forward;
            }

            if (desiredDirection.sqrMagnitude < 0.01f)
                return;

            desiredDirection = ApplyHumanLookVariation(desiredDirection);

            Vector3 flatDesired = Vector3.ProjectOnPlane(desiredDirection, Vector3.up);
            float yawDelta = flatDesired.sqrMagnitude > 0.01f
                ? Vector3.SignedAngle(transform.forward, flatDesired.normalized, Vector3.up)
                : 0f;

            Vector3 localDesired = transform.InverseTransformDirection(desiredDirection.normalized);
            float desiredPitch = -Mathf.Asin(Mathf.Clamp(localDesired.y, -1f, 1f)) * Mathf.Rad2Deg;
            float currentPitch = m_Eye != null ? NormalizeAngle(m_Eye.localEulerAngles.x) : 0f;
            float pitchDelta = Mathf.DeltaAngle(currentPitch, desiredPitch);

            float rotationSpeed = m_Controller != null ? Mathf.Max(1f, m_Controller.RotationSpeed) : 200f;

            float lookLimit = maxLookInputPerFrame;
            float lookSharpness = lookInputSharpness;
            if (m_TargetIsTurret)
            {
                lookLimit = turretMaxLookInputPerFrame;
                lookSharpness = turretLookInputSharpness;
            }
            else if (m_TargetIsRearThreat)
            {
                lookLimit = rearThreatMaxLookInputPerFrame;
                lookSharpness = rearThreatLookInputSharpness;
            }
            else if (m_TargetIsDamageSourceThreat || Time.time < m_DamageSourceLookUntil)
            {
                lookLimit = damageSourceMaxLookInputPerFrame;
                lookSharpness = damageSourceLookInputSharpness;
            }

            Vector2 targetLookInput = new Vector2(
                Mathf.Clamp(yawDelta / rotationSpeed, -lookLimit, lookLimit),
                Mathf.Clamp(pitchDelta / rotationSpeed, -lookLimit, lookLimit));

            m_LookInput = Vector2.Lerp(m_LookInput, targetLookInput, Time.deltaTime * lookSharpness);
        }

        private Vector3 ApplyHumanLookVariation(Vector3 desiredDirection)
        {
            if (m_TargetIsTurret)
                return desiredDirection;

            Vector3 variedDirection = desiredDirection;

            if (Time.time < m_LookOvershootUntil)
                variedDirection = Quaternion.Euler(0f, m_LookOvershootAngle, 0f) * variedDirection;

            if (Time.time < m_DamageFlinchUntil)
                variedDirection = Quaternion.Euler(m_DamageFlinchPitch, m_DamageFlinchYaw, 0f) * variedDirection;

            return variedDirection;
        }

        private Vector3 GetObjectiveLookDirection()
        {
            Vector3 toObjective = GetNavigationLookDirection(m_ObjectiveDestination);

            if (Time.time >= m_NextObjectiveSideLookTime)
            {
                m_NextObjectiveSideLookTime = Time.time +
                    Random.Range(objectiveSideLookIntervalMin, objectiveSideLookIntervalMax) * m_ObjectiveSideLookIntervalMultiplier;

                if (Random.value < Mathf.Clamp01(objectiveSideLookChance * m_ObjectiveSideLookChanceMultiplier))
                {
                    m_ObjectiveSideLookUntil = Time.time + Random.Range(objectiveSideLookDurationMin, objectiveSideLookDurationMax);
                    m_ObjectiveSideLookAngle = Random.Range(objectiveSideLookAngleMin, objectiveSideLookAngleMax) * RandomSign();
                }
            }

            if (Time.time >= m_ObjectiveSideLookUntil)
                return toObjective;

            Vector3 flatObjective = Vector3.ProjectOnPlane(toObjective, Vector3.up);
            if (flatObjective.sqrMagnitude < 0.01f)
                flatObjective = transform.forward;

            Vector3 sideDirection = Quaternion.Euler(0f, m_ObjectiveSideLookAngle, 0f) * flatObjective.normalized;
            return sideDirection + Vector3.up * Mathf.Clamp(toObjective.normalized.y, -0.15f, 0.15f);
        }

        private Vector3 GetNavigationLookDirection(Vector3 destination)
        {
            Vector3 lookPoint = destination;
            if (TryGetPathDirection(destination, out _))
                lookPoint = m_CurrentPathSteeringPoint;

            return lookPoint + Vector3.up * 1.2f - m_Eye.position;
        }

        private void UpdateFlagStateHumanReaction()
        {
            bool isCarryingFlag = IsCarryingFlag();
            if (isCarryingFlag == m_LastCarryingFlagState)
                return;

            m_LastCarryingFlagState = isCarryingFlag;
            m_ObjectiveSideLookUntil = Time.time + Random.Range(0.45f, 0.9f);
            m_ObjectiveSideLookAngle = Random.Range(55f, 105f) * RandomSign();
            m_ObjectiveHesitationUntil = Time.time + Random.Range(0.12f, 0.3f);
            m_ObjectiveHesitationInput = new Vector3(Random.Range(-0.35f, 0.35f), 0f, Random.Range(-0.1f, 0.2f));
        }

        private void UpdateObjectiveHesitation()
        {
            if (m_Target != null || m_IsSeekingHealth || !m_HasObjectiveDestination)
                return;

            if (Time.time < m_ObjectiveHesitationUntil || Time.time < m_NextObjectiveHesitationTime)
                return;

            m_NextObjectiveHesitationTime = Time.time + Random.Range(objectiveHesitationIntervalMin, objectiveHesitationIntervalMax);

            float chance = IsCarryingFlag() ? objectiveHesitationChance * 0.45f : objectiveHesitationChance;
            chance *= m_ObjectiveHesitationChanceMultiplier;
            if (Random.value > chance)
                return;

            m_ObjectiveHesitationUntil = Time.time +
                Random.Range(objectiveHesitationDurationMin, objectiveHesitationDurationMax) * m_ObjectiveHesitationDurationMultiplier;
            m_ObjectiveHesitationInput = new Vector3(
                Random.Range(-0.45f, 0.45f),
                0f,
                Random.Range(-0.15f, 0.25f));
        }

        private void UpdateMoveInput()
        {
            if (m_IsSeekingHealth)
            {
                UpdateHealingMoveInput();
                return;
            }

            if (m_Target != null && m_TargetIsTurret && IsTurretAware())
            {
                UpdateTurretMoveInput();
                return;
            }

            if (m_Target != null && m_TargetIsRearThreat)
            {
                UpdateCombatMoveInput();
                return;
            }

            if (m_Target != null && m_TargetIsDamageSourceThreat)
            {
                UpdateCombatMoveInput();
                return;
            }

            if (m_Target != null && !m_TargetIsTurret && m_EngageTargetMovement)
            {
                UpdateCombatMoveInput();
                return;
            }

            if (m_HasObjectiveDestination)
            {
                if (m_IsPaused)
                {
                    m_MoveInput = Vector3.zero;
                    m_SprintHeld = false;
                    return;
                }

                if (Time.time < m_ObjectiveHesitationUntil)
                {
                    m_MoveInput = Vector3.ClampMagnitude(m_ObjectiveHesitationInput, 1f);
                    m_SprintHeld = false;
                    ApplyObstacleHandling();
                    return;
                }

                UpdateObjectiveMoveInput();
                return;
            }

            if (m_IsPaused)
            {
                m_MoveInput = Vector3.zero;
                return;
            }

            if (m_Target != null && !m_TargetIsTurret)
            {
                UpdateCombatMoveInput();
                return;
            }

            Vector3 localWander = transform.InverseTransformDirection(m_WanderWorldDirection);
            if (TryGetWanderPathDirection(out Vector3 wanderPathDirection))
                localWander = transform.InverseTransformDirection(wanderPathDirection);

            if (localWander.sqrMagnitude < 0.01f)
                localWander = Vector3.forward;

            Vector3 wanderInput = Vector3.ClampMagnitude(new Vector3(localWander.x, 0f, localWander.z), 1f);
            float remainingDistance = m_HasWanderDestination
                ? Vector3.Distance(transform.position, m_WanderDestination)
                : wanderPointMinDistance;
            m_MoveInput = ApplyTraversalHumanization(wanderInput, remainingDistance);
            ApplyObstacleHandling();
        }

        private void UpdateCombatMoveInput()
        {
            Vector3 toTarget = GetTargetPoint(m_Target) - transform.position;
            float distance = toTarget.magnitude;
            Vector3 localToTarget = transform.InverseTransformDirection(toTarget.normalized);

            float forward = 0.15f;
            float preferredFireDistance = comfortableFireDistance + m_CombatDistanceOffset;
            float closeRetreatDistance = 7f + m_CombatDistanceOffset * 0.2f;

            if (distance > preferredFireDistance)
                forward = 0.75f;
            else if (distance < closeRetreatDistance)
                forward = -0.45f;

            Vector3 combatInput = new Vector3(
                Mathf.Clamp(localToTarget.x * 0.35f + m_CombatStrafe, -1f, 1f),
                0f,
                forward);

            m_MoveInput = Vector3.ClampMagnitude(combatInput, 1f);
            ApplyObstacleHandling();
        }

        private void UpdateWeaponTactics()
        {
            if (!m_TargetIsTurret || m_Target == null || !IsTurretAware())
                return;

            WeaponController activeWeapon = GetActiveWeapon();
            if (activeWeapon == null)
                return;

            float ammoNeededRatio = activeWeapon.GetAmmoNeededToShoot();
            bool needsAmmo = activeWeapon.CurrentAmmoRatio <= Mathf.Max(turretReloadAmmoRatio, ammoNeededRatio * 1.2f);
            bool stillRecoveringAmmo = Time.time < m_TurretReloadRetreatUntil &&
                                       activeWeapon.CurrentAmmoRatio < turretResumeFireAmmoRatio;

            if (!needsAmmo && !activeWeapon.IsReloading && !stillRecoveringAmmo)
                return;

            m_TurretReloadRetreatUntil = Mathf.Max(m_TurretReloadRetreatUntil, Time.time + turretReloadRetreatDuration);

            if (!activeWeapon.AutomaticReload && !activeWeapon.IsReloading && activeWeapon.CurrentAmmoRatio < 1f)
                m_ReloadDown = true;
        }

        private void UpdateFireInput()
        {
            m_FireHeldPrevious = m_FireHeld;
            m_FireHeld = false;
            m_AimHeld = false;

            if (m_IsSeekingHealth)
            {
                m_FireDown = false;
                m_FireReleased = m_FireHeldPrevious;
                return;
            }

            if (m_Target != null)
            {
                Vector3 targetPoint = GetTargetPoint(m_Target);
                Vector3 toTarget = targetPoint - m_Eye.position;
                float distance = toTarget.magnitude;
                float aimAngle = Vector3.Angle(m_Eye.forward, toTarget);
                bool reacted = Time.time >= m_TargetVisibleSince + m_CurrentReactionTime;
                bool isTurretOpening = m_TargetIsTurret && IsTurretOpeningWindow();
                bool isReloadingForTurret = m_TargetIsTurret && IsReloadingOrRecoveringFromTurret();
                if (m_TargetIsTurret)
                    reacted = IsTurretAware();

                float fireDistance = m_TargetIsTurret ? turretFireDistance : comfortableFireDistance;
                float fireAngle = m_TargetIsTurret ? turretAimAngleToFire : aimAngleToFire;
                if (m_TargetIsRearThreat)
                    fireAngle = rearThreatAimAngleToFire;

                float aimHoldAngle = m_TargetIsTurret ? Mathf.Max(aimAngleToAim, turretAimAngleToFire) : aimAngleToAim;
                if (m_TargetIsRearThreat)
                    aimHoldAngle = Mathf.Max(aimAngleToAim, rearThreatAimAngleToFire);
                if (!m_TargetIsTurret && distance < 10f)
                    fireAngle += closeEnemyFireAngleBonus;

                bool canFire = reacted && !isReloadingForTurret &&
                               distance <= fireDistance && aimAngle <= fireAngle;
                m_AimHeld = reacted && distance <= fireDistance * 1.25f && aimAngle <= aimHoldAngle;

                if (m_TargetIsTurret)
                {
                    if (canFire && !isTurretOpening && Time.time >= m_NextFireBurstTime && Time.time >= m_FireBurstUntil)
                    {
                        m_FireBurstUntil = Time.time + Random.Range(enemyBurstMin, enemyBurstMax) * m_BurstDurationMultiplier;
                        m_NextFireBurstTime = m_FireBurstUntil + Random.Range(enemyBurstPauseMin, enemyBurstPauseMax) * m_BurstPauseMultiplier;
                    }

                    m_FireHeld = canFire && (isTurretOpening || Time.time < m_FireBurstUntil);
                    m_FireDown = m_FireHeld && !m_FireHeldPrevious;
                    m_FireReleased = !m_FireHeld && m_FireHeldPrevious;
                    return;
                }

                if (canFire && Time.time >= m_NextFireBurstTime && Time.time >= m_FireBurstUntil)
                {
                    m_FireBurstUntil = Time.time + Random.Range(enemyBurstMin, enemyBurstMax) * m_BurstDurationMultiplier;
                    m_NextFireBurstTime = m_FireBurstUntil + Random.Range(enemyBurstPauseMin, enemyBurstPauseMax) * m_BurstPauseMultiplier;
                }

                m_FireHeld = canFire && Time.time < m_FireBurstUntil;
            }

            m_FireDown = m_FireHeld && !m_FireHeldPrevious;
            m_FireReleased = !m_FireHeld && m_FireHeldPrevious;
        }

        private Vector3 GetTargetPoint(Actor actor)
        {
            if (actor != null && actor.AimPoint != null)
                return actor.AimPoint.position;

            return actor != null ? actor.transform.position + Vector3.up * 1.25f : transform.position + transform.forward;
        }

        private void RememberVisibleHealthPickups()
        {
            HealthPickup[] healthPickups = FindObjectsByType<HealthPickup>(FindObjectsSortMode.None);

            foreach (HealthPickup healthPickup in healthPickups)
            {
                if (healthPickup == null || !healthPickup.gameObject.activeInHierarchy)
                    continue;

                if (!CanDiscoverHealthPickup(healthPickup))
                    continue;

                if (Random.value < Mathf.Clamp01(healthMemorySkipChance * m_HealthMemoryMultiplier))
                    continue;

                RememberHealthPickupPosition(healthPickup.transform.position);
            }
        }

        private bool CanDiscoverHealthPickup(HealthPickup healthPickup)
        {
            if (m_Eye == null)
                return false;

            Collider pickupCollider = healthPickup.GetComponentInChildren<Collider>();
            Vector3 targetPoint = pickupCollider != null
                ? pickupCollider.bounds.center
                : healthPickup.transform.position + Vector3.up * 0.5f;

            Vector3 toPickup = targetPoint - m_Eye.position;
            float distance = toPickup.magnitude;
            if (distance <= 0.01f || distance > healthPickupDiscoveryRadius)
                return false;

            bool isNearby = distance <= healthPickupNearbyAwarenessDistance;
            if (!isNearby && Vector3.Angle(m_Eye.forward, toPickup) > healthPickupDiscoveryAngle * 0.5f)
                return false;

            RaycastHit[] hits = Physics.RaycastAll(
                m_Eye.position,
                toPickup.normalized,
                distance,
                sightMask,
                QueryTriggerInteraction.Collide);

            RaycastHit closestHit = default;
            float closestDistance = float.MaxValue;

            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.transform.IsChildOf(transform))
                    continue;

                HealthPickup hitPickup = hit.collider.GetComponentInParent<HealthPickup>();
                if (hit.collider.isTrigger && hitPickup == null)
                    continue;

                if (hit.distance < closestDistance)
                {
                    closestDistance = hit.distance;
                    closestHit = hit;
                }
            }

            if (closestDistance == float.MaxValue)
                return true;

            return closestHit.collider.GetComponentInParent<HealthPickup>() == healthPickup;
        }

        private void RememberHealthPickupPosition(Vector3 position)
        {
            for (int i = 0; i < m_KnownHealthPickups.Count; i++)
            {
                if (Vector3.Distance(m_KnownHealthPickups[i], position) <= duplicateHealthMemoryDistance)
                {
                    m_KnownHealthPickups[i] = position;
                    return;
                }
            }

            m_KnownHealthPickups.Add(position);
        }

        private void UpdateCtfObjective()
        {
            m_HasObjectiveDestination = false;

            if (!pursueCtfObjective || m_PlayerSettings == null)
                return;

            int teamIndex = m_PlayerSettings.TeamIndex;
            if (teamIndex < 0)
                return;

            if (IsCarryingFlag())
            {
                if (TryGetCaptureZonePosition(teamIndex, out Vector3 homePosition))
                {
                    m_ObjectiveDestination = homePosition;
                    m_HasObjectiveDestination = true;
                }

                return;
            }

            int enemyTeamIndex = teamIndex == 0 ? 1 : 0;
            if (TryGetFlagPosition(enemyTeamIndex, out Vector3 flagPosition))
            {
                m_ObjectiveDestination = flagPosition;
                m_HasObjectiveDestination = true;
            }
        }

        private bool IsCarryingFlag()
        {
            return m_FlagCarrierTracker != null && m_FlagCarrierTracker.IsCarryingFlag.Value;
        }

        private bool TryGetFlagPosition(int flagTeamIndex, out Vector3 flagPosition)
        {
            flagPosition = Vector3.zero;
            FlagPickup[] flags = FindObjectsByType<FlagPickup>(FindObjectsSortMode.None);
            float bestDistance = float.MaxValue;
            bool found = false;

            foreach (FlagPickup flag in flags)
            {
                if (flag == null || !flag.gameObject.activeInHierarchy || flag.FlagTeamIndex != flagTeamIndex)
                    continue;

                float distance = Vector3.Distance(transform.position, flag.transform.position);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    flagPosition = flag.transform.position;
                    found = true;
                }
            }

            return found;
        }

        private bool TryGetCaptureZonePosition(int teamIndex, out Vector3 zonePosition)
        {
            zonePosition = Vector3.zero;
            CaptureZone[] captureZones = FindObjectsByType<CaptureZone>(FindObjectsSortMode.None);

            foreach (CaptureZone captureZone in captureZones)
            {
                if (captureZone == null || !captureZone.gameObject.activeInHierarchy || captureZone.BaseTeamIndex != teamIndex)
                    continue;

                zonePosition = captureZone.transform.position;
                if (captureZone.TryGetComponent<BoxCollider>(out BoxCollider boxCollider))
                    zonePosition = captureZone.transform.TransformPoint(boxCollider.center);

                return true;
            }

            return false;
        }

        private void UpdateHealingIntent()
        {
            m_IsSeekingHealth = false;

            if (m_Health == null || m_KnownHealthPickups.Count == 0)
                return;

            float healthRatio = m_Health.GetRatio();
            bool lowHealth = healthRatio <= Mathf.Clamp01(lowHealthSeekRatio * m_HealingThresholdMultiplier);
            bool recoveringAfterCombat = Time.time < m_PostCombatHealUntil &&
                                          healthRatio < Mathf.Clamp01(postCombatHealRatio * m_HealingThresholdMultiplier);

            if (!lowHealth && !recoveringAfterCombat)
                return;

            if (TryGetNearestKnownHealthPickup(out Vector3 nearestHealthPosition))
            {
                m_HealingDestination = nearestHealthPosition;
                m_IsSeekingHealth = true;
                m_SprintHeld = lowHealth;
            }
        }

        private bool TryGetNearestKnownHealthPickup(out Vector3 nearestPosition)
        {
            nearestPosition = Vector3.zero;
            float bestDistance = float.MaxValue;
            bool found = false;

            for (int i = 0; i < m_KnownHealthPickups.Count; i++)
            {
                float distance = Vector3.Distance(transform.position, m_KnownHealthPickups[i]);
                float memoryBias = StablePositionNoise(m_KnownHealthPickups[i]) * healthPickupSelectionNoise * m_HealthMemoryMultiplier;
                float score = distance + memoryBias;
                if (score < bestDistance)
                {
                    bestDistance = score;
                    nearestPosition = m_KnownHealthPickups[i];
                    found = true;
                }
            }

            return found;
        }

        private void UpdateHealingMoveInput()
        {
            Vector3 toHealth = m_HealingDestination - transform.position;
            toHealth.y = 0f;

            if (toHealth.magnitude <= healthPickupReachDistance)
            {
                if (!HasActiveHealthPickupNear(m_HealingDestination))
                {
                    ForgetHealthPickupPosition(m_HealingDestination);
                    m_MoveInput = Vector3.zero;
                    return;
                }
            }

            if (toHealth.sqrMagnitude < 0.01f)
            {
                m_MoveInput = Vector3.zero;
                return;
            }

            Vector3 worldDirection = toHealth.normalized;
            if (TryGetPathDirection(m_HealingDestination, out Vector3 pathDirection))
                worldDirection = pathDirection;

            Vector3 localToHealth = transform.InverseTransformDirection(worldDirection);
            Vector3 healingInput = new Vector3(
                Mathf.Clamp(localToHealth.x, -1f, 1f),
                0f,
                Mathf.Clamp(localToHealth.z, -1f, 1f));

            m_MoveInput = ApplyTraversalHumanization(healingInput, toHealth.magnitude);
            ApplyObstacleHandling();
        }

        private void UpdateObjectiveMoveInput()
        {
            Vector3 toObjective = m_ObjectiveDestination - transform.position;
            toObjective.y = 0f;

            if (toObjective.sqrMagnitude < 0.01f)
            {
                m_MoveInput = Vector3.zero;
                return;
            }

            Vector3 worldDirection = toObjective.normalized;
            if (toObjective.magnitude > objectiveReachDistance &&
                TryGetPathDirection(m_ObjectiveDestination, out Vector3 pathDirection))
            {
                worldDirection = pathDirection;
            }

            Vector3 localToObjective = transform.InverseTransformDirection(worldDirection);
            Vector3 objectiveInput = new Vector3(
                Mathf.Clamp(localToObjective.x, -1f, 1f),
                0f,
                Mathf.Clamp(localToObjective.z, -1f, 1f));

            m_MoveInput = ApplyTraversalHumanization(objectiveInput, toObjective.magnitude);
            ApplyObstacleHandling();
        }

        private Vector3 ApplyTraversalHumanization(Vector3 baseInput, float remainingDistance)
        {
            UpdateTraversalStrafe();

            float destinationFade = Mathf.Clamp01((remainingDistance - 1.5f) / 4f);
            Vector3 humanizedInput = baseInput;
            humanizedInput.x = Mathf.Clamp(
                humanizedInput.x + m_TraversalStrafe * destinationFade,
                -1f,
                1f);

            return Vector3.ClampMagnitude(humanizedInput, 1f);
        }

        private void UpdateTraversalStrafe()
        {
            if (Time.time >= m_TraversalStrafeUntil)
                m_TraversalStrafe = 0f;

            if (Time.time < m_NextTraversalStrafeTime)
                return;

            if (Random.value >= traversalStrafeChance)
            {
                m_NextTraversalStrafeTime = Time.time +
                    Random.Range(traversalStrafeIntervalMin, traversalStrafeIntervalMax);
                return;
            }

            float duration = Random.Range(traversalStrafeDurationMin, traversalStrafeDurationMax);
            float strength = Random.Range(traversalStrafeInputMin, traversalStrafeInputMax);
            if (Random.value < traversalWallBiasChance)
            {
                duration *= 1.8f;
                strength = Mathf.Min(strength * 1.35f, 0.35f);
            }

            m_TraversalStrafe = strength * RandomSign();
            m_TraversalStrafeUntil = Time.time + duration;
            m_NextTraversalStrafeTime = m_TraversalStrafeUntil +
                Random.Range(traversalStrafeIntervalMin, traversalStrafeIntervalMax);
        }

        private bool TryGetPathDirection(Vector3 destination, out Vector3 worldDirection)
        {
            worldDirection = Vector3.zero;

            if (Time.time >= m_NextPathUpdateTime || Vector3.Distance(destination, m_LastPathDestination) > 1.5f)
            {
                m_NextPathUpdateTime = Time.time + pathRepathInterval;
                m_LastPathDestination = destination;
                m_HasPathSteeringPoint = false;
                NavMeshPath navPath = new NavMeshPath();

                if (NavMesh.SamplePosition(destination, out NavMeshHit destinationHit, navMeshSampleDistance, NavMesh.AllAreas) &&
                    NavMesh.SamplePosition(transform.position, out NavMeshHit selfHit, navMeshSampleDistance, NavMesh.AllAreas) &&
                    NavMesh.CalculatePath(selfHit.position, destinationHit.position, NavMesh.AllAreas, navPath) &&
                    navPath.status != NavMeshPathStatus.PathInvalid &&
                    navPath.corners.Length > 1)
                {
                    int cornerIndex = 1;
                    while (cornerIndex < navPath.corners.Length - 1 &&
                           Vector3.Distance(transform.position, navPath.corners[cornerIndex]) < pathCornerReachDistance)
                    {
                        cornerIndex++;
                    }

                    m_CurrentPathSteeringPoint = navPath.corners[cornerIndex];
                    m_HasPathSteeringPoint = true;
                }
            }

            if (!m_HasPathSteeringPoint)
                return false;

            Vector3 toCorner = m_CurrentPathSteeringPoint - transform.position;
            toCorner.y = 0f;

            if (toCorner.sqrMagnitude < 0.05f)
                return false;

            worldDirection = toCorner.normalized;
            return true;
        }

        private void PickWanderDestination()
        {
            m_HasWanderDestination = false;

            for (int attempt = 0; attempt < 8; attempt++)
            {
                float distance = Random.Range(wanderPointMinDistance, wanderPointMaxDistance);
                float angle = Random.Range(-120f, 120f);
                Vector3 candidate = transform.position + Quaternion.Euler(0f, angle, 0f) * transform.forward * distance;

                if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, navMeshSampleDistance, NavMesh.AllAreas))
                {
                    m_WanderDestination = hit.position;
                    m_HasWanderDestination = true;
                    m_NextPathUpdateTime = 0f;
                    return;
                }
            }
        }

        private bool TryGetWanderPathDirection(out Vector3 worldDirection)
        {
            worldDirection = Vector3.zero;

            if (!m_HasWanderDestination)
                PickWanderDestination();

            if (!m_HasWanderDestination)
                return false;

            Vector3 toDestination = m_WanderDestination - transform.position;
            toDestination.y = 0f;

            if (toDestination.magnitude <= wanderDestinationReachDistance)
            {
                PickWanderDestination();
                return false;
            }

            return TryGetPathDirection(m_WanderDestination, out worldDirection);
        }

        private bool HasActiveHealthPickupNear(Vector3 position)
        {
            HealthPickup[] healthPickups = FindObjectsByType<HealthPickup>(FindObjectsSortMode.None);

            foreach (HealthPickup healthPickup in healthPickups)
            {
                if (healthPickup != null &&
                    healthPickup.gameObject.activeInHierarchy &&
                    Vector3.Distance(healthPickup.transform.position, position) <= duplicateHealthMemoryDistance)
                {
                    return true;
                }
            }

            return false;
        }

        private void ForgetHealthPickupPosition(Vector3 position)
        {
            for (int i = m_KnownHealthPickups.Count - 1; i >= 0; i--)
            {
                if (Vector3.Distance(m_KnownHealthPickups[i], position) <= duplicateHealthMemoryDistance)
                    m_KnownHealthPickups.RemoveAt(i);
            }
        }

        private Vector3 GetTargetAimNoise()
        {
            if (m_TargetIsTurret)
                return m_AimOffset * turretAimNoiseMultiplier;

            if (m_TargetIsRearThreat)
                return m_AimOffset * rearThreatAimNoiseMultiplier;

            if (m_TargetIsDamageSourceThreat)
                return m_AimOffset * damageSourceAimNoiseMultiplier;

            if (m_Target == null || m_Eye == null)
                return m_AimOffset;

            Vector3 toTarget = GetTargetPoint(m_Target) - m_Eye.position;
            float aimAngle = Vector3.Angle(m_Eye.forward, toTarget);
            float multiplier = aimAngle <= aimAngleToAim ? focusedAimNoiseMultiplier : 1f;
            return m_AimOffset * multiplier;
        }

        private bool IsTurretActor(Actor actor)
        {
            return actor != null && actor.GetComponent<EnemyTurret>() != null;
        }

        private float GetTurretReactionTime()
        {
            return Mathf.Max(0.03f, turretReactionTime / m_TurretAggressionMultiplier);
        }

        private bool IsTurretAware()
        {
            return m_TargetIsTurret && Time.time >= m_TurretAwareAt;
        }

        private float GetTurretOpeningFireTime()
        {
            return turretOpeningFireTime * m_TurretAggressionMultiplier;
        }

        private float GetTurretSafeDistance()
        {
            return turretSafeDistance / Mathf.Sqrt(m_TurretAggressionMultiplier);
        }

        private float GetTurretRetreatDuration()
        {
            return turretRetreatDuration / Mathf.Sqrt(m_TurretAggressionMultiplier);
        }

        private bool IsTurretOpeningWindow()
        {
            return IsTurretAware() && Time.time < m_TurretAwareAt + GetTurretOpeningFireTime();
        }

        private bool IsRetreatingFromTurret()
        {
            if (!m_TargetIsTurret || m_Target == null || !IsTurretAware())
                return false;

            float distance = Vector3.Distance(transform.position, GetTargetPoint(m_Target));
            return Time.time < m_TurretRetreatUntil ||
                   IsUnderTurretDamagePressure() ||
                   IsReloadingOrRecoveringFromTurret() ||
                   (!IsTurretOpeningWindow() && distance < GetTurretSafeDistance());
        }

        private bool IsUnderTurretDamagePressure()
        {
            return m_TargetIsTurret && Time.time < m_TurretDamagePressureRetreatUntil;
        }

        private bool IsReloadingOrRecoveringFromTurret()
        {
            if (!m_TargetIsTurret)
                return false;

            WeaponController activeWeapon = GetActiveWeapon();
            if (activeWeapon == null)
                return false;

            return activeWeapon.IsReloading ||
                   (Time.time < m_TurretReloadRetreatUntil &&
                    activeWeapon.CurrentAmmoRatio < turretResumeFireAmmoRatio);
        }

        private WeaponController GetActiveWeapon()
        {
            return m_WeaponsManager != null ? m_WeaponsManager.GetActiveWeapon() : null;
        }

        private void UpdateTurretRetreatState()
        {
            if (!m_TargetIsTurret || m_Target == null || !IsTurretAware())
                return;

            float distance = Vector3.Distance(transform.position, GetTargetPoint(m_Target));
            if (!IsTurretOpeningWindow() && distance < GetTurretSafeDistance())
                m_TurretRetreatUntil = Mathf.Max(m_TurretRetreatUntil, Time.time + GetTurretRetreatDuration());
        }

        private void UpdateTurretMoveInput()
        {
            Vector3 targetPoint = GetTargetPoint(m_Target);
            Vector3 toTarget = targetPoint - transform.position;
            toTarget.y = 0f;

            if (toTarget.sqrMagnitude < 0.01f)
            {
                m_MoveInput = Vector3.zero;
                return;
            }

            float distance = toTarget.magnitude;

            if (IsRetreatingFromTurret())
            {
                Vector3 awayFromTurret = -toTarget.normalized;
                Vector3 localAway = transform.InverseTransformDirection(awayFromTurret);
                bool pressureRetreat = IsUnderTurretDamagePressure();
                float strafeDirection = Mathf.Abs(m_CombatStrafe) > 0.05f
                    ? Mathf.Sign(m_CombatStrafe)
                    : Mathf.Sign(localAway.x);
                if (Mathf.Abs(strafeDirection) < 0.5f)
                    strafeDirection = 1f;

                float retreatX = localAway.x + m_CombatStrafe * (pressureRetreat ? 0.85f : 0.35f);
                float retreatZ = localAway.z;
                if (pressureRetreat)
                {
                    retreatX += strafeDirection * 0.45f;
                    retreatZ = Mathf.Clamp(retreatZ, -0.55f, 0.6f);
                }

                Vector3 retreatInput = new Vector3(
                    Mathf.Clamp(retreatX, -1f, 1f),
                    0f,
                    Mathf.Clamp(retreatZ, -1f, 1f));

                m_MoveInput = Vector3.ClampMagnitude(retreatInput, 1f);
                m_SprintHeld = true;
                ApplyObstacleHandling();
                return;
            }

            Vector3 localToTarget = transform.InverseTransformDirection(toTarget.normalized);
            float forward = distance > turretPreferredDistance ? 0.45f : -0.15f;
            Vector3 peekInput = new Vector3(
                Mathf.Clamp(localToTarget.x * 0.25f + m_CombatStrafe * 0.35f, -1f, 1f),
                0f,
                forward);

            m_MoveInput = Vector3.ClampMagnitude(peekInput, 1f);
            ApplyObstacleHandling();
        }

        private void ApplyObstacleHandling()
        {
            if (m_MoveInput.sqrMagnitude < 0.05f)
                return;

            Vector3 desiredWorldDirection = transform.TransformDirection(m_MoveInput);
            desiredWorldDirection.y = 0f;

            if (desiredWorldDirection.sqrMagnitude < 0.01f)
                return;

            desiredWorldDirection.Normalize();

            bool lowBlocked = HasObstacle(desiredWorldDirection, lowObstacleHeight, obstacleJumpDistance, out _);
            bool highBlocked = HasObstacle(desiredWorldDirection, highObstacleHeight, wallAvoidanceDistance, out _);
            bool wallClose = HasObstacle(desiredWorldDirection, lowObstacleHeight, wallAvoidanceDistance, out _);

            if (lowBlocked && !highBlocked && Time.time >= m_NextObstacleJumpTime)
            {
                m_JumpDown = true;
                m_JumpHeldUntil = Time.time + Random.Range(0.10f, 0.18f);
                m_NextObstacleJumpTime = Time.time + obstacleJumpCooldown;
            }

            if (!wallClose && !highBlocked)
                return;

            Vector3 leftDirection = Quaternion.Euler(0f, -sideProbeAngle, 0f) * desiredWorldDirection;
            Vector3 rightDirection = Quaternion.Euler(0f, sideProbeAngle, 0f) * desiredWorldDirection;
            float leftClearance = GetClearance(leftDirection);
            float rightClearance = GetClearance(rightDirection);

            if (Time.time >= m_AvoidanceSideUntil)
            {
                m_AvoidanceSide = rightClearance >= leftClearance ? 1f : -1f;
                m_AvoidanceSideUntil = Time.time + Random.Range(0.35f, 0.7f);
            }

            Vector3 sideWorldDirection = m_AvoidanceSide > 0f ? transform.right : -transform.right;
            Vector3 avoidWorldDirection = Vector3.Lerp(desiredWorldDirection, sideWorldDirection, 0.75f).normalized;
            Vector3 localAvoidDirection = transform.InverseTransformDirection(avoidWorldDirection);
            float localForward = m_MoveInput.z < -0.1f
                ? Mathf.Min(-0.1f, localAvoidDirection.z)
                : Mathf.Max(0.1f, localAvoidDirection.z);

            m_MoveInput = Vector3.ClampMagnitude(new Vector3(localAvoidDirection.x, 0f, localForward), 1f);
            m_SprintHeld = false;
        }

        private bool HasObstacle(Vector3 worldDirection, float height, float distance, out RaycastHit closestHit)
        {
            Vector3 origin = transform.position + Vector3.up * height;
            RaycastHit[] hits = Physics.RaycastAll(origin, worldDirection, distance, sightMask, QueryTriggerInteraction.Ignore);
            closestHit = default;
            float closestDistance = float.MaxValue;

            foreach (RaycastHit hit in hits)
            {
                Actor hitActor = hit.collider.GetComponentInParent<Actor>();
                if (hitActor != null)
                    continue;

                if (hit.distance < closestDistance)
                {
                    closestDistance = hit.distance;
                    closestHit = hit;
                }
            }

            return closestDistance < float.MaxValue;
        }

        private float GetClearance(Vector3 worldDirection)
        {
            if (HasObstacle(worldDirection, lowObstacleHeight, wallAvoidanceDistance, out RaycastHit hit))
                return hit.distance;

            return wallAvoidanceDistance;
        }

        private void UpdateStuckRecovery()
        {
            if (Time.time < m_StuckRecoveryUntil)
            {
                m_MoveInput = new Vector3(stuckStrafeInput * m_StuckRecoverySide, 0f, stuckBackInput);
                m_SprintHeld = false;
                return;
            }

            if (Time.time < m_NextStuckCheckTime)
                return;

            m_NextStuckCheckTime = Time.time + stuckCheckInterval;

            Vector3 horizontalDelta = transform.position - m_LastStuckCheckPosition;
            horizontalDelta.y = 0f;

            bool wantsToMove = m_MoveInput.sqrMagnitude > 0.15f;
            bool barelyMoved = horizontalDelta.magnitude < stuckMinMoveDistance;
            m_LastStuckCheckPosition = transform.position;

            if (!wantsToMove || !barelyMoved || Time.time < m_NextStuckRecoveryAllowed)
                return;

            EnterStuckRecovery();
        }

        private void EnterStuckRecovery()
        {
            float leftClearance = GetClearance(-transform.right);
            float rightClearance = GetClearance(transform.right);
            m_StuckRecoverySide = rightClearance >= leftClearance ? 1f : -1f;

            m_StuckRecoveryUntil = Time.time + stuckRecoveryDuration;
            m_NextStuckRecoveryAllowed = m_StuckRecoveryUntil + stuckRecoveryCooldown;
            m_StateUntil = 0f;

            float turnAngle = Random.Range(90f, 150f) * m_StuckRecoverySide;
            m_WanderWorldDirection = Quaternion.Euler(0f, turnAngle, 0f) * transform.forward;
            m_WanderWorldDirection.y = 0f;
            m_WanderWorldDirection.Normalize();
        }

        private void SmoothMoveInput()
        {
            float sharpness = m_MoveInput.sqrMagnitude < m_OutputMoveInput.sqrMagnitude ? moveInputSharpness * 1.8f : moveInputSharpness;
            m_OutputMoveInput = Vector3.MoveTowards(m_OutputMoveInput, m_MoveInput, Time.deltaTime * sharpness);
        }

        private float RandomSign()
        {
            return Random.value < 0.5f ? -1f : 1f;
        }

        private float StablePositionNoise(Vector3 position)
        {
            return Mathf.Abs(Mathf.Sin(position.x * 12.9898f + position.z * 78.233f));
        }

        private float NormalizeAngle(float angle)
        {
            while (angle > 180f)
                angle -= 360f;

            while (angle < -180f)
                angle += 360f;

            return angle;
        }

        public override Vector3 GetMoveInput() => m_OutputMoveInput;
        public override Vector2 GetLookInputs() => m_LookInput;
        public override bool GetJumpInputDown() => m_JumpDown;
        public override bool GetJumpInputHeld() => Time.time < m_JumpHeldUntil;
        public override bool GetFireInputHeld() => m_FireHeld;
        public override bool GetFireInputDown() => m_FireDown;
        public override bool GetFireInputReleased() => m_FireReleased;
        public override bool GetAimInputHeld() => m_AimHeld;
        public override bool GetSprintInputHeld() => m_SprintHeld;
        public override bool GetCrouchInputDown() => m_CrouchDown;
        public override bool GetCrouchInputReleased() => m_CrouchReleased;
        public override bool GetReloadButtonDown() => m_ReloadDown;
        public override int GetSwitchWeaponInput() => 0;
        public override int GetSelectWeaponInput() => 0;
    }
}
