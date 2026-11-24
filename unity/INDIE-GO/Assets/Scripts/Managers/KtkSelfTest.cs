using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using YutArena.Common;
using YutArena.Managers.GameProgress;

namespace YutArena.Managers
{
    // ===================================================================
    // 킬 더 킹 자동 확인 (TestWinConditionManager 우클릭 메뉴에서 실행)
    // 진짜 게임은 건드리지 않고, 가짜 게임(FakeHost) 위에 KillTheKingManager를 새로 만들어
    // 기획서 규칙대로 숫자가 나오는지 확인함. 플레이 안 하고 에디터에서 바로 눌러도 됨
    // 결과: Console에 [킬더킹 자동 테스트] ✅ / ❌ 와 통과 개수
    // ===================================================================
    public static class KtkSelfTest
    {
        private const string Tag = "[킬더킹 자동 테스트]";
        private const TeamSlot A = TeamSlot.TeamA, B = TeamSlot.TeamB, C = TeamSlot.TeamC, D = TeamSlot.TeamD;
        private static int passCount, failCount;

        // 가짜 게임: 턴 순서 P1 -> P2 -> P3 -> P4 (= TeamA -> B -> C -> D), 탈락하면 순서에서 빠짐
        private class FakeHost : IKtkHost
        {
            private readonly GameStartSettings settings;
            public GameStartSettings Settings => settings;
            public readonly List<PlayerSlot> Order = new List<PlayerSlot>();
            public readonly HashSet<TeamSlot> Active = new HashSet<TeamSlot>();
            public TeamSlot Winner = TeamSlot.None;
            public GameResultType WinType = GameResultType.None;

            public FakeHost(GameStartSettings settings)
            {
                this.settings = settings;
                for (int i = 1; i <= settings.playerCount; i++)
                {
                    Order.Add((PlayerSlot)i);
                    Active.Add(MatchCompositionRule.GetPersonTeamSlot(settings, (PlayerSlot)i));
                }
            }

            public bool IsTeamActive(TeamSlot team) => Active.Contains(team);
            public int ActiveTeamCount => Active.Count;
            public TeamSlot FirstActiveTeam => Active.Count > 0 ? Active.First() : TeamSlot.None;
            public IReadOnlyList<PlayerSlot> TurnOrder => Order;

            public void EliminateTeam(TeamSlot team)
            {
                if (!Active.Remove(team)) return;
                Order.RemoveAll(p => MatchCompositionRule.GetPersonTeamSlot(settings, p) == team);
            }

            public void DeclareWinner(TeamSlot team, GameResultType resultType)
            {
                Winner = team;
                WinType = resultType;
            }
        }

        private static (KillTheKingManager ktk, FakeHost host) NewGame(int playerCount = 4, int maxRound = 20)
        {
            var settings = new GameStartSettings
            {
                gameMode = GameMode.KillTheKing,
                isTeamMode = false,
                matchComposition = playerCount == 2 ? MatchComposition.OneVsOne
                    : playerCount == 3 ? MatchComposition.OneVsOneVsOne : MatchComposition.OneVsOneVsOneVsOne,
                playerCount = playerCount,
                maxTurnCount = maxRound
            };
            var host = new FakeHost(settings);
            var ktk = new KillTheKingManager(host) { SuppressLogs = true };
            ktk.Initialize(settings, host.Active.ToList());
            return (ktk, host);
        }

        // 팀전 가짜 게임: 1P·3P 빨강(TeamA), 2P·4P 파랑(TeamB), 턴 순서 1P -> 2P -> 3P -> 4P (팀 번갈아)
        // sharedCharacter = true면 [2인 1조], false면 [2대 2]
        private static (KillTheKingManager ktk, FakeHost host) NewTeamGame(bool sharedCharacter, int maxRound = 20)
        {
            var settings = new GameStartSettings
            {
                gameMode = GameMode.KillTheKing,
                isTeamMode = true,
                teamMode = sharedCharacter ? TeamMode.TeamMode1 : TeamMode.TeamMode2,
                matchComposition = MatchComposition.TwoVsTwo,
                playerCount = 4,
                playerTeams = new[] { 1, 2, 1, 2 },
                maxTurnCount = maxRound
            };
            var host = new FakeHost(settings);
            var ktk = new KillTheKingManager(host) { SuppressLogs = true };
            ktk.Initialize(settings, host.Active.ToList());
            return (ktk, host);
        }

