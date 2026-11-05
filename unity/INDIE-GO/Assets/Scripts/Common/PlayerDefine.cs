namespace YutArena.Common
{
    // 플레이어의 고정 자리 번호
    public enum PlayerSlot
    {
        None = 0,     // 플레이어 없음 또는 아직 지정되지 않음

        Player1 = 1,  // 1번 플레이어
        Player2 = 2,  // 2번 플레이어
        Player3 = 3,  // 3번 플레이어
        Player4 = 4,  // 4번 플레이어

        Player5 = 5,  // 추후 확장용 5번 플레이어
        Player6 = 6,  // 추후 확장용 6번 플레이어
        Player7 = 7,  // 추후 확장용 7번 플레이어
        Player8 = 8   // 추후 확장용 8번 플레이어
    }

    // 팀 구분값
    public enum TeamSlot
    {
        None = 0,  // 팀 없음 또는 아직 지정되지 않음

        TeamA = 1, // A팀
        TeamB = 2, // B팀
        TeamC = 3, // C팀
        TeamD = 4, // D팀

        TeamE = 5, // 추후 확장용
        TeamF = 6, // 추후 확장용
        TeamG = 7, // 추후 확장용
        TeamH = 8  // 추후 확장용
    }

    // 대전 구성
    // 데모에서는 1vs1, 1vs1vs1, 1vs1vs1vs1, 2vs2만 사용
    // 3vs3, 4vs4, 2vs2vs2, 2vs2vs2vs2는 추후 8인 확장용
    public enum MatchComposition
    {
        None,                 // 대전 구성이 아직 정해지지 않음

        OneVsOne,             // 1vs1, 플레이어 2명
        OneVsOneVsOne,        // 1vs1vs1, 플레이어 3명 개인전
        OneVsOneVsOneVsOne,   // 1vs1vs1vs1, 플레이어 4명 개인전
        TwoVsTwo,             // 2vs2, 플레이어 4명 팀전

        ThreeVsThree,         // 3vs3, 플레이어 6명 팀전, 추후 확장용
        FourVsFour,           // 4vs4, 플레이어 8명 팀전, 추후 확장용
        TwoVsTwoVsTwo,        // 2vs2vs2, 플레이어 6명 3팀전, 추후 확장용
        TwoVsTwoVsTwoVsTwo    // 2vs2vs2vs2, 플레이어 8명 4팀전, 추후 확장용
    }

    // 대전 구성에 따른 플레이어 수와 팀 배정을 알려주는 공통 규칙
    public static class MatchCompositionRule
    {
        // 대전 구성에 필요한 플레이어 수를 반환
        public static int GetPlayerCount(MatchComposition composition)
        {
            return composition switch
            {
                MatchComposition.OneVsOne => 2,
                MatchComposition.OneVsOneVsOne => 3,
                MatchComposition.OneVsOneVsOneVsOne => 4,
                MatchComposition.TwoVsTwo => 4,

                MatchComposition.ThreeVsThree => 6,
                MatchComposition.FourVsFour => 8,
                MatchComposition.TwoVsTwoVsTwo => 6,
                MatchComposition.TwoVsTwoVsTwoVsTwo => 8,
                _ => 0
            };
        }

        // 대전 구성과 플레이어 번호를 기준으로 해당 플레이어의 팀을 반환
        public static TeamSlot GetTeamSlot(MatchComposition composition, PlayerSlot player)
        {
            return composition switch
            {
                MatchComposition.OneVsOne => player switch
                {
                    PlayerSlot.Player1 => TeamSlot.TeamA,
                    PlayerSlot.Player2 => TeamSlot.TeamB,
                    _ => TeamSlot.None
                },

                MatchComposition.OneVsOneVsOne => player switch
                {
                    PlayerSlot.Player1 => TeamSlot.TeamA,
                    PlayerSlot.Player2 => TeamSlot.TeamB,
                    PlayerSlot.Player3 => TeamSlot.TeamC,
                    _ => TeamSlot.None
                },

                MatchComposition.OneVsOneVsOneVsOne => player switch
                {
                    PlayerSlot.Player1 => TeamSlot.TeamA,
                    PlayerSlot.Player2 => TeamSlot.TeamB,
                    PlayerSlot.Player3 => TeamSlot.TeamC,
                    PlayerSlot.Player4 => TeamSlot.TeamD,
                    _ => TeamSlot.None
                },

                MatchComposition.TwoVsTwo => player switch
                {
                    PlayerSlot.Player1 => TeamSlot.TeamA,
                    PlayerSlot.Player2 => TeamSlot.TeamA,
                    PlayerSlot.Player3 => TeamSlot.TeamB,
                    PlayerSlot.Player4 => TeamSlot.TeamB,
                    _ => TeamSlot.None
                },

                MatchComposition.ThreeVsThree => player switch
                {
                    PlayerSlot.Player1 => TeamSlot.TeamA,
                    PlayerSlot.Player2 => TeamSlot.TeamA,
                    PlayerSlot.Player3 => TeamSlot.TeamA,
                    PlayerSlot.Player4 => TeamSlot.TeamB,
                    PlayerSlot.Player5 => TeamSlot.TeamB,
                    PlayerSlot.Player6 => TeamSlot.TeamB,
                    _ => TeamSlot.None
                },

                MatchComposition.FourVsFour => player switch
                {
                    PlayerSlot.Player1 => TeamSlot.TeamA,
                    PlayerSlot.Player2 => TeamSlot.TeamA,
                    PlayerSlot.Player3 => TeamSlot.TeamA,
                    PlayerSlot.Player4 => TeamSlot.TeamA,
                    PlayerSlot.Player5 => TeamSlot.TeamB,
                    PlayerSlot.Player6 => TeamSlot.TeamB,
                    PlayerSlot.Player7 => TeamSlot.TeamB,
                    PlayerSlot.Player8 => TeamSlot.TeamB,
                    _ => TeamSlot.None
                },

                MatchComposition.TwoVsTwoVsTwo => player switch
                {
                    PlayerSlot.Player1 => TeamSlot.TeamA,
                    PlayerSlot.Player2 => TeamSlot.TeamA,
                    PlayerSlot.Player3 => TeamSlot.TeamB,
                    PlayerSlot.Player4 => TeamSlot.TeamB,
                    PlayerSlot.Player5 => TeamSlot.TeamC,
                    PlayerSlot.Player6 => TeamSlot.TeamC,
                    _ => TeamSlot.None
                },

                MatchComposition.TwoVsTwoVsTwoVsTwo => player switch
                {
                    PlayerSlot.Player1 => TeamSlot.TeamA,
                    PlayerSlot.Player2 => TeamSlot.TeamA,
                    PlayerSlot.Player3 => TeamSlot.TeamB,
                    PlayerSlot.Player4 => TeamSlot.TeamB,
                    PlayerSlot.Player5 => TeamSlot.TeamC,
                    PlayerSlot.Player6 => TeamSlot.TeamC,
                    PlayerSlot.Player7 => TeamSlot.TeamD,
                    PlayerSlot.Player8 => TeamSlot.TeamD,
                    _ => TeamSlot.None
                },

                _ => TeamSlot.None
            };
        }

        // ===================================================================
        // 버그 수정: 로비에서 고른 커스텀 팀 배정(settings.playerTeams)을 반영하는 오버로드.
        // 예전엔 항상 위의 GetTeamSlot(composition, player)만 써서 P1+P2, P3+P4처럼
        // 고정 배정만 나왔음 - 로비에서 P1+P3을 같은 팀으로 골라도 무시되던 버그.
        //
        // playerTeams[i]는 0=팀 없음, 1=Red(TeamA), 2=Blue(TeamB)를 담는다.
        // 그래서 isTeamMode가 true일 때만 이 값을 쓰고, 아니면 기존 고정 배정으로 대체함.
        // ===================================================================
        // ※ 여기서 player 는 "게임 속 캐릭터(말 주인) 번호"다.
        //   개인전·2대2는 사람 번호 = 캐릭터 번호라 기존과 똑같고,
        //   2인 1조는 캐릭터가 팀당 1개라 1번 = 빨강(TeamA), 2번 = 파랑(TeamB)으로 답한다.
        //   사람(1P~4P)의 팀이 필요하면 아래 GetPersonTeamSlot 을 쓴다.
        public static TeamSlot GetTeamSlot(GameStartSettings settings, PlayerSlot player)
        {
            if (UsesSharedTeamCharacter(settings))
            {
                return player switch
                {
                    PlayerSlot.Player1 => TeamSlot.TeamA,
                    PlayerSlot.Player2 => TeamSlot.TeamB,
                    _ => TeamSlot.None
                };
            }
            return GetPersonTeamSlot(settings, player);
        }

        // ===================================================================
        // 2인 1조(TeamMode1) 팀 캐릭터 규칙 (팀전 기획 2번)
        // - 팀이 캐릭터 1개, 말, 남은 말 칸, 스킬 포인트·쿨타임을 함께 쓴다 → 게임 속 캐릭터는 팀당 1개(총 2개)
        // - 캐릭터 번호는 팀 색으로 정한다: 빨강(TeamA) = 캐릭터 1번, 파랑(TeamB) = 캐릭터 2번
        //   (선공·후공과 상관없는 이름표. 순서 정하기는 사람 기준으로 따로 함)
        // - 턴은 사람(1P~4P) 단위로 돌고, 자기 턴인 사람이 자기 팀 캐릭터를 조작한다
        // 개인전·2대2에서는 사람 번호 = 캐릭터 번호라 아래 함수들이 기존과 같은 값을 준다.
        // ===================================================================

        // 2인 1조(팀이 캐릭터 1개를 같이 쓰는 방식)인지
        public static bool UsesSharedTeamCharacter(GameStartSettings settings)
        {
            return settings != null && settings.isTeamMode && settings.teamMode == TeamMode.TeamMode1;
        }

        // 사람(1P~4P)의 팀. 대기실 팀 배정(playerTeams: 1=빨강 TeamA, 2=파랑 TeamB) 그대로.
        // 순서 정하기, 타이브레이커처럼 "사람"이 기준인 곳에서 쓴다.
        public static TeamSlot GetPersonTeamSlot(GameStartSettings settings, PlayerSlot person)
        {
            int index = (int)person - 1;
            if (settings != null && settings.isTeamMode &&
                settings.playerTeams != null && index >= 0 && index < settings.playerTeams.Length)
            {
                return (TeamSlot)settings.playerTeams[index];
            }
            return settings != null
                ? GetTeamSlot(settings.matchComposition, person)
                : TeamSlot.None;
        }

        // 이 사람이 자기 턴에 조작하는 캐릭터 번호.
        // 2인 1조: 빨강 팀 사람 → 캐릭터 1번, 파랑 팀 사람 → 캐릭터 2번 / 그 외: 자기 자신
        public static PlayerSlot GetControlledCharacter(GameStartSettings settings, PlayerSlot person)
        {
            if (!UsesSharedTeamCharacter(settings)) return person;
            return GetPersonTeamSlot(settings, person) switch
            {
                TeamSlot.TeamA => PlayerSlot.Player1,
                TeamSlot.TeamB => PlayerSlot.Player2,
                _ => PlayerSlot.None
            };
        }

        // 게임 속 캐릭터(말 주인) 수. 2인 1조는 2, 그 외는 사람 수와 같다. (말 생성할 때 사용)
        public static int GetCharacterCount(GameStartSettings settings)
        {
            if (settings == null) return 0;
            return UsesSharedTeamCharacter(settings) ? 2 : settings.playerCount;
        }

        // 해당 대전 구성에서 유효한 플레이어인지 확인
        public static bool IsValidPlayer(MatchComposition composition, PlayerSlot player)
        {
            return GetTeamSlot(composition, player) != TeamSlot.None;
        }

        // 팀전 대전 구성인지 확인
        public static bool IsTeamMode(MatchComposition composition)
        {
            return composition switch
            {
                MatchComposition.TwoVsTwo => true,
                MatchComposition.ThreeVsThree => true,
                MatchComposition.FourVsFour => true,
                MatchComposition.TwoVsTwoVsTwo => true,
                MatchComposition.TwoVsTwoVsTwoVsTwo => true,
                _ => false
            };
        }
    }
}
