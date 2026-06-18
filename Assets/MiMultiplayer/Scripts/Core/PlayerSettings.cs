using TMPro;
using Unity.Collections;
using Unity.Netcode;
using Unity.Services.Lobbies.Models;
using UnityEngine;

namespace Unity.FPS.Core
{
    public class PlayerSettings : NetworkBehaviour
    {
        [Header("UI & Name Elements")]
        [SerializeField] private GameObject playerNamePanel;
        [SerializeField] private TextMeshProUGUI playerName;

        [Header("Cosmetics & Mesh Container")]
        [Tooltip("The parent container object holding all the robot limb parts.")]
        [SerializeField] private GameObject playerMesh;

        [Header("Targeting Settings")]
        [Tooltip("The exact name filter of the material we want to change (e.g., M_AtlasOffset). Everything else will be ignored.")]
        [SerializeField] private string paintMaterialName = "M_AtlasOffset";

        [Header("Simplified Team Materials")]
        [SerializeField] private Material blueTeamMaterial;
        [SerializeField] private Material redTeamMaterial;
        [SerializeField] private Material damageFlashMaterial;

        // Synchronized Network Variables
        private NetworkVariable<FixedString128Bytes> networkPlayerName =
            new NetworkVariable<FixedString128Bytes>("Player", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private NetworkVariable<int> networkTeamIndex =
            new NetworkVariable<int>(-1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public int TeamIndex => networkTeamIndex.Value;

        public System.Action<int> OnTeamAssigned;
        public System.Action OnProfileReady;

        [Header("Damage Flash Settings")]
        [SerializeField] private FlashController flashController;

        // NEW: Structure to back up the original materials map of the prefab asset safely
        private struct RendererMaterialBackup
        {
            public SkinnedMeshRenderer renderer;
            public Material[] originalSharedMaterials;
        }
        private RendererMaterialBackup[] m_RendererBackups;

        private void Awake()
        {
            CacheOriginalMaterials();

            if (flashController != null)
            {
                flashController.Initialize(() => ApplyTeamColor(networkTeamIndex.Value));
            }
        }

        // NEW: Stores a clean copy of the original layout before anything gets swapped or flashed
        private void CacheOriginalMaterials()
        {
            if (playerMesh == null) return;

            SkinnedMeshRenderer[] renderers = playerMesh.GetComponentsInChildren<SkinnedMeshRenderer>();
            m_RendererBackups = new RendererMaterialBackup[renderers.Length];

            for (int i = 0; i < renderers.Length; i++)
            {
                m_RendererBackups[i] = new RendererMaterialBackup
                {
                    renderer = renderers[i],
                    // Store the shared asset array directly so it survives run-time overwrites
                    originalSharedMaterials = renderers[i].sharedMaterials
                };
            }
        }

        public override void OnNetworkSpawn()
        {
            networkPlayerName.OnValueChanged += UpdateNameUI;
            networkTeamIndex.OnValueChanged += OnTeamIndexChanged;

            playerName.text = networkPlayerName.Value.ToString();
            if (networkTeamIndex.Value != -1)
            {
                ApplyTeamColor(networkTeamIndex.Value);
                if (IsOwner) OnTeamAssigned?.Invoke(networkTeamIndex.Value);
            }

            if (IsOwner)
            {
                playerNamePanel.SetActive(false);
                StartCoroutine(InitializeProfileFromLobby());
            }
        }

        private System.Collections.IEnumerator InitializeProfileFromLobby()
        {
            Lobby activeLobby = null;
            int maxAttempts = 10;
            int attempts = 0;

            while (activeLobby == null && attempts < maxAttempts)
            {
                activeLobby = LobbyManager.Instance.GetJoinedLobby();
                if (activeLobby == null)
                {
                    attempts++;
                    yield return new WaitForSeconds(0.2f);
                }
            }

            if (activeLobby != null)
            {
                string localPlayerId = Unity.Services.Authentication.AuthenticationService.Instance.PlayerId;
                Player localLobbyPlayerData = activeLobby.Players.Find(p => p.Id == localPlayerId);

                if (localLobbyPlayerData != null)
                {
                    string fetchedName = localLobbyPlayerData.Data.ContainsKey(LobbyManager.KEY_PLAYER_NAME)
                        ? localLobbyPlayerData.Data[LobbyManager.KEY_PLAYER_NAME].Value : "Player";

                    string playerTeam = localLobbyPlayerData.Data.ContainsKey(LobbyManager.KEY_PLAYER_TEAM)
                        ? localLobbyPlayerData.Data[LobbyManager.KEY_PLAYER_TEAM].Value : "Unassigned";

                    int teamIndex = -1;
                    if (playerTeam == "Blue") teamIndex = 0;
                    else if (playerTeam == "Red") teamIndex = 1;

                    SubmitIdentityProfileServerRpc(fetchedName, teamIndex);
                }
            }
        }

        [Rpc(SendTo.Server)]
        private void SubmitIdentityProfileServerRpc(string requestedName, int requestedTeamIndex)
        {
            networkPlayerName.Value = requestedName;
            networkTeamIndex.Value = requestedTeamIndex;
        }

        private void UpdateNameUI(FixedString128Bytes previousValue, FixedString128Bytes newValue)
        {
            playerName.text = newValue.ToString();
        }

        private void OnTeamIndexChanged(int previousValue, int newValue)
        {
            ApplyTeamColor(newValue);
            if (newValue != -1) OnProfileReady?.Invoke();
            if (newValue != -1 && IsOwner) OnTeamAssigned?.Invoke(newValue);
        }

        // FIXED: Rebuilds armor paint plates by looking at our permanent backup cache
        private void ApplyTeamColor(int index)
        {
            if (index < 0 || m_RendererBackups == null) return;

            Material targetMaterial = (index == 0) ? blueTeamMaterial : redTeamMaterial;
            if (targetMaterial == null) return;

            foreach (var backup in m_RendererBackups)
            {
                if (backup.renderer == null) continue;

                Material[] currentOriginalMats = backup.originalSharedMaterials;
                Material[] newMaterials = new Material[currentOriginalMats.Length];

                for (int i = 0; i < currentOriginalMats.Length; i++)
                {
                    if (currentOriginalMats[i] != null && currentOriginalMats[i].name.Contains(paintMaterialName))
                    {
                        newMaterials[i] = targetMaterial;
                    }
                    else
                    {
                        // Safely restores eyes, decals, and claws back to asset defaults
                        newMaterials[i] = currentOriginalMats[i];
                    }
                }
                backup.renderer.materials = newMaterials;
            }
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            networkPlayerName.OnValueChanged -= UpdateNameUI;
            networkTeamIndex.OnValueChanged -= OnTeamIndexChanged;
        }

        public void FlashWhite()
        {
            if (flashController != null)
            {
                flashController.FlashWhite();
            }
            else
            {
                Debug.Log("FlashWhite not found!");
            }
        }
    }
}