using UnityEngine;
using Unity.Netcode;
using Unity.FPS.Game;
using Unity.FPS.Core;

namespace Unity.FPS.Gameplay
{
    [RequireComponent(typeof(BoxCollider))]
    public class CaptureZone : MonoBehaviour
    {
        [Header("Capture Zone Settings")]
        [Tooltip("0 = Blue Base, 1 = Red Base")]
        public int BaseTeamIndex;

        private BoxCollider cacheCollider;

        private void Awake()
        {
            if (TryGetComponent<BoxCollider>(out var box))
            {
                box.isTrigger = true;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (GameScoreManager.Instance == null) return;

            // 2. Enforce that it must be networked spawned and active before running point math
            if (!GameScoreManager.Instance.IsSpawned)
            {
                Debug.LogWarning("[CaptureZone] Waiting for GameScoreManager to finish network spawning before accepting flag deliveries.");
                return;
            }

            if (!NetworkManager.Singleton.IsServer) return;

            // CRITICAL VERIFICATION: Block captures if a team has won and match countdown is active
            if (GameScoreManager.Instance != null && GameScoreManager.Instance.IsGameOver())
            {
                Debug.Log("[CTF MATCH] Guardrail: Capture attempt blocked because the match has already concluded.");
                return;
            }

            if (other.TryGetComponent<PlayerCharacterController>(out var player))
            {
                if (player.TryGetComponent<PlayerSettings>(out var settings) &&
                    player.TryGetComponent<FlagCarrierTracker>(out var tracker))
                {
                    if (settings.TeamIndex == BaseTeamIndex && tracker.IsCarryingFlag.Value)
                    {
                        int capturedEnemyTeamIndex = (BaseTeamIndex == 0) ? 1 : 0;

                        Debug.Log($"[CTF MATCH] Player from team {settings.TeamIndex} successfully delivered Team {capturedEnemyTeamIndex}'s flag to home base!");

                        // 1. Remove the flag from the player authoritatively
                        tracker.SetCarryingFlagState(false);

                        // 2. Instruct the Flag Spawner to reset the stolen enemy flag back home
                        if (FlagSpawner.Instance != null)
                        {
                            FlagSpawner.Instance.SpawnFlagForTeam(capturedEnemyTeamIndex);
                        }
                        else
                        {
                            Debug.LogError("[CTF Server] FlagSpawner.Instance could not be found to handle capture reset!");
                        }

                        // Add points to TeamIndex score 
                        if (GameScoreManager.Instance != null)
                        {
                            GameScoreManager.Instance.AddPointToTeam(BaseTeamIndex);
                        }
                        else
                        {
                            Debug.LogError("[CTF Server] GameScoreManager instance missing from scene framework!");
                        }
                    }
                }
            }
        }

        private void OnDrawGizmos()
        {
            if (cacheCollider == null)
            {
                cacheCollider = GetComponent<BoxCollider>();
            }

            if (cacheCollider == null) return;

            Color baseColor = (BaseTeamIndex == 0) ? Color.blue : Color.red;
            string teamLabel = (BaseTeamIndex == 0) ? "Blue Base" : "Red Base";

            Matrix4x4 rotationMatrix = Matrix4x4.TRS(transform.position, transform.rotation, transform.lossyScale);
            Gizmos.matrix = rotationMatrix;

            baseColor.a = 0.15f;
            Gizmos.color = baseColor;
            Gizmos.DrawCube(cacheCollider.center, cacheCollider.size);

            baseColor.a = 0.8f;
            Gizmos.color = baseColor;
            Gizmos.DrawWireCube(cacheCollider.center, cacheCollider.size);

            Gizmos.matrix = Matrix4x4.identity;

#if UNITY_EDITOR
            Vector3 worldLabelPos = transform.TransformPoint(cacheCollider.center + (Vector3.up * (cacheCollider.size.y * 0.5f + 0.5f)));
            UnityEditor.Handles.Label(worldLabelPos, $"[Capture Zone] {teamLabel}");
#endif
        }
    }
}