        private static void Escape(KillTheKingManager ktk, TeamSlot attacker, int count, params KtkSword[] swords)
            => ktk.OnEscape(attacker, count, swords);

        // 기대값과 실제값을 한 번에 비교해서 ✅ / ❌ 출력
        private static void Check(string name, params (string label, object expected, object actual)[] values)
        {
            var wrong = values.Where(v => !Equals(v.expected, v.actual)).ToList();
            if (wrong.Count == 0)
            {
                passCount++;
                Debug.Log($"{Tag} ✅ {name}");
            }
            else
            {
                failCount++;
                Debug.LogWarning($"{Tag} ❌ {name} | " +
                    string.Join(", ", wrong.Select(v => $"{v.label}: 기대 {v.expected}, 실제 {v.actual}")));
            }
        }

        private static void Begin(string title)
        {
            passCount = 0;
            failCount = 0;
            Debug.Log($"{Tag} ===== {title} 시작 =====");
        }

        private static void End(string title)
        {
            string summary = $"{Tag} ===== {title}: {passCount}/{passCount + failCount} 통과 =====";
            if (failCount == 0) Debug.Log(summary);
            else Debug.LogWarning(summary + $" (실패 {failCount}개, 위 ❌ 확인)");
        }

        // ===================================================================
        // 데미지·방패·탈락 (기획 2, 3, 5, 7, 10, 11번). 시작 HP 7, A가 B를 공격
        // ===================================================================
        public static void RunCombatTests()
        {
            Begin("데미지·방패·탈락");

            {
                var (k, _) = NewGame();
                Check("공격 대상 꼬리잡기 (A→B→C→D→A, 나를 노리는 건 바로 앞)",
                    ("A 대상", B, k.GetAttackTarget(A)), ("D 대상", A, k.GetAttackTarget(D)), ("A를 노리는 팀", D, k.GetAttacker(A)));
            }
            {
                var (k, _) = NewGame(2);
                Check("2인은 서로 공격", ("A 대상", B, k.GetAttackTarget(A)), ("B 대상", A, k.GetAttackTarget(B)));
            }
            {
                var (k, _) = NewGame();
                Escape(k, A, 1);
                Check("기본: 말 1개 탈출 = 데미지 1", ("B HP", 6, k.GetHp(B)), ("A HP", 7, k.GetHp(A)));
            }
            {
                var (k, _) = NewGame();
                Escape(k, A, 2);
                Check("업어서 말 2개 탈출 = 데미지 2", ("B HP", 5, k.GetHp(B)));
            }
            {
                var (k, _) = NewGame();
                k.AddShieldEffect(B, KtkShield.Normal);
                Escape(k, A, 1);
                Check("일반 방패: 막고 1개 소모", ("B HP", 7, k.GetHp(B)), ("B 방패 수", 0, k.GetShieldEffects(B).Count));
            }
            {
                var (k, _) = NewGame();
                k.AddShieldEffect(B, KtkShield.Thorn);
                Escape(k, A, 1);
                Check("가시 방패: 막고 공격자에게 1", ("B HP", 7, k.GetHp(B)), ("A HP", 6, k.GetHp(A)));
            }
            {
                var (k, _) = NewGame();
                k.AddShieldEffect(B, KtkShield.Mirror);
                Escape(k, A, 2);
                Check("반사의 거울: 데미지는 들어오고 전부 돌려줌", ("B HP", 5, k.GetHp(B)), ("A HP", 5, k.GetHp(A)));
            }
            {
                var (k, _) = NewGame();
                k.AddShieldEffect(B, KtkShield.Normal);
                k.AddShieldEffect(B, KtkShield.Mirror);
                Escape(k, A, 1);
                Check("방패 효과는 가장 최근 것부터 1개씩 소모",
                    ("B HP", 6, k.GetHp(B)), ("A HP", 6, k.GetHp(A)),
                    ("B 남은 방패 수", 1, k.GetShieldEffects(B).Count), ("B 남은 방패", KtkShield.Normal, k.GetShieldEffects(B)[0]));
            }
            {
                var (k, _) = NewGame();
                k.AddShieldEffect(B, KtkShield.Thorn);
                k.AddShieldEffect(A, KtkShield.Normal);
                Escape(k, A, 1);
                Check("핑퐁: 가시 반격을 내 일반 방패로 막음", ("B HP", 7, k.GetHp(B)), ("A HP", 7, k.GetHp(A)), ("A 방패 수", 0, k.GetShieldEffects(A).Count));
            }
            {
                var (k, _) = NewGame();
                k.AddShieldEffect(B, KtkShield.Thorn);
                k.AddShieldEffect(A, KtkShield.Thorn);
                Escape(k, A, 1);
                Check("핑퐁: 가시 ↔ 가시", ("B HP", 6, k.GetHp(B)), ("A HP", 7, k.GetHp(A)));
            }
            {
                var (k, _) = NewGame();
                k.AddShieldEffect(B, KtkShield.Thorn);
                k.AddShieldEffect(A, KtkShield.Mirror);
                Escape(k, A, 1);
                Check("핑퐁: 가시 반격을 내 거울로 돌려줌", ("B HP", 6, k.GetHp(B)), ("A HP", 6, k.GetHp(A)));
            }
            {
                var (k, _) = NewGame();
                k.AddShieldEffect(B, KtkShield.Mirror);
                k.AddShieldEffect(A, KtkShield.Normal);
                Escape(k, A, 1);
                Check("핑퐁: 반사를 내 일반 방패로 막음", ("B HP", 6, k.GetHp(B)), ("A HP", 7, k.GetHp(A)));
            }
            {
                var (k, _) = NewGame();
                k.AddShieldEffect(B, KtkShield.Mirror);
                k.AddShieldEffect(A, KtkShield.Thorn);
                Escape(k, A, 1);
                Check("핑퐁: 반사를 내 가시로 막고 1 반격", ("B HP", 5, k.GetHp(B)), ("A HP", 7, k.GetHp(A)));
            }
            {
                var (k, _) = NewGame();
                k.AddShieldEffect(B, KtkShield.Mirror);
                k.AddShieldEffect(A, KtkShield.Mirror);
                Escape(k, A, 1);
                Check("핑퐁: 거울 ↔ 거울", ("B HP", 5, k.GetHp(B)), ("A HP", 6, k.GetHp(A)));
            }
            {
                var (k, _) = NewGame();
                Escape(k, A, 1, KtkSword.Normal);
                Check("일반 검: 데미지 +1", ("B HP", 5, k.GetHp(B)));
            }
            {
                var (k, _) = NewGame();
                Escape(k, A, 1, KtkSword.Twin);
                Check("쌍검: 데미지 +2", ("B HP", 4, k.GetHp(B)));
            }
            {
                var (k, _) = NewGame();
                Escape(k, A, 1, KtkSword.Normal, KtkSword.Twin);
                Check("칼 여러 개는 전부 합산", ("B HP", 3, k.GetHp(B)));
            }
            {
                var (k, _) = NewGame();
                k.SetHpForTest(A, 5);
                Escape(k, A, 1, KtkSword.Vampire);
                Check("흡혈 검: 데미지 +1, 들어가면 HP 1 회복", ("B HP", 5, k.GetHp(B)), ("A HP", 6, k.GetHp(A)));
            }
            {
                var (k, _) = NewGame();
                Escape(k, A, 1, KtkSword.Vampire);
                Check("흡혈 검: 시작 HP를 넘지 않음", ("A HP", 7, k.GetHp(A)));
            }
            {
                var (k, _) = NewGame();
                k.SetHpForTest(A, 5);
                k.AddShieldEffect(B, KtkShield.Normal);
                Escape(k, A, 1, KtkSword.Vampire);
                Check("흡혈 검: 막히면 회복 없음", ("B HP", 7, k.GetHp(B)), ("A HP", 5, k.GetHp(A)));
            }
            {
                var (k, h) = NewGame();
                k.SetHpForTest(B, 2);
                Escape(k, A, 5);
                Check("남은 HP보다 큰 데미지는 버림 + HP 0이면 탈락", ("B HP", 0, k.GetHp(B)), ("B 탈락", false, h.IsTeamActive(B)));
            }
            {
                var (k, h) = NewGame();
                k.SetHpForTest(A, 5);
                k.SetHpForTest(B, 1);
                Escape(k, A, 1);
                Check("탈락 → 킬 보상 HP +1, 탈락자의 대상 이어받기",
                    ("B 탈락", false, h.IsTeamActive(B)), ("A HP", 6, k.GetHp(A)),
                    ("A 새 대상", C, k.GetAttackTarget(A)), ("C를 노리는 팀", A, k.GetAttacker(C)));
            }
            {
                var (k, _) = NewGame();
                k.SetHpForTest(B, 1);
                Escape(k, A, 1);
                Check("킬 보상도 시작 HP를 넘지 않음", ("A HP", 7, k.GetHp(A)));
            }
            {
                var (k, h) = NewGame();
                k.SetHpForTest(A, 5);
                k.AddShieldEffect(B, KtkShield.Normal);
                Escape(k, A, 1, KtkSword.Demon);
                Check("마검: 방패 무시하고 즉사, 킬 보상", ("B 탈락", false, h.IsTeamActive(B)), ("A HP", 6, k.GetHp(A)));
            }
            {
                var (k, h) = NewGame();
                k.SetHpForTest(A, 1);
                k.SetHpForTest(B, 5);
                k.AddShieldEffect(B, KtkShield.Mirror);
                Escape(k, A, 1);
                Check("반사로 공격자 탈락 → 반사한 쪽이 킬 보상, D가 B를 이어받음",
                    ("A 탈락", false, h.IsTeamActive(A)), ("B HP", 5, k.GetHp(B)), ("D 새 대상", B, k.GetAttackTarget(D)));
            }
            {
                var (k, h) = NewGame();
                k.SetHpForTest(B, 1);
                k.AddShieldEffect(B, KtkShield.Mirror);
                k.AddShieldEffect(A, KtkShield.Mirror);
                Escape(k, A, 2);
                Check("첫 공격에 대상이 탈락해도 반사의 거울 데미지는 공격자에게 감 (기획 10-8)",
                    ("B 탈락", false, h.IsTeamActive(B)), ("A HP", 5, k.GetHp(A)));
            }
            {
                var (k, _) = NewGame();
                k.SetHpForTest(A, 2);
                k.AddShieldEffect(B, KtkShield.Mirror);
                Escape(k, A, 1, KtkSword.Vampire);
                Check("기획서 예시: HP 2 + 흡혈 검으로 반사의 거울 치기 → 3 → 1로 생존", ("A HP", 1, k.GetHp(A)), ("B HP", 5, k.GetHp(B)));
            }
            {
                var (k, h) = NewGame(2);
                k.SetHpForTest(B, 1);
                Escape(k, A, 1);
                Check("최후 1인 승리", ("승자", A, h.Winner), ("종료 이유", GameResultType.TeamWin, h.WinType));
            }
            {
                var (k, h) = NewGame();
                h.EliminateTeam(D);
                k.SetHpForTest(A, 5);
                k.SetHpForTest(B, 3);
                k.SetHpForTest(C, 2);
                k.OnTurnLimitReached();
                Check("제한 턴: 5·3·2 → 2·0·0, 1등", ("A HP", 2, k.GetHp(A)), ("승자", A, h.Winner), ("종료 이유", GameResultType.TimeOver, h.WinType));
            }
            {
                var (k, h) = NewGame();
                h.EliminateTeam(D);
                k.SetHpForTest(A, 4);
                k.SetHpForTest(B, 4);
                k.SetHpForTest(C, 2);
                k.OnTurnLimitReached();
                Check("제한 턴: 4·4·2 → 2·2·0, 서든데스",
                    ("A HP", 2, k.GetHp(A)), ("B HP", 2, k.GetHp(B)), ("C 탈락", false, h.IsTeamActive(C)),
                    ("서든데스", true, k.IsSuddenDeath), ("승자 없음", TeamSlot.None, h.Winner));
            }
            {
                var (k, h) = NewGame();
                k.OnTurnLimitReached();
                Check("제한 턴: 모두 HP가 같으면 줄지 않고 바로 서든데스", ("A HP", 7, k.GetHp(A)), ("서든데스", true, k.IsSuddenDeath), ("남은 팀", 4, h.ActiveTeamCount));
            }
            {
                var (k, h) = NewGame();
                h.EliminateTeam(C);
                h.EliminateTeam(D);
                k.SetHpForTest(A, 5);
                k.SetHpForTest(B, 3);
                k.AddShieldEffect(B, KtkShield.Normal);
                k.OnTurnLimitReached();
                Check("제한 턴 HP 감소는 방패 효과 무시하고 다 깎임 (기획 확정)",
                    ("B 탈락", false, h.IsTeamActive(B)), ("A HP", 2, k.GetHp(A)), ("승자", A, h.Winner));
            }
            {
                var settings = new GameStartSettings
                {
                    gameMode = GameMode.KillTheKing, matchComposition = MatchComposition.OneVsOneVsOneVsOne,
                    playerCount = 4, maxTurnCount = 20, killTheKingStartHp = 9
                };
                var host = new FakeHost(settings);
                var k = new KillTheKingManager(host) { SuppressLogs = true };
                k.Initialize(settings, host.Active.ToList());
                Check("대기실에서 고른 HP로 시작 (killTheKingStartHp)", ("A HP", 9, k.GetHp(A)), ("시작 HP", 9, k.StartHp));
            }

            // ---- 팀전 (기획 13번): 빨강 = TeamA (1P·3P), 파랑 = TeamB (2P·4P) ----
            {
                var (k1, _) = NewTeamGame(true);
                var (k2, _) = NewTeamGame(false);
                Check("팀 HP 시작값: 2인 1조·2대 2가 각자 값을 씀 (임시값 7 / 10, 기획 미정)",
                    ("2인 1조 빨강", KillTheKingManager.TempSharedTeamStartHp, k1.GetHp(A)),
                    ("2대 2 파랑", KillTheKingManager.TempTwoVsTwoStartHp, k2.GetHp(B)));
            }
            foreach (bool shared in new[] { true, false })
            {
                string mode = shared ? "2인 1조" : "2대 2";
                {
                    var (k, _) = NewTeamGame(shared);
                    Check($"{mode}: 공격 대상은 상대 팀", ("빨강 대상", B, k.GetAttackTarget(A)), ("파랑 대상", A, k.GetAttackTarget(B)));
                }
                {
                    var (k, _) = NewTeamGame(shared);
                    int start = k.GetHp(B);
                    Escape(k, A, 1); // 1P 탈출
                    Escape(k, A, 2); // 3P(팀원)가 업어서 2개 탈출
                    Check($"{mode}: 팀원이 탈출해도 같은 팀 공격으로 합산", ("파랑 HP", start - 3, k.GetHp(B)));
                }
                {
                    var (k, _) = NewTeamGame(shared);
                    int start = k.GetHp(A);
                    k.AddShieldEffect(B, KtkShield.Mirror);
                    Escape(k, A, 2);
                    Check($"{mode}: 방패 효과·반사도 팀 HP 기준", ("파랑 HP", start - 2, k.GetHp(B)), ("빨강 HP", start - 2, k.GetHp(A)));
                }
                {
                    var (k, h) = NewTeamGame(shared);
                    k.SetHpForTest(A, 3);
                    k.SetHpForTest(B, 1);
                    Escape(k, A, 1);
                    Check($"{mode}: 팀 HP 0이면 바로 패배, 킬 보상 없음",
                        ("승자", A, h.Winner), ("종료 이유", GameResultType.TeamWin, h.WinType), ("빨강 HP", 3, k.GetHp(A)));
                }
                {
                    var (k, h) = NewTeamGame(shared);
                    k.SetHpForTest(A, 6);
                    k.SetHpForTest(B, 4);
                    k.OnTurnLimitReached();
                    Check($"{mode}: 제한 턴 끝 → HP 높은 팀 승리", ("승자", A, h.Winner), ("종료 이유", GameResultType.TimeOver, h.WinType));
                }
                {
                    var (k, h) = NewTeamGame(shared);
                    k.OnTurnLimitReached();
                    Check($"{mode}: 제한 턴 끝 팀 HP 같으면 서든데스", ("서든데스", true, k.IsSuddenDeath), ("승자 없음", TeamSlot.None, h.Winner));
                }
            }

            End("데미지·방패·탈락");
        }

