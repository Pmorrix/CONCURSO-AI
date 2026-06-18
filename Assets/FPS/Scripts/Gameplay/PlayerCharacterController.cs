using System;
using Unity.FPS.Game;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;

namespace Unity.FPS.Gameplay
{
    [RequireComponent(typeof(CharacterController), typeof(PlayerInputHandler), typeof(AudioSource))]
    public class PlayerCharacterController : NetworkBehaviour
    {
        [Header("Bot Settings")]
        [Tooltip("If true, this character is controlled by AI instead of player input")]
        public bool IsBot = false;
        private BotBehaviour m_BotBehaviour;

        [Header("References")]
        [Tooltip("Reference to the main camera used for the player")]
        public Camera PlayerCamera;

        [Tooltip("Audio source for footsteps, jump, etc...")]
        public AudioSource AudioSource;

        [Header("General")]
        [Tooltip("Force applied downward when in the air")]
        public float GravityDownForce = 20f;

        [Tooltip("Physic layers checked to consider the player grounded")]
        public LayerMask GroundCheckLayers = -1;

        [Tooltip("distance from the bottom of the character controller capsule to test for grounded")]
        public float GroundCheckDistance = 0.05f;

        [Header("Movement")]
        [Tooltip("Max movement speed when grounded (when not sprinting)")]
        public float MaxSpeedOnGround = 10f;

        [Tooltip(
            "Sharpness for the movement when grounded, a low value will make the player accelerate and decelerate slowly, a high value will do the opposite")]
        public float MovementSharpnessOnGround = 15;

        [Tooltip("Max movement speed when crouching")]
        [Range(0, 1)]
        public float MaxSpeedCrouchedRatio = 0.5f;

        [Tooltip("Max movement speed when not grounded")]
        public float MaxSpeedInAir = 10f;

        [Tooltip("Acceleration speed when in the air")]
        public float AccelerationSpeedInAir = 25f;

        [Tooltip("Multiplicator for the sprint speed (based on grounded speed)")]
        public float SprintSpeedModifier = 2f;

        [Tooltip("Height at which the player dies instantly when falling off the map")]
        public float KillHeight = -50f;

        [Header("Rotation")]
        [Tooltip("Rotation speed for moving the camera")]
        public float RotationSpeed = 200f;

        [Range(0.1f, 1f)]
        [Tooltip("Rotation speed multiplier when aiming")]
        public float AimingRotationMultiplier = 0.4f;

        [Header("Jump")]
        [Tooltip("Force applied upward when jumping")]
        public float JumpForce = 9f;

        [Header("Stance")]
        [Tooltip("Ratio (0-1) of the character height where the camera will be at")]
        public float CameraHeightRatio = 0.9f;

        [Tooltip("Height of character when standing")]
        public float CapsuleHeightStanding = 1.8f;

        [Tooltip("Height of character when crouching")]
        public float CapsuleHeightCrouching = 0.9f;

        [Tooltip("Speed of crouching transitions")]
        public float CrouchingSharpness = 10f;

        [Header("Audio")]
        [Tooltip("Amount of footstep sounds played when moving one meter")]
        public float FootstepSfxFrequency = 1f;

        [Tooltip("Amount of footstep sounds played when moving one meter while sprinting")]
        public float FootstepSfxFrequencyWhileSprinting = 1f;

        [Tooltip("Sound played for footsteps")]
        public AudioClip FootstepSfx;

        [Tooltip("Sound played when jumping")] public AudioClip JumpSfx;
        [Tooltip("Sound played when landing")] public AudioClip LandSfx;

        [Tooltip("Sound played when taking damage froma fall")]
        public AudioClip FallDamageSfx;

        [Header("Fall Damage")]
        [Tooltip("Whether the player will recieve damage when hitting the ground at high speed")]
        public bool RecievesFallDamage;

        [Tooltip("Minimun fall speed for recieving fall damage")]
        public float MinSpeedForFallDamage = 10f;

        [Tooltip("Fall speed for recieving th emaximum amount of fall damage")]
        public float MaxSpeedForFallDamage = 30f;
        
        [Tooltip("Damage recieved when falling at the mimimum speed")]
        public float FallDamageAtMinSpeed = 10f;

        [Tooltip("Damage recieved when falling at the maximum speed")]
        public float FallDamageAtMaxSpeed = 50f;

        public UnityAction<bool> OnStanceChanged;
        public static Action<GameObject> OnPlayerDie;

