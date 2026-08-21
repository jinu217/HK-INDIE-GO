using System.Collections;
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
        //   1단계: EnterCharacterSelect() - 화면만 캐릭터 선택으로 바꿈, 게임은 아직 시작 안 함
        //   2단계: StartGame() - 캐릭터 선택 다 끝난 뒤에 호출되어야 진짜 게임(첫 턴)이 시작됨
        // ===================================================================
        public void EnterCharacterSelect()
        {
            SetPhase(GamePhase.CharacterSelect);
            // 여기서 함수가 끝남. 캐릭터 선택 화면(다른 담당자가 만들 UI)이 떠 있는 동안
            // 플레이어들이 캐릭터를 고르고, 다 고르면 아래 NotifyAllPlayersSelectedCharacter()를 호출해줘야 함
        }
        // ===================================================================
        // 챔피언 픽 씬에서, 플레이어들이 고른 캐릭터 ID 배열을 저장해둠
        // (int[] 0번 인덱스부터 순서대로 P1~P4). ProbabilityTableProvider나 스킬 훅에서 "이 플레이어가
        // 무슨 캐릭터인지" 알아야 할 때 여기서 조회해서 쓸 수 있게 public으로 열어둠
        // ===================================================================
        public int[] SelectedCharacterIds { get; private set; }

        // playerId(1~) 넣으면 그 사람이 고른 캐릭터 ID를 바로 찾아주는 도우미 함수
        // (배열은 0번 인덱스부터 P1이라서, playerId - 1로 접근함)
        public int GetSelectedCharacterId(int playerId)
        {
            if (SelectedCharacterIds == null) return -1; // 아직 선택 정보 안 들어온 상태
            int index = playerId - 1;
            if (index < 0 || index >= SelectedCharacterIds.Length) return -1;
            return SelectedCharacterIds[index];
        }

        // ===================================================================
        // [캐릭터 선택 카운트다운
        // "전원 캐릭터 선택시 자동으로 게임 시작 (카운트다운 5초에서 10초 후 시작)"
        // 정확히 몇 초인지는 아직 미확정이라, 일단 기본값(characterSelectCountdownSeconds)으로 구현해두고 나중에 값만 바꾸면 되게 함
        // ===================================================================
        [Header("Character Select")]
        [SerializeField] private float characterSelectCountdownSeconds = 8f; // 5~10초 사이 임시 기본값
        private Coroutine characterSelectCountdownCoroutine;
        // 캐릭터 선택 UI가 "전원 다 골랐다"고 알려줄 때 호출하는 함수. 카운트다운 시작 후 자동으로 게임 시작됨
        public void NotifyAllPlayersSelectedCharacter()
        {
            if (Session.phase != GamePhase.CharacterSelect)
            {
                Debug.LogWarning("TestGameManager: 캐릭터 선택 단계가 아닌데 전원 선택 완료 알림이 옴");
                return;
            }
            // "누가 어떤 캐릭터를 골랐는지" 배열로 받아와서 저장해둠
            // (0번 인덱스부터 P1~P4 순서)
            SelectedCharacterIds = CharacterSelectionResult.GetSelectedCharacterIds();
            Debug.Log("[캐릭터선택] 받아온 캐릭터 ID: " + string.Join(", ", SelectedCharacterIds));

            if (characterSelectCountdownCoroutine != null) return; // 이미 카운트다운 중이면 중복 시작 방지
            characterSelectCountdownCoroutine = StartCoroutine(CharacterSelectCountdownRoutine());
        }
        // 카운트다운 도중에 취소해야 하면(예: 누가 캐릭터를 다시 바꿈) UI가 호출
        public void CancelCharacterSelectCountdown()
        {
            if (characterSelectCountdownCoroutine != null)
            {
                StopCoroutine(characterSelectCountdownCoroutine);
                characterSelectCountdownCoroutine = null;
            }
        }
        private IEnumerator CharacterSelectCountdownRoutine()
        {
            Debug.Log("[캐릭터선택] " + characterSelectCountdownSeconds + "초 후 자동으로 게임 시작");
            yield return new WaitForSeconds(characterSelectCountdownSeconds);
            characterSelectCountdownCoroutine = null;
            StartGame(); // 카운트다운 끝나면 자동으로 진짜 게임 시작
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
            // ===================================================================
            // Escape 승리조건 2번용 실시간 제한시간 타이머
            // ===================================================================
            if (settings.timeLimitMinutes != GameRuleDefine.UnlimitedTimeMinutes)
            {
                if (timeLimitCoroutine != null) StopCoroutine(timeLimitCoroutine); // 이전 판 타이머 남아있으면 정리
                timeLimitCoroutine = StartCoroutine(TimeLimitRoutine(settings.timeLimitMinutes * 60f));
            }
        }
        //  위 타이머용 필드+코루틴
        private Coroutine timeLimitCoroutine;
        private IEnumerator TimeLimitRoutine(float totalSeconds)
        {
            Debug.Log("[제한시간] " + (totalSeconds / 60f) + "분 타이머 시작");
            yield return new WaitForSeconds(totalSeconds);
            timeLimitCoroutine = null;
            winConditionManager.HandleTimeLimitReached(); // 시간 다 됐으니 지금까지 점수로 승부 결정
        }
        // WinConditionManager가 승패를 확정지었을 때호출됨
        public void EndGame(GameResultData result)
        {
            if (timeLimitCoroutine != null) //  게임이 다른 이유로 먼저 끝났으면 남은 제한시간 타이머 정리
            {
                StopCoroutine(timeLimitCoroutine);
                timeLimitCoroutine = null;
            }
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