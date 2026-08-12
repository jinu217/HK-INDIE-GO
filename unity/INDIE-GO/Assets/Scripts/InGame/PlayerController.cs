using UnityEngine;
using YutArena.InGame;
using System.Collections.Generic;

/// <summary>
/// 한 PlayerSlot의 진입점이다. 슬롯마다 같은 컴포넌트를 사용하며,
/// Initialize 호출로 각자 별도의 PlayerRuntimeData를 받는다.
/// </summary>
public sealed class PlayerController : MonoBehaviour
{
    [Header("Job Piece Prefab")]
    [Tooltip("This player's selected job prefab. One instance is created for each piece.")]
    [SerializeField] private GameObject jobPiecePrefab;

    public int PlayerId => runtimeData != null ? runtimeData.PlayerId : -1;
    public bool IsInitialized { get; private set; }
    public PlayerRuntimeData RuntimeData => runtimeData;
    public GameObject JobPiecePrefab => jobPiecePrefab;
    public IReadOnlyList<GameObject> SpawnedPieceObjects => spawnedPieceObjects;

    private PlayerRuntimeData runtimeData;
    private readonly List<GameObject> spawnedPieceObjects = new List<GameObject>();

    public void SetJobPiecePrefab(GameObject prefab)
    {
        if (IsInitialized)
            throw new System.InvalidOperationException("Set the job prefab before player initialization.");
        jobPiecePrefab = prefab;
    }

    public void Initialize(int playerId, string playerName, int pieceCount)
    {
        if (IsInitialized)
        {
            Debug.LogWarning($"{name}은(는) 이미 초기화되어 다시 초기화합니다.", this);
        }

        runtimeData = new PlayerRuntimeData(playerId, playerName, pieceCount);
        IsInitialized = true;
        SpawnPieceObjects(pieceCount);
    }

    public bool TryGetPieceData(int pieceId, out PlayerRuntimeData.PieceRuntimeData pieceData)
    {
        if (runtimeData != null)
            return runtimeData.TryGetPiece(pieceId, out pieceData);

        pieceData = null;
        return false;
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
        ClearPieceObjects();
        runtimeData = null;
        IsInitialized = false;
    }

    private void SpawnPieceObjects(int pieceCount)
    {
        ClearPieceObjects();
        if (jobPiecePrefab == null)
        {
            Debug.LogError($"{name}: no gameplay prefab is assigned.", this);
            return;
        }

        for (int pieceId = 0; pieceId < pieceCount; pieceId++)
        {
            GameObject instance = Instantiate(jobPiecePrefab, transform);
            instance.name = $"{jobPiecePrefab.name}_Piece_{pieceId + 1}";
            instance.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            spawnedPieceObjects.Add(instance);
        }
    }

    private void ClearPieceObjects()
    {
        foreach (GameObject instance in spawnedPieceObjects)
        {
            if (instance == null) continue;
            instance.SetActive(false);
            Destroy(instance);
        }
        spawnedPieceObjects.Clear();
    }
}
