using System.Collections.Generic;
using UnityEngine;

namespace YutArena.Test
{
    // 모델의 원본 피벗과 무관한 시각 중심 및 실제 크기
    internal sealed class YutVisual
    {
        public Transform Pivot { get; }
        public float Radius { get; private set; }
        private readonly Renderer[] renderers;
        private readonly List<Vector3> corners = new List<Vector3>();

        public YutVisual(GameObject model, Transform parent, string name)
        {
            renderers = model.GetComponentsInChildren<Renderer>(true);
            Bounds bounds = new Bounds(model.transform.position, Vector3.zero);
            for (int i = 0; i < renderers.Length; i++)
                if (i == 0) bounds = renderers[i].bounds;
                else bounds.Encapsulate(renderers[i].bounds);
            Pivot = new GameObject(name).transform;
            Pivot.SetParent(parent, true);
            Pivot.SetPositionAndRotation(bounds.center, Quaternion.identity);
            model.transform.SetParent(Pivot, true);
            foreach (Renderer renderer in renderers)
            {
                Bounds local = renderer.localBounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = local.center + Vector3.Scale(local.extents,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    // 회전과 크기를 반영한 월드 단위, 피벗의 초기 회전은 단위 회전
                    Vector3 offset = renderer.transform.TransformPoint(corner) - Pivot.position;
                    corners.Add(offset);
                    Radius = Mathf.Max(Radius, offset.magnitude);
                }
            }
        }

        public Vector3 Measure(Quaternion rotation)
        {
            Vector3 extents = Vector3.zero;
            foreach (Vector3 corner in corners)
            {
                Vector3 p = rotation * corner;
                extents = Vector3.Max(extents, new Vector3(Mathf.Abs(p.x), Mathf.Abs(p.y), Mathf.Abs(p.z)));
            }
            return extents * 2f;
        }

        public void SetVisible(bool visible)
        {
            foreach (Renderer renderer in renderers)
                if (renderer != null) renderer.enabled = visible;
        }
    }
}
