using System;
using System.Collections.Generic;
using UnityEngine;
using YutArena.Common;

namespace YutArena.InGame
{
    public enum BoardSkillRuntimeValueType
    {
        String,
        Integer,
        Float,
        Boolean
    }

    /// <summary>스킬마다 필요한 추가 런타임 값을 직렬화 가능한 형태로 보관합니다.</summary>
    [Serializable]
    public sealed class BoardSkillRuntimeValue
    {
        public string key;
        public BoardSkillRuntimeValueType valueType;
        public string stringValue;
        public int intValue;
        public float floatValue;
        public bool boolValue;

        public BoardSkillRuntimeValue Clone()
        {
            return new BoardSkillRuntimeValue
            {
                key = key,
                valueType = valueType,
                stringValue = stringValue,
                intValue = intValue,
                floatValue = floatValue,
                boolValue = boolValue
            };
        }
    }

    /// <summary>
    /// 외부 스킬이 보드에 정보를 설치할 때 전달하는 전체 데이터입니다.
    /// visualPrefab이 없으면 논리 데이터만 설치됩니다.
    /// </summary>
    [Serializable]
    public sealed class BoardSkillInstallRequest
    {
        public BoardTileId tileId;
        public string skillId;
        [Tooltip("CharacterData.char_ID 값입니다. 캐릭터와 무관한 설치 정보라면 -1을 사용합니다.")]
        public int characterId = -1;
        public int ownerPlayerId = -1;
        public int ownerPieceId = -1;
        public TeamSlot ownerTeam = TeamSlot.None;

        [Tooltip("-1이면 턴 제한이 없습니다.")]
        public int remainingTurns = -1;
        [Tooltip("-1이면 발동 횟수 제한이 없습니다.")]
        public int remainingTriggers = -1;
        [Min(1)] public int stackCount = 1;

        [Tooltip("스킬별로 추가 저장할 값입니다. 이 매니저는 값을 해석하거나 효과를 실행하지 않습니다.")]
        public List<BoardSkillRuntimeValue> runtimeValues = new List<BoardSkillRuntimeValue>();

        [Header("Optional 3D Visual")]
        public GameObject visualPrefab;
        public Vector3 visualLocalPosition;
        public Vector3 visualLocalEulerAngles;
        public Vector3 visualLocalScale = Vector3.one;
    }

    /// <summary>게임 한 판 동안 보드에 실제로 설치되어 있는 스킬 정보입니다.</summary>
    [Serializable]
    public sealed class InstalledBoardSkill
    {
        [SerializeField] private int installationId;
        [SerializeField] private BoardTileId tileId;
        [SerializeField] private string skillId;
        [SerializeField] private int characterId;
        [SerializeField] private int ownerPlayerId;
        [SerializeField] private int ownerPieceId;
        [SerializeField] private TeamSlot ownerTeam;
        [SerializeField] private int remainingTurns;
        [SerializeField] private int remainingTriggers;
        [SerializeField] private int stackCount;
        [SerializeField] private List<BoardSkillRuntimeValue> runtimeValues;
        [SerializeField] private GameObject visualInstance;

        public int InstallationId => installationId;
        public BoardTileId TileId => tileId;
        public string SkillId => skillId;
        public int CharacterId => characterId;
        public int OwnerPlayerId => ownerPlayerId;
        public int OwnerPieceId => ownerPieceId;
        public TeamSlot OwnerTeam => ownerTeam;
        public int RemainingTurns => remainingTurns;
        public int RemainingTriggers => remainingTriggers;
        public int StackCount => stackCount;
        public IReadOnlyList<BoardSkillRuntimeValue> RuntimeValues => runtimeValues;
        public GameObject VisualInstance => visualInstance;

