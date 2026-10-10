using System;
using System.Collections.Generic;
using UnityEngine;
using YutArena.Common;
using YutArena.Managers;

namespace YutArena.InGame
{
    // 테이블 ID를 사용하고 기존 직렬화 숫자/외부 잡기 계약은 유지합니다.
    // ()_Move_plus(n)은 Move_plus + amount로 표현합니다. 미사용 번호는 재사용하지 않습니다.
    public enum CcDefine
    {
        None = 0,
        Sturn = 1, // 표의 ID 철자 그대로
        Silence = 2, // 확장 ID: 기존 침묵/면역 계약
        Retire = 3,
        Capture = 4,
        Kill = Capture, // 외부 SetCaptured/턴 매니저 호환 이름
        protect = 5,
        Stealth = 6,
        Stack_fake = 7,
        Robot_part = 8,
        Parts = Robot_part, // 외부 부품 조회 호환 이름
        Move_scale = 9, // 확장 ID: 특정 윷 결과의 이동 배율
        MOorDO = 11,
        No_back = 12,
        Get_throw = 13,
        Get_point = 15,
        Move_plus = 16,
        Peace_move = 17,
        WindPath = 19, // 확장 ID: 표에 ID가 없는 바람길 설치 정보
        Binding = 23,
        Charm = 24,
        Move_end = 25
    }
}

namespace YutArena.InGame
{
    [Serializable]
    public sealed class CcState
    {
        [SerializeField] private CcDefine type;
        [SerializeField] private int remainingOwnerTurns;
        [SerializeField] private int value;
        [SerializeField] private int charges;
        [SerializeField] private int sourcePlayerId;
        [SerializeField] private int sourcePieceId;
        [SerializeField] private BoardTileId tile;
        [SerializeField] private List<BoardTileId> path;
        public CcDefine Type => type;
        public string SkillId => CcEffectService.GetSkillId(type);
        // 0: 소비/명시적 해제까지 유지. 양수: 대상 소유자의 턴 시작마다 감소.
        public int RemainingOwnerTurns => remainingOwnerTurns;
        public int Value => value;
        public int Charges => charges; // -1=기간 동안 무제한, 양수=남은 방어 횟수
        public int SourcePlayerId => sourcePlayerId;
        public int SourcePieceId => sourcePieceId;
        public BoardTileId Tile => tile;
        public IReadOnlyList<BoardTileId> Path => path;
        internal CcState(CcDefine type, int turns, int value, int sourcePlayerId,
            int sourcePieceId, BoardTileId tile, IReadOnlyList<BoardTileId> path, int charges)
        {
            this.type = type;
            remainingOwnerTurns = turns;
            this.value = value;
            this.charges = charges;
            this.sourcePlayerId = sourcePlayerId;
            this.sourcePieceId = sourcePieceId;
            this.tile = tile;
            this.path = path == null ? new List<BoardTileId>() : new List<BoardTileId>(path);
        }
        internal void Tick() { if (remainingOwnerTurns > 0) remainingOwnerTurns--; }
        internal void SetValue(int amount) { value = amount; }
        internal void ConsumeCharge() { if (charges > 0) charges--; }
    }

    // PlayerManager -> PlayerController.RuntimeData.Pieces[n].Cc에 실제 상태를 저장합니다.
    [Serializable]
    public sealed class PieceCcCollection
    {
        [SerializeField] private List<CcState> effects = new List<CcState>();
        public IReadOnlyList<CcState> Effects => effects.AsReadOnly();
        public CcState Get(CcDefine type) => effects.Find(effect => effect.Type == type);
        public bool Has(CcDefine type) => Get(type) != null;
        internal void Add(CcState state) { effects.Add(state); }
        internal void Remove(CcState state) { effects.Remove(state); }
        internal void Clear() { effects.Clear(); }
        // 종전 단일 CC 조회 API를 유지하되 복합 CC 판정은 Has/Effects를 사용합니다.
        public CcDefine Primary
        {
            get
            {
                foreach (var type in new[] { CcDefine.Kill, CcDefine.Retire, CcDefine.Sturn,
                    CcDefine.Binding, CcDefine.Silence, CcDefine.Robot_part })
                    if (Has(type)) return type;
                return effects.Count == 0 ? CcDefine.None : effects[0].Type;
            }
        }
    }
}

