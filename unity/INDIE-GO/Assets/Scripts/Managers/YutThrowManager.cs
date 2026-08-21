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
        [SerializeField] private Button throwButton;
        [SerializeField] private TestTurnManager turnManager;
        [SerializeField] private GameObject normalYutPrefab;
        [SerializeField] private GameObject backDoYutPrefab;
        [Tooltip("원판 중앙에 놓고 로컬 X/Y축이 원판 표면을 따르도록 설정")]
        [SerializeField] private Transform boardCenter;

        [Header("Board Area (Local XY)")]
        [SerializeField, Min(0.1f)] private float boardRadius = 2.5f;
        [SerializeField, Min(0f)] private float edgePadding = 0.35f;
        [Tooltip("카메라가 -Z에 있으면 보통 음수 사용")]
        [SerializeField] private float surfaceOffset = -0.1f;
        [SerializeField, Min(0f)] private float minimumYutSpacing = 0.65f;

        [Header("Throw Animation")]
        [SerializeField, Min(0.1f)] private float throwDuration = 0.85f;
        [SerializeField, Min(0f)] private float arcHeight = 1.2f;
        [SerializeField, Min(0f)] private float depthHop = 0.45f;
        [SerializeField] private Vector2 tumbleTurns = new Vector2(1.5f, 3f);

        [Header("Model")]
        [SerializeField] private Vector3 modelRotationOffset;
        [Tooltip("윷의 반대 면을 보이게 하는 회전. 모델 축에 따라 (180,0,0) 또는 (0,180,0) 사용")]
        [SerializeField] private Vector3 faceFlipRotation = new Vector3(180f, 0f, 0f);
        [SerializeField, Min(0.0001f)] private float modelScaleMultiplier = 1f;

        private readonly List<Transform> yuts = new List<Transform>();
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
            instance.name = instanceName;
            instance.transform.localScale *= modelScaleMultiplier;

            Rigidbody body = instance.GetComponent<Rigidbody>();
            if (body != null)
            {
                body.isKinematic = true;
                body.detectCollisions = false;
            }

            float x = (index - 1.5f) * minimumYutSpacing;
            float y = -boardRadius * 0.45f;
            instance.transform.SetPositionAndRotation(
                BoardPoint(new Vector2(x, y), surfaceOffset),
                BoardRotation(index * 12f, false));
            yuts.Add(instance.transform);
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

            for (int i = 0; i < count; i++)
            {
                starts[i] = yuts[i].position;
                targets[i] = BoardPoint(landingPoints[i], surfaceOffset - i * 0.002f);
                startRotations[i] = yuts[i].rotation;
                targetRotations[i] = BoardRotation(Random.Range(0f, 360f), flippedFaces[i]);
                tumbleAxes[i] = Random.onUnitSphere;
                tumbleAngles[i] = 360f * Random.Range(tumbleTurns.x, tumbleTurns.y);
            }

            float elapsed = 0f;
            while (elapsed < throwDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / throwDuration);
                float smoothT = t * t * (3f - 2f * t);
                float parabola = 4f * t * (1f - t);

                for (int i = 0; i < count; i++)
                {
                    Transform yut = yuts[i];
                    Vector3 position = Vector3.Lerp(starts[i], targets[i], smoothT);
                    position += boardCenter.up * (arcHeight * parabola);
                    position -= boardCenter.forward * (depthHop * parabola);
                    yut.position = position;

                    Quaternion baseRotation = Quaternion.Slerp(startRotations[i], targetRotations[i], smoothT);
                    yut.rotation = Quaternion.AngleAxis(tumbleAngles[i] * t, tumbleAxes[i]) * baseRotation;
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

        private Vector3 BoardPoint(Vector2 localPoint, float depth)
        {
            return boardCenter.position
                 + boardCenter.right * localPoint.x
                 + boardCenter.up * localPoint.y
                 + boardCenter.forward * depth;
        }

        private Quaternion BoardRotation(float angle, bool flipFace)
        {
            return boardCenter.rotation
                 * Quaternion.AngleAxis(angle, Vector3.forward)
                 * Quaternion.Euler(flipFace ? faceFlipRotation : Vector3.zero)
                 * Quaternion.Euler(modelRotationOffset);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            boardRadius = Mathf.Max(0.1f, boardRadius);
            edgePadding = Mathf.Clamp(edgePadding, 0f, boardRadius - 0.05f);
            minimumYutSpacing = Mathf.Max(0f, minimumYutSpacing);
            throwDuration = Mathf.Max(0.1f, throwDuration);
            tumbleTurns.y = Mathf.Max(tumbleTurns.x, tumbleTurns.y);
        }

        private void OnDrawGizmosSelected()
        {
            Transform center = boardCenter != null ? boardCenter : transform;
            Gizmos.color = Color.yellow;
            const int segments = 48;
            Vector3 previous = center.position + center.right * boardRadius + center.forward * surfaceOffset;
            for (int i = 1; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                Vector3 next = center.position
                             + center.right * (Mathf.Cos(angle) * boardRadius)
                             + center.up * (Mathf.Sin(angle) * boardRadius)
                             + center.forward * surfaceOffset;
                Gizmos.DrawLine(previous, next);
                previous = next;
            }
        }
#endif
    }
}
