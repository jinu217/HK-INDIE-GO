using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using YutArena.Common;
using YutArena.InGame;

/// <summary>실제 WindPath CC가 남아 있는 타일에만 표시하는 바람 이펙트입니다.</summary>
[DisallowMultipleComponent]
public sealed class ElfWindPathVisuals : MonoBehaviour
{
    [SerializeField] private bool showWindPath = true;
    [SerializeField] private Color windColor = new Color(0.25f, 1f, 0.82f, 0.85f);
    [SerializeField, Min(0.1f)] private float tileRadius = 0.52f;
    [SerializeField, Min(0f)] private float rotationSpeed = 75f;

    private sealed class TileVisual
    {
        public Transform Root;
        public readonly Transform[] Ribbons = new Transform[3];
    }

    private readonly Dictionary<BoardTileId, TileVisual> tiles = new Dictionary<BoardTileId, TileVisual>();
    private readonly HashSet<BoardTileId> activeTiles = new HashSet<BoardTileId>();
    private readonly List<BoardTileId> removedTiles = new List<BoardTileId>();
    private CHAR_008_Status source;
    private MapManager map;
    private InGamePieceDebugController board;
    private GameObject visualRoot;
    private Material material;
    private Color renderedColor;

    private void Awake() { source = GetComponent<CHAR_008_Status>(); }

    private void LateUpdate()
    {
        if (source == null) source = GetComponent<CHAR_008_Status>();
        var piece = source != null ? CcEffectService.GetPiece(source.PlayerId, source.PieceId) : null;
        var wind = piece?.Cc.Get(CcDefine.WindPath);
        if (!showWindPath || !CharacterSkillRegistry.AreSkillsEnabled || source == null ||
            !source.isActiveAndEnabled || piece != CcEffectService.StackLeader(piece) || wind == null)
        {
            ClearVisuals();
            return;
        }
        if (renderedColor != windColor) ClearVisuals();

        if (map == null) map = FindFirstObjectByType<MapManager>();
        if (board == null && map == null) board = FindFirstObjectByType<InGamePieceDebugController>();
        Camera camera = Camera.main;
        activeTiles.Clear();
        foreach (BoardTileId tile in wind.Path)
        {
            if (tile == BoardTileId.None || tile == BoardTileId.Goal || activeTiles.Contains(tile)) continue;
            if (!TryGetPosition(tile, out Vector3 position)) continue;
            activeTiles.Add(tile);
            if (!tiles.TryGetValue(tile, out TileVisual visual))
            {
                visual = CreateTileVisual(tile);
                if (visual == null) continue;
                tiles.Add(tile, visual);
            }
            visual.Root.position = position;
            visual.Root.rotation = camera != null ? camera.transform.rotation : Quaternion.identity;
            float phase = Time.time * rotationSpeed + (int)tile * 23f;
            for (int i = 0; i < visual.Ribbons.Length; i++)
            {
                visual.Ribbons[i].localRotation = Quaternion.Euler(0f, 0f, phase * (i == 1 ? -0.7f : 1f) + i * 120f);
                float pulse = 1f + 0.06f * Mathf.Sin(Time.time * 2.5f + i);
                visual.Ribbons[i].localScale = Vector3.one * tileRadius * (1f - i * 0.23f) * pulse;
            }
        }

        removedTiles.Clear();
        foreach (var pair in tiles)
            if (!activeTiles.Contains(pair.Key)) removedTiles.Add(pair.Key);
        foreach (BoardTileId tile in removedTiles)
        {
            Release(tiles[tile].Root.gameObject);
            tiles.Remove(tile);
        }
    }

    private bool TryGetPosition(BoardTileId tile, out Vector3 position)
    {
        if (map != null && map.TryGetTilePosition(tile, out position)) return true;
        if (board == null) board = FindFirstObjectByType<InGamePieceDebugController>();
        if (board != null && board.TryGetTileWorldPosition(tile, out position)) return true;
        position = default;
        return false;
    }

    private TileVisual CreateTileVisual(BoardTileId tile)
    {
        if (material == null)
        {
            // 이동 마커와 같은 기본 셰이더를 사용해 입체 타일에 묻히지 않게 합니다.
            var shader = Shader.Find("UI/Default");
            if (shader == null) return null;
            material = new Material(shader) { name = "Elf Wind", hideFlags = HideFlags.DontSave };
            material.mainTexture = Texture2D.whiteTexture;
            material.SetInt("unity_GUIZTestMode", (int)CompareFunction.Always);
            renderedColor = windColor;
        }
        if (visualRoot == null)
        {
            visualRoot = new GameObject("ElfWindPathVfx") { hideFlags = HideFlags.DontSave };
            SceneManager.MoveGameObjectToScene(visualRoot, gameObject.scene);
        }
        var root = new GameObject("Wind_" + tile).transform;
        root.SetParent(visualRoot.transform, false);
        var visual = new TileVisual { Root = root };
        for (int i = 0; i < visual.Ribbons.Length; i++)
        {
            var ribbon = new GameObject("WindRibbon").AddComponent<LineRenderer>();
            ribbon.transform.SetParent(root, false);
            visual.Ribbons[i] = ribbon.transform;
            ribbon.sharedMaterial = material;
            ribbon.useWorldSpace = false;
            ribbon.alignment = LineAlignment.TransformZ;
            ribbon.shadowCastingMode = ShadowCastingMode.Off;
            ribbon.receiveShadows = false;
            ribbon.sortingOrder = 40;
            ribbon.numCapVertices = 3;
            ribbon.widthMultiplier = 0.09f;
            ribbon.widthCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.2f, 1f), new Keyframe(1f, 0f));
            ribbon.colorGradient = new Gradient
            {
                colorKeys = new[] { new GradientColorKey(windColor, 0f), new GradientColorKey(Color.white, 1f) },
                alphaKeys = new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(windColor.a, 0.3f),
                    new GradientAlphaKey(0f, 1f) }
            };
            ribbon.positionCount = 33;
            for (int point = 0; point < ribbon.positionCount; point++)
            {
                float t = point / (float)(ribbon.positionCount - 1);
                float angle = t * Mathf.PI * 1.5f;
                float radius = Mathf.Lerp(0.78f, 1f, t);
                ribbon.SetPosition(point, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius);
            }
        }
        return visual;
    }

    private void OnDisable() { ClearVisuals(); }
    private void OnDestroy() { ClearVisuals(); }

    private void ClearVisuals()
    {
        if (visualRoot != null) Release(visualRoot);
        if (material != null) Release(material);
        visualRoot = null;
        material = null;
        tiles.Clear();
    }

    private static void Release(Object target)
    {
        if (target is GameObject visual) visual.SetActive(false);
        if (Application.isPlaying) Destroy(target);
        else DestroyImmediate(target);
    }
}
