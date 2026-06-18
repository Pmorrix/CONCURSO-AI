using Unity.FPS.Game;
using UnityEngine;
using System.Collections;
using Unity.Netcode; // Needed for NetworkManager

namespace Unity.FPS.AI
{
    public class FollowPlayer : MonoBehaviour
    {
        Transform m_PlayerTransform;
        Vector3 m_OriginalOffset;

        IEnumerator Start()
        {
            // Wait for ActorsManager to exist and have at least one player
            while (ActorsManager.Instance == null || ActorsManager.Instance.Players.Count == 0)
            {
                yield return null;
            }

            // In multiplayer, we want to follow the "Local Player" 
            // (The one that the person sitting at this computer controls)
            while (m_PlayerTransform == null)
            {
                foreach (var playerObj in ActorsManager.Instance.Players)
                {
                    NetworkObject no = playerObj.GetComponent<NetworkObject>();
                    if (no != null && no.IsLocalPlayer)
                    {
                        m_PlayerTransform = playerObj.transform;
                        break;
                    }
                }
                yield return null;
            }

            m_OriginalOffset = transform.position - m_PlayerTransform.position;
        }

        void LateUpdate()
        {
            if (m_PlayerTransform != null)
            {
                transform.position = m_PlayerTransform.position + m_OriginalOffset;
            }
        }
    }
}