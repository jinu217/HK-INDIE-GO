# 팀전 모드 전달

- Team1Btn → TeamMode.TeamMode1 (0)
- Team2Btn → TeamMode.TeamMode2 (1)
- 기본 선택: TeamMode1
- 로비 게임 시작 시 RoomSettingsData.teamMode에서 GameStartSettings.teamMode로 전달
- RoomSettingsData JSON에도 teamMode 포함
- 캐릭터 선택 이후 인게임에서도 GameStartSettingsHolder.Current로 접근
- 개인전에서는 isTeamMode가 false이므로 teamMode 값은 사용하지 않음
- 팀전별 게임 규칙 구현은 인게임 담당 작업 범위

```csharp
using YutArena.Common;

GameStartSettings settings = GameStartSettingsHolder.Current;
if (settings != null && settings.isTeamMode)
{
    switch (settings.teamMode)
    {
        case TeamMode.TeamMode1:
            // 팀전 모드 1 규칙
            break;
        case TeamMode.TeamMode2:
            // 팀전 모드 2 규칙
            break;
    }
}
```
