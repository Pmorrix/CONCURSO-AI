using Unity.Collections;
using Unity.FPS.Core;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using Unity.FPS.UI;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class VotingGunWeaponHandler : NetworkBehaviour
{
    [Header("Weapon Tracking")]
    [Tooltip("The specific name of your voting weapon to check against")]
    [SerializeField] private string votingWeaponName = "VotingRemote";

    [Header("Weapon Tracking")]
    [SerializeField] private AudioClip voteBeepSFX;

    private PlayerWeaponsManager m_WeaponsManager;
    private PlayerSettings m_LocalPlayerSettings;
    private NotificationHUDManager m_NotificationManager;

    // CHANGED: We no longer track a single outline here, the Target Tracker handles its own collection
    private FlagCarrierTracker m_CurrentTargetTracker;

    // Hex Color codes matching Team UI styles
    private const string HexBlue = "#3498db";
    private const string HexRed = "#e74c3c";

    void Awake()
    {
        m_WeaponsManager = GetComponent<PlayerWeaponsManager>();
        m_LocalPlayerSettings = GetComponent<PlayerSettings>();
    }

    void Start()
    {
        m_NotificationManager = FindAnyObjectByType<NotificationHUDManager>();

        if (m_NotificationManager == null)
        {
            Debug.LogWarning("VotingGunOutlineHandler: Could not find NotificationHUDManager in the scene hierarchy.");
        }
    }

    void Update()
    {
        if (!IsOwner || m_WeaponsManager == null) return;

        WeaponController activeWeapon = m_WeaponsManager.GetActiveWeapon();
        if (activeWeapon == null || activeWeapon.WeaponName != votingWeaponName)
        {
            ClearCurrentOutline();
            return;
        }

        Transform camTransform = m_WeaponsManager.WeaponCamera.transform;

        if (Physics.Raycast(camTransform.position, camTransform.forward, out RaycastHit hit, 1000, -1, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.GetComponentInParent<Health>() != null)
            {
                FlagCarrierTracker targetTracker = hit.collider.GetComponentInParent<Health>().GetComponent<FlagCarrierTracker>();

                if (targetTracker != null)
                {
                    PlayerSettings targetSettings = targetTracker.GetComponent<PlayerSettings>();
                    if (targetSettings != null && m_LocalPlayerSettings != null)
                    {
                        if (targetSettings.TeamIndex == m_LocalPlayerSettings.TeamIndex)
                        {
                            ClearCurrentOutline();
                            return;
                        }
                    }

                    // FIXED: If looking at a completely brand-new target enemy
                    if (targetTracker != m_CurrentTargetTracker)
                    {
                        ClearCurrentOutline();
                        m_CurrentTargetTracker = targetTracker;

                        // Instruct the targeted robot tracker to flash white across all body nodes
                        m_CurrentTargetTracker.SetVotingHoverOutline(true);
                    }

                    HandleVotingInputs(targetTracker, targetSettings);
                    return;
                }
            }
        }

        ClearCurrentOutline();
    }

    private string GetShooterName()
    {
        string shooterName = "LocalPlayer";
        if (m_LocalPlayerSettings != null)
        {
            var nameField = typeof(PlayerSettings).GetField("networkPlayerName",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (nameField != null)
            {
                var netVar = nameField.GetValue(m_LocalPlayerSettings) as NetworkVariable<FixedString128Bytes>;
                if (netVar != null) shooterName = netVar.Value.ToString();
            }
        }
        return shooterName;
    }

    private string GetTargetName(PlayerSettings targetSettings)
    {
        string targetName = "Player";
        if (targetSettings != null)
        {
            var nameField = typeof(PlayerSettings).GetField("networkPlayerName",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (nameField != null)
            {
                var netVar = nameField.GetValue(targetSettings) as NetworkVariable<FixedString128Bytes>;
                if (netVar != null) targetName = netVar.Value.ToString();
            }
        }
        return targetName;
    }

    private void HandleVotingInputs(FlagCarrierTracker targetedEnemy, PlayerSettings targetSettings)
    {
        if (m_NotificationManager == null) return;

        string targetName = GetTargetName(targetSettings);
        Mouse currentMouse = Mouse.current;
        if (currentMouse == null) return;

        string targetTeamHex = (targetSettings != null && targetSettings.TeamIndex == 0) ? HexBlue : HexRed;
        string coloredTargetName = $"<color={targetTeamHex}>{targetName}</color>";

        if (currentMouse.leftButton.wasPressedThisFrame)
        {
            AudioUtility.CreateSFX(voteBeepSFX, Vector3.zero, AudioUtility.AudioGroups.HUDObjective, 0f);

            string noticeMessage = $"You've voted that {coloredTargetName} is a <b>HUMAN</b>";
            m_NotificationManager.CreateNotification(noticeMessage);
            Debug.Log($"[VOTE LOG] Left-Clicked: {noticeMessage}");

            string activeShooterName = GetShooterName();
            SubmitVoteServerRpc(activeShooterName, targetName, "Human");
        }
        else if (currentMouse.rightButton.wasPressedThisFrame)
        {
            AudioUtility.CreateSFX(voteBeepSFX, Vector3.zero, AudioUtility.AudioGroups.HUDObjective, 0f);

            string noticeMessage = $"You've voted that {coloredTargetName} is a <b>BOT</b>";
            m_NotificationManager.CreateNotification(noticeMessage);
            Debug.Log($"[VOTE LOG] Right-Clicked: {noticeMessage}");

            string activeShooterName = GetShooterName();
            SubmitVoteServerRpc(activeShooterName, targetName, "Bot");
        }
    }

    [Rpc(SendTo.Server)]
    private void SubmitVoteServerRpc(string voter, string target, string guessType)
    {
        if (MatchVotingDataLogger.Instance != null)
        {
            MatchVotingDataLogger.Instance.RecordVote(voter, target, guessType);
        }
    }

    // Cleans up outline by calling the updated tracker cleanup API method
    private void ClearCurrentOutline()
    {
        if (m_CurrentTargetTracker != null)
        {
            // Turn off white highlight override, returning limbs to default team color checks
            m_CurrentTargetTracker.SetVotingHoverOutline(false);
        }
        m_CurrentTargetTracker = null;
    }

    private void OnDisable()
    {
        ClearCurrentOutline();
    }
}