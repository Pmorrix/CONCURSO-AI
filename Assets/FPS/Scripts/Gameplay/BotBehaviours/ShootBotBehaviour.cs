using UnityEngine;

namespace Unity.FPS.Gameplay
{
    public class ShootBotBehaviour : BotBehaviour
    {
        private float m_ShootTimer;
        private float m_ShootInterval = 5f; // Shoot every 5 seconds
        private float m_ShootHoldDuration = 0.5f; // Hold the trigger for 0.5 seconds
        private float m_ShootHoldTimer;
        private bool m_IsShooting;

        public override void Initialize(PlayerCharacterController controller)
        {
            m_ShootTimer = 0f;
            m_ShootHoldTimer = 0f;
            m_IsShooting = false;
            Debug.Log($"[ShootBot] Initialized - will shoot every {m_ShootInterval} seconds for {m_ShootHoldDuration}s");
        }

        public override void OnBotUpdate()
        {
            // Count up the main timer
            m_ShootTimer += Time.deltaTime;

            // Handle shooting hold duration
            if (m_IsShooting)
            {
                m_ShootHoldTimer += Time.deltaTime;

                if (m_ShootHoldTimer >= m_ShootHoldDuration)
                {
                    m_IsShooting = false;
                    Debug.Log($"[ShootBot] Shooting stopped after {m_ShootHoldDuration}s");
                }
            }

            // Check if it's time to start shooting
            if (!m_IsShooting && m_ShootTimer >= m_ShootInterval)
            {
                Debug.Log($"[ShootBot] Timer reached {m_ShootInterval}s, starting to shoot!");
                m_IsShooting = true;
                m_ShootHoldTimer = 0f;
                m_ShootTimer = 0f; // Reset timer for next cycle
            }

            // Log every second for debugging
            if (Mathf.FloorToInt(m_ShootTimer) != Mathf.FloorToInt(m_ShootTimer - Time.deltaTime))
            {
                Debug.Log($"[ShootBot] Timer: {Mathf.FloorToInt(m_ShootTimer)}s / {m_ShootInterval}s, IsShooting: {m_IsShooting}");
            }
        }

        public override Vector3 GetMoveInput() => Vector3.zero;
        public override Vector2 GetLookInputs() => Vector2.zero;

        public override bool GetFireInputDown()
        {
            // Return true on the first frame of shooting
            if (m_IsShooting && m_ShootHoldTimer < Time.deltaTime)
            {
                Debug.Log($"[ShootBot] GetFireInputDown() = TRUE at {Time.time:F2}s");
                return true;
            }
            return false;
        }

        public override bool GetFireInputHeld()
        {
            // Return true while actively shooting (during the hold duration)
            if (m_IsShooting && m_ShootHoldTimer < m_ShootHoldDuration)
            {
                if (m_ShootHoldTimer > 0) // Don't log every frame, just occasional
                {
                    Debug.Log($"[ShootBot] GetFireInputHeld() = TRUE (holding for {m_ShootHoldTimer:F2}s)");
                }
                return true;
            }
            return false;
        }

        public override bool GetFireInputReleased()
        {
            // Return true when shooting stops
            if (!m_IsShooting && m_ShootHoldTimer >= m_ShootHoldDuration && m_ShootHoldTimer > 0)
            {
                Debug.Log($"[ShootBot] GetFireInputReleased() = TRUE at {Time.time:F2}s");
                return true;
            }
            return false;
        }

        // Jump methods return false
        public override bool GetJumpInputDown() => false;
        public override bool GetJumpInputHeld() => false;

        // Other input methods return false
        public override bool GetAimInputHeld() => false;
        public override bool GetSprintInputHeld() => false;
        public override bool GetCrouchInputDown() => false;
        public override bool GetCrouchInputReleased() => false;
        public override bool GetReloadButtonDown() => false;
        public override int GetSwitchWeaponInput() => 0;
        public override int GetSelectWeaponInput() => 0;
    }
}