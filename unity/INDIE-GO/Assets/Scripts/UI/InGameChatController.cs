using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using YutArena.Common;
using YutArena.Managers;
using YutArena.Managers.GameProgress;

namespace YutArena.UI
{
    // 채팅 종류. 일반·파티·귓속말은 아직 입력/전송이 없고, 시스템만 게임 이벤트로 기록된다.
    public enum ChatChannel
    {
        General,
        Party,
        Whisper,
        System
    }

    /// <summary>
    /// 인게임 채팅 로그. 지금은 입력 없이 시스템 메시지(윷 결과, 한 번 더 던지기)만 표시한다.
    /// - 윷 결과: 윷 결과 연출(가운데 토큰)이 시작될 때 표시
    /// - 한 번 더 던지기: 그 결과 연출이 끝난 뒤 표시
    /// 메시지가 일정 시간 없으면 채팅창 전체가 서서히 투명해지고, 새 메시지가 오면 다시 보인다.
    /// </summary>
    public sealed class InGameChatController : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("채팅창 전체 투명도를 조절할 CanvasGroup")]
        [SerializeField] private CanvasGroup canvasGroup;
        [Tooltip("채팅 로그를 표시할 텍스트")]
        [SerializeField] private TMP_Text logText;
        [SerializeField] private TestTurnManager turnManager;
        [SerializeField] private YutResultSelectorUI resultPresenter;

        [Header("Fade")]
        [Tooltip("마지막 메시지 후 투명해지기 시작할 때까지의 시간(초)")]
        [SerializeField, Min(0f)] private float idleSeconds = 5f;
        [Tooltip("완전히 투명해지는 데 걸리는 시간(초)")]
        [SerializeField, Min(0.01f)] private float fadeSeconds = 0.5f;

        [Header("Log")]
        [SerializeField, Min(1)] private int maxMessages = 50;

        private const string SystemPrefix = "[시스템] ";
        private const string ExtraThrowMessage = "윷을 한번 더 던져주세요.";

        private readonly List<string> lines = new List<string>();
        private readonly StringBuilder builder = new StringBuilder();
        private float idleTimer;

        // 던진 결과마다 누가 던졌는지 (결과 연출은 던진 직후가 아니라 나중에 시작되므로 기억해 둠)
        private readonly Dictionary<YutThrowData, PlayerSlot> personByThrow = new Dictionary<YutThrowData, PlayerSlot>();
        private readonly Queue<PlayerSlot> nakPersons = new Queue<PlayerSlot>();
        // 윷·모로 한 번 더 던지기가 생긴 결과 → 그 결과 연출이 끝나면 안내
        private readonly HashSet<YutThrowData> extraThrowAfter = new HashSet<YutThrowData>();
        private YutThrowData lastThrow;

        private void Awake()
        {
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            if (turnManager == null) turnManager = FindFirstObjectByType<TestTurnManager>();
            if (resultPresenter == null) resultPresenter = FindFirstObjectByType<YutResultSelectorUI>();
            RefreshLog();
            SetAlpha(1f);
        }

        private void OnEnable()
        {
            if (turnManager != null)
            {
                turnManager.OnTurnPhaseChanged += HandleTurnPhaseChanged;
                turnManager.OnPendingResultsChanged += HandlePendingResultsChanged;
                turnManager.OnTurnNotice += HandleTurnNotice;
            }
            if (resultPresenter != null)
            {
                resultPresenter.OnResultPresentationStarted += HandlePresentationStarted;
                resultPresenter.OnResultPresentationEnded += HandlePresentationEnded;
            }
        }

        private void OnDisable()
        {
            if (turnManager != null)
            {
                turnManager.OnTurnPhaseChanged -= HandleTurnPhaseChanged;
                turnManager.OnPendingResultsChanged -= HandlePendingResultsChanged;
                turnManager.OnTurnNotice -= HandleTurnNotice;
            }
            if (resultPresenter != null)
            {
                resultPresenter.OnResultPresentationStarted -= HandlePresentationStarted;
                resultPresenter.OnResultPresentationEnded -= HandlePresentationEnded;
            }
        }

        private void Update()
        {
            idleTimer += Time.unscaledDeltaTime;
            float t = (idleTimer - idleSeconds) / fadeSeconds;
            SetAlpha(1f - Mathf.Clamp01(t));
        }

        public void AddMessage(ChatChannel channel, string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            lines.Add(channel == ChatChannel.System ? SystemPrefix + message : message);
            while (lines.Count > maxMessages) lines.RemoveAt(0);
            RefreshLog();
            idleTimer = 0f;
            SetAlpha(1f);
        }

        public void AddSystemMessage(string message) => AddMessage(ChatChannel.System, message);

        // ---- 게임 이벤트 ----

        private PlayerSlot CurrentPerson =>
            turnManager.CurrentPerson != PlayerSlot.None ? turnManager.CurrentPerson : turnManager.CurrentTurn.currentPlayer;

        // 낙은 결과 묶음에 안 들어가므로 던진 순간 누가 던졌는지 기억
        private void HandleTurnPhaseChanged(TurnContext turn)
        {
            if (turn != null && turn.currentPhase == TurnPhase.SaveThrowResult && YutResultRule.IsTurnCancelResult(turn.lastYutResult))
                nakPersons.Enqueue(CurrentPerson);
        }

        private void HandlePendingResultsChanged(List<YutThrowData> results)
        {
            if (results == null) return;
            foreach (var data in results)
            {
                if (personByThrow.ContainsKey(data)) continue;
                personByThrow[data] = CurrentPerson;
                lastThrow = data;
            }
        }

        private void HandleTurnNotice(TurnNotice notice)
        {
            if (notice.type != TurnNoticeType.ExtraThrow) return;
            // 윷·모로 생긴 추가 던지기는 방금 던진 결과의 연출이 끝난 뒤 표시. 잡기·스킬 보너스는 바로 표시
            if (notice.reason == ExtraThrowReason.YutMo && lastThrow != null && resultPresenter != null && resultPresenter.isActiveAndEnabled)
                extraThrowAfter.Add(lastThrow);
            else
                AddSystemMessage(ExtraThrowMessage);
        }

        private void HandlePresentationStarted(YutThrowData data, YutResult result)
        {
            PlayerSlot person;
            if (data != null) personByThrow.TryGetValue(data, out person);
            else person = nakPersons.Count > 0 ? nakPersons.Dequeue() : CurrentPerson;
            AddSystemMessage($"{(int)person}Player님의 윷 결과는 {GetResultName(result)}입니다.");
        }

        private void HandlePresentationEnded(YutThrowData data, YutResult result)
        {
            if (data == null) return;
            personByThrow.Remove(data);
            if (extraThrowAfter.Remove(data)) AddSystemMessage(ExtraThrowMessage);
        }

        private static string GetResultName(YutResult result)
        {
            return result switch
            {
                YutResult.Nak => "낙",
                YutResult.BackDo => "백도",
                YutResult.Do => "도",
                YutResult.Gae => "개",
                YutResult.Geol => "걸",
                YutResult.Yut => "윷",
                YutResult.Mo => "모",
                _ => result.ToString()
            };
        }

        // ---- 표시 ----

        private void RefreshLog()
        {
            if (logText == null) return;
            builder.Clear();
            for (int i = 0; i < lines.Count; i++)
            {
                if (i > 0) builder.Append('\n');
                builder.Append(lines[i]);
            }
            logText.text = builder.ToString();
        }

        private void SetAlpha(float alpha)
        {
            if (canvasGroup != null) canvasGroup.alpha = alpha;
        }
    }
}
