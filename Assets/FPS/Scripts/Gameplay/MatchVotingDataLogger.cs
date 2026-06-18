using System.Collections.Generic;
using System.IO;
using Unity.FPS.AI;
using Unity.Netcode;
using UnityEngine;
using Unity.FPS.Core;
using Unity.Collections;

namespace Unity.FPS.Gameplay
{
    [System.Serializable]
    public class VoteEntry
    {
        public string timestamp;
        public string voterName;
        public string targetName;
        public string votedAs; // "Human" or "Bot"
    }

    [System.Serializable]
    public class TargetSummary
    {
        public string targetName;
        public string finalVoteCast;    // "Human" or "Bot"
        public string actualIdentity;    // "Human" or "Bot"
        public bool isCorrect;
    }

    [System.Serializable]
    public class PlayerVotingSummary
    {
        public string playerName;
        public List<TargetSummary> targetVotes = new List<TargetSummary>();
    }

    [System.Serializable]
    public class MatchInformation
    {
        public string matchStartTime;
        public string matchEndTime = "In Progress...";
        public string matchWinner = "Undecided";
        public int maxPlayers;
        public List<string> redTeam = new List<string>();
        public List<string> blueTeam = new List<string>();
    }

    [System.Serializable]
    public class MatchVotingRecord
    {
        public MatchInformation matchInformation = new MatchInformation();
        public List<PlayerVotingSummary> finalVotingSummary = new List<PlayerVotingSummary>();
        public List<VoteEntry> votingTimeline = new List<VoteEntry>();
    }

    public class MatchVotingDataLogger : MonoBehaviour
    {
        public static MatchVotingDataLogger Instance { get; private set; }

        private MatchVotingRecord m_CurrentMatchRecord = new MatchVotingRecord();
        private Dictionary<string, string> m_CachedIdentities = new Dictionary<string, string>();

        private string m_CurrentSaveFilePath = string.Empty;
        private bool m_IsRecordFinalized = false;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            // REMOVED: DontDestroyOnLoad(gameObject); 
            // Keeping it local to the map scene prevents scene-transition pollution and ghost singletons.
        }

        private System.Collections.IEnumerator Start()
        {
            yield return new WaitUntil(() => NetworkManager.Singleton != null);

            if (NetworkManager.Singleton.IsServer)
            {
                yield return new WaitUntil(() => GameScoreManager.Instance != null);
                GameScoreManager.Instance.OnMatchEnded += HandleMatchEndedCleanly;
                Debug.Log("[LOGGER] Attached to GameScoreManager.");
            }
        }

        private void OnDestroy()
        {
            if (GameScoreManager.Instance != null)
            {
                GameScoreManager.Instance.OnMatchEnded -= HandleMatchEndedCleanly;
            }

            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void OnDisable()
        {
            // Catches scene cleanups or sudden unloads cleanly before references drop
            HandlePrematureTermination();
        }

        private void OnApplicationQuit()
        {
            // Catch hard desktop closures, Alt+F4, or server instance terminations
            HandlePrematureTermination();
        }

        public void RecordMatchStart(Unity.Services.Lobbies.Models.Lobby activeLobby)
        {
            if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsServer) return;

            m_IsRecordFinalized = false;
            m_CurrentMatchRecord = new MatchVotingRecord();
            m_CachedIdentities.Clear();

            m_CurrentMatchRecord.matchInformation.matchStartTime = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            if (activeLobby != null)
            {
                m_CurrentMatchRecord.matchInformation.maxPlayers = activeLobby.MaxPlayers;
                m_CurrentMatchRecord.matchInformation.redTeam.Clear();
                m_CurrentMatchRecord.matchInformation.blueTeam.Clear();

                foreach (var lobbyPlayer in activeLobby.Players)
                {
                    if (lobbyPlayer.Data != null && lobbyPlayer.Data.ContainsKey("PlayerTeam") && lobbyPlayer.Data.ContainsKey("PlayerName"))
                    {
                        string team = lobbyPlayer.Data["PlayerTeam"].Value;
                        string pName = lobbyPlayer.Data["PlayerName"].Value;
                        string prefabChoice = lobbyPlayer.Data.ContainsKey("PlayerPrefab") ? lobbyPlayer.Data["PlayerPrefab"].Value : "HumanPlayer";
                        string formattedPlayerEntry = $"{pName} ({prefabChoice})";

                        string trueIdentity = (prefabChoice.Contains("Bot") || prefabChoice.Contains("AI")) ? "Bot" : "Human";
                        if (!m_CachedIdentities.ContainsKey(pName))
                        {
                            m_CachedIdentities.Add(pName, trueIdentity);
                        }

                        if (team == "Red") m_CurrentMatchRecord.matchInformation.redTeam.Add(formattedPlayerEntry);
                        else if (team == "Blue") m_CurrentMatchRecord.matchInformation.blueTeam.Add(formattedPlayerEntry);
                    }
                }
            }

            // Generate initial live tracking file on disk instantly
            string folderPath = Path.Combine(Application.dataPath, "../", "VotingRecords");
            if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

            string fileTimestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string fileName = $"MatchVotingRecord_{fileTimestamp}.json";
            m_CurrentSaveFilePath = Path.Combine(folderPath, fileName);

            SaveRecordToDisk();
            Debug.Log($"[LOGGER] Live logging initialized at: {m_CurrentSaveFilePath}");
        }

