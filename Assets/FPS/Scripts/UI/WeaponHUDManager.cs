using System.Collections.Generic;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;

namespace Unity.FPS.UI
{
    public class WeaponHUDManager : MonoBehaviour
    {
        [Tooltip("UI panel containing the layoutGroup for displaying weapon ammo")]
        public RectTransform AmmoPanel;

        [Tooltip("Prefab for displaying weapon ammo")]
        public GameObject AmmoCounterPrefab;

        PlayerWeaponsManager m_PlayerWeaponsManager;
        List<AmmoCounter> m_AmmoCounters = new List<AmmoCounter>();

        void Start()
        {
            FindLocalPlayerWeaponsManager();
        }

        void Update()
        {
            if (m_PlayerWeaponsManager == null)
            {
                FindLocalPlayerWeaponsManager();
            }
        }

        private void FindLocalPlayerWeaponsManager()
        {
            PlayerCharacterController[] players = FindObjectsByType<PlayerCharacterController>(FindObjectsSortMode.None);

            foreach (var player in players)
            {
                if (player.IsOwner)
                {
                    PlayerWeaponsManager weaponsManager = player.GetComponent<PlayerWeaponsManager>();
                    if (weaponsManager != null && m_PlayerWeaponsManager != weaponsManager)
                    {
                        SetUpWeaponsManager(weaponsManager);
                    }
                    return;
                }
            }
        }

        private void SetUpWeaponsManager(PlayerWeaponsManager weaponsManager)
        {
            if (m_PlayerWeaponsManager != null)
            {
                m_PlayerWeaponsManager.OnAddedWeapon -= AddWeapon;
                m_PlayerWeaponsManager.OnRemovedWeapon -= RemoveWeapon;
                m_PlayerWeaponsManager.OnSwitchedToWeapon -= ChangeWeapon;

                foreach (var counter in m_AmmoCounters)
                {
                    if (counter != null)
                        Destroy(counter.gameObject);
                }
                m_AmmoCounters.Clear();
            }

            m_PlayerWeaponsManager = weaponsManager;

            m_PlayerWeaponsManager.OnAddedWeapon += AddWeapon;
            m_PlayerWeaponsManager.OnRemovedWeapon += RemoveWeapon;
            m_PlayerWeaponsManager.OnSwitchedToWeapon += ChangeWeapon;

            InitializeWithExistingWeapons();
        }

        private void InitializeWithExistingWeapons()
        {
            if (m_PlayerWeaponsManager == null) return;

            WeaponController activeWeapon = m_PlayerWeaponsManager.GetActiveWeapon();
            if (activeWeapon != null)
            {
                for (int i = 0; i < 9; i++)
                {
                    WeaponController weapon = m_PlayerWeaponsManager.GetWeaponAtSlotIndex(i);
                    if (weapon != null)
                    {
                        AddWeapon(weapon, i);
                    }
                }
            }

            if (AmmoPanel != null)
            {
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(AmmoPanel);
            }
        }

        void AddWeapon(WeaponController newWeapon, int weaponIndex)
        {
            if (AmmoPanel == null || AmmoCounterPrefab == null) return;

            foreach (var counter in m_AmmoCounters)
            {
                if (counter != null && counter.WeaponCounterIndex == weaponIndex)
                {
                    return;
                }
            }

            GameObject ammoCounterInstance = Instantiate(AmmoCounterPrefab, AmmoPanel);
            AmmoCounter newAmmoCounter = ammoCounterInstance.GetComponent<AmmoCounter>();
            DebugUtility.HandleErrorIfNullGetComponent<AmmoCounter, WeaponHUDManager>(newAmmoCounter, this,
                ammoCounterInstance.gameObject);

            newAmmoCounter.Initialize(newWeapon, weaponIndex);

            m_AmmoCounters.Add(newAmmoCounter);
        }

        void RemoveWeapon(WeaponController weaponToRemove, int weaponIndex)
        {
            int foundCounterIndex = -1;
            for (int i = 0; i < m_AmmoCounters.Count; i++)
            {
                if (m_AmmoCounters[i] != null && m_AmmoCounters[i].WeaponCounterIndex == weaponIndex)
                {
                    foundCounterIndex = i;
                    Destroy(m_AmmoCounters[i].gameObject);
                    break;
                }
            }

            if (foundCounterIndex >= 0)
            {
                m_AmmoCounters.RemoveAt(foundCounterIndex);
            }
        }

        void ChangeWeapon(WeaponController weapon)
        {
            if (AmmoPanel != null)
            {
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(AmmoPanel);
            }
        }

        void OnDestroy()
        {
            if (m_PlayerWeaponsManager != null)
            {
                m_PlayerWeaponsManager.OnAddedWeapon -= AddWeapon;
                m_PlayerWeaponsManager.OnRemovedWeapon -= RemoveWeapon;
                m_PlayerWeaponsManager.OnSwitchedToWeapon -= ChangeWeapon;
            }

            foreach (var counter in m_AmmoCounters)
            {
                if (counter != null && counter.gameObject != null)
                    Destroy(counter.gameObject);
            }
            m_AmmoCounters.Clear();
        }
    }
}