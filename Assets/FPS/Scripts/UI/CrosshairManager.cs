using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace Unity.FPS.UI
{
    public class CrosshairManager : MonoBehaviour
    {
        public Image CrosshairImage;
        public Sprite NullCrosshairSprite;
        public float CrosshairUpdateshrpness = 5f;

        private PlayerWeaponsManager m_WeaponsManager;
        private bool m_WasPointingAtEnemy;
        private RectTransform m_CrosshairRectTransform;
        private CrosshairData m_CrosshairDataDefault;
        private CrosshairData m_CrosshairDataTarget;
        private CrosshairData m_CurrentCrosshair;

        void Start()
        {
            // Cache the RectTransform early so it's safely available anytime
            if (CrosshairImage != null)
            {
                m_CrosshairRectTransform = CrosshairImage.GetComponent<RectTransform>();
            }

            FindLocalPlayerWeaponsManager();
        }

        void Update()
        {
            // Keep looking for local player if not found
            if (m_WeaponsManager == null)
            {
                FindLocalPlayerWeaponsManager();
                return;
            }

            UpdateCrosshairPointingAtEnemy(false);
            m_WasPointingAtEnemy = m_WeaponsManager.IsPointingAtEnemy;
        }

        private void FindLocalPlayerWeaponsManager()
        {
            PlayerCharacterController[] players = FindObjectsByType<PlayerCharacterController>(FindObjectsSortMode.None);

            foreach (var player in players)
            {
                if (player.IsOwner)
                {
                    PlayerWeaponsManager weaponsManager = player.GetComponent<PlayerWeaponsManager>();

                    if (weaponsManager != null && m_WeaponsManager != weaponsManager)
                    {
                        // Unsubscribe from old events if switching to a different player
                        if (m_WeaponsManager != null)
                        {
                            m_WeaponsManager.OnSwitchedToWeapon -= OnWeaponChanged;
                        }

                        m_WeaponsManager = weaponsManager;
                        m_WeaponsManager.OnSwitchedToWeapon += OnWeaponChanged;

                        // Initialize with current weapon (Handles the Voting Gun if it is already out!)
                        WeaponController activeWeapon = m_WeaponsManager.GetActiveWeapon();
                        if (activeWeapon != null)
                        {
                            OnWeaponChanged(activeWeapon);
                        }
                    }
                    return;
                }
            }
        }

        void UpdateCrosshairPointingAtEnemy(bool force)
        {
            // If there's no sprite, just turn off the image instead of locking up the whole function
            if (m_CrosshairDataDefault.CrosshairSprite == null)
            {
                if (CrosshairImage.enabled) CrosshairImage.enabled = false;
                return;
            }

            if ((force || !m_WasPointingAtEnemy) && m_WeaponsManager.IsPointingAtEnemy)
            {
                m_CurrentCrosshair = m_CrosshairDataTarget;
                CrosshairImage.sprite = m_CurrentCrosshair.CrosshairSprite;
                m_CrosshairRectTransform.sizeDelta = m_CurrentCrosshair.CrosshairSize * Vector2.one;
            }
            else if ((force || m_WasPointingAtEnemy) && !m_WeaponsManager.IsPointingAtEnemy)
            {
                m_CurrentCrosshair = m_CrosshairDataDefault;
                CrosshairImage.sprite = m_CurrentCrosshair.CrosshairSprite;
                m_CrosshairRectTransform.sizeDelta = m_CurrentCrosshair.CrosshairSize * Vector2.one;
            }

            CrosshairImage.color = Color.Lerp(CrosshairImage.color, m_CurrentCrosshair.CrosshairColor,
                Time.deltaTime * CrosshairUpdateshrpness);

            m_CrosshairRectTransform.sizeDelta = Mathf.Lerp(m_CrosshairRectTransform.sizeDelta.x,
                m_CurrentCrosshair.CrosshairSize,
                Time.deltaTime * CrosshairUpdateshrpness) * Vector2.one;
        }

        void OnWeaponChanged(WeaponController newWeapon)
        {
            if (newWeapon)
            {
                CrosshairImage.enabled = true;
                m_CrosshairDataDefault = newWeapon.CrosshairDataDefault;
                m_CrosshairDataTarget = newWeapon.CrosshairDataTargetInSight;

                if (m_CrosshairRectTransform == null)
                {
                    m_CrosshairRectTransform = CrosshairImage.GetComponent<RectTransform>();
                }

                DebugUtility.HandleErrorIfNullGetComponent<RectTransform, CrosshairManager>(m_CrosshairRectTransform,
                    this, CrosshairImage.gameObject);
            }
            else
            {
                if (NullCrosshairSprite)
                {
                    CrosshairImage.sprite = NullCrosshairSprite;
                }
                else
                {
                    CrosshairImage.enabled = false;
                }
            }

            UpdateCrosshairPointingAtEnemy(true);
        }

        void OnDestroy()
        {
            if (m_WeaponsManager != null)
            {
                m_WeaponsManager.OnSwitchedToWeapon -= OnWeaponChanged;
            }
        }
    }
}