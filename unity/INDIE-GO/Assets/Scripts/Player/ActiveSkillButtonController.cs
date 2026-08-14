using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using YutArena.Common;
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
    private const string InGameSceneName = "InGameScene";

    [Header("Dependencies (Auto Find If Empty)")]
    [SerializeField] private TestTurnManager turnManager;
    [SerializeField] private PlayerManager playerManager;

    [Header("Skill Result Events")]
    [SerializeField] private UnityEvent<string> onSkillSucceeded = new UnityEvent<string>();
    [SerializeField] private UnityEvent<string> onSkillFailed = new UnityEvent<string>();

    private Button skillButton;
    private CanvasGroup canvasGroup;
    private Text skillLabel;
    private CharacterStatusBehaviour currentCharacter;
    private int currentPlayerId = -1;
    private int preferredCasterPieceId = -1;
    private int targetPlayerId = -1;
    private int targetPieceId = -1;
    private YutResult selectedYutResult = YutResult.None;
    private bool isSubscribed;

    public event Action<CharacterActiveResult> SkillUseCompleted;

    public int CurrentCasterPieceId =>
        currentCharacter != null ? currentCharacter.PieceId : -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureInGameActiveSkillButton()
    {
        if (SceneManager.GetActiveScene().name != InGameSceneName) return;

        ActiveSkillButtonController[] existingControllers =
            FindObjectsByType<ActiveSkillButtonController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

        foreach (ActiveSkillButtonController controller in existingControllers)
        {
            if (controller.GetComponentInParent<Canvas>(true) != null)
                return;

            // A controller previously attached to a non-UI manager object must
            // not compete with the automatically generated button.
            controller.enabled = false;
        }

        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
            canvas = CreateInGameCanvas();

        EnsureEventSystem();
        CreateActiveSkillButton(canvas.transform);
    }

    private static Canvas CreateInGameCanvas()
    {
        var canvasObject = new GameObject(
            "InGameSkillCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        return canvas;
    }

    private static void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null) return;

        var eventSystemObject = new GameObject(
            "EventSystem",
            typeof(EventSystem));
        InputSystemUIInputModule inputModule =
            eventSystemObject.AddComponent<InputSystemUIInputModule>();
        inputModule.AssignDefaultActions();
    }

    private static void CreateActiveSkillButton(Transform parent)
    {
        var buttonObject = new GameObject(
            "ActiveSkillButton",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button),
            typeof(CanvasGroup));
        buttonObject.transform.SetParent(parent, false);

        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(1f, 0f);
        buttonRect.anchorMax = new Vector2(1f, 0f);
        buttonRect.pivot = new Vector2(1f, 0f);
        buttonRect.anchoredPosition = new Vector2(-48f, 48f);
        buttonRect.sizeDelta = new Vector2(280f, 84f);

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.16f, 0.34f, 0.58f, 0.96f);

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.9f, 0.55f, 1f);
        colors.pressedColor = new Color(0.72f, 0.78f, 0.9f, 1f);
        colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.55f);
        button.colors = colors;

        var labelObject = new GameObject(
            "Label",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Text));
        labelObject.transform.SetParent(buttonObject.transform, false);

        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(16f, 8f);
        labelRect.offsetMax = new Vector2(-16f, -8f);

        Text label = labelObject.GetComponent<Text>();
        label.text = "ACTIVE SKILL";
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 26;
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
        label.raycastTarget = false;

        buttonObject.AddComponent<ActiveSkillButtonController>();
    }

    private void Awake()
    {
        skillButton = GetComponent<Button>();
        canvasGroup = GetComponent<CanvasGroup>();
        skillLabel = GetComponentInChildren<Text>(true);
        skillButton.onClick.AddListener(UseCurrentActiveSkill);

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
        if (skillButton != null)
            skillButton.onClick.RemoveListener(UseCurrentActiveSkill);
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

        UpdateButtonLabel(currentCharacter);
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
        UpdateButtonLabel(currentCharacter);
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

    private void UseCurrentActiveSkill()
    {
        if (currentCharacter == null || currentPlayerId <= 0 || turnManager == null)
            return;

        TurnContext turn = turnManager.CurrentTurn;
        if (turn == null ||
            (int)turn.currentPlayer != currentPlayerId ||
            turn.currentPhase == TurnPhase.TurnEnd ||
            turn.currentPhase == TurnPhase.GameEnd)
        {
            RefreshForCurrentTurn();
            return;
        }

        CharacterActiveRequest request = new CharacterActiveRequest(
            currentPlayerId,
            currentCharacter.PieceId,
            targetPlayerId,
            targetPieceId,
            selectedYutResult);

        CharacterActiveResult result = CharacterSkillRegistry.TryUseActive(request);
        if (!result.Succeeded && preferredCasterPieceId < 0)
            result = TryOtherCasterCharacters(result);
        SkillUseCompleted?.Invoke(result);

        if (result.Succeeded)
        {
            turnManager.ResolveSkillResult(result.SuppressExtraThrow);
            onSkillSucceeded?.Invoke(result.Message);
        }
        else
            onSkillFailed?.Invoke(result.Message);

        RefreshForCurrentTurn();
    }

    private CharacterActiveResult TryOtherCasterCharacters(CharacterActiveResult firstFailure)
    {
        if (playerManager == null ||
            !playerManager.TryGetPlayer(currentPlayerId, out PlayerController player))
        {
            return firstFailure;
        }

        foreach (CharacterStatusBehaviour character in
                 player.GetComponentsInChildren<CharacterStatusBehaviour>(true))
        {
            if (character == currentCharacter ||
                !TryGetEligiblePiece(player, character, out _))
                continue;

            var request = new CharacterActiveRequest(
                currentPlayerId,
                character.PieceId,
                targetPlayerId,
                targetPieceId,
                selectedYutResult);
            CharacterActiveResult result = CharacterSkillRegistry.TryUseActive(request);
            if (!result.Succeeded) continue;

            currentCharacter = character;
            return result;
        }

        return firstFailure;
    }

    private void SetButtonVisible(bool visible)
    {
        if (canvasGroup == null || skillButton == null) return;

        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.interactable = visible;
        canvasGroup.blocksRaycasts = visible;
        skillButton.interactable = visible;
    }

    private void UpdateButtonLabel(CharacterStatusBehaviour character)
    {
        if (skillLabel == null) return;

        CharacterData data = character != null ? character.Data : null;
        string activeName = data != null && !string.IsNullOrWhiteSpace(data.active_Name)
            ? data.active_Name
            : "ACTIVE SKILL";
        int remainingCooldown = character != null
            ? CharacterSkillRegistry.GetRemainingActiveCooldown(character.PlayerId, data)
            : 0;
        skillLabel.text = remainingCooldown > 0
            ? $"{activeName} ({remainingCooldown} TURN)"
            : activeName;
    }

    private void UpdateButtonCooldownState()
    {
        if (currentCharacter == null || skillButton == null || canvasGroup == null)
            return;

        int remainingCooldown = CharacterSkillRegistry.GetRemainingActiveCooldown(
            currentCharacter.PlayerId,
            currentCharacter.Data);
        bool canUse = remainingCooldown <= 0 &&
                      currentCharacter.IsActiveUsableInCurrentPhase();
        canvasGroup.interactable = canUse;
        canvasGroup.blocksRaycasts = canUse;
        skillButton.interactable = canUse;
    }

    private void ResetTurnSelection()
    {
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
}
