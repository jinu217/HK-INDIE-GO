using System.Collections.Generic;
using System.Linq;
using YutArena.Common;
using YutArena.Managers.GameProgress;

namespace YutArena.Managers
{
    // 킬 더 킹이 기존 게임 흐름에서 쓰는 것들 (실제 게임 = TestWinConditionManager, 자동 테스트 = 가짜 게임)
    public interface IKtkHost
    {
        bool IsTeamActive(TeamSlot team);           // 아직 탈락 안 한 팀인지
        int ActiveTeamCount { get; }
        TeamSlot FirstActiveTeam { get; }
        IReadOnlyList<PlayerSlot> TurnOrder { get; } // 지금 턴 순서 (탈락자는 빠져 있음)
        void EliminateTeam(TeamSlot team);           // 탈락: 턴 순서에서 빼고 판 위 말 내림
        void DeclareWinner(TeamSlot team, GameResultType resultType);
    }

    // 결과창 수치 (기획 14번, 항목은 기획 확인 중). 팀 HP를 같이 쓰는 팀전은 팀 기준, 개인전은 팀 = 사람
    public class KtkTeamStats
    {
        public int DamageDealt;   // 준 데미지 (실제로 깎인 양, 반사·가시 포함)
        public int DamageTaken;   // 받은 데미지
        public int Captures;      // 잡기 횟수
        public int Kills;         // 탈락시킨 수
        public int ItemsPicked;   // 아이템 획득 수 (보드 연결 후)
        public int ItemsStolen;   // 아이템 뺏기 수 (보드 연결 후)
    }

    // [탈락자 능력] (기획 9번)
    public enum KtkGhostAbility
    {
        Curse,         // [저주] 구슬 3개: 말 없는 칸에 저주 칸 (닿으면 그 말 주인 SP -1)
        SoulInfusion,  // [영혼 주입] 구슬 3개: 보드 위 주인 없는 [일반 검] 강화
        DeadWish       // [죽은 자의 염원] 구슬 5개: 남은 제한 턴 -1
    }

    // ===================================================================
    // 킬 더 킹 모드 전용 상태와 규칙 (기획 "킬 더 킹 모드 룰")
    // 턴, 라운드, 탈출 말 1턴 쉬기, 탈락자 턴 빼기, 게임 끝내기는 기존 흐름을 그대로 쓰고,
    // 분기점(말 탈출, 턴 수 끝)에서 TestWinConditionManager가 킬 더 킹이면 여기로 넘김.
    // 모든 값은 팀(TeamSlot) 기준: 개인전은 팀 = 사람, 2인 1조·2대2는 팀 HP를 같이 씀
    //
    // 1단계: HP, 공격 대상, 탈출 데미지(말 수), 탈락, 킬 보상, 최후 1인 승리,
    //        턴 끝 HP 동시 감소 -> 1등 or 서든데스
    // 2단계: [방패 효과] 쌓기·소모, 탈출 처리 순서 10단계(가시·반사 핑퐁, 흡혈, 마검) -> KtkDamageResolver
    // 3단계(앞부분): 아이템 생성 시점·종류, [아레나의 열기], 서든데스 마검
    //   (칸에 놓기·획득·묶음이 들고 다니기는 보드 쪽과 연결 예정, 탈락자 능력은 다음 단계)
    // ===================================================================
    public class KillTheKingManager
    {
        // 밸런스 작업 전 임시 수치 (기획: HP 개인전 5~10, 팀 HP는 [2인 1조]/[2대 2] 따로, 제한 턴은 넉넉하게)
        public const int TempSoloStartHp = 7;
        public const int TempSharedTeamStartHp = 7;   // 2인 1조
        public const int TempTwoVsTwoStartHp = 10;    // 2대2
        public const int TempMaxRound = 20;

        // 게임 쪽 연결 (실제 게임 = TestWinConditionManager, 자동 테스트 = 가짜 게임)
        private readonly IKtkHost host;
        private GameStartSettings settings;
        // 자동 테스트 중엔 로그를 끔 (테스트 결과만 보이게)
        internal bool SuppressLogs { get; set; }
        private readonly Dictionary<TeamSlot, int> hpByTeam = new Dictionary<TeamSlot, int>();
        // 팀별 [방패 효과]. 리스트 맨 뒤가 가장 최근에 얻은 것 (맞을 때 맨 뒤부터 1개씩 소모)
        private readonly Dictionary<TeamSlot, List<KtkShield>> shieldEffectsByTeam = new Dictionary<TeamSlot, List<KtkShield>>();
        private readonly KtkDamageResolver resolver;
        // 테스트용: 다음 탈출 한 번에 붙일 칼 (아이템 획득이 생기기 전까지 우클릭 메뉴로 넣음)
        private readonly List<KtkSword> testSwordsForNextEscape = new List<KtkSword>();