        public void RecordVote(string voter, string target, string guessType)
        {
            if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsServer) return;
            if (m_IsRecordFinalized || string.IsNullOrEmpty(m_CurrentSaveFilePath)) return;

            VoteEntry newVote = new VoteEntry
            {
                timestamp = System.DateTime.Now.ToString("HH:mm:ss"),
                voterName = voter,
                targetName = target,
                votedAs = guessType
            };

            m_CurrentMatchRecord.votingTimeline.Add(newVote);

            // Sync immediately to disk
            SaveRecordToDisk();
        }

        private void HandleMatchEndedCleanly(string winner)
        {
            FinalizeMatchRecord(winner);
        }

        private void HandlePrematureTermination()
        {
            // Only execute if a game was running and never reached its formal sequence end
            if (m_IsRecordFinalized || string.IsNullOrEmpty(m_CurrentSaveFilePath)) return;

            Debug.LogWarning("[LOGGER] Match interrupted prematurely! Finalizing with failure states.");
            FinalizeMatchRecord("Match ended prematurely - No Winner");
        }

        private void FinalizeMatchRecord(string finalWinnerOutcome)
        {
            if (m_IsRecordFinalized) return;
            m_IsRecordFinalized = true;

            m_CurrentMatchRecord.matchInformation.matchEndTime = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            m_CurrentMatchRecord.matchInformation.matchWinner = finalWinnerOutcome;

            CompileFinalVotingSummary();
            SaveRecordToDisk();

            Debug.Log($"[LOGGER] Match file finalized successfully. Outcome: {finalWinnerOutcome}");
        }

        private void SaveRecordToDisk()
        {
            try
            {
                if (string.IsNullOrEmpty(m_CurrentSaveFilePath)) return;
                string jsonOutput = JsonUtility.ToJson(m_CurrentMatchRecord, true);
                File.WriteAllText(m_CurrentSaveFilePath, jsonOutput);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[LOGGER] Failed writing live logging step: {e.Message}");
            }
        }

        private void CompileFinalVotingSummary()
        {
            m_CurrentMatchRecord.finalVotingSummary.Clear();
            Dictionary<string, Dictionary<string, VoteEntry>> finalVoteMap = new Dictionary<string, Dictionary<string, VoteEntry>>();

            foreach (VoteEntry vote in m_CurrentMatchRecord.votingTimeline)
            {
                if (!finalVoteMap.ContainsKey(vote.voterName))
                {
                    finalVoteMap[vote.voterName] = new Dictionary<string, VoteEntry>();
                }
                finalVoteMap[vote.voterName][vote.targetName] = vote;
            }

            foreach (var voterKvp in finalVoteMap)
            {
                PlayerVotingSummary playerSummary = new PlayerVotingSummary { playerName = voterKvp.Key };

                foreach (var targetKvp in voterKvp.Value)
                {
                    string targetedPlayerName = targetKvp.Key;
                    string finalGuess = targetKvp.Value.votedAs;

                    string trueIdentity = m_CachedIdentities.ContainsKey(targetedPlayerName)
                        ? m_CachedIdentities[targetedPlayerName]
                        : "Unknown";

                    bool correctCheck = (trueIdentity != "Unknown") && (finalGuess.ToLower() == trueIdentity.ToLower());

                    TargetSummary targetSummary = new TargetSummary
                    {
                        targetName = targetedPlayerName,
                        finalVoteCast = finalGuess,
                        actualIdentity = trueIdentity,
                        isCorrect = correctCheck
                    };

                    playerSummary.targetVotes.Add(targetSummary);
                }
                m_CurrentMatchRecord.finalVotingSummary.Add(playerSummary);
            }
        }
    }
}