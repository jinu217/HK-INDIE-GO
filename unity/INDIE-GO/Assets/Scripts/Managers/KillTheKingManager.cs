using System.Collections.Generic;
using System.Linq;
using YutArena.Common;
using YutArena.Managers.GameProgress;

namespace YutArena.Managers
{
    // ===================================================================
    // 킬 더 킹 모드 전용 상태와 규칙 (기획 "킬 더 킹 모드 룰")
    // 턴, 라운드, 탈출 말 1턴 쉬기, 탈락자 턴 빼기, 게임 끝내기는 기존 흐름을 그대로 쓰고,
    // 분기점(말 탈출, 턴 수 끝)에서 TestWinConditionManager가 킬 더 킹이면 여기로 넘김.
    // 모든 값은 팀(TeamSlot) 기준: 개인전은 팀 = 사람, 2인 1조·2대2는 팀 HP를 같이 씀
    //
    // 1단계: HP, 공격 대상, 탈출 데미지(말 수), 탈락, 킬 보상, 최후 1인 승리,
    //        턴 끝 HP 동시 감소 -> 1등 or 서든데스
    // 2단계: [방패 효과] 쌓기·소모, 탈출 처리 순서 10단계(가시·반사 핑퐁, 흡혈, 마검) -> KtkDamageResolver
    // (아이템 생성·획득·아레나의 열기·탈락자 능력은 다음 단계)
    // ===================================================================
    public class KillTheKingManager
    {
        // 밸런스 작업 전 임시 수치 (기획: HP 개인전 5~10, 팀 HP는 [2인 1조]/[2대 2] 따로, 제한 턴은 넉넉하게)
        public const int TempSoloStartHp = 7;
        public const int TempSharedTeamStartHp = 7;   // 2인 1조
        public const int TempTwoVsTwoStartHp = 10;    // 2대2
        public const int TempMaxRound = 20;

        private readonly TestWinConditionManager winConditionManager;
        private readonly TestTurnManager turnManager;
        private GameStartSettings settings;
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

        public KillTheKingManager(TestWinConditionManager winConditionManager, TestTurnManager turnManager)
        {
            this.winConditionManager = winConditionManager;
            this.turnManager = turnManager;
            resolver = new KtkDamageResolver(this);
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
            foreach (var team in teams)
            {
                hpByTeam[team] = StartHp;
                shieldEffectsByTeam[team] = new List<KtkShield>();
            }
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
            var order = turnManager.TurnOrder.order;
            int start = order.FindIndex(p => MatchCompositionRule.GetPersonTeamSlot(settings, p) == attacker);
            if (start < 0) return TeamSlot.None;
            for (int step = 1; step <= order.Count; step++)
            {
                TeamSlot team = MatchCompositionRule.GetPersonTeamSlot(settings, order[(start + step) % order.Count]);
                if (team != attacker && winConditionManager.IsTeamActive(team)) return team;
            }
            return TeamSlot.None;
        }

