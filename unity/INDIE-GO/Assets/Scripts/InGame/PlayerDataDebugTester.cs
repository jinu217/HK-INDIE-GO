using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Unity.Profiling;
using YutArena.Common;
using YutArena.InGame;
using YutArena.Managers;

/// <summary>Fixed-layout current status for all players, plus a separate special-action stream.</summary>
[DisallowMultipleComponent]
public sealed class PlayerDataDebugTester : MonoBehaviour
{
    [SerializeField] private PlayerManager playerManager;
    [SerializeField] private PieceMovementManager pieceMovementManager;
    [SerializeField] private TestTurnManager turnManager;
    [SerializeField] private TestWinConditionManager winManager;
    private MapManager mapManager;
    [Header("QA (Editor / Development Build only)")]
    [SerializeField, Min(0.25f)] private float refreshInterval = 0.5f;
    [SerializeField, InspectorName("상태 변경을 Console에도 기록 (로그 누적)")] private bool logStateChanges;
    [SerializeField, InspectorName("Console 특별 행동")] private bool logGameplayEvents = true;
    public string CurrentSnapshot { get; private set; } = "플레이 모드에서 상태를 수집합니다.";
    public string RecentEvents { get; private set; } = "";
    public float RefreshInterval { get => refreshInterval; set => refreshInterval = Mathf.Clamp(value, 0.25f, 10f); }
    public bool LogStateChanges { get => logStateChanges; set => logStateChanges = value; }
    public bool LogGameplayEvents { get => logGameplayEvents; set => logGameplayEvents = value; }
    public int PollCount { get; private set; }
    public int ConsoleBatchCount { get; private set; }
    private const int EventCapacity = 64;
    private readonly Queue<string> events = new Queue<string>();
    private readonly Queue<string> history = new Queue<string>();
    private readonly Queue<TurnPhase> phaseHistory = new Queue<TurnPhase>();
    private readonly StringBuilder text = new StringBuilder(8192);
    private float nextPoll;
    private int droppedEvents;
    private bool forceSnapshot { get; set; }
    private bool historyDirty { get; set; }
    private TestTurnManager observedTurns;
    private int flowRound = -1, flowTurn = -1;
    private PlayerSlot flowPlayer;
    private TurnPhase lastPhase;
    private static readonly ProfilerMarker PollMarker = new ProfilerMarker("QA.StatePoll");

    private void OnEnable()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!Application.isPlaying) return;
        forceSnapshot = false;
        nextPoll = 0;
        QaDebugLog.Receiver += ReceiveEvent;
        ResolveDependencies();
#else
        enabled = false;
#endif
    }
    private void OnDisable()
    {
        QaDebugLog.Receiver -= ReceiveEvent;
        if (observedTurns != null) observedTurns.OnTurnPhaseChanged -= RecordPhase;
        observedTurns = null;
    }
    private void ResolveDependencies()
    {
        if (playerManager == null) playerManager = FindFirstObjectByType<PlayerManager>();
        if (pieceMovementManager == null) pieceMovementManager = FindFirstObjectByType<PieceMovementManager>();
        if (turnManager == null) turnManager = FindFirstObjectByType<TestTurnManager>();
        if (winManager == null) winManager = FindFirstObjectByType<TestWinConditionManager>();
        if (mapManager == null) mapManager = FindFirstObjectByType<MapManager>();
        if (observedTurns == turnManager) return;
        if (observedTurns != null) observedTurns.OnTurnPhaseChanged -= RecordPhase;
        observedTurns = turnManager;
        if (observedTurns != null)
        {
            observedTurns.OnTurnPhaseChanged += RecordPhase;
            RecordPhase(observedTurns.CurrentTurn);
        }
    }
    private void RecordPhase(TurnContext turn)
    {
        if (turn == null) return;
        if (flowRound != turn.roundNumber || flowTurn != turn.turnNumber || flowPlayer != turn.currentPlayer)
        {
            phaseHistory.Clear();
            flowRound = turn.roundNumber; flowTurn = turn.turnNumber; flowPlayer = turn.currentPlayer;
        }
        if (phaseHistory.Count > 0 && lastPhase == turn.currentPhase) return;
        if (phaseHistory.Count == 16) phaseHistory.Dequeue();
        phaseHistory.Enqueue(turn.currentPhase);
        lastPhase = turn.currentPhase;
    }
    private void Update()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (Time.unscaledTime < nextPoll) return;
        nextPoll = Time.unscaledTime + Mathf.Clamp(refreshInterval, 0.25f, 10f);
        PollNow();
