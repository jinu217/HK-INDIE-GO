using YutArena.Common;

namespace YutArena.Online
{
    public static class OnlineLobbyLabels
    {
        public static string Mode(int? mode) => mode switch
        {
            (int)GameMode.Classic => "클래식",
            (int)GameMode.Escape => "이스케이프",
            (int)GameMode.KillTheKing => "킬 더 킹",
            _ => "—"
        };
        public static string Map(int map) => (MapType)map switch
        {
            MapType.Random => "랜덤",
            MapType.Basic => "기본 맵",
            MapType.Grassland => "초원",
            MapType.Korean => "한국",
            _ => "—"
        };
        public static string Composition(RoomSettingsData settings) => !settings.isTeamMode ? "개인전" :
            settings.teamMode == TeamMode.TeamMode1 ? "팀전 · 2인 1조" : "팀전 · 2대2";
    }
}
