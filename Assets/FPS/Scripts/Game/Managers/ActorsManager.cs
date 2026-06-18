using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Unity.FPS.Game
{
    public class ActorsManager : NetworkBehaviour
    {
        // Singleton pattern makes it easier for Actors to find the manager
        public static ActorsManager Instance { get; private set; }

        public List<Actor> Actors { get; private set; } = new List<Actor>();

        // We use a list because 'The Player' is now multiple people
        public List<GameObject> Players { get; private set; } = new List<GameObject>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this.gameObject);
                return;
            }
            Instance = this;

            Actors = new List<Actor>();
            Players = new List<GameObject>();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            // Clear lists on spawn to ensure clean state transition across scenes
            if (IsServer)
            {
                Actors.Clear();
                Players.Clear();
                Debug.Log("[Server ActorsManager] Network Spawned & Tracking Initialized.");
            }
        }

        public void AddPlayer(GameObject player)
        {
            // Only the server simulates AI tracking lists
            if (!IsServer) return;

            if (!Players.Contains(player))
            {
                Players.Add(player);

                // Automatically grab the Actor component from the player object 
                // and tie it to the server's master target list.
                Actor actor = player.GetComponent<Actor>();
                if (actor != null)
                {
                    RegisterActor(actor);
                }
            }
        }

        public void RemovePlayer(GameObject player)
        {
            if (!IsServer) return;

            if (Players.Contains(player))
            {
                Players.Remove(player);

                Actor actor = player.GetComponent<Actor>();
                if (actor != null)
                {
                    UnregisterActor(actor);
                }
            }
        }

        public void RegisterActor(Actor actor)
        {
            if (!IsServer) return;

            if (!Actors.Contains(actor))
            {
                Actors.Add(actor);
                Debug.Log($"[Server ActorsManager] Registered Actor: {actor.gameObject.name}");
            }

            ShowActors();
        }

        public void UnregisterActor(Actor actor)
        {
            if (!IsServer) return;

            if (Actors.Contains(actor))
            {
                Actors.Remove(actor);
                Debug.Log($"[Server ActorsManager] Unregistered Actor: {actor.gameObject.name}");
            }

            ShowActors();
        }

        public void ShowActors()
        {
            // Only print if we are the server/host active runtime
            if (!IsServer) return;

            // FIX: Formats the entire list into readable text names
            if (Actors.Count > 0)
            {
                List<string> actorNames = new List<string>();
                foreach (Actor a in Actors)
                {
                    if (a != null) actorNames.Add(a.gameObject.name);
                }

                string combinedList = string.Join(", ", actorNames);
                Debug.Log($"SERVER: Tracking {Actors.Count} Actors -> [{combinedList}]");
            }
            else
            {
                Debug.Log("SERVER: Current actors list is completely EMPTY!");
            }
        }
    }
}