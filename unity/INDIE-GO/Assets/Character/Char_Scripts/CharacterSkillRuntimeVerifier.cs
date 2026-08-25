#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
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
                ok = skill.PassiveStatus == CharacterSkillStatus.None;
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
                skill.OnAnyPieceMoveCompleted(new CharacterMoveRecord(
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
                int markedPlayer = GetPrivateInt(skill, "markedPlayerId");
                int markedPiece = GetPrivateInt(skill, "markedPieceId");
                int pointsBefore = CharacterSkillRegistry.GetSkillPoints(1);
                skill.OnCaptureCompleted(new CharacterCaptureRequest(
                    1, 0, markedPlayer, markedPiece, 1, true));
                ok = markedPlayer == 2 && markedPiece >= 0 &&
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
#endif