        public Vector3 CharacterVelocity { get; set; }
        public bool IsGrounded { get; private set; }
        public bool HasJumpedThisFrame { get; private set; }

        [Header("Respawn System")]
        [Tooltip("Respawn Camera")]
        private GameObject RespawnCamera;
        [Tooltip("Time for players to respawn (seconds)")]
        public float RespawnDelay = 5f;
        private Coroutine m_RespawnCoroutine;
        private PlayerSpawner spawnManager;
        private Vector3 m_InitialSpawnPosition;
        private Quaternion m_InitialSpawnRotation;


        public NetworkVariable<bool> IsDead = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        [Header("VFX")]
        [Tooltip("The explosion prefab to spawn on death")]
        public GameObject DeathVfx;

        [Header("PlayerMesh")]
        [SerializeField] private GameObject playerMesh;

        [Header("Animation Bindings")]
        [Tooltip("The animator component sitting on the robot visual prefab model.")]
        [SerializeField] private Animator playerAnimator;

        [Tooltip("Smoothness damping adjustment factor for moving animation transitions.")]
        [SerializeField] private float animationDampTime = 0.1f;

        [Header("Ladder Climbing")]
        [Tooltip("Speed when climbing up or down a ladder")]
        public float LadderClimbSpeed = 5f;
        private bool m_IsOnLadder = false;
        public bool IsCrouching { get; private set; }

        public float RotationMultiplier
        {
            get
            {
                if (m_WeaponsManager.IsAiming)
                {
                    return AimingRotationMultiplier;
                }

                return 1f;
            }
        }

        Health m_Health;
        PlayerInputHandler m_InputHandler;
        CharacterController m_Controller;
        PlayerWeaponsManager m_WeaponsManager;
        ActorsManager actorsManager;
        Actor m_Actor;
        Vector3 m_GroundNormal;
        Vector3 m_CharacterVelocity;
        Vector3 m_LatestImpactSpeed;
        float m_LastTimeJumped = 0f;
        float m_CameraVerticalAngle = 0f;
        float m_FootstepDistanceCounter;
        float m_TargetCharacterHeight;

        const float k_JumpGroundingPreventionTime = 0.2f;
        const float k_GroundCheckDistanceInAir = 0.07f;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            m_InitialSpawnPosition = transform.position;
            m_InitialSpawnRotation = transform.rotation;

            // Cache standard local components
            m_Controller = GetComponent<CharacterController>();
            m_InputHandler = GetComponent<PlayerInputHandler>();
            m_WeaponsManager = GetComponent<PlayerWeaponsManager>();
            m_Health = GetComponent<Health>();
            m_Actor = GetComponent<Actor>();

            // Initial search attempt
            spawnManager = FindFirstObjectByType<PlayerSpawner>();

            if (IsServer)
            {
                TryRegisterToServerComponents();
            }

            IsDead.OnValueChanged += OnIsDeadChanged;
            UpdateMeshVisibility();

            // Hook up to the profile ready event to fix the late replication flash
            if (TryGetComponent<Unity.FPS.Core.PlayerSettings>(out var settings))
            {
                settings.OnProfileReady += HandleProfileReady;
            }

            if (IsOwner)
            {

                GameObject gameManager = GameObject.Find("GameManager");
                if (gameManager != null) 
                {
                    Debug.Log("I have found the gameManager!");

                    // Find the child transform by its exact name
                    Transform hudTransform = gameManager.transform.Find("GameHUD");

                    if (hudTransform != null)
                    {
                        // Turn on the child GameObject
                        hudTransform.gameObject.SetActive(true);
                        Debug.Log("GameHUD has been activated!");
                    }
                    else
                    {
                        Debug.LogError("Could not find a child named 'GameHUD' under GameManager!");
                    }
                }
                else 
                {
                    Debug.LogError("Could not find GameManager in the scene!");
                }

                // Configure local components safely
                if (PlayerCamera != null)
                {
                    PlayerCamera.enabled = true;
                    var localListener = PlayerCamera.GetComponent<AudioListener>();
                    if (localListener != null) localListener.enabled = true;
                }

                DebugUtility.HandleErrorIfNullGetComponent<CharacterController, PlayerCharacterController>(m_Controller, this, gameObject);
                DebugUtility.HandleErrorIfNullGetComponent<PlayerInputHandler, PlayerCharacterController>(m_InputHandler, this, gameObject);
                DebugUtility.HandleErrorIfNullGetComponent<PlayerWeaponsManager, PlayerCharacterController>(m_WeaponsManager, this, gameObject);
                DebugUtility.HandleErrorIfNullGetComponent<Health, PlayerCharacterController>(m_Health, this, gameObject);
                DebugUtility.HandleErrorIfNullGetComponent<Actor, PlayerCharacterController>(m_Actor, this, gameObject);

                m_Controller.enableOverlapRecovery = true;
                m_Health.OnDie += OnDie;

                SetCrouchingState(false, true);
                UpdateCharacterHeight(true);
                TryDisableRespawnCamera();

                if (IsBot)
                {
                    // Try to find a BotBehaviour component on the same GameObject
                    m_BotBehaviour = GetComponent<BotBehaviour>();

                    if (m_BotBehaviour == null)
                    {
                        Debug.LogWarning($"Bot {gameObject.name} has IsBot=true but no BotBehaviour component found!");
                    }
                    else
                    {
                        m_BotBehaviour.Initialize(this);
                        Debug.Log($"Bot {gameObject.name} initialized with behaviour: {m_BotBehaviour.GetType().Name}");
                    }
                }
            }
            else
            {
                if (PlayerCamera != null)
                {
                    PlayerCamera.enabled = false;
                    var proxyListener = PlayerCamera.GetComponent<AudioListener>();
                    if (proxyListener != null) proxyListener.enabled = false;
                }
                IsDead.OnValueChanged += OnIsDeadChanged;
            }
        }