#endif
    }
    private void ReceiveEvent(string message)
    {
        var turn = turnManager != null ? turnManager.CurrentTurn : null;
        if (turn != null) message = $"[R{turn.roundNumber}/T{turn.turnNumber}/{turn.currentPhase}] " + message;
        if (events.Count == EventCapacity) { events.Dequeue(); droppedEvents++; }
        events.Enqueue(message);
        if (history.Count == EventCapacity) history.Dequeue();
        history.Enqueue(message);
        historyDirty = true;
    }
    [ContextMenu("QA / Print Full Snapshot")]
    public void LogAllPieceData()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!Application.isPlaying) return;
        forceSnapshot = true;
        PollNow();
#endif
    }
    public void ClearEventHistory()
    {
        history.Clear(); events.Clear(); RecentEvents = "";
        droppedEvents = 0; historyDirty = false;
    }
    public void PollNow()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!Application.isPlaying) return;
        using (PollMarker.Auto())
        {
            ResolveDependencies();
            if (playerManager == null) return;
            PollCount++;
            text.Clear();
            var settings = turnManager?.Settings ?? GameStartSettingsHolder.Current;
            string actualMap = mapManager != null && mapManager.CurrentMap != null ? mapManager.CurrentMap.mapType.ToString() : "로딩 중";
            text.AppendLine(settings == null ? "[게임 설정] 초기화 대기" :
                $"[게임 설정] 모드={settings.gameMode}, 선택맵={settings.mapType}, 현재맵={actualMap}, 인원={settings.playerCount}, 대전방식={GetMatchModeDisplay(settings)}, " +
                $"스킬={settings.useSkill}, 실제스킬사용={CharacterSkillRegistry.AreSkillsEnabled}, 아이템={settings.useItem}, 특수칸={settings.useSpecialTile}, 목표점수={settings.targetEscapeCount}, 최대라운드={settings.maxTurnCount}");
            AppendTurn();
            if (playerManager.ActivePlayers.Count == 0)
                text.AppendLine("[플레이어] 로비 설정을 전달받아 플레이어/말을 생성하는 중입니다.");
            foreach (var player in playerManager.ActivePlayers)
                if (player != null && player.RuntimeData != null) AppendPlayer(player, settings);
            string snapshot = text.ToString();
            bool changed = snapshot != CurrentSnapshot;
            CurrentSnapshot = snapshot;
            // CurrentSnapshot is the persistent view. Console history is opt-in; changing values cannot collapse.
            if (forceSnapshot || (logStateChanges && changed))
                WriteConsole(forceSnapshot ? "[QA][현재 상태]" : "[QA][상태 변경 기록]", CurrentSnapshot);

            text.Clear();
            if (logGameplayEvents)
            {
                foreach (string item in events) text.AppendLine(item);
                if (droppedEvents > 0) text.AppendLine($"[생략] 특별 행동이 많아 오래된 {droppedEvents}개 생략");
                if (text.Length > 0) WriteConsole("[QA][특별 행동]", text.ToString());
            }
            events.Clear(); droppedEvents = 0;
            if (historyDirty)
            {
                text.Clear();
                foreach (string item in history) text.AppendLine(item);
                RecentEvents = text.ToString();
                historyDirty = false;
            }
            forceSnapshot = false;
        }
