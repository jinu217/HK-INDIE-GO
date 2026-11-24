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
    }

    // XY: 판 위 좌표, 높이: 판의 -Z 방향. 결과 배열 순서가 실제 착지 순서
    internal static class YutLandingSolver
    {
        public static YutLandingPose[] Solve(Vector3[] sizes, float[] radii, float radius,
            float padding, float spacing, bool longY, bool stacking, float clearance, float maxTilt)
        {
            var placed = new YutLandingPose[sizes.Length];
            var order = new int[sizes.Length];
            for (int i = 0; i < order.Length; i++) order[i] = i;
            for (int i = order.Length - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }
            // 두 사각형의 교집합은 최대 팔각형이다. 후보마다 같은 버퍼를 다시 쓴다.
            var polygon = new List<Vector2>(8);
            var scratch = new List<Vector2>(8);
            var contacts = new List<(float supportHeight, float longOffset)>(8 * sizes.Length);
            for (int placedCount = 0; placedCount < order.Length; placedCount++)
            {
                int index = order[placedCount];
                Vector3 size = sizes[index];
                float available = Mathf.Max(0f, radius - Mathf.Max(padding, radii[index] + 0.04f));
                // 두께가 있는 모델의 기울어진 끝부분까지 포함하는 보수적 외곽
                float halfLength = Mathf.Sqrt(Mathf.Pow(longY ? size.y : size.x, 2f) + size.z * size.z) * 0.5f;
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
                        HalfLength = halfLength,
                        HalfWidth = (longY ? size.x : size.y) * 0.5f,
                        HalfThickness = size.z * 0.5f
                    };
                    contacts.Clear();
                    int overlapCount = 0;
                    float crowding = 0f;
                    for (int j = 0; j < placedCount; j++)
                    {
                        YutLandingPose lower = placed[j];
                        crowding += Mathf.Max(0f, spacing - Vector2.Distance(pose.Center, lower.Center));
                        Intersect(pose, lower, polygon, scratch);
                        if (polygon.Count == 0) continue;
                        overlapCount++;
                        if (stacking)
                            foreach (Vector2 point in polygon)
                                contacts.Add((lower.TopAt(point) + clearance, Vector2.Dot(point - pose.Center, pose.Long)));
                    }
                    YutLandingPose supported = FindSupportedPose(pose, contacts, maxTilt);
                    float score = supported.Height * 2f + crowding * 2f + overlapCount * 0.06f;
                    if (score >= bestScore) continue;
                    bestScore = score;
                    best = supported;
                }
                placed[placedCount] = best;
            }
            return placed;
        }

        private static YutLandingPose FindSupportedPose(YutLandingPose pose,
            List<(float supportHeight, float longOffset)> contacts, float maxTilt)
        {
            pose.Height = pose.HalfThickness;
            if (contacts.Count == 0) return pose;

            YutLandingPose best = pose;
            float lowest = float.PositiveInfinity;
            int steps = maxTilt == 0f ? 0 : 12;
            for (int step = -steps; step <= steps; step++)
            {
                float tilt = maxTilt * step / 12f;
                float slope = Mathf.Tan(tilt * Mathf.Deg2Rad);
                float halfThickness = pose.HalfThickness / Mathf.Cos(tilt * Mathf.Deg2Rad);
                float height = halfThickness + Mathf.Abs(slope) * pose.HalfLength;
                foreach (var contact in contacts)
                    height = Mathf.Max(height, contact.supportHeight + halfThickness - contact.longOffset * slope);
                float cost = height + Mathf.Abs(tilt) * 0.0005f;
                if (cost >= lowest) continue;
                lowest = cost;
                best.Slope = slope;
                best.Height = height;
                best.HalfThickness = halfThickness;
            }
            return best;
        }

        private static void Intersect(YutLandingPose subject, YutLandingPose clip,
            List<Vector2> polygon, List<Vector2> scratch)
        {
            Vector2 length = subject.Long * subject.HalfLength;
            Vector2 width = subject.Short * subject.HalfWidth;
            polygon.Clear();
            polygon.Add(subject.Center - length - width);
            polygon.Add(subject.Center + length - width);
            polygon.Add(subject.Center + length + width);
            polygon.Add(subject.Center - length + width);
            Clip(polygon, scratch, clip.Center, clip.Long, clip.HalfLength);
            Clip(scratch, polygon, clip.Center, -clip.Long, clip.HalfLength);
            Clip(polygon, scratch, clip.Center, clip.Short, clip.HalfWidth);
            Clip(scratch, polygon, clip.Center, -clip.Short, clip.HalfWidth);
        }

        private static void Clip(List<Vector2> polygon, List<Vector2> output, Vector2 center, Vector2 normal, float limit)
        {
            output.Clear();
            if (polygon.Count == 0) return;
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
        }
    }
}
