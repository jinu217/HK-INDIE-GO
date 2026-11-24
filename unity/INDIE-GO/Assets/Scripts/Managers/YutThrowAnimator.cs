using System.Collections;
using UnityEngine;
using YutArena.Managers;

namespace YutArena.Test
{
    internal struct YutFlight
    {
        public Transform Pivot;
        public Vector3 Start, Target, Control;
        public Quaternion StartRotation, TargetRotation, SpinEndReference;
        public Vector3 SpinAxis;
        public Vector3 RockAxis;
        public YutVisual Visual;
        public float Duration, Height, Turns;
        public float ReboundHeight, RockDegrees, SettleTime;
    }

    internal static class YutThrowAnimator
    {
        private static Quaternion FlightRotation(YutFlight flight, float t)
        {
            // 자세 보정을 비행 전체에 나눠 하강 중에도 회전을 이어간다.
            Quaternion reference = Quaternion.Slerp(flight.StartRotation, flight.SpinEndReference, t);
            return Quaternion.AngleAxis(360f * flight.Turns * t, flight.SpinAxis) * reference;
        }

        private static void PrepareLanding(ref YutFlight flight, Vector3 normal, float settleDuration,
            float bounce, float rockAngle)
        {
            // 포물선 끝의 하강 속도와 착지 후 흔들림 축 방향의 회전량을 사용한다.
            float downSpeed = (4f * flight.Height + 2f * Vector3.Dot(flight.Control - flight.Target, normal))
                / flight.Duration;
            float diameter = Mathf.Max(0.001f, 2f * flight.Visual.Radius);
            float impact = Mathf.Clamp01(downSpeed * settleDuration / diameter);
            float rockingTurns = flight.Turns * Vector3.Dot(flight.SpinAxis, flight.RockAxis)
                * settleDuration / flight.Duration;
            // 착지 시간 동안 1/4바퀴를 돌 속도면 설정된 최대 흔들림을 준다.
            float spin = Mathf.Clamp(4f * rockingTurns, -1f, 1f);
            flight.ReboundHeight = bounce * impact;
            flight.RockDegrees = rockAngle * spin;
            flight.SettleTime = Mathf.Max(0.01f, settleDuration * Mathf.Lerp(0.5f, 1f,
                Mathf.Max(impact, Mathf.Abs(spin))));
        }

        public static IEnumerator Play(YutFlight[] flights, Vector3 origin, Quaternion frame,
            float settleDuration, float bounce, float rockAngle)
        {
            Vector3 normal = frame * Vector3.back;
            Quaternion inverseFrame = Quaternion.Inverse(frame);
            for (int i = 0; i < flights.Length; i++)
                if (flights[i].Pivot != null)
                    PrepareLanding(ref flights[i], normal, settleDuration, bounce, rockAngle);
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
                        Quaternion rotation = FlightRotation(flight, t);
                        // 비행 중 회전하는 모델의 끝이 기준 표면 아래로 들어가지 않도록 보정
                        float height = Vector3.Dot(position - origin, normal);
                        if (height < flight.Visual.Radius)
                        {
                            float bottomClearance = flight.Visual.Measure(inverseFrame * rotation).z * 0.5f;
                            position += normal * Mathf.Max(0f, bottomClearance - height);
                        }
                        flight.Pivot.SetPositionAndRotation(position, rotation);
                    }
                    else
                    {
                        float s = Mathf.Clamp01((elapsed - flight.Duration) / flight.SettleTime);
                        active |= s < 1f;
                        // 낮은 반동과 감쇠 흔들림, 회전 변위만큼 높이 확보
                        float damping = (1f - s) * (1f - s);
                        float lift = flight.ReboundHeight * 4f * s * (1f - s);
                        float rock = flight.RockDegrees * Mathf.Sin(s * Mathf.PI * 2f) * damping;
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
