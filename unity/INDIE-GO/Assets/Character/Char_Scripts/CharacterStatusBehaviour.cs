using System;
using UnityEngine;
using YutArena.Common;
using YutArena.InGame;
using YutArena.Managers;

/// <summary>
/// 캐릭터 말 프리팹의 공통 스킬 기반 클래스입니다.
/// 네 개의 말은 각각 이 컴포넌트를 가지며 PlayerId/PieceId로 레지스트리에 등록됩니다.
/// SP와 액티브 쿨타임은 CharacterSkillRegistry가 플레이어 단위로 검증합니다.
/// </summary>
public abstract class CharacterStatusBehaviour : MonoBehaviour
{
    [SerializeField] private CharacterData characterData;
    [Tooltip("-1이면 부모 PlayerController 아래 등록 순서로 PieceId를 자동 배정합니다.")]
    [SerializeField] private int pieceId = -1;

    private GameObject spawnedVisualModel;
    private bool isRegistered;
    private int grantedProtectionCharges;
    private int grantedProtectionRemainingOwnerTurns;
    private int remainingPassiveCooldownTurns;

    public CharacterData Data => characterData;
    public CharacterSkillStatus PassiveStatus =>
        characterData != null ? characterData.passive_Status : CharacterSkillStatus.None;
    public CharacterSkillStatus ActiveStatus =>
        characterData != null ? characterData.active_Status : CharacterSkillStatus.None;
    public bool HasActiveSkill => characterData != null && characterData.HasActiveSkill;
    public int ActiveCooldownTurns =>
        characterData != null ? Mathf.Max(0, characterData.active_CooldownTurns) : 0;
    public int ActiveSkillPointCost =>
        characterData != null ? Mathf.Max(0, characterData.active_SkillPointCost) : 0;
    public int PassiveCooldownTurns =>
        characterData != null ? Mathf.Max(0, characterData.passive_CooldownTurns) : 0;
    public int RemainingPassiveCooldownTurns => remainingPassiveCooldownTurns;
    public bool IsPassiveReady => remainingPassiveCooldownTurns <= 0;
    public int PlayerId => Owner != null ? Owner.PlayerId : -1;
    public int PieceId => pieceId;
    public virtual bool IsTargetable => true;
    //수정: 플레이어 전체 규칙에 적용되는 액티브는 UI에서 사용자 말을 선택하지 않아도 됩니다.
    public virtual bool RequiresCasterPieceSelection => true;

    protected PlayerController Owner { get; private set; }
    protected PlayerManager Players { get; private set; }
    protected PieceMovementManager Movement { get; private set; }
    protected TestTurnManager Turns { get; private set; }
    protected TestYutRuleManager YutRules { get; private set; }

    protected virtual void Awake()
    {
        ValidateCharacterData();
        EnsureVisualModel();
    }

    protected virtual void OnEnable()
    {
        TryRegisterRuntime();
    }

    protected virtual void Start()
    {
        TryRegisterRuntime();
    }

    private void Update()
    {
        // 프리팹이 PlayerController.Initialize보다 먼저 활성화될 수 있으므로 등록될 때까지만 재시도합니다.
        if (!isRegistered) TryRegisterRuntime();
    }

    protected virtual void OnDisable()
    {
        CharacterSkillRegistry.Unregister(this);
        isRegistered = false;
    }

