using Unity.FPS.Game;
using UnityEngine;

namespace Unity.FPS.Gameplay
{
    public class HealthPickup : Pickup
    {
        [Header("Parameters")]
        [Tooltip("Amount of health to heal on pickup")]
        public float HealAmount;

        protected override bool OnPicked(PlayerCharacterController player)
        {
            Health playerHealth = player.GetComponent<Health>();

            // Only return true if the player actually needed health and could pick it up
            if (playerHealth && playerHealth.CanPickup())
            {
                playerHealth.Heal(HealAmount);
                return true;
            }

            return false; // Tells the base class: "Don't despawn yet!"
        }
    }
}