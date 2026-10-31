using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using YutArena.Common;
using YutArena.Managers;
using YutArena.InGame;
using UnityEngine.Rendering;

namespace YutArena.Test
{
    /// <summary>
    /// 기준 표면의 로컬 XY 평면에서 결과에 맞는 윷 던지기를 진행한다.
    /// Rigidbody 중력이나 투명 벽 없이 윷을 항상 원판 내부에 착지시킨다.
    /// </summary>
    public sealed class YutThrowManager : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("던지기 입력 UI 버튼")]
        [SerializeField] private Button throwButton;
        [Tooltip("윷 결과 및 턴 상태 관리 객체")]
        [SerializeField] private TestTurnManager turnManager;
        [Tooltip("일반 윷가락 프리팹")]
        [SerializeField] private GameObject normalYutPrefab;
        [Tooltip("백도 표시 윷가락 프리팹")]
        [SerializeField] private GameObject backDoYutPrefab;

        [Header("Throw Points")]
        [Tooltip("윷 던지기 시작 위치")]
        [SerializeField] private Transform throwStartPoint;
        [Tooltip("결과 표시 유지 시간")]
        [SerializeField, Min(0f)] private float resultVisibleDuration = 5f;

        [Header("Board Tile Fade")]
        [Tooltip("말판 타일 검색 루트 및 미지정 시 현재 씬 전체 검색")]
        [SerializeField] private Transform boardTileRoot;
        [Tooltip("던지기 중 반투명 표시 대상 칸 ID")]
        [SerializeField] private BoardTileId[] fadedTileIds =
        {
            BoardTileId.Center,
            BoardTileId.Inner01, BoardTileId.Inner02, BoardTileId.Inner03, BoardTileId.Inner04,
            BoardTileId.Inner05, BoardTileId.Inner06, BoardTileId.Inner07, BoardTileId.Inner08
        };
        [Tooltip("던지기 및 결과 표시 중 말판 불투명도")]
        [SerializeField, Range(0f, 1f)] private float throwingTileAlpha = 0.3f;

        [Header("Board Area (Local XY)")]
        [Tooltip("윷 착지 원형 영역 반지름")]
        [SerializeField, Min(0.1f)] private float boardRadius = 2.5f;
        [Tooltip("원판 가장자리 안쪽 착지 여백")]
        [SerializeField, Min(0f)] private float edgePadding = 0.35f;
        [Tooltip("게임 시작 시 윷 정렬 간격")]
        [SerializeField, Min(0f)] private float startYutSpacing = 0.35f;
        [Tooltip("착지 윷 사이 목표 중심 간격")]
        [SerializeField, Min(0f)] private float minimumYutSpacing = 0.65f;

        [Header("Automatic Overlap Stacking")]
        [Tooltip("윷 몸체 교차 시 자동 위층 배치 여부")]
        [SerializeField] private bool enableOverlapStacking = true;
        [Tooltip("자동 측정 모델 두께 기준 최소 층 높이")]
        [SerializeField, Min(0.001f)] private float stackLayerHeight = 0.12f;
        [Tooltip("윷 층 사이 추가 여백")]
        [SerializeField, Min(0f)] private float stackClearance = 0.02f;
        [Tooltip("OBJ 긴 방향 로컬 Y축 여부")]
        [SerializeField] private bool modelLongAxisIsY = true;
        [Tooltip("겹친 윷 최대 기울기 각도")]
        [SerializeField, Range(0f, 30f)] private float maximumStackTilt = 12f;
        [Header("Board Placement")]
        [Tooltip("윷 착지 표면 중심 및 XY 평면 기준 객체")]
        [SerializeField] private Transform boardSurface;

        [Header("Throw Animation")]
        [Tooltip("첫 번째 윷 비행 시간")]
        [SerializeField, Min(0.1f)] private float throwDuration = 0.85f;
        [Tooltip("판 표면 기준 비행 최고 추가 높이")]
        [SerializeField, Min(0f)] private float arcHeight = 1.2f;
        [Tooltip("판 위 비행 경로 휘어짐 크기")]
        [SerializeField, Min(0f)] private float depthHop = 0.45f;
        [Tooltip("던지기 최소 및 최대 회전 바퀴 수")]
        [SerializeField] private Vector2 tumbleTurns = new Vector2(2f, 3f);

        [Header("Landing Motion")]
        [Tooltip("착지 묶음 사이 시간 간격 / 반동 유지 시간 이상의 실제 간격")]
        [SerializeField, Min(0.02f)] private float landingInterval = 0.1f;
        [Tooltip("착지 후 반동 유지 시간")]
        [SerializeField, Min(0.05f)] private float settleDuration = 0.22f;
        [Tooltip("착지 후 최대 반동 높이")]
        [SerializeField, Min(0f)] private float landingBounce = 0.045f;
        [Tooltip("착지 후 최대 흔들림 각도")]
        [SerializeField, Range(0f, 6f)] private float landingRockAngle = 2.5f;

        [Header("Model")]
        [Tooltip("OBJ 모델 기본 축 회전 보정값")]
        [SerializeField] private Vector3 modelRotationOffset;
        [Tooltip("윷 반대 면 표시 회전값")]
        [SerializeField] private Vector3 faceFlipRotation = new Vector3(180f, 0f, 0f);
        [Tooltip("생성 윷 OBJ 크기 배율")]
        [SerializeField, Min(0.0001f)] private float modelScaleMultiplier = 1f;

        private readonly List<Transform> yuts = new List<Transform>();
        private readonly List<YutVisual> visuals = new List<YutVisual>();
        private bool isThrowing;
        public bool IsThrowing => isThrowing;
        private Coroutine resetCoroutine;
        private readonly Dictionary<Renderer, Material[]> originalTileMaterials = new Dictionary<Renderer, Material[]>();
        private readonly List<Material> temporaryTileMaterials = new List<Material>();
        private static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");

        private void Awake()
        {
            if (turnManager == null)
            {
                turnManager = FindFirstObjectByType<TestTurnManager>();
            }

            if (throwStartPoint == null)
            {
                throwStartPoint = transform;
            }

            CreateYuts();
            ResetYutsToStart(true);
        }

        private void OnEnable()
        {
            if (turnManager != null)
            {
                turnManager.OnTurnPhaseChanged += HandleTurnPhaseChanged;
                RefreshThrowButton(turnManager.CurrentTurn);
            }
        }

        private void OnDisable()
        {
            if (turnManager != null) turnManager.OnTurnPhaseChanged -= HandleTurnPhaseChanged;
            StopAllCoroutines();
            RestoreBoardTileMaterials();
            isThrowing = false;
            resetCoroutine = null;
            if (yuts.Count > 0) ResetYutsToStart(true);
        }

        private void CreateYuts()
        {
            if (yuts.Count > 0) return;

            for (int i = 0; i < 3; i++)
            {
                CreateYut(normalYutPrefab, $"TestYut_Normal_{i + 1}", i);
            }

            CreateYut(backDoYutPrefab, "TestYut_BackDo", 3);
        }

        private void HandleTurnPhaseChanged(TurnContext turn)
        {
            if (turn == null) return;

            if (turn.currentPhase == TurnPhase.SaveThrowResult)
            {
                Throw(turn.lastYutResult);
            }

            RefreshThrowButton(turn);
        }

        private void RefreshThrowButton(TurnContext turn)
        {
            if (throwButton == null) return;

            throwButton.interactable =
                !isThrowing &&
                turn != null &&
                turn.currentPhase == TurnPhase.WaitThrow;
        }

        public void Throw(YutResult result)
        {
            if (isThrowing || yuts.Count == 0) return;

            if (resetCoroutine != null)
            {
                StopCoroutine(resetCoroutine);
                resetCoroutine = null;
            }

            ResetYutsToStart(false);
            SetYutsVisible(true);
            FadeBoardTiles();
            StartCoroutine(ThrowRoutine(result));
        }

        private void FadeBoardTiles()
        {
            RestoreBoardTileMaterials();
            if (fadedTileIds == null || fadedTileIds.Length == 0) return;

            var ids = new HashSet<BoardTileId>(fadedTileIds);
            BoardTileAnchor[] anchors = boardTileRoot != null
                ? boardTileRoot.GetComponentsInChildren<BoardTileAnchor>(true)
                : FindObjectsByType<BoardTileAnchor>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            foreach (BoardTileAnchor anchor in anchors)
            {
                if (anchor.gameObject.scene != gameObject.scene || !ids.Contains(anchor.TileId)) continue;

                foreach (MeshRenderer renderer in anchor.GetComponentsInChildren<MeshRenderer>(true))
                {
                    // 타일 아래에 생성된 말과 이동 마커는 투명 처리 대상에서 제외
                    if (renderer.GetComponentInParent<DebugPieceView>() != null
                        || renderer.GetComponentInParent<MoveMarker>() != null
                        || renderer.name == MoveMarker.TileHullName
                        || renderer.GetComponentInParent<BoardTileAnchor>() != anchor
                        || originalTileMaterials.ContainsKey(renderer)) continue;

                    Material[] originals = renderer.sharedMaterials;
                    Material[] faded = (Material[])originals.Clone();
                    for (int i = 0; i < originals.Length; i++)
                    {
                        Material source = originals[i];
                        if (source == null || !source.HasProperty("_Surface")
                            || !source.HasProperty(BaseColorProperty)) continue;

                        Material copy = new Material(source) { name = source.name + " (Yut Fade)" };
                        copy.SetFloat("_Surface", 1f);
                        if (copy.HasProperty("_Blend")) copy.SetFloat("_Blend", 0f);
                        copy.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                        copy.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                        if (copy.HasProperty("_SrcBlendAlpha")) copy.SetInt("_SrcBlendAlpha", (int)BlendMode.One);
                        if (copy.HasProperty("_DstBlendAlpha")) copy.SetInt("_DstBlendAlpha", (int)BlendMode.OneMinusSrcAlpha);
                        copy.SetInt("_ZWrite", 0);
                        copy.SetOverrideTag("RenderType", "Transparent");
                        copy.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                        copy.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                        copy.DisableKeyword("_ALPHAMODULATE_ON");
                        copy.DisableKeyword("_ALPHATEST_ON");
                        if (copy.HasProperty("_AlphaClip")) copy.SetFloat("_AlphaClip", 0f);
                        copy.renderQueue = (int)RenderQueue.Transparent;
                        copy.SetShaderPassEnabled("ShadowCaster", false);
                        Color color = copy.GetColor(BaseColorProperty);
                        color.a *= throwingTileAlpha;
                        copy.SetColor(BaseColorProperty, color);
                        faded[i] = copy;
                        temporaryTileMaterials.Add(copy);
                    }

                    originalTileMaterials.Add(renderer, originals);
                    renderer.sharedMaterials = faded;
                }
            }
        }

        private void RestoreBoardTileMaterials()
        {
            foreach (var pair in originalTileMaterials)
            {
                if (pair.Key != null) pair.Key.sharedMaterials = pair.Value;
            }
            originalTileMaterials.Clear();
            foreach (Material material in temporaryTileMaterials)
            {
                if (material != null) Destroy(material);
            }
            temporaryTileMaterials.Clear();
        }

        private void CreateYut(GameObject prefab, string instanceName, int index)
        {
            if (prefab == null) return;

            GameObject instance = Instantiate(prefab, transform);
            instance.name = instanceName + "_Model";
            instance.transform.localScale *= modelScaleMultiplier;

            Rigidbody[] bodies = instance.GetComponentsInChildren<Rigidbody>();
            foreach (Rigidbody body in bodies)
            {
                body.isKinematic = true;
                body.detectCollisions = false;
            }

            var visual = new YutVisual(instance, transform, instanceName);
            visuals.Add(visual);
            yuts.Add(visual.Pivot);
            visual.Pivot.SetPositionAndRotation(GetStartPosition(index), GetStartRotation(index));
        }

        private Vector3 GetStartPosition(int index)
        {
            Transform start = throwStartPoint != null ? throwStartPoint : transform;
            float x = (index - 1.5f) * startYutSpacing;
            return start.position + start.right * x;
        }

        private Quaternion GetStartRotation(int index)
        {
            Transform start = throwStartPoint != null ? throwStartPoint : transform;
            return start.rotation
                 * Quaternion.AngleAxis(index * 12f, Vector3.forward)
                 * Quaternion.Euler(modelRotationOffset);
        }

        private void ResetYutsToStart(bool hide)
        {
            for (int i = 0; i < yuts.Count; i++)
            {
                if (yuts[i] == null) continue;
                yuts[i].SetPositionAndRotation(GetStartPosition(i), GetStartRotation(i));
            }

            if (hide) SetYutsVisible(false);
        }

        private void SetYutsVisible(bool visible)
        {
            foreach (YutVisual visual in visuals) visual.SetVisible(visible);
        }

        private IEnumerator ThrowRoutine(YutResult result)
        {
            isThrowing = true;
            if (throwButton != null) throwButton.interactable = false;
            int count = yuts.Count;
            bool[] faces = CreateFaceResults(result, count);
            var sizes = new Vector3[count];
            var radii = new float[count];
            Quaternion frame = GetBoardFrameRotation();
            Vector3 origin = GetSurfaceOrigin();
            for (int i = 0; i < count; i++)
            {
                Quaternion face = Quaternion.Euler(faces[i] ? faceFlipRotation : Vector3.zero)
                    * Quaternion.Euler(modelRotationOffset);
                sizes[i] = visuals[i].Measure(face);
                sizes[i].z = Mathf.Max(sizes[i].z, stackLayerHeight);
                radii[i] = visuals[i].Radius;
                if (radii[i] + 0.04f > boardRadius)
                    Debug.LogWarning("윷 모델이 착지 영역보다 큼: Model Scale 또는 Board Radius 조정 필요", this);
            }
            YutLandingPose[] poses = YutLandingSolver.Solve(sizes, radii, boardRadius,
                edgePadding, minimumYutSpacing, modelLongAxisIsY, enableOverlapStacking,
                stackClearance, maximumStackTilt);
            var flights = new YutFlight[count];
            int minTurns = Mathf.Max(1, Mathf.CeilToInt(tumbleTurns.x));
            int maxTurns = Mathf.Max(minTurns, Mathf.FloorToInt(tumbleTurns.y));
            // 인접한 착지 순서 사이의 경계를 각각 무작위 선택
            // 4개 기준 1-2-3-4부터 1234까지 총 8가지 묶음 형태
            int groupBoundaries = Random.Range(0, 1 << Mathf.Max(0, count - 1));
            int landingGroup = 0;
            for (int order = 0; order < count; order++)
            {
                if (order > 0 && (groupBoundaries & (1 << (order - 1))) != 0)
                    landingGroup++;
                YutLandingPose pose = poses[order];
                int i = pose.Index;
                Vector3 target = origin + frame * new Vector3(pose.Center.x, pose.Center.y, -pose.Height);
                Vector3 shortAxis = frame * new Vector3(pose.Short.x, pose.Short.y, 0f);
                Quaternion rotation = Quaternion.AngleAxis(Mathf.Atan(pose.Slope) * Mathf.Rad2Deg, shortAxis)
                    * BoardRotation(pose.Angle, faces[i]);
                Vector3 start = yuts[i].position;
                Vector3 localStart = Quaternion.Inverse(frame) * (start - origin);
                Vector2 controlXY = ((Vector2)localStart + pose.Center) * 0.5f + Vector2.up * depthHop;
                // 비행 중 옆 방향 확산도 판 내부의 제어점을 통해 제한
                float available = Mathf.Max(0f, boardRadius - radii[i] - 0.04f);
                controlXY = Vector2.ClampMagnitude(controlXY, available);
                Vector3 control = origin + frame * new Vector3(controlXY.x, controlXY.y,
                    (localStart.z - pose.Height) * 0.5f);
                flights[order] = new YutFlight
                {
                    Pivot = yuts[i], Start = start, Target = target, Control = control,
                    Visual = visuals[i], RockAxis = shortAxis,
                    StartRotation = yuts[i].rotation, TargetRotation = rotation,
                    SpinAxis = frame * new Vector3(Random.Range(0.75f, 1f), Random.Range(-0.3f, 0.3f), 0f).normalized,
                    Duration = throwDuration + landingGroup * Mathf.Max(landingInterval, settleDuration),
                    Height = arcHeight * Random.Range(0.85f, 1.05f),
                    Turns = Random.Range(minTurns, maxTurns + 1)
                };
            }
            yield return YutThrowAnimator.Play(flights, origin, frame, settleDuration, landingBounce, landingRockAngle);
            isThrowing = false;
            RefreshThrowButton(turnManager != null ? turnManager.CurrentTurn : null);
            resetCoroutine = StartCoroutine(HideAndResetAfterDelay());
        }

        private IEnumerator HideAndResetAfterDelay()
        {
            yield return new WaitForSeconds(resultVisibleDuration);
            SetYutsVisible(false);
            RestoreBoardTileMaterials();
            ResetYutsToStart(false);
            resetCoroutine = null;
        }

        private static bool[] CreateFaceResults(YutResult result, int count)
        {
            // OBJ의 기본 방향이 결과 면을 보여주므로, true인 윷만 180도 돌려 반대 면을 보인다.
            var flipped = new bool[count];
            if (count == 0) return flipped;

            // 생성 순서상 마지막 윷이 백도 윷이다.
            int backDoIndex = count - 1;
            if (result == YutResult.BackDo)
            {
                // 백도 윷만 OBJ 기본 면을 유지하고 나머지 일반 윷은 반대 면으로 돌린다.
                for (int i = 0; i < count; i++) flipped[i] = i != backDoIndex;
                return flipped;
            }

            int originalFaceCount;
            switch (result)
            {
                case YutResult.Do: originalFaceCount = 1; break;
                case YutResult.Gae: originalFaceCount = 2; break;
                case YutResult.Geol: originalFaceCount = 3; break;
                case YutResult.Yut: originalFaceCount = 4; break;
                case YutResult.Mo: originalFaceCount = 0; break;
                default: return flipped; // 낙, None은 현재 방향 유지
            }

            // 우선 전부 반대 면으로 돌린 뒤 결과에 필요한 개수만 OBJ 기본 면으로 되돌린다.
            for (int i = 0; i < count; i++) flipped[i] = true;

            var candidates = new List<int>(count);
            for (int i = 0; i < count; i++) candidates.Add(i);

            // 도는 백도 표시가 나오면 안 되므로 일반 윷 하나만 기본 면으로 남긴다.
            if (result == YutResult.Do) candidates.Remove(backDoIndex);

            for (int i = 0; i < originalFaceCount && candidates.Count > 0; i++)
            {
                int candidateIndex = Random.Range(0, candidates.Count);
                flipped[candidates[candidateIndex]] = false;
                candidates.RemoveAt(candidateIndex);
            }

            return flipped;
        }

        private Quaternion BoardRotation(float angle, bool flipFace)
        {
            return GetBoardFrameRotation()
                 * Quaternion.AngleAxis(angle, Vector3.forward)
                 * Quaternion.Euler(flipFace ? faceFlipRotation : Vector3.zero)
                 * Quaternion.Euler(modelRotationOffset);
        }

        private Quaternion GetBoardFrameRotation()
        {
            return boardSurface != null ? boardSurface.rotation : transform.rotation;
        }

        private Vector3 GetSurfaceOrigin()
        {
            return boardSurface != null ? boardSurface.position : transform.position;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            boardRadius = Mathf.Max(0.1f, boardRadius);
            edgePadding = Mathf.Clamp(edgePadding, 0f, boardRadius - 0.05f);
            startYutSpacing = Mathf.Max(0f, startYutSpacing);
            minimumYutSpacing = Mathf.Max(0f, minimumYutSpacing);
            stackLayerHeight = Mathf.Max(0.001f, stackLayerHeight);
            stackClearance = Mathf.Max(0f, stackClearance);
            maximumStackTilt = Mathf.Clamp(maximumStackTilt, 0f, 30f);
            resultVisibleDuration = Mathf.Max(0f, resultVisibleDuration);
            throwDuration = Mathf.Max(0.1f, throwDuration);
            tumbleTurns.y = Mathf.Max(tumbleTurns.x, tumbleTurns.y);
        }

        private void OnDrawGizmosSelected()
        {
            Quaternion boardFrame = GetBoardFrameRotation();
            Vector3 boardOrigin = GetSurfaceOrigin();
            Gizmos.color = Color.yellow;
            const int segments = 48;
            Vector3 previous = boardOrigin
                             + boardFrame * Vector3.right * boardRadius;
            for (int i = 1; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                Vector3 next = boardOrigin
                             + boardFrame * Vector3.right * (Mathf.Cos(angle) * boardRadius)
                             + boardFrame * Vector3.up * (Mathf.Sin(angle) * boardRadius);
                Gizmos.DrawLine(previous, next);
                previous = next;
            }
        }
#endif
    }
}
