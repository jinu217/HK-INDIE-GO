using System;
using System.Collections.Generic;
using UnityEngine;
using YutArena.Common;
using YutArena.Managers;

namespace YutArena.InGame
{
    /// <summary>규칙 매니저의 생성 요청을 실제 보드 배치와 칸 통과 획득에 연결합니다.</summary>
    public sealed class KtkBoardItemController : MonoBehaviour
    {
        [SerializeField, Min(0.05f)] private float visualScale = 0.45f;
        private KillTheKingManager rules;
        private GameStartSettings settings;
        private PlayerManager players;
        private MapManager map;
        private InGamePieceDebugController boardView;
        private readonly List<PlayerRuntimeData> owners = new List<PlayerRuntimeData>();
        private bool subscribed;
        private GameObject swordVisual, shieldVisual;
        private Material itemMaterial;
        private KtkSword? displayedSword;
        private KtkShield? displayedShield;

        public BoardTileId SwordTile { get; private set; } = BoardTileId.None;
        public BoardTileId ShieldTile { get; private set; } = BoardTileId.None;
        public event Action<PlayerRuntimeData.PieceRuntimeData> ItemPickedUp;

        public void Initialize(KillTheKingManager manager, GameStartSettings gameSettings,
            PlayerManager playerManager, MapManager mapManager)
        {
            Unsubscribe();
            ClearSword();
            ClearShield();
            rules = manager;
            settings = gameSettings;
            players = playerManager;
            map = mapManager;
            boardView = FindFirstObjectByType<InGamePieceDebugController>();
            owners.Clear();
            if (players != null)
                foreach (var player in players.ActivePlayers)
                    if (player.RuntimeData != null) owners.Add(player.RuntimeData);
            Subscribe();
        }

        private void OnEnable() => Subscribe();
        private void OnDisable() => Unsubscribe();
        private void OnDestroy()
        {
            Unsubscribe();
            DestroyRuntime(itemMaterial);
        }

        private void Subscribe()
        {
            if (subscribed || rules == null || settings == null || settings.gameMode != GameMode.KillTheKing) return;
            subscribed = true;
            rules.OnSwordSpawnRequested += HandleSwordSpawn;
            rules.OnShieldSpawnRequested += HandleShieldSpawn;
            rules.OnBoardSwordUpgraded += HandleSwordUpgrade;
            rules.OnConvertSwordsToDemonRequested += ConvertSwords;
            foreach (var owner in owners)
            {
                owner.PieceEnteredTile += OnPieceEnteredTile;
                owner.ItemsStolen += OnItemsStolen;
            }
        }

        private void Unsubscribe()
        {
            if (!subscribed) return;
            subscribed = false;
            rules.OnSwordSpawnRequested -= HandleSwordSpawn;
            rules.OnShieldSpawnRequested -= HandleShieldSpawn;
            rules.OnBoardSwordUpgraded -= HandleSwordUpgrade;
            rules.OnConvertSwordsToDemonRequested -= ConvertSwords;
            foreach (var owner in owners)
            {
                owner.PieceEnteredTile -= OnPieceEnteredTile;
                owner.ItemsStolen -= OnItemsStolen;
            }
        }

        private void HandleSwordSpawn(KtkSword sword) => SynchronizeBoard();
        private void HandleShieldSpawn(KtkShield shield) => SynchronizeBoard();
        private void HandleSwordUpgrade(KtkSword sword)
        {
            if (SwordTile != BoardTileId.None) ShowSword(sword);
        }

        private void ConvertSwords()
        {
            foreach (var owner in owners)
                foreach (var piece in owner.Pieces) piece.KtkItems.ConvertSwordsToDemon();
            if (SwordTile != BoardTileId.None) ShowSword(KtkSword.Demon);
        }

        private void LateUpdate() => SynchronizeBoard();

