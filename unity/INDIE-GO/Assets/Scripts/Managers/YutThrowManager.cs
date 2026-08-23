using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using YutArena.Common;
using YutArena.Managers;

namespace YutArena.Test
{
    /// <summary>
    /// 원판의 로컬 XY 평면에서 윷 던지기를 연출하는 독립 2.5D 테스트.
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
        [Tooltip("원판 중앙 및 로컬 X/Y축 기준점")]
        [SerializeField] private Transform boardCenter;

        [Header("Board Area (Local XY)")]
        [Tooltip("윷 착지 원형 영역 반지름")]
        [SerializeField, Min(0.1f)] private float boardRadius = 2.5f;
        [Tooltip("원판 가장자리 안쪽 착지 여백")]
        [SerializeField, Min(0f)] private float edgePadding = 0.35f;
        [Tooltip("원판 표면 기준 카메라 방향 깊이 오프셋")]
        [SerializeField] private float surfaceOffset = -0.1f;
        [Tooltip("게임 시작 시 윷 정렬 간격")]
        [SerializeField, Min(0f)] private float startYutSpacing = 0.35f;
        [Tooltip("시작 윷 중심 위치 X/Y 오프셋")]
        [SerializeField] private Vector2 startYutPositionOffset;
        [Tooltip("착지 윷 사이 최소 중심 간격")]
        [SerializeField, Min(0f)] private float minimumYutSpacing = 0.65f;

        [Header("Automatic Overlap Stacking")]
        [Tooltip("윷 몸체 교차 시 자동 위층 배치 여부")]
        [SerializeField] private bool enableOverlapStacking = true;
        [Tooltip("원판 평면 기준 윷 길이")]
        [SerializeField, Min(0.01f)] private float yutFootprintLength = 1.6f;
        [Tooltip("원판 평면 기준 윷 폭")]
        [SerializeField, Min(0.01f)] private float yutFootprintWidth = 0.35f;
        [Tooltip("자동 측정 모델 두께 기준 최소 층 높이")]
        [SerializeField, Min(0.001f)] private float stackLayerHeight = 0.12f;
        [Tooltip("윷 층 사이 추가 여백")]
        [SerializeField, Min(0f)] private float stackClearance = 0.02f;
        [Tooltip("OBJ 긴 방향 로컬 Y축 여부")]
        [SerializeField] private bool modelLongAxisIsY = true;
        [Tooltip("겹친 윷 최대 기울기 각도")]
        [SerializeField, Range(0f, 30f)] private float maximumStackTilt = 12f;
        [Header("Board Placement")]
        [Tooltip("원판 X축 위아래 기울기 각도")]
        [SerializeField, Range(-45f, 45f)] private float boardTiltX = 12f;
        [Tooltip("Board Center 기준 윷 배치 영역 Y축 위치")]
        [SerializeField] private float boardPositionY;

        [Header("Throw Animation")]
        [Tooltip("윷 던지기부터 착지까지의 연출 시간")]
        [SerializeField, Min(0.1f)] private float throwDuration = 0.85f;
        [Tooltip("던지기 포물선 높이")]
        [SerializeField, Min(0f)] private float arcHeight = 1.2f;
        [Tooltip("던지기 카메라 방향 깊이")]
        [SerializeField, Min(0f)] private float depthHop = 0.45f;
        [Tooltip("던지기 최소 및 최대 회전 바퀴 수")]
        [SerializeField] private Vector2 tumbleTurns = new Vector2(2f, 3f);

        [Header("Model")]
        [Tooltip("OBJ 모델 기본 축 회전 보정값")]
        [SerializeField] private Vector3 modelRotationOffset;
        [Tooltip("윷 반대 면 표시 회전값")]
        [SerializeField] private Vector3 faceFlipRotation = new Vector3(180f, 0f, 0f);
        [Tooltip("생성 윷 OBJ 크기 배율")]
        [SerializeField, Min(0.0001f)] private float modelScaleMultiplier = 1f;

        private readonly List<Transform> yuts = new List<Transform>();
        private readonly List<float> yutVisualThicknesses = new List<float>();
        private bool isThrowing;

        private void Awake()
        {
            if (turnManager == null)
            {
                turnManager = FindFirstObjectByType<TestTurnManager>();
            }

            if (boardCenter == null)
            {
                boardCenter = transform;
            }

            CreateYuts();
        }

        private void OnEnable()
        {
            if (turnManager != null) turnManager.OnTurnPhaseChanged += HandleTurnPhaseChanged;
        }

        private void OnDisable()
        {
            if (turnManager != null) turnManager.OnTurnPhaseChanged -= HandleTurnPhaseChanged;
        }

