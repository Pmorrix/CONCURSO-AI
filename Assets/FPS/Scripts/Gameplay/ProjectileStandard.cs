using System.Collections.Generic;
using Unity.FPS.Core;
using Unity.FPS.Game;
using Unity.Netcode;
using Unity.Services.Lobbies.Models; // Added to access UGS Player Data if needed
using UnityEngine;

namespace Unity.FPS.Gameplay
{
    public class ProjectileStandard : ProjectileBase
    {
        [Header("General")]
        [Tooltip("Radius of this projectile's collision detection")]
        public float Radius = 0.01f;

        [Tooltip("Transform representing the root of the projectile (used for accurate collision detection)")]
        public Transform Root;

        [Tooltip("Transform representing the tip of the projectile (used for accurate collision detection)")]
        public Transform Tip;

        [Tooltip("LifeTime of the projectile")]
        public float MaxLifeTime = 5f;

        [Tooltip("VFX prefab to spawn upon impact")]
        public GameObject ImpactVfx;

        [Tooltip("LifeTime of the VFX before being destroyed")]
        public float ImpactVfxLifetime = 5f;

        [Tooltip("Offset along the hit normal where the VFX will be spawned")]
        public float ImpactVfxSpawnOffset = 0.1f;

        [Tooltip("Clip to play on impact")]
        public AudioClip ImpactSfxClip;

        [Tooltip("Clip to play on hit")]
        public AudioClip DamageSfxClip;

        [Tooltip("Layers this projectile can collide with")]
        public LayerMask HittableLayers = -1;

        [Header("Movement")]
        [Tooltip("Speed of the projectile")]
        public float Speed = 20f;

        [Tooltip("Downward acceleration from gravity")]
        public float GravityDownAcceleration = 0f;

        [Tooltip("Distance over which the projectile will correct its course to fit the intended trajectory. At values under 0, there is no correction")]
        public float TrajectoryCorrectionDistance = -1;

        [Tooltip("Determines if the projectile inherits the velocity that the weapon's muzzle had when firing")]
        public bool InheritWeaponVelocity = false;

        [Header("Damage")]
        [Tooltip("Damage of the projectile")]
        public float Damage = 40f;

        [Tooltip("Area of damage. Keep empty if you don't want area damage")]
        public DamageArea AreaOfDamage;

        [Header("Debug")]
        [Tooltip("Color of the projectile radius debug view")]
        public Color RadiusColor = Color.cyan * 0.2f;

        ProjectileBase m_ProjectileBase;
        Vector3 m_LastRootPosition;
        Vector3 m_Velocity;
        bool m_HasTrajectoryOverride;
        float m_ShootTime;
        Vector3 m_TrajectoryCorrectionVector;
        Vector3 m_ConsumedTrajectoryCorrectionVector;
        List<Collider> m_IgnoredColliders;

        const QueryTriggerInteraction k_TriggerInteraction = QueryTriggerInteraction.Collide;

        void OnEnable()
        {

        }

        public override void OnNetworkSpawn()
        {
            m_ProjectileBase = GetComponent<ProjectileBase>();
            DebugUtility.HandleErrorIfNullGetComponent<ProjectileBase, ProjectileStandard>(m_ProjectileBase, this, gameObject);

            m_ProjectileBase.OnShoot += OnShoot;

            if (IsServer)
            {
                m_ShootTime = Time.time;
            }
        }

        new void OnShoot()
        {
            m_LastRootPosition = Root.position;
            m_Velocity = transform.forward * Speed;
            m_IgnoredColliders = new List<Collider>();

            // If InheritWeaponVelocity is checked, add the muzzle's existing speed 
            // to the bullet velocity permanently so it matches the player's momentum frame-over-frame.
            if (InheritWeaponVelocity)
            {
                m_Velocity += m_ProjectileBase.InheritedMuzzleVelocity;
            }

            // Ignore colliders of owner
            Collider[] ownerColliders = m_ProjectileBase.Owner.GetComponentsInChildren<Collider>();
            m_IgnoredColliders.AddRange(ownerColliders);

            // Handle case of player shooting
            PlayerWeaponsManager playerWeaponsManager = m_ProjectileBase.Owner.GetComponent<PlayerWeaponsManager>();
            if (playerWeaponsManager)
            {
                m_HasTrajectoryOverride = true;

                Vector3 cameraToMuzzle = (m_ProjectileBase.InitialPosition - playerWeaponsManager.WeaponCamera.transform.position);

                m_TrajectoryCorrectionVector = Vector3.ProjectOnPlane(-cameraToMuzzle, playerWeaponsManager.WeaponCamera.transform.forward);
                if (TrajectoryCorrectionDistance == 0)
                {
                    transform.position += m_TrajectoryCorrectionVector;
                    m_ConsumedTrajectoryCorrectionVector = m_TrajectoryCorrectionVector;
                }
                else if (TrajectoryCorrectionDistance < 0)
                {
                    m_HasTrajectoryOverride = false;
                }

                if (Physics.Raycast(playerWeaponsManager.WeaponCamera.transform.position, cameraToMuzzle.normalized,
                    out RaycastHit hit, cameraToMuzzle.magnitude, HittableLayers, k_TriggerInteraction))
                {
                    if (IsHitValid(hit))
                    {
                        OnHit(hit.point, hit.normal, hit.collider);
                    }
                }
            }
        }

