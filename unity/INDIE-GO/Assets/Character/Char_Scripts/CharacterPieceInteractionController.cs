using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using YutArena.Common;
using YutArena.InGame;
using YutArena.Managers;
using YutArena.Managers.GameProgress;

/// <summary>
/// Owns piece/skill selection flow, not movement or skill effects. The existing
/// turn manager executes moves; MoveDestinationSelector only displays hints.
/// Installed by the navigation view, without scene or prefab edits.
/// </summary>
public sealed class CharacterPieceInteractionController : MonoBehaviour
{
    private MoveDestinationSelector navigation;
    private TestTurnManager turns;
    private PlayerManager players;
    private PieceMovementManager movement;
    private ActiveSkillButtonController activeButton;
    private int selectedPieceId = -1;
    private int selectedPlayerId = -1;
    private DebugPieceView selectedView;
    private bool subscribed;
    private bool refreshing;
    private string renderedState;

    public int SelectedPieceId => selectedPieceId;

    public void Initialize(MoveDestinationSelector view)
    {
        navigation = view;
        navigation.BindWaitingPieceSelection(CanSelectWaitingPiece, TrySelectWaitingPiece);
        ResolveDependencies();
        RefreshNavigation();
    }

    private void OnEnable()
    {
        ResolveDependencies();
        if (navigation != null)
            navigation.BindWaitingPieceSelection(CanSelectWaitingPiece, TrySelectWaitingPiece);
        InvalidateNavigation();
    }

    public void InvalidateNavigation()
    {
        renderedState = null;
        RefreshNavigation();
    }

    private void OnDisable()
    {
        if (subscribed && turns != null)
        {
            turns.OnPendingResultsChanged -= HandlePendingResultsChanged;
            turns.OnTurnPhaseChanged -= HandlePhaseChanged;
            turns.OnTurnEnded -= HandleTurnEnded;
        }
        subscribed = false;
        BindActiveButton(null);
        ClearPieceSelection();
        if (navigation != null)
        {
            navigation.BindWaitingPieceSelection(null, null);
            navigation.ClearAll();
        }
        renderedState = null;
    }

    private void ResolveDependencies()
    {
        if (turns == null) turns = FindFirstObjectByType<TestTurnManager>();
        if (players == null) players = FindFirstObjectByType<PlayerManager>();
        if (movement == null) movement = FindFirstObjectByType<PieceMovementManager>();
        if (!subscribed && turns != null)
        {
            turns.OnPendingResultsChanged += HandlePendingResultsChanged;
            turns.OnTurnPhaseChanged += HandlePhaseChanged;
            turns.OnTurnEnded += HandleTurnEnded;
            subscribed = true;
        }
        if (activeButton == null) BindActiveButton(FindFirstObjectByType<ActiveSkillButtonController>());
    }

    private void BindActiveButton(ActiveSkillButtonController button)
    {
        if (activeButton == button) return;
        if (activeButton != null)
        {
            activeButton.SelectionChanged -= RefreshNavigation;
            activeButton.SkillUseCompleted -= HandleSkillCompleted;
        }
        activeButton = button;
        if (activeButton != null)
        {
            activeButton.SelectionChanged += RefreshNavigation;
            activeButton.SkillUseCompleted += HandleSkillCompleted;
        }
    }

    private bool CanAcceptInput => navigation != null && navigation.isActiveAndEnabled &&
        turns != null && turns.CurrentTurn != null && !turns.CurrentTurn.isGameEnded &&
        LocalSelectionInput.SharedInputAvailable &&
        (turns.CurrentTurn.currentPhase == TurnPhase.WaitThrow ||
         turns.CurrentTurn.currentPhase == TurnPhase.WaitAction);

    private bool SkillSelectionActive => activeButton != null &&
        (activeButton.IsSelectingCaster || activeButton.IsSelectingTarget);

