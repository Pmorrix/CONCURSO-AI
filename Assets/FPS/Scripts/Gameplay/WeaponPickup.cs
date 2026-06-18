using Unity.FPS.Game;
using Unity.Netcode;
using UnityEngine;

namespace Unity.FPS.Gameplay
{
    // Make sure WeaponPickup inherits from NetworkBehaviour so it can use RPCs
    public class WeaponPickup : Pickup
    {
        [Tooltip("The prefab for the weapon that will be added to the player on pickup")]
        public WeaponController WeaponPrefab;

        void Start()
        {
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
            {
                if (t != transform)
                    t.gameObject.layer = 0;
            }
        }

        protected override bool OnPicked(PlayerCharacterController byPlayer)
        {
            // If we are the Server/Host, handle it directly like normal
            if (NetworkManager.Singleton.IsServer)
            {
                return ExecutePickupLogic(byPlayer);
            }
            else
            {
                // If we are a Client, ask the server to do it for us!
                // We pass the NetworkObjectId of the player so the server knows WHO picked it up.
                NetworkObject playerNetObj = byPlayer.GetComponent<NetworkObject>();
                if (playerNetObj != null)
                {
                    RequestPickupServerRpc(playerNetObj.NetworkObjectId);

                    // Return true locally so the pickup immediately disappears/gives feedback on client
                    return true;
                }
            }

            return false;
        }

        [Rpc(SendTo.Server)]
        private void RequestPickupServerRpc(ulong playerNetworkObjectId)
        {
            // Find the player object on the server using their ID
            if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(playerNetworkObjectId, out NetworkObject playerNetObj))
            {
                PlayerCharacterController player = playerNetObj.GetComponent<PlayerCharacterController>();
                if (player != null)
                {
                    ExecutePickupLogic(player);
                }
            }
        }

        // Helper method to hold the actual inventory logic
        private bool ExecutePickupLogic(PlayerCharacterController player)
        {
            PlayerWeaponsManager playerWeaponsManager = player.GetComponent<PlayerWeaponsManager>();

            if (playerWeaponsManager)
            {
                if (playerWeaponsManager.AddWeapon(WeaponPrefab))
                {
                    if (playerWeaponsManager.GetActiveWeapon() == null)
                    {
                        playerWeaponsManager.SwitchWeapon(true);
                    }

                    // If we are on the server, we also need to ensure the pickup object is network-despawned
                    NetworkObject pickupNetObj = GetComponent<NetworkObject>();
                    if (pickupNetObj != null && pickupNetObj.IsSpawned)
                    {
                        pickupNetObj.Despawn(); // Removes it for everyone
                    }

                    return true;
                }
            }
            return false;
        }
    }
}