        // 시작 HP = 회복 상한 (킬 보상, 흡혈 검)
        public int StartHp { get; private set; }
        public bool IsSuddenDeath { get; private set; }

        // ---- UI가 구독할 수 있는 이벤트 ----
        public System.Action<TeamSlot, int> OnHpChanged;              // 팀, 남은 HP
        public System.Action<TeamSlot, TeamSlot, int> OnDamaged;      // 공격 팀, 맞은 팀, 실제로 깎인 HP
        public System.Action<TeamSlot, TeamSlot> OnTeamEliminated;    // 탈락 팀, 탈락시킨 팀 (턴 끝 HP 감소면 None)
        public System.Action OnSuddenDeathStarted;
        public System.Action<TeamSlot, IReadOnlyList<KtkShield>> OnShieldEffectsChanged; // 팀, 지금 쌓인 방패 효과 (뒤가 최근)

        public KillTheKingManager(IKtkHost host)
        {
            this.host = host;
            resolver = new KtkDamageResolver(this);
        }

        internal void Log(string message)
        {
            if (!SuppressLogs) QaDebugLog.Event(message);
        }

        // 새 게임 시작 시 (TestWinConditionManager.Initialize)
        public void Initialize(GameStartSettings gameSettings, IEnumerable<TeamSlot> teams)
        {
            settings = gameSettings;
            IsSuddenDeath = false;
            StartHp = GetStartHp(settings);
            hpByTeam.Clear();
            shieldEffectsByTeam.Clear();
            testSwordsForNextEscape.Clear();
            statsByTeam.Clear();
            orbsByTeam.Clear();
            usedAbilitiesByTeam.Clear();
            eliminatedRoundByTeam.Clear();
            foreach (var team in teams)
            {
                hpByTeam[team] = StartHp;
                shieldEffectsByTeam[team] = new List<KtkShield>();
                statsByTeam[team] = new KtkTeamStats();
                orbsByTeam[team] = 0;
                usedAbilitiesByTeam[team] = new HashSet<KtkGhostAbility>();
            }

            BoardSword = null;
            BoardShield = null;
            swordPickedRound = 0;
            shieldPickedRound = 0;
            CurrentRound = 0;
            IsArenaHeat = false;
            // 처음 설정한 제한 턴의 절반이 지나면 시작 (예: 20 -> 10라운드가 끝나고 11라운드 시작 때)
            // 홀수(예: 15)일 때 7라운드 뒤인지 8라운드 뒤인지는 기획 확인 중 -> 지금은 15/2 = 7 다음인 8라운드 시작 때
            arenaHeatStartRound = settings != null && settings.maxTurnCount > 0 ? settings.maxTurnCount / 2 + 1 : int.MaxValue;
        }

        private static int GetStartHp(GameStartSettings s)
        {
            if (s == null || !s.isTeamMode) return TempSoloStartHp;
            return s.teamMode == TeamMode.TeamMode1 ? TempSharedTeamStartHp : TempTwoVsTwoStartHp;
        }

        public int GetHp(TeamSlot team) => hpByTeam.TryGetValue(team, out var hp) ? hp : 0;

        // ===================================================================
        // [공격 대상] = 턴 순서에서 나 다음에 오는, 살아 있는 상대 팀
        // - 개인전: 다음 플레이어, 마지막은 첫 번째. 2인이면 서로
        // - 탈락자가 생기면 탈락자는 순서에서 빠지므로 "탈락자의 대상을 이어받기"가 자동으로 됨
        //   (반사로 공격자·대상이 같이 탈락해도 결과가 같음)
        // - 2인 1조·2대2: 순서가 팀 번갈아라 항상 상대 팀
        // ===================================================================
        public TeamSlot GetAttackTarget(TeamSlot attacker)
        {
            var order = host.TurnOrder;
            int start = -1;
            for (int i = 0; i < order.Count && start < 0; i++)
                if (MatchCompositionRule.GetPersonTeamSlot(settings, order[i]) == attacker) start = i;
            if (start < 0) return TeamSlot.None;
            for (int step = 1; step <= order.Count; step++)
            {
                TeamSlot team = MatchCompositionRule.GetPersonTeamSlot(settings, order[(start + step) % order.Count]);
                if (team != attacker && host.IsTeamActive(team)) return team;
            }
            return TeamSlot.None;
        }