        void Update()
        {
            // Move
            transform.position += m_Velocity * Time.deltaTime;

            if (IsServer)
            {
                if (Time.time - m_ShootTime >= MaxLifeTime)
                {
                    GetComponent<NetworkObject>().Despawn();
                    return;
                }
            }

            // Drift towards trajectory override
            if (m_HasTrajectoryOverride && m_ConsumedTrajectoryCorrectionVector.sqrMagnitude < m_TrajectoryCorrectionVector.sqrMagnitude)
            {
                Vector3 correctionLeft = m_TrajectoryCorrectionVector - m_ConsumedTrajectoryCorrectionVector;
                float distanceThisFrame = (Root.position - m_LastRootPosition).magnitude;
                Vector3 correctionThisFrame = (distanceThisFrame / TrajectoryCorrectionDistance) * m_TrajectoryCorrectionVector;
                correctionThisFrame = Vector3.ClampMagnitude(correctionThisFrame, correctionLeft.magnitude);
                m_ConsumedTrajectoryCorrectionVector += correctionThisFrame;

                if (m_ConsumedTrajectoryCorrectionVector.sqrMagnitude == m_TrajectoryCorrectionVector.sqrMagnitude)
                {
                    m_HasTrajectoryOverride = false;
                }

                transform.position += correctionThisFrame;
            }

            transform.forward = m_Velocity.normalized;

            if (GravityDownAcceleration > 0)
            {
                m_Velocity += Vector3.down * GravityDownAcceleration * Time.deltaTime;
            }

            if (!IsServer) return;

            // Hit detection
            {
                RaycastHit closestHit = new RaycastHit();
                closestHit.distance = Mathf.Infinity;
                bool foundHit = false;

                Vector3 displacementSinceLastFrame = Tip.position - m_LastRootPosition;
                RaycastHit[] hits = Physics.SphereCastAll(m_LastRootPosition, Radius,
                    displacementSinceLastFrame.normalized, displacementSinceLastFrame.magnitude, HittableLayers, k_TriggerInteraction);

                foreach (var hit in hits)
                {
                    if (IsHitValid(hit) && hit.distance < closestHit.distance)
                    {
                        foundHit = true;
                        closestHit = hit;
                    }
                }

                if (foundHit)
                {
                    if (closestHit.distance <= 0f)
                    {
                        closestHit.point = Root.position;
                        closestHit.normal = -transform.forward;
                    }

                    OnHit(closestHit.point, closestHit.normal, closestHit.collider);
                }
            }

            m_LastRootPosition = Root.position;
        }

        bool IsHitValid(RaycastHit hit)
        {
            if (hit.collider.GetComponent<IgnoreHitDetection>())
            {
                return false;
            }

            if (hit.collider.isTrigger && hit.collider.GetComponent<Damageable>() == null)
            {
                return false;
            }

            if (m_IgnoredColliders != null && m_IgnoredColliders.Contains(hit.collider))
            {
                return false;
            }

            // FRIENDLY FIRE CHECK: Prevent standard bullet impact on a teammate
            if (m_ProjectileBase != null && m_ProjectileBase.Owner != null)
            {
                if (AreOnSameTeam(m_ProjectileBase.Owner, hit.collider.gameObject))
                {
                    return false; // The bullet passes through them entirely
                }
            }

            return true;
        }

        void OnHit(Vector3 point, Vector3 normal, Collider collider)
        {
            if (IsServer)
            {
                bool hitValidTarget = false;

                if (AreaOfDamage)
                {
                    // Area damage returns true if any valid damageable entities took damage
                    if (AreaOfDamage.InflictDamageInArea(Damage, point, HittableLayers, k_TriggerInteraction, m_ProjectileBase.Owner))
                    {
                        // Double check that we didn't just blow ourselves up or hit an ally before playing hit sound
                        if (!AreOnSameTeam(m_ProjectileBase.Owner, collider.gameObject))
                        {
                            hitValidTarget = true;
                        }
                    }
                }
                else
                {
                    // Point damage
                    Damageable damageable = collider.GetComponent<Damageable>();
                    if (damageable)
                    {
                        damageable.InflictDamage(Damage, false, m_ProjectileBase.Owner);
                        hitValidTarget = true;
                    }
                }

                // If valid target hit, play hitmarker
                if (hitValidTarget && m_ProjectileBase.Owner != null)
                {
                    if (m_ProjectileBase.Owner.tag == "Player")
                    {
                        if (m_ProjectileBase.Owner.TryGetComponent<NetworkObject>(out var attackerNetObj))
                        {
                            RpcParams rpcParams = RpcTarget.Single(attackerNetObj.OwnerClientId, RpcTargetUse.Temp);
                            PlayHitMarkerClientRpc(rpcParams);
                        }
                    }
                }
            }

            SpawnImpactVisualsRpc(point, normal);

            if (IsServer)
            {
                GetComponent<NetworkObject>().Despawn();
            }
        }