namespace YutArena.InGame
{
    /// <summary>
    /// CC 상태의 유일한 쓰기/실행 진입점. 스킬/아이템 모두 이 API를 이용합니다.
    /// 캐릭터 종류는 알지 않으며 CcDefine에 대응하는 효과만 처리합니다.
    /// </summary>
    public static class CcEffectService
    {
        // 즉시 효과도 Added -> 실행 -> Removed 순서로 알려 결과 UI/VFX가 구독할 수 있습니다.
        public static event Action<PlayerRuntimeData.PieceRuntimeData, CcState, bool> Changed;
        public static string GetSkillId(CcDefine type) => type == CcDefine.Capture ? nameof(CcDefine.Capture) :
            type == CcDefine.Robot_part ? nameof(CcDefine.Robot_part) : type.ToString();
        private static int windMoveDepth;
        internal static bool IsResolvingWindMove => windMoveDepth > 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntime() { Changed = null; windMoveDepth = 0; }

        public static bool CanMove(PlayerRuntimeData.PieceRuntimeData piece) =>
            piece != null && piece.State != PieceState.Goal &&
            (!piece.IsStacked || piece.StackLeaderPieceId == piece.PieceId) &&
            !piece.Cc.Has(CcDefine.Sturn) && !piece.Cc.Has(CcDefine.Binding) &&
            !piece.Cc.Has(CcDefine.Robot_part);

        public static bool CanUseSkill(PlayerRuntimeData.PieceRuntimeData piece) =>
            piece != null && piece.State != PieceState.Goal &&
            !piece.Cc.Has(CcDefine.Sturn) && !piece.Cc.Has(CcDefine.Silence) &&
            !piece.Cc.Has(CcDefine.Robot_part) &&
            (!piece.IsStacked || piece.StackLeaderPieceId == piece.PieceId);

        internal static PlayerRuntimeData.PieceRuntimeData StackLeader(
            PlayerRuntimeData.PieceRuntimeData piece)
        {
            if (piece != null && piece.IsStacked)
            {
                var owner = FindOwner(piece);
                if (owner != null && owner.TryGetPieceData(piece.StackLeaderPieceId, out var leader))
                    return leader;
            }
            return piece;
        }

        public static bool IsTargetable(PlayerRuntimeData.PieceRuntimeData piece)
        {
            piece = StackLeader(piece);
            return piece != null && !piece.Cc.Has(CcDefine.Stealth) && !piece.Cc.Has(CcDefine.Robot_part);
        }

