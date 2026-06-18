using System;
using System.Collections.Generic;
using TMPro;
using Unity.FPS.Core;
using Unity.Netcode;
using UnityEngine;
using Unity.FPS.Game;

namespace Unity.FPS.Gameplay
{
    public class GameScoreManager : NetworkBehaviour
    {
        public static GameScoreManager Instance { get; private set; }

        [Header("Points needed for team to win")]
        [SerializeField] private int pointsToWin = 3;

        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI blueScoreText;
        [SerializeField] private TextMeshProUGUI redScoreText;

        [Header("Gain Point Sound")]
        [SerializeField] private AudioClip gainPointSFX;

        [Header("Match End Sound")]
        [SerializeField] private AudioClip matchEndSFX;

        [Header("Match Transition Settings")]
        [Tooltip("Time in seconds to wait after a team wins before transitioning scenes.")]
        [SerializeField] private float matchEndDelay = 2.0f;

        public NetworkVariable<int> BlueTeamScore = new NetworkVariable<int>(0);
        public NetworkVariable<int> RedTeamScore = new NetworkVariable<int>(0);

        // Other scripts can subscribe to this event to know exactly when the match finishes
        public event Action<string> OnMatchEnded;

        private bool m_MatchEnding = false;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"[GameScoreManager] Duplicate instance detected on {gameObject.name}. Destroying extra.");
                Destroy(gameObject);
                return;
            }
            Instance = this;
            Debug.Log($"[GameScoreManager] Awake assigned Instance successfully to {gameObject.name}!");
        }

        public override void OnNetworkSpawn()
        {
            // Force Instance assignment here just in case scene loading shifted things
            Instance = this;
            Debug.Log($"[GameScoreManager] OnNetworkSpawn complete. Am I Server? {IsServer}. Am I Host? {IsHost}");

            BlueTeamScore.OnValueChanged += OnBlueScoreChanged;
            RedTeamScore.OnValueChanged += OnRedScoreChanged;

            UpdateBlueUI(BlueTeamScore.Value);
            UpdateRedUI(RedTeamScore.Value);
        }

        public void AddPointToTeam(int teamIndex)
        {
            // Fix: Ensure we are actually spawned into the netcode layer before checking network properties
            if (!IsSpawned)
            {
                Debug.LogError("[GameScoreManager] AddPointToTeam called, but this object is NOT networked spawned yet!");
                return;
            }

            Debug.Log($"[GameScoreManager] add point to team {teamIndex}. IsServer: {IsServer}, Object Name: {gameObject.name}");

            if (!IsServer) Debug.Log("[GameScoreManager] I am not the server!");
            if (m_MatchEnding) Debug.Log("[GameScoreManager] Match is already ending!");

            if (!IsServer || m_MatchEnding) return;

            if (teamIndex == 0) BlueTeamScore.Value++;
            else if (teamIndex == 1) RedTeamScore.Value++;

            if (BlueTeamScore.Value >= pointsToWin)
            {
                StartMatchEndSequence("Blue Team Wins!");
            }
            else if (RedTeamScore.Value >= pointsToWin)
            {
                StartMatchEndSequence("Red Team Wins!");
            }
        }

        public override void OnNetworkDespawn()
        {
            BlueTeamScore.OnValueChanged -= OnBlueScoreChanged;
            RedTeamScore.OnValueChanged -= OnRedScoreChanged;
        }

        public bool IsGameOver()
        {
            return BlueTeamScore.Value >= pointsToWin || RedTeamScore.Value >= pointsToWin || m_MatchEnding;
        }
       

        private void StartMatchEndSequence(string victoryMessage)
        {
            if (m_MatchEnding) return;
            m_MatchEnding = true;

            // Fire ClientRpc so all connected clients run visual updates and play audio local to their machine
            NotifyMatchEndClientRpc(victoryMessage);

            // Instantly save status metadata across systems before delay sequence
            if (LobbyManager.Instance != null)
            {
                LobbyManager.Instance.LocalMatchResultStatus = victoryMessage;
            }

            StartCoroutine(EndMatchCoroutine(victoryMessage));
        }

        [Rpc(SendTo.Everyone)]
        private void NotifyMatchEndClientRpc(string victoryMessage)
        {
            // If host runs this, don't duplicate logic that was handled directly in StartMatchEndSequence
            if (!IsServer)
            {
                m_MatchEnding = true;
            }

            // Everyone updates their UI highlights locally
            if (victoryMessage.Contains("Blue"))
            {
                if (blueScoreText != null) blueScoreText.color = Color.yellow;
            }
            else if (victoryMessage.Contains("Red"))
            {
                if (redScoreText != null) redScoreText.color = Color.yellow;
            }

            // Everyone plays the conclusion sound local to their headset/speakers
            if (matchEndSFX != null)
            {
                AudioUtility.CreateSFX(matchEndSFX, Vector3.zero, AudioUtility.AudioGroups.HUDObjective, 0f);
            }
        }

        private System.Collections.IEnumerator EndMatchCoroutine(string victoryMessage)
        {
            Debug.Log($"[SERVER] Win condition achieved! Transitioning in {matchEndDelay} seconds...");

            yield return new WaitForSeconds(matchEndDelay);

            if (NetworkManager.Singleton.IsServer)
            {
                OnMatchEnded?.Invoke(victoryMessage);

                Debug.Log("[SERVER] Delay finished. Purging gameplay objects...");

                var spawnedObjects = new List<NetworkObject>(NetworkManager.Singleton.SpawnManager.SpawnedObjects.Values);

                foreach (NetworkObject netObj in spawnedObjects)
                {
                    if (netObj != null && netObj.IsSpawned)
                    {
                        if (netObj.GetComponent<LobbyManager>() != null || netObj.GetComponent<GameScoreManager>() != null)
                            continue;

                        try
                        {
                            netObj.Despawn(true);
                        }
                        catch (System.Exception e)
                        {
                            Debug.LogWarning($"[SERVER] Object wipe skip: {netObj.name} - {e.Message}");
                        }
                    }
                }

                NetworkManager.Singleton.SceneManager.LoadScene("MatchResults", UnityEngine.SceneManagement.LoadSceneMode.Single);
            }
        }

        private void OnBlueScoreChanged(int previousValue, int newValue)
        {
            UpdateBlueUI(newValue);
            if (newValue > previousValue && newValue < pointsToWin) PlayPointSFX();
        }

        private void OnRedScoreChanged(int previousValue, int newValue)
        {
            UpdateRedUI(newValue);
            if (newValue > previousValue && newValue < pointsToWin) PlayPointSFX();
        }

        private void PlayPointSFX()
        {
            if (gainPointSFX != null)
            {
                AudioUtility.CreateSFX(gainPointSFX, Vector3.zero, AudioUtility.AudioGroups.HUDObjective, 0f);
            }
        }

        private void UpdateBlueUI(int score)
        {
            if (blueScoreText != null) blueScoreText.text = score.ToString();
        }

        private void UpdateRedUI(int score)
        {
            if (redScoreText != null) redScoreText.text = score.ToString();
        }
    }
}