        // ===================================================================
        // 아이템 생성·아레나의 열기·서든데스 (기획 4-1, 4-2, 6, 12번). 제한 턴 20
        // ===================================================================
        public static void RunItemTests()
        {
            Begin("아이템 생성·아레나의 열기·서든데스");

            {
                var (k, _) = NewGame();
                var swords = new List<KtkSword>();
                var shields = new List<KtkShield>();
                k.OnSwordSpawnRequested += swords.Add;
                k.OnShieldSpawnRequested += shields.Add;
                k.OnRoundStarted(1);
                Check("1라운드엔 아이템 없음", ("칼 생성 수", 0, swords.Count), ("방패 생성 수", 0, shields.Count));
                k.OnRoundStarted(2);
                Check("2라운드에 일반 검·일반 방패 하나씩 (가정: 라운드 시작 때)",
                    ("칼 생성 수", 1, swords.Count), ("칼", KtkSword.Normal, k.BoardSword), ("방패", KtkShield.Normal, k.BoardShield));
                k.OnRoundStarted(3);
                Check("안 먹히면 더 안 생김 (각각 최대 1개)", ("칼 생성 수", 1, swords.Count), ("방패 생성 수", 1, shields.Count));
            }
            {
                var (k, _) = NewGame();
                k.OnRoundStarted(1);
                k.OnRoundStarted(2);
                k.OnRoundStarted(3);
                k.NotifySwordPickedUp();
                k.NotifyShieldPickedUp();
                int swordRound = 0, shieldRound = 0;
                for (int r = 4; r <= 10; r++)
                {
                    k.OnRoundStarted(r);
                    if (swordRound == 0 && k.BoardSword != null) swordRound = r;
                    if (shieldRound == 0 && k.BoardShield != null) shieldRound = r;
                }
                Check("재생성: 3라운드에 먹힘 → 칼 6라운드, 방패 8라운드", ("칼 생성 라운드", 6, swordRound), ("방패 생성 라운드", 8, shieldRound));
            }
            {
                var (k, _) = NewGame();
                bool onlyNormal = Enumerable.Range(0, 300).All(_ => k.RollSword() == KtkSword.Normal && k.RollShield() == KtkShield.Normal);
                Check("아레나의 열기 전엔 일반 검·일반 방패만", ("일반만", true, onlyNormal));
            }
            {
                var (k, _) = NewGame(4, 20);
                int heatRound = 0;
                for (int r = 1; r <= 12 && heatRound == 0; r++)
                {
                    k.OnRoundStarted(r);
                    if (k.IsArenaHeat) heatRound = r;
                }
                Check("아레나의 열기: 제한 턴 20 → 11번째 턴부터 (기획 확정)", ("시작 라운드", 11, heatRound));
            }
            {
                var (k, _) = NewGame(4, 15);
                int heatRound = 0;
                for (int r = 1; r <= 12 && heatRound == 0; r++)
                {
                    k.OnRoundStarted(r);
                    if (k.IsArenaHeat) heatRound = r;
                }
                Check("아레나의 열기: 제한 턴 15 → 8번째 턴부터 (기획 확정)", ("시작 라운드", 8, heatRound));
            }
            {
                var (k, _) = NewGame();
                k.TestStartArenaHeat();
                var rolls = Enumerable.Range(0, 3000).Select(_ => k.RollSword()).ToList();
                float twin = rolls.Count(s => s == KtkSword.Twin) / 3000f;
                Check("열기 후 칼: 일반·쌍검·흡혈만, 쌍검이 가장 많음(60%)",
                    ("마검 없음", 0, rolls.Count(s => s == KtkSword.Demon)),
                    ("일반 나옴", true, rolls.Contains(KtkSword.Normal)), ("흡혈 나옴", true, rolls.Contains(KtkSword.Vampire)),
                    ("쌍검 50~70%", true, twin > 0.5f && twin < 0.7f));
            }
            {
                var (k, _) = NewGame();
                k.TestStartArenaHeat();
                var rolls = Enumerable.Range(0, 3000).Select(_ => k.RollShield()).ToList();
                float mirror = rolls.Count(s => s == KtkShield.Mirror) / 3000f;
                Check("열기 후 방패: 일반·가시·반사, 반사는 드물게(5%)",
                    ("가시 나옴", true, rolls.Contains(KtkShield.Thorn)), ("반사 1~10%", true, mirror > 0.01f && mirror < 0.1f));
            }
            {
                var (k, _) = NewGame();
                int convertRequests = 0;
                k.OnConvertSwordsToDemonRequested += () => convertRequests++;
                k.OnRoundStarted(1);
                k.OnRoundStarted(2);
                k.OnTurnLimitReached(); // 모두 HP 7 -> 바로 서든데스
                Check("서든데스: 칼 전부 마검으로 요청, 보드 위 칼도 마검", ("변환 요청", 1, convertRequests), ("보드 칼", KtkSword.Demon, k.BoardSword));
            }
            {
                var (k, _) = NewGame();
                var swords = new List<KtkSword>();
                k.OnSwordSpawnRequested += swords.Add;
                k.OnRoundStarted(1);
                k.OnRoundStarted(2);
                k.NotifySwordPickedUp();
                k.OnTurnLimitReached();
                Check("서든데스: 보드에 칼이 없으면 바로 마검 생성",
                    ("보드 칼", KtkSword.Demon, k.BoardSword), ("마지막 생성", KtkSword.Demon, swords.LastOrDefault()));
            }
            {
                var (k, _) = NewGame();
                k.OnRoundStarted(1);
                k.OnRoundStarted(2);
                k.OnTurnLimitReached();
                k.OnRoundStarted(3);
                k.NotifySwordPickedUp(); // 3라운드에 먹힘
                k.OnRoundStarted(4);
                bool emptyAt4 = k.BoardSword == null;
                k.OnRoundStarted(5);
                Check("서든데스 칼 재생성: 3라운드에 먹힘 → 5라운드에 마검",
                    ("4라운드 비어 있음", true, emptyAt4), ("5라운드 칼", KtkSword.Demon, k.BoardSword));
            }

            End("아이템 생성·아레나의 열기·서든데스");
        }

