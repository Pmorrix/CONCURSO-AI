using Unity.FPS.Game;
using Unity.Netcode;
using UnityEngine;
using Unity.FPS.Core;

namespace Unity.FPS.Gameplay
{
    public class FlagPickup : Pickup
    {
        [Header("Capture The Flag parameters")]
        [Tooltip("0 = Blue Team, 1 = Red Team")]
        public int FlagTeamIndex;

        // --- OVERRIDE BASE INTERACTION ---
        private void OnTriggerEnter(Collider other)
        {
            PlayerCharacterController pickingPlayer = other.GetComponent<PlayerCharacterController>();

            // 1. CLIENT SAFETY GUARD: Only allow living local players to request a pickup loop
            if (pickingPlayer != null && pickingPlayer.IsLocalPlayer)
            {
                if (pickingPlayer.TryGetComponent<Health>(out var health) && health.CurrentHealth.Value <= 0f)
                {
                    return; // Dead locally, don't waste network bandwidth
                }

                // Send the intent to the server authoritatively
                RequestFlagPickupServerRpc();
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void RequestFlagPickupServerRpc(RpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;

            // Locate the actual server copy of the player object using their Client ID
            if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var networkClient))
            {
                GameObject playerObject = networkClient.PlayerObject.gameObject;

                if (playerObject.TryGetComponent<PlayerCharacterController>(out var player) &&
                    playerObject.TryGetComponent<PlayerSettings>(out var settings) &&
                    playerObject.TryGetComponent<Health>(out var health)) // Grab health ref
                {
                    // 2. SERVER HEALTH VALIDATION PASS:
                    // Absolute security check. If the player is dead or dying, reject immediately.
                    if (health.CurrentHealth.Value <= 0f)
                    {
                        Debug.Log($"[CTF Server] Rejected Pickup! Client {clientId} tried to take Flag {FlagTeamIndex} but they are dead.");
                        return;
                    }

                    // 3. Authoritative Team Validation Pass on the Server
                    if (settings.TeamIndex != FlagTeamIndex && settings.TeamIndex != -1)
                    {
                        Debug.Log($"[CTF Server] Validated Pickup! Client {clientId} (Team {settings.TeamIndex}) successfully stole Flag {FlagTeamIndex}.");

                        // Assign flag carrying variables
                        ExecuteServerFlagOwnership(player);

                        // Broadcast feedback triggers globally
                        PlayFeedbackClientRpc();

                        // Fire local engine event architectures
                        PickupEvent evt = Events.PickupEvent;
                        evt.Pickup = gameObject;
                        EventManager.Broadcast(evt);

                        // Clean up the object from the network safely
                        if (NetworkObject != null && NetworkObject.IsSpawned)
                        {
                            NetworkObject.Despawn();
                        }
                    }
                    else
                    {
                        Debug.Log($"[CTF Server] Rejected Pickup! Client {clientId} (Team {settings.TeamIndex}) tried to take Flag {FlagTeamIndex}.");
                    }
                }
            }
        }

        private void ExecuteServerFlagOwnership(PlayerCharacterController player)
        {
            if (player.TryGetComponent<FlagCarrierTracker>(out var tracker))
            {
                tracker.SetCarryingFlagState(true);
            }
        }

        [Rpc(SendTo.Everyone)]
        private void PlayFeedbackClientRpc()
        {
            PlayPickupFeedback();
        }
    }
}