        internal InstalledBoardSkill(int id, BoardSkillInstallRequest request)
        {
            installationId = id;
            tileId = request.tileId;
            skillId = request.skillId;
            characterId = request.characterId;
            ownerPlayerId = request.ownerPlayerId;
            ownerPieceId = request.ownerPieceId;
            ownerTeam = request.ownerTeam;
            remainingTurns = request.remainingTurns;
            remainingTriggers = request.remainingTriggers;
            stackCount = Mathf.Max(1, request.stackCount);
            runtimeValues = new List<BoardSkillRuntimeValue>();

            if (request.runtimeValues == null)
                return;

            foreach (BoardSkillRuntimeValue value in request.runtimeValues)
            {
                if (value != null)
                    runtimeValues.Add(value.Clone());
            }
        }

        public bool TryGetRuntimeValue(string key, out BoardSkillRuntimeValue value)
        {
            if (!string.IsNullOrWhiteSpace(key))
            {
                foreach (BoardSkillRuntimeValue candidate in runtimeValues)
                {
                    if (candidate != null && candidate.key == key)
                    {
                        value = candidate;
                        return true;
                    }
                }
            }

            value = null;
            return false;
        }

        internal void SetVisualInstance(GameObject value) => visualInstance = value;
        internal void SetRemainingTurns(int value) => remainingTurns = value;
        internal void SetRemainingTriggers(int value) => remainingTriggers = value;
        internal void SetStackCount(int value) => stackCount = Mathf.Max(1, value);
    }

    /// <summary>
    /// 게임 시작부터 종료까지 보드에 설치된 스킬 정보를 보관합니다.
    /// 스킬 효과는 실행하지 않으며, 외부 스킬 코드가 등록/조회/수정/제거 API를 사용합니다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MapManager))]
    public sealed class BoardTileRuntimeStateManager : MonoBehaviour
    {
        [SerializeField] private MapManager mapManager;
        [SerializeField] private List<InstalledBoardSkill> installedSkills = new List<InstalledBoardSkill>();

        private readonly Dictionary<BoardTileId, List<InstalledBoardSkill>> skillsByTile =
            new Dictionary<BoardTileId, List<InstalledBoardSkill>>();
        private int nextInstallationId = 1;

        public IReadOnlyList<InstalledBoardSkill> InstalledSkills => installedSkills;

        public event Action<InstalledBoardSkill> SkillInstalled;
        public event Action<InstalledBoardSkill> SkillRemoved;
        public event Action<InstalledBoardSkill> SkillChanged;

        private void Awake()
        {
            if (mapManager == null)
                mapManager = GetComponent<MapManager>();

            RebuildLookup();
        }

        public bool TryInstall(BoardSkillInstallRequest request, out InstalledBoardSkill installed)
        {
            installed = null;
            if (!ValidateRequest(request))
                return false;

            installed = new InstalledBoardSkill(nextInstallationId++, request);
            installedSkills.Add(installed);

            if (!skillsByTile.TryGetValue(request.tileId, out List<InstalledBoardSkill> tileSkills))
            {
                tileSkills = new List<InstalledBoardSkill>();
                skillsByTile.Add(request.tileId, tileSkills);
            }
            tileSkills.Add(installed);

            if (request.visualPrefab != null)
                CreateVisual(installed, request);

            SkillInstalled?.Invoke(installed);
            return true;
        }

        public bool TryGetInstallation(int installationId, out InstalledBoardSkill installed)
        {
            installed = installedSkills.Find(skill =>
                skill != null && skill.InstallationId == installationId);
            return installed != null;
        }

        public IReadOnlyList<InstalledBoardSkill> GetSkills(BoardTileId tileId)
        {
            return skillsByTile.TryGetValue(tileId, out List<InstalledBoardSkill> skills)
                ? skills.ToArray()
                : Array.Empty<InstalledBoardSkill>();
        }

