#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using YutArena.Common;
using YutArena.Managers;

// Observes the real UI flow. Does not create settings, select characters, move pieces, or change turns.
[InitializeOnLoad]
internal static class QaStartFlowVerification
{
    private const string Flag = "Temp/verify-qa-start-flow.flag";
    private const string Report = "Temp/qa-start-flow-verification.log";
    private static bool active, sawBoardPiece;
    private static double nextPoll;
    private static string lobbySettings, previousReport;
    private static int automaticStatusLogs, actionLogs;
    private static readonly List<string> scenes = new List<string>();
    private static readonly Dictionary<string, bool> checks = new Dictionary<string, bool>();
    static QaStartFlowVerification()
    {
        EditorApplication.playModeStateChanged += OnPlayMode;
        EditorApplication.update += Observe;
        SceneManager.sceneLoaded += SceneLoaded;
        Application.logMessageReceived += LogReceived;
    }
    [MenuItem("Tools/QA/Verify StartScene Flow (read only)")]
    private static void Begin()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().name != "StartScene")
        {
            Debug.LogWarning("StartScene을 연 Edit Mode에서 실행하고, 평소처럼 로비와 캐릭터 선택을 진행하세요.");
            return;
        }
        File.WriteAllText(Flag, "run");
        EditorApplication.isPlaying = true;
    }
    private static void OnPlayMode(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode && File.Exists(Flag))
        {
            File.Delete(Flag);
            active = true; sawBoardPiece = false; nextPoll = 0;
            lobbySettings = null; previousReport = null;
            automaticStatusLogs = 0; actionLogs = 0; checks.Clear(); scenes.Clear();
            string start = SceneManager.GetActiveScene().name;
            scenes.Add(start);
            checks["Started in StartScene"] = start == "StartScene";
            WriteReport();
        }
        if (state == PlayModeStateChange.ExitingPlayMode && active) { WriteReport(); active = false; }
    }
    private static void SceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!active) return;
        scenes.Add(scene.name);
        if (scene.name == "ChampionPickScene") lobbySettings = JsonUtility.ToJson(GameStartSettingsHolder.Current);
        WriteReport();
    }
    private static void LogReceived(string message, string stack, LogType type)
    {
        if (!active) return;
        if (message.StartsWith("[QA][상태 변경 기록]", StringComparison.Ordinal)) automaticStatusLogs++;
        if (message.StartsWith("[QA][특별 행동]", StringComparison.Ordinal)) actionLogs++;
    }
    private static void Observe()
    {
        if (!active || !EditorApplication.isPlaying || EditorApplication.timeSinceStartup < nextPoll) return;
        nextPoll = EditorApplication.timeSinceStartup + 0.5;
        if (SceneManager.GetActiveScene().name != "InGameScene") return;
        var monitor = UnityEngine.Object.FindFirstObjectByType<PlayerDataDebugTester>();
        var players = UnityEngine.Object.FindFirstObjectByType<PlayerManager>();
        var turns = UnityEngine.Object.FindFirstObjectByType<TestTurnManager>();
        var settings = GameStartSettingsHolder.Current;
        if (monitor == null || players == null || turns?.Settings == null || settings == null || turns.CurrentTurn.roundNumber < 1) return;
        monitor.PollNow();
        string snapshot = monitor.CurrentSnapshot;
        var sections = snapshot.Replace("\r\n", "\n").Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries);
        checks["Traversed lobby and character selection"] = scenes.Contains("LocalLobbyScene") && scenes.Contains("ChampionPickScene");
        checks["In-game settings match the lobby selection"] = lobbySettings != null && lobbySettings == JsonUtility.ToJson(settings) &&
            lobbySettings == JsonUtility.ToJson(turns.Settings);
        checks["Player count matches the saved lobby setting"] = players.ActivePlayers.Count == settings.playerCount;
        bool characters = true, headers = true, pieces = true;
        foreach (var player in players.ActivePlayers)
        {
            string section = sections.FirstOrDefault(s => s.StartsWith("[P" + player.PlayerId + "]", StringComparison.Ordinal)) ?? "";
            characters &= CharacterSelectionResult.TryGetCharacterData(player.PlayerId - 1, out var chosen) && chosen == player.SelectedCharacter;
            headers &= section.Contains(player.SelectedCharacter != null ? player.SelectedCharacter.char_Name : "미등록") &&
                section.Contains($"SP={CharacterSkillRegistry.GetSkillPoints(player.PlayerId)}/{CharacterSkillRegistry.GetMaxSkillPoints(player.PlayerId)}");
            int board = 0;
            foreach (var piece in player.RuntimeData.Pieces)
            {
                if (piece.State != PieceState.InBoard) continue;
                board++; sawBoardPiece = true;
                string label = $"말{piece.PieceId + 1}(id={piece.PieceId}) | 상태=InBoard, 위치={piece.CurrentTileId}";
                pieces &= section.Contains(label);
            }
            if (board == 0) pieces &= section.Contains("보드 위 말 없음");
            pieces &= !section.Contains("상태=Waiting") && !section.Contains("상태=Goal");
        }
        checks["Actual selected characters survive the scene transition"] = characters;
        checks["Every player has their actual character and SP header"] = headers;
        checks["Only board pieces appear under their own player"] = pieces;
        checks["Round, turn and current phase stay visible"] = snapshot.Contains($"라운드={turns.CurrentTurn.roundNumber}, 전체턴={turns.CurrentTurn.turnNumber}") &&
            snapshot.Contains($"단계={turns.CurrentTurn.currentPhase}");
        checks["Default status updates add no Console history"] = automaticStatusLogs == 0 && !monitor.LogStateChanges;
        var window = Resources.FindObjectsOfTypeAll<QaPlayingDebugWindow>().FirstOrDefault();
        if (window != null && window.CachedStatus == snapshot)
            checks["Automatic live window has a separate panel for every player"] = window.VisibleStatusBlockCount == settings.playerCount + 1;
        WriteReport();
    }
    private static void WriteReport()
    {
        string body = "READ-ONLY StartScene flow verification\nScenes: " + string.Join(" -> ", scenes) + "\n" +
            string.Join("\n", checks.Select(c => (c.Value ? "PASS " : "FAIL ") + c.Key)) + "\n" +
            (sawBoardPiece ? "PASS " : "PENDING ") + "Observed a piece entering the board through normal play\n" +
            "Console action batches observed: " + actionLogs + "\n" +
            (checks.ContainsKey("Automatic live window has a separate panel for every player") ? "" : "PENDING Live window binding\n");
        if (body == previousReport) return;
        previousReport = body;
        File.WriteAllText(Report, body);
    }
}
#endif