        // 나를 노리는 팀 (UI 표시용). 개인전은 턴 순서상 내 바로 앞
        public TeamSlot GetAttacker(TeamSlot target)
        {
            foreach (var team in hpByTeam.Keys)
                if (team != target && host.IsTeamActive(team) && GetAttackTarget(team) == target)
                    return team;
            return TeamSlot.None;
        }

        // ===================================================================
        // 말 탈출 (TestWinConditionManager.OnPieceMoveResolved에서 킬 더 킹이면 이쪽으로)
        // 데미지 = 탈출한 말 수(업힌 말·팀원 말 포함, 홍길동 분신은 진짜 말이 아니라 원래 안 셈) + 칼
        // swords / shields = 탈출한 묶음이 가진 칼·방패 (아이템 단계에서 영서 쪽 묶음 데이터로 넘어옴)
        // ===================================================================
        public void OnEscape(TeamSlot attacker, int escapedCount,
            IReadOnlyList<KtkSword> swords = null, IReadOnlyList<KtkShield> shields = null)
        {
            if (escapedCount <= 0 || !host.IsTeamActive(attacker)) return;
            TeamSlot target = GetAttackTarget(attacker);
            if (target == TeamSlot.None) return;

            var allSwords = new List<KtkSword>();
            if (swords != null) allSwords.AddRange(swords);
            allSwords.AddRange(testSwordsForNextEscape);
            testSwordsForNextEscape.Clear();

            resolver.ResolveEscape(attacker, target, escapedCount, allSwords,
                shields ?? (IReadOnlyList<KtkShield>)System.Array.Empty<KtkShield>());
        }

        // ---- 탈출 처리(KtkDamageResolver)가 쓰는 기본 동작 ----
        internal bool IsTeamActive(TeamSlot team) => host.IsTeamActive(team);
        internal bool IsGameOver => host.ActiveTeamCount <= 1;

        // 데미지 적용: 남은 HP보다 큰 만큼은 버리고, 실제로 깎인 양을 돌려줌
        internal int ApplyDamage(TeamSlot from, TeamSlot to, int amount)
        {
            int before = GetHp(to);
            int dealt = System.Math.Min(amount, before);
            SetHp(to, before - dealt);
            Log($"[킬더킹] {from} → {to} 데미지 {amount} (실제 {dealt}) | {to} HP {before} → {GetHp(to)}");
            if (statsByTeam.TryGetValue(from, out var fromStats)) fromStats.DamageDealt += dealt;
            if (statsByTeam.TryGetValue(to, out var toStats)) toStats.DamageTaken += dealt;
            OnDamaged?.Invoke(from, to, dealt);
            return dealt;
        }

        // 회복 (시작 HP 넘지 않음)
        internal void Heal(TeamSlot team, int amount, string reason)
        {
            int before = GetHp(team);
            SetHp(team, before + amount);
            Log($"[킬더킹] {team} {reason} 회복 | HP {before} → {GetHp(team)}");
        }

        // HP 0이면 탈락 처리: 턴에서 빼고 판 위 말 내림 -> 킬 보상 -> 1팀 남으면 승리. 탈락했으면 true
        internal bool KnockOutIfDead(TeamSlot team, TeamSlot killer)
        {
            if (GetHp(team) > 0 || !host.IsTeamActive(team)) return false;
            Log($"[킬더킹] {team} 탈락" + (killer != TeamSlot.None ? $" (탈락시킨 팀 {killer})" : ""));
            host.EliminateTeam(team);
            shieldEffectsByTeam[team].Clear();
            eliminatedRoundByTeam[team] = CurrentRound;
            if (killer != TeamSlot.None && statsByTeam.TryGetValue(killer, out var killerStats)) killerStats.Kills++;
            OnTeamEliminated?.Invoke(team, killer);

            // [킬 보상] 적을 탈락시키면 HP +1 (시작 HP 넘지 않음, 이미 탈락한 팀은 못 받음)
            // 팀전은 킬 보상 없음 (기획 13번, 탈락이 곧 게임 종료라 아래 승리 처리로 바로 끝남)
            bool isTeamMatch = settings != null && settings.isTeamMode;
            if (!isTeamMatch && killer != TeamSlot.None && host.IsTeamActive(killer) && GetHp(killer) < StartHp)
                Heal(killer, 1, "킬 보상");

            if (host.ActiveTeamCount == 1)
                host.DeclareWinner(host.FirstActiveTeam, GameResultType.TeamWin);
            return true;
        }

