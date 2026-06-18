using UnityEngine;
using Unity.FPS.Core;   
using Unity.FPS.Game;  

namespace Unity.FPS.Gameplay
{
    public class FlagAudioObserver : MonoBehaviour
    {
        [Header("SFX Configurations")]
        [SerializeField] private AudioClip flagCollectedSFX;

        private void Start()
        {
            // Subscribe to the clean core event hook
            FlagCarrierTracker.OnLocalPlayerFlagStateChanged += HandleFlagSound;
        }

        private void OnDestroy()
        {
            FlagCarrierTracker.OnLocalPlayerFlagStateChanged -= HandleFlagSound;
        }

        private void HandleFlagSound(bool isCarrying)
        {
            // If they just successfully picked it up, play the sound
            if (isCarrying && flagCollectedSFX != null)
            {
                AudioUtility.CreateSFX(
                    flagCollectedSFX,
                    Vector3.zero,
                    AudioUtility.AudioGroups.HUDObjective,
                    0f
                );
            }
        }
    }
}