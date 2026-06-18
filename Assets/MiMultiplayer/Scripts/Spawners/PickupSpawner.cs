using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Unity.FPS.Gameplay
{
    public class PickupSpawner : NetworkBehaviour
    {
        [System.Serializable]
        public struct PickupSpawnData
        {
            public string Name; // Just for organization in inspector
            public bool IsEnabled;
            public GameObject PickupPrefab;
            public Transform SpawnPoint;
        }

        [Header("Pickup Configurations")]
        public List<PickupSpawnData> PickupsToSpawn = new List<PickupSpawnData>();

        [Header("Respawn Settings")]
        [Tooltip("Time in seconds before a consumed pickup item respawns at its location")]
        public float RespawnDelay = 10f;

        // Internal structural arrays to manage real-time tracking on the server
        private NetworkObject[] m_ActiveInstances;
        private float[] m_RespawnTimers;

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;

            // Initialize tracking arrays matching our list capacity
            m_ActiveInstances = new NetworkObject[PickupsToSpawn.Count];
            m_RespawnTimers = new float[PickupsToSpawn.Count];

            SpawnAllPickups();
        }

        void SpawnAllPickups()
        {
            for (int i = 0; i < PickupsToSpawn.Count; i++)
            {
                var data = PickupsToSpawn[i];
                if (!data.IsEnabled) continue;

                SpawnPickupAtIndex(i);
            }
        }

        void SpawnPickupAtIndex(int index)
        {
            var data = PickupsToSpawn[index];
            if (data.PickupPrefab == null || data.SpawnPoint == null) return;

            GameObject pickupInstance = Instantiate(
                data.PickupPrefab,
                data.SpawnPoint.position,
                data.SpawnPoint.rotation
            );

            NetworkObject netObj = pickupInstance.GetComponent<NetworkObject>();
            if (netObj != null)
            {
                netObj.Spawn();
                m_ActiveInstances[index] = netObj; // Store reference to track when it goes missing
            }
            else
            {
                Debug.LogError($"Prefab {data.Name} at index {index} is missing a NetworkObject component!");
                Destroy(pickupInstance);
            }
        }

        void Update()
        {
            // Only the server coordinates respawn loops!
            if (!IsServer) return;

            for (int i = 0; i < PickupsToSpawn.Count; i++)
            {
                // Skip disabled configurations
                if (!PickupsToSpawn[i].IsEnabled) continue;

                // If the tracking slot has become null, it means a player successfully consumed it
                if (m_ActiveInstances[i] == null)
                {
                    m_RespawnTimers[i] += Time.deltaTime;

                    if (m_RespawnTimers[i] >= RespawnDelay)
                    {
                        m_RespawnTimers[i] = 0f; // Reset tracking clock
                        SpawnPickupAtIndex(i);    // Re-spawn item across network
                    }
                }
            }
        }

        private void OnDrawGizmos()
        {
            foreach (var data in PickupsToSpawn)
            {
                if (data.SpawnPoint == null) continue;

                Gizmos.color = GetPickupColor(data.PickupPrefab);

                if (data.PickupPrefab != null && data.PickupPrefab.TryGetComponent<BoxCollider>(out BoxCollider box))
                {
                    Matrix4x4 rotationMatrix = Matrix4x4.TRS(data.SpawnPoint.position, data.SpawnPoint.rotation, Vector3.one);
                    Gizmos.matrix = rotationMatrix;
                    Gizmos.DrawWireCube(box.center, box.size);
                    Gizmos.matrix = Matrix4x4.identity;
                }
                else
                {
                    Gizmos.DrawWireCube(data.SpawnPoint.position, Vector3.one * 0.5f);
                }

#if UNITY_EDITOR
                Vector3 labelPos = data.SpawnPoint.position + (Vector3.up * 0.8f);
                UnityEditor.Handles.Label(labelPos, string.IsNullOrEmpty(data.Name) ? "Unassigned Pickup" : data.Name);
#endif
            }
        }

        private Color GetPickupColor(GameObject prefab)
        {
            if (prefab == null) return Color.gray;
            string lowerName = prefab.name.ToLower();

            if (lowerName.Contains("health")) return Color.green;
            if (lowerName.Contains("jetpack")) return Color.cyan;

            return new Color(1f, 0.5f, 0f);
        }
    }
}