        // 앵커 준비가 늦거나 모든 후보 칸이 차 있으면 빈칸이 생길 때까지 생성 요청을 유지합니다.
        internal void SynchronizeBoard()
        {
            if (!subscribed) return;
            if (rules.BoardSword == null) ClearSword();
            else
            {
                if (SwordTile == BoardTileId.None)
                {
                    SwordTile = ChooseFreeTile();
                    if (SwordTile != BoardTileId.None)
                        QaDebugLog.Event($"[킬더킹][아이템 배치] {QaDebugLog.Sword(rules.BoardSword.Value)} | 위치={SwordTile}");
                }
                if (SwordTile != BoardTileId.None && (swordVisual == null || displayedSword != rules.BoardSword))
                    ShowSword(rules.BoardSword.Value);
                PositionVisual(swordVisual, SwordTile);
            }
            if (rules.BoardShield == null) ClearShield();
            else
            {
                if (ShieldTile == BoardTileId.None)
                {
                    ShieldTile = ChooseFreeTile();
                    if (ShieldTile != BoardTileId.None)
                        QaDebugLog.Event($"[킬더킹][아이템 배치] {QaDebugLog.Shield(rules.BoardShield.Value)} | 위치={ShieldTile}");
                }
                if (ShieldTile != BoardTileId.None && (shieldVisual == null || displayedShield != rules.BoardShield))
                    ShowShield(rules.BoardShield.Value);
                PositionVisual(shieldVisual, ShieldTile);
            }
        }

        private BoardTileId ChooseFreeTile()
        {
            var available = new List<BoardTileId>();
            foreach (BoardTileId tile in Enum.GetValues(typeof(BoardTileId)))
            {
                if (!IsSpawnTile(tile) || tile == SwordTile || tile == ShieldTile || !TryGetPosition(tile, out _)) continue;
                bool occupied = false;
                foreach (var owner in owners)
                    foreach (var piece in owner.Pieces)
                        if (piece.State == PieceState.InBoard && piece.CurrentTileId == tile) occupied = true;
                if (!occupied) available.Add(tile);
            }
            return available.Count == 0 ? BoardTileId.None : available[UnityEngine.Random.Range(0, available.Count)];
        }

        private static bool IsSpawnTile(BoardTileId tile) =>
            (tile >= BoardTileId.Outer05 && tile <= BoardTileId.Outer16 &&
                tile != BoardTileId.Corner02 && tile != BoardTileId.Corner03) ||
            (tile >= BoardTileId.Inner01 && tile <= BoardTileId.Inner08);

        private bool TryGetPosition(BoardTileId tile, out Vector3 position)
        {
            if (map != null && map.TryGetPiecePosition(tile, out position)) return true;
            if (boardView != null && boardView.TryGetTileWorldPosition(tile, out position)) return true;
            position = default;
            return false;
        }

        private void OnPieceEnteredTile(PlayerRuntimeData.PieceRuntimeData piece, BoardTileId tile)
        {
            if (tile == BoardTileId.None || tile == BoardTileId.Goal ||
                piece.State != PieceState.InBoard || piece.Cc.Has(CcDefine.Robot_part)) return;
            TeamSlot team = MatchCompositionRule.GetTeamSlot(settings, (PlayerSlot)piece.OwnerPlayerId);
            bool picked = false;
            string acquired = "";
            if (tile == SwordTile && rules.BoardSword.HasValue)
            {
                acquired = QaDebugLog.Sword(rules.BoardSword.Value);
                piece.KtkItems.Add(rules.BoardSword.Value);
                ClearSword();
                rules.NotifySwordPickedUp(team);
                picked = true;
            }
            if (tile == ShieldTile && rules.BoardShield.HasValue)
            {
                acquired += (picked ? ", " : "") + QaDebugLog.Shield(rules.BoardShield.Value);
                piece.KtkItems.Add(rules.BoardShield.Value);
                ClearShield();
                rules.NotifyShieldPickedUp(team);
                picked = true;
            }
            if (!picked) return;
            QaDebugLog.Event($"[킬더킹] P{piece.OwnerPlayerId}/말{piece.PieceId + 1} 아이템 획득=[{acquired}] | 칸={tile}, 묶음 소지품={piece.KtkItems.Count}");
            ItemPickedUp?.Invoke(piece);
        }

