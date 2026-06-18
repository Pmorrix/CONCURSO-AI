using UnityEngine;
using System;

namespace Unity.FPS.Game
{
    public static class PlayerDeathBridge
    {
        // This event lives in the Game namespace, accessible by both AI and Gameplay assemblies
        public static Action<GameObject> OnAnyPlayerDied;

        public static void RaisePlayerDied(GameObject player)
        {
            OnAnyPlayerDied?.Invoke(player);
        }
    }
}