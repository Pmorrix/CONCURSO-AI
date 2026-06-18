using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;

namespace Unity.FPS.UI
{
    public class NotificationHUDManager : MonoBehaviour
    {
        [Tooltip("UI panel containing the layoutGroup for displaying notifications")]
        public RectTransform NotificationPanel;

        [Tooltip("Prefab for the notifications")]
        public GameObject NotificationPrefab;

        private PlayerWeaponsManager m_LocalPlayerWeaponsManager;
        private Jetpack m_LocalJetpack;
        private bool m_HasEventListeners = false;

        void Start()
        {
            FindLocalPlayer();
        }

        void Update()
        {
            if (m_LocalPlayerWeaponsManager == null || m_LocalJetpack == null)
            {
                FindLocalPlayer();
            }
        }

        private void FindLocalPlayer()
        {
            PlayerCharacterController[] players = FindObjectsByType<PlayerCharacterController>(FindObjectsSortMode.None);

            foreach (var player in players)
            {
                if (player.IsOwner)
                {
                    PlayerWeaponsManager weaponsManager = player.GetComponent<PlayerWeaponsManager>();
                    Jetpack jetpack = player.GetComponent<Jetpack>();

                    if (weaponsManager != null && jetpack != null)
                    {
                        if (m_LocalPlayerWeaponsManager != weaponsManager || m_LocalJetpack != jetpack)
                        {
                            RemoveEventListeners();

                            m_LocalPlayerWeaponsManager = weaponsManager;
                            m_LocalJetpack = jetpack;

                            SetupEventListeners();
                        }
                    }
                    return;
                }
            }
        }

        private void SetupEventListeners()
        {
            if (m_HasEventListeners) return;

            if (m_LocalPlayerWeaponsManager != null)
            {
                m_LocalPlayerWeaponsManager.OnAddedWeapon += OnPickupWeapon;
            }

            if (m_LocalJetpack != null)
            {
                m_LocalJetpack.OnUnlockJetpack += OnUnlockJetpack;
            }

            EventManager.AddListener<ObjectiveUpdateEvent>(OnObjectiveUpdateEvent);

            m_HasEventListeners = true;
        }

        private void RemoveEventListeners()
        {
            if (!m_HasEventListeners) return;

            if (m_LocalPlayerWeaponsManager != null)
            {
                m_LocalPlayerWeaponsManager.OnAddedWeapon -= OnPickupWeapon;
            }

            if (m_LocalJetpack != null)
            {
                m_LocalJetpack.OnUnlockJetpack -= OnUnlockJetpack;
            }

            EventManager.RemoveListener<ObjectiveUpdateEvent>(OnObjectiveUpdateEvent);

            m_HasEventListeners = false;
        }

        void OnObjectiveUpdateEvent(ObjectiveUpdateEvent evt)
        {
            if (!string.IsNullOrEmpty(evt.NotificationText))
                CreateNotification(evt.NotificationText);
        }

        void OnPickupWeapon(WeaponController weaponController, int index)
        {
            if (index != 0)
                CreateNotification("Picked up weapon: " + weaponController.WeaponName);
        }

        void OnUnlockJetpack(bool unlock)
        {
            if (unlock)
                CreateNotification("Jetpack unlocked");
        }

        public void CreateNotification(string text)
        {
            if (NotificationPrefab == null || NotificationPanel == null)
            {
                Debug.LogWarning("Notification prefab or panel not set!");
                return;
            }

            GameObject notificationInstance = Instantiate(NotificationPrefab, NotificationPanel);
            notificationInstance.transform.SetSiblingIndex(0);

            NotificationToast toast = notificationInstance.GetComponent<NotificationToast>();
            if (toast)
            {
                toast.Initialize(text);
            }
        }

        void OnDestroy()
        {
            RemoveEventListeners();
        }
    }
}