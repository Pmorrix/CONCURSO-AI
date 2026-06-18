using UnityEngine;

namespace Unity.FPS.Gameplay
{
    public class JetpackPickup : Pickup
    {
        protected override bool OnPicked(PlayerCharacterController byPlayer)
        {

            var jetpack = byPlayer.GetComponent<Jetpack>();

            // If the player doesn't have a jetpack component, the pickup fails
            if (!jetpack)
                return false;

            if (jetpack.TryUnlock())
            {
                Debug.Log("Unlocked Jetpack!");
                return true;
            }

                return false;
        }
    }
}