        private void HandleProfileReady()
        {
            // Profile caught up. Re-evaluate if we should show the mesh now
            UpdateMeshVisibility();
        }

        /// Safely locates the assigned team spawnpoint from the Spawn Manager and moves the controller.
        private void TryTeleportToSpawnPoint()
        {
            if (!IsOwner) return;

            if (m_Controller != null)
                m_Controller.enabled = false;

            // Teleport back to exactly where the PlayerSpawner originally placed us
            transform.position = m_InitialSpawnPosition;
            transform.rotation = m_InitialSpawnRotation;

            if (m_Controller != null && !IsDead.Value)
                m_Controller.enabled = true;

            Debug.Log($"[Success] Owner Client {OwnerClientId} safely teleported to initial spawn: {transform.position}");
        }

        /// Attempts to locate the ActorsManager and pass the player instance over to it.
        private void TryRegisterToServerComponents()
        {
            if (!IsServer) return;

            if (actorsManager == null)
            {
                actorsManager = FindFirstObjectByType<ActorsManager>();
            }

            if (actorsManager != null)
            {
                actorsManager.AddPlayer(gameObject);
                Debug.Log($"[Server Success] Registered player {OwnerClientId} to ActorsManager.");
            }
            else
            {
                Debug.LogWarning($"[Server Retry] ActorsManager still not found for player {OwnerClientId}. Waiting on scene completion...");
            }
        }

        /// Updates the visibility of the third person player mesh components.
        public void UpdateMeshVisibility(bool? isCurrentlyDead = null)
        {
            if (playerMesh == null) return;

            // Fall back to the NetworkVariable state if no specific override is passed
            bool deadState = isCurrentlyDead ?? IsDead.Value;

            bool hasJoinedTeam = true;
            if (TryGetComponent<Unity.FPS.Core.PlayerSettings>(out var settings))
            {
                if (settings.TeamIndex == -1)
                {
                    hasJoinedTeam = false;
                }
            }


            // 1. Calculate if a regular player should see this mesh
            bool shouldBeVisible = !deadState && hasJoinedTeam;

            if (IsOwner)
            {
                // The owner's mesh GameObject must ALWAYS stay active so its shadow can render
                playerMesh.SetActive(shouldBeVisible);

                // Grab all Skinned or Static Mesh Renderers on the player model
                Renderer[] renderers = playerMesh.GetComponentsInChildren<Renderer>(true);
                foreach (Renderer ren in renderers)
                {
                    // Skip FP Weapon layers if they happen to be structured under playerMesh
                    if (ren.gameObject.layer == LayerMask.NameToLayer("FpsWeapon")) continue;

                    // If alive and on a team, turn into a shadow ghost. If dead, turn off shadows completely.
                    ren.shadowCastingMode = shouldBeVisible ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.Off;
                }
            }
            else
            {
                // 2. Standard behavior for remote players (hide/show completely)
                bool remoteVisibility = shouldBeVisible;

                if (playerMesh.activeSelf != remoteVisibility)
                {
                    playerMesh.SetActive(remoteVisibility);
                }

                // Reset shadow casting mode back to normal just in case this client cached it strangely
                if (remoteVisibility)
                {
                    Renderer[] renderers = playerMesh.GetComponentsInChildren<Renderer>(true);
                    foreach (Renderer ren in renderers)
                    {
                        ren.shadowCastingMode = ShadowCastingMode.On;
                    }
                }
            }
        }