        // ===================================================================
        // [방패 효과] (기획 5번): 방패 든 묶음이 탈출하면 플레이어(팀)가 얻음. 여러 개 쌓이고,
        // 데미지를 받을 때 가장 최근 것부터 1개씩 소모. [마검]에는 발동·소모 안 함
        // ===================================================================
        public IReadOnlyList<KtkShield> GetShieldEffects(TeamSlot team)
            => shieldEffectsByTeam.TryGetValue(team, out var list) ? list : (IReadOnlyList<KtkShield>)System.Array.Empty<KtkShield>();

        public void AddShieldEffect(TeamSlot team, KtkShield shield)
        {
            if (!shieldEffectsByTeam.TryGetValue(team, out var list)) return;
            list.Add(shield);
            Log($"[킬더킹] {team} 방패 효과 +{shield} | 지금 [{string.Join(", ", list)}]");
            OnShieldEffectsChanged?.Invoke(team, list);
        }

        // 가장 최근 방패 효과 1개를 꺼냄 (없으면 null)
        internal KtkShield? PopShieldEffect(TeamSlot team)
        {
            if (!shieldEffectsByTeam.TryGetValue(team, out var list) || list.Count == 0) return null;
            KtkShield shield = list[list.Count - 1];
            list.RemoveAt(list.Count - 1);
            OnShieldEffectsChanged?.Invoke(team, list);
            return shield;
        }

        // 테스트용: 다음 탈출 한 번에 칼을 붙임
        public void AddTestSwordForNextEscape(KtkSword sword)
        {
            testSwordsForNextEscape.Add(sword);
            Log($"[킬더킹] 테스트: 다음 탈출에 {sword} 붙임 | [{string.Join(", ", testSwordsForNextEscape)}]");
        }

        // 테스트용: 지금 상태 한 번에 출력
        public void LogState()
        {
            Log("[킬더킹] 상태 | " + string.Join(" / ", hpByTeam.Keys.Select(t =>
                $"{t} HP {GetHp(t)}" + (host.IsTeamActive(t)
                    ? $", 대상 {GetAttackTarget(t)}, 방패 [{string.Join(", ", GetShieldEffects(t))}]"
                    : $" (탈락, 구슬 {GetOrbs(t)})"))) + (IsSuddenDeath ? " / 서든데스" : ""));
            Log($"[킬더킹] 아이템 | {CurrentRound}라운드, 아레나의 열기 {(IsArenaHeat ? "ON" : $"OFF({arenaHeatStartRound}라운드부터)")}" +
                $" | 보드 칼 {(BoardSword?.ToString() ?? $"없음(먹힌 라운드 {swordPickedRound})")}" +
                $" | 보드 방패 {(BoardShield?.ToString() ?? $"없음(먹힌 라운드 {shieldPickedRound})")}");
            Log("[킬더킹] 통계 | " + string.Join(" / ", statsByTeam.Select(p =>
                $"{p.Key} 준 {p.Value.DamageDealt}, 받은 {p.Value.DamageTaken}, 잡기 {p.Value.Captures}, 탈락시킴 {p.Value.Kills}")));
        }

