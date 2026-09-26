using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
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
    private InGamePieceDebugController suspendedPieceController;
    private bool suspendedPieceControllerWasEnabled;

    public event Action<CharacterActiveResult> SkillUseCompleted;

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
        // 말 프리팹 생성이 UI 초기화보다 늦는 경우에도, 현재 턴의 활성 스킬 UI를 놓치지 않도록 재탐색한다.
        if (currentCharacter == null)
        {
            RefreshForCurrentTurn();
        }

        if ((!isSelectingCaster && !isSelectingTarget) ||
            (isSelectingCaster && Time.frameCount == casterSelectionStartedFrame) ||
            (isSelectingTarget && Time.frameCount == targetSelectionStartedFrame))
            return;

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            EndSkillSelection(clearCaster: true, clearTarget: true, refresh: true);
            return;
        }

        if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
            return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        if (isSelectingTarget)
            TrySelectTargetAtPointer();
        else
            TrySelectCasterAtPointer();
    }

    /// <summary>
    /// Called by piece-selection UI. The matching CharacterStatusBehaviour and
    /// its PieceId will be used as the active-skill caster.
    /// </summary>
    public void SetCasterPiece(int pieceId)
    {
        preferredCasterPieceId = pieceId;
        RefreshForCurrentTurn();
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
        targetPieceId = pieceId;
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

        if (turnManager == null)
        {
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

        if (playerManager == null ||
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
        CharacterStatusBehaviour[] characters =
            player.GetComponentsInChildren<CharacterStatusBehaviour>(true);

        foreach (CharacterStatusBehaviour character in characters)
        {
            if (!TryGetEligiblePiece(player, character, out PlayerRuntimeData.PieceRuntimeData piece))
                continue;

            if (character.PieceId == preferredCasterPieceId)
                return character;

            if (piece.State == PieceState.InBoard && firstOnBoard == null)
                firstOnBoard = character;
            else if (piece.State == PieceState.Waiting && firstWaiting == null)
                firstWaiting = character;
        }

        return firstOnBoard != null ? firstOnBoard : firstWaiting;
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

            if (character.CanSelectAsActiveCaster(piece))
                return true;
        }

        return false;
    }

    private void HandleActiveSkillButtonClicked()
    {
        //수정: 플레이어 전체에 적용되는 액티브는 말 선택 모드 없이 한 번의 클릭으로 사용합니다.
        if (currentCharacter == null)
            return;

        if (currentCharacter.RequiresCasterPieceSelection && !HasSelectableActiveCaster())
        {
            if (isSelectingCaster || isSelectingTarget)
                EndSkillSelection(clearCaster: true, clearTarget: true, refresh: true);
            return;
        }

        if (!currentCharacter.RequiresCasterPieceSelection)
        {
            if (currentCharacter.RequiresTargetPieceSelection && targetPieceId < 0)
            {
                BeginTargetSelection();
                return;
            }

            if (UseCurrentActiveSkill())
                EndSkillSelection(clearCaster: true, clearTarget: true, refresh: true);
            return;
        }

        if (!isSelectingCaster && !isSelectingTarget)
        {
            BeginCasterSelection();
            return;
        }

        if (isSelectingCaster)
        {
            if (preferredCasterPieceId < 0)
                return;

            if (currentCharacter.RequiresTargetPieceSelection)
                BeginTargetSelection();
            else if (UseCurrentActiveSkill())
                EndSkillSelection(clearCaster: true, clearTarget: true, refresh: true);
            return;
        }

        if (preferredCasterPieceId < 0 ||
            (currentCharacter.RequiresTargetPieceSelection && targetPieceId < 0))
            return;

        if (UseCurrentActiveSkill())
            EndSkillSelection(clearCaster: true, clearTarget: true, refresh: true);
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
            turnManager.ResolveSkillResult(result.SuppressExtraThrow);
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
        if (currentCharacter == null || skillButton == null || canvasGroup == null)
            return;

        int remainingCooldown = CharacterSkillRegistry.GetRemainingActiveCooldown(
            currentCharacter.PlayerId,
            currentCharacter.Data);
        int requiredSkillPoints = currentCharacter.ActiveSkillPointCost;
        int currentSkillPoints = CharacterSkillRegistry.GetSkillPoints(
            currentCharacter.PlayerId);
        bool canUse = remainingCooldown <= 0 &&
                      currentSkillPoints >= requiredSkillPoints &&
                      currentCharacter.IsActiveUsableInCurrentPhase();
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
        ClearTarget();
        ClearSelectedCasterHighlight();
        ClearSelectedTargetHighlight();
        isSelectingTarget = false;
        targetSelectionStartedFrame = -1;
        isSelectingCaster = true;
        casterSelectionStartedFrame = Time.frameCount;
        SuspendNormalPieceClickHandling();
        ClearAllSelectionHighlights();
    }

    private void TrySelectCasterAtPointer()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null) return;

        Vector2 screenPosition = Mouse.current.position.ReadValue();
        if (!DebugPieceView.TryFindAtScreenPosition(mainCamera, screenPosition, out DebugPieceView view) ||
            view.PlayerId != currentPlayerId)
            return;
        if (playerManager == null ||
            !playerManager.TryGetPlayer(currentPlayerId, out PlayerController player) ||
            !player.TryGetPieceData(view.PieceId, out PlayerRuntimeData.PieceRuntimeData piece) ||
            piece.State == PieceState.Goal ||
            !CharacterSkillRegistry.TryGet(
                currentPlayerId,
                view.PieceId,
                out CharacterStatusBehaviour character) ||
            !character.HasActiveSkill ||
            !character.CanSelectAsActiveCaster(piece))
        {
            return;
        }

        ClearSelectedCasterHighlight();
        selectedCasterView = view;
        selectedCasterView.SetSelected(true);
        SetCasterPiece(view.PieceId);

        if (currentCharacter != null && currentCharacter.RequiresTargetPieceSelection)
            BeginTargetSelection();
    }

    private void BeginTargetSelection()
    {
        if (currentCharacter == null || currentPlayerId <= 0 || turnManager == null)
            return;
        if (currentCharacter.RequiresCasterPieceSelection && preferredCasterPieceId < 0)
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
        SuspendNormalPieceClickHandling();
    }

    private void TrySelectTargetAtPointer()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null) return;

        Vector2 screenPosition = Mouse.current.position.ReadValue();
        if (!DebugPieceView.TryFindAtScreenPosition(mainCamera, screenPosition, out DebugPieceView view) ||
            view.PlayerId == currentPlayerId)
            return;
        if (playerManager == null ||
            !playerManager.TryGetPlayer(view.PlayerId, out PlayerController player) ||
            !player.TryGetPieceData(view.PieceId, out PlayerRuntimeData.PieceRuntimeData piece) ||
            piece.State != PieceState.InBoard ||
            !CharacterSkillRegistry.IsTargetable(view.PlayerId, view.PieceId))
        {
            return;
        }

        ClearSelectedTargetHighlight();
        selectedTargetView = view;
        selectedTargetView.SetSelected(true);
        SetTarget(view.PlayerId, view.PieceId);
    }

    private void EndSkillSelection(bool clearCaster, bool clearTarget, bool refresh)
    {
        ClearSelectedCasterHighlight();
        ClearSelectedTargetHighlight();
        RestoreNormalPieceClickHandling();
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

    private void SuspendNormalPieceClickHandling()
    {
        if (suspendedPieceController != null) return;

        suspendedPieceController = FindFirstObjectByType<InGamePieceDebugController>();
        if (suspendedPieceController == null) return;

        suspendedPieceControllerWasEnabled = suspendedPieceController.enabled;
        suspendedPieceController.enabled = false;
    }

    private void RestoreNormalPieceClickHandling()
    {
        if (suspendedPieceController != null)
            suspendedPieceController.enabled = suspendedPieceControllerWasEnabled;

        suspendedPieceController = null;
        suspendedPieceControllerWasEnabled = false;
    }
}