        // B를 1라운드에 탈락시킨 개인전 (A가 B를 공격). 이후 라운드를 넘기면 B가 구슬을 받음
        private static (KillTheKingManager ktk, FakeHost host) NewGameWithGhostB(int maxRound = 20)
        {
            var (k, h) = NewGame(4, maxRound);
            k.OnRoundStarted(1);
            k.SetHpForTest(B, 1);
            Escape(k, A, 1);
            return (k, h);
        }

        private static void StartRounds(KillTheKingManager ktk, int from, int to)
        {
            for (int r = from; r <= to; r++) ktk.OnRoundStarted(r);
        }

        // ===================================================================
        // 결과 통계 (기획 14번) · 탈락자 능력 (기획 9번)
        // ===================================================================
        public static void RunStatsAndGhostTests()
        {
            Begin("결과 통계·탈락자 능력");

            {
                var (k, _) = NewGame();
                Escape(k, A, 2);
                Check("통계: 준 데미지 / 받은 데미지", ("A 준 데미지", 2, k.GetStats(A).DamageDealt), ("B 받은 데미지", 2, k.GetStats(B).DamageTaken));
            }
            {
                var (k, _) = NewGame();
                k.AddShieldEffect(B, KtkShield.Mirror);
                Escape(k, A, 2);
                Check("통계: 반사로 돌려준 것도 준 데미지 (가정, 통계 기준은 기획 확인 중)",
                    ("B 준 데미지", 2, k.GetStats(B).DamageDealt), ("A 받은 데미지", 2, k.GetStats(A).DamageTaken));
            }
            {
                var (k, _) = NewGame();
                k.SetHpForTest(B, 1);
                Escape(k, A, 5);
                Check("통계: 실제로 깎인 양만 셈 + 탈락시킨 수 (가정, 통계 기준은 기획 확인 중)", ("A 준 데미지", 1, k.GetStats(A).DamageDealt), ("A 탈락시킨 수", 1, k.GetStats(A).Kills));
            }
            {
                var (k, _) = NewGame();
                k.RecordCapture(A);
                k.RecordCapture(A);
                Check("통계: 잡기 횟수 (가정: 잡은 말 수가 아니라 횟수)", ("A 잡기", 2, k.GetStats(A).Captures));
            }
            {
                var (k, _) = NewGame();
                k.OnRoundStarted(1);
                k.OnRoundStarted(2);
                k.NotifySwordPickedUp(A);
                k.NotifyShieldPickedUp(C);
                k.NotifyItemsStolen(A, 2);
                Check("통계: 아이템 획득·뺏기 (보드가 알려준 만큼)",
                    ("A 획득", 1, k.GetStats(A).ItemsPicked), ("C 획득", 1, k.GetStats(C).ItemsPicked), ("A 뺏기", 2, k.GetStats(A).ItemsStolen));
            }
            {
                var (k, _) = NewGameWithGhostB();
                int afterElim = k.GetOrbs(B);
                StartRounds(k, 2, 4);
                Check("구슬: 탈락한 다음 라운드부터 라운드마다 +1, 살아 있는 사람은 0",
                    ("탈락 직후", 0, afterElim), ("4라운드 시작 후", 3, k.GetOrbs(B)), ("A 구슬", 0, k.GetOrbs(A)));
            }
            {
                var (k, _) = NewGame();
                bool ok = k.TryUseGhostAbility(A, KtkGhostAbility.Curse, out _);
                Check("살아 있는 사람은 탈락자 능력 못 씀", ("성공", false, ok));
            }
            {
                var (k, _) = NewGameWithGhostB();
                StartRounds(k, 2, 4); // 구슬 3
                bool ok = k.TryUseGhostAbility(B, KtkGhostAbility.DeadWish, out _);
                Check("구슬 부족이면 못 씀 (죽은 자의 염원 5개)", ("성공", false, ok), ("구슬 그대로", 3, k.GetOrbs(B)));
            }
            {
                var (k, _) = NewGameWithGhostB();
                int curseRequests = 0;
                k.OnCurseRequested += _ => curseRequests++;
                StartRounds(k, 2, 4);
                bool first = k.TryUseGhostAbility(B, KtkGhostAbility.Curse, out _);
                StartRounds(k, 5, 7);
                bool second = k.TryUseGhostAbility(B, KtkGhostAbility.Curse, out _);
                Check("저주: 구슬 3개 쓰고 보드에 요청, 한 번만",
                    ("첫 사용", true, first), ("요청 수", 1, curseRequests), ("두 번째", false, second), ("남은 구슬", 3, k.GetOrbs(B)));
            }
            {
                var (k, _) = NewGameWithGhostB();
                StartRounds(k, 2, 4); // 2라운드에 일반 검 생성, 구슬 3
                bool ok = k.TryUseGhostAbility(B, KtkGhostAbility.SoulInfusion, out _);
                bool upgraded = k.BoardSword == KtkSword.Twin || k.BoardSword == KtkSword.Vampire || k.BoardSword == KtkSword.Demon;
                Check("영혼 주입: 보드 위 일반 검을 강화", ("성공", true, ok), ("강화됨", true, upgraded));
            }
            {
                var (k, _) = NewGameWithGhostB();
                StartRounds(k, 2, 4);
                k.NotifySwordPickedUp(); // 보드에 일반 검 없음
                bool ok = k.TryUseGhostAbility(B, KtkGhostAbility.SoulInfusion, out _);
                Check("영혼 주입: 보드에 일반 검이 없으면 못 씀", ("성공", false, ok), ("구슬 그대로", 3, k.GetOrbs(B)));
            }
            {
                var (k, _) = NewGame();
                var rolls = Enumerable.Range(0, 4000).Select(_ => k.RollSoulInfusion()).ToList();
                float twin = rolls.Count(s => s == KtkSword.Twin) / 4000f;
                Check("영혼 주입 확률: 쌍검 75% / 흡혈 24% / 마검 1%",
                    ("일반 검 안 나옴", 0, rolls.Count(s => s == KtkSword.Normal)), ("쌍검 65~85%", true, twin > 0.65f && twin < 0.85f),
                    ("흡혈 나옴", true, rolls.Contains(KtkSword.Vampire)));
            }
            {
                var (k, h) = NewGameWithGhostB(20);
                StartRounds(k, 2, 6); // 구슬 5
                bool ok = k.TryUseGhostAbility(B, KtkGhostAbility.DeadWish, out _);
                Check("죽은 자의 염원: 남은 제한 턴 -1, 아레나의 열기 기준은 그대로",
                    ("성공", true, ok), ("제한 턴", 19, h.Settings.maxTurnCount), ("구슬", 0, k.GetOrbs(B)));
                StartRounds(k, 7, 11);
                Check("죽은 자의 염원 후에도 아레나의 열기는 처음 기준(11라운드)", ("11라운드 열기", true, k.IsArenaHeat));
            }
            {
                var (k, _) = NewGameWithGhostB(6);
                StartRounds(k, 2, 6); // 6라운드 = 마지막 라운드, 구슬 5
                bool ok = k.TryUseGhostAbility(B, KtkGhostAbility.DeadWish, out _);
                Check("죽은 자의 염원: 마지막 라운드에는 못 씀", ("성공", false, ok));
            }
            {
                var (k, _) = NewGameWithGhostB();
                StartRounds(k, 2, 4);
                k.OnTurnLimitReached(); // A·C·D 모두 HP 7 -> 서든데스
                bool ok = k.TryUseGhostAbility(B, KtkGhostAbility.Curse, out _);
                Check("서든데스에서는 탈락자 능력 못 씀", ("서든데스", true, k.IsSuddenDeath), ("성공", false, ok));
            }
            {
                var (k, _) = NewTeamGame(false);
                k.OnRoundStarted(1);
                bool ok = k.TryUseGhostAbility(B, KtkGhostAbility.Curse, out _);
                Check("팀전은 탈락자 능력 없음", ("성공", false, ok));
            }

            End("결과 통계·탈락자 능력");
        }
    }
}
