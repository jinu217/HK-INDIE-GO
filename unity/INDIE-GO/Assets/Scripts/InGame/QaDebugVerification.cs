#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using YutArena.Common;
using YutArena.InGame;
using YutArena.Managers;

// Plain-data checks: no scene loading, lobby writes, or changes to the current game's pieces.
[InitializeOnLoad]
internal static class QaPieceFieldsVerification
{
    private const string Flag = "Temp/verify-qa-piece-fields.flag";
    static QaPieceFieldsVerification()
    {
        EditorApplication.delayCall += () => { if (File.Exists(Flag)) Run(); };
    }
    [MenuItem("Tools/QA/Verify Piece Status Fields")]
    private static void Run()
    {
        if (File.Exists(Flag)) File.Delete(Flag);
        var checks = new List<string>();
        void Check(string name, bool value) => checks.Add((value ? "PASS " : "FAIL ") + name);
        var owner = new PlayerRuntimeData(1, "QA isolated data", 4);
        foreach (var piece in owner.Pieces) piece.MoveTo(BoardTileId.Outer01);
        string Format(int id, TeamSlot team = TeamSlot.TeamA)
        {
            var output = new StringBuilder();
            PlayerDataDebugTester.AppendPieceDetails(output, owner, owner.Pieces[id], team);
            return output.ToString();
        }
        Check("Solo piece has zero carried pieces and no stack leader", Format(3).Contains("업기역할=단독, 리더말=-, 업힌말=[], 업힌수=0, 그룹전체=1"));
        for (int i = 0; i < 3; i++) owner.Pieces[i].SetStackGroup(0, 0);
        string leader = Format(0), passenger = Format(1);
        Check("Leader is distinct from carried members", leader.Contains("업기역할=리더, 리더말=P1/말1(id=0), 업힌말=[P1/말2(id=1), P1/말3(id=2)]"));
        Check("Carried row identifies the actual carrier", passenger.Contains("업기역할=업힌 말, 리더말=P1/말1(id=0)"));
        Check("Carried count excludes the leader", leader.Contains("업힌수=2, 그룹전체=3") && passenger.Contains("업힌수=2, 그룹전체=3"));
        Check("Unstacked piece on the same tile is excluded", !leader.Contains("말4(id=3)"));
        Check("Each piece exposes the supplied lobby team", Format(0, TeamSlot.TeamB).StartsWith("    팀=TeamB,"));
        owner.Pieces[2].State = PieceState.Waiting;
        Check("Off-board stale group members are excluded", Format(0).Contains("업힌말=[P1/말2(id=1)], 업힌수=1, 그룹전체=2"));
        owner.Pieces[0].State = PieceState.Goal;
        Check("Missing leader is visible instead of fabricating a board piece", Format(1).Contains("[보드에서 찾지 못함]"));

        foreach (var sample in new[] {
            (BoardTileId.None, 1), (BoardTileId.Outer16, 2), (BoardTileId.Inner08, 2),
            (BoardTileId.Center, 4), (BoardTileId.Corner01, 7), (BoardTileId.Corner02, 7),
            (BoardTileId.Outer01, 11), (BoardTileId.Inner03, 8) })
            Check("Minimum goal distance " + sample.Item1,
                PieceMovementManager.TryCountMinimumGoalSteps(sample.Item1, BoardTileId.None, out int distance) && distance == sample.Item2);
        Check("Undefined tiles do not produce a fake distance", !PieceMovementManager.TryCountMinimumGoalSteps((BoardTileId)999, BoardTileId.None, out _));
        Check("Formatting does not change piece state or stack data", owner.Pieces[1].State == PieceState.InBoard &&
            owner.Pieces[1].CurrentTileId == BoardTileId.Outer01 && owner.Pieces[1].StackGroupId == 0 && owner.Pieces[1].StackLeaderPieceId == 0);
        File.WriteAllLines("Temp/qa-piece-fields-verification.log", checks);
        Debug.Log($"[QA Piece Fields] {checks.Count(c => c.StartsWith("PASS"))} passed / {checks.Count(c => c.StartsWith("FAIL"))} failed.");
    }
}

