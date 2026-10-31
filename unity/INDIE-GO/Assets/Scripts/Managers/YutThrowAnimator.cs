using System.Collections;
using UnityEngine;
using YutArena.Managers;

namespace YutArena.Test
{
    internal struct YutFlight
    {
        public Transform Pivot;
        public Vector3 Start, Target, Control;
        public Quaternion StartRotation, TargetRotation;
        public Vector3 SpinAxis;
        public Vector3 RockAxis;
        public YutVisual Visual;
        public float Duration, Height, Turns;
    }

    internal static class YutThrowAnimator
    {
        public static IEnumerator Play(YutFlight[] flights, Vector3 origin, Quaternion frame,
            float settleDuration, float bounce, float rockAngle)
        {
            Vector3 normal = frame * Vector3.back;
            float elapsed = 0f;
            bool active = true;
            while (active)
            {
                active = false;
                elapsed += Time.deltaTime * GeneralSettingsManager.PresentationSpeedMultiplier;
                foreach (YutFlight flight in flights)
                {
                    if (flight.Pivot == null) continue;
                    float t = Mathf.Clamp01(elapsed / flight.Duration);
                    if (t < 1f)
                    {
                        active = true;
                        // 판 위 이동과 판에서 뜬 높이를 분리한 포물선
                        Vector3 position = (1f - t) * (1f - t) * flight.Start
                            + 2f * (1f - t) * t * flight.Control + t * t * flight.Target;
                        position += normal * (4f * flight.Height * t * (1f - t));
                        // 공중 회전 속도 유지, 마지막 구간에서만 접촉 자세로 연결
                        float alignment = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1f, t));
                        Quaternion rotation = Quaternion.AngleAxis(360f * flight.Turns * t, flight.SpinAxis)
                            * Quaternion.Slerp(flight.StartRotation, flight.TargetRotation, alignment);
                        // 비행 중 회전하는 모델의 끝이 기준 표면 아래로 들어가지 않도록 보정
                        float bottomClearance = flight.Visual.Measure(Quaternion.Inverse(frame) * rotation).z * 0.5f;
                        float height = Vector3.Dot(position - origin, normal);
                        position += normal * Mathf.Max(0f, bottomClearance - height);
                        flight.Pivot.SetPositionAndRotation(position, rotation);
                    }
                    else
                    {
                        float s = Mathf.Clamp01((elapsed - flight.Duration) / Mathf.Max(0.01f, settleDuration));
                        active |= s < 1f;
                        // 낮은 반동과 감쇠 흔들림, 회전 변위만큼 높이 확보
                        float lift = bounce * Mathf.Abs(Mathf.Sin(s * Mathf.PI * 2f)) * (1f - s) * (1f - s);
                        float rock = rockAngle * Mathf.Sin(s * Mathf.PI * 4f) * (1f - s) * (1f - s);
                        lift += 2f * flight.Visual.Radius * Mathf.Sin(Mathf.Abs(rock) * Mathf.Deg2Rad * 0.5f) * 1.25f;
                        flight.Pivot.SetPositionAndRotation(flight.Target + normal * lift,
                            Quaternion.AngleAxis(rock, flight.RockAxis) * flight.TargetRotation);
                    }
                }
                yield return null;
            }
        }
    }
}