#endif
    }
    private static string GetMatchModeDisplay(GameStartSettings settings)
    {
        if (settings == null || !settings.isTeamMode) return "개인전";
        switch (settings.teamMode)
        {
            case TeamMode.TeamMode1: return "팀전 / 2인1팀";
            case TeamMode.TeamMode2: return "팀전 / 2대2";
            default: return $"팀전 / 알 수 없음 ({(int)settings.teamMode})";
        }
    }
    private void WriteConsole(string label, string body)
    {
        Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, this, "{0}\n{1}", label, body);
        ConsoleBatchCount++;
    }
    private void AppendTurn()
    {
        text.Append("[턴 순서] ");
        if (turnManager?.TurnOrder?.order != null)
        {
            bool first = true;
            foreach (var player in turnManager.TurnOrder.order)
            {
                if (!first) text.Append(" → ");
                text.Append(player); first = false;
            }
        }
        text.AppendLine();
        var turn = turnManager != null ? turnManager.CurrentTurn : null;
        if (turn == null) { text.AppendLine("[턴 진행] 초기화 대기"); return; }
        RecordPhase(turn);
        text.AppendLine($"[턴 진행] 라운드={turn.roundNumber}, 전체턴={turn.turnNumber}, 현재={turn.currentPlayer}/{turn.currentTeam}, " +
            $"단계={turn.currentPhase}, 종료={turn.isGameEnded}, 낙취소={turn.isTurnCanceledByNak}");
        text.Append("  진행 흐름: ");
        bool firstPhase = true;
        foreach (var phase in phaseHistory)
        {
            if (!firstPhase) text.Append(" → ");
            text.Append(phase); firstPhase = false;
        }
        text.AppendLine();
        text.Append($"  최근윷={turn.lastYutResult}; 남은결과=");
        foreach (var result in turnManager.PendingResults) text.Append(result.result).Append(' ');
        text.AppendLine($"; 남은던지기: 기본={turnManager.HasPendingRegularThrow}, 잡기={turnManager.PendingCaptureThrows}, 스킬={turnManager.PendingSkillThrows}");
        if (winManager != null && winManager.IsTieBreakerActive) text.AppendLine("  타이브레이커 진행 중");
    }
    private void AppendPlayer(PlayerController player, GameStartSettings settings)
    {
        int board = 0, waiting = 0, finished = 0;
        var characterData = player.SelectedCharacter;
        foreach (var piece in player.RuntimeData.Pieces)
        {
            if (piece.State == PieceState.InBoard) board++;
            else if (piece.State == PieceState.Waiting) waiting++;
            else if (piece.State == PieceState.Goal) finished++;
            if (characterData == null && CharacterSkillRegistry.TryGet(player.PlayerId, piece.PieceId, out var status))
                characterData = status.Data;
        }
        var team = settings != null ? MatchCompositionRule.GetTeamSlot(settings, (PlayerSlot)player.PlayerId) : TeamSlot.None;
        text.AppendLine();
        text.Append($"[P{player.PlayerId}] {(characterData != null ? characterData.char_Name : "미등록")} | " +
            $"SP={CharacterSkillRegistry.GetSkillPoints(player.PlayerId)}/{CharacterSkillRegistry.GetMaxSkillPoints(player.PlayerId)} | ");
        int total = player.RuntimeData.Pieces.Count;
        if (settings != null && settings.gameMode == GameMode.Classic)
            text.Append($"완주={finished}/{total} | 남은말={total - finished} (보드={board}, 대기={waiting})");
        else if (settings != null && settings.gameMode == GameMode.Escape)
            text.Append($"{(settings.isTeamMode ? "팀점수" : "점수")}={(winManager != null ? winManager.GetEscapeCount(team).ToString() : "조회 대기")}/{settings.targetEscapeCount} | 보드={board}, 대기={waiting}");
        else
            text.Append($"보드={board}, 대기={waiting}, 완주={finished}/{total}");
        text.Append($" | 팀={team}");
        if (characterData != null)
            text.Append($" | 액티브CD={CharacterSkillRegistry.GetRemainingActiveCooldown(player.PlayerId, characterData)}");
        text.AppendLine();
        foreach (var piece in player.RuntimeData.Pieces)
            if (piece.State == PieceState.InBoard) AppendPiece(player, piece, team);
        if (board == 0) text.AppendLine("  보드 위 말 없음");
    }
    private void AppendPiece(PlayerController player, PlayerRuntimeData.PieceRuntimeData piece, TeamSlot team)
    {
        CharacterSkillRegistry.TryGet(player.PlayerId, piece.PieceId, out var skill);
        text.Append($"  말{piece.PieceId + 1}(id={piece.PieceId}) | 상태={piece.State}, 위치={piece.CurrentTileId}, 이전={piece.PreviousTileId}");
        if (pieceMovementManager != null &&
            pieceMovementManager.TryGetRemainingStepsToGoal(player.PlayerId, piece.PieceId, out int remaining))
            text.Append($", 남은칸={remaining}");
        text.Append(piece.IsStacked ? $", 업기그룹={piece.StackGroupId}, 선두=P{piece.StackLeaderPlayerId}/말{piece.StackLeaderPieceId + 1}" : ", 업기=-");
        if (skill != null)
            text.Append($", 패시브CD={skill.RemainingPassiveCooldownTurns}, 스킬사용가능={CharacterSkillRegistry.CanUseActive(player.PlayerId, piece.PieceId)}");
        text.Append(", CC=[");
        foreach (var cc in piece.Cc.Effects)
        {
            text.Append($"{cc.SkillId}(턴={cc.RemainingOwnerTurns}, 값={cc.Value}, 횟수={cc.Charges}, 출처=P{cc.SourcePlayerId}/id={cc.SourcePieceId}");
            if (cc.Path.Count > 0) { text.Append(", 경로="); foreach (var tile in cc.Path) text.Append(tile).Append('>'); }
            text.Append(") ");
        }
        text.AppendLine("]");
        AppendPieceDetails(text, player.RuntimeData, piece, team);
    }
    // Additional line only: the original status, distance score, stack, cooldown and CC fields stay intact.
    internal static void AppendPieceDetails(StringBuilder output, PlayerRuntimeData owner,
        PlayerRuntimeData.PieceRuntimeData piece, TeamSlot team)
    {
        output.Append($"    팀={team}, 골인까지=");
        if (PieceMovementManager.TryCountMinimumGoalSteps(piece.CurrentTileId, piece.PreviousTileId, out int remaining))
            output.Append(remaining).Append("칸(최단)");
        else
            output.Append("조회불가");
        if (!piece.IsStacked)
        {
            output.AppendLine(", 업기역할=단독, 리더말=-, 업힌말=[], 업힌수=0, 그룹전체=1");
            return;
        }

        int total = 0, carried = 0;
        bool leaderPresent = false;
        var members = piece.GetStackMembers();
        foreach (var member in members)
        {
            if (!IsSameBoardStack(piece, member)) continue;
            total++;
            if (member.IsStackLeader) leaderPresent = true;
            else carried++;
        }
        output.Append(piece.IsStackLeader ? ", 업기역할=리더" : ", 업기역할=업힌 말");
        output.Append($", 리더말=P{piece.StackLeaderPlayerId}/말{piece.StackLeaderPieceId + 1}(id={piece.StackLeaderPieceId})");
        if (!leaderPresent) output.Append("[보드에서 찾지 못함]");
        output.Append(", 업힌말=[");
        bool first = true;
        foreach (var member in members)
        {
            if (!IsSameBoardStack(piece, member) || member.IsStackLeader) continue;
            if (!first) output.Append(", ");
            output.Append($"P{member.OwnerPlayerId}/말{member.PieceId + 1}(id={member.PieceId})");
            first = false;
        }
        output.AppendLine($"], 업힌수={carried}, 그룹전체={total}");
    }
    private static bool IsSameBoardStack(PlayerRuntimeData.PieceRuntimeData piece, PlayerRuntimeData.PieceRuntimeData member) =>
        member.State == PieceState.InBoard && piece.IsInSameStack(member);
}
