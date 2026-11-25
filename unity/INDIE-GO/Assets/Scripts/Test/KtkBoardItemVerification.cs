#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using YutArena.Common;
using YutArena.InGame;
using YutArena.Managers;

/// <summary>실제 이동·업기·잡기 API로 아이템 배치/획득을 확인합니다. 저장된 씬은 변경하지 않습니다.</summary>
public static class KtkBoardItemVerification
{
    private static readonly List<string> results = new List<string>();
    private static void Check(string label, bool condition)
    {
        results.Add((condition ? "PASS " : "FAIL ") + label);
        if (!condition) throw new InvalidOperationException(label);
    }

    public static void RunBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Batch verification only.");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        bool passed = RunChecks();
        EditorApplication.Exit(passed ? 0 : 1);
    }

    [MenuItem("Tools/Verification/Kill The King Board Items")]
    public static void RunInEditor()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play mode before running edit-mode verification.");
        var previousScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var activeRoots = new List<GameObject>();
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            foreach (var root in UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).GetRootGameObjects())
                if (root.activeSelf) activeRoots.Add(root);
        var testScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        try
        {
            foreach (var root in activeRoots) root.SetActive(false);
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(testScene);
            RunChecks();
        }
        finally
        {
            EditorSceneManager.CloseScene(testScene, true);
            foreach (var root in activeRoots) if (root != null) root.SetActive(true);
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(previousScene);
        }
    }

    private static bool RunChecks()
    {
        results.Clear();
        try
        {
            CheckSpawnAndPickup();
            CheckSkillAndStackPickup();
            CheckSharedTeamPickup();
            CheckCaptureAndRetire();
            CheckEscapeAndSuddenDeath();
            CheckOccupiedBoardAndReinitialize();
            CheckQaItemDisplay();
            var movementRegression = TeamCaptureStackVerification.RunChecks();
            results.AddRange(movementRegression.Select(line => "Movement regression: " + line));
            Check("Existing solo/team movement and capture regressions have no failures",
                !movementRegression.Any(line => line.StartsWith("FAIL")));
            int failures = 0;
            void Log(string message, string trace, LogType type)
            {
                if (message.Contains("킬더킹 자동 테스트") && message.Contains("❌")) failures++;
            }
            Application.logMessageReceived += Log;
            try
            {
                KtkSelfTest.RunCombatTests();
                KtkSelfTest.RunItemTests();
                KtkSelfTest.RunStatsAndGhostTests();
            }
            finally { Application.logMessageReceived -= Log; }
            Check("Existing KTK rule tests have no failures", failures == 0);
        }
        catch (Exception exception) { results.Add("FAIL EXCEPTION " + exception); }
        Directory.CreateDirectory("Temp");
        File.WriteAllLines("Temp/KtkBoardItemVerification.log", results);
        Debug.Log(string.Join("\n", results));
        return !results.Any(line => line.StartsWith("FAIL"));
    }

    private sealed class Fixture : IDisposable
    {
        public readonly GameObject Root = new GameObject("KTK board item verification");
        public readonly PlayerManager Players;
        public readonly PieceMovementManager Movement;
        public readonly TestTurnManager Turns;
        public readonly TestWinConditionManager Wins;
        public readonly MapManager Map;
        public readonly GameStartSettings Settings;
        public KtkBoardItemController Items => Root.GetComponent<KtkBoardItemController>();
        public KillTheKingManager Rules => Wins.KillTheKing;
        private readonly GameStartSettings previous = GameStartSettingsHolder.Current;

        public Fixture(bool team = false, bool shared = false)
        {
            typeof(CharacterSkillRegistry).GetMethod("ResetRuntimeState", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, null);
            Players = Root.AddComponent<PlayerManager>();
            Movement = Root.AddComponent<PieceMovementManager>();
            Turns = Root.AddComponent<TestTurnManager>();
            Wins = Root.AddComponent<TestWinConditionManager>();
            Map = Root.AddComponent<MapManager>();
            var positions = (Dictionary<BoardTileId, Vector3>)GetField(Map, "tilePositions");
            // 후보 칸 둘 외에는 전부 생성 금지 칸입니다.
            foreach (var tile in new[] { BoardTileId.Start, BoardTileId.Goal, BoardTileId.Outer01,
                BoardTileId.Outer02, BoardTileId.Outer03, BoardTileId.Outer04, BoardTileId.Corner01,
                BoardTileId.Corner02, BoardTileId.Corner03, BoardTileId.Corner04, BoardTileId.Center,
                BoardTileId.Outer06, BoardTileId.Outer08 }) positions[tile] = new Vector3((int)tile, 0, 0);
            var active = (List<PlayerController>)GetField(Players, "activePlayers");
            for (int i = 1; i <= (shared ? 2 : 4); i++)
            {
                var obj = new GameObject("P" + i);
                obj.transform.SetParent(Root.transform);
                var player = obj.AddComponent<PlayerController>();
                player.Initialize(i, "P" + i, 3);
                active.Add(player);
            }
            SetField(Movement, "playerManager", Players);
            SetField(Turns, "playerManager", Players);
            SetField(Turns, "pieceMovementManager", Movement);
            SetField(Wins, "playerManager", Players);
            SetField(Wins, "turnManager", Turns);
            Settings = new GameStartSettings { gameMode = GameMode.KillTheKing, playerCount = 4,
                isTeamMode = team, teamMode = shared ? TeamMode.TeamMode1 : TeamMode.TeamMode2, playerTeams = new[] { 1, 2, 1, 2 },
                matchComposition = team ? MatchComposition.TwoVsTwo : MatchComposition.OneVsOneVsOneVsOne,
                killTheKingStartHp = 10, maxTurnCount = 20, useItem = true, useSkill = false };
            GameStartSettingsHolder.Current = Settings;
            Turns.Initialize(Settings);
            Wins.Initialize(Settings);
        }
        public PlayerController Owner(int id) { Players.TryGetPlayer(id, out var player); return player; }
        public PlayerRuntimeData.PieceRuntimeData P(int id, int piece = 0) => Owner(id).RuntimeData.Pieces[piece];
        public void Spawn() { Rules.OnRoundStarted(1); Rules.OnRoundStarted(2); }
        public void Dispose()
        {
            GameStartSettingsHolder.Current = previous;
            UnityEngine.Object.DestroyImmediate(Root);
        }
    }

    private static object GetField(object instance, string name) =>
        instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(instance);
    private static void SetField(object instance, string name, object value) =>
        instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(instance, value);
    private static BoardTileId Before(BoardTileId tile) => tile == BoardTileId.Outer06 ? BoardTileId.Outer05 : BoardTileId.Outer07;
    private static BoardTileId After(BoardTileId tile) => tile == BoardTileId.Outer06 ? BoardTileId.Outer07 : BoardTileId.Corner02;

    private static void CheckSpawnAndPickup()
    {
        using (var f = new Fixture())
        {
            Check("Game initialization installs the board item controller", f.Items != null);
            f.Rules.OnRoundStarted(1);
            Check("Round one has no placed items", f.Items.SwordTile == BoardTileId.None && f.Items.ShieldTile == BoardTileId.None);
            f.Rules.OnRoundStarted(2);
            var swordTile = f.Items.SwordTile;
            var shieldTile = f.Items.ShieldTile;
            Check("Items use real anchors and exclude starting segment/corners/center", new[] { BoardTileId.Outer06, BoardTileId.Outer08 }.Contains(swordTile) &&
                new[] { BoardTileId.Outer06, BoardTileId.Outer08 }.Contains(shieldTile));
            Check("Sword and shield occupy different cells", swordTile != shieldTile);
            Check("Both items have visible geometry without selection colliders", f.Items.GetComponentsInChildren<Renderer>().Length > 0 &&
                f.Items.GetComponentsInChildren<Collider>().Length == 0);
            f.P(1).MoveTo(Before(swordTile));
            Check("Normal move passes through the sword cell", f.Movement.TryMovePiece(1, 0, 2) && f.P(1).CurrentTileId == After(swordTile));
            Check("Passing collects the sword and removes its board object", f.P(1).KtkItems.Swords.Count == 1 &&
                f.Rules.BoardSword == null && f.Items.SwordTile == BoardTileId.None);
            Check("Pickup is counted once", f.Rules.GetStats(TeamSlot.TeamA).ItemsPicked == 1);
            f.P(1).MoveTo(swordTile);
            Check("Revisiting does not duplicate inventory", f.P(1).KtkItems.Swords.Count == 1);
            f.P(1).MoveTo(shieldTile);
            Check("Landing collects shield but does not equip its effect yet", f.P(1).KtkItems.Shields.Count == 1 &&
                f.Rules.GetShieldEffects(TeamSlot.TeamA).Count == 0 && f.Items.ShieldTile == BoardTileId.None);
            f.P(1).MoveTo(BoardTileId.Outer07);
            f.Rules.OnRoundStarted(3); f.Rules.OnRoundStarted(4);
            Check("Sword waits two complete rounds", f.Items.SwordTile == BoardTileId.None);
            f.Rules.OnRoundStarted(5);
            Check("Sword respawns on round five after round-two pickup", f.Items.SwordTile != BoardTileId.None);
            f.Rules.OnRoundStarted(6);
            Check("Shield waits four complete rounds", f.Items.ShieldTile == BoardTileId.None);
            f.Rules.OnRoundStarted(7);
            Check("Shield respawns on round seven", f.Items.ShieldTile != BoardTileId.None);
        }
    }

    private static void CheckSkillAndStackPickup()
    {
        using (var f = new Fixture(true))
        {
            f.Spawn();
            var tile = f.Items.SwordTile;
            f.P(1).MoveTo(Before(tile));
            f.P(3).MoveTo(Before(tile));
            f.P(1).SetStackGroup(0, f.P(1)); f.P(3).SetStackGroup(0, f.P(1));
            CcBoardEffects.MoveStackAlongPath(f.Owner(1), f.P(1), new[] { tile, After(tile) }, ignoresInstalledItems: true);
            Check("Forced path movement also collects items", f.P(1).KtkItems.Swords.Count == 1 && f.Rules.BoardSword == null);
            Check("Mixed-owner cargo shares one inventory without duplicate pickups", ReferenceEquals(f.P(1).KtkItems, f.P(3).KtkItems) &&
                f.Rules.GetStats(TeamSlot.TeamA).ItemsPicked == 1);
        }
        using (var f = new Fixture(true))
        {
            f.Spawn();
            var tile = f.Items.SwordTile;
            f.P(3).MoveTo(After(tile));
            f.P(3).KtkItems.Add(KtkShield.Mirror);
            f.P(1).MoveTo(Before(tile));
            Check("Pickup followed by teammate stacking succeeds", f.Movement.TryMovePiece(1, 0, 2));
            Check("Merging carries both inventories to the stationary leader", f.P(1).StackLeaderPlayerId == 3 &&
                f.P(3).KtkItems.Swords.Count == 1 && f.P(3).KtkItems.Shields.Count == 1);
        }
    }

    private static void CheckCaptureAndRetire()
    {
        using (var f = new Fixture(true))
        {
            f.P(2).MoveTo(BoardTileId.Outer07); f.P(4).MoveTo(BoardTileId.Outer07);
            f.P(2).KtkItems.Add(KtkSword.Twin); f.P(4).KtkItems.Add(KtkShield.Normal);
            f.P(2).SetStackGroup(0, f.P(2)); f.P(4).SetStackGroup(0, f.P(2));
            f.P(1).MoveTo(BoardTileId.Outer06);
            Check("Actual capture steals the whole mixed-owner inventory", f.Movement.TryMovePiece(1, 0, 1) &&
                f.P(1).KtkItems.Count == 2 && f.P(2).KtkItems.Count == 0 && f.P(4).KtkItems.Count == 0);
            Check("Stolen items are recorded", f.Rules.GetStats(TeamSlot.TeamA).ItemsStolen == 2);
            f.P(1).SetCaptured(CcDefine.Retire);
            Check("Retirement without a captor destroys inventory", f.P(1).KtkItems.Count == 0);
        }
        using (var f = new Fixture())
        {
            f.P(2).MoveTo(BoardTileId.Outer07); f.P(2).KtkItems.Add(KtkSword.Normal);
            f.P(1).MoveTo(BoardTileId.Outer06);
            Check("Solo capture also transfers inventory", f.Movement.TryMovePiece(1, 0, 1) && f.P(1).KtkItems.Count == 1);
            CcEffectService.Apply(f.P(1), CcDefine.Robot_part, turns: 2);
            Check("Robot parts discard held items", f.P(1).KtkItems.Count == 0);
        }
    }

    private static void CheckSharedTeamPickup()
    {
        using (var f = new Fixture(team: true, shared: true))
        {
            f.Spawn();
            f.P(1).MoveTo(Before(f.Items.SwordTile));
            Check("Shared team's character collects a passing sword", f.Movement.TryMovePiece(1, 0, 2) &&
                f.P(1).KtkItems.Swords.Count == 1 && f.Rules.GetStats(TeamSlot.TeamA).ItemsPicked == 1);
            f.P(2).MoveTo(f.Items.ShieldTile);
            Check("Shared blue character credits TeamB regardless of person numbering", f.P(2).KtkItems.Shields.Count == 1 &&
                f.Rules.GetStats(TeamSlot.TeamB).ItemsPicked == 1);
            f.P(1).MoveTo(BoardTileId.Corner04);
            f.Movement.TryMovePiece(1, 0, 2);
            f.Wins.OnPieceMoveResolved(PlayerSlot.Player3, TeamSlot.TeamA, true, 1);
            Check("A teammate's turn uses the shared character's escape inventory", f.Rules.GetHp(TeamSlot.TeamB) == 8 && f.P(1).KtkItems.Count == 0);
        }
    }

    private static void CheckEscapeAndSuddenDeath()
    {
        using (var f = new Fixture())
        {
            f.P(1).KtkItems.Add(KtkSword.Normal); f.P(1).KtkItems.Add(KtkShield.Normal);
            f.P(1).MoveTo(BoardTileId.Corner04);
            Check("Inventory survives until escape resolution", f.Movement.TryMovePiece(1, 0, 2) && f.P(1).IsFinished && f.P(1).KtkItems.Count == 2);
            f.Wins.OnPieceMoveResolved(PlayerSlot.Player1, TeamSlot.TeamA, true, 1);
            Check("Actual escape hands sword and shield to combat exactly once", f.Rules.GetHp(TeamSlot.TeamB) == 8 &&
                f.Rules.GetShieldEffects(TeamSlot.TeamA).Count == 1 && f.P(1).KtkItems.Count == 0);
        }
        using (var f = new Fixture())
        {
            f.Spawn();
            f.P(1).KtkItems.Add(KtkSword.Twin);
            f.Rules.OnTurnLimitReached();
            f.Items.SynchronizeBoard();
            Check("Sudden death converts carried and placed swords", f.P(1).KtkItems.Swords[0] == KtkSword.Demon &&
                f.Rules.BoardSword == KtkSword.Demon && f.Items.transform.Find("KtkSword_Demon") != null);
        }
    }

    private static void CheckQaItemDisplay()
    {
        using (var f = new Fixture(team: true))
        {
            var output = new System.Text.StringBuilder();
            string Board()
            {
                output.Clear();
                PlayerDataDebugTester.AppendKtkBoardItems(output, f.Settings, f.Rules, f.Items);
                return output.ToString();
            }
            f.Rules.OnRoundStarted(1);
            Check("QA shows the first item generation round", Board().Contains("생성 예정=2라운드 시작"));
            f.Rules.OnRoundStarted(2);
            var tile = f.Items.SwordTile;
            Check("QA shows placed item kinds and board positions", Board().Contains("일반 검(Normal) | 위치=" + tile) &&
                Board().Contains("일반 방패(Normal) | 위치=" + f.Items.ShieldTile));
            f.P(1).MoveTo(tile);
            Check("QA switches picked items to the correct respawn round", Board().Contains("보드 검: 없음 | 생성 예정=5라운드 시작"));
            f.P(3).MoveTo(tile);
            f.P(1).SetStackGroup(0, f.P(1)); f.P(3).SetStackGroup(0, f.P(1));
            output.Clear();
            PlayerDataDebugTester.AppendKtkInventory(output, f.P(3));
            Check("QA cargo identifies its shared inventory and actual carrier", output.ToString().Contains("보관=P1/말1") &&
                output.ToString().Contains("검=[일반 검(Normal)]") && output.ToString().Contains("총=1"));
            f.Rules.AddShieldEffect(TeamSlot.TeamA, KtkShield.Normal);
            f.Rules.AddShieldEffect(TeamSlot.TeamA, KtkShield.Mirror);
            output.Clear();
            PlayerDataDebugTester.AppendKtkPlayerItems(output, f.Rules, TeamSlot.TeamA);
            Check("QA distinguishes equipped shields in consumption order from carried items", output.ToString().Contains(
                "방패 효과(소모 순서: 최근→과거)=[반사의 거울(Mirror), 일반 방패(Normal)]"));
            Check("QA includes team HP and attack target", output.ToString().Contains("HP=10/10") && output.ToString().Contains("공격 대상=TeamB"));
            var monitor = f.Root.AddComponent<PlayerDataDebugTester>();
            SetField(monitor, "turnManager", f.Turns);
            SetField(monitor, "winManager", f.Wins);
            typeof(PlayerDataDebugTester).GetMethod("AppendPlayer", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(monitor, new object[] { f.Owner(3), f.Settings });
            string playerBlock = ((System.Text.StringBuilder)GetField(monitor, "text")).ToString();
            Check("QA player snapshot includes both carried items and equipped shields", playerBlock.Contains("소지품(묶음 공유, 보관=P1/말1)") &&
                playerBlock.Contains("일반 검(Normal)") && playerBlock.Contains("방패 효과(소모 순서: 최근→과거)"));
            Check("QA formatting does not consume items or shield effects", f.P(1).KtkItems.Count == 1 &&
                f.Rules.GetShieldEffects(TeamSlot.TeamA).SequenceEqual(new[] { KtkShield.Normal, KtkShield.Mirror }));
            output.Clear();
            PlayerDataDebugTester.AppendKtkBoardItems(output, new GameStartSettings { gameMode = GameMode.Escape }, f.Rules, f.Items);
            Check("QA item block is limited to Kill The King", output.Length == 0);
        }
    }

    private static void CheckOccupiedBoardAndReinitialize()
    {
        using (var f = new Fixture())
        {
            f.P(1).MoveTo(BoardTileId.Outer06); f.P(2).MoveTo(BoardTileId.Outer08);
            f.Spawn();
            Check("Occupied board defers placement instead of spawning under pieces", f.Items.SwordTile == BoardTileId.None && f.Items.ShieldTile == BoardTileId.None);
            var pendingDisplay = new System.Text.StringBuilder();
            PlayerDataDebugTester.AppendKtkBoardItems(pendingDisplay, f.Settings, f.Rules, f.Items);
            Check("QA distinguishes placement waiting from respawn waiting", pendingDisplay.ToString().Contains("배치 대기(빈칸/앵커 확인)") &&
                !pendingDisplay.ToString().Contains("생성 예정="));
            f.P(3).MoveTo(BoardTileId.Corner04); f.P(3).MoveTo(BoardTileId.None);
            Check("Shared start cannot collect an item still awaiting a free cell", f.P(3).KtkItems.Count == 0 &&
                f.Rules.BoardSword.HasValue && f.Rules.BoardShield.HasValue);
            f.P(1).MoveTo(BoardTileId.Outer07); f.Items.SynchronizeBoard();
            Check("Pending sword uses a newly freed cell", f.Items.SwordTile == BoardTileId.Outer06 && f.Items.ShieldTile == BoardTileId.None);
            f.P(2).MoveTo(BoardTileId.Outer09); f.Items.SynchronizeBoard();
            Check("Pending shield uses the next free cell", f.Items.ShieldTile == BoardTileId.Outer08);
            f.Wins.Initialize(f.Settings);
            Check("Reinitialization clears old board objects", f.Items.SwordTile == BoardTileId.None && f.Items.ShieldTile == BoardTileId.None);
            f.Spawn();
            f.P(1).MoveTo(f.Items.SwordTile);
            Check("Reinitialization does not duplicate event subscriptions", f.Rules.GetStats(TeamSlot.TeamA).ItemsPicked == 1 && f.P(1).KtkItems.Count == 1);
            var tile = f.Items.ShieldTile;
            var otherMode = new GameStartSettings { gameMode = GameMode.Escape };
            f.Items.Initialize(f.Rules, otherMode, f.Players, f.Map);
            f.P(1).MoveTo(tile);
            Check("Other game modes do not collect KTK items", f.P(1).KtkItems.Shields.Count == 0);
        }
    }
}
#endif
