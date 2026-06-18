using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Unity.FPS.Gameplay
{
    public class FlagSpawner : NetworkBehaviour
    {
        public static FlagSpawner Instance { get; private set; }

        [System.Serializable]
        public struct TeamFlagData
        {
            public string TeamName;
            public int TeamIndex;   // 0 = Blue, 1 = Red
            public GameObject FlagPrefab;
            public Transform FlagSpawnPoint;
            public Color TeamColor;
        }

        [Header("CTF Team Configurations")]
        public List<TeamFlagData> Teams = new List<TeamFlagData>();

        private Dictionary<int, GameObject> liveFlagInstances = new Dictionary<int, GameObject>();

        // Vector indicating that ground calculations completely failed
        public static readonly Vector3 InvalidDropPosition = new Vector3(-9999f, -9999f, -9999f);

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            Unity.FPS.Core.FlagCarrierTracker.OnFlagDroppedServer += HandleExternalFlagDrop;
        }

        public override void OnDestroy()
        {
            Unity.FPS.Core.FlagCarrierTracker.OnFlagDroppedServer -= HandleExternalFlagDrop;

            base.OnDestroy();
        }

        private void HandleExternalFlagDrop(int teamIndex, Vector3 dropPosition)
        {
            if (!IsServer) return;

            // --- GROUND RAYCAST CHECK ---
            // If the position sent matches our invalid fallback marker, force a home base respawn!
            if (dropPosition == InvalidDropPosition)
            {
                Debug.LogWarning($"[FlagSpawner] Flag drop position was invalid! Instantly returning Flag {teamIndex} to base origin.");
                SpawnFlagForTeam(teamIndex);
                return;
            }

            Debug.Log($"[FlagSpawner] Spawning flag for Team {teamIndex} at ground coordinates: {dropPosition}");
            SpawnFlagAtPosition(teamIndex, dropPosition);
        }

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;
            SpawnAllFlags();
        }

        private void SpawnAllFlags()
        {
            foreach (var teamData in Teams)
            {
                if (teamData.FlagPrefab == null || teamData.FlagSpawnPoint == null) continue;
                SpawnFlagForTeam(teamData.TeamIndex);
            }
        }

        public void SpawnFlagForTeam(int teamIndex)
        {
            if (!IsServer) return;

            TeamFlagData data = Teams.Find(t => t.TeamIndex == teamIndex);
            if (data.FlagSpawnPoint == null || data.FlagPrefab == null) return;

            if (liveFlagInstances.ContainsKey(teamIndex) && liveFlagInstances[teamIndex] != null)
            {
                var netObj = liveFlagInstances[teamIndex].GetComponent<NetworkObject>();
                if (netObj != null && netObj.IsSpawned) netObj.Despawn();
                else Destroy(liveFlagInstances[teamIndex]);

                liveFlagInstances.Remove(teamIndex);
            }

            GameObject flagInstance = Instantiate(data.FlagPrefab, data.FlagSpawnPoint.position, data.FlagSpawnPoint.rotation);

            if (flagInstance.TryGetComponent<FlagPickup>(out var flagPickup))
            {
                flagPickup.FlagTeamIndex = data.TeamIndex;
            }

            liveFlagInstances[teamIndex] = flagInstance;

            NetworkObject networkObject = flagInstance.GetComponent<NetworkObject>();
            if (networkObject != null)
            {
                networkObject.Spawn();
            }
        }

        public Vector3 GetFlagHomePosition(int teamIndex)
        {
            TeamFlagData data = Teams.Find(t => t.TeamIndex == teamIndex);
            if (data.FlagSpawnPoint != null) return data.FlagSpawnPoint.position;
            return Vector3.zero;
        }

        public void SpawnFlagAtPosition(int teamIndex, Vector3 customPosition)
        {
            if (!IsServer) return;

            TeamFlagData data = Teams.Find(t => t.TeamIndex == teamIndex);
            if (data.FlagPrefab == null) return;

            if (liveFlagInstances.ContainsKey(teamIndex) && liveFlagInstances[teamIndex] != null)
            {
                var netObj = liveFlagInstances[teamIndex].GetComponent<NetworkObject>();
                if (netObj != null && netObj.IsSpawned) netObj.Despawn();
                else Destroy(liveFlagInstances[teamIndex]);

                liveFlagInstances.Remove(teamIndex);
            }

            GameObject flagInstance = Instantiate(data.FlagPrefab, customPosition, Quaternion.identity);

            if (flagInstance.TryGetComponent<FlagPickup>(out var flagPickup))
            {
                flagPickup.FlagTeamIndex = data.TeamIndex;
            }

            liveFlagInstances[teamIndex] = flagInstance;

            NetworkObject networkObject = flagInstance.GetComponent<NetworkObject>();
            if (networkObject != null) networkObject.Spawn();
        }

        private void OnDrawGizmos()
        {
            foreach (var teamData in Teams)
            {
                if (teamData.FlagSpawnPoint == null) continue;

                Gizmos.color = teamData.TeamColor;
                Matrix4x4 rotationMatrix = Matrix4x4.TRS(teamData.FlagSpawnPoint.position, teamData.FlagSpawnPoint.rotation, Vector3.one);
                Gizmos.matrix = rotationMatrix;

                if (teamData.FlagPrefab != null && teamData.FlagPrefab.TryGetComponent<BoxCollider>(out BoxCollider box))
                {
                    Gizmos.DrawWireCube(box.center, box.size);
                }
                else
                {
                    Gizmos.DrawWireCube(Vector3.up * 0.5f, new Vector3(0.5f, 1f, 0.5f));
                }

                Gizmos.matrix = Matrix4x4.identity;

#if UNITY_EDITOR
                Vector3 labelPos = teamData.FlagSpawnPoint.position + (Vector3.up * 1.5f);
                UnityEditor.Handles.Label(labelPos, $"[CTF] {teamData.TeamName} Flag Spawn Point");
#endif
            }
        }
    }
}