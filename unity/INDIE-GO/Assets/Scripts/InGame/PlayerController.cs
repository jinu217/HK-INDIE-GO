using UnityEngine;
using YutArena.InGame;

/// <summary>
/// 한 PlayerSlot의 진입점이다. 슬롯마다 같은 컴포넌트를 사용하며,
/// Initialize 호출로 각자 별도의 PlayerRuntimeData를 받는다.
/// </summary>
public sealed class PlayerController : MonoBehaviour
{
    [Header("Job Piece Prefab")]
    [Tooltip("This player's selected job prefab. One instance is created for each piece.")]
    [SerializeField] private GameObject jobPiecePrefab;

    [Header("Runtime Champion (Debug)")]
    [Tooltip("ChampionPickScene에서 선택되어 이 PlayerSlot에 적용된 챔피언입니다.")]
    [SerializeField] private CharacterData selectedCharacter;

    public int PlayerId => runtimeData != null ? runtimeData.PlayerId : -1;
    public bool IsInitialized { get; private set; }
    public PlayerRuntimeData RuntimeData => runtimeData;
    public GameObject JobPiecePrefab => jobPiecePrefab;
    public CharacterData SelectedCharacter => selectedCharacter;

    private PlayerRuntimeData runtimeData;
    private GameObject defaultJobPiecePrefab;
    private bool hasCachedDefaultJobPiecePrefab;

    private void Awake()
    {
        defaultJobPiecePrefab = jobPiecePrefab;
        hasCachedDefaultJobPiecePrefab = true;
    }

    public void Initialize(int playerId, string playerName, int pieceCount)
    {
        if (IsInitialized)
        {
            Debug.LogWarning($"{name}은(는) 이미 초기화되어 다시 초기화합니다.", this);
        }

        runtimeData = new PlayerRuntimeData(playerId, playerName, pieceCount);
        IsInitialized = true;
    }

    public bool TryGetPieceData(int pieceId, out PlayerRuntimeData.PieceRuntimeData pieceData)
    {
        if (runtimeData != null)
            return runtimeData.TryGetPiece(pieceId, out pieceData);

        pieceData = null;
        return false;
    }

    /// <summary>
    /// ChampionPickScene에서 선택된 챔피언을 이 플레이어 슬롯에 반영합니다.
    /// 피스 생성기는 SelectedCharacter의 piecePrefab을 우선 사용합니다.
    /// </summary>
    public void SetSelectedCharacter(CharacterData characterData)
    {
        selectedCharacter = characterData;
        jobPiecePrefab = characterData != null ? characterData.piecePrefab : defaultJobPiecePrefab;

        Debug.Log(
            $"{name}: 선택 챔피언 '{(characterData != null ? characterData.char_Name : "없음")}'의 Job Piece Prefab을 " +
            $"'{(jobPiecePrefab != null ? jobPiecePrefab.name : "없음")}'으로 적용했습니다.",
            this);
    }

    /// <summary>
    /// 턴/스킬 시스템이 말에 CC를 부여하거나 제거할 때 사용하는 진입점.
    /// </summary>
    public bool TrySetPieceCc(int pieceId, CcDefine ccType, int remainingTurns = 0)
    {
        if (!TryGetPieceData(pieceId, out PlayerRuntimeData.PieceRuntimeData pieceData))
            return false;

        pieceData.SetCc(ccType, remainingTurns);
        return true;
    }

    /// <summary>
    /// 슬롯을 재사용하거나 게임을 다시 시작하기 전 PlayerManager가 호출한다.
    /// 이후 말 오브젝트, 스킬, UI 초기화도 이 지점에 연결한다.
    /// </summary>
    public void ResetPlayer()
    {
        runtimeData = null;
        IsInitialized = false;
        selectedCharacter = null;

        if (hasCachedDefaultJobPiecePrefab)
        {
            jobPiecePrefab = defaultJobPiecePrefab;
        }
    }
}