        public static bool Apply(PlayerRuntimeData.PieceRuntimeData piece, CcDefine type,
            int turns = 0, int value = 1, int sourcePlayerId = -1, int sourcePieceId = -1,
            IReadOnlyList<BoardTileId> path = null, bool isSimpleMove = true,
            int charges = -1, bool ignoresInstalledItems = false)
        {
            if (piece == null) return false;
            if (!Enum.IsDefined(typeof(CcDefine), type)) throw new ArgumentOutOfRangeException(nameof(type));
            if (turns < 0) throw new ArgumentOutOfRangeException(nameof(turns));
            if (type != CcDefine.None && type != CcDefine.Move_plus && value <= 0)
                throw new ArgumentOutOfRangeException(nameof(value));
            if (charges < -1 || charges == 0) throw new ArgumentOutOfRangeException(nameof(charges));
            if (type == CcDefine.None) { Clear(piece); return true; }

            PlayerController owner = FindOwner(piece);
            int effectivePieceId = piece.IsStacked ? piece.StackLeaderPieceId : piece.PieceId;
            if (owner != null &&
                CharacterSkillRegistry.TryGet(owner.PlayerId, effectivePieceId, out var status) &&
                status.IsImmuneToEffect(type))
                return false;

            // 보호는 잡기뿐 아니라 직접 부여되는 해로운 상태이상도 막습니다.
            if ((type == CcDefine.Sturn || type == CcDefine.Binding ||
                 type == CcDefine.Silence ||
                 type == CcDefine.Retire || type == CcDefine.Kill ||
                 type == CcDefine.Robot_part) && ConsumeGuard(piece, CcDefine.protect))
                return false;

            if (type == CcDefine.Retire && owner != null &&
                CharacterSkillRegistry.TryGet(owner.PlayerId, piece.PieceId, out var targetStatus) &&
                targetStatus.TryReplaceRetire(piece))
                return true;

            bool needsOwner = type == CcDefine.Stack_fake || type == CcDefine.Get_throw ||
                type == CcDefine.Get_point || type == CcDefine.Move_plus || IsPathMove(type);
            if (needsOwner && owner == null) return false;
            if (type == CcDefine.Stack_fake &&
                (piece.State != PieceState.InBoard || CcBoardEffects.CountStackUnits(owner, piece) + value > 4))
                return false;
            TestTurnManager turnsManager = null;
            PieceMovementManager movement = null;
            if (type == CcDefine.Get_throw)
            {
                turnsManager = UnityEngine.Object.FindFirstObjectByType<TestTurnManager>();
                if (turnsManager == null || turnsManager.CurrentTurn == null ||
                    (int)turnsManager.CurrentTurn.currentPlayer != owner.PlayerId ||
                    turnsManager.CurrentTurn.currentPhase != TurnPhase.WaitAction) return false;
            }
            if (type == CcDefine.Move_plus || IsPathMove(type))
            {
                if (!CanMove(piece) || value == 0) return false;
                if (IsPathMove(type) && (path == null || path.Count == 0)) return false;
                movement = UnityEngine.Object.FindFirstObjectByType<PieceMovementManager>();
                if (type == CcDefine.Move_plus && movement == null) return false;
            }
            if (type == CcDefine.Kill || type == CcDefine.Retire)
            {
                int oldGroup = piece.StackGroupId;
                Clear(piece);
                piece.ApplyCapturedPosition();
                CcBoardEffects.NormalizeStack(owner, oldGroup);
            }
            else
            {
                CcState previous = piece.Cc.Get(type);
                // 보호는 출처/횟수 유형별로 따로 유지합니다. 기간 보호와 일회성 보호가 덮어쓰지 않습니다.
                if (type == CcDefine.protect || type == CcDefine.Charm)
                    previous = new List<CcState>(piece.Cc.Effects).Find(effect =>
                        effect.Type == type && effect.SourcePlayerId == sourcePlayerId &&
                        effect.SourcePieceId == sourcePieceId && (effect.Charges < 0) == (charges < 0));
                if (previous != null)
                {
                    if (type == CcDefine.Stack_fake) value += previous.Value;
                    else
                    {
                        turns = previous.RemainingOwnerTurns == 0 || turns == 0
                            ? 0 : Math.Max(turns, previous.RemainingOwnerTurns);
                        value = Math.Max(value, previous.Value);
                        if (charges > 0) charges = Math.Max(charges, previous.Charges);
                    }
                    Remove(piece, previous);
                }
            }

            var state = new CcState(type, turns, value, sourcePlayerId, sourcePieceId,
                piece.CurrentTileId, path, charges);
            piece.Cc.Add(state);
            Changed?.Invoke(piece, state, true);
            Debug.Log($"[CC][Apply] Player={owner?.PlayerId}, Piece={piece.PieceId}, " +
                $"Effect={GetSkillId(type)}, Turns={turns}, Value={value}, Charges={charges}, " +
                $"Source={sourcePlayerId}/{sourcePieceId}");
            bool succeeded = true;
            switch (type)
            {
                case CcDefine.Kill:
                case CcDefine.Retire:
                    if (owner != null) CharacterSkillRegistry.NotifyPieceRetired(owner.PlayerId, piece.PieceId);
                    break;
                case CcDefine.Robot_part:
                    int oldGroup = piece.StackGroupId;
                    piece.ClearStack();
                    CcBoardEffects.NormalizeStack(owner, oldGroup);
                    break;
                case CcDefine.Stack_fake: CcBoardEffects.RefreshClones(owner, piece); break;
                case CcDefine.Get_throw:
                    for (int i = 0; i < value; i++) turnsManager.GrantSkillExtraThrow();
                    break;
                case CcDefine.Get_point: CharacterSkillRegistry.RequestSkillPoint(owner.PlayerId, value); break;
                case CcDefine.Move_plus: succeeded = movement.TryMovePiece(owner.PlayerId, piece.PieceId, value, true); break;
                case CcDefine.Peace_move:
                case CcDefine.Move_end:
                    CcBoardEffects.MoveStackAlongPath(owner, piece, path, ignoresInstalledItems, isSimpleMove); break;
            }
            if (type == CcDefine.Get_throw || type == CcDefine.Get_point ||
                type == CcDefine.Move_plus || IsPathMove(type)) Remove(piece, state);
            return succeeded;
        }