        private void OnItemsStolen(PlayerRuntimeData.PieceRuntimeData captor, int count) =>
            rules.NotifyItemsStolen(MatchCompositionRule.GetTeamSlot(settings, (PlayerSlot)captor.OwnerPlayerId), count);

        private void PositionVisual(GameObject visual, BoardTileId tile)
        {
            if (visual == null || !TryGetPosition(tile, out var position)) return;
            var camera = Camera.main;
            visual.transform.position = position + (camera != null ? -camera.transform.forward : Vector3.up) * 0.12f;
            visual.transform.rotation = camera != null ? camera.transform.rotation : Quaternion.Euler(90f, 0f, 0f);
        }

        private GameObject CreateVisual(string label)
        {
            var visual = new GameObject(label);
            visual.transform.SetParent(transform, false);
            visual.transform.localScale = Vector3.one * visualScale;
            return visual;
        }

        private void ShowSword(KtkSword sword)
        {
            DestroyRuntime(swordVisual);
            swordVisual = CreateVisual($"KtkSword_{sword}");
            displayedSword = sword;
            Color color = sword == KtkSword.Demon ? new Color(1f, 0.15f, 0.15f) :
                sword == KtkSword.Vampire ? new Color(0.85f, 0.2f, 0.85f) : new Color(0.7f, 0.9f, 1f);
            int blades = sword == KtkSword.Twin ? 2 : 1;
            for (int i = 0; i < blades; i++)
            {
                float x = blades == 2 ? (i == 0 ? -0.22f : 0.22f) : 0f;
                Part(swordVisual, PrimitiveType.Cube, new Vector3(x, 0.18f, 0f), new Vector3(0.14f, 0.7f, 0.06f), color);
                Part(swordVisual, PrimitiveType.Cube, new Vector3(x, -0.18f, 0f), new Vector3(0.4f, 0.1f, 0.08f), Color.yellow);
                Part(swordVisual, PrimitiveType.Cube, new Vector3(x, -0.35f, 0f), new Vector3(0.12f, 0.25f, 0.08f), Color.yellow);
            }
            PositionVisual(swordVisual, SwordTile);
        }

        private void ShowShield(KtkShield shield)
        {
            DestroyRuntime(shieldVisual);
            shieldVisual = CreateVisual($"KtkShield_{shield}");
            displayedShield = shield;
            Color color = shield == KtkShield.Thorn ? new Color(1f, 0.55f, 0.15f) :
                shield == KtkShield.Mirror ? Color.white : new Color(0.2f, 0.55f, 1f);
            Part(shieldVisual, PrimitiveType.Sphere, Vector3.zero, new Vector3(0.8f, 0.95f, 0.12f), color);
            Part(shieldVisual, PrimitiveType.Cube, new Vector3(0f, 0f, -0.08f), new Vector3(0.12f, 0.55f, 0.04f), Color.white);
            PositionVisual(shieldVisual, ShieldTile);
        }

        private void Part(GameObject root, PrimitiveType type, Vector3 position, Vector3 scale, Color color)
        {
            var part = GameObject.CreatePrimitive(type);
            part.transform.SetParent(root.transform, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            var collider = part.GetComponent<Collider>();
            collider.enabled = false;
            DestroyRuntime(collider);
            if (itemMaterial == null)
                itemMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default"));
            var renderer = part.GetComponent<Renderer>();
            renderer.sharedMaterial = itemMaterial;
            var properties = new MaterialPropertyBlock();
            properties.SetColor("_BaseColor", color);
            properties.SetColor("_Color", color);
            renderer.SetPropertyBlock(properties);
        }

        private void ClearSword()
        {
            DestroyRuntime(swordVisual);
            swordVisual = null;
            SwordTile = BoardTileId.None;
            displayedSword = null;
        }

        private void ClearShield()
        {
            DestroyRuntime(shieldVisual);
            shieldVisual = null;
            ShieldTile = BoardTileId.None;
            displayedShield = null;
        }

        private static void DestroyRuntime(UnityEngine.Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }
    }
}