        private void TryDisableRespawnCamera()
        {
            if (RespawnCamera == null)
            {
                RespawnCamera = GameObject.Find("RespawnView");
            }

            if (RespawnCamera != null)
            {
                RespawnCamera.SetActive(false);
                Debug.Log("Respawn Cam found and turned OFF successfully!");
            }
            else
            {
                Debug.LogWarning("Respawn Cam still not found in this frame.");
            }
        }

        void Update()
        {
            
            // Bot update logic - runs even if not owner
            if (IsBot && m_BotBehaviour != null)
            {
                m_BotBehaviour.OnBotUpdate();
            } 

            if (!IsOwner) return;

            // Exit early if dead so we don't call Move() on a disabled controller
            if (IsDead.Value) return;

            // check for Y kill
            if (transform.position.y < KillHeight)
            {
                OnDie();
                return;
            }

            HasJumpedThisFrame = false;

            bool wasGrounded = IsGrounded;
            GroundCheck();

            // landing
            if (IsGrounded && !wasGrounded)
            {
                // Fall damage
                float fallSpeed = -Mathf.Min(CharacterVelocity.y, m_LatestImpactSpeed.y);
                float fallSpeedRatio = (fallSpeed - MinSpeedForFallDamage) /
                                       (MaxSpeedForFallDamage - MinSpeedForFallDamage);
                if (RecievesFallDamage && fallSpeedRatio > 0f)
                {
                    float dmgFromFall = Mathf.Lerp(FallDamageAtMinSpeed, FallDamageAtMaxSpeed, fallSpeedRatio);
                    m_Health.TakeDamage(dmgFromFall, null);

                    // fall damage SFX
                    AudioSource.PlayOneShot(FallDamageSfx);
                }
                else
                {
                    // land SFX
                    AudioSource.PlayOneShot(LandSfx);
                }
            }

            // crouching
            if (m_InputHandler.GetCrouchInputDown())
            {
                SetCrouchingState(!IsCrouching, false);
            }

            UpdateCharacterHeight(false);

            HandleCharacterMovement();
        }

        void OnDie()
        {
            if (IsOwner)
            {
                // Server logic
                RequestDeathServerRpc();

                // Owner-side Visuals
                IsDeadOwnerLogic();
            }
        }

        [Rpc(SendTo.Server)]
        void RequestDeathServerRpc()
        {
            if (IsDead.Value) return;
            IsDead.Value = true;

            PlayerDeathBridge.RaisePlayerDied(gameObject);

            if (TryGetComponent<Unity.FPS.Core.FlagCarrierTracker>(out var tracker))
            {
                tracker.DropCarriedFlag();
            }

            Vector3 deathPosition = transform.position;
            SpawnDeathVfxClientRpc(deathPosition);

            // Stop AI from shooting
            if (actorsManager != null) actorsManager.RemovePlayer(gameObject);

        }

        [Rpc(SendTo.Everyone)]
        void SpawnDeathVfxClientRpc(Vector3 spawnPos)
        {
            // This runs on every player's screen
            if (DeathVfx != null)
            {
                var vfx = Instantiate(DeathVfx, spawnPos, Quaternion.identity);
                Destroy(vfx, 5f);
            }
        }

        void IsDeadOwnerLogic()
        {
            CharacterVelocity = Vector3.zero;
            m_Controller.enabled = false;

            // Switch Cameras
            PlayerCamera.enabled = false;
            if (RespawnCamera != null) RespawnCamera.SetActive(true);

            // Lose jetpack
            Jetpack jetpack = GetComponent<Jetpack>();
            if (jetpack != null)
            {
                jetpack.LoseJetpack();
            }

            if (m_WeaponsManager != null)
            {
                m_WeaponsManager.ResetToStartingWeapon();
            }

            TryTeleportToSpawnPoint();

            // Start Timer
            if (m_RespawnCoroutine != null) StopCoroutine(m_RespawnCoroutine);
            m_RespawnCoroutine = StartCoroutine(RespawnTimerCoroutine());
        }
        private System.Collections.IEnumerator RespawnTimerCoroutine()
        {
            float timer = RespawnDelay;
            while (timer > 0)
            {
                Debug.Log($"Respawning in: {Mathf.Ceil(timer)}");
                timer -= Time.deltaTime;
                yield return null;
            }
            RespawnServerRpc();
        }

