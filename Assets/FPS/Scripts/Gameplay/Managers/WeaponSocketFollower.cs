using UnityEngine;

namespace Unity.FPS.Gameplay
{
    public class WeaponSocketFollower : MonoBehaviour
    {
        public Transform TargetSocket;

        void LateUpdate()
        {
            if (TargetSocket != null)
            {
                transform.position = TargetSocket.position;
                transform.rotation = TargetSocket.rotation;
            }
        }
    }
}