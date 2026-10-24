using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using YutArena.Common;
using YutArena.InGame;
using YutArena.Managers;

/// <summary>
/// A single in-game active-skill button. It finds the current player's
/// CharacterStatusBehaviour children and uses the selected component's PieceId
/// when sending the active-skill request.
/// </summary>
[RequireComponent(typeof(Button))]
[RequireComponent(typeof(CanvasGroup))]
public sealed class ActiveSkillButtonController : MonoBehaviour
{
    [Header("Dependencies (Auto Find If Empty)")]
    [SerializeField] private TestTurnManager turnManager;
    [SerializeField] private PlayerManager playerManager;

    [Header("Skill Result Events")]
    [SerializeField] private UnityEvent<string> onSkillSucceeded = new UnityEvent<string>();
    [SerializeField] private UnityEvent<string> onSkillFailed = new UnityEvent<string>();

    private Button skillButton;
    private CanvasGroup canvasGroup;
    private CharacterStatusBehaviour currentCharacter;
    private int currentPlayerId = -1;
    private int preferredCasterPieceId = -1;
    private int targetPlayerId = -1;
    private int targetPieceId = -1;
    private YutResult selectedYutResult = YutResult.None;
    private bool isSubscribed;
    private bool isSelectingCaster;
    private bool isSelectingTarget;
    private int casterSelectionStartedFrame = -1;
    private int targetSelectionStartedFrame = -1;
    private DebugPieceView selectedCasterView;
    private DebugPieceView selectedTargetView;

    public event Action<CharacterActiveResult> SkillUseCompleted;
    public event Action SelectionChanged;
    public int SelectedCasterPieceId => preferredCasterPieceId;
    public bool SelectionStartedThisFrame =>
        Time.frameCount == casterSelectionStartedFrame || Time.frameCount == targetSelectionStartedFrame;

    public int CurrentCasterPieceId =>
        currentCharacter != null ? currentCharacter.PieceId : -1;
    public bool IsSelectingCaster => isSelectingCaster;
    public bool IsSelectingTarget => isSelectingTarget;

    private void Awake()
    {
        skillButton = GetComponent<Button>();
        canvasGroup = GetComponent<CanvasGroup>();
        skillButton.onClick.AddListener(HandleActiveSkillButtonClicked);

        ResolveDependencies();
        SetButtonVisible(false);
    }

    private void OnEnable()
    {
        ResolveDependencies();
        SubscribeToTurnEvents();
        StartCoroutine(RefreshAfterRuntimeRegistration());
    }

    private void OnDisable()
    {
        UnsubscribeFromTurnEvents();
        ResetTurnSelection();
        SetButtonVisible(false);
    }

    private void OnDestroy()
    {
        EndSkillSelection(clearCaster: true, clearTarget: true, refresh: false);
        if (skillButton != null)
            skillButton.onClick.RemoveListener(HandleActiveSkillButtonClicked);
    }