// Explicit opt-in integration test. Never runs in a shipped build or normal play.
[InitializeOnLoad]
internal static class QaVerificationGate
{
    internal const string Flag = "Temp/verify-qa.flag";
    static QaVerificationGate()
    {
        EditorApplication.delayCall += () =>
        {
            if (File.Exists(Flag) && !EditorApplication.isPlayingOrWillChangePlaymode) Begin();
        };
    }
    [MenuItem("Tools/QA/Verify InGame QA Logging")]
    public static void Begin()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "InGameScene")
        {
            Debug.LogWarning("QA 검증은 InGameScene을 연 Edit Mode에서 실행하세요.");
            return;
        }
        File.WriteAllText(Flag, "run");
        File.WriteAllText(Path.Combine(Path.GetTempPath(), "indiego-character-runtime-verifier.flag"), "run");
        EditorApplication.isPlaying = true;
    }
}
internal sealed class QaDebugVerification : MonoBehaviour
{
    private readonly List<string> traces = new List<string>();
    private readonly List<string> checks = new List<string>();
    private DateTime entered;
    private bool hadInitialQaListener;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (!File.Exists(QaVerificationGate.Flag)) return;
        File.Delete(QaVerificationGate.Flag);
        new GameObject("QA Verification (temporary)").AddComponent<QaDebugVerification>();
    }
    private void Awake()
    {
        entered = DateTime.UtcNow;
        hadInitialQaListener = QaDebugLog.Enabled;
        QaDebugLog.Receiver += Capture;
    }
    private void OnDestroy() => QaDebugLog.Receiver -= Capture;
    private void Capture(string line) { if (traces.Count < 4096) traces.Add(line); }
    private void Check(string name, bool passed) => checks.Add((passed ? "PASS " : "FAIL ") + name);
    private IEnumerator Start()
    {
        string report = Path.Combine(Path.GetTempPath(), "indiego-character-runtime-verifier.log");
        float deadline = Time.realtimeSinceStartup + 60;
        while ((!File.Exists(report) || File.GetLastWriteTimeUtc(report) < entered) && Time.realtimeSinceStartup < deadline)
            yield return null;
        Check("Character integration report updated", File.Exists(report) && File.GetLastWriteTimeUtc(report) >= entered);
        if (File.Exists(report)) Check("Character integration has no failures", File.ReadLines(report).First().Contains("Failed=0"));
        var monitor = FindFirstObjectByType<PlayerDataDebugTester>();
        var players = FindFirstObjectByType<PlayerManager>();
        Check("Existing DataDebuger automatically connected", monitor != null && hadInitialQaListener);
        if (monitor != null && players != null)
        {
            // Timers are stopped by the character verifier's end-game check.
            monitor.PollNow();
            int batches = monitor.ConsoleBatchCount;
            for (int i = 0; i < 10; i++) monitor.PollNow();
            Check("Stable state generates no extra Console batches", monitor.ConsoleBatchCount == batches);
            Check("All player headers remain visible even with no board pieces",
                players.ActivePlayers.All(p => monitor.CurrentSnapshot.Contains("[P" + p.PlayerId + "]")));
            Check("Settings and order precede the permanent turn flow",
                monitor.CurrentSnapshot.IndexOf("[게임 설정]", StringComparison.Ordinal) < monitor.CurrentSnapshot.IndexOf("[턴 순서]", StringComparison.Ordinal) &&
                monitor.CurrentSnapshot.IndexOf("[턴 순서]", StringComparison.Ordinal) < monitor.CurrentSnapshot.IndexOf("[턴 진행]", StringComparison.Ordinal));
            CharacterSkillRegistry.TryGet(1, 0, out var registeredCharacter);
            var displayedCharacter = players.ActivePlayers[0].SelectedCharacter ?? registeredCharacter.Data;
            Check("Direct-scene fallback header uses the registered character",
                monitor.CurrentSnapshot.Contains("[P1] " + displayedCharacter.char_Name));
            var member = players.ActivePlayers[0].RuntimeData.Pieces[0];
            member.Reset(); member.MoveTo(BoardTileId.Outer01);
            monitor.PollNow();
            Check("Direct runtime data changes are detected", monitor.CurrentSnapshot.Contains("위치=Outer01"));
            Check("Human piece numbers expose zero-based ids", monitor.CurrentSnapshot.Contains("말1(id=0)"));
            Func<int, string> playerBlock = id => monitor.CurrentSnapshot.Replace("\r\n", "\n")
                .Split(new[] { "\n\n" }, StringSplitOptions.None).First(s => s.StartsWith("[P" + id + "]", StringComparison.Ordinal));
            var otherMember = players.ActivePlayers[1].RuntimeData.Pieces[2];
            otherMember.Reset(); otherMember.MoveTo(BoardTileId.Outer09); monitor.PollNow();
            Check("Board pieces from multiple players appear in their own sections",
                playerBlock(1).Contains("위치=Outer01") && playerBlock(2).Contains("말3(id=2)") && playerBlock(2).Contains("위치=Outer09"));
            otherMember.Reset(); monitor.PollNow();
            Check("Only pieces currently on the board have status rows",
                !monitor.CurrentSnapshot.Contains("상태=Waiting") && !monitor.CurrentSnapshot.Contains("상태=Goal"));
            // Exercise simultaneous effects without gameplay-trigger setup assumptions.
            member.Cc.Add(new CcState(CcDefine.Sturn, 2, 1, 2, 0, member.CurrentTileId, null, -1));
            member.Cc.Add(new CcState(CcDefine.Silence, 3, 1, 2, 0, member.CurrentTileId, null, -1));
            monitor.PollNow();
            Check("Multiple simultaneous effects and their sources are visible",
                monitor.CurrentSnapshot.Contains("Sturn(") && monitor.CurrentSnapshot.Contains("Silence(") &&
                monitor.CurrentSnapshot.Contains("출처=P2/id=0"));
            Check("Snapshot includes SP, cooldown and all-CC section",
                monitor.CurrentSnapshot.Contains("SP=") && monitor.CurrentSnapshot.Contains("액티브CD=") && monitor.CurrentSnapshot.Contains("CC=["));
            var turns = FindFirstObjectByType<TestTurnManager>();
            var originalMode = turns.Settings.gameMode;
            int originalTarget = turns.Settings.targetEscapeCount;
            turns.Settings.gameMode = GameMode.Classic;
            monitor.PollNow();
            int finished = players.ActivePlayers[0].RuntimeData.Pieces.Count(p => p.IsFinished);
            int total = players.ActivePlayers[0].RuntimeData.Pieces.Count;
            Check("Classic status shows finished and remaining piece counts",
                monitor.CurrentSnapshot.Contains($"완주={finished}/{total}") &&
                monitor.CurrentSnapshot.Contains($"남은말={total - finished}") &&
                !monitor.CurrentSnapshot.Contains("| 점수=") && !monitor.CurrentSnapshot.Contains("| 팀점수="));
            turns.Settings.gameMode = GameMode.Escape; turns.Settings.targetEscapeCount = 5;
            monitor.PollNow();
            var scoreTeam = MatchCompositionRule.GetTeamSlot(turns.Settings, (PlayerSlot)players.ActivePlayers[0].PlayerId);
            int score = FindFirstObjectByType<TestWinConditionManager>().GetEscapeCount(scoreTeam);
            Check("Escape status uses the mode's score and target",
                playerBlock(1).Contains($"{(turns.Settings.isTeamMode ? "팀점수" : "점수")}={score}/5") &&
                !playerBlock(1).Contains("남은말="));
            turns.Settings.gameMode = originalMode; turns.Settings.targetEscapeCount = originalTarget;
            int previousTurn = turns.CurrentTurn.turnNumber;
            var previousPhase = turns.CurrentTurn.currentPhase;
            turns.CurrentTurn.turnNumber++;
            foreach (var phase in new[] { TurnPhase.Throwing, TurnPhase.SaveThrowResult, TurnPhase.CheckExtraThrow, TurnPhase.WaitAction })
            {
                turns.CurrentTurn.currentPhase = phase;
                turns.OnTurnPhaseChanged?.Invoke(turns.CurrentTurn);
            }
            monitor.PollNow();
            Check("Turn flow retains phases completed between polls", monitor.CurrentSnapshot.Contains("Throwing → SaveThrowResult → CheckExtraThrow → WaitAction"));
            turns.CurrentTurn.turnNumber = previousTurn; turns.CurrentTurn.currentPhase = previousPhase;
            monitor.PollNow();
            var console = new List<string>();
            Application.LogCallback captureConsole = (message, stack, type) => { if (message.StartsWith("[QA]", StringComparison.Ordinal)) console.Add(message); };
            Application.logMessageReceived += captureConsole;
            try
            {
                member.MoveTo(BoardTileId.Outer02); monitor.PollNow();
                Check("Status updates do not accumulate in Console by default", console.Count == 0 && !monitor.LogStateChanges);
                monitor.LogAllPieceData();
                string full = console.LastOrDefault(m => m.StartsWith("[QA][현재 상태]", StringComparison.Ordinal));
                Check("Manual Console snapshot contains the complete player layout", full != null && full.Contains("[게임 설정]") &&
                    players.ActivePlayers.All(p => full.Contains("[P" + p.PlayerId + "]")));
                QaDebugLog.Event("[QA-test special action]"); monitor.PollNow();
                Check("Special actions and current status use separate Console entries",
                    console.Any(m => m.StartsWith("[QA][특별 행동]", StringComparison.Ordinal) && !m.Contains("[게임 설정]")));
            }
            finally { Application.logMessageReceived -= captureConsole; }
            member.Reset(); monitor.PollNow();
            Check("Retired pieces disappear from permanent status", !playerBlock(1).Contains("말1(id=0) | 상태=InBoard"));
            member.MoveTo(BoardTileId.Outer01); monitor.PollNow();
            foreach (string id in new[] {"001_1", "001_2", "002", "003", "004", "005", "006", "007", "008", "009", "010"})
            {
                Check("Active trace " + id, traces.Any(t => t.Contains("[액티브 요청]") && t.Contains("CHAR_" + id + ")") && t.Contains("성공=True")));
                Check("Passive trace " + id, traces.Any(t => t.Contains("[패시브") && t.Contains("CHAR_" + id + ")")));
            }
            Check("Passive effects identify source and target", traces.Any(t => t.Contains("[패시브]") && t.Contains(" -> P") && t.Contains("처리성공=True")));
            Check("Immunity passive identifies the attacker", traces.Any(t => t.Contains("[패시브/면역]") && t.Contains(" -> P")));
            Check("Capture traces include both pieces and the decision", traces.Any(t => t.Contains("[잡기]") && t.Contains(" -> P") && t.Contains("방어판정=")));
            Check("Blocked captures are distinguishable", traces.Any(t => t.Contains("[잡기]") && t.Contains("잡히지 않음")));
            Check("Ordinary SP changes are not duplicated as action spam", !traces.Any(t => t.Contains("[SP]")));
            Check("Normal movement is not duplicated as action spam", !traces.Any(t => t.Contains(" spaces)")));
            var map = FindFirstObjectByType<MapManager>();
            if (map != null)
            {
                var fields = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                var tiles = (Dictionary<BoardTileId, MapSpecialTileSetting>)typeof(MapManager).GetField("specialTileSettings", fields).GetValue(map);
                var enabledField = typeof(MapManager).GetField("useSpecialTiles", fields);
                bool wasEnabled = (bool)enabledField.GetValue(map);
                tiles.TryGetValue(BoardTileId.Corner01, out var oldSetting);
                var mapEvent = ScriptableObject.CreateInstance<LogMapSpecialTileEvent>();
                try
                {
                    enabledField.SetValue(map, true);
                    tiles[BoardTileId.Corner01] = new MapSpecialTileSetting { tileId = BoardTileId.Corner01, tileEvent = mapEvent, value = 7 };
                    member.MoveTo(BoardTileId.Corner01);
                    int beforeMap = traces.Count(t => t.Contains("[맵 기믹]"));
                    map.ResolveSpecialTile(players.ActivePlayers[0], member, 1, FindFirstObjectByType<PieceMovementManager>());
                    Check("Every map event emits one common action trace", traces.Count(t => t.Contains("[맵 기믹]")) == beforeMap + 1 &&
                        traces.Last(t => t.Contains("[맵 기믹]")).Contains("대상=P1/말1"));
                }
                finally
                {
                    enabledField.SetValue(map, wasEnabled);
                    if (oldSetting == null) tiles.Remove(BoardTileId.Corner01); else tiles[BoardTileId.Corner01] = oldSetting;
                    DestroyImmediate(mapEvent);
                }
                monitor.PollNow();
            }

            for (int i = 0; i < 80; i++) QaDebugLog.Event("burst-" + i);
            batches = monitor.ConsoleBatchCount;
            monitor.PollNow();
            Check("Burst events use a single bounded batch", monitor.ConsoleBatchCount == batches + 1 &&
                monitor.RecentEvents.Split(new[] {'\n'}, StringSplitOptions.RemoveEmptyEntries).Length == 64);
            monitor.LogStateChanges = false; monitor.LogGameplayEvents = false;
            batches = monitor.ConsoleBatchCount;
            member.MoveTo(BoardTileId.Outer02); QaDebugLog.Event("muted event"); monitor.PollNow();
            Check("Console can be muted while live view still updates", monitor.ConsoleBatchCount == batches &&
                monitor.CurrentSnapshot.Contains("위치=Outer02"));
            monitor.LogStateChanges = false; monitor.LogGameplayEvents = true;
            int polls = monitor.PollCount;
            yield return new WaitForSecondsRealtime(1.1f);
            Check("Default polling is bounded to 0.5 seconds", monitor.PollCount - polls <= 3 && monitor.PollCount - polls >= 1);

            // Informational measurement in this editor; not a hardware-independent performance guarantee.
            var watch = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < 100; i++) monitor.PollNow();
            watch.Stop();
            checks.Add("MEASURE stable poll average_ms=" + (watch.Elapsed.TotalMilliseconds / 100).ToString("F4"));
            monitor.enabled = false;
            Check("Disabled monitor unsubscribes", !QaDebugLog.Receiver.GetInvocationList().Any(d => ReferenceEquals(d.Target, monitor)));
            monitor.enabled = true;
            QaDebugLog.Event("reenabled observer"); monitor.PollNow();
            Check("Reenabled monitor reconnects once", monitor.RecentEvents.Contains("reenabled observer") &&
                QaDebugLog.Receiver.GetInvocationList().Count(d => ReferenceEquals(d.Target, monitor)) == 1);
        }
        File.WriteAllLines("Temp/qa-verification.log", checks);
        Debug.Log("[QA Verification] " + checks.Count(t => t.StartsWith("PASS")) + " passed / " + checks.Count(t => t.StartsWith("FAIL")) + " failed. Temp/qa-verification.log");
        EditorApplication.isPlaying = false;
    }
}
#endif
