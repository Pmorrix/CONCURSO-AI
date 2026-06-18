using UnityEngine;

namespace Unity.FPS.Gameplay
{
    public class JumpingBotBehaviour : BotBehaviour
    {
        private float m_JumpTimer;
        private float m_JumpInterval = 5f;
        private float m_JumpHoldTimer;
        private float m_JumpHoldDuration = 2f; // Hold for 2 seconds
        private bool m_IsJumping = false;

        // Movement variables
        private Vector3 m_MoveDirection;

        private bool m_WishJump = false; // Flag indicating the bot wants to start a jump

        public override void Initialize(PlayerCharacterController controller)
        {
            m_JumpTimer = 0f;
            m_JumpHoldTimer = 0f;
            m_IsJumping = false;

            // Set constant forward movement direction
            m_MoveDirection = Vector3.forward; // Always move forward relative to the bot's facing direction

            Debug.Log($"[JumpingBot] Initialized - will walk forward constantly and jump every {m_JumpInterval}s");
        }

        public override void OnBotUpdate()
        {
            m_JumpTimer += Time.deltaTime;

            // Trigger intent to jump every 5 seconds
            if (m_JumpTimer >= m_JumpInterval)
            {
                m_WishJump = true;
                m_JumpTimer = 0f; // Reset the clock safely here
                Debug.Log("[JumpingBot] Intent to jump primed!");
            }
        }

        public override Vector3 GetMoveInput()
        {
            // Always move forward (relative to bot's transform)
            // The PlayerCharacterController will transform this relative to the bot's rotation
            return m_MoveDirection;
        }

        public override Vector2 GetLookInputs()
        {
            // No camera movement - bot looks straight ahead
            return Vector2.zero;
        }

        public override bool GetJumpInputDown()
        {
            // If the bot wants to jump, return true
            if (m_WishJump)
            {
                // We clear the flag only when this gets consumed by the character script
                m_WishJump = false;
                Debug.Log($"[JumpingBot] Jump consumed by Character Controller at {Time.time:F2}s!");
                return true;
            }
            return false;
        }

        public override bool GetJumpInputHeld()
        {
            // Return true while we're in the jump hold period
            if (m_IsJumping && m_JumpHoldTimer < m_JumpHoldDuration)
            {
                Debug.Log($"[JumpingBot] GetJumpInputHeld() = TRUE at {Time.time:F2}s (Hold progress: {m_JumpHoldTimer:F2}/{m_JumpHoldDuration}s)");
                return true;
            }
            return false;
        }

        // Optional: Add sprint occasionally for variety
        public override bool GetSprintInputHeld()
        {
            // Sprint 30% of the time for variety
            // This will make the bot move faster when true
            return Random.Range(0f, 1f) < 0.3f;
        }

        // All other input methods return false
        public override bool GetFireInputHeld() => false;
        public override bool GetFireInputDown() => false;
        public override bool GetFireInputReleased() => false;
        public override bool GetAimInputHeld() => false;
        public override bool GetCrouchInputDown() => false;
        public override bool GetCrouchInputReleased() => false;
        public override bool GetReloadButtonDown() => false;
        public override int GetSwitchWeaponInput() => 0;
        public override int GetSelectWeaponInput() => 0;
    }
}