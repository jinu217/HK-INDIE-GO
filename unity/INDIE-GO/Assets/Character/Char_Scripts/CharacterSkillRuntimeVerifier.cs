#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using YutArena.Common;
using YutArena.InGame;
using YutArena.Managers;
using YutArena.Managers.GameProgress;

/// <summary>
/// Editor-only integration verifier for the character prefabs. It is completely
/// inert unless the temp-file flag is present, so normal play and builds are not
/// affected. The verifier intentionally lives under Assets/Character because it
/// validates only character-owned assets while exercising the public in-game API.
/// </summary>
[DefaultExecutionOrder(32000)]
internal sealed class CharacterSkillRuntimeVerifier : MonoBehaviour
{
    private const string FlagFileName = "indiego-character-runtime-verifier.flag";
    private const string ReportFileName = "indiego-character-runtime-verifier.log";
    private const string PrefabRoot = "Assets/Character/Char_Prefabs/";

    private static readonly string[] CharacterIds =
    {
        "CHAR_001_1", "CHAR_001_2", "CHAR_002", "CHAR_003", "CHAR_004",
        "CHAR_005", "CHAR_006", "CHAR_007", "CHAR_008", "CHAR_009",
        "CHAR_010", "CHAR_018", "CHAR_019"
    };

    private readonly List<string> report = new List<string>();
    private PlayerManager players;
    private PieceMovementManager movement;
    private TestTurnManager turns;
    private TestWinConditionManager wins;
    private TestGameManager game;
    private int passed;
    private int failed;

    private static string FlagPath => Path.Combine(Path.GetTempPath(), FlagFileName);
    private static string ReportPath => Path.Combine(Path.GetTempPath(), ReportFileName);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallWhenRequested()
    {
        if (SceneManager.GetActiveScene().name != "InGameScene" || !File.Exists(FlagPath))
            return;

        File.Delete(FlagPath);
        new GameObject(nameof(CharacterSkillRuntimeVerifier))
            .AddComponent<CharacterSkillRuntimeVerifier>();
    }

    private IEnumerator Start()
    {
        yield return null;
        yield return null;

        players = FindFirstObjectByType<PlayerManager>();
        movement = FindFirstObjectByType<PieceMovementManager>();
        turns = FindFirstObjectByType<TestTurnManager>();
        wins = FindFirstObjectByType<TestWinConditionManager>();
        game = FindFirstObjectByType<TestGameManager>();

        InGamePieceDebugController debugController =
            FindFirstObjectByType<InGamePieceDebugController>();
        if (debugController != null) debugController.enabled = false;

        Check("Scene managers are available",
            players != null && movement != null && turns != null && wins != null && game != null);
        if (failed > 0)
        {
            Finish();
            yield break;
        }

        foreach (string characterId in CharacterIds)
        {
            yield return InstallCharacterForEveryPlayer(characterId);
            VerifyRegistration(characterId);
            VerifyPassive(characterId);
            VerifyActive(characterId);
        }

        VerifyClassicEndGame();
        Finish();
    }

    private IEnumerator InstallCharacterForEveryPlayer(string characterId)
    {
        foreach (PlayerController player in players.ActivePlayers)
        {
            CharacterStatusBehaviour[] existing =
                player.GetComponentsInChildren<CharacterStatusBehaviour>(true);
            foreach (CharacterStatusBehaviour behaviour in existing)
            {
                if (behaviour != null) Destroy(behaviour.gameObject);
            }
            player.RuntimeData.ResetPieces();
        }

        yield return null;

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            PrefabRoot + characterId + ".prefab");
        Check(characterId + " prefab loads", prefab != null);
        if (prefab == null) yield break;

        foreach (PlayerController player in players.ActivePlayers)
        {
            for (int pieceId = 0; pieceId < player.RuntimeData.Pieces.Count; pieceId++)
                Instantiate(prefab, player.transform);
        }

