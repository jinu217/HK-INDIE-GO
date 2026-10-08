using System;
using System.Collections.Generic;
using UnityEngine;
using YutArena.Common;
using YutArena.Managers;

namespace YutArena.InGame
{
    // 기존 저장 값 0~4는 변경하지 않습니다. CC에는 버프와 즉시 효과도 포함합니다.
    public enum CcDefine
    {
        None = 0,
        Stun = 1,
        Silence = 2,
        Retire = 3,
        Kill = 4,
        Protection = 5,
        Hidden = 6,
        Clone = 7,
        Parts = 8,
        DoubleMove = 9,
        MoveBonus = 10,
        DoOrMo = 11,
        RemoveBackDo = 12,
        ExtraThrow = 13,
        ReverseExtraThrow = 14,
        SkillPoint = 15,
        Move = 16,
        MovePath = 17,
        Mark = 18,
        WindPath = 19,
        LimitCapture = 20,
        YutMoExtraThrow = 21,
        TalismanProtection = 22,
        Binding = 23
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
        [SerializeField] private int sourcePlayerId;
        [SerializeField] private int sourcePieceId;
        [SerializeField] private BoardTileId tile;
        [SerializeField] private List<BoardTileId> path;
        public CcDefine Type => type;
        // 0: 소비/명시적 해제까지 유지. 양수: 대상 소유자의 턴 시작마다 감소.
        public int RemainingOwnerTurns => remainingOwnerTurns;
        public int Value => value;
        public int SourcePlayerId => sourcePlayerId;
        public int SourcePieceId => sourcePieceId;
        public BoardTileId Tile => tile;
        public IReadOnlyList<BoardTileId> Path => path;
        internal CcState(CcDefine type, int turns, int value, int sourcePlayerId,
            int sourcePieceId, BoardTileId tile, IReadOnlyList<BoardTileId> path)
        {
            this.type = type;
            remainingOwnerTurns = turns;
            this.value = value;
            this.sourcePlayerId = sourcePlayerId;
            this.sourcePieceId = sourcePieceId;
            this.tile = tile;
            this.path = path == null ? new List<BoardTileId>() : new List<BoardTileId>(path);
        }
        internal void Tick() { if (remainingOwnerTurns > 0) remainingOwnerTurns--; }
        internal void SetValue(int amount) { value = amount; }
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
                foreach (var type in new[] { CcDefine.Kill, CcDefine.Retire, CcDefine.Stun,
                    CcDefine.Binding, CcDefine.Silence, CcDefine.Parts })
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
        private static int windMoveDepth;
        internal static bool IsResolvingWindMove => windMoveDepth > 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntime() { Changed = null; windMoveDepth = 0; }

        public static bool CanMove(PlayerRuntimeData.PieceRuntimeData piece) =>
            piece != null && piece.State != PieceState.Goal &&
            (!piece.IsStacked || piece.StackLeaderPieceId == piece.PieceId) &&
            !piece.Cc.Has(CcDefine.Stun) && !piece.Cc.Has(CcDefine.Binding) &&
            !piece.Cc.Has(CcDefine.Parts);

        public static bool CanUseSkill(PlayerRuntimeData.PieceRuntimeData piece) =>
            piece != null && piece.State != PieceState.Goal &&
            !piece.Cc.Has(CcDefine.Stun) && !piece.Cc.Has(CcDefine.Silence) &&
            !piece.Cc.Has(CcDefine.Parts) &&
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
            return piece != null && !piece.Cc.Has(CcDefine.Hidden) && !piece.Cc.Has(CcDefine.Parts);
        }