        [Rpc(SendTo.SpecifiedInParams)]
        void PlayHitMarkerClientRpc(RpcParams rpcParams)
        {
            if (DamageSfxClip != null)
            {
                AudioUtility.CreateSFX(
                    clip: DamageSfxClip,
                    position: Vector3.zero,
                    audioGroup: AudioUtility.AudioGroups.DamageTick,
                    spatialBlend: 0f
                );
            }

            Debug.Log("Hit Sound!");
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = RadiusColor;
            Gizmos.DrawSphere(transform.position, Radius);
        }

        [Rpc(SendTo.Everyone)]
        void SpawnImpactVisualsRpc(Vector3 point, Vector3 normal)
        {
            if (ImpactVfx)
            {
                GameObject impactVfxInstance = Instantiate(ImpactVfx, point + (normal * ImpactVfxSpawnOffset), Quaternion.LookRotation(normal));
                if (ImpactVfxLifetime > 0)
                {
                    Destroy(impactVfxInstance.gameObject, ImpactVfxLifetime);
                }
            }

            if (ImpactSfxClip)
            {
                AudioUtility.CreateSFX(ImpactSfxClip, point, AudioUtility.AudioGroups.Impact, 1f, 3f);
            }
        }

        /// <summary>
        /// Compares Lobby UGS teams to verify if the attacker and victim are allies.
        /// </summary>
        /// <summary>
        /// Compares Lobby UGS teams to verify if the attacker and victim are allies.
        /// </summary>
        private bool AreOnSameTeam(GameObject attacker, GameObject victim)
        {
            if (attacker == null || victim == null) return false;

            // AI ENEMY CHECK: If the entity firing this projectile is an AI bot (not a player), 
            // bypass friendly fire logic completely so enemies can hurt players.
            if (!attacker.CompareTag("Player"))
            {
                return false;
            }

            // Conversely, if a player shoots an environment object or AI enemy, it's a valid hit
            GameObject victimRoot = victim.transform.root.gameObject;
            if (!victimRoot.CompareTag("Player"))
            {
                return false;
            }

            if (LobbyManager.Instance == null) return false;

            Lobby activeLobby = LobbyManager.Instance.GetJoinedLobby();
            if (activeLobby == null) return false;

            // Find Attacker's Netcode ID
            if (!attacker.TryGetComponent<NetworkObject>(out var attackerNetObj)) return false;
            ulong attackerId = attackerNetObj.OwnerClientId;

            // Find Victim's Netcode ID (traverse parent elements if hit on a body-part subcollider)
            NetworkObject victimNetObj = victim.GetComponentInParent<NetworkObject>();
            if (victimNetObj == null) return false;
            ulong victimId = victimNetObj.OwnerClientId;

            // Self-harm is always allowed/handled separately (e.g., rocket jumps)
            if (attackerId == victimId) return false;

            // Translate Netcode IDs to UGS Player IDs
            string attackerUgsId = (attackerId == NetworkManager.ServerClientId)
                ? Unity.Services.Authentication.AuthenticationService.Instance.PlayerId
                : LobbyManager.Instance.GetUgsIdFromClientId(attackerId);

            string victimUgsId = (victimId == NetworkManager.ServerClientId)
                ? Unity.Services.Authentication.AuthenticationService.Instance.PlayerId
                : LobbyManager.Instance.GetUgsIdFromClientId(victimId);

            if (string.IsNullOrEmpty(attackerUgsId) || string.IsNullOrEmpty(victimUgsId)) return false;

            // Extract team strings from Lobby records
            string attackerTeam = "";
            string victimTeam = "";

            foreach (var player in activeLobby.Players)
            {
                if (player.Id == attackerUgsId && player.Data.ContainsKey(LobbyManager.KEY_PLAYER_TEAM))
                {
                    attackerTeam = player.Data[LobbyManager.KEY_PLAYER_TEAM].Value;
                }
                if (player.Id == victimUgsId && player.Data.ContainsKey(LobbyManager.KEY_PLAYER_TEAM))
                {
                    victimTeam = player.Data[LobbyManager.KEY_PLAYER_TEAM].Value;
                }
            }

            // If teams match exactly, it's friendly fire!
            if (!string.IsNullOrEmpty(attackerTeam) && attackerTeam == victimTeam)
            {
                Debug.Log($"[FRIENDLY FIRE] Blocked damage from Attacker NetID {attackerId} to Ally NetID {victimId} on Team '{attackerTeam}'");
                return true;
            }

            return false;
        }
    }
}