using UnityEngine;

namespace Unity.FPS.Gameplay
{
    public abstract class BotBehaviour : MonoBehaviour
    {
        // This will be called every frame to get movement input
        public abstract Vector3 GetMoveInput();

        // Camera rotation inputs (horizontal and vertical)
        public abstract Vector2 GetLookInputs();

        // Action inputs
        public abstract bool GetJumpInputDown();
        public abstract bool GetJumpInputHeld();
        public abstract bool GetFireInputHeld();
        public abstract bool GetFireInputDown();
        public abstract bool GetFireInputReleased();
        public abstract bool GetAimInputHeld();
        public abstract bool GetSprintInputHeld();
        public abstract bool GetCrouchInputDown();
        public abstract bool GetCrouchInputReleased();
        public abstract bool GetReloadButtonDown();
        public abstract int GetSwitchWeaponInput();
        public abstract int GetSelectWeaponInput();

        // Optional: Initialize method called when bot is spawned
        public virtual void Initialize(PlayerCharacterController controller) { }

        // Optional: Update method for bot-specific logic
        public virtual void OnBotUpdate() { }
    }
}