        yield return null;
        yield return null;
    }

    private void VerifyRegistration(string characterId)
    {
        bool allRegistered = true;
        Type expectedType = Type.GetType(characterId + "_Status, Assembly-CSharp");
        foreach (PlayerController player in players.ActivePlayers)
        {
            for (int pieceId = 0; pieceId < player.RuntimeData.Pieces.Count; pieceId++)
            {
                allRegistered &= CharacterSkillRegistry.TryGet(
                    player.PlayerId, pieceId, out CharacterStatusBehaviour behaviour) &&
                    behaviour != null && behaviour.GetType() == expectedType &&
                    behaviour.Data != null;
            }
        }
        Check(characterId + " registers four pieces for every player", allRegistered);
    }

    private void VerifyPassive(string characterId)
    {
        ResetBoard();
        CharacterSkillRegistry.TryGet(1, 0, out CharacterStatusBehaviour skill);
        PlayerController p1 = players.ActivePlayers[0];
        PlayerController p2 = players.ActivePlayers[1];
        PlayerRuntimeData.PieceRuntimeData caster = p1.RuntimeData.Pieces[0];
        PlayerRuntimeData.PieceRuntimeData ally = p1.RuntimeData.Pieces[1];
        PlayerRuntimeData.PieceRuntimeData enemy = p2.RuntimeData.Pieces[0];
        caster.MoveTo(BoardTileId.Outer01);
        ally.MoveTo(BoardTileId.Outer03);
        enemy.MoveTo(BoardTileId.Outer02);

        var capture = new CharacterCaptureRequest(2, 0, 1, 0, 1, true);
        bool ok;
        switch (characterId)
        {
            case "CHAR_001_1":
                ok = skill.ModifyMoveCount(new CharacterMoveRequest(1, 0, 2, true)) == 3;
                break;
            case "CHAR_001_2":
                int garamPointsBefore = CharacterSkillRegistry.GetSkillPoints(1);
                skill.OnCaptureCompleted(new CharacterCaptureRequest(
                    1, 0, 2, 0, 1, true));
                int garamStackId = p1.RuntimeData.CreateStackGroupId();
                caster.SetStackGroup(garamStackId, ally.PieceId);
                ally.SetStackGroup(garamStackId, ally.PieceId);
                skill.OnMoveCompleted(new CharacterMoveRecord(
                    1, 0, BoardTileId.Outer02, BoardTileId.Outer03,
                    new[] { BoardTileId.Outer03 }));
                skill.OnMoveCompleted(new CharacterMoveRecord(
                    1, 0, BoardTileId.Outer02, BoardTileId.Outer03,
                    new[] { BoardTileId.Outer03 }));
                ok = skill.PassiveStatus == CharacterSkillStatus.Get_point &&
                     CharacterSkillRegistry.GetSkillPoints(1) == garamPointsBefore + 2;
                break;
            case "CHAR_002":
                skill.OnPieceEnteredBoard();
                CharacterSkillRegistry.TryGet(1, 1, out CharacterStatusBehaviour protectedAlly);
                ok = protectedAlly.EvaluateIncomingCapture(
                    new CharacterCaptureRequest(2, 0, 1, 1, 1, true)) ==
                    CharacterCaptureDecision.Prevent;
                break;
            case "CHAR_003":
                ok = skill.EvaluateIncomingCapture(capture) ==
                     CharacterCaptureDecision.LimitRetireToAttackingCount;
                break;
            case "CHAR_004":
                ok = skill.EvaluateIncomingCapture(capture) == CharacterCaptureDecision.Prevent;
                break;
            case "CHAR_005":
                (YutResult, float)[] table = skill.ModifyYutProbability(new[]
                {
                    (YutResult.Yut, 10f), (YutResult.Mo, 10f), (YutResult.BackDo, 4f)
                });
                ok = Array.TrueForAll(table, entry => entry.Item1 != YutResult.BackDo);
                break;
            case "CHAR_006":
                ok = false;
                UnityEngine.Random.InitState(6006);
                for (int i = 0; i < 128 && !ok; i++)
                    ok = skill.EvaluateIncomingCapture(capture) == CharacterCaptureDecision.Prevent;
                break;
            case "CHAR_007":
                int before = CharacterSkillRegistry.GetSkillPoints(1);
                skill.OnCaptureCompleted(new CharacterCaptureRequest(1, 0, 2, 0, 1, true));
                ok = CharacterSkillRegistry.GetSkillPoints(1) == before + 1;
                break;
            case "CHAR_008":
                skill.OnMoveCompleted(new CharacterMoveRecord(
                    1, 0, BoardTileId.Outer01, BoardTileId.Outer02,
                    new[] { BoardTileId.Outer02 }));
                skill.OnOwnerTurnEnded();
                ally.Reset();
                ally.MoveTo(BoardTileId.Outer02);
                CharacterSkillRegistry.NotifyMoveCompleted(new CharacterMoveRecord(
                    1, 1, BoardTileId.Outer01, BoardTileId.Outer02,
                    new[] { BoardTileId.Outer02 }));
                ok = ally.CurrentTileId == BoardTileId.Outer03;
                break;
            case "CHAR_009":
                ok = skill.EvaluateIncomingCapture(capture) ==
                         CharacterCaptureDecision.ConvertToParts &&
                     !skill.IsTargetable &&
                     skill.EvaluateIncomingCapture(capture) ==
                         CharacterCaptureDecision.Prevent;
                break;
            case "CHAR_010":
                int stackId = p1.RuntimeData.CreateStackGroupId();
                caster.SetStackGroup(stackId, 0);
                ally.SetStackGroup(stackId, 0);
                ok = skill.ModifyMoveCount(new CharacterMoveRequest(1, 0, 2, false)) == 3;
                break;
            case "CHAR_018":
                skill.OnPieceEnteredBoard();
                int pointsBefore = CharacterSkillRegistry.GetSkillPoints(1);
                CcState mark = enemy.Cc.Get(CcDefine.Mark);
                CcBoardEffects.TryCapture(1, 0, new CharacterPieceReference(p2, enemy),
                    1, true, out _);
                ok = mark != null && mark.SourcePlayerId == 1 && mark.SourcePieceId == 0 &&
                     CharacterSkillRegistry.GetSkillPoints(1) == pointsBefore + 1;
                break;
            case "CHAR_019":
                skill.OnOwnerTurnStarted();
                ok = skill.ShouldGrantExtraThrow(YutResult.Yut, false);
                break;
            default:
                ok = false;
                break;
        }
        Check(characterId + " passive", ok);
    }

    private void VerifyActive(string characterId)
    {
        ResetBoard();
        CharacterSkillRegistry.TryGet(1, 0, out CharacterStatusBehaviour skill);
        PlayerController p1 = players.ActivePlayers[0];
        PlayerController p2 = players.ActivePlayers[1];
        PlayerRuntimeData.PieceRuntimeData caster = p1.RuntimeData.Pieces[0];
        caster.MoveTo(BoardTileId.Outer01);
        p2.RuntimeData.Pieces[0].MoveTo(BoardTileId.Outer01);
        p2.RuntimeData.Pieces[1].MoveTo(BoardTileId.Outer02);

        turns.CurrentTurn.currentPlayer = PlayerSlot.Player1;
        turns.CurrentTurn.currentTeam = TeamSlot.TeamA;
        turns.CurrentTurn.currentPhase =
            characterId == "CHAR_001_1" || characterId == "CHAR_019"
                ? TurnPhase.WaitThrow
                : TurnPhase.WaitAction;

        int skillPointCost = skill.ActiveSkillPointCost;
        int currentSkillPoints = CharacterSkillRegistry.GetSkillPoints(1);
        if (currentSkillPoints < skillPointCost)
            CharacterSkillRegistry.RequestSkillPoint(1, skillPointCost - currentSkillPoints);
        int skillPointsBeforeUse = CharacterSkillRegistry.GetSkillPoints(1);

        CharacterActiveResult result = CharacterSkillRegistry.TryUseActive(
            new CharacterActiveRequest(1, 0, 2, 0, YutResult.Do));
        Check(characterId + " active succeeds", result.Succeeded, result.Message);
        Check(characterId + " active spends configured SP",
            CharacterSkillRegistry.GetSkillPoints(1) == skillPointsBeforeUse - skillPointCost);
        if (characterId == "CHAR_004")
        {
            CharacterCaptureDecision hiddenCapture = skill.EvaluateIncomingCapture(
                new CharacterCaptureRequest(2, 0, 1, 0, 1, true));
            Check("CHAR_004 hide blocks ordinary landing capture",
                !skill.IsTargetable && hiddenCapture == CharacterCaptureDecision.Prevent);
        }
        if (characterId == "CHAR_007")
        {
            Check("CHAR_007 uses SP instead of a turn cooldown",
                skill.ActiveSkillPointCost == 3 && skill.ActiveCooldownTurns == 0);
            CharacterActiveResult secondUse = CharacterSkillRegistry.TryUseActive(
                new CharacterActiveRequest(1, 0, 2, 0, YutResult.Do));
            Check("CHAR_007 cannot be reused without another 3 SP",
                !secondUse.Succeeded &&
                secondUse.Message.Contains("requires 3 skill point"),
                secondUse.Message);
        }
        Check(characterId + " cooldown is isolated per player",
            CharacterSkillRegistry.GetRemainingActiveCooldown(2, skill.Data) == 0);
    }

    private void VerifyClassicEndGame()
    {
        ResetBoard();
        var settings = new GameStartSettings
        {
            gameMode = GameMode.Classic,
            mapType = MapType.Basic,
            matchComposition = MatchComposition.OneVsOne,
            playerCount = 2,
            pieceCountPerPlayer = 4,
            turnTimeMode = TurnTimeMode.Unlimited,
            useSkill = true
        };

        bool ended = false;
        GameResultData result = null;
        Action<GameResultData> handler = value =>
        {
            ended = true;
            result = value;
        };
        game.OnGameEnded += handler;
        EnsureClassicRuleForVerification();
        wins.Initialize(settings);

        PlayerController p1 = players.ActivePlayers[0];
        for (int pieceId = 0; pieceId < p1.RuntimeData.Pieces.Count; pieceId++)
        {
            int before = CountGoals(p1);
            bool moved = movement.TryMovePiece(1, pieceId, 21, true);
            int newlyFinished = CountGoals(p1) - before;
            wins.OnPieceMoveResolved(
                PlayerSlot.Player1, TeamSlot.TeamA, newlyFinished > 0, newlyFinished);
            Check("Classic goal move for piece " + pieceId, moved && newlyFinished == 1);
        }

        game.OnGameEnded -= handler;
        Check("Classic reaches EndGame",
            ended && result != null && result.winningTeam == TeamSlot.TeamA &&
            game.Session.phase == GamePhase.Result);
    }

    private void EnsureClassicRuleForVerification()
    {
        FieldInfo rulesField = typeof(TestWinConditionManager).GetField(
            "modeRules", BindingFlags.Instance | BindingFlags.NonPublic);
        var rules = rulesField?.GetValue(wins) as Dictionary<GameMode, IGameModeRule>;
        bool sceneHasClassic = rules != null && rules.ContainsKey(GameMode.Classic);
        Check("InGameScene has a ClassicModeRule source", sceneHasClassic,
            "Missing scene wiring; injecting an editor-only runtime fallback for the remaining test.");

        if (rules != null && !sceneHasClassic)
            rules[GameMode.Classic] = gameObject.AddComponent<ClassicModeRule>();
    }

    private void ResetBoard()
    {
        foreach (PlayerController player in players.ActivePlayers)
            player.RuntimeData.ResetPieces();
    }

    private static int CountGoals(PlayerController player)
    {
        int count = 0;
        foreach (PlayerRuntimeData.PieceRuntimeData piece in player.RuntimeData.Pieces)
            if (piece.State == PieceState.Goal) count++;
        return count;
    }

    private static int GetPrivateInt(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        return field != null ? (int)field.GetValue(target) : -1;
    }

    private void Check(string name, bool condition, string detail = "")
    {
        if (condition)
        {
            passed++;
            report.Add("PASS | " + name);
            Debug.Log("[CharacterVerification][PASS] " + name, this);
        }
        else
        {
            failed++;
            string line = "FAIL | " + name +
                          (string.IsNullOrWhiteSpace(detail) ? "" : " | " + detail);
            report.Add(line);
            Debug.LogError("[CharacterVerification] " + line, this);
        }
    }

    private void Finish()
    {
        report.Insert(0, $"SUMMARY | Passed={passed} Failed={failed}");
        File.WriteAllLines(ReportPath, report);
        Debug.Log($"[CharacterVerification][DONE] Passed={passed}, Failed={failed}, " +
                  $"Report={ReportPath}", this);
    }
}