        public static void Remove(PlayerRuntimeData.PieceRuntimeData piece, CcDefine type)
        {
            foreach (CcState state in Snapshot(piece))
                if (state.Type == type) Remove(piece, state);
        }

        private static void Remove(PlayerRuntimeData.PieceRuntimeData piece, CcState state)
        {
            if (piece == null || !Contains(piece, state)) return;
            piece.Cc.Remove(state);
            if (state.Type == CcDefine.Stack_fake) CcBoardEffects.RefreshClones(FindOwner(piece), piece);
            Changed?.Invoke(piece, state, false);
        }

        public static void Clear(PlayerRuntimeData.PieceRuntimeData piece)
        {
            if (piece == null) return;
            foreach (CcState state in Snapshot(piece)) Remove(piece, state);
        }

        public static int ResolveMove(PlayerRuntimeData.PieceRuntimeData piece, CharacterMoveRequest request)
        {
            if (!CanMove(piece)) return 0;
            int count = request.MoveCount;
            if (request.IsActiveSkillMove) return count;
            CcState scale = piece.Cc.Get(CcDefine.Move_scale);
            if (scale != null)
            {
                count *= scale.Value;
                Remove(piece, scale);
            }
            return count;
        }

        public static CharacterCaptureDecision ResolveCapture(PlayerRuntimeData.PieceRuntimeData piece,
            CharacterCaptureRequest request)
        {
            if (piece == null) return CharacterCaptureDecision.Proceed;
            if (!IsTargetable(piece)) return CharacterCaptureDecision.Prevent;
            var guardPiece = piece;
            if (ConsumeGuard(guardPiece, CcDefine.protect) || ConsumeGuard(guardPiece, CcDefine.Charm))
                return CharacterCaptureDecision.Prevent;
            CcState clone = piece.Cc.Get(CcDefine.Stack_fake);
            if (clone != null)
            {
                ConsumeClones(piece, 1);
                return CharacterCaptureDecision.ConsumeCloneWithoutBonus;
            }
            return CharacterCaptureDecision.Proceed;
        }

        internal static void ConsumeClones(PlayerRuntimeData.PieceRuntimeData piece, int count)
        {
            CcState clone = piece?.Cc.Get(CcDefine.Stack_fake);
            if (clone == null || count <= 0) return;
            clone.SetValue(Math.Max(0, clone.Value - count));
            if (clone.Value == 0) Remove(piece, clone);
            else CcBoardEffects.RefreshClones(FindOwner(piece), piece);
        }

        public static (YutResult, float)[] ResolveProbability(int playerId, (YutResult, float)[] table)
        {
            if (table == null || table.Length == 0) table = CharacterSkillRegistry.DefaultYutProbabilityTable;
            foreach (var piece in PlayerPieces(playerId))
            {
                if (piece.Cc.Has(CcDefine.MOorDO))
                {
                    Remove(piece, CcDefine.MOorDO);
                    return new[] { (YutResult.Do, 50f), (YutResult.Mo, 50f) };
                }
                if (!piece.Cc.Has(CcDefine.No_back)) continue;
                Remove(piece, CcDefine.No_back);
                var result = new List<(YutResult, float)>();
                float weight = 0;
                foreach (var entry in table)
                    if (entry.Item1 == YutResult.BackDo) weight += entry.Item2;
                    else result.Add(entry);
                AddWeight(result, YutResult.Yut, weight / 2);
                AddWeight(result, YutResult.Mo, weight / 2);
                return result.ToArray();
            }
            return table;
        }