        // ===================================================================
        // 제한 턴이 끝났을 때 (TestWinConditionManager.HandleTimeLimitReached에서 킬 더 킹이면 이쪽으로)
        // 모든 플레이어 HP가 동시에 1씩 계속 줄어서, HP가 남은 팀이 1팀(1등) 또는 같은 HP 여러 팀(공동 1등)만
        // 남으면 멈춤. 1팀이면 승리, 여러 팀이면 서든데스. 모두 HP가 같으면 줄지 않고 바로 서든데스
        // 예: 5·3·2 -> 2·0·0 (1등) / 4·4·2 -> 2·2·0 (서든데스)
        // = 두 번째로 높은 HP만큼 한 번에 빼는 것과 같음
        // ===================================================================
        public void OnTurnLimitReached()
        {
            var alive = hpByTeam.Keys.Where(t => host.IsTeamActive(t)).ToList();
            if (alive.Count <= 1) return;
            Log("[킬더킹] 제한 턴 종료 | " + string.Join(", ", alive.Select(t => $"{t} HP {GetHp(t)}")));

            int top = alive.Max(t => GetHp(t));
            var lower = alive.Where(t => GetHp(t) < top).ToList();
            if (lower.Count > 0)
            {
                int drain = lower.Max(t => GetHp(t));
                foreach (var team in alive)
                    SetHp(team, GetHp(team) - drain);
                Log($"[킬더킹] 전원 HP -{drain} | " + string.Join(", ", alive.Select(t => $"{t} HP {GetHp(t)}")));
                foreach (var team in lower)
                {
                    Log($"[킬더킹] {team} 탈락 (제한 턴 HP 감소)");
                    host.EliminateTeam(team);
                    shieldEffectsByTeam[team].Clear();
                    eliminatedRoundByTeam[team] = CurrentRound;
                    OnTeamEliminated?.Invoke(team, TeamSlot.None);
                }
            }

            if (host.ActiveTeamCount == 1)
            {
                host.DeclareWinner(host.FirstActiveTeam, GameResultType.TimeOver);
                return;
            }
            StartSuddenDeath();
        }

        // 서든데스: 남은 팀끼리 공격 대상을 다시 이음(탈락자가 순서에서 빠져서 자동), 라운드 제한 없음
        // 말이 가진 칼과 보드 위 주인 없는 칼이 전부 [마검], 보드에 칼이 없으면 바로 생성 (기획 12번)
        // (탈락자 능력 금지는 탈락자 능력 단계에서)
        private void StartSuddenDeath()
        {
            IsSuddenDeath = true;
            Log("[킬더킹] 서든데스 시작 | " + string.Join(", ",
                hpByTeam.Keys.Where(t => host.IsTeamActive(t)).Select(t => $"{t} HP {GetHp(t)} → 대상 {GetAttackTarget(t)}")));
            OnSuddenDeathStarted?.Invoke();

            Log("[킬더킹] 서든데스: 말이 가진 칼과 보드 위 칼을 전부 마검으로 바꿔달라고 요청");
            OnConvertSwordsToDemonRequested?.Invoke();
            if (BoardSword != null)
                BoardSword = KtkSword.Demon;
            else
                SpawnSword(KtkSword.Demon);
        }

        // ===================================================================
        // 아이템(칼·방패) 생성 시점과 종류 (기획 4-2, 6, 12)
        // - 보드 위 주인 없는 칼·방패는 각각 최대 1개, 1라운드엔 없고 2라운드부터 라운드 시작 때 생성
        // - 재생성: 먹힌 라운드를 빼고 칼은 2라운드, 방패는 4라운드를 비운 뒤 다음 라운드
        //   (3라운드에 먹힘 -> 칼 6라운드, 방패 8라운드). 서든데스 칼은 1라운드만 비움 (3 -> 5)
        // - 종류: [아레나의 열기] 전엔 일반만, 후엔 확률. 서든데스 칼은 전부 [마검]
        // 칸을 골라 놓는 것과 말이 먹는 건 보드 쪽 일: 아래 요청 이벤트를 받아 칸에 놓고,
        // 말이 먹으면 NotifySwordPickedUp / NotifyShieldPickedUp 을 불러주면 재생성 타이머가 돎
        // ===================================================================
        private const int FirstItemRound = 2;
        private const int SwordRespawnGapRounds = 2;
        private const int ShieldRespawnGapRounds = 4;
        private const int SuddenDeathSwordRespawnGapRounds = 1;

        public KtkSword? BoardSword { get; private set; }     // 보드 위 주인 없는 칼 (없으면 null)
        public KtkShield? BoardShield { get; private set; }   // 보드 위 주인 없는 방패 (없으면 null)
        public bool IsArenaHeat { get; private set; }
        public int CurrentRound { get; private set; }          // 마지막으로 시작된 라운드
        private int swordPickedRound;    // 마지막으로 칼이 먹힌 라운드 (아직 없으면 0)
        private int shieldPickedRound;
        private int arenaHeatStartRound; // 이 라운드가 시작될 때 [아레나의 열기] 시작