        [Rpc(SendTo.Server)]
        void RespawnServerRpc()
        {
            if (TryGetComponent<Health>(out var health)) health.Revive();


            if (actorsManager != null) actorsManager.AddPlayer(gameObject);

            IsDead.Value = false;

            RespawnClientRpc();
        }

        [Rpc(SendTo.Everyone)]
        void RespawnClientRpc()
        {
            if (IsOwner)
            {

                // Reset the camera and UI
                if (m_RespawnCoroutine != null)
                {
                    StopCoroutine(m_RespawnCoroutine);
                    m_RespawnCoroutine = null;
                }

                if (RespawnCamera != null) RespawnCamera.SetActive(false);
                PlayerCamera.enabled = true;
                CharacterVelocity = Vector3.zero;
                UpdateCharacterHeight(true);
            }
        }

        private void OnIsDeadChanged(bool previousValue, bool newValue)
        {
            // 1. Handle controller collision and weapon switching for everyone
            SetPlayerState(!newValue);

            // 2. Handle mesh visibility and shadow casting states for EVERYONE (Owner and Proxies)
            UpdateMeshVisibility(newValue);
        }
        void SetPlayerState(bool active)
        {
            Debug.Log($"[Network State] Setting player collision/input state to: {active}");

            if (m_Controller != null)
            {
                m_Controller.enabled = active;
            }

            // Crucial: If they are dead, move them to an 'Ignore Raycast' layer 
            // so projectiles/hit detectors skip over their phantom body completely.
            gameObject.layer = active ? LayerMask.NameToLayer("Player") : LayerMask.NameToLayer("Ignore Raycast");

            if (m_WeaponsManager != null)
            {
                m_WeaponsManager.SwitchToWeaponIndex(active ? 0 : -1, true);
            }
        }

        void GroundCheck()
        {
            // Make sure that the ground check distance while already in air is very small, to prevent suddenly snapping to ground
            float chosenGroundCheckDistance =
                IsGrounded ? (m_Controller.skinWidth + GroundCheckDistance) : k_GroundCheckDistanceInAir;

            // reset values before the ground check
            IsGrounded = false;
            m_GroundNormal = Vector3.up;

            // only try to detect ground if it's been a short amount of time since last jump; otherwise we may snap to the ground instantly after we try jumping
            if (Time.time >= m_LastTimeJumped + k_JumpGroundingPreventionTime)
            {
                // if we're grounded, collect info about the ground normal with a downward capsule cast representing our character capsule
                if (Physics.CapsuleCast(GetCapsuleBottomHemisphere(), GetCapsuleTopHemisphere(m_Controller.height),
                    m_Controller.radius, Vector3.down, out RaycastHit hit, chosenGroundCheckDistance, GroundCheckLayers,
                    QueryTriggerInteraction.Ignore))
                {
                    // storing the upward direction for the surface found
                    m_GroundNormal = hit.normal;

                    // Only consider this a valid ground hit if the ground normal goes in the same direction as the character up
                    // and if the slope angle is lower than the character controller's limit
                    if (Vector3.Dot(hit.normal, transform.up) > 0f &&
                        IsNormalUnderSlopeLimit(m_GroundNormal))
                    {
                        IsGrounded = true;

                        // handle snapping to the ground
                        if (hit.distance > m_Controller.skinWidth)
                        {
                            if (hit.distance > m_Controller.skinWidth && m_Controller.enabled && !IsDead.Value)
                            {
                                m_Controller.Move(Vector3.down * hit.distance);
                            }
                        }
                    }
                }
            }
        }

