using TMPro;
using Unity.Services.Authentication;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.UI;

namespace Unity.FPS.Core
{
    public class LobbyPlayerSingleUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI playerNameText;
        [SerializeField] private TextMeshProUGUI teamText;
        [SerializeField] private Button kickButton;

        // Custom styling options for the Host player assignment
        [Header("Host Visual Highlights")]
        [SerializeField] private Color hostNameColor = new Color(1f, 0.5f, 0f); // Bright Orange
        [SerializeField] private Color defaultNameColor = Color.white;

        private Player player;

        private void Awake()
        {
            if (kickButton != null)
            {
                kickButton.onClick.AddListener(() =>
                {
                    if (player != null)
                    {
                        LobbyManager.Instance.KickPlayer(player.Id);
                    }
                });
            }
        }

        // Added 'isHost' check to decouple UI updates from structural timing states
        public void UpdatePlayer(Player player, bool isHost)
        {
            this.player = player;

            string nameVal = player.Data.ContainsKey(LobbyManager.KEY_PLAYER_NAME)
                ? player.Data[LobbyManager.KEY_PLAYER_NAME].Value : "Unknown Player";

            string teamVal = player.Data.ContainsKey(LobbyManager.KEY_PLAYER_TEAM)
                ? player.Data[LobbyManager.KEY_PLAYER_TEAM].Value : "Unassigned";

            // =========================================================================
            // DYNAMIC HOST NAME COLOR HIGHLIGHTING
            // =========================================================================
            if (isHost)
            {
                // Appends a clean label tag and overrides text vertex colors cleanly
                playerNameText.text = $"{nameVal} <size=80%><b>(HOST)</b></size>";
                playerNameText.color = hostNameColor;
            }
            else
            {
                playerNameText.text = nameVal;
                playerNameText.color = defaultNameColor;
            }

            // Convert "Unassigned" into a clean "?" visual representation
            if (teamVal == "Unassigned")
            {
                teamText.text = "?";
                teamText.color = Color.gray;
            }
            else
            {
                teamText.text = $"<b>{teamVal.ToUpper()}</b>";
                teamText.color = (teamVal == "Blue") ? Color.blue : Color.red;
            }
        }

        public void SetKickButtonVisibility(bool visible)
        {
            if (kickButton != null) kickButton.gameObject.SetActive(visible);
        }
    }
}