        // 나를 노리는 팀 (UI 표시용). 개인전은 턴 순서상 내 바로 앞
        public TeamSlot GetAttacker(TeamSlot target)
        {
            foreach (var team in hpByTeam.Keys)
                if (team != target && winConditionManager.IsTeamActive(team) && GetAttackTarget(team) == target)
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
            if (escapedCount <= 0 || !winConditionManager.IsTeamActive(attacker)) return;
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
        internal bool IsTeamActive(TeamSlot team) => winConditionManager.IsTeamActive(team);
        internal bool IsGameOver => winConditionManager.ActiveTeamCount <= 1;

        // 데미지 적용: 남은 HP보다 큰 만큼은 버리고, 실제로 깎인 양을 돌려줌
        internal int ApplyDamage(TeamSlot from, TeamSlot to, int amount)
        {
            int before = GetHp(to);
            int dealt = System.Math.Min(amount, before);
            SetHp(to, before - dealt);
            QaDebugLog.Event($"[킬더킹] {from} → {to} 데미지 {amount} (실제 {dealt}) | {to} HP {before} → {GetHp(to)}");
            OnDamaged?.Invoke(from, to, dealt);
            return dealt;
        }

        // 회복 (시작 HP 넘지 않음)
        internal void Heal(TeamSlot team, int amount, string reason)
        {
            int before = GetHp(team);
            SetHp(team, before + amount);
            QaDebugLog.Event($"[킬더킹] {team} {reason} 회복 | HP {before} → {GetHp(team)}");
        }

        // HP 0이면 탈락 처리: 턴에서 빼고 판 위 말 내림 -> 킬 보상 -> 1팀 남으면 승리. 탈락했으면 true
        internal bool KnockOutIfDead(TeamSlot team, TeamSlot killer)
        {
            if (GetHp(team) > 0 || !winConditionManager.IsTeamActive(team)) return false;
            QaDebugLog.Event($"[킬더킹] {team} 탈락" + (killer != TeamSlot.None ? $" (탈락시킨 팀 {killer})" : ""));
            winConditionManager.EliminateTeamForKillTheKing(team);
            shieldEffectsByTeam[team].Clear();
            OnTeamEliminated?.Invoke(team, killer);

            // [킬 보상] 적을 탈락시키면 HP +1 (시작 HP 넘지 않음, 이미 탈락한 팀은 못 받음)
            // 팀전은 탈락이 곧 게임 종료라 아래 승리 처리로 바로 끝남
            if (killer != TeamSlot.None && winConditionManager.IsTeamActive(killer) && GetHp(killer) < StartHp)
                Heal(killer, 1, "킬 보상");

            if (winConditionManager.ActiveTeamCount == 1)
                winConditionManager.DeclareKillTheKingWinner(winConditionManager.FirstActiveTeam, GameResultType.TeamWin);
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
            QaDebugLog.Event($"[킬더킹] {team} 방패 효과 +{shield} | 지금 [{string.Join(", ", list)}]");
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
            QaDebugLog.Event($"[킬더킹] 테스트: 다음 탈출에 {sword} 붙임 | [{string.Join(", ", testSwordsForNextEscape)}]");
        }

        // 테스트용: 지금 상태 한 번에 출력
        public void LogState()
        {
            QaDebugLog.Event("[킬더킹] 상태 | " + string.Join(" / ", hpByTeam.Keys.Select(t =>
                $"{t} HP {GetHp(t)}" + (winConditionManager.IsTeamActive(t)
                    ? $", 대상 {GetAttackTarget(t)}, 방패 [{string.Join(", ", GetShieldEffects(t))}]"
                    : " (탈락)"))) + (IsSuddenDeath ? " / 서든데스" : ""));
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
            var alive = hpByTeam.Keys.Where(t => winConditionManager.IsTeamActive(t)).ToList();
            if (alive.Count <= 1) return;
            QaDebugLog.Event("[킬더킹] 제한 턴 종료 | " + string.Join(", ", alive.Select(t => $"{t} HP {GetHp(t)}")));

            int top = alive.Max(t => GetHp(t));
            var lower = alive.Where(t => GetHp(t) < top).ToList();
            if (lower.Count > 0)
            {
                int drain = lower.Max(t => GetHp(t));
                foreach (var team in alive)
                    SetHp(team, GetHp(team) - drain);
                QaDebugLog.Event($"[킬더킹] 전원 HP -{drain} | " + string.Join(", ", alive.Select(t => $"{t} HP {GetHp(t)}")));
                foreach (var team in lower)
                {
                    QaDebugLog.Event($"[킬더킹] {team} 탈락 (제한 턴 HP 감소)");
                    winConditionManager.EliminateTeamForKillTheKing(team);
                    OnTeamEliminated?.Invoke(team, TeamSlot.None);
                }
            }

            if (winConditionManager.ActiveTeamCount == 1)
            {
                winConditionManager.DeclareKillTheKingWinner(winConditionManager.FirstActiveTeam, GameResultType.TimeOver);
                return;
            }
            StartSuddenDeath();
        }

        // 서든데스: 남은 팀끼리 공격 대상을 다시 이음(탈락자가 순서에서 빠져서 자동), 라운드 제한 없음
        // (칼이 전부 [마검]으로 바뀌는 것, 탈락자 능력 금지는 아이템·탈락자 능력 단계에서)
        private void StartSuddenDeath()
        {
            IsSuddenDeath = true;
            QaDebugLog.Event("[킬더킹] 서든데스 시작 | " + string.Join(", ",
                hpByTeam.Keys.Where(t => winConditionManager.IsTeamActive(t)).Select(t => $"{t} HP {GetHp(t)} → 대상 {GetAttackTarget(t)}")));
            OnSuddenDeathStarted?.Invoke();
        }

        private void SetHp(TeamSlot team, int hp)
        {
            hpByTeam[team] = System.Math.Max(0, System.Math.Min(StartHp, hp));
            OnHpChanged?.Invoke(team, hpByTeam[team]);
        }
    }
}