        [ContextMenu("Create Yuts")]
        public void CreateYuts()
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
            if (turn != null && turn.currentPhase == TurnPhase.SaveThrowResult)
            {
                Throw(turn.lastYutResult);
            }
        }

        public void Throw(YutResult result)
        {
            if (!isThrowing && yuts.Count > 0) StartCoroutine(ThrowRoutine(result));
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

            // OBJ 원본 피벗 위치와 관계없이 렌더링되는 윷의 정중앙을 회전축으로 사용한다.
            Vector3 visualCenter = GetVisualCenter(instance);
            GameObject pivotObject = new GameObject(instanceName);
            Transform pivot = pivotObject.transform;
            pivot.SetParent(transform);
            pivot.SetPositionAndRotation(visualCenter, Quaternion.identity);
            instance.transform.SetParent(pivot, true);

            float x = startYutPositionOffset.x + (index - 1.5f) * startYutSpacing;
            float y = -boardRadius * 0.45f + startYutPositionOffset.y;
            pivot.SetPositionAndRotation(
                BoardPoint(new Vector2(x, y), surfaceOffset),
                BoardRotation(index * 12f, false));
            yuts.Add(pivot);
            Vector3 boardNormal = GetBoardFrameRotation() * Vector3.forward;
            yutVisualThicknesses.Add(GetProjectedVisualSize(instance, boardNormal));
        }

        private static Vector3 GetVisualCenter(GameObject instance)
        {
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                return instance.transform.position;
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return bounds.center;
        }

        private static float GetProjectedVisualSize(GameObject instance, Vector3 direction)
        {
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return 0f;

            direction.Normalize();
            float minimum = float.PositiveInfinity;
            float maximum = float.NegativeInfinity;

            foreach (Renderer renderer in renderers)
            {
                Bounds localBounds = renderer.localBounds;
                Vector3 center = localBounds.center;
                Vector3 extents = localBounds.extents;

                for (int x = -1; x <= 1; x += 2)
                {
                    for (int y = -1; y <= 1; y += 2)
                    {
                        for (int z = -1; z <= 1; z += 2)
                        {
                            Vector3 localCorner = center + Vector3.Scale(
                                extents,
                                new Vector3(x, y, z));
                            Vector3 worldCorner = renderer.transform.TransformPoint(localCorner);
                            float projection = Vector3.Dot(worldCorner, direction);
                            minimum = Mathf.Min(minimum, projection);
                            maximum = Mathf.Max(maximum, projection);
                        }
                    }
                }
            }

            return Mathf.Max(0f, maximum - minimum);
        }

        private IEnumerator ThrowRoutine(YutResult result)
        {
            isThrowing = true;
            if (throwButton != null) throwButton.interactable = false;

            int count = yuts.Count;
            var starts = new Vector3[count];
            var targets = new Vector3[count];
            var startRotations = new Quaternion[count];
            var targetRotations = new Quaternion[count];
            var tumbleAxes = new Vector3[count];
            var tumbleAngles = new float[count];
            List<Vector2> landingPoints = CreateLandingPoints(count);
            bool[] flippedFaces = CreateFaceResults(result, count);
            var landingAngles = new float[count];
            var stackLevels = new int[count];
            var stackContactOffsets = new float[count];
            Quaternion boardFrame = GetBoardFrameRotation();
            int minimumTurns = Mathf.Max(1, Mathf.CeilToInt(tumbleTurns.x));
            int maximumTurns = Mathf.Max(minimumTurns, Mathf.FloorToInt(tumbleTurns.y));
            float effectiveStackHeight = stackLayerHeight;
            for (int i = 0; i < yutVisualThicknesses.Count; i++)
            {
                effectiveStackHeight = Mathf.Max(effectiveStackHeight, yutVisualThicknesses[i]);
            }
            effectiveStackHeight += stackClearance;

            for (int i = 0; i < count; i++)
            {
                landingAngles[i] = Random.Range(0f, 360f);
                if (!enableOverlapStacking) continue;

                for (int j = 0; j < i; j++)
                {
                    if (DoYutFootprintsOverlap(
                            landingPoints[i], landingAngles[i],
                            landingPoints[j], landingAngles[j]))
                    {
                        int candidateLevel = stackLevels[j] + 1;
                        if (candidateLevel >= stackLevels[i])
                        {
                            stackLevels[i] = candidateLevel;
                            stackContactOffsets[i] = GetContactOffsetOnTopYut(
                                landingPoints[i], landingAngles[i],
                                landingPoints[j], landingAngles[j]);
                        }
                    }
                }
            }

            for (int i = 0; i < count; i++)
            {
                starts[i] = yuts[i].position;
                float layerDepth = surfaceOffset - stackLevels[i] * effectiveStackHeight - i * 0.002f;
                startRotations[i] = yuts[i].rotation;
                Quaternion landingRotation = BoardRotation(landingAngles[i], flippedFaces[i]);
                float appliedTilt = 0f;
                if (stackLevels[i] > 0 && Mathf.Abs(stackContactOffsets[i]) > 0.001f)
                {
                    GetFootprintAxes(landingAngles[i], out Vector2 longAxis2D, out Vector2 shortAxis2D);
                    Vector3 longAxis3D = boardFrame * new Vector3(longAxis2D.x, longAxis2D.y, 0f);
                    Vector3 shortAxis3D = boardFrame * new Vector3(shortAxis2D.x, shortAxis2D.y, 0f);
                    Vector3 boardNormal = boardFrame * Vector3.forward;
                    float crossSign = Mathf.Sign(Vector3.Dot(
                        Vector3.Cross(shortAxis3D, longAxis3D),
                        boardNormal));
                    float contactRatio = Mathf.Clamp01(
                        Mathf.Abs(stackContactOffsets[i]) / (yutFootprintLength * 0.4f));
                    appliedTilt = -Mathf.Sign(stackContactOffsets[i])
                                * crossSign
                                * maximumStackTilt
                                * contactRatio;
                    landingRotation = Quaternion.AngleAxis(appliedTilt, shortAxis3D)
                                    * landingRotation;
                }
                float tiltDepth = Mathf.Sin(Mathf.Abs(appliedTilt) * Mathf.Deg2Rad)
                                * yutFootprintLength * 0.5f;
                targets[i] = BoardPoint(landingPoints[i], layerDepth - tiltDepth);
                targetRotations[i] = landingRotation;
                Vector3 localTumbleAxis = new Vector3(
                    Random.Range(0.65f, 1f),
                    Random.Range(-1f, 1f),
                    Random.Range(-0.25f, 0.25f)).normalized;
                tumbleAxes[i] = boardFrame * localTumbleAxis;
                tumbleAngles[i] = 360f * Random.Range(minimumTurns, maximumTurns + 1);
            }

            float elapsed = 0f;
            while (elapsed < throwDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / throwDuration);
                float smoothT = t * t * (3f - 2f * t);
                float parabola = 4f * t * (1f - t);
                float spinProgress = 1f - (1f - t) * (1f - t);

                for (int i = 0; i < count; i++)
                {
                    Transform yut = yuts[i];
                    Vector3 position = Vector3.Lerp(starts[i], targets[i], smoothT);
                    position += boardFrame * Vector3.up * (arcHeight * parabola);
                    position -= boardFrame * Vector3.forward * (depthHop * parabola);
                    yut.position = position;

                    Quaternion baseRotation = Quaternion.Slerp(startRotations[i], targetRotations[i], smoothT);
                    yut.rotation = Quaternion.AngleAxis(tumbleAngles[i] * spinProgress, tumbleAxes[i]) * baseRotation;
                }

                yield return null;
            }

            for (int i = 0; i < count; i++)
            {
                yuts[i].SetPositionAndRotation(targets[i], targetRotations[i]);
            }

            isThrowing = false;
            if (throwButton != null)
            {
                throwButton.interactable = turnManager == null
                    || turnManager.CurrentTurn.currentPhase == TurnPhase.WaitThrow;
            }
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

        private List<Vector2> CreateLandingPoints(int count)
        {
            var points = new List<Vector2>(count);
            float usableRadius = Mathf.Max(0.05f, boardRadius - edgePadding);

            for (int i = 0; i < count; i++)
            {
                Vector2 candidate = Vector2.zero;
                for (int attempt = 0; attempt < 40; attempt++)
                {
                    candidate = Random.insideUnitCircle * usableRadius;
                    bool overlaps = false;
                    for (int j = 0; j < points.Count; j++)
                    {
                        if (Vector2.Distance(candidate, points[j]) < minimumYutSpacing)
                        {
                            overlaps = true;
                            break;
                        }
                    }

                    if (!overlaps) break;
                }

                points.Add(candidate);
            }

            return points;
        }

        private bool DoYutFootprintsOverlap(
            Vector2 centerA, float angleA,
            Vector2 centerB, float angleB)
        {
            GetFootprintAxes(angleA, out Vector2 longA, out Vector2 shortA);
            GetFootprintAxes(angleB, out Vector2 longB, out Vector2 shortB);
            Vector2 centerDelta = centerB - centerA;
            float halfLength = yutFootprintLength * 0.5f;
            float halfWidth = yutFootprintWidth * 0.5f;

            return OverlapsOnAxis(centerDelta, longA, longA, shortA, longB, shortB, halfLength, halfWidth)
                && OverlapsOnAxis(centerDelta, shortA, longA, shortA, longB, shortB, halfLength, halfWidth)
                && OverlapsOnAxis(centerDelta, longB, longA, shortA, longB, shortB, halfLength, halfWidth)
                && OverlapsOnAxis(centerDelta, shortB, longA, shortA, longB, shortB, halfLength, halfWidth);
        }

        private float GetContactOffsetOnTopYut(
            Vector2 topCenter, float topAngle,
            Vector2 bottomCenter, float bottomAngle)
        {
            GetFootprintAxes(topAngle, out Vector2 topLong, out _);
            GetFootprintAxes(bottomAngle, out Vector2 bottomLong, out _);
            Vector2 centerDelta = bottomCenter - topCenter;
            float denominator = Cross2D(topLong, bottomLong);
            float halfLength = yutFootprintLength * 0.5f;

            if (Mathf.Abs(denominator) > 0.001f)
            {
                float intersectionOffset = Cross2D(centerDelta, bottomLong) / denominator;
                return Mathf.Clamp(intersectionOffset, -halfLength, halfLength);
            }

            // 거의 평행한 경우에는 아래 윷 중심을 위 윷의 긴 축에 투영해 접촉 방향을 정한다.
            return Mathf.Clamp(Vector2.Dot(centerDelta, topLong), -halfLength, halfLength);
        }

        private static float Cross2D(Vector2 a, Vector2 b)
        {
            return a.x * b.y - a.y * b.x;
        }

        private void GetFootprintAxes(float angle, out Vector2 longAxis, out Vector2 shortAxis)
        {
            float radians = angle * Mathf.Deg2Rad;
            Vector2 localX = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
            Vector2 localY = new Vector2(-Mathf.Sin(radians), Mathf.Cos(radians));
            longAxis = modelLongAxisIsY ? localY : localX;
            shortAxis = modelLongAxisIsY ? localX : localY;
        }

        private static bool OverlapsOnAxis(
            Vector2 centerDelta,
            Vector2 testAxis,
            Vector2 longA,
            Vector2 shortA,
            Vector2 longB,
            Vector2 shortB,
            float halfLength,
            float halfWidth)
        {
            float centerDistance = Mathf.Abs(Vector2.Dot(centerDelta, testAxis));
            float radiusA = halfLength * Mathf.Abs(Vector2.Dot(longA, testAxis))
                          + halfWidth * Mathf.Abs(Vector2.Dot(shortA, testAxis));
            float radiusB = halfLength * Mathf.Abs(Vector2.Dot(longB, testAxis))
                          + halfWidth * Mathf.Abs(Vector2.Dot(shortB, testAxis));
            return centerDistance <= radiusA + radiusB;
        }

        private Vector3 BoardPoint(Vector2 localPoint, float depth)
        {
            Quaternion boardFrame = GetBoardFrameRotation();
            return GetBoardOrigin()
                 + boardFrame * Vector3.right * localPoint.x
                 + boardFrame * Vector3.up * localPoint.y
                 + boardFrame * Vector3.forward * depth;
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
            return boardCenter.rotation * Quaternion.Euler(boardTiltX, 0f, 0f);
        }

        private Vector3 GetBoardOrigin()
        {
            return boardCenter.position + Vector3.up * boardPositionY;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            boardRadius = Mathf.Max(0.1f, boardRadius);
            edgePadding = Mathf.Clamp(edgePadding, 0f, boardRadius - 0.05f);
            startYutSpacing = Mathf.Max(0f, startYutSpacing);
            minimumYutSpacing = Mathf.Max(0f, minimumYutSpacing);
            yutFootprintLength = Mathf.Max(0.01f, yutFootprintLength);
            yutFootprintWidth = Mathf.Max(0.01f, yutFootprintWidth);
            stackLayerHeight = Mathf.Max(0.001f, stackLayerHeight);
            stackClearance = Mathf.Max(0f, stackClearance);
            maximumStackTilt = Mathf.Clamp(maximumStackTilt, 0f, 30f);
            throwDuration = Mathf.Max(0.1f, throwDuration);
            tumbleTurns.y = Mathf.Max(tumbleTurns.x, tumbleTurns.y);
        }

        private void OnDrawGizmosSelected()
        {
            Transform center = boardCenter != null ? boardCenter : transform;
            Quaternion boardFrame = center.rotation * Quaternion.Euler(boardTiltX, 0f, 0f);
            Vector3 boardOrigin = center.position + Vector3.up * boardPositionY;
            Gizmos.color = Color.yellow;
            const int segments = 48;
            Vector3 previous = boardOrigin
                             + boardFrame * Vector3.right * boardRadius
                             + boardFrame * Vector3.forward * surfaceOffset;
            for (int i = 1; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                Vector3 next = boardOrigin
                             + boardFrame * Vector3.right * (Mathf.Cos(angle) * boardRadius)
                             + boardFrame * Vector3.up * (Mathf.Sin(angle) * boardRadius)
                             + boardFrame * Vector3.forward * surfaceOffset;
                Gizmos.DrawLine(previous, next);
                previous = next;
            }
        }
#endif
    }
}