        /// <summary>
        /// 지정하지 않은 조건은 무시하고 일치하는 설치 정보 전부를 반환합니다.
        /// 문자열 null/빈 값, ID -1, TeamSlot.None은 해당 조건을 사용하지 않는다는 뜻입니다.
        /// </summary>
        public IReadOnlyList<InstalledBoardSkill> FindSkills(
            BoardTileId tileId,
            string skillId = null,
            int characterId = -1,
            int ownerPlayerId = -1,
            int ownerPieceId = -1,
            TeamSlot ownerTeam = TeamSlot.None)
        {
            if (!skillsByTile.TryGetValue(tileId, out List<InstalledBoardSkill> tileSkills))
                return Array.Empty<InstalledBoardSkill>();

            var result = new List<InstalledBoardSkill>();
            foreach (InstalledBoardSkill installed in tileSkills)
            {
                if (installed == null)
                    continue;
                if (!string.IsNullOrWhiteSpace(skillId) && installed.SkillId != skillId)
                    continue;
                if (characterId >= 0 && installed.CharacterId != characterId)
                    continue;
                if (ownerPlayerId >= 0 && installed.OwnerPlayerId != ownerPlayerId)
                    continue;
                if (ownerPieceId >= 0 && installed.OwnerPieceId != ownerPieceId)
                    continue;
                if (ownerTeam != TeamSlot.None && installed.OwnerTeam != ownerTeam)
                    continue;

                result.Add(installed);
            }

            return result;
        }

        public bool HasSkill(BoardTileId tileId, string skillId)
        {
            return FindSkill(tileId, skill => skill.SkillId == skillId) != null;
        }

        public bool HasSkillOwnedByPlayer(BoardTileId tileId, string skillId, int ownerPlayerId)
        {
            return FindSkill(tileId, skill =>
                skill.SkillId == skillId && skill.OwnerPlayerId == ownerPlayerId) != null;
        }

        public bool HasSkillFromCharacter(BoardTileId tileId, string skillId, int characterId)
        {
            return FindSkill(tileId, skill =>
                skill.SkillId == skillId && skill.CharacterId == characterId) != null;
        }

        public bool HasSkillOwnedByPiece(
            BoardTileId tileId,
            string skillId,
            int ownerPlayerId,
            int ownerPieceId)
        {
            return FindSkill(tileId, skill =>
                skill.SkillId == skillId &&
                skill.OwnerPlayerId == ownerPlayerId &&
                skill.OwnerPieceId == ownerPieceId) != null;
        }

        public bool HasSkillFromTeam(BoardTileId tileId, string skillId, TeamSlot ownerTeam)
        {
            return FindSkill(tileId, skill =>
                skill.SkillId == skillId && skill.OwnerTeam == ownerTeam) != null;
        }

        public bool TryFindSkill(
            BoardTileId tileId,
            string skillId,
            out InstalledBoardSkill installed)
        {
            installed = FindSkill(tileId, skill => skill.SkillId == skillId);
            return installed != null;
        }

        public bool TrySetRemainingTurns(int installationId, int remainingTurns)
        {
            if (remainingTurns < -1 || !TryGetInstallation(installationId, out InstalledBoardSkill installed))
                return false;

            installed.SetRemainingTurns(remainingTurns);
            SkillChanged?.Invoke(installed);
            return true;
        }

        public bool TrySetRemainingTriggers(int installationId, int remainingTriggers)
        {
            if (remainingTriggers < -1 || !TryGetInstallation(installationId, out InstalledBoardSkill installed))
                return false;

            installed.SetRemainingTriggers(remainingTriggers);
            SkillChanged?.Invoke(installed);
            return true;
        }

        public bool TrySetStackCount(int installationId, int stackCount)
        {
            if (stackCount < 1 || !TryGetInstallation(installationId, out InstalledBoardSkill installed))
                return false;

            installed.SetStackCount(stackCount);
            SkillChanged?.Invoke(installed);
            return true;
        }

        public bool RemoveInstallation(int installationId)
        {
            if (!TryGetInstallation(installationId, out InstalledBoardSkill installed))
                return false;

            RemoveInternal(installed);
            return true;
        }

        public int RemoveSkills(BoardTileId tileId, string skillId = null, int ownerPlayerId = -1)
        {
            if (!skillsByTile.TryGetValue(tileId, out List<InstalledBoardSkill> tileSkills))
                return 0;

            var targets = new List<InstalledBoardSkill>();
            foreach (InstalledBoardSkill installed in tileSkills)
            {
                if (installed == null)
                    continue;
                if (!string.IsNullOrWhiteSpace(skillId) && installed.SkillId != skillId)
                    continue;
                if (ownerPlayerId >= 0 && installed.OwnerPlayerId != ownerPlayerId)
                    continue;

                targets.Add(installed);
            }

            foreach (InstalledBoardSkill target in targets)
                RemoveInternal(target);

            return targets.Count;
        }

