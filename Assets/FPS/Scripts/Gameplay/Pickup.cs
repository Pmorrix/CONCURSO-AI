using Unity.FPS.Game;
using UnityEngine;
using Unity.Netcode;

namespace Unity.FPS.Gameplay
{
    [RequireComponent(typeof(Rigidbody), typeof(Collider))]
    public class Pickup : NetworkBehaviour
    {
        [Tooltip("Frequency at which the item will move up and down")]
        public float VerticalBobFrequency = 1f;

        [Tooltip("Distance the item will move up and down")]
        public float BobbingAmount = 1f;

        [Tooltip("Rotation angle per second")] public float RotatingSpeed = 360f;

        [Tooltip("Sound played on pickup")] public AudioClip PickupSfx;
        [Tooltip("VFX spawned on pickup")] public GameObject PickupVfxPrefab;

        public Rigidbody PickupRigidbody { get; private set; }

        Collider m_Collider;
        Vector3 m_StartPosition;
        bool m_HasPlayedFeedback;

        void Awake()
        {
            m_StartPosition = transform.position;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            PickupRigidbody = GetComponent<Rigidbody>();
            DebugUtility.HandleErrorIfNullGetComponent<Rigidbody, Pickup>(PickupRigidbody, this, gameObject);
            m_Collider = GetComponent<Collider>();
            DebugUtility.HandleErrorIfNullGetComponent<Collider, Pickup>(m_Collider, this, gameObject);

            // ensure the physics setup is a kinematic rigidbody trigger
            PickupRigidbody.isKinematic = true;
            m_Collider.isTrigger = true;
        }

        void Update()
        {
            // Handle bobbing
            float bobbingAnimationPhase = ((Mathf.Sin(Time.time * VerticalBobFrequency) * 0.5f) + 0.5f) * BobbingAmount;
            transform.position = m_StartPosition + Vector3.up * bobbingAnimationPhase;

            // Handle rotating
            transform.Rotate(Vector3.up, RotatingSpeed * Time.deltaTime, Space.Self);
        }

        void OnTriggerEnter(Collider other)
        {

            PlayerCharacterController pickingPlayer = other.GetComponent<PlayerCharacterController>();

            if (pickingPlayer != null && pickingPlayer.IsLocalPlayer)
            {
                if (OnPicked(pickingPlayer))
                {
                    RequestPickupServerRpc();

                    PlayPickupFeedback();
                    PickupEvent evt = Events.PickupEvent;
                    evt.Pickup = gameObject;
                    EventManager.Broadcast(evt);
                }
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        void RequestPickupServerRpc(RpcParams rpcParams = default)
        {
            if (NetworkObject != null && NetworkObject.IsSpawned)
            {
                NetworkObject.Despawn();
            }

        }

        [Rpc(SendTo.SpecifiedInParams)]
        void PlayFeedbackClientRpc(RpcParams rpcParams)
        {
            //PlayPickupFeedback();
        }

        // Change this to return a bool so the server knows if the pickup was "successful"
        protected virtual bool OnPicked(PlayerCharacterController playerController)
        {
            PlayPickupFeedback();
            return true;
        }

        public void PlayPickupFeedback()
        {
            if (m_HasPlayedFeedback)
                return;

            if (PickupSfx)
            {
                AudioUtility.CreateSFX(PickupSfx, transform.position, AudioUtility.AudioGroups.Pickup, 0f);
            }

            if (PickupVfxPrefab)
            {
                var pickupVfxInstance = Instantiate(PickupVfxPrefab, transform.position, Quaternion.identity);
            }

            m_HasPlayedFeedback = true;
        }
    }
}