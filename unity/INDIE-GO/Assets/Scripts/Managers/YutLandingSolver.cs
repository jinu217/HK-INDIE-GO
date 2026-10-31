using System.Collections.Generic;
using UnityEngine;

namespace YutArena.Test
{
    internal struct YutLandingPose
    {
        public int Index;
        public Vector2 Center, Long, Short;
        public float Angle, HalfLength, HalfWidth, HalfThickness, Height, Slope;
        public float TopAt(Vector2 p) => Height + Vector2.Dot(p - Center, Long) * Slope + HalfThickness;
        public Vector2[] Footprint() => new[]
        {
            Center - Long * HalfLength - Short * HalfWidth,
            Center + Long * HalfLength - Short * HalfWidth,
            Center + Long * HalfLength + Short * HalfWidth,
            Center - Long * HalfLength + Short * HalfWidth
        };
    }

    // XY: 판 위 좌표, 높이: 판의 -Z 방향. 결과 배열 순서가 실제 착지 순서
    internal static class YutLandingSolver
    {
        public static YutLandingPose[] Solve(Vector3[] sizes, float[] radii, float radius,
            float padding, float spacing, bool longY, bool stacking, float clearance, float maxTilt)
        {
            var placed = new List<YutLandingPose>();
            var order = new List<int>();
            for (int i = 0; i < sizes.Length; i++) order.Add(i);
            for (int i = order.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }
            foreach (int index in order)
            {
                Vector3 size = sizes[index];
                float available = Mathf.Max(0f, radius - Mathf.Max(padding, radii[index] + 0.04f));
                YutLandingPose best = default;
                float bestScore = float.PositiveInfinity;
                for (int attempt = 0; attempt < 80; attempt++)
                {
                    float angle = Random.Range(0f, 360f);
                    float radians = (angle + (longY ? 90f : 0f)) * Mathf.Deg2Rad;
                    Vector2 axis = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
                    var pose = new YutLandingPose
                    {
                        Index = index, Center = Random.insideUnitCircle * available, Angle = angle,
                        Long = axis, Short = new Vector2(-axis.y, axis.x),
                        // 두께가 있는 모델의 기울어진 끝부분까지 포함하는 보수적 외곽
                        HalfLength = Mathf.Sqrt(Mathf.Pow(longY ? size.y : size.x, 2f) + size.z * size.z) * 0.5f,
                        HalfWidth = (longY ? size.x : size.y) * 0.5f,
                        HalfThickness = size.z * 0.5f
                    };
                    var overlaps = new List<(YutLandingPose lower, List<Vector2> polygon)>();
                    float crowding = 0f;
                    foreach (YutLandingPose lower in placed)
                    {
                        crowding += Mathf.Max(0f, spacing - Vector2.Distance(pose.Center, lower.Center));
                        List<Vector2> polygon = Intersect(pose.Footprint(), lower);
                        if (polygon.Count > 0) overlaps.Add((lower, polygon));
                    }
                    float lowest = float.PositiveInfinity;
                    YutLandingPose supported = pose;
                    // 모든 접촉 꼭짓점의 높이 제약을 만족하는 가장 낮은 받침 자세
                    for (int step = -12; step <= 12; step++)
                    {
                        float tilt = stacking && overlaps.Count > 0 ? maxTilt * step / 12f : 0f;
                        float slope = Mathf.Tan(tilt * Mathf.Deg2Rad);
                        float halfThickness = size.z * 0.5f / Mathf.Cos(tilt * Mathf.Deg2Rad);
                        float height = halfThickness + Mathf.Abs(slope) * pose.HalfLength;
                        if (stacking)
                            foreach (var contact in overlaps)
                                foreach (Vector2 point in contact.polygon)
                                    height = Mathf.Max(height, contact.lower.TopAt(point) + clearance
                                        + halfThickness - Vector2.Dot(point - pose.Center, pose.Long) * slope);
                        float cost = height + Mathf.Abs(tilt) * 0.0005f;
                        if (cost >= lowest) continue;
                        lowest = cost;
                        supported = pose;
                        supported.Slope = slope;
                        supported.Height = height;
                        supported.HalfThickness = halfThickness;
                    }
                    float score = supported.Height * 2f + crowding * 2f + overlaps.Count * 0.06f;
                    if (score >= bestScore) continue;
                    bestScore = score;
                    best = supported;
                }
                placed.Add(best);
            }
            return placed.ToArray();
        }

        private static List<Vector2> Intersect(Vector2[] subject, YutLandingPose clip)
        {
            var polygon = new List<Vector2>(subject);
            Clip(ref polygon, clip.Center, clip.Long, clip.HalfLength);
            Clip(ref polygon, clip.Center, -clip.Long, clip.HalfLength);
            Clip(ref polygon, clip.Center, clip.Short, clip.HalfWidth);
            Clip(ref polygon, clip.Center, -clip.Short, clip.HalfWidth);
            return polygon;
        }

        private static void Clip(ref List<Vector2> polygon, Vector2 center, Vector2 normal, float limit)
        {
            if (polygon.Count == 0) return;
            var output = new List<Vector2>();
            Vector2 previous = polygon[polygon.Count - 1];
            float previousDistance = Vector2.Dot(previous - center, normal) - limit;
            foreach (Vector2 current in polygon)
            {
                float distance = Vector2.Dot(current - center, normal) - limit;
                if ((distance <= 0f) != (previousDistance <= 0f))
                    output.Add(Vector2.Lerp(previous, current, previousDistance / (previousDistance - distance)));
                if (distance <= 0f) output.Add(current);
                previous = current;
                previousDistance = distance;
            }
            polygon = output;
        }
    }
}
