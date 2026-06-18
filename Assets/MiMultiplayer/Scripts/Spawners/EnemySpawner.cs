using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Unity.FPS.AI;

namespace Unity.FPS.Gameplay
{
    public class EnemySpawner : NetworkBehaviour
    {
        [System.Serializable]
        public struct EnemySpawnConfig
        {
            public string Name;
            public GameObject EnemyPrefab;
            public Transform SpawnPoint;
            public PatrolPath AssignedPatrolPath;
        }

        [Header("Enemy Configurations")]
        public List<EnemySpawnConfig> EnemiesToSpawn = new List<EnemySpawnConfig>();

        [Header("Gizmo Settings")]
        public Color GizmoColor = Color.red;
        public Vector3 EnemySize = new Vector3(0.5f, 2f, 0.5f);

        public override void OnNetworkSpawn()
        {
            // Critical: Only the Server/Host manages AI and spawning
            if (!IsServer) return;

            SpawnAllEnemies();
        }

        public void SpawnAllEnemies()
        {
            foreach(var config in EnemiesToSpawn)
            {
                if (config.EnemyPrefab == null) continue;

                if (config.SpawnPoint == null)
                {
                    Debug.LogWarning($"Enemy '{config.Name}' is missing a assigned SpawnPoint Transform! Skipping.");
                    continue;
                }

                // 1. Instantiate on the Server using the Transform's real-time position/rotation
                GameObject enemyInstance = Instantiate(
                    config.EnemyPrefab,
                    config.SpawnPoint.position,
                    config.SpawnPoint.rotation
                );

                // Patrol Path
                if (config.AssignedPatrolPath != null)
                {
                    EnemyController enemyCtrl = enemyInstance.GetComponent<EnemyController>();
                    if (enemyCtrl != null)
                    {
                        enemyCtrl.PatrolPath = config.AssignedPatrolPath;
                    }
                    else
                    {
                        Debug.LogWarning($"Enemy '{config.Name}' has a Patrol Path assigned in the spawner, but the prefab is missing an EnemyController component!");
                    }
                }

                // 2. Spawn onto the Network
                NetworkObject netObj = enemyInstance.GetComponent<NetworkObject>();
                if (netObj != null)
                {
                    netObj.Spawn();
                }
                else
                {
                    Debug.LogError($"Enemy {config.Name} is missing a NetworkObject! It won't appear on Clients.");
                    // Clean up un-spawned server gameobjects to prevent desync ghosts
                    Destroy(enemyInstance);
                }
            }
        }

        // Visualizing the spawn points in the Editor
        private void OnDrawGizmos()
        {
            if (EnemiesToSpawn == null) return;

            foreach (var config in EnemiesToSpawn)
            {
                if (config.SpawnPoint == null) continue;

                // Grab the current matrix of the empty GameObject spawn point
                Matrix4x4 rotationMatrix = Matrix4x4.TRS(config.SpawnPoint.position, config.SpawnPoint.rotation, config.SpawnPoint.localScale);

                // Backup current gizmo matrix so we don't screw up other gizmos in the scene
                Matrix4x4 oldMatrix = Gizmos.matrix;
                Gizmos.matrix = rotationMatrix;

                Gizmos.color = GizmoColor;
                // Draw the enemy bounding box (offset by half height so pivot sits cleanly on the ground)
                Gizmos.DrawWireCube(new Vector3(0, EnemySize.y / 2, 0), EnemySize);

                // Draw a little forward direction line matching the transform's local Z axis
                Gizmos.DrawLine(new Vector3(0, EnemySize.y / 2, 0), new Vector3(0, EnemySize.y / 2, 1.0f));

                // Restore old matrix
                Gizmos.matrix = oldMatrix;

                #if UNITY_EDITOR
                UnityEditor.Handles.Label(config.SpawnPoint.position + Vector3.up * (EnemySize.y + 0.2f), $"Enemy Spawn: {config.Name}");
                #endif
            }
        }
    }
}