using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;

namespace Unity.FPS.Gameplay
{
    public class BotInputOverrider : NetworkBehaviour
    {
        private PlayerInputHandler m_InputHandler;
        private float m_JumpTimer;
        private bool m_ShouldJumpThisFrame;

        private void Awake()
        {
            m_InputHandler = GetComponent<PlayerInputHandler>();
        }

        public override void OnNetworkSpawn()
        {
            // If this is a human player, self-destruct this script immediately 
            // so it doesn't interfere with standard gameplay.
            if (IsOwner && !IsBotInstance())
            {
                Destroy(this);
                return;
            }

            // FOR BOTS: Kill the player input system completely so no keyboard/mouse 
            // inputs filter down into the game systems.
            if (TryGetComponent<PlayerInput>(out PlayerInput playerInput))
            {
                playerInput.enabled = false;
            }

            // Reset our jump cadence timer
            m_JumpTimer = 0f;
        }

        private void Update()
        {
            // Only simulate bot behavior where physics/logic updates are appropriate
            if (!IsServer && !IsOwner) return;

            // Track our 5-second jump cadence interval
            m_JumpTimer += Time.deltaTime;
            if (m_JumpTimer >= 5.0f)
            {
                m_ShouldJumpThisFrame = true;
                m_JumpTimer = 0f;
                Debug.Log($"[BOT AI] Jump triggered!");
            }
        }

        private void LateUpdate()
        {
            // Reset the jump state at the very end of the frame once processing finishes
            if (m_ShouldJumpThisFrame)
            {
                m_ShouldJumpThisFrame = false;
            }
        }

        // --- Simulated Bot Inputs replacing human actions ---

        public bool GetBotJumpInputDown()
        {
            return m_ShouldJumpThisFrame;
        }

        public Vector3 GetBotMoveInput()
        {
            // Return Vector3.zero for now, or add pathfinding/patrol vector data here
            return Vector3.zero;
        }

        /// Helper function to check if this specific instance was flagged as a bot on initialization.
        /// Replace this logic with your actual LobbyManager/PlayerSettings configuration check!
        private bool IsBotInstance()
        {
            return true;
        }
    }
}