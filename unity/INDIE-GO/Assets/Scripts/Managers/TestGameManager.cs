using UnityEngine;
using YutArena.Common;

namespace YutArena.Managers
{
    public class TestGameManager : MonoBehaviour
    {
        public static TestGameManager Instance { get; private set; }

        [Header("Managers")]
        [SerializeField] private TestTurnManager turnManager;
        [SerializeField] private TestYutRuleManager yutRuleManager;
        [SerializeField] private TestWinConditionManager winConditionManager;

        public GameSessionData Session { get; private set; } = new GameSessionData { phase = GamePhase.MainMenu };

        public System.Action<GamePhase> OnGamePhaseChanged;
        public System.Action<GameResultData> OnGameEnded;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public void StartGame(GameStartSettings settings)
        {
            Session = new GameSessionData
            {
                sessionId = System.Guid.NewGuid().ToString(),
                settings = settings,
                elapsedSeconds = 0f
            };
            SetPhase(GamePhase.InGame);

            winConditionManager.Initialize(settings);
            turnManager.Initialize(settings);
            turnManager.StartFirstTurn();
        }

        public void EndGame(GameResultData result)
        {
            SetPhase(GamePhase.Result);
            OnGameEnded?.Invoke(result);
        }

        public void ReturnToLobby()
        {
            SetPhase(GamePhase.Lobby);
        }

        private void SetPhase(GamePhase phase)
        {
            Session.phase = phase;
            OnGamePhaseChanged?.Invoke(phase);
        }
    }
}