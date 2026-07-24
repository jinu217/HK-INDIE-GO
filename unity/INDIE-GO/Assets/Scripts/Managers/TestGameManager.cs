using UnityEngine;
using YutArena.Common;

namespace YutArena.Managers
{
    // 게임 전체 흐름(메인화면 -> 대기실 -> 인게임 -> 결과 -> 대기실)을 총괄하는 매니저

    public class TestGameManager : MonoBehaviour
    {
        // 싱글턴: 씬 어디서든 TestGameManager.Instance로 이 객체 하나에 바로 접근 가능
        public static TestGameManager Instance { get; private set; }

        [Header("Managers")]
        [SerializeField] private TestTurnManager turnManager;
        [SerializeField] private TestYutRuleManager yutRuleManager;
        [SerializeField] private TestWinConditionManager winConditionManager;

        // 지금 게임 세션(단계 + 설정값) 정보, 처음엔 게임 시작 전이니 MainMenu로 초기화
        public GameSessionData Session { get; private set; } = new GameSessionData { phase = GamePhase.MainMenu };

        public System.Action<GamePhase> OnGamePhaseChanged;  // 게임 단계가 바뀔 때마다 방송
        public System.Action<GameResultData> OnGameEnded;    // 게임이 끝났을 때 방송 (승자 정보 포함)

        // 싱글턴: 씬에 GameManager가 중복 생성돼도 하나만 유지
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject); // 이미 인스턴스가 존재하면 자신을 파괴
                return;
            }
            Instance = this;
        }

        // 대기실에서 [게임 시작] 버튼을 눌렀을 때 UI가 호출하는 함수
        public void StartGame(GameStartSettings settings)
        {
            // 새 세션 데이터를 만들어서 교체 (이전 판 정보가 남지 않도록)
            Session = new GameSessionData
            {
                sessionId = System.Guid.NewGuid().ToString(), // 이 게임 판을 구분하는 고유 id(중복 안됨)
                settings = settings,
                elapsedSeconds = 0f
            };
            SetPhase(GamePhase.InGame);

            // 다른 매니저들에게 "게임 시작"을 알리면서 설정값을 넘겨줌
            winConditionManager.Initialize(settings);
            turnManager.Initialize(settings);
            turnManager.StartFirstTurn(); // 턴 시작
        }

        // WinConditionManager가 승패를 확정지었을 때(Declare/DeclareSurrender에서) 호출됨
        public void EndGame(GameResultData result)
        {
            SetPhase(GamePhase.Result); // 결과화면으로 전환
            OnGameEnded?.Invoke(result); // 결과화면 UI에 승자 정보 전달
        }

        // 결과 화면에서 대기실로 돌아갈 때 UI가 호출
        public void ReturnToLobby()
        {
            SetPhase(GamePhase.Lobby);
        }

        // 게임 단계 변경을 여기서만 처리. 값 바꾸기 + 방송하기가 항상 같이 일어나게 함
        private void SetPhase(GamePhase phase)
        {
            Session.phase = phase;
            OnGamePhaseChanged?.Invoke(phase);
        }
    }
}