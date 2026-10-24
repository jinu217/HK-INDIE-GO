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
        "CHAR_010"
    };

    private readonly List<string> report = new List<string>();
    private PlayerManager players;
    private PieceMovementManager movement;
    private TestTurnManager turns;
    private TestWinConditionManager wins;
    private TestGameManager game;
    private int passed;
    private int failed;
    private string uiCharacterId;
    private string lastUiState;

    private static string FlagPath => Path.Combine(Path.GetTempPath(), FlagFileName);
    private static string ReportPath => Path.Combine(Path.GetTempPath(), ReportFileName);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallWhenRequested()
    {
        SceneManager.sceneLoaded -= OnVerificationSceneLoaded;
        SceneManager.sceneLoaded += OnVerificationSceneLoaded;
        TryInstallWhenRequested();
    }

    private static void OnVerificationSceneLoaded(Scene scene, LoadSceneMode mode) =>
        TryInstallWhenRequested();

    private static void TryInstallWhenRequested()
    {
        if (SceneManager.GetActiveScene().name == "ChampionPickScene" && File.Exists(FlagPath) &&
            File.ReadAllText(FlagPath).Trim().StartsWith("ui:"))
        {
            var selection = UnityEngine.Object.FindFirstObjectByType<YutArena.UI.CharacterScene.CharacterSelectController>();
            if (selection == null) return;
            var selectionType = selection.GetType();
            var choices = (List<CharacterData>)selectionType.GetField("runtimeCharacters",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(selection);
            string requestedId = File.ReadAllText(FlagPath).Trim().Substring(3);
            int index = choices.FindIndex(data => data.name == requestedId + "_SO");
            if (index >= 0)
            {
                ((int[])selectionType.GetField("cursorIndexes", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(selection))[0] = index;
                ((bool[])selectionType.GetField("selectedPlayers", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(selection))[0] = true;
            }
            return; // Test-only selection; normal game initialization still creates the real views.
        }
        if (SceneManager.GetActiveScene().name != "InGameScene" || !File.Exists(FlagPath))
            return;

        string request = File.ReadAllText(FlagPath).Trim();
        File.Delete(FlagPath);
        if (request.StartsWith("ui:") && Array.IndexOf(CharacterIds, request.Substring(3)) >= 0)
        {
            // Runs from an unsaved direct InGameScene too, before TestRunner.Start initializes players.
            var selected = AssetDatabase.LoadAssetAtPath<CharacterData>(
                "Assets/Character/Char_Info/" + request.Substring(3) + "_SO.asset");
            var opponent = AssetDatabase.LoadAssetAtPath<CharacterData>(
                "Assets/Character/Char_Info/CHAR_004_SO.asset");
            CharacterSelectionResult.Set(new[] { selected.char_ID, opponent.char_ID, opponent.char_ID, opponent.char_ID },
                new[] { selected, opponent, opponent, opponent }, 4);
            LocalSelectionInput.SelectSharedKeyboard();
        }
        var verifier = new GameObject(nameof(CharacterSkillRuntimeVerifier))
            .AddComponent<CharacterSkillRuntimeVerifier>();
        if (request.StartsWith("ui:") && Array.IndexOf(CharacterIds, request.Substring(3)) >= 0)
            verifier.uiCharacterId = request.Substring(3);
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
        if (debugController != null && uiCharacterId == null) debugController.enabled = false;

        Check("Scene managers are available",
            players != null && movement != null && turns != null && wins != null && game != null);
        if (failed > 0)
        {
            Finish();
            yield break;
        }

        if (uiCharacterId != null)
        {
            // Keep the actual selection screen's pieces: their DebugPieceViews
            // are also held by the normal input controller.
            Check("UI uses selected " + uiCharacterId,
                CharacterSkillRegistry.TryGet(1, 0, out var selected) &&
                selected.GetType().Name == uiCharacterId + "_Status");
            if (failed > 0) { Finish(); yield break; }
            var first = players.ActivePlayers[0];
            first.RuntimeData.Pieces[0].MoveTo(BoardTileId.Outer01);
            players.ActivePlayers[1].RuntimeData.Pieces[0].MoveTo(BoardTileId.Outer03);
            turns.Settings.turnTimeMode = TurnTimeMode.Unlimited;
            typeof(TestTurnManager).GetMethod("StopTurnTimer", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(turns, null);
            turns.CurrentTurn.currentPlayer = PlayerSlot.Player1;
            turns.CurrentTurn.currentPhase = uiCharacterId == "CHAR_001_2" ||
                uiCharacterId == "CHAR_001_1" || uiCharacterId == "CHAR_002"
                    ? TurnPhase.WaitThrow : TurnPhase.WaitAction;
            CharacterSkillRegistry.RequestSkillPoint(1, 5);
            turns.OnTurnStarted?.Invoke(PlayerSlot.Player1);
            turns.OnTurnPhaseChanged?.Invoke(turns.CurrentTurn);
            var button = FindFirstObjectByType<ActiveSkillButtonController>();
            button?.RefreshForCurrentTurn();
            // Unlimited turn timers keep the fixture stable without freezing throw/move animations.
            Debug.Log("[CharacterVerification][UI Ready] " + uiCharacterId);
            yield break;
        }

        foreach (string characterId in CharacterIds)
        {
            yield return InstallCharacterForEveryPlayer(characterId);
            VerifyRegistration(characterId);
            VerifyPassive(characterId);
            VerifyActive(characterId);
        }

        yield return VerifyMixedCharacterInteractions();
        VerifyClassicEndGame();
        Finish();
    }

    private void Update()
    {
        if (uiCharacterId == null || players == null) return;
        var button = FindFirstObjectByType<ActiveSkillButtonController>();
        var normalInput = FindFirstObjectByType<InGamePieceDebugController>();
        if (button == null) return;
        var piece = players.ActivePlayers[0].RuntimeData.Pieces[0];
        var markers = FindObjectsByType<MoveMarker>(FindObjectsSortMode.None);
        string state = $"CasterSelection={button.IsSelectingCaster}, TargetSelection={button.IsSelectingTarget}, " +
            $"NormalInput={normalInput != null && normalInput.enabled}, " +
            $"SP={CharacterSkillRegistry.GetSkillPoints(1)}, Clones={piece.Cc.Get(CcDefine.Stack_fake)?.Value ?? 0}, " +
            $"Phase={turns.CurrentTurn.currentPhase}, Pending={turns.PendingResults.Count}, " +
            $"SkillThrows={GetPrivateInt(turns, "pendingSkillThrows")}, Caster={button.SelectedCasterPieceId}, " +
            $"P1Piece0={piece.State}/{piece.CurrentTileId}, Hints={markers.Length}";
        if (state == lastUiState) return;
        lastUiState = state;
        Debug.Log("[CharacterVerification][UI State] " + state);
        foreach (var marker in markers)
            if (marker.Role == MoveMarkerRole.PieceSelect && Camera.main != null)
                Debug.Log($"[CharacterVerification][Hint] Piece={marker.PieceId}, " +
                    $"Screen={Camera.main.WorldToScreenPoint(marker.transform.position)}, " +
                    $"World={marker.transform.position}");
    }

    private void OnDestroy()
    {
        if (uiCharacterId != null) Time.timeScale = 1f;
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

    private IEnumerator InstallMatchup(string firstCharacterId, string secondCharacterId)
    {
        foreach (PlayerController player in players.ActivePlayers)
        {
            foreach (CharacterStatusBehaviour behaviour in
                     player.GetComponentsInChildren<CharacterStatusBehaviour>(true))
                if (behaviour != null) Destroy(behaviour.gameObject);
            player.RuntimeData.ResetPieces();
        }
        yield return null;

        foreach (PlayerController player in players.ActivePlayers)
        {
            string characterId = player.PlayerId == 2 ? secondCharacterId : firstCharacterId;
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                PrefabRoot + characterId + ".prefab");
            Check("Mixed " + characterId + " prefab loads", prefab != null);
            if (prefab == null) continue;
            for (int pieceId = 0; pieceId < player.RuntimeData.Pieces.Count; pieceId++)
                Instantiate(prefab, player.transform);
        }
        yield return null;
        yield return null;
    }

    private IEnumerator VerifyMixedCharacterInteractions()
    {
        yield return InstallMatchup("CHAR_002", "CHAR_006");
        PlayerController first = players.ActivePlayers[0];
        PlayerController second = players.ActivePlayers[1];
        var talismanSource = first.RuntimeData.Pieces[0];
        var protectedAlly = first.RuntimeData.Pieces[1];
        var hero = second.RuntimeData.Pieces[0];
        talismanSource.MoveTo(BoardTileId.Outer03);
        protectedAlly.MoveTo(BoardTileId.Outer02);
        hero.MoveTo(BoardTileId.Outer01);
        CharacterSkillRegistry.NotifyPieceEnteredBoard(1, 0);
        bool talismanApplied = protectedAlly.Cc.Has(CcDefine.protect);
        turns.CurrentTurn.currentPlayer = PlayerSlot.Player2;
        turns.CurrentTurn.currentPhase = TurnPhase.WaitAction;
        CharacterSkillRegistry.RequestSkillPoint(2, 5);
        int throwsBefore = GetPrivateInt(turns, "pendingSkillThrows");
        CharacterActiveResult aura = CharacterSkillRegistry.TryUseActive(
            new CharacterActiveRequest(2, 0));
        Check("Talisman blocks enemy Sword Aura retirement",
            talismanApplied && aura.Succeeded &&
            protectedAlly.State == PieceState.InBoard &&
            GetPrivateInt(turns, "pendingSkillThrows") == throwsBefore);

        yield return InstallMatchup("CHAR_006", "CHAR_007");
        first = players.ActivePlayers[0]; second = players.ActivePlayers[1];
        hero = first.RuntimeData.Pieces[0];
        var orc = second.RuntimeData.Pieces[0];
        hero.MoveTo(BoardTileId.Outer02);
        orc.MoveTo(BoardTileId.Outer01);
        turns.CurrentTurn.currentPlayer = PlayerSlot.Player2;
        turns.CurrentTurn.currentPhase = TurnPhase.WaitAction;
        CharacterSkillRegistry.RequestSkillPoint(2, 3);
        CharacterActiveResult charge = CharacterSkillRegistry.TryUseActive(
            new CharacterActiveRequest(2, 0));
        Check("Hero ignores Orc path stun",
            charge.Succeeded && !hero.Cc.Has(CcDefine.Sturn) &&
            hero.State == PieceState.InBoard);

        yield return InstallMatchup("CHAR_009", "CHAR_005");
        first = players.ActivePlayers[0]; second = players.ActivePlayers[1];
        var robot = first.RuntimeData.Pieces[0];
        var samurai = second.RuntimeData.Pieces[0];
        robot.MoveTo(BoardTileId.Outer02);
        samurai.MoveTo(BoardTileId.Outer01);
        turns.CurrentTurn.currentPlayer = PlayerSlot.Player2;
        turns.CurrentTurn.currentPhase = TurnPhase.WaitAction;
        CharacterSkillRegistry.RequestSkillPoint(2, 5);
        CharacterActiveResult slash = CharacterSkillRegistry.TryUseActive(
            new CharacterActiveRequest(2, 0));
        Check("Samurai retirement converts Robot to Parts without capture bonus",
            slash.Succeeded && robot.Cc.Has(CcDefine.Robot_part) &&
            robot.State == PieceState.InBoard);
        var rescuer = first.RuntimeData.Pieces[1];
        rescuer.MoveTo(BoardTileId.Outer01);
        bool revived = movement.TryMovePiece(1, 1, 1) &&
                       robot.State == PieceState.InBoard &&
                       !robot.Cc.Has(CcDefine.Robot_part) &&
                       robot.IsStacked && robot.StackLeaderPieceId == rescuer.PieceId;
        bool convertedAgain = CcEffectService.Apply(robot, CcDefine.Retire) &&
                              robot.Cc.Has(CcDefine.Robot_part);
        Check("Robot revives only on its tile and can later become Parts again",
            revived && convertedAgain);

        yield return InstallMatchup("CHAR_008", "CHAR_004");
        first = players.ActivePlayers[0]; second = players.ActivePlayers[1];
        var windSource = first.RuntimeData.Pieces[0];
        var windAlly = first.RuntimeData.Pieces[1];
        var fox = second.RuntimeData.Pieces[0];
        windSource.MoveTo(BoardTileId.Outer01);
        windAlly.MoveTo(BoardTileId.Outer01);
        fox.MoveTo(BoardTileId.Outer03);
        CcEffectService.Apply(windSource, CcDefine.WindPath,
            path: new[] { BoardTileId.Outer02 });
        Check("Wind extra movement does not capture its landing enemy",
            movement.TryMovePiece(1, 1, 1) &&
            windAlly.CurrentTileId == BoardTileId.Outer03 &&
            fox.State == PieceState.InBoard);
        windAlly.MoveTo(BoardTileId.Outer01);
        CharacterBoardUtility.MoveStackAlongPath(first, windAlly, new[] { BoardTileId.Outer02 });
        Check("Runtime simple movement does not retrigger Wind", windAlly.CurrentTileId == BoardTileId.Outer02);
        windSource.MoveTo(BoardTileId.Outer01);
        windAlly.MoveTo(BoardTileId.Outer01);
        int windGroup = first.RuntimeData.CreateStackGroupId();
        windSource.SetStackGroup(windGroup, 0); windAlly.SetStackGroup(windGroup, 0);
        bool windStackMoved = movement.TryMovePiece(1, 0, 1);
        CharacterSkillRegistry.NotifyOwnerTurnEnded(1);
        // The player-wide path is stored by one registered piece, whose registration order
        // is not guaranteed by Unity. Verify the path rather than assuming piece 0 stores it.
        CcState storedWind = null;
        foreach (var candidate in first.RuntimeData.Pieces)
            if (candidate.Cc.Get(CcDefine.WindPath) is CcState wind) storedWind = wind;
        Check("Runtime Wind stack retains both original and extra landing",
            windStackMoved && windSource.CurrentTileId == BoardTileId.Outer03 &&
            windAlly.CurrentTileId == BoardTileId.Outer03 && storedWind != null &&
            storedWind.Path.Count == 2 && storedWind.Path[1] == BoardTileId.Outer03);

        yield return InstallMatchup("CHAR_003", "CHAR_006");
        first = players.ActivePlayers[0];
        var hong = first.RuntimeData.Pieces[0];
        hong.MoveTo(BoardTileId.Outer01);
        Check("Runtime clone capacity rejects a fourth clone",
            CcEffectService.Apply(hong, CcDefine.Stack_fake, value: 3) &&
            !CcEffectService.Apply(hong, CcDefine.Stack_fake));

        yield return InstallMatchup("CHAR_006", "CHAR_004");
        first = players.ActivePlayers[0]; second = players.ActivePlayers[1];
        hero = first.RuntimeData.Pieces[0]; fox = second.RuntimeData.Pieces[0];
        hero.MoveTo(BoardTileId.Outer01); fox.MoveTo(BoardTileId.Outer02);
        var previousComposition = turns.Settings.matchComposition;
        bool previousTeamMode = turns.Settings.isTeamMode;
        turns.Settings.matchComposition = MatchComposition.TwoVsTwo;
        turns.Settings.isTeamMode = true;
        // Earlier matchups used the same SO, so let its shared cooldown expire.
        for (int i = 0; i < 5; i++) CharacterSkillRegistry.NotifyOwnerTurnStarted(1);
        turns.CurrentTurn.currentPlayer = PlayerSlot.Player1;
        turns.CurrentTurn.currentPhase = TurnPhase.WaitAction;
        CharacterSkillRegistry.RequestSkillPoint(1, 5);
        throwsBefore = GetPrivateInt(turns, "pendingSkillThrows");
        aura = CharacterSkillRegistry.TryUseActive(new CharacterActiveRequest(1, 0));
        Check("Team Sword Aura preserves allies and grants no throws",
            aura.Succeeded && fox.State == PieceState.InBoard &&
            GetPrivateInt(turns, "pendingSkillThrows") == throwsBefore, aura.Message);
        turns.Settings.matchComposition = previousComposition;
        turns.Settings.isTeamMode = previousTeamMode;
    }

    private void VerifyRegistration(string characterId)
    {
        bool allRegistered = true;
        Type expectedType = Type.GetType(characterId + "_Status, Assembly-CSharp");
        foreach (PlayerController player in players.ActivePlayers)
        {
            allRegistered &= player.GetComponentsInChildren<CharacterStatusBehaviour>(true).Length ==
                             player.RuntimeData.Pieces.Count;
            for (int pieceId = 0; pieceId < player.RuntimeData.Pieces.Count; pieceId++)
            {
                allRegistered &= CharacterSkillRegistry.TryGet(
                    player.PlayerId, pieceId, out CharacterStatusBehaviour behaviour) &&
                    behaviour != null && behaviour.GetType() == expectedType &&
                    behaviour.Data != null;
            }
        }
        Check(characterId + " registers four pieces for every player", allRegistered);
        var data = AssetDatabase.LoadAssetAtPath<CharacterData>(
            "Assets/Character/Char_Info/" + characterId + "_SO.asset");
        Check(characterId + " visual is empty or a model, never a recursive gameplay prefab",
            data != null && (data.visualModelPrefab == null ||
                CharacterStatusBehaviour.IsVisualOnlyPrefab(data.visualModelPrefab)));
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
        string detail = "";
        switch (characterId)
        {
            case "CHAR_001_1":
                caster.Reset();
                ok = movement.TryMovePiece(1, 0, 2) &&
                     caster.CurrentTileId == BoardTileId.Outer02 &&
                     ((CHAR_001_1_Status)skill).HasPendingForcedMove &&
                     ((CHAR_001_1_Status)skill).ExecutePendingForcedMove() &&
                     caster.CurrentTileId == BoardTileId.Outer03;
                break;
            case "CHAR_001_2":
                int garamPointsBefore = CharacterSkillRegistry.GetSkillPoints(1);
                skill.OnCaptureCompleted(new CharacterCaptureRequest(
                    1, 0, 2, 0, 1, true));
                bool stacked = movement.TryMovePiece(1, 0, 2);
                int pointsAfterStack = CharacterSkillRegistry.GetSkillPoints(1);
                skill.OnMoveCompleted(new CharacterMoveRecord(
                    1, 0, BoardTileId.Outer02, BoardTileId.Outer03,
                    new[] { BoardTileId.Outer03 }));
                bool movedExistingStack = movement.TryMovePiece(1, ally.PieceId, 1);
                int pointsAfterStackMove = CharacterSkillRegistry.GetSkillPoints(1);
                CcEffectService.Apply(caster, CcDefine.Retire);
                caster.MoveTo(BoardTileId.Outer02);
                bool stackedAgain = movement.TryMovePiece(1, 0, 2);
                ok = skill.PassiveStatus == CharacterSkillStatus.Get_point &&
                     stacked && caster.IsStacked &&
                     pointsAfterStack == garamPointsBefore + 2 &&
                     movedExistingStack && pointsAfterStackMove == pointsAfterStack &&
                     stackedAgain && CharacterSkillRegistry.GetSkillPoints(1) ==
                         System.Math.Min(pointsAfterStack + 1,
                             CharacterSkillRegistry.GetMaxSkillPoints(1));
                break;
            case "CHAR_002":
                skill.OnPieceEnteredBoard();
                CharacterSkillRegistry.TryGet(1, 1, out CharacterStatusBehaviour protectedAlly);
                var teamSettings = new GameStartSettings
                {
                    matchComposition = MatchComposition.TwoVsTwo,
                    isTeamMode = true
                };
                CharacterPieceReference? teamAlly = CharacterBoardUtility.FindNearestAlly(
                    players, 1, 0, caster.CurrentTileId, teamSettings);
                ok = protectedAlly.EvaluateIncomingCapture(
                    new CharacterCaptureRequest(2, 0, 1, 1, 1, true)) ==
                    CharacterCaptureDecision.Prevent && teamAlly.HasValue &&
                    teamAlly.Value.Player.PlayerId == 2;
                break;
            case "CHAR_003":
                bool soloCaptureProceeds = skill.EvaluateIncomingCapture(capture) ==
                                           CharacterCaptureDecision.Proceed;
                caster.MoveTo(BoardTileId.Outer03);
                ally.MoveTo(BoardTileId.Outer01);
                bool formedDefendingStack = movement.TryMovePiece(1, ally.PieceId, 2) &&
                                            caster.IsStacked && ally.IsStacked &&
                                            caster.StackLeaderPieceId == caster.PieceId;
                bool defendedStack = movement.TryMovePiece(2, enemy.PieceId, 1) &&
                                     caster.State == PieceState.InBoard &&
                                     ally.State == PieceState.InBoard && ally.IsStacked;

                ResetBoard();
                caster.MoveTo(BoardTileId.Outer03);
                ally.MoveTo(BoardTileId.Outer01);
                enemy.MoveTo(BoardTileId.Outer02);
                var secondAttacker = p2.RuntimeData.Pieces[1];
                secondAttacker.MoveTo(BoardTileId.Outer01);
                bool formedBothStacks = movement.TryMovePiece(1, ally.PieceId, 2) &&
                                        movement.TryMovePiece(2, secondAttacker.PieceId, 1);
                bool clonedLeader = CcEffectService.Apply(caster, CcDefine.Stack_fake);
                bool cloneConsumedStackDefended = movement.TryMovePiece(2, enemy.PieceId, 1) &&
                                             !caster.Cc.Has(CcDefine.Stack_fake) &&
                                             caster.State == PieceState.InBoard &&
                                             ally.State == PieceState.InBoard && ally.IsStacked;

                ResetBoard();
                caster.MoveTo(BoardTileId.Outer03);
                ally.MoveTo(BoardTileId.Outer01);
                enemy.MoveTo(BoardTileId.Outer02);
                secondAttacker.MoveTo(BoardTileId.Outer01);
                bool equalStacks = movement.TryMovePiece(1, ally.PieceId, 2) &&
                                   movement.TryMovePiece(2, secondAttacker.PieceId, 1);
                bool ordinaryCapture = movement.TryMovePiece(2, enemy.PieceId, 1) &&
                                       caster.State == PieceState.Waiting &&
                                       ally.State == PieceState.Waiting &&
                                       caster.CurrentCc == CcDefine.Kill &&
                                       ally.CurrentCc == CcDefine.Retire;
                ok = soloCaptureProceeds && formedDefendingStack && defendedStack &&
                     formedBothStacks && clonedLeader && cloneConsumedStackDefended &&
                     equalStacks && ordinaryCapture;
                detail = $"solo={soloCaptureProceeds}, firstStack={formedDefendingStack}, " +
                         $"wholeStack={defendedStack}, bothStacks={formedBothStacks}, " +
                         $"clone={clonedLeader}, cloneConsumedStackDefended={cloneConsumedStackDefended}, " +
                         $"equalStacks={equalStacks}, ordinary={ordinaryCapture}";
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
                ok = !CcEffectService.Apply(caster, CcDefine.Sturn, 2) &&
                     !CcEffectService.Apply(caster, CcDefine.Binding, 2) &&
                     !CcEffectService.Apply(caster, CcDefine.Silence, 2) &&
                     !caster.Cc.Has(CcDefine.Sturn) &&
                     !caster.Cc.Has(CcDefine.Binding) &&
                     !caster.Cc.Has(CcDefine.Silence) &&
                     CcEffectService.Apply(caster, CcDefine.Retire) &&
                     caster.State == PieceState.Waiting;
                break;
            case "CHAR_007":
                int before = CharacterSkillRegistry.GetSkillPoints(1);
                skill.OnCaptureCompleted(new CharacterCaptureRequest(1, 0, 2, 0, 1, true));
                ok = CharacterSkillRegistry.GetSkillPoints(1) == before + 1;
                break;
            case "CHAR_008":
                caster.MoveTo(BoardTileId.Outer02);
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
                ok = skill.EvaluateIncomingCapture(capture) == CharacterCaptureDecision.Proceed &&
                     CcEffectService.Apply(caster, CcDefine.Retire) &&
                     caster.Cc.Has(CcDefine.Robot_part) &&
                     caster.State == PieceState.InBoard &&
                     !skill.IsTargetable;
                break;
            case "CHAR_010":
                ally.MoveTo(BoardTileId.Outer01);
                int stackId = p1.RuntimeData.CreateStackGroupId();
                caster.SetStackGroup(stackId, 0);
                ally.SetStackGroup(stackId, 0);
                enemy.MoveTo(BoardTileId.Outer03);
                var extraLandingEnemy = p2.RuntimeData.Pieces[1];
                extraLandingEnemy.MoveTo(BoardTileId.Outer04);
                ok = movement.TryMovePiece(1, 0, 2) &&
                     caster.CurrentTileId == BoardTileId.Outer04 &&
                     ally.CurrentTileId == BoardTileId.Outer04 &&
                     enemy.State == PieceState.Waiting &&
                     extraLandingEnemy.State == PieceState.Waiting;
                break;
            default:
                ok = false;
                break;
        }
        Check(characterId + " passive", ok, detail);
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
        if (characterId == "CHAR_007")
        {
            p2.RuntimeData.Pieces[0].MoveTo(BoardTileId.Outer02);
            p2.RuntimeData.Pieces[1].MoveTo(BoardTileId.Outer03);
        }

        turns.CurrentTurn.currentPlayer = PlayerSlot.Player1;
        turns.CurrentTurn.currentTeam = TeamSlot.TeamA;
        turns.CurrentTurn.currentPhase =
            characterId == "CHAR_001_1" || characterId == "CHAR_002"
                ? TurnPhase.WaitThrow
                : TurnPhase.WaitAction;

        int skillPointCost = skill.ActiveSkillPointCost;
        int currentSkillPoints = CharacterSkillRegistry.GetSkillPoints(1);
        if (currentSkillPoints < skillPointCost)
            CharacterSkillRegistry.RequestSkillPoint(1, skillPointCost - currentSkillPoints);
        int skillPointsBeforeUse = CharacterSkillRegistry.GetSkillPoints(1);
        int skillThrowsBeforeUse = GetPrivateInt(turns, "pendingSkillThrows");
        int captureThrowsBeforeUse = GetPrivateInt(turns, "pendingCaptureThrows");

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
        if (characterId == "CHAR_006")
            Check("CHAR_006 retires both tiles and grants one throw per enemy",
                p2.RuntimeData.Pieces[0].State == PieceState.Waiting &&
                p2.RuntimeData.Pieces[1].State == PieceState.Waiting &&
                GetPrivateInt(turns, "pendingSkillThrows") == skillThrowsBeforeUse + 2 &&
                GetPrivateInt(turns, "pendingCaptureThrows") == captureThrowsBeforeUse);
        if (characterId == "CHAR_010")
            Check("CHAR_010 retreat ends the owner's turn",
                turns.CurrentTurn.currentPlayer != PlayerSlot.Player1);
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
        turns.Initialize(settings);
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
        Scene fixture = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
            Application.isBatchMode && string.IsNullOrEmpty(previous.path)
                ? NewSceneMode.Single : NewSceneMode.Additive);
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
            turns.Initialize(new GameStartSettings
            {
                gameMode = GameMode.Escape,
                useSkill = true,
                playerCount = 2,
                pieceCountPerPlayer = 4,
                turnTimeMode = TurnTimeMode.Unlimited
            });
            var p1 = new GameObject("CC Test P1").AddComponent<PlayerController>();
            var p2 = new GameObject("CC Test P2").AddComponent<PlayerController>();
            p1.Initialize(1, "Test 1", 4);
            p2.Initialize(2, "Test 2", 4);
            var visualFixture = new GameObject("Visual-only validation fixture");
            Check("Visual-only prefab is accepted", CharacterStatusBehaviour.IsVisualOnlyPrefab(visualFixture));
            var nestedStatus = new GameObject("Inactive nested gameplay status");
            nestedStatus.transform.SetParent(visualFixture.transform);
            nestedStatus.SetActive(false);
            nestedStatus.AddComponent<CHAR_002_Status>();
            Check("Inactive nested Status is rejected before Instantiate",
                !CharacterStatusBehaviour.IsVisualOnlyPrefab(visualFixture));
            Check("Gameplay prefab root is rejected before Instantiate",
                !CharacterStatusBehaviour.IsVisualOnlyPrefab(nestedStatus));
            UnityEngine.Object.DestroyImmediate(visualFixture);
            var players = (List<PlayerController>)Get(manager, "activePlayers");
            players.Add(p1); players.Add(p2);
            var piece = p1.RuntimeData.Pieces[0];
            piece.MoveTo(BoardTileId.Outer01);
            Check("Legacy enum values preserved", (int)CcDefine.Sturn == 1 && (int)CcDefine.Kill == 4);
            manager.TryApplyPieceCc(1, 0, CcDefine.Sturn, 2);
            manager.TryApplyPieceCc(1, 0, CcDefine.Silence, 3);
            manager.TryApplyPieceCc(1, 0, CcDefine.protect, 4);
            Check("Manager stores all simultaneous effects", piece.Cc.Effects.Count == 3);
            Check("Stun blocks direct movement", !movement.TryMovePiece(1, 0, 1));
            CcEffectService.TickOwnerTurn(p1);
            Check("CC decrements independently", piece.Cc.Get(CcDefine.Sturn).RemainingOwnerTurns == 1 &&
                piece.Cc.Get(CcDefine.Silence).RemainingOwnerTurns == 2);
            CcEffectService.TickOwnerTurn(p1);
            Check("Stun expires without removing Silence or Protection", !piece.Cc.Has(CcDefine.Sturn) &&
                piece.Cc.Has(CcDefine.Silence) && piece.Cc.Has(CcDefine.protect));
            Check("Silence allows movement but blocks active", CcEffectService.CanMove(piece) &&
                !CcEffectService.CanUseSkill(piece));
            Check("Protection prevents Kill before capture", !CcEffectService.Apply(piece, CcDefine.Kill) &&
                piece.State == PieceState.InBoard);
            CcEffectService.Remove(piece, CcDefine.protect);
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
            CcEffectService.Apply(piece, CcDefine.Robot_part, 3);
            Check("Parts keeps the entire stack disabled", piece.IsStacked && carried.IsStacked &&
                ReferenceEquals(piece.Cc.Get(CcDefine.Robot_part), carried.Cc.Get(CcDefine.Robot_part)) &&
                !CcEffectService.CanMove(carried) && !CcEffectService.CanUseSkill(carried));
            piece.ClearCc(); piece.SetStackGroup(51, 0); carried.SetStackGroup(51, 0);
            CcEffectService.Apply(piece, CcDefine.Retire);
            Check("Retiring a leader retires every real member", !carried.IsStacked &&
                piece.State == PieceState.Waiting && carried.State == PieceState.Waiting);
            VerifyStackState(p1, p2, movement);

            string[] ids = { "001_1", "001_2", "002", "003", "004", "005", "006",
                "007", "008", "009", "010" };
            foreach (string id in ids)
            {
                p1.RuntimeData.ResetPieces(); p2.RuntimeData.ResetPieces();
                ResetRegistry();
                turns.OnTurnStarted = null; turns.OnTurnEnded = null;
                Set(turns, "pendingSkillThrows", 0); Set(turns, "pendingCaptureThrows", 0);
                ((List<YutThrowData>)Get(turns, "pendingResults")).Clear();
                turns.CurrentTurn.currentPlayer = PlayerSlot.Player1;
                turns.TurnOrder.currentIndex = turns.TurnOrder.order.IndexOf(PlayerSlot.Player1);
                turns.CurrentTurn.currentPhase = id == "001_1" || id == "002"
                    ? TurnPhase.WaitThrow : TurnPhase.WaitAction;
                YutThrowData earlierResult = null;
                if (id == "002")
                {
                    earlierResult = new YutThrowData
                    {
                        player = PlayerSlot.Player1,
                        result = YutResult.Gae,
                        throwIndexInTurn = 0
                    };
                    ((List<YutThrowData>)Get(turns, "pendingResults")).Add(earlierResult);
                }
                var host = new GameObject("CC Test Character " + id);
                host.transform.SetParent(p1.transform);
                var skill = (CharacterStatusBehaviour)host.AddComponent(Type.GetType("CHAR_" + id + "_Status, Assembly-CSharp"));
                var asset = AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/Character/Char_Info/CHAR_" + id + "_SO.asset");
                Check(id + " original SO loads", asset != null);
                Check(id + " active IDs are configured", asset.active_Effects != null &&
                    asset.active_Effects.Length > 0 &&
                    Array.TrueForAll(asset.active_Effects, effect => effect != null &&
                        effect.id != CcDefine.None && Enum.IsDefined(typeof(CcDefine), effect.id)));
                Check(id + " passive IDs are configured", asset.passive_Effects != null &&
                    Array.TrueForAll(asset.passive_Effects, effect => effect != null &&
                        effect.id != CcDefine.None && Enum.IsDefined(typeof(CcDefine), effect.id)));
                var data = UnityEngine.Object.Instantiate(asset);
                data.visualModelPrefab = null;
                data.active_CooldownTurns = 2; data.active_SkillPointCost = 1;
                copies.Add(data); skill.Initialize(data);
                typeof(CharacterStatusBehaviour).GetMethod("TryRegisterRuntime", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(skill, null);
                VerifyDisabledCharacterSkills(id, skill, movement, turns, p1, p2);
                int pointsBeforeTurn = CharacterSkillRegistry.GetSkillPoints(1);
                CharacterSkillRegistry.NotifyOwnerTurnStarted(1);
                int expectedAfterTurn = id == "007"
                    ? pointsBeforeTurn
                    : Math.Min(pointsBeforeTurn + 1, CharacterSkillRegistry.GetMaxSkillPoints(1));
                Check(id + " base turn SkillPoint gain",
                    CharacterSkillRegistry.GetSkillPoints(1) == expectedAfterTurn);
                piece.MoveTo(BoardTileId.Outer01);
                VerifySkillInputPhases(id, skill, turns, piece);
                var enemy = p2.RuntimeData.Pieces[0];
                enemy.MoveTo(id == "007" || id == "005" ? BoardTileId.Outer02 : BoardTileId.Outer01);
                var previousComposition = turns.Settings.matchComposition;
                bool previousTeamMode = turns.Settings.isTeamMode;
                turns.Settings.matchComposition = MatchComposition.TwoVsTwo;
                turns.Settings.isTeamMode = true;
                Check(id + " team skill enemy list excludes allied player",
                    CharacterBoardUtility.GetEnemiesOnBoard(manager, 1).Count == 0);
                if (id == "008")
                    Check("Spirit Arrow rejects teammate during selection and execution",
                        !skill.CanSelectActiveTarget(2, 0) &&
                        !((CharacterActiveResult)typeof(CHAR_008_Status)
                            .GetMethod("ExecuteActive", BindingFlags.Instance | BindingFlags.NonPublic)
                            .Invoke(skill, new object[] { new CharacterActiveRequest(1, 0, 2, 0), piece })).Succeeded &&
                        !enemy.Cc.Has(CcDefine.Binding));
                turns.Settings.matchComposition = previousComposition;
                turns.Settings.isTeamMode = previousTeamMode;
                if (id == "007")
                    p2.RuntimeData.Pieces[1].MoveTo(BoardTileId.Outer03);
                CharacterSkillRegistry.RequestSkillPoint(1, 1);
                bool silenceApplied = CcEffectService.Apply(piece, CcDefine.Silence, 2);
                if (id == "006")
                    Check("006 ignores Silence before casting", !silenceApplied &&
                        !piece.Cc.Has(CcDefine.Silence));
                else
                {
                    Check(id + " active rejects Silence", !CharacterSkillRegistry.TryUseActive(
                        new CharacterActiveRequest(1, 0, 2, 0)).Succeeded);
                    Check(id + " rejected active spends no SP/cooldown", CharacterSkillRegistry.GetSkillPoints(1) == 1 &&
                        CharacterSkillRegistry.GetRemainingActiveCooldown(1, data) == 0);
                }
                CcEffectService.Remove(piece, CcDefine.Silence);
                var result = CharacterSkillRegistry.TryUseActive(new CharacterActiveRequest(1, 0, 2, 0));
                Check(id + " active executes through registry", result.Succeeded, result.Message);
                Check(id + " success spends SP and shares cooldown", CharacterSkillRegistry.GetSkillPoints(1) == 0 &&
                    CharacterSkillRegistry.GetRemainingActiveCooldown(1, data) == 2);
                switch (id)
                {
                    case "001_1":
                        Check("DoOrMo stored on piece", piece.Cc.Has(CcDefine.MOorDO));
                        var table = skill.ModifyYutProbability(new[] { (YutResult.Gae, 100f) });
                        Check("DoOrMo consumed on next throw", table.Length == 2 && !piece.Cc.Has(CcDefine.MOorDO));
                        skill.ShouldGrantExtraThrow(YutResult.Do, false);
                        Check("DoOrMo remains phase-eligible for a later bonus throw",
                            skill.IsActiveUsableInCurrentPhase());
                        break;
                    case "001_2":
                        Check("ExtraThrow schedules exactly once", (int)Get(turns, "pendingSkillThrows") == 1);
                        break;
                    case "002":
                        var nextResult = new YutThrowData
                        {
                            player = PlayerSlot.Player1,
                            result = YutResult.Geol,
                            throwIndexInTurn = 1
                        };
                        turns.OnPendingResultsChanged?.Invoke(
                            new List<YutThrowData> { earlierResult, nextResult });
                        turns.OnPendingResultsChanged?.Invoke(
                            new List<YutThrowData> { nextResult });
                        Check("DoubleMove ignores an earlier pending result",
                            skill.ModifyMoveCount(new CharacterMoveRequest(1, 0, 2, false)) == 2);
                        turns.OnPendingResultsChanged?.Invoke(new List<YutThrowData>());
                        Check("DoubleMove applies to the next throw exactly once",
                            skill.ModifyMoveCount(new CharacterMoveRequest(1, 0, 3, false)) == 6 &&
                            skill.ModifyMoveCount(new CharacterMoveRequest(1, 0, 3, false)) == 3);
                        break;
                    case "003":
                        Check("Clone stored and stacked", piece.Cc.Get(CcDefine.Stack_fake)?.Value == 1 && piece.IsStacked);
                        Check("Clone absorbs capture without bonus", skill.EvaluateIncomingCapture(
                            new CharacterCaptureRequest(2, 0, 1, 0, 1, true)) == CharacterCaptureDecision.ConsumeCloneWithoutBonus &&
                            !piece.Cc.Has(CcDefine.Stack_fake) && !piece.IsStacked);
                        CcEffectService.Apply(piece, CcDefine.Stack_fake, value: 3);
                        Check("Clone cannot exceed three carried units",
                            !CcEffectService.Apply(piece, CcDefine.Stack_fake) &&
                            piece.Cc.Get(CcDefine.Stack_fake)?.Value == 3);
                        CcEffectService.Remove(piece, CcDefine.Stack_fake);
                        var cloneCargo = p1.RuntimeData.Pieces[1];
                        cloneCargo.MoveTo(piece.CurrentTileId);
                        int cloneGroup = p1.RuntimeData.CreateStackGroupId();
                        piece.SetStackGroup(cloneGroup, piece.PieceId);
                        cloneCargo.SetStackGroup(cloneGroup, piece.PieceId);
                        Check("Clone capacity includes real cargo",
                            CcEffectService.Apply(piece, CcDefine.Stack_fake, value: 2) &&
                            !CcEffectService.Apply(piece, CcDefine.Stack_fake));
                        var joiningCargo = p1.RuntimeData.Pieces[2];
                        joiningCargo.MoveTo(piece.CurrentTileId);
                        joiningCargo.SetStackGroup(cloneGroup, piece.PieceId);
                        CharacterSkillRegistry.NotifyMoveCompleted(new CharacterMoveRecord(1, 2,
                            BoardTileId.Outer02, piece.CurrentTileId, new[] { piece.CurrentTileId }));
                        Check("Real cargo joining replaces excess clones",
                            piece.Cc.Get(CcDefine.Stack_fake)?.Value == 1 && joiningCargo.IsStacked);
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
                    case "006":
                        Check("Sword Aura retires without capture bonus", enemy.State == PieceState.Waiting &&
                            enemy.CurrentCc == CcDefine.None &&
                            (int)Get(turns, "pendingSkillThrows") == 1 &&
                            (int)Get(turns, "pendingCaptureThrows") == 0);
                        break;
                    case "007":
                        Check("Charge stuns every enemy on its path",
                            enemy.Cc.Has(CcDefine.Sturn) &&
                            p2.RuntimeData.Pieces[1].Cc.Has(CcDefine.Sturn));
                        CcEffectService.TickOwnerTurn(p2);
                        Check("Charge stun blocks for one full turn", !movement.TryMovePiece(2, 0, 1));
                        CcEffectService.TickOwnerTurn(p2);
                        Check("Charge stun expires", CcEffectService.CanMove(enemy));
                        piece.MoveTo(BoardTileId.None);
                        typeof(CHAR_007_Status).GetMethod("ExecuteActive", BindingFlags.Instance | BindingFlags.NonPublic)
                            .Invoke(skill, new object[] { new CharacterActiveRequest(1, 0), piece });
                        Check("Charge across finish becomes Goal", piece.State == PieceState.Goal);
                        break;
                    case "008":
                        Check(id + " Binding stored on target", enemy.Cc.Has(CcDefine.Binding));
                        Check(id + " Binding leaves skills available", CcEffectService.CanUseSkill(enemy));
                        CcEffectService.TickOwnerTurn(p2);
                        Check(id + " target cannot move for one complete turn", !movement.TryMovePiece(2, 0, 1));
                        CcEffectService.TickOwnerTurn(p2);
                        Check(id + " Binding expires", CcEffectService.CanMove(enemy));
                        var enemyCargo = p2.RuntimeData.Pieces[1];
                        enemyCargo.MoveTo(enemy.CurrentTileId);
                        enemy.SetStackGroup(70, enemy.PieceId);
                        enemyCargo.SetStackGroup(70, enemy.PieceId);
                        typeof(CHAR_008_Status).GetMethod("ExecuteActive", BindingFlags.Instance | BindingFlags.NonPublic)
                            .Invoke(skill, new object[] { new CharacterActiveRequest(1, 0, 2, 1), piece });
                        Check("Binding selected cargo blocks its stack leader",
                            enemy.Cc.Has(CcDefine.Binding) && !movement.TryMovePiece(2, 0, 1));
                        CcEffectService.Apply(enemy, CcDefine.Stealth, 3);
                        Check("Hidden leader makes carried target untargetable",
                            !skill.CanSelectActiveTarget(2, 1));
                        break;
                    case "009":
                        Check("Self destruct retires enemy and converts robot to Parts",
                            piece.State == PieceState.InBoard && piece.Cc.Has(CcDefine.Robot_part) &&
                            enemy.State == PieceState.Waiting);
                        Check("Parts conversion stored in CC", skill.EvaluateIncomingCapture(
                            new CharacterCaptureRequest(2, 0, 1, 0, 1, true)) ==
                            CharacterCaptureDecision.Prevent && piece.Cc.Has(CcDefine.Robot_part));
                        Check("Parts cannot move/use skills", !movement.TryMovePiece(1, 0, 1) && !CcEffectService.CanUseSkill(piece));
                        var ally = p1.RuntimeData.Pieces[1]; ally.MoveTo(BoardTileId.Outer02);
                        CcEffectService.Apply(ally, CcDefine.Stack_fake, value: 3);
                        ally.MoveTo(BoardTileId.Outer01);
                        CharacterSkillRegistry.NotifyMoveCompleted(new CharacterMoveRecord(1, 1, BoardTileId.Outer03,
                            BoardTileId.Outer01, new[] { BoardTileId.Outer01 }));
                        Check("Ally revives and carries Parts on the same tile",
                            !piece.Cc.Has(CcDefine.Robot_part) &&
                            piece.CurrentTileId == BoardTileId.Outer01 &&
                            piece.IsStacked && piece.StackLeaderPieceId == ally.PieceId);
                        Check("Parts revival respects the clone and cargo limit",
                            ally.Cc.Get(CcDefine.Stack_fake)?.Value == 2 &&
                            CcBoardEffects.CountStackUnits(p1, ally) == 4);
                        break;
                    case "010": Check("Retreat moves one tile back", piece.CurrentTileId == BoardTileId.None); break;
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
                        Check("First move keeps original Geol distance and queues Do",
                            movement.TryMovePiece(1, 0, 3) &&
                            piece.CurrentTileId == BoardTileId.Outer03 &&
                            enemy.State == PieceState.Waiting &&
                            ((CHAR_001_1_Status)skill).HasPendingForcedMove);
                        Check("Queued Do moves after first landing",
                            ((CHAR_001_1_Status)skill).ExecutePendingForcedMove() &&
                            piece.CurrentTileId == BoardTileId.Outer04);
                        Check("First landing and forced Do landing both capture",
                            enemy.State == PieceState.Waiting &&
                            secondEnemy.State == PieceState.Waiting);
                        Check("Forced Do does not repeat on the next move",
                            movement.TryMovePiece(1, 0, 1) &&
                            piece.CurrentTileId == BoardTileId.Corner01);

                        p1.RuntimeData.ResetPieces(); p2.RuntimeData.ResetPieces();
                        skill.OnPieceRetired();
                        Check("Retire restores passive and corner landing queues shortcut Do",
                            movement.TryMovePiece(1, 0, 5) &&
                            piece.CurrentTileId == BoardTileId.Corner01 &&
                            ((CHAR_001_1_Status)skill).HasPendingForcedMove);
                        Check("Forced Do takes the corner shortcut",
                            ((CHAR_001_1_Status)skill).ExecutePendingForcedMove() &&
                            piece.CurrentTileId == BoardTileId.Inner01);
                        break;
                    case "001_2": case "007":
                        int beforeCapturePoint = CharacterSkillRegistry.GetSkillPoints(1);
                        skill.OnCaptureCompleted(new CharacterCaptureRequest(1, 0, 2, 0, 1, true));
                        Check(id + " passive SkillPoint respects maximum",
                            CharacterSkillRegistry.GetSkillPoints(1) == Math.Min(
                                beforeCapturePoint + 1,
                                CharacterSkillRegistry.GetMaxSkillPoints(1)));
                        break;
                    case "002":
                        skill.OnPieceEnteredBoard();
                        Check("Talisman stored on nearest ally with correct source", passiveAlly.Cc.Has(CcDefine.protect) &&
                            passiveAlly.Cc.Get(CcDefine.protect).SourcePieceId == 0);
                        //수정: 수신 대상은 자신과 업힌 말을 제외한 필드 위 아군 한 말입니다.
                        Check("Talisman does not protect its own source", !piece.Cc.Has(CcDefine.protect));
                        var joiningAlly = p1.RuntimeData.Pieces[2];
                        joiningAlly.MoveTo(BoardTileId.Outer02);
                        Check("Joining cargo reads the leader's talisman",
                            movement.TryMovePiece(1, 2, 1) &&
                            passiveAlly.Cc.Has(CcDefine.protect) &&
                            ReferenceEquals(joiningAlly.Cc.Get(CcDefine.protect), passiveAlly.Cc.Get(CcDefine.protect)));
                        Check("Talisman application to cargo redirects to its leader",
                            skill.TryApplyPassiveEffect(joiningAlly));
                        Check("Talisman protects for the whole turn", CharacterSkillRegistry.EvaluateIncomingCapture(
                            new CharacterCaptureRequest(2, 0, 1, 1, 1, true)) == CharacterCaptureDecision.Prevent &&
                            CharacterSkillRegistry.EvaluateIncomingCapture(
                                new CharacterCaptureRequest(2, 0, 1, 1, 1, true)) == CharacterCaptureDecision.Prevent &&
                            !CcEffectService.Apply(passiveAlly, CcDefine.Sturn, 1) &&
                            !CcEffectService.Apply(passiveAlly, CcDefine.Retire) &&
                            passiveAlly.State == PieceState.InBoard);
                        CcEffectService.Apply(passiveAlly, CcDefine.protect, turns: 3, charges: 1);
                        CcEffectService.TickOwnerTurn(p1);
                        Check("Talisman expires without shortening another protection",
                            passiveAlly.Cc.Get(CcDefine.protect)?.Charges == 1 &&
                            passiveAlly.Cc.Get(CcDefine.protect)?.RemainingOwnerTurns == 2 &&
                            passiveAlly.Cc.Effects.Count == 1);

                        p1.RuntimeData.ResetPieces(); p2.RuntimeData.ResetPieces();
                        skill.OnPieceRetired();
                        passiveAlly.MoveTo(BoardTileId.Outer01);
                        Check("First entry onto an ally protects both leader and cargo",
                            movement.TryMovePiece(1, 0, 1) && piece.IsStacked &&
                            piece.StackLeaderPieceId == passiveAlly.PieceId &&
                            passiveAlly.Cc.Has(CcDefine.protect) &&
                            ReferenceEquals(piece.Cc.Get(CcDefine.protect), passiveAlly.Cc.Get(CcDefine.protect)));

                        p1.RuntimeData.ResetPieces(); p2.RuntimeData.ResetPieces();
                        skill.OnPieceRetired();
                        piece.MoveTo(BoardTileId.Outer01);
                        passiveAlly.MoveTo(BoardTileId.Outer01);
                        passiveAlly.SetStackGroup(80, joiningAlly.PieceId);
                        joiningAlly.MoveTo(BoardTileId.Outer01);
                        joiningAlly.SetStackGroup(80, joiningAlly.PieceId);
                        p1.RuntimeData.Pieces[3].MoveTo(BoardTileId.Outer04);
                        skill.OnPieceEnteredBoard();
                        Check("Nearest selection skips cargo and chooses the nearby leader",
                            joiningAlly.Cc.Has(CcDefine.protect) &&
                            ReferenceEquals(passiveAlly.Cc.Get(CcDefine.protect), joiningAlly.Cc.Get(CcDefine.protect)) &&
                            !p1.RuntimeData.Pieces[3].Cc.Has(CcDefine.protect) &&
                            !piece.Cc.Has(CcDefine.protect));

                        p1.RuntimeData.ResetPieces(); p2.RuntimeData.ResetPieces();
                        skill.OnPieceRetired();
                        piece.MoveTo(BoardTileId.Outer01);
                        passiveAlly.SetGoal();
                        joiningAlly.MoveTo(BoardTileId.Outer01);
                        CcEffectService.Apply(joiningAlly, CcDefine.Robot_part, 3);
                        var missingTileAlly = p1.RuntimeData.Pieces[3];
                        missingTileAlly.State = PieceState.InBoard;
                        enemy.MoveTo(BoardTileId.Outer01);
                        Check("Nearest selection ignores goal, parts, missing tiles and enemies",
                            !CharacterBoardUtility.FindNearestAlly(manager, 1, 0,
                                piece.CurrentTileId, turns.Settings).HasValue);
                        skill.OnPieceEnteredBoard();
                        Check("No eligible ally never falls back to self",
                            !piece.Cc.Has(CcDefine.protect) &&
                            !enemy.Cc.Has(CcDefine.protect));
                        Check("Talisman rejects self, goal and off-board targets",
                            !skill.TryApplyPassiveEffect(piece) &&
                            !skill.TryApplyPassiveEffect(passiveAlly) &&
                            !skill.TryApplyPassiveEffect(missingTileAlly));
                        passiveAlly.Reset();
                        skill.OnPieceEnteredBoard();
                        Check("Waiting ally is not a talisman target",
                            !passiveAlly.Cc.Has(CcDefine.protect));
                        passiveAlly.MoveTo(BoardTileId.Outer03);
                        skill.OnPieceEnteredBoard();
                        Check("Missing target does not consume passive availability",
                            passiveAlly.Cc.Has(CcDefine.protect));
                        CcEffectService.Remove(passiveAlly, CcDefine.protect);
                        skill.OnPieceEnteredBoard();
                        Check("Passive does not repeat before retirement",
                            !passiveAlly.Cc.Has(CcDefine.protect));
                        CcEffectService.Apply(piece, CcDefine.Retire);
                        Check("Retirement lets the next board entry use the passive again",
                            movement.TryMovePiece(1, 0, 1) &&
                            passiveAlly.Cc.Has(CcDefine.protect) &&
                            !piece.Cc.Has(CcDefine.protect));
                        break;
                    case "003":
                        Check("Solo Hong uses ordinary capture", skill.EvaluateIncomingCapture(
                            new CharacterCaptureRequest(2, 0, 1, 0, 1, true)) == CharacterCaptureDecision.Proceed);
                        break;
                    case "004":
                        Check("Charm passive blocks capture without granting protect", skill.EvaluateIncomingCapture(
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
                        Check("Hero ignores stun, binding and silence but remains capturable",
                            !CcEffectService.Apply(piece, CcDefine.Sturn, 2) &&
                            !CcEffectService.Apply(piece, CcDefine.Binding, 2) &&
                            !CcEffectService.Apply(piece, CcDefine.Silence, 2) &&
                            skill.EvaluateIncomingCapture(
                                new CharacterCaptureRequest(2, 0, 1, 0, 1, true)) ==
                            CharacterCaptureDecision.Proceed);
                        break;
                    case "008":
                        piece.MoveTo(BoardTileId.Outer02);
                        skill.OnMoveCompleted(new CharacterMoveRecord(1, 0, BoardTileId.Outer01, BoardTileId.Outer02, new[] { BoardTileId.Outer02 }));
                        skill.OnOwnerTurnEnded();
                        Check("Wind path stored in CC", piece.Cc.Has(CcDefine.WindPath));
                        passiveAlly.MoveTo(BoardTileId.Outer02);
                        CharacterSkillRegistry.NotifyMoveCompleted(new CharacterMoveRecord(1, 1, BoardTileId.Outer01, BoardTileId.Outer02, new[] { BoardTileId.Outer02 }));
                        Check("Wind moves ally once through Move CC", passiveAlly.CurrentTileId == BoardTileId.Outer03);
                        CharacterSkillRegistry.NotifyMoveCompleted(new CharacterMoveRecord(1, 1, BoardTileId.Outer01, BoardTileId.Outer02, new[] { BoardTileId.Outer02 }));
                        Check("Wind duplicate notification does not move twice", passiveAlly.CurrentTileId == BoardTileId.Outer03);
                        passiveAlly.MoveTo(BoardTileId.Outer02);
                        CharacterSkillRegistry.NotifyMoveCompleted(new CharacterMoveRecord(1, 1,
                            BoardTileId.Outer01, BoardTileId.Outer02, new[] { BoardTileId.Outer02 }));
                        Check("A later real landing can activate Wind again", passiveAlly.CurrentTileId == BoardTileId.Outer03);
                        CcEffectService.TickOwnerTurn(p1);
                        passiveAlly.MoveTo(BoardTileId.Outer01);
                        CharacterBoardUtility.MoveStackAlongPath(p1, passiveAlly,
                            new[] { BoardTileId.Outer02 }, ignoresInstalledItems: true);
                        Check("Simple movement cannot activate Wind", passiveAlly.CurrentTileId == BoardTileId.Outer02);
                        int windStack = p1.RuntimeData.CreateStackGroupId();
                        piece.MoveTo(BoardTileId.Outer02);
                        piece.SetStackGroup(windStack, 0); passiveAlly.SetStackGroup(windStack, 0);
                        skill.OnMoveCompleted(new CharacterMoveRecord(1, 0,
                            BoardTileId.Outer01, BoardTileId.Outer02, new[] { BoardTileId.Outer02 }));
                        CharacterSkillRegistry.NotifyMoveCompleted(new CharacterMoveRecord(1, 1,
                            BoardTileId.Outer01, BoardTileId.Outer02, new[] { BoardTileId.Outer02 }));
                        Check("Wind moves the merged stack leader and cargo",
                            piece.CurrentTileId == BoardTileId.Outer03 && passiveAlly.CurrentTileId == BoardTileId.Outer03);
                        skill.OnMoveCompleted(new CharacterMoveRecord(1, 0,
                            BoardTileId.Outer01, BoardTileId.Outer02, new[] { BoardTileId.Outer02 }));
                        skill.OnOwnerTurnEnded();
                        Check("Wind preserves the extra landing in the next stored path",
                            piece.Cc.Get(CcDefine.WindPath)?.Path.Count == 2 &&
                            piece.Cc.Get(CcDefine.WindPath).Path[1] == BoardTileId.Outer03);
                        turns.Settings.matchComposition = MatchComposition.TwoVsTwo;
                        turns.Settings.isTeamMode = true;
                        var teamWindAlly = p2.RuntimeData.Pieces[0];
                        teamWindAlly.MoveTo(BoardTileId.Outer02);
                        CharacterSkillRegistry.NotifyMoveCompleted(new CharacterMoveRecord(2, 0,
                            BoardTileId.Outer01, BoardTileId.Outer02, new[] { BoardTileId.Outer02 }));
                        Check("Wind assists a different allied player", teamWindAlly.CurrentTileId == BoardTileId.Outer03);
                        turns.Settings.matchComposition = previousComposition;
                        turns.Settings.isTeamMode = previousTeamMode;
                        break;
                    case "009":
                        CcEffectService.Apply(piece, CcDefine.Retire);
                        CcEffectService.TickOwnerTurn(p1); CcEffectService.TickOwnerTurn(p1); CcEffectService.TickOwnerTurn(p1);
                        Check("Unrevived Parts retires on third turn", piece.State == PieceState.Waiting && piece.Cc.Has(CcDefine.Retire));
                        break;
                    case "010":
                        int stack = p1.RuntimeData.CreateStackGroupId();
                        piece.SetStackGroup(stack, 0); passiveAlly.SetStackGroup(stack, 0);
                        Check("Stack bonus waits for its own landing",
                            skill.ModifyMoveCount(new CharacterMoveRequest(1, 0, 2, false)) == 2);
                        Check("Carried piece cannot use active", !CcEffectService.CanUseSkill(passiveAlly));
                        piece.MoveTo(BoardTileId.Inner04); piece.MoveTo(BoardTileId.Corner03);
                        passiveAlly.MoveTo(BoardTileId.Corner03);
                        typeof(CHAR_010_Status).GetMethod("ExecuteActive", BindingFlags.Instance | BindingFlags.NonPublic)
                            .Invoke(skill, new object[] { new CharacterActiveRequest(1, 0), piece });
                        Check("Stack retreat uses the outer route at shortcut exit",
                            piece.CurrentTileId == BoardTileId.Outer11);
                        piece.ClearStack(); passiveAlly.ClearStack();
                        piece.MoveTo(BoardTileId.Inner04); piece.MoveTo(BoardTileId.Corner03);
                        typeof(CHAR_010_Status).GetMethod("ExecuteActive", BindingFlags.Instance | BindingFlags.NonPublic)
                            .Invoke(skill, new object[] { new CharacterActiveRequest(1, 0), piece });
                        Check("Single retreat retraces the incoming shortcut",
                            piece.CurrentTileId == BoardTileId.Inner04);
                        break;
                }
                VerifyReusablePassive(id, skill, movement, p1, p2);
                if (id == "001_2") VerifyImmediateExtraThrows(skill, turns, p1, p2);
                VerifyAnytimeActive(id, skill, turns, p1, p2);
                if (id == "003" || id == "010") VerifyRequestedTurnSequence(id, skill, turns, p1, p2);
                if (id == "003" || id == "004" || id == "008")
                    VerifyNavigationFlow(id, skill, manager, movement, turns, p1, p2);
                if (id == "002" || id == "003" || id == "004" || id == "006" || id == "009")
                    VerifyStackCharacter(id, skill, movement, p1, p2);
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

    // Run only in an isolated headless project, never by closing the user's editor.
    public static void RunBatch()
    {
        Run();
        if (File.Exists(Report)) File.Copy(Report, "CharacterVerificationReport.log", true);
        EditorApplication.Exit(results.Count == 0 || results.Exists(line => line.StartsWith("FAIL")) ? 1 : 0);
    }

    private static void VerifyStackState(PlayerController owner, PlayerController attacker,
        PieceMovementManager movement)
    {
        foreach (int leaderId in new[] { 0, 1 })
        {
            foreach (CcDefine guard in new[] { CcDefine.protect, CcDefine.Charm })
            {
                owner.RuntimeData.ResetPieces(); attacker.RuntimeData.ResetPieces();
                var leader = owner.RuntimeData.Pieces[leaderId];
                var cargo = owner.RuntimeData.Pieces[1 - leaderId];
                leader.MoveTo(BoardTileId.Outer03); cargo.MoveTo(BoardTileId.Outer03);
                int group = owner.RuntimeData.CreateStackGroupId();
                leader.SetStackGroup(group, leaderId); cargo.SetStackGroup(group, leaderId);
                CcEffectService.Apply(cargo, guard, turns: 3, charges: 1);
                string label = $"Stack leader={leaderId} guard={guard}";
                Check(label + " cargo applies one shared state", leader.Cc.Has(guard) &&
                    ReferenceEquals(leader.Cc.Get(guard), cargo.Cc.Get(guard)));
                attacker.RuntimeData.Pieces[0].MoveTo(BoardTileId.Outer02);
                Check(label + " one charge defends every member of the same attack",
                    movement.TryMovePiece(2, 0, 1) && leader.State == PieceState.InBoard &&
                    cargo.State == PieceState.InBoard && !leader.Cc.Has(guard) && !cargo.Cc.Has(guard));
                attacker.RuntimeData.Pieces[1].MoveTo(BoardTileId.Outer02);
                Check(label + " next attack captures every member",
                    movement.TryMovePiece(2, 1, 1) && leader.State == PieceState.Waiting &&
                    cargo.State == PieceState.Waiting && !leader.IsStacked && !cargo.IsStacked);
                Check(label + " cargo-first iteration still grants exactly one capture marker",
                    CcEffectService.ConsumeCapture(leader) && !CcEffectService.ConsumeCapture(cargo) &&
                    !CcEffectService.ConsumeCapture(leader));
            }

            owner.RuntimeData.ResetPieces(); attacker.RuntimeData.ResetPieces();
            var timedLeader = owner.RuntimeData.Pieces[leaderId];
            var timedCargo = owner.RuntimeData.Pieces[1 - leaderId];
            timedLeader.MoveTo(BoardTileId.Outer03); timedCargo.MoveTo(BoardTileId.Outer03);
            CcEffectService.Apply(timedCargo, CcDefine.Silence, 9);
            int timedGroup = owner.RuntimeData.CreateStackGroupId();
            timedLeader.SetStackGroup(timedGroup, leaderId); timedCargo.SetStackGroup(timedGroup, leaderId);
            Check($"Stack {leaderId} ignores cargo's former independent status",
                !timedLeader.Cc.Has(CcDefine.Silence) && !timedCargo.Cc.Has(CcDefine.Silence));
            CcEffectService.Apply(timedCargo, CcDefine.Binding, 2);
            CcEffectService.TickOwnerTurn(owner);
            Check($"Stack {leaderId} duration ticks once for all real members",
                timedLeader.Cc.Get(CcDefine.Binding)?.RemainingOwnerTurns == 1 &&
                ReferenceEquals(timedLeader.Cc.Get(CcDefine.Binding), timedCargo.Cc.Get(CcDefine.Binding)));
            CcEffectService.TickOwnerTurn(owner);
            Check($"Stack {leaderId} expires its shared state together",
                !timedLeader.Cc.Has(CcDefine.Binding) && !timedCargo.Cc.Has(CcDefine.Binding));
            CcEffectService.Apply(timedCargo, CcDefine.Sturn, 2);
            CcEffectService.Remove(timedCargo, CcDefine.Sturn);
            Check($"Stack {leaderId} removing through cargo clears the leader's state",
                !timedLeader.Cc.Has(CcDefine.Sturn) && !timedCargo.Cc.Has(CcDefine.Sturn));
            CcEffectService.Apply(timedCargo, CcDefine.Retire);
            Check($"Stack {leaderId} direct cargo retirement retires the full group without bonus",
                timedLeader.State == PieceState.Waiting && timedCargo.State == PieceState.Waiting &&
                !CcEffectService.ConsumeCapture(timedLeader) && !CcEffectService.ConsumeCapture(timedCargo));

            owner.RuntimeData.ResetPieces(); attacker.RuntimeData.ResetPieces();
            timedLeader.MoveTo(BoardTileId.Outer04); timedCargo.MoveTo(BoardTileId.Outer04);
            timedLeader.SetStackGroup(timedGroup, leaderId); timedCargo.SetStackGroup(timedGroup, leaderId);
            Check($"Stack {leaderId} Yut capture retires all real members without capture bonus",
                movement.TryMovePiece(2, 0, 4) && timedLeader.State == PieceState.Waiting &&
                timedCargo.State == PieceState.Waiting && !CcEffectService.ConsumeCapture(timedLeader) &&
                !CcEffectService.ConsumeCapture(timedCargo));

            owner.RuntimeData.ResetPieces(); attacker.RuntimeData.ResetPieces();
            timedLeader.MoveTo(BoardTileId.Outer03); timedCargo.MoveTo(BoardTileId.Outer03);
            timedLeader.SetStackGroup(timedGroup, leaderId); timedCargo.SetStackGroup(timedGroup, leaderId);
            CcEffectService.Apply(timedLeader, CcDefine.protect, charges: 1);
            var target = new CharacterPieceReference(owner, timedCargo);
            Check($"Stack {leaderId} direct capture API shares leader protection",
                !CcBoardEffects.TryCapture(2, 0, target, 1, true, out var defended) &&
                defended == CharacterCaptureDecision.Prevent && timedLeader.State == PieceState.InBoard &&
                timedCargo.State == PieceState.InBoard);
            Check($"Stack {leaderId} direct capture API later retires the whole group with one marker",
                CcBoardEffects.TryCapture(2, 0, target, 1, true, out var captured) &&
                captured == CharacterCaptureDecision.Proceed && timedLeader.State == PieceState.Waiting &&
                timedCargo.State == PieceState.Waiting && CcEffectService.ConsumeCapture(timedLeader) &&
                !CcEffectService.ConsumeCapture(timedCargo));
        }
    }

    private static void VerifyStackCharacter(string id, CharacterStatusBehaviour first,
        PieceMovementManager movement, PlayerController owner, PlayerController attacker)
    {
        var host = new GameObject("Stack regression companion " + id);
        host.transform.SetParent(owner.transform);
        var second = (CharacterStatusBehaviour)host.AddComponent(first.GetType());
        second.Initialize(first.Data);
        typeof(CharacterStatusBehaviour).GetMethod("TryRegisterRuntime", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(second, null);
        try
        {
            foreach (int leaderId in new[] { 0, 1 })
            {
                owner.RuntimeData.ResetPieces(); attacker.RuntimeData.ResetPieces();
                first.OnPieceRetired(); second.OnPieceRetired();
                var leader = owner.RuntimeData.Pieces[leaderId];
                var cargo = owner.RuntimeData.Pieces[1 - leaderId];
                string label = $"CHAR_{id} stack leader={leaderId}";
                if (id == "002")
                {
                    bool entered = movement.TryMovePiece(1, leaderId, 1) &&
                        movement.TryMovePiece(1, cargo.PieceId, 1);
                    Check(label + " two Do entries share the leader's talisman", entered &&
                        leader.StackLeaderPieceId == leaderId && leader.Cc.Has(CcDefine.protect) &&
                        ReferenceEquals(leader.Cc.Get(CcDefine.protect), cargo.Cc.Get(CcDefine.protect)));
                    Check(label + " enemy landing leaves both real pieces alive",
                        movement.TryMovePiece(2, 0, 1) && leader.State == PieceState.InBoard &&
                        cargo.State == PieceState.InBoard && leader.StackGroupId == cargo.StackGroupId);
                    CcEffectService.TickOwnerTurn(owner);
                    Check(label + " talisman expires for the whole stack",
                        !leader.Cc.Has(CcDefine.protect) && !cargo.Cc.Has(CcDefine.protect));
                    continue;
                }

                leader.MoveTo(BoardTileId.Outer03); cargo.MoveTo(BoardTileId.Outer03);
                int group = owner.RuntimeData.CreateStackGroupId();
                leader.SetStackGroup(group, leaderId); cargo.SetStackGroup(group, leaderId);
                attacker.RuntimeData.Pieces[0].MoveTo(BoardTileId.Outer02);
                if (id == "003")
                {
                    Check(label + " numerical defense saves the entire real stack",
                        movement.TryMovePiece(2, 0, 1) && leader.State == PieceState.InBoard &&
                        cargo.State == PieceState.InBoard);
                    attacker.RuntimeData.ResetPieces();
                    CcEffectService.Apply(leader, CcDefine.Stack_fake);
                    var enemyLeader = attacker.RuntimeData.Pieces[1];
                    var enemyCargo = attacker.RuntimeData.Pieces[2];
                    enemyLeader.MoveTo(BoardTileId.Outer02); enemyCargo.MoveTo(BoardTileId.Outer02);
                    int enemyGroup = attacker.RuntimeData.CreateStackGroupId();
                    enemyLeader.SetStackGroup(enemyGroup, 1); enemyCargo.SetStackGroup(enemyGroup, 1);
                    Check(label + " consumes clones while keeping every real member alive",
                        movement.TryMovePiece(2, 1, 1) && !leader.Cc.Has(CcDefine.Stack_fake) &&
                        leader.State == PieceState.InBoard && cargo.State == PieceState.InBoard);
                    enemyLeader.MoveTo(BoardTileId.Outer02); enemyCargo.MoveTo(BoardTileId.Outer02);
                    Check(label + " equal real stacks are captured together",
                        movement.TryMovePiece(2, 1, 1) && leader.State == PieceState.Waiting &&
                        cargo.State == PieceState.Waiting);
                }
                else if (id == "004")
                {
                    Check(label + " leader charm protects every real member",
                        movement.TryMovePiece(2, 0, 1) && leader.State == PieceState.InBoard && cargo.State == PieceState.InBoard);
                    attacker.RuntimeData.Pieces[1].MoveTo(BoardTileId.Outer02);
                    Check(label + " cargo cannot independently reactivate charm",
                        movement.TryMovePiece(2, 1, 1) && leader.State == PieceState.Waiting && cargo.State == PieceState.Waiting);
                }
                else if (id == "006")
                {
                    Check(label + " leader immunity protects the whole stack",
                        !CcEffectService.Apply(cargo, CcDefine.Binding, 2) &&
                        !leader.Cc.Has(CcDefine.Binding) && !cargo.Cc.Has(CcDefine.Binding));
                    var leaderStatus = leaderId == 0 ? first : second;
                    var noImmunity = UnityEngine.Object.Instantiate(leaderStatus.Data);
                    noImmunity.passive_Immunities = Array.Empty<CcDefine>();
                    var originalData = leaderStatus.Data;
                    leaderStatus.Initialize(noImmunity);
                    Check(label + " cargo immunity cannot override its unprotected leader",
                        CcEffectService.Apply(cargo, CcDefine.Binding, 2) &&
                        leader.Cc.Has(CcDefine.Binding) && cargo.Cc.Has(CcDefine.Binding));
                    CcEffectService.Remove(leader, CcDefine.Binding);
                    leaderStatus.Initialize(originalData);
                    UnityEngine.Object.DestroyImmediate(noImmunity);

                    attacker.RuntimeData.ResetPieces();
                    var enemyLeader = attacker.RuntimeData.Pieces[0];
                    var enemyCargo = attacker.RuntimeData.Pieces[1];
                    enemyLeader.MoveTo(BoardTileId.Outer04); enemyCargo.MoveTo(BoardTileId.Outer04);
                    int enemyGroup = attacker.RuntimeData.CreateStackGroupId();
                    enemyLeader.SetStackGroup(enemyGroup, 0); enemyCargo.SetStackGroup(enemyGroup, 0);
                    var turns = UnityEngine.Object.FindFirstObjectByType<TestTurnManager>();
                    turns.CurrentTurn.currentPlayer = PlayerSlot.Player1;
                    turns.CurrentTurn.currentPhase = TurnPhase.WaitAction;
                    Set(turns, "pendingSkillThrows", 0);
                    CcEffectService.Apply(enemyLeader, CcDefine.protect, charges: 1);
                    typeof(CHAR_006_Status).GetMethod("ExecuteActive", BindingFlags.Instance | BindingFlags.NonPublic)
                        .Invoke(leaderStatus, new object[] { new CharacterActiveRequest(1, leaderId), leader });
                    Check(label + " one area hit consumes one guard for the entire enemy stack",
                        enemyLeader.State == PieceState.InBoard && enemyCargo.State == PieceState.InBoard &&
                        !enemyLeader.Cc.Has(CcDefine.protect) && (int)Get(turns, "pendingSkillThrows") == 0);
                    typeof(CHAR_006_Status).GetMethod("ExecuteActive", BindingFlags.Instance | BindingFlags.NonPublic)
                        .Invoke(leaderStatus, new object[] { new CharacterActiveRequest(1, leaderId), leader });
                    Check(label + " area retirement counts actual real members for its reward",
                        enemyLeader.State == PieceState.Waiting && enemyCargo.State == PieceState.Waiting &&
                        (int)Get(turns, "pendingSkillThrows") == 2);
                }
                else if (id == "009")
                {
                    CcEffectService.Apply(cargo, CcDefine.Retire);
                    CcEffectService.TickOwnerTurn(owner);
                    Check(label + " retirement converts the entire group to Parts",
                        leader.State == PieceState.InBoard && cargo.State == PieceState.InBoard &&
                        leader.IsStacked && leader.Cc.Get(CcDefine.Robot_part)?.RemainingOwnerTurns == 2 &&
                        ReferenceEquals(leader.Cc.Get(CcDefine.Robot_part), cargo.Cc.Get(CcDefine.Robot_part)));
                    var rescuer = owner.RuntimeData.Pieces[2]; rescuer.MoveTo(BoardTileId.Outer02);
                    Check(label + " ally rescues every member into its own stack",
                        movement.TryMovePiece(1, 2, 1) && leader.StackLeaderPieceId == 2 && cargo.StackLeaderPieceId == 2 &&
                        !leader.Cc.Has(CcDefine.Robot_part) && !cargo.Cc.Has(CcDefine.Robot_part) &&
                        CcBoardEffects.CountStackUnits(owner, rescuer) == 3);
                    owner.RuntimeData.ResetPieces(); attacker.RuntimeData.ResetPieces();
                    first.OnPieceRetired(); second.OnPieceRetired();
                    leader.MoveTo(BoardTileId.Outer04); cargo.MoveTo(BoardTileId.Outer04);
                    leader.SetStackGroup(group, leaderId); cargo.SetStackGroup(group, leaderId);
                    Check(label + " Yut capture converts every member without a second retirement",
                        movement.TryMovePiece(2, 0, 4) && leader.State == PieceState.InBoard &&
                        cargo.State == PieceState.InBoard && leader.Cc.Has(CcDefine.Robot_part) &&
                        cargo.Cc.Has(CcDefine.Robot_part) && !leader.Cc.Has(CcDefine.Kill));
                    owner.RuntimeData.ResetPieces(); first.OnPieceRetired(); second.OnPieceRetired();
                    leader.MoveTo(BoardTileId.Outer03); cargo.MoveTo(BoardTileId.Outer03);
                    leader.SetStackGroup(group, leaderId); cargo.SetStackGroup(group, leaderId);
                    CcEffectService.Apply(cargo, CcDefine.Retire);
                    for (int tick = 0; tick < 3; tick++) CcEffectService.TickOwnerTurn(owner);
                    Check(label + " Parts expiration retires all members without reconversion",
                        leader.State == PieceState.Waiting && cargo.State == PieceState.Waiting &&
                        !leader.Cc.Has(CcDefine.Robot_part) && !cargo.Cc.Has(CcDefine.Robot_part));
                }
            }
        }
        finally
        {
            CharacterSkillRegistry.Unregister(second);
            UnityEngine.Object.DestroyImmediate(host);
            owner.RuntimeData.ResetPieces(); attacker.RuntimeData.ResetPieces();
        }
    }

    private static void VerifyDisabledCharacterSkills(string id, CharacterStatusBehaviour skill,
        PieceMovementManager movement, TestTurnManager turns, PlayerController p1, PlayerController p2)
    {
        GameMode previousMode = turns.Settings.gameMode;
        bool previousUseSkill = turns.Settings.useSkill;
        TurnPhase previousPhase = turns.CurrentTurn.currentPhase;
        try
        {
            foreach (var setting in new[] { (GameMode.Classic, false), (GameMode.Classic, true),
                (GameMode.Escape, false), (GameMode.KillTheKing, false) })
            {
                turns.Settings.gameMode = setting.Item1;
                turns.Settings.useSkill = setting.Item2;
                turns.CurrentTurn.currentPhase = TurnPhase.WaitAction;
                p1.RuntimeData.ResetPieces();
                p2.RuntimeData.ResetPieces();
                var piece = p1.RuntimeData.Pieces[0];
                var enemy = p2.RuntimeData.Pieces[0];
                string label = id + " " + setting.Item1 + " useSkill=" + setting.Item2;
                Check(label + " preserves character registration while disabling skills",
                    !CharacterSkillRegistry.AreSkillsEnabled &&
                    CharacterSkillRegistry.TryGet(1, 0, out var registered) && registered == skill &&
                    skill.isActiveAndEnabled && !skill.IsPassiveReady);

                CharacterSkillRegistry.NotifyOwnerTurnStarted(1);
                CharacterSkillRegistry.RequestSkillPoint(1, 10);
                piece.MoveTo(BoardTileId.Outer01);
                enemy.MoveTo(BoardTileId.Outer02);
                Check(label + " rejects UI and direct active requests without SP/cooldown",
                    !CharacterSkillRegistry.CanUseActive(1, 0) && !skill.IsActiveUsableInCurrentPhase() &&
                    !CharacterSkillRegistry.TryUseActive(new CharacterActiveRequest(1, 0, 2, 0)).Succeeded &&
                    !skill.TryUseActive(new CharacterActiveRequest(1, 0, 2, 0)).Succeeded &&
                    CharacterSkillRegistry.GetSkillPoints(1) == 0 &&
                    CharacterSkillRegistry.GetMaxSkillPoints(1) == 0 &&
                    CharacterSkillRegistry.GetRemainingActiveCooldown(1, skill.Data) == 0);

                p1.RuntimeData.ResetPieces();
                p2.RuntimeData.ResetPieces();
                bool entered = movement.TryMovePiece(1, 0, 1);
                CharacterSkillRegistry.NotifyOwnerTurnEnded(1);
                Check(label + " ordinary entry has no extra movement, CC or turn-end passive",
                    entered && piece.CurrentTileId == BoardTileId.Outer01 &&
                    piece.Cc.Effects.Count == 0 && CharacterSkillRegistry.GetSkillPoints(1) == 0 &&
                    (!(skill is CHAR_001_1_Status yunnuri) || !yunnuri.HasPendingForcedMove));

                var table = new[] { (YutResult.BackDo, 25f), (YutResult.Do, 75f) };
                Check(label + " preserves yut probabilities and default bonus throws",
                    ReferenceEquals(CharacterSkillRegistry.ModifyYutProbability(1, table), table) &&
                    CharacterSkillRegistry.ShouldGrantExtraThrow(1, YutResult.Yut, true) &&
                    !CharacterSkillRegistry.ShouldGrantExtraThrow(1, YutResult.Do, false));

                piece.MoveTo(BoardTileId.Outer02);
                enemy.MoveTo(BoardTileId.Outer01);
                bool captured = movement.TryMovePiece(2, 0, 1);
                Check(label + " ordinary capture ignores character shields, clones and parts",
                    captured && piece.State == PieceState.Waiting &&
                    !piece.Cc.Has(CcDefine.Robot_part) && CcEffectService.ConsumeCapture(piece) &&
                    !CcEffectService.ConsumeCapture(piece));

                piece.MoveTo(BoardTileId.Outer03);
                bool stunned = CcEffectService.Apply(piece, CcDefine.Sturn, 2);
                Check(label + " generic CC remains available without character immunity",
                    !skill.IsImmuneToEffect(CcDefine.Sturn) && stunned && !CcEffectService.CanMove(piece));
                CcEffectService.Remove(piece, CcDefine.Sturn);
                CcEffectService.Apply(piece, CcDefine.Retire);
                Check(label + " generic retirement does not activate Robot passive",
                    piece.State == PieceState.Waiting && piece.Cc.Has(CcDefine.Retire) &&
                    !piece.Cc.Has(CcDefine.Robot_part) && !CcEffectService.ConsumeCapture(piece));
            }
        }
        finally
        {
            turns.Settings.gameMode = previousMode;
            turns.Settings.useSkill = previousUseSkill;
            turns.CurrentTurn.currentPhase = previousPhase;
            p1.RuntimeData.ResetPieces();
            p2.RuntimeData.ResetPieces();
        }
        Check(id + " Escape skills remain enabled after Classic verification",
            CharacterSkillRegistry.AreSkillsEnabled);
    }

    private static void VerifySkillInputPhases(string id, CharacterStatusBehaviour skill,
        TestTurnManager turns, PlayerRuntimeData.PieceRuntimeData piece)
    {
        TurnPhase previous = turns.CurrentTurn.currentPhase;
        foreach (TurnPhase phase in Enum.GetValues(typeof(TurnPhase)))
        {
            turns.CurrentTurn.currentPhase = phase;
            bool expected = phase == TurnPhase.WaitThrow || phase == TurnPhase.WaitAction;
            Check(id + " input phase " + phase, skill.IsActiveUsableInCurrentPhase() == expected);
            if (!expected)
            {
                int before = CharacterSkillRegistry.GetSkillPoints(1);
                Check(id + " rejects active execution during " + phase,
                    !CharacterSkillRegistry.TryUseActive(new CharacterActiveRequest(1, 0, 2, 0)).Succeeded &&
                    CharacterSkillRegistry.GetSkillPoints(1) == before &&
                    CharacterSkillRegistry.GetRemainingActiveCooldown(1, skill.Data) == 0);
            }
        }
        turns.CurrentTurn.currentPlayer = PlayerSlot.Player2;
        turns.CurrentTurn.currentPhase = previous;
        int points = CharacterSkillRegistry.GetSkillPoints(1);
        Check(id + " rejects another player's turn without spending SP",
            !CharacterSkillRegistry.CanUseActive(1, 0) &&
            !CharacterSkillRegistry.TryUseActive(new CharacterActiveRequest(1, 0)).Succeeded &&
            CharacterSkillRegistry.GetSkillPoints(1) == points);
        turns.CurrentTurn.currentPlayer = PlayerSlot.Player1;
        turns.CurrentTurn.currentPhase = previous;
    }

    private static void PrepareActiveAttempt(TestTurnManager turns, PlayerController p1, PlayerController p2)
    {
        CcEffectService.EndOwnerTurn(1);
        p1.RuntimeData.ResetPieces();
        p2.RuntimeData.ResetPieces();
        turns.CurrentTurn.currentPlayer = PlayerSlot.Player1;
        // A previous Nak test really advances the turn. Keep the synthetic fixture's
        // order cursor consistent when starting another independent Player1 case.
        turns.TurnOrder.currentIndex = turns.TurnOrder.order.IndexOf(PlayerSlot.Player1);
        turns.CurrentTurn.currentPhase = TurnPhase.WaitThrow;
        turns.CurrentTurn.isGameEnded = false;
        turns.CurrentTurn.extraThrowByYutMoCount = 0;
        Set(turns, "pendingSkillThrows", 0);
        Set(turns, "pendingCaptureThrows", 0);
        Set(turns, "pendingRegularThrow", true);
        ((List<YutThrowData>)Get(turns, "pendingResults")).Clear();
        CharacterSkillRegistry.NotifyOwnerTurnStarted(1);
        CharacterSkillRegistry.NotifyOwnerTurnStarted(1);
        CharacterSkillRegistry.RequestSkillPoint(1, 10);
    }

    private static void VerifyAnytimeActive(string id, CharacterStatusBehaviour skill,
        TestTurnManager turns, PlayerController p1, PlayerController p2)
    {
        var keyboard = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>();
        LocalSelectionInput.SelectSharedKeyboard();
        var ui = new GameObject("Anytime skill button " + id, typeof(RectTransform),
            typeof(UnityEngine.UI.Button), typeof(CanvasGroup)).AddComponent<ActiveSkillButtonController>();
        try
        {
            if (Get(ui, "skillButton") == null)
                typeof(ActiveSkillButtonController).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(ui, null);
            typeof(ActiveSkillButtonController).GetMethod("SubscribeToTurnEvents", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(ui, null);
            foreach (TurnPhase phase in new[] { TurnPhase.WaitThrow, TurnPhase.WaitAction })
            {
                PrepareActiveAttempt(turns, p1, p2);
                skill.OnPieceRetired();
                turns.CurrentTurn.currentPhase = phase;
                ui.RefreshForCurrentTurn();
                Check(id + " preserves board-caster requirement during " + phase,
                    CharacterSkillRegistry.CanUseActive(1, 0) == !skill.RequiresCasterPieceSelection &&
                    ui.GetComponent<UnityEngine.UI.Button>().interactable == !skill.RequiresCasterPieceSelection);

                var piece = p1.RuntimeData.Pieces[0]; piece.MoveTo(BoardTileId.Outer02);
                p2.RuntimeData.Pieces[0].MoveTo(BoardTileId.Outer03);
                typeof(TestTurnManager).GetMethod("BeginTurnFor", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(turns, new object[] { PlayerSlot.Player1 });
                Check(id + " turn-start event enables button before the first throw",
                    turns.CurrentTurn.currentPhase == TurnPhase.WaitThrow && turns.PendingResults.Count == 0 &&
                    CharacterSkillRegistry.CanUseActive(1, 0) && ui.GetComponent<UnityEngine.UI.Button>().interactable);
                var pending = (List<YutThrowData>)Get(turns, "pendingResults");
                if (phase == TurnPhase.WaitAction)
                {
                    turns.CurrentTurn.currentPhase = phase;
                    Set(turns, "pendingRegularThrow", false);
                    pending.Add(new YutThrowData { player = PlayerSlot.Player1, result = YutResult.Geol });
                }
                ui.RefreshForCurrentTurn();
                ui.SetCasterPiece(0);
                ui.SetTarget(2, 0);
                int before = CharacterSkillRegistry.GetSkillPoints(1);
                Check(id + " button and registry agree during " + phase,
                    ui.GetComponent<UnityEngine.UI.Button>().interactable && CharacterSkillRegistry.CanUseActive(1, 0));
                bool succeeded = (bool)typeof(ActiveSkillButtonController).GetMethod("UseCurrentActiveSkill",
                    BindingFlags.Instance | BindingFlags.NonPublic).Invoke(ui, null);
                Check(id + " button executes and spends SP exactly once during " + phase,
                    succeeded && CharacterSkillRegistry.GetSkillPoints(1) == before - skill.ActiveSkillPointCost &&
                    CharacterSkillRegistry.GetRemainingActiveCooldown(1, skill.Data) == skill.ActiveCooldownTurns);
                Check(id + " cooldown/SP disable navigation readiness during " + phase,
                    !CharacterSkillRegistry.CanUseActive(1, 0));
                if (id == "010")
                    Check(id + " retreat ends turn during " + phase,
                        piece.CurrentTileId == BoardTileId.Outer01 && turns.CurrentTurn.currentPlayer == PlayerSlot.Player2 &&
                        turns.CurrentTurn.currentPhase == TurnPhase.WaitThrow && pending.Count == 0 &&
                        (bool)Get(turns, "pendingRegularThrow"));
                else
                    Check(id + " skill preserves unused throws/results during " + phase,
                        turns.CurrentTurn.currentPlayer == PlayerSlot.Player1 &&
                        pending.Count == (phase == TurnPhase.WaitAction ? 1 : 0) &&
                        (bool)Get(turns, "pendingRegularThrow") == (phase == TurnPhase.WaitThrow));
            }
        }
        finally
        {
            typeof(ActiveSkillButtonController).GetMethod("UnsubscribeFromTurnEvents", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(ui, null);
            UnityEngine.Object.DestroyImmediate(ui.gameObject);
            UnityEngine.InputSystem.InputSystem.RemoveDevice(keyboard);
        }
    }

    private static void VerifyRequestedTurnSequence(string id, CharacterStatusBehaviour skill,
        TestTurnManager turns, PlayerController p1, PlayerController p2)
    {
        var keyboard = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>();
        LocalSelectionInput.SelectSharedKeyboard();
        var rules = new GameObject("Requested turn sequence rules " + id).AddComponent<TestYutRuleManager>();
        var wins = new GameObject("Requested turn sequence wins " + id).AddComponent<TestWinConditionManager>();
        object previousRules = Get(turns, "yutRuleManager");
        object previousWins = Get(turns, "winConditionManager");
        Set(turns, "yutRuleManager", rules); Set(turns, "winConditionManager", wins);
        wins.Initialize(turns.Settings);
        try
        {
            PrepareActiveAttempt(turns, p1, p2);
            skill.OnPieceRetired();
            var piece = p1.RuntimeData.Pieces[0]; piece.MoveTo(BoardTileId.Outer02);
            if (id == "003")
            {
                rules.ProbabilityTableProvider = _ => new[] { (YutResult.Gae, 100f) };
                Check("Hong turn starts with an available clone skill and no thrown result",
                    turns.PendingResults.Count == 0 && CharacterSkillRegistry.CanUseActive(1, 0));
                Check("Hong clones before throwing while preserving the base throw",
                    CharacterSkillRegistry.TryUseActive(new CharacterActiveRequest(1, 0)).Succeeded &&
                    piece.Cc.Get(CcDefine.Stack_fake)?.Value == 1 && turns.CurrentTurn.currentPhase == TurnPhase.WaitThrow &&
                    (bool)Get(turns, "pendingRegularThrow") && turns.PendingResults.Count == 0);
                turns.RequestThrow();
                Check("Hong can throw normally after using Clone Technique",
                    turns.CurrentTurn.currentPhase == TurnPhase.WaitAction && turns.PendingResults.Count == 1 &&
                    turns.PendingResults[0].result == YutResult.Gae && piece.Cc.Get(CcDefine.Stack_fake)?.Value == 1);
            }
            else
            {
                int roll = 0;
                rules.ProbabilityTableProvider = _ => new[] { (roll++ == 0 ? YutResult.Yut : YutResult.Geol, 100f) };
                Check("Soldier skill is available before throwing Yut and Geol", CharacterSkillRegistry.CanUseActive(1, 0));
                turns.RequestThrow(); turns.RequestThrow();
                Check("Soldier can still use skill after Yut and Geol are stored",
                    turns.PendingResults.Count == 2 && turns.CurrentTurn.currentPhase == TurnPhase.WaitAction &&
                    CharacterSkillRegistry.CanUseActive(1, 0));
                turns.RequestMovePiece(0, YutResult.Yut);
                Check("Soldier can use retreat after moving Yut while Geol remains",
                    turns.CurrentTurn.currentPlayer == PlayerSlot.Player1 && turns.CurrentTurn.currentPhase == TurnPhase.WaitAction &&
                    turns.PendingResults.Count == 1 && turns.PendingResults[0].result == YutResult.Geol &&
                    CharacterSkillRegistry.CanUseActive(1, 0));
                var before = piece.CurrentTileId;
                int points = CharacterSkillRegistry.GetSkillPoints(1);
                Check("Soldier retreat discards remaining Geol and advances the turn once",
                    CharacterSkillRegistry.TryUseActive(new CharacterActiveRequest(1, 0)).Succeeded &&
                    piece.CurrentTileId != before && turns.CurrentTurn.currentPlayer == PlayerSlot.Player2 &&
                    turns.CurrentTurn.currentPhase == TurnPhase.WaitThrow && turns.PendingResults.Count == 0 &&
                    CharacterSkillRegistry.GetSkillPoints(1) == points - skill.ActiveSkillPointCost &&
                    (int)Get(turns, "pendingSkillThrows") == 0 && (int)Get(turns, "pendingCaptureThrows") == 0);
            }
        }
        finally
        {
            Set(turns, "yutRuleManager", previousRules); Set(turns, "winConditionManager", previousWins);
            UnityEngine.Object.DestroyImmediate(wins.gameObject);
            UnityEngine.Object.DestroyImmediate(rules.gameObject);
            UnityEngine.InputSystem.InputSystem.RemoveDevice(keyboard);
        }
    }

    private static void VerifyImmediateExtraThrows(CharacterStatusBehaviour skill,
        TestTurnManager turns, PlayerController p1, PlayerController p2)
    {
        var rules = new GameObject("Deterministic skill throw rules").AddComponent<TestYutRuleManager>();
        rules.ProbabilityTableProvider = _ => new[] { (YutResult.Gae, 100f) };
        Set(turns, "yutRuleManager", rules);
        var keyboard = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>();
        LocalSelectionInput.SelectSharedKeyboard();
        try
        {
            foreach (bool beforeFirstThrow in new[] { false, true })
            {
                PrepareActiveAttempt(turns, p1, p2);
                var pending = (List<YutThrowData>)Get(turns, "pendingResults");
                if (!beforeFirstThrow)
                {
                    pending.Add(new YutThrowData { player = PlayerSlot.Player1, result = YutResult.Gae });
                    turns.CurrentTurn.currentPhase = TurnPhase.WaitAction;
                    Set(turns, "pendingRegularThrow", false);
                }
                Check("001_2 active grants additional throw " + beforeFirstThrow,
                    CharacterSkillRegistry.TryUseActive(new CharacterActiveRequest(1, 0)).Succeeded &&
                    turns.CurrentTurn.currentPhase == TurnPhase.WaitThrow);
                if (beforeFirstThrow)
                {
                    Check("001_2 before first throw grants immediately without replacing base throw",
                        (int)Get(turns, "pendingSkillThrows") == 1 &&
                        (bool)Get(turns, "pendingRegularThrow") &&
                        !p1.RuntimeData.Pieces[0].Cc.Has(CcDefine.Get_throw));
                    turns.RequestThrow();
                    Check("001_2 first throw preserves one extra opportunity",
                        turns.CurrentTurn.currentPhase == TurnPhase.WaitThrow && pending.Count == 1 &&
                        (int)Get(turns, "pendingSkillThrows") == 1);
                }
                turns.RequestThrow();
                Check("001_2 can throw again without moving and retains both results " + beforeFirstThrow,
                    pending.Count == 2 && turns.CurrentTurn.currentPhase == TurnPhase.WaitAction &&
                    (int)Get(turns, "pendingSkillThrows") == 0 &&
                    turns.CurrentTurn.extraThrowByYutMoCount == 0 &&
                    !p1.RuntimeData.Pieces[0].Cc.Has(CcDefine.Get_throw));
            }
            PrepareActiveAttempt(turns, p1, p2);
            int roll = 0;
            rules.ProbabilityTableProvider = _ => new[] { (roll++ == 0 ? YutResult.Yut : YutResult.Gae, 100f) };
            CharacterSkillRegistry.TryUseActive(new CharacterActiveRequest(1, 0));
            turns.RequestThrow();
            turns.RequestThrow();
            if (turns.CurrentTurn.currentPhase == TurnPhase.WaitThrow) turns.RequestThrow();
            Check("001_2 first Yut plus skill preserves base, natural bonus and skill throw",
                turns.PendingResults.Count == 3 && turns.CurrentTurn.extraThrowByYutMoCount == 1,
                $"Results={turns.PendingResults.Count}, Phase={turns.CurrentTurn.currentPhase}");

            PrepareActiveAttempt(turns, p1, p2);
            roll = 0;
            rules.ProbabilityTableProvider = _ => new[] { (roll++ == 0 ? YutResult.Mo : YutResult.Gae, 100f) };
            CharacterSkillRegistry.TryUseActive(new CharacterActiveRequest(1, 0));
            for (int i = 0; i < 4 && turns.CurrentTurn.currentPhase == TurnPhase.WaitThrow; i++) turns.RequestThrow();
            Check("001_2 first Mo plus skill retains three throws",
                turns.PendingResults.Count == 3 && turns.CurrentTurn.extraThrowByYutMoCount == 1);

            PrepareActiveAttempt(turns, p1, p2);
            roll = 0;
            rules.ProbabilityTableProvider = _ =>
            {
                int index = roll++;
                return new[] { (index == 0 ? YutResult.Yut : index == 1 ? YutResult.Mo : YutResult.Gae, 100f) };
            };
            CharacterSkillRegistry.TryUseActive(new CharacterActiveRequest(1, 0));
            for (int i = 0; i < 5 && turns.CurrentTurn.currentPhase == TurnPhase.WaitThrow; i++) turns.RequestThrow();
            Check("001_2 chained Yut Mo plus skill retains four throws",
                turns.PendingResults.Count == 4 && turns.CurrentTurn.extraThrowByYutMoCount == 2);

            PrepareActiveAttempt(turns, p1, p2);
            turns.CurrentTurn.extraThrowByYutMoCount = GameRuleDefine.MaxYutMoExtraThrowCount;
            rules.ProbabilityTableProvider = _ => new[] { (YutResult.Yut, 100f) };
            CharacterSkillRegistry.TryUseActive(new CharacterActiveRequest(1, 0));
            for (int i = 0; i < 3 && turns.CurrentTurn.currentPhase == TurnPhase.WaitThrow; i++) turns.RequestThrow();
            Check("001_2 skill remains available beyond the natural extra-throw limit",
                turns.PendingResults.Count == 2 && turns.CurrentTurn.currentPhase == TurnPhase.WaitAction &&
                turns.CurrentTurn.extraThrowByYutMoCount == GameRuleDefine.MaxYutMoExtraThrowCount);

            PrepareActiveAttempt(turns, p1, p2);
            turns.CurrentTurn.currentPhase = TurnPhase.WaitAction;
            Set(turns, "pendingRegularThrow", false);
            Set(turns, "pendingCaptureThrows", 2);
            ((List<YutThrowData>)Get(turns, "pendingResults")).Add(
                new YutThrowData { player = PlayerSlot.Player1, result = YutResult.Gae });
            roll = 0;
            rules.ProbabilityTableProvider = _ => new[] { (roll++ == 0 ? YutResult.Yut : YutResult.Gae, 100f) };
            CharacterSkillRegistry.TryUseActive(new CharacterActiveRequest(1, 0));
            for (int i = 0; i < 5 && turns.CurrentTurn.currentPhase == TurnPhase.WaitThrow; i++) turns.RequestThrow();
            int captureThrows = 0;
            foreach (var result in turns.PendingResults) if (result.isBonusThrowFromCapture) captureThrows++;
            Check("Capture, natural and skill throw credits are independent",
                turns.PendingResults.Count == 5 && captureThrows == 2 &&
                (int)Get(turns, "pendingSkillThrows") == 0 && (int)Get(turns, "pendingCaptureThrows") == 0);

            PrepareActiveAttempt(turns, p1, p2);
            rules.ProbabilityTableProvider = _ => new[] { (YutResult.Nak, 100f) };
            CharacterSkillRegistry.TryUseActive(new CharacterActiveRequest(1, 0));
            turns.RequestThrow();
            Check("Nak ends the turn and discards unused skill throws",
                turns.CurrentTurn.currentPlayer == PlayerSlot.Player2 &&
                turns.PendingResults.Count == 0 && (int)Get(turns, "pendingSkillThrows") == 0 &&
                (bool)Get(turns, "pendingRegularThrow"));
        }
        finally
        {
            UnityEngine.InputSystem.InputSystem.RemoveDevice(keyboard);
            UnityEngine.Object.DestroyImmediate(rules.gameObject);
        }
    }

    private static void VerifyReusablePassive(string id, CharacterStatusBehaviour skill,
        PieceMovementManager movement, PlayerController p1, PlayerController p2)
    {
        if (!skill.ResetsPassiveOnBoardReentry) return;
        foreach (string exit in new[] { "Capture", "Retire", "Goal", "WaitingReset" })
        {
            p1.RuntimeData.ResetPieces();
            p2.RuntimeData.ResetPieces();
            skill.OnPieceRetired();
            var piece = p1.RuntimeData.Pieces[0];
            var ally = p1.RuntimeData.Pieces[1];
            if (id == "002") ally.MoveTo(BoardTileId.Outer03);
            Check(id + " first passive applies before " + exit,
                movement.TryMovePiece(1, 0, 1) && ProbeReusablePassive(id, skill, ally));
            if (exit == "Goal")
            {
                piece.SetGoal();
                CharacterSkillRegistry.NotifyMoveCompleted(new CharacterMoveRecord(1, 0,
                    BoardTileId.Outer16, BoardTileId.None, new[] { BoardTileId.None }));
                piece.State = PieceState.Waiting;
            }
            else if (exit == "WaitingReset") piece.Reset();
            else CcEffectService.Apply(piece, exit == "Capture" ? CcDefine.Capture : CcDefine.Retire);
            CcEffectService.Remove(ally, CcDefine.protect);
            Check(id + " passive rearms after " + exit + " and reentry",
                movement.TryMovePiece(1, 0, 1) && ProbeReusablePassive(id, skill, ally));
        }
    }

    private static bool ProbeReusablePassive(string id, CharacterStatusBehaviour skill,
        PlayerRuntimeData.PieceRuntimeData ally)
    {
        if (id == "001_1")
        {
            var yunnuri = (CHAR_001_1_Status)skill;
            return yunnuri.HasPendingForcedMove && yunnuri.ExecutePendingForcedMove();
        }
        if (id == "002") return ally.Cc.Has(CcDefine.protect);
        if (id == "004")
        {
            var capture = new CharacterCaptureRequest(2, 0, 1, 0, 1, true);
            return skill.EvaluateIncomingCapture(capture) == CharacterCaptureDecision.Prevent &&
                skill.EvaluateIncomingCapture(capture) == CharacterCaptureDecision.Proceed;
        }
        return false;
    }

    private static void VerifyNavigationFlow(string id, CharacterStatusBehaviour skill,
        PlayerManager players, PieceMovementManager movement, TestTurnManager turns,
        PlayerController p1, PlayerController p2)
    {
        PrepareActiveAttempt(turns, p1, p2);
        turns.CurrentTurn.currentPhase = TurnPhase.WaitThrow;
        p1.RuntimeData.Pieces[0].MoveTo(BoardTileId.Outer01);
        p2.RuntimeData.Pieces[0].MoveTo(BoardTileId.Outer02);
        var keyboard = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>();
        LocalSelectionInput.SelectSharedKeyboard();
        var map = new GameObject("Navigation test map").AddComponent<MapManager>();
        var positions = (Dictionary<BoardTileId, Vector3>)Get(map, "tilePositions");
        foreach (BoardTileId tile in Enum.GetValues(typeof(BoardTileId)))
            positions[tile] = new Vector3((int)tile, 0f, 0f);
        var ownedView = new GameObject("Navigation own piece").AddComponent<DebugPieceView>();
        ownedView.Configure(1, 0);
        var enemyView = new GameObject("Navigation enemy piece").AddComponent<DebugPieceView>();
        enemyView.Configure(2, 0);
        var ui = new GameObject("Navigation test skill button", typeof(RectTransform),
            typeof(UnityEngine.UI.Button), typeof(CanvasGroup)).AddComponent<ActiveSkillButtonController>();
        var view = new GameObject("Navigation test view").AddComponent<MoveDestinationSelector>();
        Set(view, "playerManager", players);
        Set(view, "mapManager", map);
        var flow = view.gameObject.AddComponent<CharacterPieceInteractionController>();
        try
        {
            if (Get(ui, "skillButton") == null)
                typeof(ActiveSkillButtonController).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(ui, null);
            ui.RefreshForCurrentTurn();
            flow.Initialize(view);
            Check(id + " no yut result still shows an available active caster",
                ((List<MoveMarker>)Get(view, "pieceArrows")).Count == 1 &&
                ((List<MoveMarker>)Get(view, "destMarkers")).Count == 0);
            int before = CharacterSkillRegistry.GetSkillPoints(1);
            typeof(ActiveSkillButtonController).GetMethod("HandleActiveSkillButtonClicked",
                BindingFlags.Instance | BindingFlags.NonPublic).Invoke(ui, null);
            Check(id + " active click with no piece enters caster selection without spending SP",
                ui.IsSelectingCaster && CharacterSkillRegistry.GetSkillPoints(1) == before &&
                ((List<MoveMarker>)Get(view, "pieceArrows")).Count == 1);
            Set(ui, "casterSelectionStartedFrame", -1);
            Check(id + " selectable navigation accepts explicit caster", ui.TrySelectSkillPiece(ownedView));
            if (id == "008")
            {
                Check("008 caster selection switches navigation to legal enemy targets", ui.IsSelectingTarget);
                Set(ui, "targetSelectionStartedFrame", -1);
                int completed = 0;
                ui.SkillUseCompleted += result => { if (result.Succeeded) completed++; };
                Check("008 target selection rejects an allied piece without spending SP",
                    !ui.TrySelectSkillPiece(ownedView) && ui.IsSelectingTarget &&
                    CharacterSkillRegistry.GetSkillPoints(1) == before);
                p2.RuntimeData.Pieces[0].MoveTo(BoardTileId.Outer10);
                Check("008 target selection rejects an enemy outside active range",
                    !ui.TrySelectSkillPiece(enemyView) && ui.IsSelectingTarget &&
                    CharacterSkillRegistry.GetSkillPoints(1) == before);
                p2.RuntimeData.Pieces[0].MoveTo(BoardTileId.Outer02);
                CcEffectService.Apply(p2.RuntimeData.Pieces[0], CcDefine.Stealth, 3);
                Check("008 target selection rejects an untargetable enemy",
                    !ui.TrySelectSkillPiece(enemyView) && ui.IsSelectingTarget &&
                    CharacterSkillRegistry.GetSkillPoints(1) == before);
                CcEffectService.Remove(p2.RuntimeData.Pieces[0], CcDefine.Stealth);
                Check("008 button-first flow casts immediately when enemy is selected before throwing",
                    ui.TrySelectSkillPiece(enemyView) && p2.RuntimeData.Pieces[0].Cc.Has(CcDefine.Binding) &&
                    !ui.IsSelectingCaster && !ui.IsSelectingTarget && completed == 1 &&
                    CharacterSkillRegistry.GetSkillPoints(1) == before - skill.ActiveSkillPointCost &&
                    CharacterSkillRegistry.GetRemainingActiveCooldown(1, skill.Data) == skill.ActiveCooldownTurns &&
                    turns.CurrentTurn.currentPhase == TurnPhase.WaitThrow && turns.PendingResults.Count == 0);
                Check("008 repeated enemy click cannot cast or spend twice",
                    !ui.TrySelectSkillPiece(enemyView) && completed == 1 &&
                    CharacterSkillRegistry.GetSkillPoints(1) == before - skill.ActiveSkillPointCost);
            }
            else
                typeof(ActiveSkillButtonController).GetMethod("HandleActiveSkillButtonClicked",
                    BindingFlags.Instance | BindingFlags.NonPublic).Invoke(ui, null);
            Check(id + " selected caster/target confirms once and spends SP once",
                CharacterSkillRegistry.GetSkillPoints(1) == before - skill.ActiveSkillPointCost &&
                !ui.IsSelectingCaster && !ui.IsSelectingTarget);

            PrepareActiveAttempt(turns, p1, p2);
            p1.RuntimeData.Pieces[0].MoveTo(BoardTileId.Outer01);
            p2.RuntimeData.Pieces[0].MoveTo(BoardTileId.Outer02);
            turns.CurrentTurn.currentPhase = TurnPhase.WaitAction;
            ((List<YutThrowData>)Get(turns, "pendingResults")).Add(
                new YutThrowData { player = PlayerSlot.Player1, result = YutResult.Gae });
            ui.RefreshForCurrentTurn();
            Check(id + " HUD waiting piece selects destinations through preserved API",
                view.CanSelectWaitingPiece(1, 1) && view.TrySelectWaitingPiece(1, 1) &&
                flow.SelectedPieceId == 1 && ((List<MoveMarker>)Get(view, "destMarkers")).Count == 1);
            typeof(CharacterPieceInteractionController).GetMethod("SelectPiece",
                BindingFlags.Instance | BindingFlags.NonPublic).Invoke(flow, new object[] { 1, 0, ownedView });
            flow.RefreshNavigation();
            Check(id + " ordinary piece selection shows movement and active navigation together",
                ui.SelectedCasterPieceId == 0 && ((List<MoveMarker>)Get(view, "destMarkers")).Count == 1 &&
                ((List<MoveMarker>)Get(view, "pieceArrows")).Count == 1);
            Check(id + " preselected piece bypasses a redundant caster selection",
                skill.GetNextInputStep(false, true, false) ==
                (id == "008" ? CharacterSkillInputStep.Target : CharacterSkillInputStep.Confirm));
            Check(id + " navigation refresh never consumes movement results",
                turns.PendingResults.Count == 1 && p1.RuntimeData.Pieces[0].CurrentTileId == BoardTileId.Outer01);
            if (id == "008")
            {
                int beforePreselected = CharacterSkillRegistry.GetSkillPoints(1);
                typeof(ActiveSkillButtonController).GetMethod("HandleActiveSkillButtonClicked",
                    BindingFlags.Instance | BindingFlags.NonPublic).Invoke(ui, null);
                Check("008 preselected ally starts enemy selection directly without spending SP",
                    ui.IsSelectingTarget && !ui.IsSelectingCaster &&
                    CharacterSkillRegistry.GetSkillPoints(1) == beforePreselected);
                Set(ui, "targetSelectionStartedFrame", -1);
                Check("008 ally-first flow casts immediately on enemy selection after throwing",
                    ui.TrySelectSkillPiece(enemyView) && p2.RuntimeData.Pieces[0].Cc.Has(CcDefine.Binding) &&
                    !ui.IsSelectingCaster && !ui.IsSelectingTarget && ui.SelectedCasterPieceId == -1 &&
                    CharacterSkillRegistry.GetSkillPoints(1) == beforePreselected - skill.ActiveSkillPointCost);
                Check("008 immediate cast preserves the pending movement result and clears movement selection",
                    turns.PendingResults.Count == 1 && turns.CurrentTurn.currentPhase == TurnPhase.WaitAction &&
                    p1.RuntimeData.Pieces[0].CurrentTileId == BoardTileId.Outer01 && flow.SelectedPieceId == -1);
                VerifyElfBlockedTargetFlow(skill, ui, flow, ownedView, enemyView, turns, p1, p2);
            }

            GameMode previousMode = turns.Settings.gameMode;
            try
            {
                turns.Settings.gameMode = GameMode.Classic;
                ui.RefreshForCurrentTurn();
                flow.RefreshNavigation();
                var canvas = ui.GetComponent<CanvasGroup>();
                Check(id + " Classic hides skill UI and clears caster/target selection",
                    canvas.alpha == 0f && !canvas.interactable && !canvas.blocksRaycasts &&
                    !ui.IsSelectingCaster && !ui.IsSelectingTarget);
                Check(id + " Classic preserves ordinary waiting-piece navigation",
                    view.CanSelectWaitingPiece(1, 1) && view.TrySelectWaitingPiece(1, 1) &&
                    flow.SelectedPieceId == 1 && ((List<MoveMarker>)Get(view, "destMarkers")).Count == 1);
            }
            finally { turns.Settings.gameMode = previousMode; }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(view.gameObject);
            UnityEngine.Object.DestroyImmediate(ui.gameObject);
            UnityEngine.Object.DestroyImmediate(ownedView.gameObject);
            UnityEngine.Object.DestroyImmediate(enemyView.gameObject);
            UnityEngine.Object.DestroyImmediate(map.gameObject);
            UnityEngine.InputSystem.InputSystem.RemoveDevice(keyboard);
        }
    }

    private static void VerifyElfBlockedTargetFlow(CharacterStatusBehaviour skill, ActiveSkillButtonController ui,
        CharacterPieceInteractionController flow, DebugPieceView ownedView, DebugPieceView enemyView,
        TestTurnManager turns, PlayerController p1, PlayerController p2)
    {
        foreach (bool preselected in new[] { false, true })
        foreach (string defense in new[] { "OneCharge", "StackTwoCharges", "Duration", "Immunity" })
        {
            PrepareActiveAttempt(turns, p1, p2);
            var caster = p1.RuntimeData.Pieces[0]; caster.MoveTo(BoardTileId.Outer01);
            var enemy = p2.RuntimeData.Pieces[0]; enemy.MoveTo(BoardTileId.Outer02);
            if (preselected)
            {
                turns.CurrentTurn.currentPhase = TurnPhase.WaitAction;
                Set(turns, "pendingRegularThrow", false);
                ((List<YutThrowData>)Get(turns, "pendingResults")).Add(
                    new YutThrowData { player = PlayerSlot.Player1, result = YutResult.Gae });
            }
            CharacterStatusBehaviour defender = null;
            CharacterData defenderData = null;
            try
            {
                if (defense == "Immunity")
                {
                    var host = new GameObject("Immune arrow target"); host.transform.SetParent(p2.transform);
                    defender = host.AddComponent<CHAR_006_Status>();
                    defenderData = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<CharacterData>(
                        "Assets/Character/Char_Info/CHAR_006_SO.asset"));
                    defenderData.visualModelPrefab = null; defender.Initialize(defenderData);
                    typeof(CharacterStatusBehaviour).GetMethod("TryRegisterRuntime", BindingFlags.Instance | BindingFlags.NonPublic)
                        .Invoke(defender, null);
                }
                else
                {
                    if (defense == "StackTwoCharges")
                    {
                        var cargo = p2.RuntimeData.Pieces[1]; cargo.MoveTo(enemy.CurrentTileId);
                        int group = p2.RuntimeData.CreateStackGroupId();
                        enemy.SetStackGroup(group, 0); cargo.SetStackGroup(group, 0);
                    }
                    CcEffectService.Apply(enemy, CcDefine.protect, turns: 2,
                        charges: defense == "Duration" ? -1 : defense == "StackTwoCharges" ? 2 : 1);
                }
                string label = $"008 protected/resisted target {defense}, preselected={preselected}";
                ui.CancelSkillSelection();
                ui.RefreshForCurrentTurn();
                if (preselected)
                    typeof(CharacterPieceInteractionController).GetMethod("SelectPiece", BindingFlags.Instance | BindingFlags.NonPublic)
                        .Invoke(flow, new object[] { 1, 0, ownedView });
                typeof(ActiveSkillButtonController).GetMethod("HandleActiveSkillButtonClicked",
                    BindingFlags.Instance | BindingFlags.NonPublic).Invoke(ui, null);
                if (!preselected)
                {
                    Set(ui, "casterSelectionStartedFrame", -1);
                    Check(label + " accepts caster", ui.TrySelectSkillPiece(ownedView));
                }
                Set(ui, "targetSelectionStartedFrame", -1);
                int points = CharacterSkillRegistry.GetSkillPoints(1);
                Check(label + " remains a valid selectable enemy", ui.IsSelectingTarget && skill.CanSelectActiveTarget(2, 0));
                Check(label + " completes the cast without applying Binding",
                    ui.TrySelectSkillPiece(enemyView) && !enemy.Cc.Has(CcDefine.Binding) &&
                    !ui.IsSelectingCaster && !ui.IsSelectingTarget && ui.SelectedCasterPieceId == -1);
                Check(label + " spends SP and starts cooldown once",
                    CharacterSkillRegistry.GetSkillPoints(1) == points - skill.ActiveSkillPointCost &&
                    CharacterSkillRegistry.GetRemainingActiveCooldown(1, skill.Data) == skill.ActiveCooldownTurns);
                if (defense == "OneCharge")
                    Check(label + " intentionally removes the one-hit protection", !enemy.Cc.Has(CcDefine.protect));
                else if (defense == "StackTwoCharges")
                    Check(label + " consumes one shared stack protection charge",
                        enemy.Cc.Get(CcDefine.protect)?.Charges == 1 &&
                        ReferenceEquals(enemy.Cc.Get(CcDefine.protect), p2.RuntimeData.Pieces[1].Cc.Get(CcDefine.protect)));
                else if (defense == "Duration")
                    Check(label + " preserves duration-based protection until expiry",
                        enemy.Cc.Get(CcDefine.protect)?.Charges == -1 && enemy.Cc.Get(CcDefine.protect)?.RemainingOwnerTurns == 2);
                else
                    Check(label + " preserves the defender's immunity", defender.IsImmuneToEffect(CcDefine.Binding));
                Check(label + " cannot spend twice or consume a movement result",
                    !ui.TrySelectSkillPiece(enemyView) && CharacterSkillRegistry.GetSkillPoints(1) == points - skill.ActiveSkillPointCost &&
                    turns.PendingResults.Count == (preselected ? 1 : 0) && caster.CurrentTileId == BoardTileId.Outer01 &&
                    turns.CurrentTurn.currentPhase == (preselected ? TurnPhase.WaitAction : TurnPhase.WaitThrow));
            }
            finally
            {
                if (defender != null)
                {
                    CharacterSkillRegistry.Unregister(defender);
                    UnityEngine.Object.DestroyImmediate(defender.gameObject);
                }
                if (defenderData != null) UnityEngine.Object.DestroyImmediate(defenderData);
            }
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
                EditorGUILayout.LabelField($"Piece {piece.PieceId} / {piece.State}",
                    $"CC: {CcEffectService.GetSkillId(piece.CurrentCc)}");
                using (new EditorGUI.IndentLevelScope())
                foreach (var effect in piece.Cc.Effects)
                    EditorGUILayout.LabelField(effect.SkillId,
                        $"Turns: {effect.RemainingOwnerTurns}, Value: {effect.Value}, " +
                        $"Charges: {effect.Charges}, " +
                        $"Source: {effect.SourcePlayerId}/{effect.SourcePieceId}");
            }
        }
        Repaint();
    }
}
#endif
