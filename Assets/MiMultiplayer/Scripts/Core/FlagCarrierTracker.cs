using System;
using System.Collections.Generic; // Added to use List<>
using Unity.Netcode;
using UnityEngine;

namespace Unity.FPS.Core
{
    public class FlagCarrierTracker : NetworkBehaviour
    {
        [Header("Outline Configuration")]
        [Tooltip("The parent visual mesh GameObject (e.g., RandomModularRobots) that holds all the modular part renderers.")]
        [SerializeField] private GameObject visualMeshTarget;

        [Header("Flag Drop Settings")]
        [Tooltip("Height offset applied above the floor level when dropping the flag to prevent mesh clipping.")]
        [SerializeField] private float flagFloorDropOffset = 0.5f;

        [Header("Flag Collected SOund")]
        [SerializeField] private AudioClip flagCollectedSFX;

        public readonly NetworkVariable<bool> IsCarryingFlag = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        // Converted from a single Outline reference to a list for multiple modular components
        private List<Outline> activeOutlines = new List<Outline>();
        private PlayerSettings playerSettings;

        // Global static hooks that other assemblies can listen to safely
        public static event Action<int, Vector3> OnFlagDroppedServer;
        public static event Action<NetworkObject> OnFlagGrabbedServer;
        public static event Action<NetworkObject, int> OnFlagDroppedNotificationServer;
        public static event Action<bool> OnLocalPlayerFlagStateChanged;

        private void Awake()
        {
            playerSettings = GetComponent<PlayerSettings>();
            if (visualMeshTarget == null)
            {
                Debug.LogError("FlagCarrierTracker: Player mesh not assigned.");
            }
        }

        public override void OnNetworkSpawn()
        {
            InitializeOutlineComponent();

            // Hook up the state machine event listener
            IsCarryingFlag.OnValueChanged += OnCarryingFlagStateChanged;

            // Handle initial state evaluation for late joiners
            EnableOutline(IsCarryingFlag.Value);
        }

        public override void OnNetworkDespawn()
        {
            IsCarryingFlag.OnValueChanged -= OnCarryingFlagStateChanged;
        }

        /// Server-authoritative method to update the carrier state.
        public void SetCarryingFlagState(bool carrying)
        {
            if (!IsServer) return;

            // Check if the state is transitioning from false to true to detect a fresh pickup
            if (carrying && !IsCarryingFlag.Value)
            {
                OnFlagGrabbedServer?.Invoke(NetworkObject);
            }

            IsCarryingFlag.Value = carrying;
        }

        private void OnCarryingFlagStateChanged(bool oldState, bool newState)
        {
            if (IsOwner)
            {
                OnLocalPlayerFlagStateChanged?.Invoke(newState);
            }

            EnableOutline(newState);
        }

        // FIXED: Scans every child mesh piece of the modular robot model to ensure the entire body gets outlined
        private void InitializeOutlineComponent()
        {
            if (visualMeshTarget == null) return;

            // Clear our tracking list so we don't duplicate references if called multiple times
            activeOutlines.Clear();

            // Find all SkinnedMeshRenderers attached to the robot limbs/body parts
            SkinnedMeshRenderer[] renderers = visualMeshTarget.GetComponentsInChildren<SkinnedMeshRenderer>();

            foreach (var renderer in renderers)
            {
                // Look for or dynamically add an Outline component to this specific robot part
                if (!renderer.gameObject.TryGetComponent<Outline>(out var outline))
                {
                    outline = renderer.gameObject.AddComponent<Outline>();
                    outline.enabled = false;
                }

                activeOutlines.Add(outline);
            }
        }

        // Loops through all tracked child outlines instead of just adjusting a single root reference
        private void EnableOutline(bool enable)
        {
            InitializeOutlineComponent();

            Color enemyColor = GetEnemyOutlineColor();

            foreach (var outline in activeOutlines)
            {
                if (outline == null) continue;

                if (enable)
                {
                    outline.OutlineMode = Outline.Mode.OutlineAll;
                    outline.OutlineColor = enemyColor;
                    outline.OutlineWidth = 8f;
                    outline.enabled = true;
                }
                else
                {
                    outline.enabled = false;
                }
            }
        }

        private Color GetEnemyOutlineColor()
        {
            if (playerSettings != null)
            {
                if (playerSettings.TeamIndex == 0) return Color.red;
                if (playerSettings.TeamIndex == 1) return Color.blue;
            }
            return Color.yellow;
        }

        /// Server-authoritative drop calculation. Drops the enemy flag on the ground directly below the player.
        public void DropCarriedFlag()
        {
            if (!IsServer || !IsCarryingFlag.Value) return;

            // 1. Determine which flag they are carrying (the ENEMY team's flag)
            int enemyTeamIndex = (playerSettings.TeamIndex == 0) ? 1 : 0;

            // 2. Broadcast the notification hook BEFORE we wipe out states or drop references
            OnFlagDroppedNotificationServer?.Invoke(NetworkObject, enemyTeamIndex);

            // 3. Clear carrier state immediately so their outline turns off
            SetCarryingFlagState(false);

            // 4. Raycast down to find ground level
            Vector3 dropPosition;
            Ray ray = new Ray(transform.position + Vector3.up * 0.5f, Vector3.down);

            if (Physics.Raycast(ray, out RaycastHit hit, 20f, LayerMask.GetMask("Default", "Ground"), QueryTriggerInteraction.Ignore))
            {
                // Aadjustable inspector configuration factor
                dropPosition = hit.point + Vector3.up * flagFloorDropOffset;
            }
            else
            {
                // Custom vector marker to signal FlagSpawner to return it home directly
                dropPosition = new Vector3(-9999f, -9999f, -9999f);
            }

            // 4. Raise the event
            OnFlagDroppedServer?.Invoke(enemyTeamIndex, dropPosition);

            Debug.Log($"[CTF Server] Flag drop event authoritatively synced for Team {enemyTeamIndex}");
        }

        /// <summary>
        /// Forces all child outline components to temporarily turn white for the local player's voting gun hover check.
        /// </summary>
        public void SetVotingHoverOutline(bool active)
        {
            // Re-initialize lists safely if late caching
            if (activeOutlines.Count == 0) InitializeOutlineComponent();

            foreach (var outline in activeOutlines)
            {
                if (outline == null) continue;

                if (active)
                {
                    outline.OutlineMode = Outline.Mode.OutlineAll;
                    outline.OutlineColor = Color.white;
                    outline.OutlineWidth = 8f;
                    outline.enabled = true;
                }
                else
                {
                    // Revert immediately back to standard flag-carrier display color rules
                    outline.enabled = IsCarryingFlag.Value;
                    outline.OutlineColor = GetEnemyOutlineColor();
                }
            }
        }
    }
}