    private void Update()
    {
        ResolveDependencies();
        RefreshNavigation();
        if (!CanAcceptInput || Mouse.current == null || Camera.main == null) return;
        Vector2 pointer = Mouse.current.position.ReadValue();
        navigation.HoverAt(Camera.main, pointer);
        if (Mouse.current.rightButton.wasPressedThisFrame ||
            (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame))
        {
            if (SkillSelectionActive) activeButton.CancelSkillSelection();
            ClearPieceSelection();
            activeButton?.ClearCasterPiece();
            RefreshNavigation();
            return;
        }
        if (!Mouse.current.leftButton.wasPressedThisFrame ||
            (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) ||
            (SkillSelectionActive && activeButton.SelectionStartedThisFrame)) return;

        bool pickedArrow = navigation.TryPickPieceMarker(Camera.main, pointer, out var view);
        if (SkillSelectionActive)
        {
            if (!pickedArrow) TryFindPiece(Camera.main, pointer, IsSkillCandidate, out view);
            if (view != null) activeButton.TrySelectSkillPiece(view);
            else if (!DebugPieceView.TryFindAtScreenPosition(Camera.main, pointer, out _))
            {
                activeButton.CancelSkillSelection();
                ClearPieceSelection();
            }
            RefreshNavigation();
            return;
        }

        if (pickedArrow && CanSelectPiece(view.PlayerId, view.PieceId))
            SelectPiece(view.PlayerId, view.PieceId, view);
        else if (selectedPieceId >= 0 && turns.CurrentTurn.currentPhase == TurnPhase.WaitAction &&
                 navigation.TryPickDestination(Camera.main, pointer, out var destination) &&
                 destination.PieceId == selectedPieceId && CanMovePiece(selectedPieceId))
        {
            // Do not implement turn completion, capture rewards or passive movement here.
            turns.RequestMovePiece(selectedPieceId, destination.Result);
            ClearPieceSelection();
            activeButton?.ClearCasterPiece();
        }
        else if (TryFindPiece(Camera.main, pointer,
                     candidate => CanSelectPiece(candidate.PlayerId, candidate.PieceId), out view))
            SelectPiece(view.PlayerId, view.PieceId, view);
        else
        {
            ClearPieceSelection();
            activeButton?.ClearCasterPiece();
        }
        RefreshNavigation();
    }

    public bool CanSelectWaitingPiece(int playerId, int pieceId) =>
        CanAcceptInput && !SkillSelectionActive &&
        players != null && players.TryGetPlayer(playerId, out var player) &&
        player.TryGetPieceData(pieceId, out var piece) && piece.State == PieceState.Waiting &&
        playerId == (int)turns.CurrentTurn.currentPlayer && CanMovePiece(pieceId);

    public bool TrySelectWaitingPiece(int playerId, int pieceId)
    {
        if (!CanSelectWaitingPiece(playerId, pieceId)) return false;
        SelectPiece(playerId, pieceId, null);
        RefreshNavigation();
        return true;
    }

    private bool CanSelectPiece(int playerId, int pieceId)
    {
        if (!CanAcceptInput || playerId != (int)turns.CurrentTurn.currentPlayer ||
            players == null || !players.TryGetPlayer(playerId, out var player) ||
            !player.TryGetPieceData(pieceId, out var piece) || piece.State != PieceState.InBoard) return false;
        return CanMovePiece(pieceId) || CharacterSkillRegistry.CanUseActive(playerId, pieceId);
    }

    private bool CanMovePiece(int pieceId)
    {
        if (turns == null || players == null || movement == null || turns.CurrentTurn == null ||
            turns.CurrentTurn.currentPhase != TurnPhase.WaitAction || turns.TryGetPendingForcedMove(out _) ||
            !players.TryGetPlayer((int)turns.CurrentTurn.currentPlayer, out var player) ||
            !player.TryGetPieceData(pieceId, out var piece) || !CcEffectService.CanMove(piece)) return false;
        foreach (var result in turns.PendingResults)
        {
            int count = PreviewMoveCount(piece, result);
            if (count == 0 || (piece.State == PieceState.Waiting && count < 0)) continue;
            if (movement.TryPreviewLanding(player.PlayerId, pieceId, count, out _, out _)) return true;
        }
        return false;
    }

    private int PreviewMoveCount(PlayerRuntimeData.PieceRuntimeData piece, YutThrowData result)
    {
        int count = YutResultRule.GetMoveCount(result.result);
        if (CharacterSkillRegistry.AreSkillsEnabled &&
            CharacterSkillRegistry.TryGet((int)turns.CurrentTurn.currentPlayer, piece.PieceId, out var status))
            return status.PreviewMoveCount(count, result);
        var scale = piece.Cc.Get(CcDefine.Move_scale);
        return scale != null ? count * scale.Value : count;
    }

    private bool IsSkillCandidate(DebugPieceView view)
    {
        if (view == null || activeButton == null) return false;
        if (activeButton.IsSelectingTarget)
            return CharacterSkillRegistry.TryGet((int)turns.CurrentTurn.currentPlayer,
                activeButton.CurrentCasterPieceId, out var caster) &&
                caster.CanSelectActiveTarget(view.PlayerId, view.PieceId);
        return view.PlayerId == (int)turns.CurrentTurn.currentPlayer &&
            CharacterSkillRegistry.CanUseActive(view.PlayerId, view.PieceId);
    }

    private void SelectPiece(int playerId, int pieceId, DebugPieceView view)
    {
        ClearPieceSelection();
        selectedPlayerId = playerId;
        selectedPieceId = pieceId;
        selectedView = view;
        if (selectedView != null) selectedView.SetSelected(true);
        activeButton?.SetCasterPiece(pieceId);
    }

