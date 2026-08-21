using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace YutArena.Test
{
    /// <summary>
    /// 일반 윷 프리팹 3개와 백도 윷 프리팹 1개를 생성하고 UI 버튼으로 물리 던지기를 시험한다.
    /// 기존 게임 로직과 연결되지 않는 독립 테스트 컴포넌트다.
    /// </summary>
    public sealed class YutThrowTest : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Button throwButton;
        [Tooltip("일반 윷가락 1개짜리 프리팹")]
        [SerializeField] private GameObject normalYutPrefab;
        [Tooltip("백도 표시가 있는 윷가락 1개짜리 프리팹")]
        [SerializeField] private GameObject backDoYutPrefab;
        [SerializeField] private Transform spawnPoint;

        [Header("Spawn")]
        [SerializeField, Min(1)] private int normalYutCount = 3;
        [SerializeField, Min(0f)] private float spawnSpacing = 0.18f;

        [Header("Throw")]
        [Tooltip("윷이 위로 출발하는 속도. 2.5면 약 0.3 유닛 높이까지 올라갑니다.")]
        [SerializeField] private float upwardImpulse = 2.5f;
        [SerializeField] private float upwardRandomness = 0.35f;
        [SerializeField] private float scatterImpulse = 0.45f;
        [SerializeField] private float torqueImpulse = 8f;
        [SerializeField] private float minimumThrowInterval = 1f;

        [Header("Automatic Physics Setup")]
        [SerializeField] private bool addRigidbodyIfMissing = true;
        [SerializeField] private bool addBoxColliderIfMissing = true;
        [SerializeField, Min(0.01f)] private float mass = 0.15f;

        private readonly List<Rigidbody> yutBodies = new List<Rigidbody>();
        private readonly List<Vector3> yutSpawnOffsets = new List<Vector3>();
        private float nextThrowTime;

        private void Awake()
        {
            if (spawnPoint == null)
            {
                spawnPoint = transform;
            }

            CreateYuts();
        }

        private void OnEnable()
        {
            if (throwButton != null)
            {
                throwButton.onClick.AddListener(Throw);
            }
        }

        private void OnDisable()
        {
            if (throwButton != null)
            {
                throwButton.onClick.RemoveListener(Throw);
            }
        }

        [ContextMenu("Create Yuts")]
        public void CreateYuts()
        {
            if ((normalYutPrefab == null && backDoYutPrefab == null) || yutBodies.Count > 0)
            {
                return;
            }

            int totalCount = (normalYutPrefab != null ? normalYutCount : 0)
                           + (backDoYutPrefab != null ? 1 : 0);
            float centerOffset = (totalCount - 1) * 0.5f;
            int spawnIndex = 0;

            for (int i = 0; i < normalYutCount && normalYutPrefab != null; i++)
            {
                SpawnYut(normalYutPrefab, $"TestYut_Normal_{i + 1}", spawnIndex++, centerOffset);
            }

            if (backDoYutPrefab != null)
            {
                SpawnYut(backDoYutPrefab, "TestYut_BackDo", spawnIndex, centerOffset);
            }
        }

        private void SpawnYut(GameObject prefab, string instanceName, int index, float centerOffset)
        {
            Vector3 localOffset = Vector3.right * ((index - centerOffset) * spawnSpacing);
            GameObject instance = Instantiate(
                prefab,
                spawnPoint.TransformPoint(localOffset),
                spawnPoint.rotation,
                transform);

            instance.name = instanceName;
            Rigidbody body = PreparePhysics(instance);
            if (body != null)
            {
                body.isKinematic = true;
                yutBodies.Add(body);
                yutSpawnOffsets.Add(localOffset);
            }
        }

        /// <summary>Button OnClick에서도 직접 연결할 수 있는 공개 메서드.</summary>
        public void Throw()
        {
            if (Time.time < nextThrowTime)
            {
                return;
            }

            if (yutBodies.Count == 0)
            {
                CreateYuts();
            }

            nextThrowTime = Time.time + minimumThrowInterval;

            for (int i = 0; i < yutBodies.Count; i++)
            {
                Rigidbody body = yutBodies[i];
                if (body == null)
                {
                    continue;
                }

                body.isKinematic = true;
                body.position = spawnPoint.TransformPoint(yutSpawnOffsets[i]);
                body.rotation = spawnPoint.rotation;
                body.isKinematic = false;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.WakeUp();

                float up = upwardImpulse + Random.Range(-upwardRandomness, upwardRandomness);
                Vector2 scatter = Random.insideUnitCircle * scatterImpulse;
                Vector3 impulse = Vector3.up * Mathf.Max(0f, up)
                                  + new Vector3(scatter.x, 0f, scatter.y);
                body.AddForce(impulse, ForceMode.VelocityChange);
                body.AddTorque(Random.onUnitSphere * torqueImpulse, ForceMode.VelocityChange);
            }
        }

        private Rigidbody PreparePhysics(GameObject instance)
        {
            Rigidbody body = instance.GetComponent<Rigidbody>();
            if (body == null && addRigidbodyIfMissing)
            {
                body = instance.AddComponent<Rigidbody>();
            }

            if (body != null)
            {
                body.mass = mass;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                body.interpolation = RigidbodyInterpolation.Interpolate;
            }

            if (addBoxColliderIfMissing && instance.GetComponentInChildren<Collider>() == null)
            {
                AddBoundsCollider(instance);
            }

            return body;
        }

        private static void AddBoundsCollider(GameObject instance)
        {
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                instance.AddComponent<BoxCollider>();
                return;
            }

            Bounds worldBounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                worldBounds.Encapsulate(renderers[i].bounds);
            }

            BoxCollider box = instance.AddComponent<BoxCollider>();
            box.center = instance.transform.InverseTransformPoint(worldBounds.center);

            Vector3 scale = instance.transform.lossyScale;
            box.size = new Vector3(
                SafeDivide(worldBounds.size.x, scale.x),
                SafeDivide(worldBounds.size.y, scale.y),
                SafeDivide(worldBounds.size.z, scale.z));
        }

        private static float SafeDivide(float value, float divisor)
        {
            return Mathf.Abs(divisor) > Mathf.Epsilon ? value / Mathf.Abs(divisor) : value;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            normalYutCount = Mathf.Max(1, normalYutCount);
            mass = Mathf.Max(0.01f, mass);
            minimumThrowInterval = Mathf.Max(0f, minimumThrowInterval);
        }
#endif
    }
}
