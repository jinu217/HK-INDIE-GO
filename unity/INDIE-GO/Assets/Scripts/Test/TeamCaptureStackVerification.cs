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

/// <summary>Explicit batch-only regression checks; never runs in normal play or modifies saved scenes.</summary>
public static class TeamCaptureStackVerification
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
        results.Clear();
        try
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            foreach (TeamMode mode in new[] { TeamMode.TeamMode1, TeamMode.TeamMode2 })
            {
                CheckLobbySettings(mode);
                CheckCaptureThenStack(mode);
                CheckGroupMerge(mode);
                CheckUnlimitedStack(mode);
                CheckYutCapture(mode);
                CheckGoal(mode);
                CheckDirectCapture(mode);
                CheckTeamStackDebugDisplay(mode);
            }
            CheckSolo();
            CheckExistingSoloStackRegression();
        }
        catch (Exception exception)
        {
            results.Add("FAIL EXCEPTION " + exception);
        }
        File.WriteAllLines("TeamCaptureStackVerification.log", results);
        bool failed = results.Any(line => line.StartsWith("FAIL"));
        Debug.Log(string.Join("\n", results));
        EditorApplication.Exit(failed ? 1 : 0);
    }

    private static void CheckLobbySettings(TeamMode mode)
    {
        var room = new RoomSettingsData { isTeamMode = true, teamMode = mode,
            playerCount = 4, playerTeams = new[] { 1, 2, 1, 2 },
            gameMode = (int)GameMode.Escape, pieceCountPerPlayer = 3 };
        var copy = RoomSettingsData.FromJson(room.ToJson()).ToGameStartSettings();
        Check(mode + " lobby JSON preserves mode and custom teams",
            copy.isTeamMode && copy.teamMode == mode && copy.playerTeams.SequenceEqual(room.playerTeams));
        Check(mode + " serialized value", (int)mode == (mode == TeamMode.TeamMode1 ? 0 : 1));
    }

    private sealed class Fixture : IDisposable
    {
        public readonly GameObject Root = new GameObject("Team capture / stacking regression");
        public readonly PlayerManager Players;
        public readonly PieceMovementManager Movement;
        public readonly TestTurnManager Turns;
        public readonly GameStartSettings Settings;
        private readonly GameStartSettings previousSettings;

        public Fixture(TeamMode mode, bool team = true, int count = 4)
        {
            typeof(CharacterSkillRegistry).GetMethod("ResetRuntimeState", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, null);
            Players = Root.AddComponent<PlayerManager>();
            Movement = Root.AddComponent<PieceMovementManager>();
            Turns = Root.AddComponent<TestTurnManager>();
            var active = (List<PlayerController>)typeof(PlayerManager).GetField("activePlayers",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Players);
            for (int i = 1; i <= 4; i++)
            {
                var host = new GameObject("P" + i);
                host.transform.SetParent(Root.transform);
                var player = host.AddComponent<PlayerController>();
                player.Initialize(i, "P" + i, count);
                active.Add(player);
            }
            SetField(Movement, "playerManager", Players);
            SetField(Turns, "playerManager", Players);
            SetField(Turns, "pieceMovementManager", Movement);
            Settings = new GameStartSettings { isTeamMode = team, teamMode = mode,
                playerTeams = new[] { 1, 2, 1, 2 }, playerCount = 4, useSkill = false,
                gameMode = GameMode.Escape, matchComposition = team ? MatchComposition.TwoVsTwo :
                    MatchComposition.OneVsOneVsOneVsOne };
            previousSettings = GameStartSettingsHolder.Current;
            GameStartSettingsHolder.Current = Settings;
            Turns.Initialize(Settings);
        }
        public PlayerController Owner(int id) { Players.TryGetPlayer(id, out var player); return player; }
        public PlayerRuntimeData.PieceRuntimeData P(int owner, int id = 0) => Owner(owner).RuntimeData.Pieces[id];
        public int Bonus(int attacker) => (int)typeof(TestTurnManager).GetMethod("ConsumeCaptureResults",
            BindingFlags.Instance | BindingFlags.NonPublic).Invoke(Turns, new object[] { attacker, false });
        public void Dispose()
        {
            GameStartSettingsHolder.Current = previousSettings;
            UnityEngine.Object.DestroyImmediate(Root);
        }
    }

    private static void SetField(object instance, string name, object value) =>
        instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(instance, value);

    private static void CheckCaptureThenStack(TeamMode mode)
    {
        using (var f = new Fixture(mode))
        {
            f.P(1).MoveTo(BoardTileId.Outer01);
            foreach (int owner in new[] { 2, 3, 4 }) f.P(owner).MoveTo(BoardTileId.Outer02);
            CcEffectService.Apply(f.P(3), CcDefine.protect, charges: 1);
            CcEffectService.Apply(f.P(3), CcDefine.Stack_fake);
            Check(mode + " move captures enemies before stacking", f.Movement.TryMovePiece(1, 0, 1) &&
                f.P(2).State == PieceState.Waiting && f.P(4).State == PieceState.Waiting &&
                f.P(1).IsInSameStack(f.P(3)) && f.P(3).IsStackLeader && !f.P(1).IsStackLeader);
            Check(mode + " equal piece IDs retain teammate leader owner", f.P(1).StackLeaderPlayerId == 3 &&
                CcEffectService.StackLeader(f.P(1)) == f.P(3));
            Check(mode + " ally protection and clone are not consumed",
                f.P(3).Cc.Get(CcDefine.protect)?.Charges == 1 && f.P(3).Cc.Get(CcDefine.Stack_fake)?.Value == 1);
            Check(mode + " two enemy owners give one capture throw", f.Bonus(1) == 1 && f.Bonus(1) == 0);
            Check(mode + " cargo cannot move alone", !f.Movement.TryMovePiece(1, 0, 1));
            Check(mode + " leader moves the full team stack", f.Movement.TryMovePiece(3, 0, 1) &&
                f.P(1).CurrentTileId == BoardTileId.Outer03 && f.P(3).CurrentTileId == BoardTileId.Outer03);
            CcEffectService.Remove(f.P(3), CcDefine.protect);
            CcEffectService.Apply(f.P(1), CcDefine.Sturn, 2);
            Check(mode + " stack status resolves through teammate leader", f.P(3).Cc.Has(CcDefine.Sturn) &&
                !CcEffectService.CanMove(f.P(1)) && !CcEffectService.CanMove(f.P(3)));
            CcEffectService.Remove(f.P(3), CcDefine.Sturn);
            CcEffectService.Apply(f.P(3), CcDefine.protect, charges: 1);
            CcEffectService.TickOwnerTurn(f.Owner(1));
            f.P(2).MoveTo(BoardTileId.Outer02);
            Check(mode + " protected mixed-owner stack blocks capture once",
                f.Movement.TryMovePiece(2, 0, 1) && f.P(1).State == PieceState.InBoard &&
                f.P(3).State == PieceState.InBoard && !f.P(3).Cc.Has(CcDefine.protect) && f.Bonus(2) == 0);
            f.P(2).MoveTo(BoardTileId.Outer02);
            Check(mode + " clone protects whole team stack without bonus",
                f.Movement.TryMovePiece(2, 0, 1) && f.P(1).State == PieceState.InBoard &&
                f.P(3).State == PieceState.InBoard && !f.P(3).Cc.Has(CcDefine.Stack_fake) && f.Bonus(2) == 0);
            f.P(2).MoveTo(BoardTileId.Outer02);
            Check(mode + " capture retires all cargo to each original owner",
                f.Movement.TryMovePiece(2, 0, 1) && f.P(1).State == PieceState.Waiting &&
                f.P(3).State == PieceState.Waiting && !f.P(1).IsStacked && !f.P(3).IsStacked &&
                f.P(1).OwnerPlayerId == 1 && f.P(3).OwnerPlayerId == 3 && f.Bonus(2) == 1);
            f.P(2).MoveTo(BoardTileId.Outer02);
            Check(mode + " captured cargo can re-enter independently", f.Movement.TryMovePiece(1, 0, 1) &&
                f.P(1).CurrentTileId == BoardTileId.Outer01 && f.P(3).State == PieceState.Waiting);
        }
    }

    private static void CheckGroupMerge(TeamMode mode)
    {
        using (var f = new Fixture(mode))
        {
            for (int i = 0; i < 2; i++)
            {
                f.P(1, i).MoveTo(BoardTileId.Outer01); f.P(1, i).SetStackGroup(0, 1);
                f.P(3, i).MoveTo(BoardTileId.Outer02); f.P(3, i).SetStackGroup(0, 1);
                f.P(2, i).MoveTo(BoardTileId.Outer07); f.P(2, i).SetStackGroup(0, 1);
            }
            Check(mode + " same numeric IDs across owners are separate stacks", !f.P(1).IsInSameStack(f.P(3)));
            Check(mode + " merge preserves stationary leader and all cargo", f.Movement.TryMovePiece(1, 1, 1) &&
                CcEffectService.StackMembers(f.P(3, 1)).Count == 4 && f.P(3, 1).IsStackLeader &&
                f.P(1).StackLeaderPlayerId == 3 && f.P(1).StackLeaderPieceId == 1 &&
                f.P(2).CurrentTileId == BoardTileId.Outer07);
        }
    }

    private static void CheckUnlimitedStack(TeamMode mode)
    {
        using (var f = new Fixture(mode, count: 8))
        {
            f.P(3).MoveTo(BoardTileId.Outer02);
            CcEffectService.Apply(f.P(3), CcDefine.Stack_fake);
            for (int i = 0; i < 8; i++)
            {
                f.P(1, i).MoveTo(BoardTileId.Outer01); f.P(1, i).SetStackGroup(0, 0);
                f.P(3, i).MoveTo(BoardTileId.Outer02); f.P(3, i).SetStackGroup(0, 0);
            }
            Check(mode + " no team stack size limit", f.Movement.TryMovePiece(1, 0, 1) &&
                CcEffectService.StackMembers(f.P(3)).Count == 16 && f.P(3).Cc.Get(CcDefine.Stack_fake)?.Value == 1);
        }
    }

    private static void CheckYutCapture(TeamMode mode)
    {
        using (var f = new Fixture(mode))
        {
            f.P(2).MoveTo(BoardTileId.Outer07);
            CcEffectService.Apply(f.P(1), CcDefine.Move_scale, value: 2);
            Check(mode + " Yut capture grants no extra even when skill changes distance",
                f.Movement.TryMovePiece(1, 0, 4) && f.P(2).State == PieceState.Waiting && f.Bonus(1) == 0);
        }
        using (var f = new Fixture(mode))
        {
            f.P(2).MoveTo(BoardTileId.Corner01);
            Check(mode + " Mo capture grants no extra", f.Movement.TryMovePiece(1, 0, 5) &&
                f.P(2).State == PieceState.Waiting && f.Bonus(1) == 0);
        }
        using (var f = new Fixture(mode))
        {
            f.P(2).MoveTo(BoardTileId.Outer05);
            CcEffectService.Apply(f.P(1), CcDefine.Move_scale, value: 2);
            Check(mode + " Geol capture uses throw result rather than modified distance",
                f.Movement.TryMovePiece(1, 0, 3) && f.P(2).State == PieceState.Waiting && f.Bonus(1) == 1);
        }
    }

    private static void CheckGoal(TeamMode mode)
    {
        using (var f = new Fixture(mode))
        {
            f.P(1).MoveTo(BoardTileId.Outer16); f.P(3).MoveTo(BoardTileId.Outer16);
            f.P(1).SetStackGroup(0, f.P(1)); f.P(3).SetStackGroup(0, f.P(1));
            Check(mode + " mixed-owner stack leaves the board together", f.Movement.TryMovePiece(1, 0, 2) &&
                f.P(1).IsFinished && f.P(3).IsFinished && !f.P(1).IsStacked && !f.P(3).IsStacked);
        }
    }

    private static void CheckSolo()
    {
        using (var f = new Fixture(TeamMode.TeamMode2, team: false))
        {
            f.P(1).MoveTo(BoardTileId.Outer01); f.P(3).MoveTo(BoardTileId.Outer02);
            Check("Solo ignores lobby team array and captures other owners", f.Movement.TryMovePiece(1, 0, 1) &&
                f.P(3).State == PieceState.Waiting && !f.P(1).IsStacked && f.Bonus(1) == 1);
            f.P(1, 1).MoveTo(BoardTileId.Outer03);
            Check("Solo keeps existing stationary leader rule", f.Movement.TryMovePiece(1, 0, 1) &&
                f.P(1).IsInSameStack(f.P(1, 1)) && f.P(1, 1).IsStackLeader);
            Check("Solo stack moves as before", f.Movement.TryMovePiece(1, 1, 1) &&
                f.P(1).CurrentTileId == BoardTileId.Outer04 && f.P(1, 1).CurrentTileId == BoardTileId.Outer04);
        }
    }

    private static void CheckDirectCapture(TeamMode mode)
    {
        using (var f = new Fixture(mode))
        {
            f.P(1, 1).MoveTo(BoardTileId.Outer02);
            f.P(3, 1).MoveTo(BoardTileId.Outer02);
            f.P(3, 1).SetStackGroup(0, f.P(3, 1));
            f.P(1, 1).SetStackGroup(0, f.P(3, 1));
            CcEffectService.Apply(f.P(3, 1), CcDefine.protect, charges: 1);
            var cargo = new CharacterPieceReference(f.Owner(1), f.P(1, 1));
            Check(mode + " direct capture resolves teammate carrier protection",
                !CcBoardEffects.TryCapture(2, 0, cargo, 1, true, out var defended) &&
                defended == CharacterCaptureDecision.Prevent && f.P(1, 1).State == PieceState.InBoard &&
                f.P(3, 1).State == PieceState.InBoard);
            Check(mode + " direct cargo capture retires both owners with one bonus",
                CcBoardEffects.TryCapture(2, 0, cargo, 1, true, out var captured) &&
                captured == CharacterCaptureDecision.Proceed && f.P(1, 1).State == PieceState.Waiting &&
                f.P(3, 1).State == PieceState.Waiting && f.Bonus(2) == 1);
        }
    }

    private static void CheckExistingSoloStackRegression()
    {
        using (var f = new Fixture(TeamMode.TeamMode1, team: false))
        {
            var type = typeof(CcArchitectureVerifier);
            var existing = (List<string>)type.GetField("results", BindingFlags.Static | BindingFlags.NonPublic)
                .GetValue(null);
            existing.Clear();
            type.GetMethod("VerifyStackState", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { f.Owner(1), f.Owner(2), f.Movement });
            results.AddRange(existing.Select(line => line.StartsWith("PASS ")
                ? "PASS Existing solo: " + line.Substring(5) : line));
            Check("Existing solo stack regression contains no failures", !existing.Any(line => line.StartsWith("FAIL")));
        }
    }

    private static void CheckTeamStackDebugDisplay(TeamMode mode)
    {
        using (var f = new Fixture(mode))
        {
            f.P(2).MoveTo(BoardTileId.Outer02);
            f.P(4).MoveTo(BoardTileId.Outer01);
            Check(mode + " P4 lands on teammate P2", f.Movement.TryMovePiece(4, 0, 1) &&
                f.P(4).StackLeaderPlayerId == 2 && f.P(4).IsInSameStack(f.P(2)));
            string Format(int owner)
            {
                var output = new System.Text.StringBuilder();
                PlayerDataDebugTester.AppendPieceDetails(output, f.Owner(owner).RuntimeData, f.P(owner), TeamSlot.TeamB);
                return output.ToString();
            }
            string cargo = Format(4), leader = Format(2);
            Check(mode + " P4 debug row names P2 as its carrier despite equal piece IDs",
                cargo.Contains("업기역할=업힌 말, 리더말=P2/말1(id=0)") && !cargo.Contains("[보드에서 찾지 못함]"));
            Check(mode + " P2 debug row names P4 as cargo", leader.Contains("업기역할=리더, 리더말=P2/말1(id=0)") &&
                leader.Contains("업힌말=[P4/말1(id=0)], 업힌수=1, 그룹전체=2"));
            Check(mode + " P4 debug counts include both players", cargo.Contains("업힌수=1, 그룹전체=2"));
            var monitor = f.Root.AddComponent<PlayerDataDebugTester>();
            typeof(PlayerDataDebugTester).GetMethod("AppendPiece", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(monitor, new object[] { f.Owner(4), f.P(4), TeamSlot.TeamB });
            var fullText = (System.Text.StringBuilder)typeof(PlayerDataDebugTester)
                .GetField("text", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(monitor);
            Check(mode + " first status line also identifies P2 as leader", fullText.ToString().Contains("선두=P2/말1"));
        }
        using (var f = new Fixture(mode))
        {
            f.P(4).MoveTo(BoardTileId.Outer02);
            f.P(2).MoveTo(BoardTileId.Outer01);
            Check(mode + " opposite arrival preserves P4 as carrier", f.Movement.TryMovePiece(2, 0, 1));
            var output = new System.Text.StringBuilder();
            PlayerDataDebugTester.AppendPieceDetails(output, f.Owner(2).RuntimeData, f.P(2), TeamSlot.TeamB);
            Check(mode + " P2 carried row correctly names P4 in reverse case",
                output.ToString().Contains("업기역할=업힌 말, 리더말=P4/말1(id=0)"));
        }
    }
}
#endif