        public static bool Apply(PlayerRuntimeData.PieceRuntimeData piece, CcDefine type,
            int turns = 0, int value = 1, int sourcePlayerId = -1, int sourcePieceId = -1,
            IReadOnlyList<BoardTileId> path = null, bool isSimpleMove = true)
        {
            if (piece == null) return false;
            if (!Enum.IsDefined(typeof(CcDefine), type)) throw new ArgumentOutOfRangeException(nameof(type));
            if (turns < 0) throw new ArgumentOutOfRangeException(nameof(turns));
            if (type != CcDefine.None && type != CcDefine.Move && value <= 0)
                throw new ArgumentOutOfRangeException(nameof(value));
            if (type == CcDefine.None) { Clear(piece); return true; }

            PlayerController owner = FindOwner(piece);
            int effectivePieceId = piece.IsStacked ? piece.StackLeaderPieceId : piece.PieceId;
            if (owner != null &&
                CharacterSkillRegistry.TryGet(owner.PlayerId, effectivePieceId, out var status) &&
                status.IsImmuneToEffect(type))
                return false;

            // 보호는 잡기뿐 아니라 직접 부여되는 해로운 상태이상도 막습니다.
            if ((type == CcDefine.Stun || type == CcDefine.Binding ||
                 type == CcDefine.Silence ||
                 type == CcDefine.Retire || type == CcDefine.Kill ||
                 type == CcDefine.Parts || type == CcDefine.Mark) &&
                (piece.Cc.Has(CcDefine.TalismanProtection) || ConsumeProtection(piece)))
                return false;

            if (type == CcDefine.Retire && owner != null &&
                CharacterSkillRegistry.TryGet(owner.PlayerId, piece.PieceId, out var targetStatus) &&
                targetStatus is CHAR_009_Status robot && robot.TryEnterParts(piece))
                return true;

            bool needsOwner = type == CcDefine.Clone || type == CcDefine.ExtraThrow ||
                type == CcDefine.SkillPoint || type == CcDefine.Move || type == CcDefine.MovePath;
            if (needsOwner && owner == null) return false;
            if (type == CcDefine.Clone &&
                (piece.State != PieceState.InBoard || CcBoardEffects.CountStackUnits(owner, piece) + value > 4))
                return false;
            TestTurnManager turnsManager = null;
            PieceMovementManager movement = null;
            if (type == CcDefine.ExtraThrow)
            {
                turnsManager = UnityEngine.Object.FindFirstObjectByType<TestTurnManager>();
                if (turnsManager == null || turnsManager.CurrentTurn == null ||
                    (int)turnsManager.CurrentTurn.currentPlayer != owner.PlayerId ||
                    turnsManager.CurrentTurn.currentPhase != TurnPhase.WaitAction) return false;
            }
            if (type == CcDefine.Move || type == CcDefine.MovePath)
            {
                if (!CanMove(piece) || value == 0) return false;
                if (type == CcDefine.MovePath && (path == null || path.Count == 0)) return false;
                movement = UnityEngine.Object.FindFirstObjectByType<PieceMovementManager>();
                if (type == CcDefine.Move && movement == null) return false;
            }
            if (type == CcDefine.Kill || type == CcDefine.Retire)
            {
                int oldGroup = piece.StackGroupId;
                Clear(piece);
                piece.ApplyCapturedPosition();
                CcBoardEffects.NormalizeStack(owner, oldGroup);
            }
            else if (type != CcDefine.Mark)
            {
                CcState previous = piece.Cc.Get(type);
                if (type == CcDefine.Clone && previous != null) value += previous.Value;
                if (previous != null && (type == CcDefine.Protection ||
                    type == CcDefine.TalismanProtection || type == CcDefine.Stun ||
                    type == CcDefine.Binding ||
                    type == CcDefine.Silence || type == CcDefine.Hidden))
                {
                    turns = previous.RemainingOwnerTurns == 0 || turns == 0
                        ? 0 : Math.Max(turns, previous.RemainingOwnerTurns);
                    value = Math.Max(value, previous.Value);
                }
                Remove(piece, type);
            }
            else
            {
                foreach (CcState mark in Snapshot(piece))
                    if (mark.Type == type && mark.SourcePlayerId == sourcePlayerId &&
                        mark.SourcePieceId == sourcePieceId) Remove(piece, mark);
            }

            var state = new CcState(type, turns, value, sourcePlayerId, sourcePieceId,
                piece.CurrentTileId, path);
            piece.Cc.Add(state);
            Changed?.Invoke(piece, state, true);
            Debug.Log($"[CC][Apply] Player={owner?.PlayerId}, Piece={piece.PieceId}, " +
                $"Effect={type}, Turns={turns}, Value={value}, Source={sourcePlayerId}/{sourcePieceId}");
            bool succeeded = true;
            switch (type)
            {
                case CcDefine.Kill:
                case CcDefine.Retire:
                    if (owner != null) CharacterSkillRegistry.NotifyPieceRetired(owner.PlayerId, piece.PieceId);
                    break;
                case CcDefine.Parts:
                    int oldGroup = piece.StackGroupId;
                    piece.ClearStack();
                    CcBoardEffects.NormalizeStack(owner, oldGroup);
                    break;
                case CcDefine.Clone: CcBoardEffects.RefreshClones(owner, piece); break;
                case CcDefine.ExtraThrow: turnsManager.GrantSkillExtraThrow(); break;
                case CcDefine.SkillPoint: CharacterSkillRegistry.RequestSkillPoint(owner.PlayerId, value); break;
                case CcDefine.Move: succeeded = movement.TryMovePiece(owner.PlayerId, piece.PieceId, value, true); break;
                case CcDefine.MovePath: CcBoardEffects.MoveStackAlongPath(owner, piece, path, value > 1, isSimpleMove); break;
            }
            if (type == CcDefine.ExtraThrow || type == CcDefine.SkillPoint ||
                type == CcDefine.Move || type == CcDefine.MovePath) Remove(piece, state);
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
            if (state.Type == CcDefine.Clone) CcBoardEffects.RefreshClones(FindOwner(piece), piece);
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
            CcState bonus = piece.Cc.Get(CcDefine.MoveBonus);
            if (bonus != null)
            {
                count += count < 0 ? -bonus.Value : bonus.Value;
                Remove(piece, bonus);
            }
            if (piece.Cc.Has(CcDefine.DoubleMove))
            {
                count *= 2;
                Remove(piece, CcDefine.DoubleMove);
            }
            return count;
        }