        void HandleCharacterMovement()
        {
            // horizontal character rotation
            {
                // rotate the transform with the input speed around its local Y axis
                transform.Rotate(
                    new Vector3(0f, (GetLookInputsHorizontal() * RotationSpeed * RotationMultiplier),
                        0f), Space.Self);
            }

            // vertical camera rotation
            {
                // add vertical inputs to the camera's vertical angle
                m_CameraVerticalAngle += GetLookInputsVertical() * RotationSpeed * RotationMultiplier;

                // limit the camera's vertical angle to min/max
                m_CameraVerticalAngle = Mathf.Clamp(m_CameraVerticalAngle, -89f, 89f);

                // apply the vertical angle as a local rotation to the camera transform along its right axis (makes it pivot up and down)
                PlayerCamera.transform.localEulerAngles = new Vector3(m_CameraVerticalAngle, 0, 0);
            }

            // character movement handling
            bool isSprinting = GetSprintInputHeld();
            {
                if (isSprinting)
                {
                    isSprinting = SetCrouchingState(false, false);
                }

                float speedModifier = isSprinting ? SprintSpeedModifier : 1f;
                Vector3 rawInput = GetMoveInput(); // Raw Vector3(input.x, 0, input.y)
                Vector3 worldspaceMoveInput = transform.TransformVector(rawInput);

                // Ladder climbing
                if (m_IsOnLadder)
                {
                    // rawInput.z represents the vertical axis of the input vector (holding W or Forward)
                    float verticalClimbInput = rawInput.z;

                    // Calculate climb velocity strictly straight up/down based on forward/backward input
                    Vector3 targetLadderVelocity = Vector3.up * verticalClimbInput * LadderClimbSpeed;

                    // Allow slight horizontal strafe tracking on the ladder for adjustment comfort
                    Vector3 horizontalStrafe = transform.right * rawInput.x * (MaxSpeedOnGround * 0.5f);

                    // Combine them into the final velocity overriding normal physics tracking
                    CharacterVelocity = Vector3.Lerp(CharacterVelocity, targetLadderVelocity + horizontalStrafe, MovementSharpnessOnGround * Time.deltaTime);

                    // Force ground flags to false while climbing up
                    IsGrounded = false;
                }

                else if (IsGrounded)
                {
                    // calculate the desired velocity from inputs, max speed, and current slope
                    Vector3 targetVelocity = worldspaceMoveInput * MaxSpeedOnGround * speedModifier;
                    // reduce speed if crouching by crouch speed ratio
                    if (IsCrouching)
                        targetVelocity *= MaxSpeedCrouchedRatio;
                    targetVelocity = GetDirectionReorientedOnSlope(targetVelocity.normalized, m_GroundNormal) *
                                     targetVelocity.magnitude;

                    // smoothly interpolate between our current velocity and the target velocity based on acceleration speed
                    CharacterVelocity = Vector3.Lerp(CharacterVelocity, targetVelocity,
                        MovementSharpnessOnGround * Time.deltaTime);

                    // jumping
                    if (IsGrounded && GetJumpInputDown())
                    {
                        Debug.Log($"[JumpDebug] Conditions met! IsGrounded: {IsGrounded}, InputDown: true. Attempting to clear crouch...");

                        bool crouchCleared = SetCrouchingState(false, false);
                        Debug.Log($"[JumpDebug] SetCrouchingState returned: {crouchCleared}");

                        if (crouchCleared)
                        {
                            CharacterVelocity = new Vector3(CharacterVelocity.x, 0f, CharacterVelocity.z);
                            CharacterVelocity += Vector3.up * JumpForce;

                            Debug.Log($"[JumpDebug] JUMP FORCE APPLIED! Velocity.y is now: {CharacterVelocity.y}");

                            AudioSource.PlayOneShot(JumpSfx);
                            m_LastTimeJumped = Time.time;
                            HasJumpedThisFrame = true;
                            IsGrounded = false;
                            m_GroundNormal = Vector3.up;
                        }
                    }

                    // footsteps sound handling...
                    float chosenFootstepSfxFrequency = (isSprinting ? FootstepSfxFrequencyWhileSprinting : FootstepSfxFrequency);
                    if (m_FootstepDistanceCounter >= 1f / chosenFootstepSfxFrequency)
                    {
                        m_FootstepDistanceCounter = 0f;
                        AudioSource.PlayOneShot(FootstepSfx);
                    }
                    m_FootstepDistanceCounter += CharacterVelocity.magnitude * Time.deltaTime;
                }

                else
                {
                    // add air acceleration
                    CharacterVelocity += worldspaceMoveInput * AccelerationSpeedInAir * Time.deltaTime;

                    // limit air speed to a maximum, but only horizontally
                    float verticalVelocity = CharacterVelocity.y;
                    Vector3 horizontalVelocity = Vector3.ProjectOnPlane(CharacterVelocity, Vector3.up);
                    horizontalVelocity = Vector3.ClampMagnitude(horizontalVelocity, MaxSpeedInAir * speedModifier);
                    CharacterVelocity = horizontalVelocity + (Vector3.up * verticalVelocity);

                    // apply the gravity to the velocity
                    CharacterVelocity += Vector3.down * GravityDownForce * Time.deltaTime;
                }
            }

            // apply the final calculated velocity value as a character movement
            Vector3 capsuleBottomBeforeMove = GetCapsuleBottomHemisphere();
            Vector3 capsuleTopBeforeMove = GetCapsuleTopHemisphere(m_Controller.height);

            if (IsDead.Value) return;
            else if (m_Controller.enabled) m_Controller.Move(CharacterVelocity * Time.deltaTime);

            // detect obstructions to adjust velocity accordingly
            m_LatestImpactSpeed = Vector3.zero;
            if (Physics.CapsuleCast(capsuleBottomBeforeMove, capsuleTopBeforeMove, m_Controller.radius,
                CharacterVelocity.normalized, out RaycastHit hit, CharacterVelocity.magnitude * Time.deltaTime, -1,
                QueryTriggerInteraction.Ignore))
            {
                // We remember the last impact speed because the fall damage logic might need it
                m_LatestImpactSpeed = CharacterVelocity;

                CharacterVelocity = Vector3.ProjectOnPlane(CharacterVelocity, hit.normal);
            }

            // --- ANIMATION UPDATE LOGIC ---
            if (playerAnimator != null)
            {
                // Only calculate parameters if we own this player
                if (IsOwner)
                {
                    // 1. Isolate the horizontal plane vector to ignore vertical gravity or jump velocities
                    Vector3 horizontalVelocity = new Vector3(CharacterVelocity.x, 0f, CharacterVelocity.z);

                    // 2. Get the raw velocity magnitude
                    float currentHorizontalSpeed = horizontalVelocity.magnitude;

                    // 3. Normalize the speed against maximum base movement values
                    float targetNormalizedSpeed = 0f;

                    if (currentHorizontalSpeed > 0.1f && IsGrounded)
                    {
                        // NEW: Check if the movement vector is pointing forward or backward relative to our transform
                        // If moving backward, dotProduct will be negative
                        float dotProduct = Vector3.Dot(horizontalVelocity.normalized, transform.forward);
                        float directionSign = (dotProduct >= 0f) ? 1f : -1f;

                        // Multiply the normal 0.5f or 1.0f speed by our direction sign (- or +)
                        targetNormalizedSpeed = (isSprinting ? 1.0f : 0.5f) * directionSign;
                    }
                    else
                    {
                        targetNormalizedSpeed = 0f;
                    }

                    // 4. Pass movement values to the Animator (MoveSpeed can now be negative!)
                    playerAnimator.SetFloat("MoveSpeed", targetNormalizedSpeed, animationDampTime, Time.deltaTime);

                    // 5. Update the falling/grounded state
                    playerAnimator.SetBool("IsGrounded", IsGrounded);

                    // 6. Check if we initiated a jump this frame and fire the instantaneous trigger
                    if (HasJumpedThisFrame)
                    {
                        playerAnimator.SetTrigger("Jump");

                        // Reset the frame flag so we don't trigger it repeatedly
                        HasJumpedThisFrame = false;
                    }

                    playerAnimator.SetBool("IsGrounded", IsGrounded || m_IsOnLadder);
                }
            }
        } 

