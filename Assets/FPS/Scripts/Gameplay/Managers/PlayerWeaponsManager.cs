using System.Collections;
using System.Collections.Generic;
using Unity.FPS.Game;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace Unity.FPS.Gameplay
{
    [RequireComponent(typeof(PlayerInputHandler))]
    public class PlayerWeaponsManager : NetworkBehaviour
    {
        public enum WeaponSwitchState
        {
            Up,
            Down,
            PutDownPrevious,
            PutUpNew,
        }

        [Tooltip("List of weapons the player will start with")]
        public List<WeaponController> StartingWeapons = new List<WeaponController>();

        [Header("References")]
        [Tooltip("Secondary camera used to avoid seeing weapon go through geometries")]
        public Camera WeaponCamera;

        [Tooltip("Parent transform where all weapons will be added in the hierarchy")]
        public Transform WeaponParentSocket;

        [Tooltip("Position for weapons when active but not actively aiming")]
        public Transform DefaultWeaponPosition;

        [Tooltip("Position for weapons when aiming")]
        public Transform AimingWeaponPosition;

        [Tooltip("Position for inactive weapons")]
        public Transform DownWeaponPosition;

        [Header("Weapon Bob")]
        [Tooltip("Frequency at which the weapon will move around in the screen when the player is in movement")]
        public float BobFrequency = 10f;

        [Tooltip("How fast the weapon bob is applied, the bigger value the fastest")]
        public float BobSharpness = 10f;

        [Tooltip("Distance the weapon bobs when not aiming")]
        public float DefaultBobAmount = 0.05f;

        [Tooltip("Distance the weapon bobs when aiming")]
        public float AimingBobAmount = 0.02f;

        [Header("Weapon Recoil")]
        [Tooltip("This will affect how fast the recoil moves the weapon, the bigger the value, the fastest")]
        public float RecoilSharpness = 50f;

        [Tooltip("Maximum distance the recoil can affect the weapon")]
        public float MaxRecoilDistance = 0.5f;

        [Tooltip("How fast the weapon goes back to it's original position after the recoil is finished")]
        public float RecoilRestitutionSharpness = 10f;

        [Header("Misc")]
        [Tooltip("Speed at which the aiming animation is played")]
        public float AimingAnimationSpeed = 10f;

        [Tooltip("Field of view when not aiming")]
        public float DefaultFov = 60f;

        [Tooltip("Portion of the regular FOV to apply to the weapon camera")]
        public float WeaponFovMultiplier = 1f;

        [Tooltip("Delay before switching weapon a second time, to avoid receiving multiple inputs from mouse wheel")]
        public float WeaponSwitchDelay = 1f;

        [Tooltip("Layer to set FPS weapon gameObjects to")]
        public LayerMask FpsWeaponLayer;

        public bool IsAiming { get; private set; }
        public bool IsPointingAtEnemy { get; private set; }

        public NetworkVariable<int> ActiveWeaponIndex = new NetworkVariable<int>(-1, NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);

        private bool IsBot;
        private BotBehaviour m_BotBehaviour;

        public UnityAction<WeaponController> OnSwitchedToWeapon;
        public UnityAction<WeaponController, int> OnAddedWeapon;
        public UnityAction<WeaponController, int> OnRemovedWeapon;

        WeaponController[] m_WeaponSlots = new WeaponController[9]; // 9 available weapon slots
        PlayerInputHandler m_InputHandler;
        PlayerCharacterController m_PlayerCharacterController;
        float m_WeaponBobFactor;
        Vector3 m_LastCharacterPosition;
        Vector3 m_WeaponMainLocalPosition;
        Vector3 m_WeaponBobLocalPosition;
        Vector3 m_WeaponRecoilLocalPosition;
        Vector3 m_AccumulatedRecoil;
        float m_TimeStartedWeaponSwitch;
        WeaponSwitchState m_WeaponSwitchState;
        int m_WeaponSwitchNewWeaponIndex;

        public override void OnNetworkSpawn()
        {
            ActiveWeaponIndex.OnValueChanged += (oldValue, newValue) =>
            {
                UpdateWeaponVisibility(newValue);
            };

            // If we are joining late, run once to catch up to current state
            if (ActiveWeaponIndex.Value != -1)
            {
                UpdateWeaponVisibility(ActiveWeaponIndex.Value);
            }

            if (!IsOwner) return;

            m_WeaponSwitchState = WeaponSwitchState.Down;

            m_InputHandler = GetComponent<PlayerInputHandler>();
            DebugUtility.HandleErrorIfNullGetComponent<PlayerInputHandler, PlayerWeaponsManager>(m_InputHandler, this, gameObject);

            m_PlayerCharacterController = GetComponent<PlayerCharacterController>();
            DebugUtility.HandleErrorIfNullGetComponent<PlayerCharacterController, PlayerWeaponsManager>(m_PlayerCharacterController, this, gameObject);

            SetFov(DefaultFov);

            OnSwitchedToWeapon += OnWeaponSwitched;

            // Add starting weapons
            Debug.Log("Client: Time to spawn my weapon(s)!");
            RequestStartingWeaponsServerRpc();

            if (IsOwner)
            {
                IsBot = m_PlayerCharacterController.IsBot;
                if (IsBot)
                {
                    m_BotBehaviour = GetComponent<BotBehaviour>();
                }
            }
        }

        private void UpdateWeaponVisibility(int newIndex)
        {
            for (int i = 0; i < m_WeaponSlots.Length; i++)
            {
                if (m_WeaponSlots[i] != null)
                {
                    bool shouldBeVisible = (i == newIndex);
                    m_WeaponSlots[i].ShowWeapon(shouldBeVisible);

                    if (shouldBeVisible && IsOwner)
                    {
                        OnSwitchedToWeapon?.Invoke(m_WeaponSlots[i]);
                    }
                }
            }
        }

        void Update()
        {
            if (!IsOwner) return;

            WeaponController activeWeapon = GetActiveWeapon();

            if (activeWeapon != null && activeWeapon.IsReloading)
                return;

            if (activeWeapon != null && m_WeaponSwitchState == WeaponSwitchState.Up)
            {
                if (!activeWeapon.AutomaticReload && GetReloadButtonDown() && activeWeapon.CurrentAmmoRatio < 1.0f)
                {
                    IsAiming = false;
                    activeWeapon.StartReloadAnimation();
                    return;
                }

                // Handle aiming down sights
                IsAiming = GetAimInputHeld();

                // Handle shooting
                bool hasFired = activeWeapon.HandleShootInputs(
                    GetFireInputDown(),
                    GetFireInputHeld(),
                    GetFireInputReleased());

                if (hasFired)
                {
                    m_AccumulatedRecoil += Vector3.back * activeWeapon.RecoilForce;
                    m_AccumulatedRecoil = Vector3.ClampMagnitude(m_AccumulatedRecoil, MaxRecoilDistance);
                }
            }

            // Weapon switch handling
            if (!IsAiming &&
                (activeWeapon == null || !activeWeapon.IsCharging) &&
                (m_WeaponSwitchState == WeaponSwitchState.Up || m_WeaponSwitchState == WeaponSwitchState.Down))
            {
                int switchWeaponInput = GetSwitchWeaponInput();
                if (switchWeaponInput != 0)
                {
                    bool switchUp = switchWeaponInput > 0;
                    SwitchWeapon(switchUp);
                }
                else
                {
                    switchWeaponInput = GetSelectWeaponInput();
                    if (switchWeaponInput != 0)
                    {
                        int targetSlotIndex = switchWeaponInput - 1;

                        // Only execute the switch if the player actually HAS a weapon in that slot
                        if (GetWeaponAtSlotIndex(targetSlotIndex) != null)
                        {
                            SwitchToWeaponIndex(targetSlotIndex);
                        }
                    }
                }
            }

            // Pointing at enemy handling
            IsPointingAtEnemy = false;
            if (activeWeapon)
            {
                if (Physics.Raycast(WeaponCamera.transform.position, WeaponCamera.transform.forward, out RaycastHit hit,
                    1000, -1, QueryTriggerInteraction.Ignore))
                {
                    if (hit.collider.GetComponentInParent<Health>() != null)
                    {
                        IsPointingAtEnemy = true;
                    }
                }
            }
        }

        void LateUpdate()
        {
            if (!IsOwner) return;

            UpdateWeaponAiming();
            UpdateWeaponBob();
            UpdateWeaponRecoil();
            UpdateWeaponSwitching();

            WeaponParentSocket.localPosition =
                m_WeaponMainLocalPosition + m_WeaponBobLocalPosition + m_WeaponRecoilLocalPosition;
        }

        public void SetFov(float fov)
        {
            if (!IsOwner) return;

            m_PlayerCharacterController.PlayerCamera.fieldOfView = fov;
            WeaponCamera.fieldOfView = fov * WeaponFovMultiplier;
        }

        public void SwitchWeapon(bool ascendingOrder)
        {
            if (!IsOwner) return;

            int newWeaponIndex = -1;
            int closestSlotDistance = m_WeaponSlots.Length;
            for (int i = 0; i < m_WeaponSlots.Length; i++)
            {
                if (i != ActiveWeaponIndex.Value && GetWeaponAtSlotIndex(i) != null)
                {
                    int distanceToActiveIndex = GetDistanceBetweenWeaponSlots(ActiveWeaponIndex.Value, i, ascendingOrder);

                    if (distanceToActiveIndex < closestSlotDistance)
                    {
                        closestSlotDistance = distanceToActiveIndex;
                        newWeaponIndex = i;
                    }
                }
            }

            SwitchToWeaponIndex(newWeaponIndex);
        }

        public void SwitchToWeaponIndex(int newWeaponIndex, bool force = false)
        {
            if (!IsOwner) return;

            if (force || (newWeaponIndex != ActiveWeaponIndex.Value && newWeaponIndex >= 0))
            {
                m_WeaponSwitchNewWeaponIndex = newWeaponIndex;
                m_TimeStartedWeaponSwitch = Time.time;

                if (GetActiveWeapon() == null)
                {
                    m_WeaponMainLocalPosition = DownWeaponPosition.localPosition;
                    m_WeaponSwitchState = WeaponSwitchState.PutUpNew;
                    ActiveWeaponIndex.Value = m_WeaponSwitchNewWeaponIndex;

                    WeaponController newWeapon = GetWeaponAtSlotIndex(m_WeaponSwitchNewWeaponIndex);
                    OnSwitchedToWeapon?.Invoke(newWeapon);
                }
                else
                {
                    m_WeaponSwitchState = WeaponSwitchState.PutDownPrevious;
                }
            }
        }

        public WeaponController HasWeapon(WeaponController weaponPrefab)
        {
            if (!IsOwner) return null;

            for (var index = 0; index < m_WeaponSlots.Length; index++)
            {
                var w = m_WeaponSlots[index];
                if (w != null && w.SourcePrefab == weaponPrefab.gameObject)
                {
                    return w;
                }
            }

            return null;
        }

        void UpdateWeaponAiming()
        {
            if (!IsOwner) return;

            if (m_WeaponSwitchState == WeaponSwitchState.Up)
            {
                WeaponController activeWeapon = GetActiveWeapon();

                // If there is no weapon, or it's the voting gun/tool, force aiming to FALSE
                if (activeWeapon == null || activeWeapon.IsVotingGun)
                {
                    IsAiming = false;
                    return;
                }

                if (IsAiming && activeWeapon)
                {
                    m_WeaponMainLocalPosition = Vector3.Lerp(m_WeaponMainLocalPosition,
                        AimingWeaponPosition.localPosition + activeWeapon.AimOffset,
                        AimingAnimationSpeed * Time.deltaTime);
                    SetFov(Mathf.Lerp(m_PlayerCharacterController.PlayerCamera.fieldOfView,
                        activeWeapon.AimZoomRatio * DefaultFov, AimingAnimationSpeed * Time.deltaTime));
                }
                else
                {
                    m_WeaponMainLocalPosition = Vector3.Lerp(m_WeaponMainLocalPosition,
                        DefaultWeaponPosition.localPosition, AimingAnimationSpeed * Time.deltaTime);
                    SetFov(Mathf.Lerp(m_PlayerCharacterController.PlayerCamera.fieldOfView, DefaultFov,
                        AimingAnimationSpeed * Time.deltaTime));
                }
            }
        }

        void UpdateWeaponBob()
        {
            if (!IsOwner) return;

            if (Time.deltaTime > 0f)
            {
                Vector3 playerCharacterVelocity =
                    (m_PlayerCharacterController.transform.position - m_LastCharacterPosition) / Time.deltaTime;

                float characterMovementFactor = 0f;
                if (m_PlayerCharacterController.IsGrounded)
                {
                    characterMovementFactor =
                        Mathf.Clamp01(playerCharacterVelocity.magnitude /
                                      (m_PlayerCharacterController.MaxSpeedOnGround *
                                       m_PlayerCharacterController.SprintSpeedModifier));
                }

                m_WeaponBobFactor =
                    Mathf.Lerp(m_WeaponBobFactor, characterMovementFactor, BobSharpness * Time.deltaTime);

                float bobAmount = IsAiming ? AimingBobAmount : DefaultBobAmount;
                float frequency = BobFrequency;
                float hBobValue = Mathf.Sin(Time.time * frequency) * bobAmount * m_WeaponBobFactor;
                float vBobValue = ((Mathf.Sin(Time.time * frequency * 2f) * 0.5f) + 0.5f) * bobAmount *
                    m_WeaponBobFactor;

                m_WeaponBobLocalPosition.x = hBobValue;
                m_WeaponBobLocalPosition.y = Mathf.Abs(vBobValue);

                m_LastCharacterPosition = m_PlayerCharacterController.transform.position;
            }
        }

        void UpdateWeaponRecoil()
        {
            if (!IsOwner) return;

            if (m_WeaponRecoilLocalPosition.z >= m_AccumulatedRecoil.z * 0.99f)
            {
                m_WeaponRecoilLocalPosition = Vector3.Lerp(m_WeaponRecoilLocalPosition, m_AccumulatedRecoil,
                    RecoilSharpness * Time.deltaTime);
            }
            else
            {
                m_WeaponRecoilLocalPosition = Vector3.Lerp(m_WeaponRecoilLocalPosition, Vector3.zero,
                    RecoilRestitutionSharpness * Time.deltaTime);
                m_AccumulatedRecoil = m_WeaponRecoilLocalPosition;
            }
        }

        void UpdateWeaponSwitching()
        {
            if (!IsOwner) return;

            float switchingTimeFactor = 0f;
            if (WeaponSwitchDelay == 0f)
            {
                switchingTimeFactor = 1f;
            }
            else
            {
                switchingTimeFactor = Mathf.Clamp01((Time.time - m_TimeStartedWeaponSwitch) / WeaponSwitchDelay);
            }

            if (switchingTimeFactor >= 1f)
            {
                if (m_WeaponSwitchState == WeaponSwitchState.PutDownPrevious)
                {
                    WeaponController oldWeapon = GetWeaponAtSlotIndex(ActiveWeaponIndex.Value);
                    if (oldWeapon != null)
                    {
                        oldWeapon.ShowWeapon(false);
                    }

                    ActiveWeaponIndex.Value = m_WeaponSwitchNewWeaponIndex;
                    switchingTimeFactor = 0f;

                    WeaponController newWeapon = GetWeaponAtSlotIndex(ActiveWeaponIndex.Value);
                    OnSwitchedToWeapon?.Invoke(newWeapon);

                    if (newWeapon)
                    {
                        m_TimeStartedWeaponSwitch = Time.time;
                        m_WeaponSwitchState = WeaponSwitchState.PutUpNew;
                    }
                    else
                    {
                        m_WeaponSwitchState = WeaponSwitchState.Down;
                    }
                }
                else if (m_WeaponSwitchState == WeaponSwitchState.PutUpNew)
                {
                    m_WeaponSwitchState = WeaponSwitchState.Up;
                }
            }

            if (m_WeaponSwitchState == WeaponSwitchState.PutDownPrevious)
            {
                m_WeaponMainLocalPosition = Vector3.Lerp(DefaultWeaponPosition.localPosition,
                    DownWeaponPosition.localPosition, switchingTimeFactor);
            }
            else if (m_WeaponSwitchState == WeaponSwitchState.PutUpNew)
            {
                m_WeaponMainLocalPosition = Vector3.Lerp(DownWeaponPosition.localPosition,
                    DefaultWeaponPosition.localPosition, switchingTimeFactor);
            }
        }

        public bool AddWeapon(WeaponController weaponPrefab)
        {
            if (!IsServer) return false;

            if (HasWeapon(weaponPrefab) != null) return false;

            for (int i = 0; i < m_WeaponSlots.Length; i++)
            {
                if (m_WeaponSlots[i] == null)
                {
                    WeaponController weaponInstance = Instantiate(weaponPrefab);
                    weaponInstance.SourcePrefab = weaponPrefab.gameObject;

                    NetworkObject weaponNetObj = weaponInstance.GetComponent<NetworkObject>();
                    weaponNetObj.SpawnWithOwnership(OwnerClientId);

                    weaponInstance.transform.SetParent(this.transform);

                    SetupWeaponClientRpc(weaponNetObj.NetworkObjectId, i);
                    return true;
                }
            }
            return false;
        }

        [Rpc(SendTo.ClientsAndHost)]
        void SetupWeaponClientRpc(ulong weaponNetId, int slotIndex)
        {
            StartCoroutine(WaitForWeaponAndSetup(weaponNetId, slotIndex));
        }

        IEnumerator WaitForWeaponAndSetup(ulong weaponNetId, int slotIndex)
        {
            float timeout = 2.0f;
            float timer = 0;

            while (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.ContainsKey(weaponNetId) && timer < timeout)
            {
                timer += Time.deltaTime;
                yield return null;
            }

            if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(weaponNetId, out NetworkObject netObj))
            {
                WeaponController weaponInstance = netObj.GetComponent<WeaponController>();

                WeaponSocketFollower follower = weaponInstance.gameObject.GetComponent<WeaponSocketFollower>();
                if (follower == null) follower = weaponInstance.gameObject.AddComponent<WeaponSocketFollower>();
                follower.TargetSocket = WeaponParentSocket;

                int targetLayerIndex;
                if (IsOwner)
                {
                    targetLayerIndex = Mathf.RoundToInt(Mathf.Log(FpsWeaponLayer.value, 2));
                }
                else
                {
                    targetLayerIndex = LayerMask.NameToLayer("Default");
                    if (targetLayerIndex == -1) targetLayerIndex = 0;
                }

                foreach (Transform t in weaponInstance.gameObject.GetComponentsInChildren<Transform>(true))
                {
                    t.gameObject.layer = targetLayerIndex;
                }

                weaponInstance.Owner = gameObject;

                m_WeaponSlots[slotIndex] = weaponInstance;
                OnAddedWeapon?.Invoke(weaponInstance, slotIndex);

                if (ActiveWeaponIndex.Value != -1)
                {
                    UpdateWeaponVisibility(ActiveWeaponIndex.Value);
                }
                else
                {
                    weaponInstance.ShowWeapon(slotIndex == 0);
                }

                if (IsOwner && (GetActiveWeapon() == null || ActiveWeaponIndex.Value == slotIndex))
                {
                    SwitchToWeaponIndex(slotIndex, true);
                }
            }
        }

        [Rpc(SendTo.Server)]
        void RequestStartingWeaponsServerRpc()
        {
            Debug.Log("Server: Request to spawn weapons(s) for a client received!");
            foreach (var weaponPrefab in StartingWeapons)
            {
                AddWeapon(weaponPrefab);
            }
        }

        public bool RemoveWeapon(WeaponController weaponInstance)
        {
            if (!IsServer) return false;

            for (int i = 0; i < m_WeaponSlots.Length; i++)
            {
                if (m_WeaponSlots[i] == weaponInstance)
                {
                    m_WeaponSlots[i] = null;
                    CleanupWeaponClientRpc(i);

                    if (weaponInstance.TryGetComponent(out NetworkObject netObj))
                    {
                        netObj.Despawn(true);
                    }

                    return true;
                }
            }
            return false;
        }

        [Rpc(SendTo.Everyone)]
        void CleanupWeaponClientRpc(int slotIndex)
        {
            WeaponController weaponInstance = m_WeaponSlots[slotIndex];

            if (weaponInstance != null)
            {
                m_WeaponSlots[slotIndex] = null;
                OnRemovedWeapon?.Invoke(weaponInstance, slotIndex);

                if (IsOwner && slotIndex == ActiveWeaponIndex.Value)
                {
                    SwitchWeapon(true);
                }
            }
        }

        public WeaponController GetActiveWeapon()
        {
            return GetWeaponAtSlotIndex(ActiveWeaponIndex.Value);
        }

        public WeaponController GetWeaponAtSlotIndex(int index)
        {
            if (index >= 0 && index < m_WeaponSlots.Length)
            {
                return m_WeaponSlots[index];
            }
            return null;
        }

        int GetDistanceBetweenWeaponSlots(int fromSlotIndex, int toSlotIndex, bool ascendingOrder)
        {
            int distanceBetweenSlots = 0;

            if (ascendingOrder)
            {
                distanceBetweenSlots = toSlotIndex - fromSlotIndex;
            }
            else
            {
                distanceBetweenSlots = -1 * (toSlotIndex - fromSlotIndex);
            }

            if (distanceBetweenSlots < 0)
            {
                distanceBetweenSlots = m_WeaponSlots.Length + distanceBetweenSlots;
            }

            return distanceBetweenSlots;
        }

        void OnWeaponSwitched(WeaponController newWeapon)
        {
            if (newWeapon != null)
            {
                newWeapon.ShowWeapon(true);
            }
        }

        public void ResetToStartingWeapon()
        {
            if (IsOwner)
            {
                ActiveWeaponIndex.Value = 0;
            }

            for (int i = m_WeaponSlots.Length - 1; i >= 0; i--)
            {
                WeaponController weapon = m_WeaponSlots[i];
                if (weapon == null) continue;
                if (i == 0) continue;

                OnRemovedWeapon?.Invoke(weapon, i);

                if (IsServer)
                {
                    CleanupWeaponClientRpc(i);
                    var netObj = weapon.GetComponent<Unity.Netcode.NetworkObject>();
                    if (netObj != null && netObj.IsSpawned)
                    {
                        netObj.Despawn(true);
                    }
                    else
                    {
                        Destroy(weapon.gameObject);
                    }
                }
                m_WeaponSlots[i] = null;
            }
        }

        public bool GetFireInputHeld()
        {
            if (IsBot && m_BotBehaviour != null)
            {
                bool botValue = m_BotBehaviour.GetFireInputHeld();
                if (botValue) Debug.Log($"[WeaponManager Bot] GetFireInputHeld = {botValue}");
                return botValue;
            }

            return m_InputHandler != null && m_InputHandler.GetFireInputHeld();
        }

        public bool GetFireInputDown()
        {
            if (IsBot && m_BotBehaviour != null)
            {
                bool botValue = m_BotBehaviour.GetFireInputDown();
                if (botValue) Debug.Log($"[WeaponManager Bot] GetFireInputDown = {botValue}");
                return botValue;
            }

            return m_InputHandler != null && m_InputHandler.GetFireInputDown();
        }

        public bool GetFireInputReleased()
        {
            if (IsBot && m_BotBehaviour != null)
            {
                return m_BotBehaviour.GetFireInputReleased();
            }

            return m_InputHandler != null && m_InputHandler.GetFireInputReleased();
        }

        public bool GetAimInputHeld()
        {
            if (IsBot && m_BotBehaviour != null)
                return m_BotBehaviour.GetAimInputHeld();

            return m_InputHandler != null && m_InputHandler.GetAimInputHeld();
        }

        public bool GetReloadButtonDown()
        {
            if (IsBot && m_BotBehaviour != null)
                return m_BotBehaviour.GetReloadButtonDown();

            return m_InputHandler != null && m_InputHandler.GetReloadButtonDown();
        }

        public int GetSwitchWeaponInput()
        {
            if (IsBot && m_BotBehaviour != null)
                return m_BotBehaviour.GetSwitchWeaponInput();

            return m_InputHandler != null ? m_InputHandler.GetSwitchWeaponInput() : 0;
        }

        public int GetSelectWeaponInput()
        {
            if (IsBot && m_BotBehaviour != null)
                return m_BotBehaviour.GetSelectWeaponInput();

            return m_InputHandler != null ? m_InputHandler.GetSelectWeaponInput() : 0;
        }
    }
}