using Unity.Netcode;
using UnityEngine;

namespace Unity.FPS.Game
{
    // This class contains general information describing an actor (player or enemies).
    // It is mostly used for AI detection logic and determining if an actor is friend or foe
    public class Actor : NetworkBehaviour
    {
        [Tooltip("Represents the affiliation (or team) of the actor. Actors of the same affiliation are friendly to each other")]
        public int Affiliation;

        [Tooltip("Represents point where other actors will aim when they attack this actor")]
        public Transform AimPoint;

        ActorsManager m_ActorsManager;

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;

            if (ActorsManager.Instance != null)
            {
                // If this is an AI/Enemy, register it directly here.
                // (Players are automatically registered when ActorsManager.Instance.AddPlayer() is called)
                if (!ActorsManager.Instance.Actors.Contains(this))
                {
                    ActorsManager.Instance.RegisterActor(this);
                }
            }
        }

        // OnNetworkDespawn is safer for cleanup in Netcode
        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();

            // Only the server should modify the master list
            if (IsServer && ActorsManager.Instance != null)
            {
                ActorsManager.Instance.UnregisterActor(this);
            }
        }

        // This fixes the warning by using 'override' and calling 'base.OnDestroy()'
        public override void OnDestroy()
        {
            // Always call the base method so NetworkBehaviour can clean itself up!
            base.OnDestroy();

            // Server-side safety cleanup if the object is abruptly destroyed 
            if (IsServer && ActorsManager.Instance != null)
            {
                ActorsManager.Instance.UnregisterActor(this);
            }
        }
    }
}