    private void Update()
    {
        if (!CharacterSkillRegistry.AreSkillsEnabled)
        {
            if (currentCharacter != null || isSelectingCaster || isSelectingTarget)
                ResetTurnSelection();
            SetButtonVisible(false);
            return;
        }
        // 말 프리팹 생성이 UI 초기화보다 늦는 경우에도, 현재 턴의 활성 스킬 UI를 놓치지 않도록 재탐색한다.
        if (currentCharacter == null)
        {
            RefreshForCurrentTurn();
        }
        UpdateButtonCooldownState();

        // The guide uses F for the same select / confirm flow as the button.
        if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame &&
            skillButton != null && skillButton.IsInteractable())
        {
            HandleActiveSkillButtonClicked();
            return;
        }

    }

    // Shared world input belongs to CharacterPieceInteractionController, not to two competing UI scripts.
    public void CancelSkillSelection() =>
        EndSkillSelection(clearCaster: true, clearTarget: true, refresh: true);

    public bool TrySelectSkillPiece(DebugPieceView view)
    {
        if (!CharacterSkillRegistry.AreSkillsEnabled ||
            !LocalSelectionInput.SharedInputAvailable || view == null || SelectionStartedThisFrame) return false;
        if (isSelectingTarget) return SelectTarget(view);
        return isSelectingCaster && SelectCaster(view);
    }

    /// <summary>
    /// Called by piece-selection UI. The matching CharacterStatusBehaviour and
    /// its PieceId will be used as the active-skill caster.
    /// </summary>
    public void SetCasterPiece(int pieceId)
    {
        preferredCasterPieceId = pieceId;
        RefreshForCurrentTurn();
        SelectionChanged?.Invoke();
    }

    public void ClearCasterPiece()
    {
        preferredCasterPieceId = -1;
        RefreshForCurrentTurn();
    }

    /// <summary>
    /// Called by target-selection UI for active skills that require a target.
    /// </summary>
    public void SetTarget(int playerId, int pieceId)
    {
        targetPlayerId = playerId;
        targetPieceId = CcEffectService.StackLeader(CcEffectService.GetPiece(playerId, pieceId))?.PieceId ?? pieceId;
        SelectionChanged?.Invoke();
    }

    public void ClearTarget()
    {
        targetPlayerId = -1;
        targetPieceId = -1;
    }

    public void SetSelectedYutResult(YutResult result)
    {
        selectedYutResult = result;
    }

    public void ClearSelectedYutResult()
    {
        selectedYutResult = YutResult.None;
    }

    public void RefreshForCurrentTurn()
    {
        ResolveDependencies();

        if (!CharacterSkillRegistry.AreSkillsEnabled || turnManager == null)
        {
            ResetTurnSelection();
            SetButtonVisible(false);
            return;
        }

        TurnContext turn = turnManager.CurrentTurn;
        if (turn == null ||
            turn.currentPlayer == PlayerSlot.None ||
            turn.currentPhase == TurnPhase.None ||
            turn.currentPhase == TurnPhase.TurnEnd ||
            turn.currentPhase == TurnPhase.GameEnd)
        {
            currentCharacter = null;
            currentPlayerId = -1;
            SetButtonVisible(false);
            return;
        }

        ShowButtonForPlayer((int)turn.currentPlayer);
    }

    private IEnumerator RefreshAfterRuntimeRegistration()
    {
        // Player pieces are instantiated after game initialization. Waiting two
        // frames lets their CharacterStatusBehaviour components register PieceId.
        yield return null;
        yield return null;
        if (!isActiveAndEnabled) yield break;

        ResolveDependencies();
        SubscribeToTurnEvents();
        RefreshForCurrentTurn();
    }

    private IEnumerator RetryPlayerAfterPieceCreation(int playerId)
    {
        yield return null;
        if (!isActiveAndEnabled || currentPlayerId != playerId) yield break;

        ShowButtonForPlayer(playerId);
    }

    private void ResolveDependencies()
    {
        if (turnManager == null)
            turnManager = FindFirstObjectByType<TestTurnManager>();
        if (playerManager == null)
            playerManager = FindFirstObjectByType<PlayerManager>();
    }

    private void SubscribeToTurnEvents()
    {
        if (isSubscribed || turnManager == null) return;

        turnManager.OnTurnStarted += HandleTurnStarted;
        turnManager.OnTurnEnded += HandleTurnEnded;
        turnManager.OnTurnPhaseChanged += HandleTurnPhaseChanged;
        isSubscribed = true;
    }

    private void UnsubscribeFromTurnEvents()
    {
        if (!isSubscribed || turnManager == null) return;

        turnManager.OnTurnStarted -= HandleTurnStarted;
        turnManager.OnTurnEnded -= HandleTurnEnded;
        turnManager.OnTurnPhaseChanged -= HandleTurnPhaseChanged;
        isSubscribed = false;
    }

    private void HandleTurnStarted(PlayerSlot player)
    {
        EndSkillSelection(clearCaster: true, clearTarget: true, refresh: false);
        ResetSkillArguments();
        preferredCasterPieceId = -1;
        ShowButtonForPlayer((int)player);

        if (currentCharacter == null)
            StartCoroutine(RetryPlayerAfterPieceCreation((int)player));
    }

    private void HandleTurnEnded(PlayerSlot player)
    {
        if (currentPlayerId != (int)player) return;

        ResetTurnSelection();
        SetButtonVisible(false);
    }

    private void HandleTurnPhaseChanged(TurnContext turn)
    {
        if (turn == null || (int)turn.currentPlayer != currentPlayerId)
            return;

        if ((isSelectingCaster || isSelectingTarget) &&
            (turn.currentPhase == TurnPhase.TurnEnd ||
             turn.currentPhase == TurnPhase.GameEnd))
        {
            EndSkillSelection(clearCaster: true, clearTarget: true, refresh: false);
        }

        UpdateButtonCooldownState();
    }

    private void ShowButtonForPlayer(int playerId)
    {
        currentPlayerId = playerId;
        currentCharacter = null;
        SetButtonVisible(false);

        if (!CharacterSkillRegistry.AreSkillsEnabled || playerManager == null ||
            !playerManager.TryGetPlayer(playerId, out PlayerController player) ||
            player.RuntimeData == null)
        {
            return;
        }

        currentCharacter = FindCasterCharacter(player);
        SetButtonVisible(currentCharacter != null);
        UpdateButtonCooldownState();
    }

    private CharacterStatusBehaviour FindCasterCharacter(PlayerController player)
    {
        CharacterStatusBehaviour firstOnBoard = null;
        CharacterStatusBehaviour firstWaiting = null;
        CharacterStatusBehaviour firstUnavailable = null;
        CharacterStatusBehaviour[] characters =
            player.GetComponentsInChildren<CharacterStatusBehaviour>(true);

        foreach (CharacterStatusBehaviour character in characters)
        {
            if (!TryGetEligiblePiece(player, character, out PlayerRuntimeData.PieceRuntimeData piece))
                continue;

            if (character.PieceId == preferredCasterPieceId)
                return character;

            if (!CcEffectService.CanUseSkill(piece) || !character.CanSelectAsActiveCaster(piece))
            {
                if (firstUnavailable == null) firstUnavailable = character;
                continue;
            }

            if (piece.State == PieceState.InBoard && firstOnBoard == null)
                firstOnBoard = character;
            else if (piece.State == PieceState.Waiting && firstWaiting == null)
                firstWaiting = character;
        }

        return firstOnBoard != null ? firstOnBoard : firstWaiting != null ? firstWaiting : firstUnavailable;
    }

    private static bool TryGetEligiblePiece(
        PlayerController player,
        CharacterStatusBehaviour character,
        out PlayerRuntimeData.PieceRuntimeData piece)
    {
        piece = null;
        return character != null &&
               character.PlayerId == player.PlayerId &&
               character.PieceId >= 0 &&
               character.HasActiveSkill &&
               player.TryGetPieceData(character.PieceId, out piece) &&
               piece.State != PieceState.Goal;
    }

    private bool HasSelectableActiveCaster()
    {
        if (playerManager == null ||
            !playerManager.TryGetPlayer(currentPlayerId, out PlayerController player))
            return false;

        CharacterStatusBehaviour[] characters =
            player.GetComponentsInChildren<CharacterStatusBehaviour>(true);
        foreach (CharacterStatusBehaviour character in characters)
        {
            if (character == null ||
                character.PlayerId != currentPlayerId ||
                !character.HasActiveSkill ||
                !player.TryGetPieceData(
                    character.PieceId,
                    out PlayerRuntimeData.PieceRuntimeData piece))
            {
                continue;
            }

            if (CharacterSkillRegistry.CanUseActive(currentPlayerId, character.PieceId))
                return true;
        }

        return false;
    }

    private void HandleActiveSkillButtonClicked()
    {
        if (!CharacterSkillRegistry.AreSkillsEnabled ||
            currentCharacter == null || !LocalSelectionInput.SharedInputAvailable) return;
        bool hasCaster = preferredCasterPieceId >= 0 && currentCharacter.PieceId == preferredCasterPieceId &&
            CharacterSkillRegistry.CanUseActive(currentPlayerId, preferredCasterPieceId);
        switch (currentCharacter.GetNextInputStep(isSelectingCaster || isSelectingTarget,
            hasCaster, targetPieceId >= 0))
        {
            case CharacterSkillInputStep.Caster:
                if (HasSelectableActiveCaster() && !isSelectingCaster) BeginCasterSelection();
                break;
            case CharacterSkillInputStep.Target:
                if (!isSelectingTarget) BeginTargetSelection();
                break;
            case CharacterSkillInputStep.Confirm:
                if (UseCurrentActiveSkill())
                    EndSkillSelection(clearCaster: true, clearTarget: true, refresh: true);
                break;
        }
    }

    private bool UseCurrentActiveSkill()
    {
        if (currentCharacter == null || currentPlayerId <= 0 || turnManager == null)
            return false;

        TurnContext turn = turnManager.CurrentTurn;
        if (turn == null ||
            (int)turn.currentPlayer != currentPlayerId ||
            turn.currentPhase == TurnPhase.TurnEnd ||
            turn.currentPhase == TurnPhase.GameEnd)
        {
            RefreshForCurrentTurn();
            return false;
        }

        CharacterActiveRequest request = new CharacterActiveRequest(
            currentPlayerId,
            currentCharacter.PieceId,
            targetPlayerId,
            targetPieceId,
            selectedYutResult);

        CharacterActiveResult result = CharacterSkillRegistry.TryUseActive(request);
        SkillUseCompleted?.Invoke(result);

        if (result.Succeeded)
        {
            onSkillSucceeded?.Invoke(result.Message);
        }
        else
            onSkillFailed?.Invoke(result.Message);

        RefreshForCurrentTurn();
        return result.Succeeded;
    }

    private void SetButtonVisible(bool visible)
    {
        if (canvasGroup == null || skillButton == null) return;

        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.interactable = visible;
        canvasGroup.blocksRaycasts = visible;
        skillButton.interactable = visible;
    }

    private void UpdateButtonCooldownState()
    {
        if (!CharacterSkillRegistry.AreSkillsEnabled)
        {
            SetButtonVisible(false);
            return;
        }
        if (currentCharacter == null || skillButton == null || canvasGroup == null)
            return;

        bool canUse = LocalSelectionInput.SharedInputAvailable && HasSelectableActiveCaster();
        canvasGroup.interactable = canUse;
        // 사용할 수 없는 상태에서도 Hover 상세 설명은 볼 수 있어야 합니다.
        canvasGroup.blocksRaycasts = true;
        skillButton.interactable = canUse;
    }

    private void ResetTurnSelection()
    {
        EndSkillSelection(clearCaster: true, clearTarget: true, refresh: false);
        currentCharacter = null;
        currentPlayerId = -1;
        preferredCasterPieceId = -1;
        ResetSkillArguments();
    }

    private void ResetSkillArguments()
    {
        ClearTarget();
        ClearSelectedYutResult();
    }

    private void BeginCasterSelection()
    {
        if (currentCharacter == null || currentPlayerId <= 0 || turnManager == null)
            return;

        TurnContext turn = turnManager.CurrentTurn;
        if (turn == null || (int)turn.currentPlayer != currentPlayerId)
            return;

        preferredCasterPieceId = -1;
        if (!isSelectingTarget)
        {
            ClearTarget();
            ClearSelectedTargetHighlight();
        }
        ClearSelectedCasterHighlight();
        isSelectingTarget = false;
        targetSelectionStartedFrame = -1;
        isSelectingCaster = true;
        casterSelectionStartedFrame = Time.frameCount;
        ClearAllSelectionHighlights();
        SelectionChanged?.Invoke();
    }

    private bool SelectCaster(DebugPieceView view)
    {
        if (view == null || view.PlayerId != currentPlayerId) return false;
        if (playerManager == null ||
            !playerManager.TryGetPlayer(currentPlayerId, out PlayerController player) ||
            !player.TryGetPieceData(view.PieceId, out var piece) ||
            !CharacterSkillRegistry.CanUseActive(currentPlayerId, view.PieceId)) return false;

        ClearSelectedCasterHighlight();
        selectedCasterView = view;
        selectedCasterView.SetSelected(true);
        SetCasterPiece(view.PieceId);

        if (currentCharacter != null && currentCharacter.GetNextInputStep(true, true,
                targetPieceId >= 0) == CharacterSkillInputStep.Target)
            BeginTargetSelection();
        SelectionChanged?.Invoke();
        return true;
    }

    private void BeginTargetSelection()
    {
        if (currentCharacter == null || currentPlayerId <= 0 || turnManager == null)
            return;

        TurnContext turn = turnManager.CurrentTurn;
        if (turn == null || (int)turn.currentPlayer != currentPlayerId)
            return;

        ClearTarget();
        ClearSelectedTargetHighlight();
        isSelectingCaster = false;
        casterSelectionStartedFrame = -1;
        isSelectingTarget = true;
        targetSelectionStartedFrame = Time.frameCount;
        SelectionChanged?.Invoke();
    }

    private bool SelectTarget(DebugPieceView view)
    {
        if (view == null || currentCharacter == null ||
            !CharacterSkillRegistry.CanUseActive(currentPlayerId, currentCharacter.PieceId) ||
            !currentCharacter.CanSelectActiveTarget(view.PlayerId, view.PieceId)) return false;

        ClearSelectedTargetHighlight();
        selectedTargetView = view;
        selectedTargetView.SetSelected(true);
        SetTarget(view.PlayerId, view.PieceId);
        return true;
    }

    private void EndSkillSelection(bool clearCaster, bool clearTarget, bool refresh)
    {
        ClearSelectedCasterHighlight();
        ClearSelectedTargetHighlight();
        isSelectingCaster = false;
        isSelectingTarget = false;
        casterSelectionStartedFrame = -1;
        targetSelectionStartedFrame = -1;
        if (clearCaster)
            preferredCasterPieceId = -1;
        if (clearTarget)
            ClearTarget();

        if (refresh && isActiveAndEnabled)
            RefreshForCurrentTurn();
        SelectionChanged?.Invoke();
    }

    private void ClearSelectedCasterHighlight()
    {
        if (selectedCasterView != null)
            selectedCasterView.SetSelected(false);
        selectedCasterView = null;
    }

    private void ClearSelectedTargetHighlight()
    {
        if (selectedTargetView != null)
            selectedTargetView.SetSelected(false);
        selectedTargetView = null;
    }

    private static void ClearAllSelectionHighlights()
    {
        DebugPieceView[] views = FindObjectsByType<DebugPieceView>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        foreach (DebugPieceView view in views)
            view.SetSelected(false);
    }


}
