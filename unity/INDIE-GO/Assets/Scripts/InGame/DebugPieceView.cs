using UnityEngine;

namespace YutArena.InGame
{
    public sealed class DebugPieceView : MonoBehaviour
    {
        public int PlayerId { get; private set; }
        public int PieceId { get; private set; }
        private Vector3 initialScale;

        private void Awake()
        {
            initialScale = transform.localScale;
        }

        public void Configure(int playerId, int pieceId)
        {
            PlayerId = playerId;
            PieceId = pieceId;
            gameObject.name = $"Piece_P{playerId}_{pieceId + 1}";
        }

        /// <summary>
        /// InGamePieceDebugController의 Inspector 배율을 말 프리팹에 적용합니다.
        /// 이후 선택 강조가 발생해도 이 크기를 기준으로 유지됩니다.
        /// </summary>
        public void SetBaseScaleMultiplier(float multiplier)
        {
            initialScale *= Mathf.Max(0.01f, multiplier);
            transform.localScale = initialScale;
        }

        public void SetSelected(bool selected)
        {
            transform.localScale = initialScale * (selected ? 1.1f : 1f);
        }

        public void AttachFoothold(GameObject foothold)
        {
            if (foothold == null) return;
            EnsureSelectionCollider(foothold);
        }

        public static void EnsureSelectionCollider(GameObject visualObject)
        {
            if (visualObject == null || visualObject.GetComponentInChildren<Collider>(true) != null)
                return;

            Renderer[] renderers = visualObject.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                return;

            Transform visualTransform = visualObject.transform;
            Bounds localBounds = default;
            bool hasBounds = false;
            foreach (Renderer visualRenderer in renderers)
            {
                Bounds rendererBounds = visualRenderer.localBounds;
                Vector3 center = rendererBounds.center;
                Vector3 extents = rendererBounds.extents;
                for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 rendererLocalCorner = center + Vector3.Scale(extents, new Vector3(x, y, z));
                    Vector3 visualLocalCorner = visualTransform.InverseTransformPoint(
                        visualRenderer.transform.TransformPoint(rendererLocalCorner));
                    if (!hasBounds)
                    {
                        localBounds = new Bounds(visualLocalCorner, Vector3.zero);
                        hasBounds = true;
                    }
                    else
                    {
                        localBounds.Encapsulate(visualLocalCorner);
                    }
                }
            }

            if (!hasBounds || localBounds.size.sqrMagnitude <= Mathf.Epsilon)
                return;

            BoxCollider selectionCollider = visualObject.AddComponent<BoxCollider>();
            selectionCollider.center = localBounds.center;
            selectionCollider.size = localBounds.size;
            selectionCollider.isTrigger = true;
        }

        public static bool TryFindAtScreenPosition(
            Camera selectionCamera,
            Vector2 screenPosition,
            out DebugPieceView selectedView)
        {
            selectedView = null;
            if (selectionCamera == null)
                return false;

            Ray pointerRay = selectionCamera.ScreenPointToRay(screenPosition);
            float nearestDistance = float.PositiveInfinity;
            DebugPieceView[] candidates = FindObjectsByType<DebugPieceView>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

            foreach (DebugPieceView candidate in candidates)
            {
                if (!candidate.TryGetPointerHitDistance(pointerRay, selectionCamera, out float distance) ||
                    distance >= nearestDistance) continue;

                selectedView = candidate;
                nearestDistance = distance;
            }

            return selectedView != null;
        }

        private bool TryGetPointerHitDistance(Ray pointerRay, Camera selectionCamera, out float distance)
        {
            distance = float.PositiveInfinity;
            bool wasHit = false;

            foreach (Collider modelCollider in GetComponentsInChildren<Collider>(false))
            {
                if (!modelCollider.enabled ||
                    !modelCollider.Raycast(pointerRay, out RaycastHit hit, selectionCamera.farClipPlane) ||
                    hit.distance >= distance) continue;

                distance = hit.distance;
                wasHit = true;
            }

            return wasHit;
        }
    }
}