        public void ClearAll()
        {
            var targets = new List<InstalledBoardSkill>(installedSkills);
            foreach (InstalledBoardSkill target in targets)
            {
                if (target != null)
                    RemoveInternal(target);
            }

            installedSkills.Clear();
            skillsByTile.Clear();
            nextInstallationId = 1;
        }

        private InstalledBoardSkill FindSkill(
            BoardTileId tileId,
            Predicate<InstalledBoardSkill> predicate)
        {
            if (!skillsByTile.TryGetValue(tileId, out List<InstalledBoardSkill> skills))
                return null;

            return skills.Find(skill => skill != null && predicate(skill));
        }

        private bool ValidateRequest(BoardSkillInstallRequest request)
        {
            if (request == null)
            {
                Debug.LogError("Board skill install request is null.", this);
                return false;
            }
            if (request.tileId == BoardTileId.None || request.tileId == BoardTileId.Goal)
            {
                Debug.LogError($"Board skill cannot be installed on {request.tileId}.", this);
                return false;
            }
            if (string.IsNullOrWhiteSpace(request.skillId))
            {
                Debug.LogError("Board skill requires a non-empty skillId.", this);
                return false;
            }
            if (request.remainingTurns < -1 || request.remainingTriggers < -1 || request.stackCount < 1)
            {
                Debug.LogError("Board skill counters contain an invalid value.", this);
                return false;
            }

            return true;
        }

        private void CreateVisual(InstalledBoardSkill installed, BoardSkillInstallRequest request)
        {
            if (mapManager == null ||
                !mapManager.TryGetTileAnchor(request.tileId, out Transform tileAnchor))
            {
                Debug.LogWarning(
                    $"{request.tileId} Anchor를 찾지 못해 {request.skillId}의 논리 정보만 설치했습니다.",
                    this);
                return;
            }

            Transform visualRoot = tileAnchor.Find("InstalledSkillVisuals");
            if (visualRoot == null)
            {
                var rootObject = new GameObject("InstalledSkillVisuals");
                visualRoot = rootObject.transform;
                visualRoot.SetParent(tileAnchor, false);
            }

            GameObject instance = Instantiate(request.visualPrefab, visualRoot);
            instance.name = $"{request.skillId}_{installed.InstallationId}";
            instance.transform.localPosition = request.visualLocalPosition;
            instance.transform.localRotation = Quaternion.Euler(request.visualLocalEulerAngles);
            instance.transform.localScale = request.visualLocalScale;
            installed.SetVisualInstance(instance);
        }

        private void RemoveInternal(InstalledBoardSkill installed)
        {
            installedSkills.Remove(installed);
            if (skillsByTile.TryGetValue(installed.TileId, out List<InstalledBoardSkill> tileSkills))
            {
                tileSkills.Remove(installed);
                if (tileSkills.Count == 0)
                    skillsByTile.Remove(installed.TileId);
            }

            if (installed.VisualInstance != null)
                Destroy(installed.VisualInstance);

            SkillRemoved?.Invoke(installed);
        }

        private void RebuildLookup()
        {
            skillsByTile.Clear();
            int greatestId = 0;

            foreach (InstalledBoardSkill installed in installedSkills)
            {
                if (installed == null)
                    continue;

                if (!skillsByTile.TryGetValue(installed.TileId, out List<InstalledBoardSkill> tileSkills))
                {
                    tileSkills = new List<InstalledBoardSkill>();
                    skillsByTile.Add(installed.TileId, tileSkills);
                }

                tileSkills.Add(installed);
                greatestId = Mathf.Max(greatestId, installed.InstallationId);
            }

            nextInstallationId = greatestId + 1;
        }
    }
}