        public static void TickOwnerTurn(PlayerController owner)
        {
            if (owner == null || owner.RuntimeData == null) return;
            foreach (var piece in owner.RuntimeData.Pieces)
            {
                bool retireExpiredParts = false;
                foreach (CcState state in Snapshot(piece))
                {
                    if (state.RemainingOwnerTurns == 0) continue;
                    state.Tick();
                    if (state.RemainingOwnerTurns > 0) continue;
                    if (state.Type == CcDefine.Robot_part)
                        retireExpiredParts = true;
                    Remove(piece, state);
                }

                // 같은 턴에 만료되는 보호를 먼저 제거하고 부품 퇴장을 판정합니다.
                if (retireExpiredParts)
                    CcBoardEffects.Retire(new CharacterPieceReference(owner, piece), false);
            }
        }

        public static void EndOwnerTurn(int playerId)
        {
            foreach (var piece in PlayerPieces(playerId))
            {
                Remove(piece, CcDefine.Move_scale);
                Remove(piece, CcDefine.MOorDO);
            }
        }

        public static bool ConsumeCapture(PlayerRuntimeData.PieceRuntimeData piece)
        {
            bool kill = piece.Cc.Has(CcDefine.Kill);
            Remove(piece, CcDefine.Kill);
            Remove(piece, CcDefine.Retire);
            return kill;
        }

        public static int CountFinishedPieces(int playerId)
        {
            int count = 0;
            foreach (var piece in PlayerPieces(playerId)) if (piece.IsFinished) count++;
            return count;
        }

        public static void ResolveActiveResult(CharacterActiveRequest request,
            CharacterActiveResult result, int finishedBefore)
        {
            var turns = UnityEngine.Object.FindFirstObjectByType<TestTurnManager>();
            if (turns == null || turns.CurrentTurn == null) return;
            turns.ResolveSkillResult(result.SuppressExtraThrow);
            int newlyFinished = CountFinishedPieces(request.PlayerId) - finishedBefore;
            if (newlyFinished > 0 && turns.Settings != null)
            {
                // 스킬 이동으로 완주한 말도 기존 승리 판정 API에 결과만 전달합니다.
                var wins = UnityEngine.Object.FindFirstObjectByType<TestWinConditionManager>();
                if (wins != null) wins.OnPieceMoveResolved((PlayerSlot)request.PlayerId,
                    MatchCompositionRule.GetTeamSlot(turns.Settings, (PlayerSlot)request.PlayerId), true, newlyFinished);
                if (turns.Settings.gameMode == GameMode.Escape)
                    foreach (var piece in PlayerPieces(request.PlayerId))
                        if (piece.IsFinished) piece.State = PieceState.Waiting;
            }
            if (result.EndTurnAfterResolution)
                turns.EndTurnAfterSkill(request.PlayerId);
        }

        public static void OnMoveCompleted(CharacterMoveRecord record)
        {
            var movedOwner = FindOwner(GetPiece(record.PlayerId, record.PieceId));
            var movedPiece = GetPiece(record.PlayerId, record.PieceId);
            if (movedOwner != null && movedPiece != null)
                CcBoardEffects.EnforceCloneCapacity(movedOwner, movedPiece);
            //수정: 부적은 지정된 아군 한 말의 효과이며 업기 시 다른 말로 복사하지 않습니다.
            foreach (var piece in PlayerPieces(record.PlayerId))
            {
                CcState parts = piece.Cc.Get(CcDefine.Robot_part);
                if (parts != null && piece.PieceId != record.PieceId &&
                    record.To == parts.Tile)
                {
                    PlayerController partsOwner = FindOwner(piece);
                    if (partsOwner == null ||
                        !partsOwner.TryGetPieceData(record.PieceId, out var arrived) ||
                        arrived.State != PieceState.InBoard)
                        continue;
                    piece.MoveTo(parts.Tile);
                    Remove(piece, parts);
                    int groupId = arrived.IsStacked
                        ? arrived.StackGroupId : partsOwner.RuntimeData.CreateStackGroupId();
                    int leaderId = arrived.IsStacked
                        ? arrived.StackLeaderPieceId : arrived.PieceId;
                    arrived.SetStackGroup(groupId, leaderId);
                    piece.SetStackGroup(groupId, leaderId);
                    CharacterSkillRegistry.NotifyPieceEnteredBoard(record.PlayerId, piece.PieceId);
                }
            }
            if (movedOwner != null && movedPiece != null)
                CcBoardEffects.EnforceCloneCapacity(movedOwner, movedPiece);

            if (record.IsSimpleMove || IsResolvingWindMove || movedPiece == null ||
                movedPiece.State != PieceState.InBoard || movedPiece.CurrentTileId != record.To ||
                record.Path.Count == 0 || record.Path[record.Path.Count - 1] != record.To) return;
            var movedLeader = movedPiece.IsStacked
                ? GetPiece(record.PlayerId, movedPiece.StackLeaderPieceId) : movedPiece;
            if (!CanMove(movedLeader)) return;
            var players = UnityEngine.Object.FindFirstObjectByType<PlayerManager>();
            if (players == null) return;
            foreach (var windOwner in players.ActivePlayers)
            {
                if (!CharacterBoardUtility.AreAllies(record.PlayerId, windOwner.PlayerId)) continue;
                foreach (var sourcePiece in windOwner.RuntimeData.Pieces)
                {
                    CcState wind = sourcePiece.Cc.Get(CcDefine.WindPath);
                    if (wind == null || !new List<BoardTileId>(wind.Path).Contains(record.To)) continue;
                    if (!CharacterSkillRegistry.TryGet(windOwner.PlayerId, sourcePiece.PieceId, out var source))
                        continue;
                    windMoveDepth++;
                    bool applied;
                    try { applied = source.TryApplyWindMove(movedLeader); }
                    finally { windMoveDepth--; }
                    if (!applied) continue;
                    return; // 하나의 착지는 한 개의 바람 이동만 일으킵니다.
                }
            }
        }

