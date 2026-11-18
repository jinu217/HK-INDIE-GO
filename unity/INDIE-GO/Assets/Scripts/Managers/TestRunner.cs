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
    [Tooltip("로비 없이 InGameScene을 바로 열 때 쓸 모드. 로비에서 킬 더 킹 시작이 막혀 있어서 테스트용으로 여기서 고름.")]
    [SerializeField] private GameMode fallbackGameMode = GameMode.Escape;

    // 버그 수정: 프로젝트가 플레이 시작 때 값을 초기화하지 않는 설정이라, 지난 플레이에서 만든 설정(예: Escape)이
    // 메모리에 남아 Fallback Game Mode를 바꿔도 그 모드로 시작됐음. 플레이를 누를 때마다 남은 설정을 지움.
    // (로비 -> 인게임 흐름은 플레이 시작 뒤에 로비가 설정을 새로 넣으므로 영향 없음)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ClearSettingsFromLastPlay() => GameStartSettingsHolder.Current = null;

    void Start()
    {
        // A lobby-provided setting always wins. This fallback is only for opening InGameScene directly.
        if (GameStartSettingsHolder.Current == null && createFallbackSettingsWhenMissing)
        {
            GameStartSettingsHolder.Current = new GameStartSettings
            {
                gameMode = fallbackGameMode,
                mapType = MapType.Basic,
                matchComposition = MatchComposition.OneVsOneVsOneVsOne,
                isTeamMode = false,
                playerCount = 4,
                pieceCountPerPlayer = GameRuleDefine.DefaultPieceCountPerPlayer,
                targetEscapeCount = GameRuleDefine.DefaultTargetEscapeCount, // 필요 점수 (기본 5점)
                timeLimitMinutes = GameRuleDefine.UnlimitedTimeMinutes,
                maxTurnCount = fallbackGameMode == GameMode.KillTheKing
                    ? KillTheKingManager.TempMaxRound      // 킬 더 킹 제한 턴은 밸런스 전 임시값
                    : GameRuleDefine.DefaultMaxTurnCount,
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
        QaDebugLog.Event("[TestRunner] 시작 모드=" + GameStartSettingsHolder.Current.gameMode +
            ", 인원=" + GameStartSettingsHolder.Current.playerCount + ", 최대 라운드=" + GameStartSettingsHolder.Current.maxTurnCount);

        // 1단계: 캐릭터 선택 화면으로 전환 (진짜 게임은 아직 시작 안 됨)
        gameManager.EnterCharacterSelect();

        // 실제로는 여기서 플레이어들이 캐릭터를 고를 때까지 기다려야 하는데,
        // 지금은 UI가 없으니 테스트용으로 "선택 끝났다"고 바로 이어서 처리함
        // (진짜 흐름이었다면 캐릭터선택 UI가 StartGame()을 나중에 따로 호출해야 함)
        gameManager.StartGame();
    }
}