        // Returns true if the slope angle represented by the given normal is under the slope angle limit of the character controller
        bool IsNormalUnderSlopeLimit(Vector3 normal)
        {
            return Vector3.Angle(transform.up, normal) <= m_Controller.slopeLimit;
        }

        // Gets the center point of the bottom hemisphere of the character controller capsule    
        Vector3 GetCapsuleBottomHemisphere()
        {
            return transform.position + (transform.up * m_Controller.radius);
        }

        // Gets the center point of the top hemisphere of the character controller capsule    
        Vector3 GetCapsuleTopHemisphere(float atHeight)
        {
            return transform.position + (transform.up * (atHeight - m_Controller.radius));
        }

        // Gets a reoriented direction that is tangent to a given slope
        public Vector3 GetDirectionReorientedOnSlope(Vector3 direction, Vector3 slopeNormal)
        {
            Vector3 directionRight = Vector3.Cross(direction, transform.up);
            return Vector3.Cross(slopeNormal, directionRight).normalized;
        }

        void UpdateCharacterHeight(bool force)
        {
            // Update height instantly
            if (force)
            {
                m_Controller.height = m_TargetCharacterHeight;
                m_Controller.center = Vector3.up * m_Controller.height * 0.5f;
                PlayerCamera.transform.localPosition = Vector3.up * m_TargetCharacterHeight * CameraHeightRatio;
                m_Actor.AimPoint.transform.localPosition = m_Controller.center;
            }
            // Update smooth height
            else if (m_Controller.height != m_TargetCharacterHeight)
            {
                // resize the capsule and adjust camera position
                m_Controller.height = Mathf.Lerp(m_Controller.height, m_TargetCharacterHeight,
                    CrouchingSharpness * Time.deltaTime);
                m_Controller.center = Vector3.up * m_Controller.height * 0.5f;
                PlayerCamera.transform.localPosition = Vector3.Lerp(PlayerCamera.transform.localPosition,
                    Vector3.up * m_TargetCharacterHeight * CameraHeightRatio, CrouchingSharpness * Time.deltaTime);
                m_Actor.AimPoint.transform.localPosition = m_Controller.center;
            }
        }

