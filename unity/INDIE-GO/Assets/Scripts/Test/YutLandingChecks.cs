#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEngine;
using YutArena.Common;

namespace YutArena.Test
{
    public static class YutLandingChecks
    {
        // CLI에서 실행 가능한 재현성 있는 착지 기하 검증, 플레이어 빌드 제외
        public static string Run()
        {
            UnityEngine.Random.State state = UnityEngine.Random.state;
            int contacts = 0;
            try
            {
                for (int seed = 0; seed < 100; seed++)
                {
                    UnityEngine.Random.InitState(seed);
                    bool longY = seed % 2 == 0;
                    Vector3 size = longY ? new Vector3(0.25f, 1.37f, 0.19f) : new Vector3(1.37f, 0.25f, 0.19f);
                    var poses = YutLandingSolver.Solve(new[] { size, size, size, size },
                        new[] { 0.705f, 0.705f, 0.705f, 0.705f }, 2f, 1.3f, 0.65f, longY, true, 0.02f, 12f);
                    for (int i = 0; i < poses.Length; i++)
                    {
                        var p = poses[i];
                        if (float.IsNaN(p.Height)) throw new Exception("Invalid landing height");
                        // 판 표면 관통 및 모델 전체의 원형 범위 이탈 검사
                        if (p.Height - Mathf.Abs(p.Slope) * p.HalfLength - p.HalfThickness < -0.0001f)
                            throw new Exception("Board penetration");
                        if (p.Center.magnitude + 0.705f > 2.0001f) throw new Exception("Outside board");
                        for (int j = 0; j < i; j++)
                        {
                            var lower = poses[j];
                            // 독립적인 격자 표본으로 교차 구간의 상하 분리 검사
                            for (int x = 0; x <= 16; x++)
                            for (int y = 0; y <= 6; y++)
                            {
                                Vector2 point = p.Center + p.Long * Mathf.Lerp(-p.HalfLength, p.HalfLength, x / 16f)
                                    + p.Short * Mathf.Lerp(-p.HalfWidth, p.HalfWidth, y / 6f);
                                Vector2 delta = point - lower.Center;
                                if (Mathf.Abs(Vector2.Dot(delta, lower.Long)) > lower.HalfLength
                                    || Mathf.Abs(Vector2.Dot(delta, lower.Short)) > lower.HalfWidth) continue;
                                contacts++;
                                float bottom = p.TopAt(point) - p.HalfThickness * 2f;
                                if (bottom < lower.TopAt(point) + 0.0199f)
                                    throw new Exception("Yut penetration at seed " + seed);
                            }
                        }
                    }
                }
                var method = typeof(YutThrowManager).GetMethod("CreateFaceResults", BindingFlags.Static | BindingFlags.NonPublic);
                var results = new[] { YutResult.BackDo, YutResult.Do, YutResult.Gae, YutResult.Geol, YutResult.Yut, YutResult.Mo };
                var expected = new[] { 1, 1, 2, 3, 4, 0 };
                for (int r = 0; r < results.Length; r++)
                {
                    var faces = (bool[])method.Invoke(null, new object[] { results[r], 4 });
                    int originals = 0;
                    foreach (bool flipped in faces) if (!flipped) originals++;
                    if (originals != expected[r] || (r == 0 && faces[3]) || (r == 1 && !faces[3]))
                        throw new Exception("Incorrect result face: " + results[r]);
                }
                return "PASS: 100 layouts, " + contacts + " overlap samples, 6 result faces";
            }
            finally { UnityEngine.Random.state = state; }
        }
    }
}
#endif
