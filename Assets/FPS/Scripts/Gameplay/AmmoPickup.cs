using Unity.FPS.Game;
using Unity.Netcode;
using UnityEngine;

namespace Unity.FPS.Gameplay
{
    public class AmmoPickup : Pickup
    {
        [Tooltip("Weapon those bullets are for")]
        public WeaponController Weapon;

        [Tooltip("Number of bullets the player gets")]
        public int BulletCount = 30;

        protected override bool OnPicked(PlayerCharacterController byPlayer)
        {
            PlayerWeaponsManager playerWeaponsManager = byPlayer.GetComponent<PlayerWeaponsManager>();

            if (playerWeaponsManager)
            {
                WeaponController weapon = playerWeaponsManager.HasWeapon(Weapon);

                // Only succeed if the player actually owns the weapon this ammo is for
                if (weapon != null)
                {
                    weapon.AddCarriablePhysicalBullets(BulletCount);

                    // Broadcast the event on the server
                    AmmoPickupEvent evt = Events.AmmoPickupEvent;
                    evt.Weapon = weapon;
                    EventManager.Broadcast(evt);

                    // Return true to trigger the ClientRpc feedback and Despawn in the base class
                    return true;
                }
            }

            return false;
        }
    }
}