        // 외부 기본 잡기 코드의 기존 호출 계약만 유지합니다. 사용되지 않는 Mark 효과는 제거했습니다.
        public static void RewardMarks(PlayerRuntimeData.PieceRuntimeData target,
            CharacterCaptureRequest request) { }

        internal static PlayerController FindOwner(PlayerRuntimeData.PieceRuntimeData piece)
        {
            if (piece == null) return null;
            var players = UnityEngine.Object.FindFirstObjectByType<PlayerManager>();
            if (players == null) return null;
            foreach (var owner in players.ActivePlayers)
                if (owner.TryGetPieceData(piece.PieceId, out var candidate) && ReferenceEquals(candidate, piece))
                    return owner;
            return null;
        }

        public static PlayerRuntimeData.PieceRuntimeData GetPiece(int playerId, int pieceId)
        {
            foreach (var piece in PlayerPieces(playerId)) if (piece.PieceId == pieceId) return piece;
            return null;
        }

        private static IReadOnlyList<PlayerRuntimeData.PieceRuntimeData> PlayerPieces(int playerId)
        {
            var players = UnityEngine.Object.FindFirstObjectByType<PlayerManager>();
            return players != null && players.TryGetPlayer(playerId, out var owner)
                ? owner.RuntimeData.Pieces : Array.Empty<PlayerRuntimeData.PieceRuntimeData>();
        }
        private static List<CcState> Snapshot(PlayerRuntimeData.PieceRuntimeData piece) =>
            piece == null ? new List<CcState>() : new List<CcState>(piece.Cc.Effects);
        private static bool Contains(PlayerRuntimeData.PieceRuntimeData piece, CcState state) =>
            new List<CcState>(piece.Cc.Effects).Contains(state);
        private static void AddWeight(List<(YutResult, float)> table, YutResult result, float weight)
        {
            for (int i = 0; i < table.Count; i++)
                if (table[i].Item1 == result) { table[i] = (result, table[i].Item2 + weight); return; }
            table.Add((result, weight));
        }

        private static bool IsPathMove(CcDefine type) =>
            type == CcDefine.Peace_move || type == CcDefine.Move_end;

        private static bool ConsumeGuard(PlayerRuntimeData.PieceRuntimeData piece, CcDefine type)
        {
            if (piece == null) return false;
            // 기간 보호를 먼저 사용하여 다른 출처의 일회성 보호를 낭비하지 않습니다.
            var guards = new List<CcState>(piece.Cc.Effects);
            CcState guard = guards.Find(state => state.Type == type && state.Charges < 0) ??
                guards.Find(state => state.Type == type && state.Charges > 0);
            if (guard == null) return false;
            guard.ConsumeCharge();
            if (guard.Charges == 0) Remove(piece, guard);
            return true;
        }
    }
}