        // ---- 보드(영서)·UI 연결용 ----
        public System.Action<KtkSword> OnSwordSpawnRequested;    // 보드: 규칙에 맞는 칸에 이 칼을 놓기
        public System.Action<KtkShield> OnShieldSpawnRequested;  // 보드: 규칙에 맞는 칸에 이 방패를 놓기
        public System.Action OnConvertSwordsToDemonRequested;    // 보드: 판 위 칼, 말이 가진 칼 전부 [마검]으로
        public System.Action OnArenaHeatStarted;                 // UI: "아레나의 열기가 뜨거워져 아이템이 강화됩니다!"

        // TestTurnManager.OnRoundStarted 에서 불림 (TestWinConditionManager가 연결)
        public void OnRoundStarted(int round)
        {
            if (settings == null || settings.gameMode != GameMode.KillTheKing) return;
            if (round <= CurrentRound) return; // 같은 라운드 중복 처리 방지
            CurrentRound = round;
            GiveGhostOrbs();

            if (!IsArenaHeat && round >= arenaHeatStartRound)
                StartArenaHeat();
            if (round < FirstItemRound) return;

            int swordGap = IsSuddenDeath ? SuddenDeathSwordRespawnGapRounds : SwordRespawnGapRounds;
            if (BoardSword == null && (swordPickedRound == 0 || round > swordPickedRound + swordGap))
                SpawnSword(RollSword());
            if (BoardShield == null && (shieldPickedRound == 0 || round > shieldPickedRound + ShieldRespawnGapRounds))
                SpawnShield(RollShield());
        }

        private void StartArenaHeat()
        {
            IsArenaHeat = true;
            Log($"[킬더킹] {CurrentRound}라운드: 아레나의 열기가 뜨거워져 아이템이 강화됩니다!");
            OnArenaHeatStarted?.Invoke();
        }

        // 칼 종류: 서든데스 = 마검 / 열기 전 = 일반 검 / 열기 후 = 일반 25%, 쌍검 60%, 흡혈 15%
        internal KtkSword RollSword()
        {
            if (IsSuddenDeath) return KtkSword.Demon;
            if (!IsArenaHeat) return KtkSword.Normal;
            float r = UnityEngine.Random.value;
            if (r < 0.25f) return KtkSword.Normal;
            if (r < 0.85f) return KtkSword.Twin;
            return KtkSword.Vampire;
        }

        // 방패 종류: 열기 전 = 일반 방패 / 열기 후 = 일반 60%, 가시 35%, 반사의 거울 5%
        internal KtkShield RollShield()
        {
            if (!IsArenaHeat) return KtkShield.Normal;
            float r = UnityEngine.Random.value;
            if (r < 0.60f) return KtkShield.Normal;
            if (r < 0.95f) return KtkShield.Thorn;
            return KtkShield.Mirror;
        }

        private void SpawnSword(KtkSword sword)
        {
            BoardSword = sword;
            Log($"[킬더킹] {CurrentRound}라운드: 칼 생성 → {sword}" + (OnSwordSpawnRequested == null ? " (보드 연결 전, 로그만)" : ""));
            OnSwordSpawnRequested?.Invoke(sword);
        }

        private void SpawnShield(KtkShield shield)
        {
            BoardShield = shield;
            Log($"[킬더킹] {CurrentRound}라운드: 방패 생성 → {shield}" + (OnShieldSpawnRequested == null ? " (보드 연결 전, 로그만)" : ""));
            OnShieldSpawnRequested?.Invoke(shield);
        }

        // 보드 쪽이 부름: 보드 위 주인 없는 칼/방패를 말이 먹었을 때 (재생성 타이머 시작)
        // picker = 먹은 팀 (결과창 "아이템 획득 수" 통계용, 모르면 None)
        public void NotifySwordPickedUp(TeamSlot picker = TeamSlot.None)
        {
            if (BoardSword == null) return;
            Log($"[킬더킹] {CurrentRound}라운드: 보드 위 {BoardSword} 먹힘 → 재생성 대기");
            BoardSword = null;
            swordPickedRound = CurrentRound;
            if (statsByTeam.TryGetValue(picker, out var stats)) stats.ItemsPicked++;
        }

        public void NotifyShieldPickedUp(TeamSlot picker = TeamSlot.None)
        {
            if (BoardShield == null) return;
            Log($"[킬더킹] {CurrentRound}라운드: 보드 위 {BoardShield} 먹힘 → 재생성 대기");
            BoardShield = null;
            shieldPickedRound = CurrentRound;
            if (statsByTeam.TryGetValue(picker, out var stats)) stats.ItemsPicked++;
        }