    /// <summary>
    /// 런타임에 다른 CharacterData를 주입해야 할 때 사용합니다.
    /// 동일 프리팹을 풀링할 경우 기존 외형을 정리한 뒤 새 외형을 생성합니다.
    /// </summary>
    public void Initialize(CharacterData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data), "CharacterData cannot be null.");

        if (characterData == data && spawnedVisualModel != null)
            return;

        characterData = data;

        if (spawnedVisualModel != null)
            Destroy(spawnedVisualModel);

        spawnedVisualModel = null;
        EnsureVisualModel();
    }

    public bool TryGetPassiveStatus(out CharacterSkillStatus status)
    {
        status = PassiveStatus;
        return status != CharacterSkillStatus.None;
    }

    public bool TryGetActiveStatus(out CharacterSkillStatus status)
    {
        status = ActiveStatus;
        return status != CharacterSkillStatus.None;
    }

    public virtual int ModifyMoveCount(CharacterMoveRequest request)
    {
        return request.MoveCount;
    }

    public virtual (YutResult, float)[] ModifyYutProbability(
        (YutResult, float)[] currentTable)
    {
        return currentTable;
    }

    public virtual bool ShouldGrantExtraThrow(YutResult result, bool defaultValue)
    {
        return defaultValue;
    }

    public virtual CharacterCaptureDecision EvaluateIncomingCapture(CharacterCaptureRequest request)
    {
        return TryConsumeGrantedProtection()
            ? CharacterCaptureDecision.Prevent
            : CharacterCaptureDecision.Proceed;
    }

    public virtual void OnPieceEnteredBoard() { }
    public virtual void OnPieceRetired() { }
    public virtual void OnMoveCompleted(CharacterMoveRecord record) { }
    public virtual void OnAnyPieceMoveCompleted(CharacterMoveRecord record) { }
    public virtual void OnCaptureCompleted(CharacterCaptureRequest request) { }

    public virtual void OnOwnerTurnStarted()
    {
        if (remainingPassiveCooldownTurns > 0)
            remainingPassiveCooldownTurns--;

        if (grantedProtectionRemainingOwnerTurns <= 0) return;

        grantedProtectionRemainingOwnerTurns--;
        if (grantedProtectionRemainingOwnerTurns == 0)
            grantedProtectionCharges = 0;
    }

    public virtual void OnOwnerTurnEnded() { }

    public CharacterActiveResult TryUseActive(CharacterActiveRequest request)
    {
        if (!isRegistered)
            return CharacterActiveResult.Failure("Character skill runtime is not registered.");
        if (request.PlayerId != PlayerId || request.CasterPieceId != PieceId)
            return CharacterActiveResult.Failure("Active request does not match this character piece.");
        if (Turns == null)
            return CharacterActiveResult.Failure("Turn manager is not available.");
        if (Turns.CurrentTurn == null || (int)Turns.CurrentTurn.currentPlayer != PlayerId)
            return CharacterActiveResult.Failure("The active skill can be used only during its owner's turn.");
        if (!CanUseActiveDuringPhase(Turns.CurrentTurn.currentPhase))
            return CharacterActiveResult.Failure(
                $"The active skill cannot be used during {Turns.CurrentTurn.currentPhase}.");
        if (!TryGetPiece(out PlayerRuntimeData.PieceRuntimeData caster))
            return CharacterActiveResult.Failure("Caster piece runtime data is missing.");
        if (caster.State == PieceState.Goal)
            return CharacterActiveResult.Failure("A goal piece cannot use an active skill.");

        return ExecuteActive(request, caster);
    }

    public bool IsActiveUsableInCurrentPhase()
    {
        return isRegistered &&
               Turns != null &&
               Turns.CurrentTurn != null &&
               (int)Turns.CurrentTurn.currentPlayer == PlayerId &&
               CanUseActiveDuringPhase(Turns.CurrentTurn.currentPhase);
    }

    /// <summary>
    /// 수호의 부적처럼 다른 캐릭터가 부여하는 일회성 보호에 사용합니다.
    /// remainingOwnerTurns=1이면 보호받는 플레이어의 다음 턴 시작까지 유지됩니다.
    /// </summary>
    public void GrantProtection(int charges, int remainingOwnerTurns)
    {
        if (charges <= 0) throw new ArgumentOutOfRangeException(nameof(charges));
        if (remainingOwnerTurns <= 0) throw new ArgumentOutOfRangeException(nameof(remainingOwnerTurns));

        grantedProtectionCharges = Math.Max(grantedProtectionCharges, charges);
        grantedProtectionRemainingOwnerTurns = Math.Max(
            grantedProtectionRemainingOwnerTurns,
            remainingOwnerTurns);
    }

    protected virtual CharacterActiveResult ExecuteActive(
        CharacterActiveRequest request,
        PlayerRuntimeData.PieceRuntimeData caster)
    {
        return CharacterActiveResult.Failure(
            $"{GetType().Name} has no implemented active skill.");
    }

    protected virtual bool CanUseActiveDuringPhase(TurnPhase phase)
    {
        return phase == TurnPhase.WaitAction;
    }

    protected bool TryStartPassiveCooldown()
    {
        if (!IsPassiveReady) return false;

        remainingPassiveCooldownTurns = PassiveCooldownTurns;
        if (remainingPassiveCooldownTurns > 0)
        {
            Debug.Log(
                $"[CharacterSkill][PassiveCooldown] Started " +
                $"{remainingPassiveCooldownTurns} turn(s). Player={PlayerId}, Piece={PieceId}",
                this);
        }

        return true;
    }

    protected void ResetPassiveCooldown()
    {
        remainingPassiveCooldownTurns = 0;
    }

    protected bool TryGetPiece(out PlayerRuntimeData.PieceRuntimeData piece)
    {
        if (Owner != null && Owner.TryGetPieceData(pieceId, out piece))
            return true;

        piece = null;
        return false;
    }

    protected bool TryGetPiece(
        int playerId,
        int targetPieceId,
        out CharacterPieceReference reference)
    {
        return CharacterBoardUtility.TryGetPiece(
            Players,
            playerId,
            targetPieceId,
            out reference);
    }

    protected int GetStackPieceCount(PlayerRuntimeData.PieceRuntimeData caster)
    {
        if (caster == null || Owner == null || Owner.RuntimeData == null)
            return 1;
        if (!caster.IsStacked)
            return 1;

        int count = 0;
        foreach (PlayerRuntimeData.PieceRuntimeData piece in Owner.RuntimeData.Pieces)
        {
            if (piece.State == PieceState.InBoard &&
                piece.StackGroupId == caster.StackGroupId)
            {
                count++;
            }
        }

        return Mathf.Max(1, count);
    }

    protected void RequestSkillPoint(int amount = 1)
    {
        CharacterSkillRegistry.RequestSkillPoint(PlayerId, amount);
    }

    private bool TryConsumeGrantedProtection()
    {
        if (grantedProtectionCharges <= 0) return false;
        grantedProtectionCharges--;
        return true;
    }

    private void TryRegisterRuntime()
    {
        if (isRegistered) return;

        Owner = GetComponentInParent<PlayerController>(true);
        if (Owner == null || !Owner.IsInitialized || Owner.RuntimeData == null)
            return;

        Players = FindFirstObjectByType<PlayerManager>();
        Movement = FindFirstObjectByType<PieceMovementManager>();
        Turns = FindFirstObjectByType<TestTurnManager>();
        YutRules = FindFirstObjectByType<TestYutRuleManager>();

        try
        {
            pieceId = CharacterSkillRegistry.Register(Owner, pieceId, this);
            CharacterSkillRegistry.EnsureManagerBridges(YutRules, Turns);
            isRegistered = true;
        }
        catch (Exception exception)
        {
            Debug.LogError($"{GetType().Name}: runtime registration failed. {exception.Message}", this);
            enabled = false;
        }
    }

    private void ValidateCharacterData()
    {
        if (characterData == null)
        {
            Debug.LogError(
                $"{GetType().Name}: CharacterData is not assigned. " +
                "Skill status lookup and visual creation will be skipped.",
                this);
        }
    }

    private void EnsureVisualModel()
    {
        if (characterData == null || characterData.visualModelPrefab == null || spawnedVisualModel != null)
            return;

        // 자기 자신을 외형 프리팹으로 지정하면 무한 재귀 생성이 발생하므로 차단합니다.
        if (characterData.visualModelPrefab == gameObject)
        {
            Debug.LogError(
                $"{GetType().Name}: visualModelPrefab cannot reference its owner GameObject.",
                this);
            return;
        }

        spawnedVisualModel = Instantiate(characterData.visualModelPrefab, transform);
        spawnedVisualModel.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
    }
}
