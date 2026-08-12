using UnityEngine;
using YutArena.Common;
using YutArena.Managers;

/// <summary>
/// InGameScene bootstrap. The class name is retained because the scene already
/// serializes it, but it no longer overwrites lobby/character-selection data.
/// </summary>
public sealed class TestRunner : MonoBehaviour
{
    public TestGameManager gameManager;

    private void Start()
    {
        if (gameManager == null)
        {
            Debug.LogError("InGame bootstrap requires TestGameManager.", this);
            return;
        }

        if (GameStartSettingsHolder.Current == null)
            GameStartSettingsHolder.Current = CreateOfflineFallbackSettings();

        gameManager.StartGame();
    }

    private static GameStartSettings CreateOfflineFallbackSettings()
    {
        return new GameStartSettings
        {
            gameMode = GameMode.Classic,
            mapType = MapType.Basic,
            matchComposition = MatchComposition.OneVsOne,
            playerCount = 2,
            pieceCountPerPlayer = 4,
            targetEscapeCount = 4,
            timeLimitMinutes = GameRuleDefine.UnlimitedTimeMinutes,
            maxTurnCount = GameRuleDefine.DefaultMaxTurnCount,
            turnTimeMode = TurnTimeMode.Unlimited,
            throwTimeSeconds = GameRuleDefine.DefaultThrowTimeSeconds,
            actionTimeSeconds = GameRuleDefine.DefaultActionTimeSeconds,
            useSkill = true,
            useItem = false,
            useSpecialTile = false
        };
    }
}