        // 보드 쪽이 부름: 아이템 가진 묶음을 잡아서 아이템을 뺏었을 때 (통계용)
        public void NotifyItemsStolen(TeamSlot thief, int count)
        {
            if (count > 0 && statsByTeam.TryGetValue(thief, out var stats)) stats.ItemsStolen += count;
        }

        // ===================================================================
        // 결과창 수치 (기획 14번). 데미지·탈락은 여기서 자동으로 세고, 잡기는 턴 알림을 받아 RecordCapture,
        // 아이템 획득·뺏기는 보드 쪽이 Notify...를 불러줄 때 셈
        // ===================================================================
        private readonly Dictionary<TeamSlot, KtkTeamStats> statsByTeam = new Dictionary<TeamSlot, KtkTeamStats>();

        public KtkTeamStats GetStats(TeamSlot team)
            => statsByTeam.TryGetValue(team, out var stats) ? stats : new KtkTeamStats();

        public void RecordCapture(TeamSlot team)
        {
            if (statsByTeam.TryGetValue(team, out var stats)) stats.Captures++;
        }

        // ===================================================================
        // [탈락자 능력] (기획 9번) - 개인전만 (팀전은 탈락이 곧 게임 종료라 없음)
        // - 탈락한 뒤 라운드가 시작될 때마다 [원혼 구슬] +1, 게임 끝까지 쌓임
        // - 능력 3개는 각각 한 번씩만, 아무 턴에나 사용 (쓰는 화면 흐름은 기획서도 "추후 작업")
        // - 서든데스에서는 못 씀
        // [저주] 칸 고르기·SP -1·2라운드 뒤 사라짐은 보드·스킬 쪽 일 -> OnCurseRequested 로 요청만
        // [영혼 주입] 보드 위 칼 종류는 여기서 들고 있어서 바로 강화, 보드에는 OnBoardSwordUpgraded 로 알림
        // [죽은 자의 염원] settings.maxTurnCount 를 1 줄임 (턴 매니저가 같은 설정을 봄, 아레나의 열기 기준은 안 바뀜)
        // ===================================================================
        private readonly Dictionary<TeamSlot, int> orbsByTeam = new Dictionary<TeamSlot, int>();
        private readonly Dictionary<TeamSlot, HashSet<KtkGhostAbility>> usedAbilitiesByTeam = new Dictionary<TeamSlot, HashSet<KtkGhostAbility>>();
        private readonly Dictionary<TeamSlot, int> eliminatedRoundByTeam = new Dictionary<TeamSlot, int>(); // HP 0으로 탈락한 라운드

        public System.Action<TeamSlot, int> OnOrbsChanged;              // 탈락 팀, 지금 구슬 수
        public System.Action<TeamSlot, KtkGhostAbility> OnGhostAbilityUsed;
        public System.Action<TeamSlot> OnCurseRequested;                // 보드: 말 없는 랜덤 칸(꼭짓점·중앙 제외)에 저주 칸
        public System.Action<KtkSword> OnBoardSwordUpgraded;            // 보드: 판 위 주인 없는 칼 모양 바꾸기
        public System.Action<int> OnMaxRoundChanged;                    // UI: 바뀐 제한 턴

        public static int GetAbilityCost(KtkGhostAbility ability) => ability == KtkGhostAbility.DeadWish ? 5 : 3;
        public int GetOrbs(TeamSlot team) => orbsByTeam.TryGetValue(team, out var orbs) ? orbs : 0;
        public bool HasUsedAbility(TeamSlot team, KtkGhostAbility ability)
            => usedAbilitiesByTeam.TryGetValue(team, out var used) && used.Contains(ability);

        private void GiveGhostOrbs()
        {
            foreach (var pair in eliminatedRoundByTeam)
            {
                if (CurrentRound <= pair.Value) continue; // 탈락한 다음 라운드부터
                orbsByTeam[pair.Key] = GetOrbs(pair.Key) + 1;
                Log($"[킬더킹] {CurrentRound}라운드: {pair.Key} 원혼 구슬 +1 → {orbsByTeam[pair.Key]}");
                OnOrbsChanged?.Invoke(pair.Key, orbsByTeam[pair.Key]);
            }
        }