// 저장된 씬/SO를 변경하지 않는 명시적 Edit Mode 회귀 검증입니다.
[InitializeOnLoad]
internal static class CcArchitectureVerifier
{
    private const string Flag = "Temp/verify-cc.flag";
    private const string Report = "Temp/cc-verification.log";
    private static readonly List<string> results = new List<string>();
    static CcArchitectureVerifier()
    {
        EditorApplication.delayCall += () =>
        {
            if (!File.Exists(Flag)) return;
            File.Delete(Flag);
            Run();
        };
    }

    [MenuItem("Tools/Character/Verify CC Pipeline")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            UnityEngine.Object.FindFirstObjectByType<PlayerManager>() != null)
        {
            Debug.LogWarning("CC 검증은 PlayerManager가 없는 씬에서 Edit Mode로 실행하세요.");
            return;
        }
        results.Clear();
        Scene previous = SceneManager.GetActiveScene();
        Scene fixture = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        var copies = new List<CharacterData>();
        try
        {
            ResetRegistry();
            var manager = new GameObject("CC Test Players").AddComponent<PlayerManager>();
            var movement = new GameObject("CC Test Movement").AddComponent<PieceMovementManager>();
            Set(movement, "playerManager", manager);
            var turns = new GameObject("CC Test Turns").AddComponent<TestTurnManager>();
            Set(turns, "playerManager", manager);
            Set(turns, "pieceMovementManager", movement);
            var p1 = new GameObject("CC Test P1").AddComponent<PlayerController>();
            var p2 = new GameObject("CC Test P2").AddComponent<PlayerController>();
            p1.Initialize(1, "Test 1", 4);
            p2.Initialize(2, "Test 2", 4);
            var players = (List<PlayerController>)Get(manager, "activePlayers");
            players.Add(p1); players.Add(p2);
            var piece = p1.RuntimeData.Pieces[0];
            piece.MoveTo(BoardTileId.Outer01);
            Check("Legacy enum values preserved", (int)CcDefine.Stun == 1 && (int)CcDefine.Kill == 4);
            manager.TryApplyPieceCc(1, 0, CcDefine.Stun, 2);
            manager.TryApplyPieceCc(1, 0, CcDefine.Silence, 3);
            manager.TryApplyPieceCc(1, 0, CcDefine.Protection, 4);
            Check("Manager stores all simultaneous effects", piece.Cc.Effects.Count == 3);
            Check("Stun blocks direct movement", !movement.TryMovePiece(1, 0, 1));
            CcEffectService.TickOwnerTurn(p1);
            Check("CC decrements independently", piece.Cc.Get(CcDefine.Stun).RemainingOwnerTurns == 1 &&
                piece.Cc.Get(CcDefine.Silence).RemainingOwnerTurns == 2);
            CcEffectService.TickOwnerTurn(p1);
            Check("Stun expires without removing Silence or Protection", !piece.Cc.Has(CcDefine.Stun) &&
                piece.Cc.Has(CcDefine.Silence) && piece.Cc.Has(CcDefine.Protection));
            Check("Silence allows movement but blocks active", CcEffectService.CanMove(piece) &&
                !CcEffectService.CanUseSkill(piece));
            CcEffectService.Apply(piece, CcDefine.Kill);
            Check("Capture resets position and replaces effects", piece.State == PieceState.Waiting &&
                piece.CurrentTileId == BoardTileId.None && piece.Cc.Effects.Count == 1);
            Check("Kill is consumed exactly once", CcEffectService.ConsumeCapture(piece) &&
                !CcEffectService.ConsumeCapture(piece));
            CcEffectService.Apply(piece, CcDefine.Retire);
            Check("Retire grants no bonus", !CcEffectService.ConsumeCapture(piece));
            piece.MoveTo(BoardTileId.Outer01);
            var carried = p1.RuntimeData.Pieces[1]; carried.MoveTo(BoardTileId.Outer01);
            piece.SetStackGroup(50, 0); carried.SetStackGroup(50, 0);
            CcEffectService.Apply(piece, CcDefine.Parts, 3);
            Check("Parts leader detaches without stranding ally", !piece.IsStacked && !carried.IsStacked);
            piece.ClearCc(); piece.SetStackGroup(51, 0); carried.SetStackGroup(51, 0);
            CcEffectService.Apply(piece, CcDefine.Retire);
            Check("Retired leader does not strand surviving ally", !carried.IsStacked && CcEffectService.CanUseSkill(carried));

            string[] ids = { "001_1", "001_2", "002", "003", "004", "005", "006",
                "007", "008", "009", "010", "018", "019" };
            foreach (string id in ids)
            {
                p1.RuntimeData.ResetPieces(); p2.RuntimeData.ResetPieces();
                ResetRegistry();
                turns.OnTurnStarted = null; turns.OnTurnEnded = null;
                Set(turns, "pendingSkillThrows", 0); Set(turns, "pendingCaptureThrows", 0);
                turns.CurrentTurn.currentPlayer = PlayerSlot.Player1;
                turns.CurrentTurn.currentPhase = id == "001_1" || id == "019"
                    ? TurnPhase.WaitThrow : TurnPhase.WaitAction;
                var host = new GameObject("CC Test Character " + id);
                host.transform.SetParent(p1.transform);
                var skill = (CharacterStatusBehaviour)host.AddComponent(Type.GetType("CHAR_" + id + "_Status, Assembly-CSharp"));
                var asset = AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/Character/Char_Info/CHAR_" + id + "_SO.asset");
                Check(id + " original SO loads", asset != null);
                var data = UnityEngine.Object.Instantiate(asset);
                data.visualModelPrefab = null;
                data.active_CooldownTurns = 2; data.active_SkillPointCost = 1;
                copies.Add(data); skill.Initialize(data);
                typeof(CharacterStatusBehaviour).GetMethod("TryRegisterRuntime", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(skill, null);
                skill.OnOwnerTurnStarted();
                piece.MoveTo(BoardTileId.Outer01);
                var enemy = p2.RuntimeData.Pieces[0];
                enemy.MoveTo(id == "007" || id == "005" ? BoardTileId.Outer02 : BoardTileId.Outer01);
                CharacterSkillRegistry.RequestSkillPoint(1, 1);
                CcEffectService.Apply(piece, CcDefine.Silence, 2);
                Check(id + " active rejects Silence", !CharacterSkillRegistry.TryUseActive(
                    new CharacterActiveRequest(1, 0, 2, 0)).Succeeded);
                Check(id + " rejected active spends no SP/cooldown", CharacterSkillRegistry.GetSkillPoints(1) == 1 &&
                    CharacterSkillRegistry.GetRemainingActiveCooldown(1, data) == 0);
                CcEffectService.Remove(piece, CcDefine.Silence);
                var result = CharacterSkillRegistry.TryUseActive(new CharacterActiveRequest(1, 0, 2, 0));
                Check(id + " active executes through registry", result.Succeeded, result.Message);
                Check(id + " success spends SP and shares cooldown", CharacterSkillRegistry.GetSkillPoints(1) == 0 &&
                    CharacterSkillRegistry.GetRemainingActiveCooldown(1, data) == 2);
                switch (id)
                {
                    case "001_1":
                        Check("DoOrMo stored on piece", piece.Cc.Has(CcDefine.DoOrMo));
                        var table = skill.ModifyYutProbability(new[] { (YutResult.Gae, 100f) });
                        Check("DoOrMo consumed on next throw", table.Length == 2 && !piece.Cc.Has(CcDefine.DoOrMo));
                        skill.ShouldGrantExtraThrow(YutResult.Do, false);
                        Check("DoOrMo prohibited after first throw", !skill.IsActiveUsableInCurrentPhase());
                        break;
                    case "001_2":
                        Check("ExtraThrow schedules exactly once", (int)Get(turns, "pendingSkillThrows") == 1);
                        break;
                    case "002":
                        Check("DoubleMove is stored then consumed once", piece.Cc.Has(CcDefine.DoubleMove) &&
                            skill.ModifyMoveCount(new CharacterMoveRequest(1, 0, 2, false)) == 4 &&
                            skill.ModifyMoveCount(new CharacterMoveRequest(1, 0, 2, false)) == 2);
                        break;
                    case "003":
                        Check("Clone stored and stacked", piece.Cc.Get(CcDefine.Clone)?.Value == 1 && piece.IsStacked);
                        Check("Clone absorbs capture without bonus", skill.EvaluateIncomingCapture(
                            new CharacterCaptureRequest(2, 0, 1, 0, 1, true)) == CharacterCaptureDecision.ConsumeCloneWithoutBonus &&
                            !piece.Cc.Has(CcDefine.Clone) && !piece.IsStacked);
                        break;
                    case "004":
                        Check("Hidden blocks targeting and capture", !skill.IsTargetable &&
                            skill.EvaluateIncomingCapture(new CharacterCaptureRequest(2, 0, 1, 0, 1, true)) == CharacterCaptureDecision.Prevent);
                        CcEffectService.TickOwnerTurn(p1); CcEffectService.TickOwnerTurn(p1); CcEffectService.TickOwnerTurn(p1);
                        Check("Hidden expires on third owner start", skill.IsTargetable);
                        break;
                    case "005":
                        Check("Issen moves and retires without bonus", piece.CurrentTileId == BoardTileId.Outer04 &&
                            enemy.State == PieceState.Waiting && (int)Get(turns, "pendingCaptureThrows") == 0);
                        break;
                    case "006": case "018":
                        Check(id + " capture processed without UI", enemy.State == PieceState.Waiting &&
                            enemy.CurrentCc == CcDefine.None && (int)Get(turns, "pendingCaptureThrows") == 1);
                        break;
                    case "007": case "008":
                        Check(id + " Stun stored on target", enemy.Cc.Has(CcDefine.Stun));
                        CcEffectService.TickOwnerTurn(p2);
                        Check(id + " target cannot move for one complete turn", !movement.TryMovePiece(2, 0, 1));
                        CcEffectService.TickOwnerTurn(p2);
                        Check(id + " Stun expires", CcEffectService.CanMove(enemy));
                        break;
                    case "009":
                        Check("Self destruct retires caster and enemy", piece.State == PieceState.Waiting && enemy.State == PieceState.Waiting);
                        piece.MoveTo(BoardTileId.Outer01);
                        Check("Parts conversion stored in CC", skill.EvaluateIncomingCapture(new CharacterCaptureRequest(2, 0, 1, 0, 1, true)) ==
                            CharacterCaptureDecision.ConvertToParts && piece.Cc.Has(CcDefine.Parts));
                        Check("Parts cannot move/use skills", !movement.TryMovePiece(1, 0, 1) && !CcEffectService.CanUseSkill(piece));
                        var ally = p1.RuntimeData.Pieces[1]; ally.MoveTo(BoardTileId.Outer02);
                        CharacterSkillRegistry.NotifyMoveCompleted(new CharacterMoveRecord(1, 1, BoardTileId.Outer03,
                            BoardTileId.Outer02, new[] { BoardTileId.Outer02 }));
                        Check("Ally revives Parts via CC", !piece.Cc.Has(CcDefine.Parts) && piece.CurrentTileId == BoardTileId.Outer01);
                        break;
                    case "010": Check("Retreat moves one tile back", piece.CurrentTileId == BoardTileId.None); break;
                    case "019":
                        Check("Reverse throw rule stored and consumed", piece.Cc.Has(CcDefine.ReverseExtraThrow) &&
                            skill.ShouldGrantExtraThrow(YutResult.Do, false) && !piece.Cc.Has(CcDefine.ReverseExtraThrow));
                        break;
                }
                if (id != "004")
                {
                    CharacterSkillRegistry.NotifyOwnerTurnStarted(1);
                    Check(id + " cooldown ticks once per owner", CharacterSkillRegistry.GetRemainingActiveCooldown(1, data) == 1);
                }
                p1.RuntimeData.ResetPieces(); p2.RuntimeData.ResetPieces();
                skill.OnPieceRetired();
                piece.MoveTo(BoardTileId.Outer01);
                enemy.MoveTo(BoardTileId.Outer02);
                var passiveAlly = p1.RuntimeData.Pieces[1];
                passiveAlly.MoveTo(BoardTileId.Outer03);
                switch (id)
                {
                    case "001_1":
                        p1.RuntimeData.ResetPieces(); p2.RuntimeData.ResetPieces();
                        skill.OnPieceRetired();
                        enemy.MoveTo(BoardTileId.Outer03);
                        var secondEnemy = p2.RuntimeData.Pieces[1];
                        secondEnemy.MoveTo(BoardTileId.Outer04);
                        Check("First move keeps original Geol distance then forces Do",
                            movement.TryMovePiece(1, 0, 3) &&
                            piece.CurrentTileId == BoardTileId.Outer04);
                        Check("First landing and forced Do landing both capture",
                            enemy.State == PieceState.Waiting &&
                            secondEnemy.State == PieceState.Waiting);
                        Check("Forced Do does not repeat on the next move",
                            movement.TryMovePiece(1, 0, 1) &&
                            piece.CurrentTileId == BoardTileId.Corner01);

                        p1.RuntimeData.ResetPieces(); p2.RuntimeData.ResetPieces();
                        skill.OnPieceRetired();
                        Check("Retire restores passive and corner landing starts shortcut Do",
                            movement.TryMovePiece(1, 0, 5) &&
                            piece.CurrentTileId == BoardTileId.Inner01);
                        break;
                    case "001_2": case "007":
                        int beforeCapturePoint = CharacterSkillRegistry.GetSkillPoints(1);
                        skill.OnCaptureCompleted(new CharacterCaptureRequest(1, 0, 2, 0, 1, true));
                        Check(id + " passive SkillPoint effect", CharacterSkillRegistry.GetSkillPoints(1) == beforeCapturePoint + 1);
                        break;
                    case "002":
                        skill.OnPieceEnteredBoard();
                        Check("Protection stored on nearest ally with correct source", passiveAlly.Cc.Has(CcDefine.Protection) &&
                            passiveAlly.Cc.Get(CcDefine.Protection).SourcePieceId == 0);
                        Check("Protection works without character component", CharacterSkillRegistry.EvaluateIncomingCapture(
                            new CharacterCaptureRequest(2, 0, 1, 1, 1, true)) == CharacterCaptureDecision.Prevent &&
                            !passiveAlly.Cc.Has(CcDefine.Protection));
                        break;
                    case "003":
                        Check("Capture limit resolves through CC", skill.EvaluateIncomingCapture(
                            new CharacterCaptureRequest(2, 0, 1, 0, 1, true)) == CharacterCaptureDecision.LimitRetireToAttackingCount);
                        break;
                    case "004":
                        Check("Charm passive uses Protection CC", skill.EvaluateIncomingCapture(
                            new CharacterCaptureRequest(2, 0, 1, 0, 1, true)) == CharacterCaptureDecision.Prevent &&
                            skill.EvaluateIncomingCapture(new CharacterCaptureRequest(2, 0, 1, 0, 1, true)) == CharacterCaptureDecision.Proceed);
                        break;
                    case "005":
                        var probability = skill.ModifyYutProbability(new[] { (YutResult.Yut, 10f), (YutResult.Mo, 10f), (YutResult.BackDo, 4f) });
                        Check("BackDo probability redistributed without loss", probability.Length == 2 &&
                            probability[0].Item2 == 12 && probability[1].Item2 == 12);
                        piece.MoveTo(BoardTileId.Outer16);
                        var goalPath = CharacterBoardUtility.GetForwardPath(piece, 3);
                        Check("Skill path stops at goal", goalPath.Count == 2 && goalPath[0] == BoardTileId.None && goalPath[1] == BoardTileId.None);
                        CharacterBoardUtility.MoveStackAlongPath(p1, piece, goalPath);
                        Check("Skill path goals instead of wrapping board", piece.State == PieceState.Goal);
                        break;
                    case "006":
                        var randomState = UnityEngine.Random.state;
                        UnityEngine.Random.InitState(6006);
                        bool dodged = false;
                        for (int i = 0; i < 128 && !dodged; i++)
                            dodged = skill.EvaluateIncomingCapture(new CharacterCaptureRequest(2, 0, 1, 0, 1, true)) == CharacterCaptureDecision.Prevent;
                        UnityEngine.Random.state = randomState;
                        Check("Dodge passive resolves through Protection", dodged);
                        Check("Sword target range validated before confirmation", !skill.CanSelectActiveTarget(2, 1));
                        break;
                    case "008":
                        skill.OnMoveCompleted(new CharacterMoveRecord(1, 0, BoardTileId.Outer01, BoardTileId.Outer02, new[] { BoardTileId.Outer02 }));
                        skill.OnOwnerTurnEnded();
                        Check("Wind path stored in CC", piece.Cc.Has(CcDefine.WindPath));
                        passiveAlly.MoveTo(BoardTileId.Outer02);
                        CharacterSkillRegistry.NotifyMoveCompleted(new CharacterMoveRecord(1, 1, BoardTileId.Outer01, BoardTileId.Outer02, new[] { BoardTileId.Outer02 }));
                        Check("Wind moves ally once through Move CC", passiveAlly.CurrentTileId == BoardTileId.Outer03);
                        CharacterSkillRegistry.NotifyMoveCompleted(new CharacterMoveRecord(1, 1, BoardTileId.Outer01, BoardTileId.Outer02, new[] { BoardTileId.Outer02 }));
                        Check("Wind duplicate notification does not move twice", passiveAlly.CurrentTileId == BoardTileId.Outer03);
                        break;
                    case "009":
                        skill.EvaluateIncomingCapture(new CharacterCaptureRequest(2, 0, 1, 0, 1, true));
                        CcEffectService.TickOwnerTurn(p1); CcEffectService.TickOwnerTurn(p1); CcEffectService.TickOwnerTurn(p1);
                        Check("Unrevived Parts retires on third turn", piece.State == PieceState.Waiting && piece.Cc.Has(CcDefine.Retire));
                        break;
                    case "010":
                        int stack = p1.RuntimeData.CreateStackGroupId();
                        piece.SetStackGroup(stack, 0); passiveAlly.SetStackGroup(stack, 0);
                        Check("Stack bonus resolves through CC", skill.ModifyMoveCount(new CharacterMoveRequest(1, 0, 2, false)) == 3);
                        Check("Carried piece cannot use active", !CcEffectService.CanUseSkill(passiveAlly));
                        break;
                    case "018":
                        skill.OnPieceEnteredBoard();
                        Check("Mark stored on enemy with source", enemy.Cc.Get(CcDefine.Mark)?.SourcePieceId == 0);
                        int markPoints = CharacterSkillRegistry.GetSkillPoints(1);
                        CcBoardEffects.TryCapture(1, 0, new CharacterPieceReference(p2, enemy), 1, true, out _);
                        Check("Actual marked capture grants SP once", CharacterSkillRegistry.GetSkillPoints(1) == markPoints + 1 && !enemy.Cc.Has(CcDefine.Mark));
                        break;
                    case "019":
                        skill.OnOwnerTurnStarted();
                        Check("Extra YutMo passive allowance once per turn", skill.ShouldGrantExtraThrow(YutResult.Yut, false) &&
                            !skill.ShouldGrantExtraThrow(YutResult.Yut, false));
                        break;
                }
                CharacterSkillRegistry.Unregister(skill);
                UnityEngine.Object.DestroyImmediate(host);
            }
        }
        catch (Exception exception) { results.Add("FAIL EXCEPTION " + exception); }
        finally
        {
            ResetRegistry();
            EditorSceneManager.CloseScene(fixture, true);
            if (previous.IsValid()) SceneManager.SetActiveScene(previous);
            foreach (var data in copies) UnityEngine.Object.DestroyImmediate(data);
            File.WriteAllLines(Report, results);
            Debug.Log($"[CC Verification] {results.FindAll(line => line.StartsWith("PASS")).Count} passed, " +
                $"{results.FindAll(line => line.StartsWith("FAIL")).Count} failed. {Report}");
        }
    }
    private static void Check(string name, bool passed, string detail = "") =>
        results.Add((passed ? "PASS " : "FAIL ") + name + " " + detail);
    private static object Get(object target, string field) => target.GetType()
        .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    private static void Set(object target, string field, object value) => target.GetType()
        .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private static void ResetRegistry() => typeof(CharacterSkillRegistry)
        .GetMethod("ResetRuntimeState", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
}

// 런타임 CC를 복사하지 않고 PlayerManager가 관리하는 실제 말 데이터를 표시합니다.
[CustomEditor(typeof(PlayerManager))]
internal sealed class PlayerCcInspector : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var manager = (PlayerManager)target;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Runtime Piece CC (Read Only)", EditorStyles.boldLabel);
        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("플레이 중 각 말의 CC 종류, 남은 턴, 수치, 발동자를 표시합니다.", MessageType.Info);
            return;
        }
        foreach (var player in manager.ActivePlayers)
        {
            EditorGUILayout.LabelField($"Player {player.PlayerId}", EditorStyles.boldLabel);
            foreach (var piece in player.RuntimeData.Pieces)
            {
                EditorGUILayout.LabelField($"Piece {piece.PieceId} / {piece.State}", $"CC: {piece.CurrentCc}");
                using (new EditorGUI.IndentLevelScope())
                foreach (var effect in piece.Cc.Effects)
                    EditorGUILayout.LabelField(effect.Type.ToString(),
                        $"Turns: {effect.RemainingOwnerTurns}, Value: {effect.Value}, " +
                        $"Source: {effect.SourcePlayerId}/{effect.SourcePieceId}");
            }
        }
        Repaint();
    }
}
#endif
