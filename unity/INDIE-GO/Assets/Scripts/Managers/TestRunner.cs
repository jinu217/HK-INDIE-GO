using UnityEngine;
using YutArena.Common;
using YutArena.Managers;

// 실제로는 대기실/캐릭터선택 UI가 이 흐름을 처리하는데, 지금은 UI가 없으니
// 흉내내서 테스트하는 임시 스크립트 (UI 완성되면 삭제)
public class TestRunner : MonoBehaviour
{
    public TestGameManager gameManager;

    [Tooltip("Only creates hard-coded settings when the scene is opened directly without a lobby.")]
    [SerializeField] private bool createFallbackSettingsWhenMissing = true;

    void Start()
    {
        // A lobby-provided setting always wins. This fallback is only for opening InGameScene directly.
        if (GameStartSettingsHolder.Current == null && createFallbackSettingsWhenMissing)
        {
            GameStartSettingsHolder.Current = new GameStartSettings
            {
                gameMode = GameMode.Escape,
                mapType = MapType.Basic,
                matchComposition = MatchComposition.TwoVsTwo,
                isTeamMode = true,
                playerTeams = new[] { 1, 1, 2, 2 },
                playerCount = 4,
                pieceCountPerPlayer = 4,
                targetEscapeCount = 4,
                timeLimitMinutes = 0,
                maxTurnCount = GameRuleDefine.DefaultMaxTurnCount,
                turnTimeMode = TurnTimeMode.Limited,
                throwTimeSeconds = GameRuleDefine.DefaultThrowTimeSeconds,
                actionTimeSeconds = GameRuleDefine.DefaultActionTimeSeconds,
                useSkill = true,
                useItem = true,
                useSpecialTile = true
            };
        }

        if (GameStartSettingsHolder.Current == null)
        {
            Debug.LogError("TestRunner: No lobby settings were provided.");
            return;
        }

        // 1단계: 캐릭터 선택 화면으로 전환 (진짜 게임은 아직 시작 안 됨)
        gameManager.EnterCharacterSelect();

        // 실제로는 여기서 플레이어들이 캐릭터를 고를 때까지 기다려야 하는데,
        // 지금은 UI가 없으니 테스트용으로 "선택 끝났다"고 바로 이어서 처리함
        // (진짜 흐름이었다면 캐릭터선택 UI가 StartGame()을 나중에 따로 호출해야 함)
        gameManager.StartGame();
    }
}