        // 쓸 수 있으면 쓰고 true. 못 쓰면 false + 이유
        public bool TryUseGhostAbility(TeamSlot team, KtkGhostAbility ability, out string reason)
        {
            reason = GetGhostAbilityBlockReason(team, ability);
            if (reason != null)
            {
                Log($"[킬더킹] {team} {ability} 사용 불가: {reason}");
                return false;
            }

            int cost = GetAbilityCost(ability);
            orbsByTeam[team] -= cost;
            usedAbilitiesByTeam[team].Add(ability);
            Log($"[킬더킹] {CurrentRound}라운드: {team} {ability} 사용 (구슬 -{cost} → {orbsByTeam[team]})");
            OnOrbsChanged?.Invoke(team, orbsByTeam[team]);

            switch (ability)
            {
                case KtkGhostAbility.Curse:
                    Log("[킬더킹] 저주 칸 만들어달라고 요청 (다음 턴부터 적용)" + (OnCurseRequested == null ? " (보드 연결 전, 로그만)" : ""));
                    OnCurseRequested?.Invoke(team);
                    break;
                case KtkGhostAbility.SoulInfusion:
                    KtkSword upgraded = RollSoulInfusion();
                    Log($"[킬더킹] 영혼 주입: 보드 위 일반 검 → {upgraded}");
                    BoardSword = upgraded;
                    OnBoardSwordUpgraded?.Invoke(upgraded);
                    break;
                case KtkGhostAbility.DeadWish:
                    settings.maxTurnCount -= 1;
                    Log($"[킬더킹] 죽은 자의 염원: 제한 턴 → {settings.maxTurnCount}");
                    OnMaxRoundChanged?.Invoke(settings.maxTurnCount);
                    break;
            }
            OnGhostAbilityUsed?.Invoke(team, ability);
            return true;
        }

        // 못 쓰는 이유 (쓸 수 있으면 null). UI가 버튼을 막을 때도 씀
        public string GetGhostAbilityBlockReason(TeamSlot team, KtkGhostAbility ability)
        {
            if (settings == null || settings.isTeamMode) return "팀전은 탈락자 능력 없음";
            if (!eliminatedRoundByTeam.ContainsKey(team)) return "탈락한 플레이어만 사용 가능";
            if (host.ActiveTeamCount <= 1) return "게임이 끝남";
            if (IsSuddenDeath) return "서든데스에서는 사용 불가";
            if (HasUsedAbility(team, ability)) return "이미 사용함 (능력마다 한 번)";
            int cost = GetAbilityCost(ability);
            if (GetOrbs(team) < cost) return $"구슬 부족 ({GetOrbs(team)}/{cost})";
            if (ability == KtkGhostAbility.SoulInfusion && BoardSword != KtkSword.Normal)
                return "보드 위에 주인 없는 일반 검이 없음";
            if (ability == KtkGhostAbility.DeadWish && (settings.maxTurnCount <= 0 || CurrentRound >= settings.maxTurnCount))
                return "마지막 라운드에는 사용 불가";
            return null;
        }

        // [영혼 주입] 강화 확률: 쌍검 75% / 흡혈 검 24% / 마검 1%
        internal KtkSword RollSoulInfusion()
        {
            float r = UnityEngine.Random.value;
            if (r < 0.75f) return KtkSword.Twin;
            if (r < 0.99f) return KtkSword.Vampire;
            return KtkSword.Demon;
        }

        // 테스트용: 첫 번째 탈락 팀 (우클릭 메뉴로 탈락자 능력 써볼 때)
        public TeamSlot FirstEliminatedTeam => eliminatedRoundByTeam.Count > 0 ? eliminatedRoundByTeam.Keys.First() : TeamSlot.None;

        // 테스트용: 라운드가 4턴씩 안 돌아도 "다음 라운드 시작" 처리만 해봄 (아이템 생성 타이머 확인용)
        public void TestStartNextRound() => OnRoundStarted(CurrentRound + 1);
        // 자동 테스트용: HP를 바로 정함 (시작 HP 넘지 않음)
        internal void SetHpForTest(TeamSlot team, int hp) => SetHp(team, hp);
        public void TestStartArenaHeat() { if (!IsArenaHeat) StartArenaHeat(); }

        private void SetHp(TeamSlot team, int hp)
        {
            hpByTeam[team] = System.Math.Max(0, System.Math.Min(StartHp, hp));
            OnHpChanged?.Invoke(team, hpByTeam[team]);
        }
    }
}
