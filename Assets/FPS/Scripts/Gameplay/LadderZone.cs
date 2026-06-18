using UnityEngine;

namespace Unity.FPS.Gameplay
{


    public class LadderZone : MonoBehaviour
    {
        private void OnTriggerEnter(Collider other)
        {
            if (other.TryGetComponent(out PlayerCharacterController player))
            {
                player.SetOnLadderState(true);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.TryGetComponent(out PlayerCharacterController player))
            {
                player.SetOnLadderState(false);
            }
        }
    }
}