        // returns false if there was an obstruction
        bool SetCrouchingState(bool crouched, bool ignoreObstructions)
        {
            // set appropriate heights
            if (crouched)
            {
                m_TargetCharacterHeight = CapsuleHeightCrouching;
            }
            else
            {
                // Detect obstructions
                if (!ignoreObstructions)
                {
                    Collider[] standingOverlaps = Physics.OverlapCapsule(
                        GetCapsuleBottomHemisphere(),
                        GetCapsuleTopHemisphere(CapsuleHeightStanding),
                        m_Controller.radius,
                        -1,
                        QueryTriggerInteraction.Ignore);
                    foreach (Collider c in standingOverlaps)
                    {
                        if (c != m_Controller)
                        {
                            return false;
                        }
                    }
                }

                m_TargetCharacterHeight = CapsuleHeightStanding;
            }

            if (OnStanceChanged != null)
            {
                OnStanceChanged.Invoke(crouched);
            }

            IsCrouching = crouched;
            return true;
        }
        public void SetOnLadderState(bool isOnLadder)
        {
            m_IsOnLadder = isOnLadder;

            // If we just stepped off a ladder, clear our vertical velocity so we don't drop like a stone instantly
            if (!isOnLadder)
            {
                CharacterVelocity = new Vector3(CharacterVelocity.x, 0f, CharacterVelocity.z);
            }
        }

        public Vector3 GetMoveInput()
        {
            // If this is a bot with behaviour, get input from the bot
            if (IsBot && m_BotBehaviour != null)
            {
                return m_BotBehaviour.GetMoveInput();
            }

            // Otherwise use the existing player input logic
            if (m_InputHandler != null)
            {
                return m_InputHandler.GetMoveInput();
            }
            return Vector3.zero;
        }

        public float GetLookInputsHorizontal()
        {
            if (IsBot && m_BotBehaviour != null)
            {
                Vector2 lookInputs = m_BotBehaviour.GetLookInputs();
                return lookInputs.x;
            }

            if (m_InputHandler == null)
                return 0.0f;

            return m_InputHandler.GetLookInputsHorizontal();
        }

        public float GetLookInputsVertical()
        {
            if (IsBot && m_BotBehaviour != null)
            {
                Vector2 lookInputs = m_BotBehaviour.GetLookInputs();
                return lookInputs.y;
            }

            if (m_InputHandler == null)
                return 0.0f;

            return m_InputHandler.GetLookInputsVertical();
        }

        public bool GetJumpInputDown()
        {

            if (IsBot && m_BotBehaviour != null)
            {
                return m_BotBehaviour.GetJumpInputDown();
            }

            return m_InputHandler != null && m_InputHandler.GetJumpInputDown();
        }

        public bool GetJumpInputHeld()
        {
            if (IsBot && m_BotBehaviour != null)
                return m_BotBehaviour.GetJumpInputHeld();

            return m_InputHandler != null && m_InputHandler.GetJumpInputHeld();
        }

        

        public bool GetSprintInputHeld()
        {
            if (IsBot && m_BotBehaviour != null)
                return m_BotBehaviour.GetSprintInputHeld();

            return m_InputHandler != null && m_InputHandler.GetSprintInputHeld();
        }

        public bool GetCrouchInputDown()
        {
            if (IsBot && m_BotBehaviour != null)
                return m_BotBehaviour.GetCrouchInputDown();

            return m_InputHandler != null && m_InputHandler.GetCrouchInputDown();
        }

        public bool GetCrouchInputReleased()
        {
            if (IsBot && m_BotBehaviour != null)
                return m_BotBehaviour.GetCrouchInputReleased();

            return m_InputHandler != null && m_InputHandler.GetCrouchInputReleased();
        }

    }
}