    private void ClearPieceSelection()
    {
        if (selectedView != null) selectedView.SetSelected(false);
        selectedView = null;
        selectedPieceId = -1;
        selectedPlayerId = -1;
    }

    private void HandlePendingResultsChanged(List<YutThrowData> results)
    {
        ClearPieceSelection();
        if (!SkillSelectionActive) activeButton?.ClearCasterPiece();
        RefreshNavigation();
    }

    private void HandlePhaseChanged(TurnContext context)
    {
        if (context == null || (selectedPlayerId > 0 && selectedPlayerId != (int)context.currentPlayer))
            ClearPieceSelection();
        RefreshNavigation();
    }

    private void HandleTurnEnded(PlayerSlot player)
    {
        ClearPieceSelection();
        renderedState = null;
        if (navigation != null) navigation.ClearAll();
    }

    private void HandleSkillCompleted(CharacterActiveResult result)
    {
        if (result.Succeeded) ClearPieceSelection();
        RefreshNavigation();
    }

    public void RefreshNavigation()
    {
        if (refreshing || navigation == null) return;
        refreshing = true;
        try
        {
            var hints = new List<MoveDestinationSelector.PieceHint>();
            var destinations = new List<MoveDestinationSelector.DestinationHint>();
            var key = new StringBuilder();
            if (CanAcceptInput)
            {
                int ownerId = (int)turns.CurrentTurn.currentPlayer;
                key.Append(ownerId).Append('/').Append(turns.CurrentTurn.currentPhase)
                    .Append('/').Append(selectedPieceId).Append('/').Append(SkillSelectionActive)
                    .Append('/').Append(activeButton != null && activeButton.IsSelectingTarget);
                var views = FindObjectsByType<DebugPieceView>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                Array.Sort(views, (a, b) => a.GetInstanceID().CompareTo(b.GetInstanceID()));
                foreach (var pieceView in views)
                {
                    bool isSkill = SkillSelectionActive ? IsSkillCandidate(pieceView) :
                        pieceView.PlayerId == ownerId && CanSelectPiece(ownerId, pieceView.PieceId) &&
                        CharacterSkillRegistry.CanUseActive(ownerId, pieceView.PieceId);
                    bool isMove = !SkillSelectionActive && pieceView.PlayerId == ownerId &&
                        CanSelectPiece(ownerId, pieceView.PieceId) && CanMovePiece(pieceView.PieceId);
                    if (!isSkill && !isMove) continue;
                    hints.Add(new MoveDestinationSelector.PieceHint(pieceView, isSkill));
                    key.Append('|').Append(pieceView.GetInstanceID()).Append(':').Append(isSkill);
                }
                if (!SkillSelectionActive && selectedPieceId >= 0 && CanMovePiece(selectedPieceId) &&
                    players.TryGetPlayer(ownerId, out var owner) && owner.TryGetPieceData(selectedPieceId, out var selected))
                {
                    var seen = new HashSet<YutResult>();
                    var results = new List<YutThrowData>(turns.PendingResults);
                    results.Sort((a, b) => YutResultRule.GetMoveCount(a.result).CompareTo(YutResultRule.GetMoveCount(b.result)));
                    foreach (var result in results)
                    {
                        if (!seen.Add(result.result)) continue;
                        int count = PreviewMoveCount(selected, result);
                        if (count == 0 || (selected.State == PieceState.Waiting && count < 0) ||
                            !movement.TryPreviewLanding(ownerId, selectedPieceId, count, out var tile, out bool finished)) continue;
                        destinations.Add(new MoveDestinationSelector.DestinationHint(selectedPieceId, result.result, tile, finished));
                        key.Append('>').Append(result.result).Append(':').Append(tile).Append(':').Append(finished);
                    }
                }
            }
            string state = key.ToString();
            if (state == renderedState) return;
            renderedState = state;
            navigation.ShowNavigation(hints, destinations);
        }
        finally { refreshing = false; }
    }

    private static bool TryFindPiece(Camera camera, Vector2 position,
        Func<DebugPieceView, bool> eligible, out DebugPieceView selected)
    {
        selected = null;
        Ray ray = camera.ScreenPointToRay(position);
        float nearest = float.PositiveInfinity;
        foreach (var view in FindObjectsByType<DebugPieceView>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (!eligible(view)) continue;
            foreach (var collider in view.GetComponentsInChildren<Collider>(false))
            {
                if (!collider.enabled || !collider.Raycast(ray, out var hit, camera.farClipPlane) || hit.distance >= nearest) continue;
                nearest = hit.distance;
                selected = view;
            }
        }
        if (selected == null && DebugPieceView.TryFindAtScreenPosition(camera, position, out var fallback) && eligible(fallback))
            selected = fallback;
        return selected != null;
    }
}
