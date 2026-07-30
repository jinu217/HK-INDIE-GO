using UnityEngine;
using YutArena.Common;
using YutArena.Managers.GameProgress;
using YutArena.Managers;

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

        // ===================================================================
        // 새로 추가한 부분 (7/3 회의록 확정: "대기실 설정 후 인게임 들어가서 캐릭터 선택")
        // 대기실에서 [게임 시작]을 누르면 원래는 바로 게임(첫 턴)이 시작됐는데,
        // 이제는 그 사이에 "캐릭터 선택" 단계가 하나 더 끼어들어야 해서 함수를 2단계로 나눔
        //   1단계: EnterCharacterSelect() - 화면만 캐릭터 선택으로 바꿈, 게임은 아직 시작 안 함
        //   2단계: StartGame() - 캐릭터 선택 다 끝난 뒤에 호출되어야 진짜 게임(첫 턴)이 시작됨
        // ===================================================================
        public void EnterCharacterSelect()
        {
            SetPhase(GamePhase.CharacterSelect);
            // 여기서 함수가 끝남. 캐릭터 선택 화면(다른 담당자가 만들 UI)이 떠 있는 동안
            // 플레이어들이 캐릭터를 고르고, 다 고르면(또는 제한시간 끝나면) 그 UI가 아래 StartGame()을 호출해줘야 함
        }

        // 캐릭터 선택이 끝났을 때 UI가 호출하는 함수 - 여기서부터 진짜 게임이 시작됨
        public void StartGame()
        {
            var settings = GameStartSettingsHolder.Current;
            if (settings == null)
            {
                Debug.LogError("TestGameManager: GameStartSettingsHolder.Current가 비어있음");
                return;
            }
            StartGameWithSettings(settings);
        }

        private void StartGameWithSettings(GameStartSettings settings)
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