        public static CharacterCaptureDecision ResolveCapture(PlayerRuntimeData.PieceRuntimeData piece,
            CharacterCaptureRequest request)
        {
            if (piece == null) return CharacterCaptureDecision.Proceed;
            if (!IsTargetable(piece)) return CharacterCaptureDecision.Prevent;
            if (piece.Cc.Has(CcDefine.TalismanProtection))
                return CharacterCaptureDecision.Prevent;
            CcState shield = piece.Cc.Get(CcDefine.Protection);
            if (shield != null)
            {
                ConsumeProtection(piece);
                return CharacterCaptureDecision.Prevent;
            }
            CcState clone = piece.Cc.Get(CcDefine.Clone);
            if (clone != null)
            {
                clone.SetValue(clone.Value - 1);
                if (clone.Value <= 0) Remove(piece, clone);
                else CcBoardEffects.RefreshClones(FindOwner(piece), piece);
                return CharacterCaptureDecision.ConsumeCloneWithoutBonus;
            }
            if (piece.Cc.Has(CcDefine.LimitCapture))
            {
                Remove(piece, CcDefine.LimitCapture);
                return CharacterCaptureDecision.LimitRetireToAttackingCount;
            }
            return CharacterCaptureDecision.Proceed;
        }

        public static (YutResult, float)[] ResolveProbability(int playerId, (YutResult, float)[] table)
        {
            if (table == null || table.Length == 0) table = CharacterSkillRegistry.DefaultYutProbabilityTable;
            foreach (var piece in PlayerPieces(playerId))
            {
                if (piece.Cc.Has(CcDefine.DoOrMo))
                {
                    Remove(piece, CcDefine.DoOrMo);
                    return new[] { (YutResult.Do, 50f), (YutResult.Mo, 50f) };
                }
                if (!piece.Cc.Has(CcDefine.RemoveBackDo)) continue;
                Remove(piece, CcDefine.RemoveBackDo);
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

        public static bool ResolveExtraThrow(int playerId, YutResult result, bool defaultValue)
        {
            foreach (var piece in PlayerPieces(playerId))
            {
                if (!piece.Cc.Has(CcDefine.ReverseExtraThrow)) continue;
                Remove(piece, CcDefine.ReverseExtraThrow);
                return result == YutResult.Do || result == YutResult.Gae ||
                    result == YutResult.Geol || result == YutResult.BackDo;
            }
            foreach (var piece in PlayerPieces(playerId))
            {
                if (!piece.Cc.Has(CcDefine.YutMoExtraThrow)) continue;
                Remove(piece, CcDefine.YutMoExtraThrow);
                return defaultValue || result == YutResult.Yut || result == YutResult.Mo;
            }
            return defaultValue;
        }

        public static bool HasPlayerEffect(int playerId, CcDefine type)
        {
            foreach (var piece in PlayerPieces(playerId)) if (piece.Cc.Has(type)) return true;
            return false;
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
                    if (state.Type == CcDefine.Parts)
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
                Remove(piece, CcDefine.DoubleMove);
                Remove(piece, CcDefine.DoOrMo);
                Remove(piece, CcDefine.ReverseExtraThrow);
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
            PropagateDurableProtectionToStack(record.PlayerId, record.PieceId);
            foreach (var piece in PlayerPieces(record.PlayerId))
            {
                CcState parts = piece.Cc.Get(CcDefine.Parts);
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
                    if (!CharacterSkillRegistry.TryGet(windOwner.PlayerId, sourcePiece.PieceId, out var source) ||
                        !source.TryTriggerPassiveCooldown()) continue;
                    windMoveDepth++;
                    try
                    {
                        Apply(movedLeader, CcDefine.MovePath, sourcePlayerId: windOwner.PlayerId,
                            sourcePieceId: sourcePiece.PieceId,
                            path: CharacterBoardUtility.GetForwardPath(movedLeader, 1));
                    }
                    finally { windMoveDepth--; }
                    return; // 하나의 착지는 한 개의 바람 이동만 일으킵니다.
                }
            }
        }

        public static void RewardMarks(PlayerRuntimeData.PieceRuntimeData target, CharacterCaptureRequest request)
        {
            foreach (CcState state in Snapshot(target))
            {
                if (state.Type != CcDefine.Mark || state.SourcePlayerId != request.AttackerPlayerId ||
                    state.SourcePieceId != request.AttackerPieceId) continue;
                foreach (var source in PlayerPieces(state.SourcePlayerId))
                    if (source.PieceId == state.SourcePieceId) Apply(source, CcDefine.SkillPoint);
                Remove(target, state);
            }
        }

        public static void ClearMarksFrom(int playerId, int pieceId)
        {
            var players = UnityEngine.Object.FindFirstObjectByType<PlayerManager>();
            if (players == null) return;
            foreach (var owner in players.ActivePlayers)
            foreach (var piece in owner.RuntimeData.Pieces)
            foreach (CcState state in Snapshot(piece))
                if (state.Type == CcDefine.Mark && state.SourcePlayerId == playerId &&
                    state.SourcePieceId == pieceId) Remove(piece, state);
        }

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

        private static bool ConsumeProtection(PlayerRuntimeData.PieceRuntimeData piece)
        {
            CcState shield = piece.Cc.Get(CcDefine.Protection);
            if (shield == null) return false;

            shield.SetValue(shield.Value - 1);
            if (shield.Value <= 0) Remove(piece, shield);
            return true;
        }

        private static void PropagateDurableProtectionToStack(int playerId, int movedPieceId)
        {
            IReadOnlyList<PlayerRuntimeData.PieceRuntimeData> pieces = PlayerPieces(playerId);
            PlayerRuntimeData.PieceRuntimeData moved = null;
            PlayerRuntimeData.PieceRuntimeData leader = null;
            foreach (PlayerRuntimeData.PieceRuntimeData piece in pieces)
            {
                if (piece.PieceId == movedPieceId) moved = piece;
            }
            if (moved == null || !moved.IsStacked) return;

            foreach (PlayerRuntimeData.PieceRuntimeData piece in pieces)
            {
                if (piece.PieceId == moved.StackLeaderPieceId &&
                    piece.StackGroupId == moved.StackGroupId)
                {
                    leader = piece;
                    break;
                }
            }

            CcState shield = leader?.Cc.Get(CcDefine.TalismanProtection);
            if (shield == null || shield.RemainingOwnerTurns <= 0)
                return;

            foreach (PlayerRuntimeData.PieceRuntimeData piece in pieces)
            {
                if (piece.State != PieceState.InBoard ||
                    piece.StackGroupId != leader.StackGroupId || piece == leader)
                    continue;

                CcState existing = piece.Cc.Get(CcDefine.TalismanProtection);
                if (existing != null && existing.Value >= shield.Value &&
                    (existing.RemainingOwnerTurns == 0 ||
                     existing.RemainingOwnerTurns >= shield.RemainingOwnerTurns))
                    continue;

                Apply(piece, CcDefine.TalismanProtection, shield.RemainingOwnerTurns, shield.Value,
                    shield.SourcePlayerId, shield.SourcePieceId);
            }
        }
    }
}
