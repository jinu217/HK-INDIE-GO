#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using YutArena.Common;
using YutArena.InGame;
using YutArena.Managers;

/// <summary>Explicit, isolated batch Play Mode verification. Never starts from normal game scenes.</summary>
public sealed class PieceVisualLifecycleVerification : MonoBehaviour
{
    private const string Flag = "PieceVisualLifecycleVerification.flag";
    private readonly List<string> results = new List<string>();
    private bool failed;

    public static void RunBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Batch verification only.");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        File.WriteAllText(Flag, "run");
        EditorApplication.isPlaying = true;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (!Application.isBatchMode || !File.Exists(Flag)) return;
        File.Delete(Flag);
        new GameObject("Piece visual lifecycle verification").AddComponent<PieceVisualLifecycleVerification>();
    }

    private IEnumerator Start()
    {
        IEnumerator body = Verify();
        while (true)
        {
            bool next;
            try { next = body.MoveNext(); }
            catch (Exception exception)
            {
                failed = true;
                results.Add("FAIL " + exception);
                break;
            }
            if (!next) break;
            yield return body.Current;
        }
        File.WriteAllLines("PieceVisualLifecycleVerification.log", results);
        Debug.Log(string.Join("\n", results));
        EditorApplication.Exit(failed ? 1 : 0);
    }

    private void Check(string label, bool value)
    {
        results.Add((value ? "PASS " : "FAIL ") + label);
        if (!value) throw new InvalidOperationException(label);
    }

    private static void Set(object target, string field, object value, Type type = null) =>
        (type ?? target.GetType()).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

    private static object Call(object target, string method, params object[] args) =>
        target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);

    private IEnumerator Verify()
    {
        var fixture = new GameObject("Visual lifecycle fixture");
        var sources = new GameObject("Inactive source templates");
        sources.transform.SetParent(fixture.transform, false);
        sources.SetActive(false);
        var model = GameObject.CreatePrimitive(PrimitiveType.Cube);
        model.transform.SetParent(sources.transform, false);
        model.name = "Character visual template";
        model.AddComponent<PieceVisualLifecycleProbe>();
        var foot = GameObject.CreatePrimitive(PrimitiveType.Cube);
        foot.transform.SetParent(sources.transform, false);
        foot.name = "Foothold visual template";
        foot.AddComponent<PieceVisualLifecycleProbe>();
        var data = ScriptableObject.CreateInstance<CharacterData>();
        data.name = "Visual lifetime test character";
        data.visualModelPrefab = model;
        var prefab = new GameObject("Status-only character template");
        prefab.transform.SetParent(sources.transform, false);
        var template = prefab.AddComponent<CHAR_001_1_Status>();
        Set(template, "characterData", data, typeof(CharacterStatusBehaviour));

        var players = fixture.AddComponent<PlayerManager>();
        var movement = fixture.AddComponent<PieceMovementManager>();
        var turns = fixture.AddComponent<TestTurnManager>();
        Set(movement, "playerManager", players);
        Set(turns, "playerManager", players);
        Set(turns, "pieceMovementManager", movement);
        var active = (List<PlayerController>)typeof(PlayerManager).GetField("activePlayers",
            BindingFlags.Instance | BindingFlags.NonPublic).GetValue(players);
        for (int id = 1; id <= 2; id++)
        {
            var host = new GameObject("Player " + id);
            host.transform.SetParent(fixture.transform, false);
            var owner = host.AddComponent<PlayerController>();
            owner.Initialize(id, "P" + id, 2);
            Set(owner, "jobPiecePrefab", id == 1 ? prefab : model);
            active.Add(owner);
        }
        var settings = new GameStartSettings { gameMode = GameMode.Escape, isTeamMode = false,
            playerCount = 2, useSkill = false, matchComposition = MatchComposition.OneVsOne };
        GameStartSettingsHolder.Current = settings;
        turns.Initialize(settings);
        var controller = fixture.AddComponent<InGamePieceDebugController>();
        controller.enabled = false; // Drive it explicitly; never initialize a normal game/scene.
        Set(controller, "playerManager", players);
        Set(controller, "turnManager", turns);
        Set(controller, "pieceMovementManager", movement);
        var catalog = ScriptableObject.CreateInstance<FixedFootholdCatalog>();
        Set(catalog, "playerFootholds", new[] { foot, foot });
        Set(controller, "footholdCatalog", catalog);
        var home = new GameObject("Unused exterior home").transform;
        home.SetParent(fixture.transform, false);
        home.position = new Vector3(99, 123, 200);
        Set(controller, "playerHomeAnchors", new[] { home, home, home, home });
        var positions = (Dictionary<BoardTileId, Vector3>)typeof(InGamePieceDebugController)
            .GetField("boardPositions", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
        positions.Add(BoardTileId.Start, new Vector3(-2, -1, -3));
        positions.Add(BoardTileId.Outer01, new Vector3(-1, 0, -3));
        positions.Add(BoardTileId.Outer02, new Vector3(0, 1, -3));
        positions.Add(BoardTileId.Outer16, new Vector3(2, -1, -3));
        PieceVisualLifecycleProbe.CreatedAt.Clear();
        Call(controller, "InitializePieceRuntimes");
        movement.OnVisualMoveResolved += (owner, ids, path) =>
            Call(controller, "HandleVisualMoveResolved", owner, ids, path);
        var views = (List<DebugPieceView>)typeof(InGamePieceDebugController)
            .GetField("pieceViews", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
        DebugPieceView View(int owner, int id = 0) => views.Find(v => v.PlayerId == owner && v.PieceId == id);
        PlayerRuntimeData.PieceRuntimeData Piece(int owner, int id = 0) => active[owner - 1].RuntimeData.Pieces[id];
        int Renderers(int owner) => active[owner - 1].GetComponentsInChildren<Renderer>(true).Length;
        int Colliders(int owner) => active[owner - 1].GetComponentsInChildren<Collider>(true).Length;

        Check("Waiting status-prefab pieces have no model or foothold objects", Renderers(1) == 0 && Colliders(1) == 0);
        Check("Waiting direct-model prefabs have not been instantiated", Renderers(2) == 0 && Colliders(2) == 0 &&
            PieceVisualLifecycleProbe.CreatedAt.Count == 0);
        Check("Waiting skills remain registered without 3D models", CharacterSkillRegistry.TryGet(1, 0, out var status) &&
            status != template && CharacterSkillRegistry.TryGet(1, 1, out _));
        Set(status, "remainingPassiveCooldownTurns", 2, typeof(CharacterStatusBehaviour));
        controller.TryGetTileWorldPosition(BoardTileId.Start, out var start);
        controller.TryGetTileWorldPosition(BoardTileId.Outer01, out var outer);
        Check("First deployment succeeds", movement.TryMovePiece(1, 0, 1));
        Check("Model and foothold are created only after InBoard", Renderers(1) == 2 && Colliders(1) == 2);
        Check("First deployment starts at Start rather than exterior home", Vector3.Distance(View(1).transform.position, start) < 0.001f);
        Check("Model Awake and foothold Awake both occur at Start", PieceVisualLifecycleProbe.CreatedAt.Count == 2 &&
            PieceVisualLifecycleProbe.CreatedAt.TrueForAll(p => Vector3.Distance(p, start) < 0.001f));
        var firstModel = status.GetComponentInChildren<MeshRenderer>().gameObject;
        float until = Time.realtimeSinceStartup + 3f;
        while (Vector3.Distance(View(1).transform.position, outer) > 0.001f && Time.realtimeSinceStartup < until)
        {
            Call(controller, "RefreshPiecePositions");
            yield return null;
        }
        Check("Existing movement animation reaches Outer01", Vector3.Distance(View(1).transform.position, outer) < 0.001f);
        Check("Spawning does not replace skill component or reset cooldown", CharacterSkillRegistry.TryGet(1, 0, out var current) &&
            current == status && status.RemainingPassiveCooldownTurns == 2);

        Piece(2).MoveTo(BoardTileId.Outer02);
        Check("Enemy captures deployed piece", movement.TryMovePiece(2, 0, -1) && Piece(1).State == PieceState.Waiting);
        Call(controller, "RefreshPiecePositions");
        yield return null;
        Check("Capture destroys model and foothold instead of storing or hiding them", firstModel == null &&
            Renderers(1) == 0 && Colliders(1) == 0 && status.transform.childCount == 0);
        Check("Captured piece keeps data and its registered skill component", CharacterSkillRegistry.TryGet(1, 0, out current) && current == status);
        Check("Direct-model prefab is instantiated only when on board", Renderers(2) == 2 && Colliders(2) == 2);

        Check("Captured piece can deploy again", movement.TryMovePiece(1, 0, 1));
        Call(controller, "RefreshPiecePositions");
        yield return null;
        Check("Re-deployment creates a fresh model at Start, not from storage", firstModel == null && Renderers(1) == 2 &&
            Vector3.Distance(View(1).transform.position, home.position) > 10f);
        Check("Captured direct-model prefab is really destroyed", Renderers(2) == 0 && Colliders(2) == 0);
        var goalModel = status.GetComponentInChildren<MeshRenderer>().gameObject;
        Piece(1).MoveTo(BoardTileId.Outer16);
        Check("Goal movement succeeds", movement.TryMovePiece(1, 0, 2) && Piece(1).State == PieceState.Goal);
        Call(controller, "RefreshPiecePositions");
        yield return null;
        Check("Goal destroys model and foothold instead of parking them outside", goalModel == null &&
            Renderers(1) == 0 && Colliders(1) == 0 && status.transform.childCount == 0);
        Check("Goal does not remove skill data", CharacterSkillRegistry.TryGet(1, 0, out current) && current == status);
        Piece(1).State = PieceState.Waiting;
        Call(controller, "RefreshPiecePositions");
        Check("Escape return to waiting still has no 3D objects", Renderers(1) == 0 && Colliders(1) == 0);
        Check("Re-entry after goal spawns at Start again", movement.TryMovePiece(1, 0, 1) &&
            Vector3.Distance(View(1).transform.position, start) < 0.001f && Renderers(1) == 2);
        Piece(1, 1).MoveTo(BoardTileId.Outer01);
        Call(controller, "RefreshPiecePositions");
        Piece(1).SetStackGroup(0, Piece(1));
        Piece(1, 1).SetStackGroup(0, Piece(1));
        Call(controller, "RefreshPiecePositions");
        yield return null;
        Check("Carried pieces also have no offscreen model stored elsewhere", Renderers(1) == 2 &&
            View(1, 1).transform.childCount == 0);
        Check("Carried skill registration survives model destruction", CharacterSkillRegistry.TryGet(1, 1, out _));
        Destroy(fixture);
        Destroy(data);
        Destroy(catalog);
        yield return null;
    }
}

public sealed class PieceVisualLifecycleProbe : MonoBehaviour
{
    public static readonly List<Vector3> CreatedAt = new List<Vector3>();
    private void Awake() => CreatedAt.Add(transform.position);
}
#endif
