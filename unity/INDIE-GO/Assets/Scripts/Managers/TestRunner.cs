using UnityEngine;
using YutArena.Common;
using YutArena.Managers;

// 실제로는 대기실/캐릭터선택 UI가 이 흐름을 처리하는데, 지금은 UI가 없으니
// 흉내내서 테스트하는 임시 스크립트 (UI 완성되면 삭제)
public class TestRunner : MonoBehaviour
{
    public TestGameManager gameManager;

    void Start()
    {
        // 대기실이 하는 일: 설정값을 GameStartSettingsHolder.Current에 저장
        GameStartSettingsHolder.Current = new GameStartSettings
        {
            gameMode = GameMode.Escape,
            mapType = MapType.Basic,
            matchComposition = MatchComposition.TwoVsTwo,
            playerCount = 4,
            pieceCountPerPlayer = 4,
            targetEscapeCount = 4,
            timeLimitMinutes = 0,
            maxTurnCount = GameRuleDefine.DefaultMaxTurnCount,
            turnTimeMode = TurnTimeMode.Unlimited,
            throwTimeSeconds = GameRuleDefine.DefaultThrowTimeSeconds,
            actionTimeSeconds = GameRuleDefine.DefaultActionTimeSeconds,
            useSkill = true,
            useItem = true,
            useSpecialTile = true
        };

        // 1단계: 캐릭터 선택 화면으로 전환 (진짜 게임은 아직 시작 안 됨)
        gameManager.EnterCharacterSelect();

        // 실제로는 여기서 플레이어들이 캐릭터를 고를 때까지 기다려야 하는데,
        // 지금은 UI가 없으니 테스트용으로 "선택 끝났다"고 바로 이어서 처리함
        // (진짜 흐름이었다면 캐릭터선택 UI가 StartGame()을 나중에 따로 호출해야 함)
        gameManager.StartGame();
    }
}