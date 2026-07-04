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
