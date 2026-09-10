using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using YutArena.Common;
using YutArena.Managers;
using YutArena.Managers.GameProgress;

namespace YutArena.UI
{
    // ===================================================================
    // 윷 결과 표시 UI (표시 전용)
    //
    // [변경] 예전에는 결과를 클릭 가능한 버튼으로 생성했지만, 기획 의도에 맞춰
    // "아직 사용하지 않은 윷 결과를 보여주기만 하는 칸"으로 바꿨다.
    // 실제 이동 선택(말 클릭 → 목적지 타일 클릭)은 MoveDestinationSelector 가 담당한다.
    //
    // 사용법:
    // 1. 패널 오브젝트에 이 스크립트를 붙이고 Inspector 에서 turnManager 연결
    // 2. 라벨(TextMeshProUGUI)은 없으면 Awake 에서 자동 생성됨 (프리팹 불필요)
    // ===================================================================
    public class YutResultSelectorUI : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private TestTurnManager turnManager;

        [Header("표시")]
        [Tooltip("비우면 Awake 에서 자식으로 자동 생성합니다.")]
        [SerializeField] private TextMeshProUGUI resultLabel;
        [SerializeField] private string emptyText = "";

        private void Awake()
        {
            EnsureLabel();
            SetText(string.Empty);
        }

        private void OnEnable()
        {
            EnsureLabel();
            SetText(string.Empty);

            if (turnManager == null)
            {
                Debug.LogError("YutResultSelectorUI: turnManager가 연결 안 됨");
                return;
            }

            turnManager.OnPendingResultsChanged += RefreshDisplay;
            turnManager.OnTurnStarted += HandleTurnStarted;
        }

        private void OnDisable()
        {
            if (turnManager == null) return;
            turnManager.OnPendingResultsChanged -= RefreshDisplay;
            turnManager.OnTurnStarted -= HandleTurnStarted;
        }

        // 턴이 시작되면 pendingResults 는 비워지지만 이벤트가 오지 않으므로 여기서 라벨을 비운다.
        private void HandleTurnStarted(PlayerSlot _) => SetText(string.Empty);

        // pendingResults 가 바뀔 때마다(던짐/소비) 아직 안 쓴 결과 목록을 갱신 표시.
        private void RefreshDisplay(List<YutThrowData> pendingResults)
        {
            if (pendingResults == null || pendingResults.Count == 0)
            {
                SetText(string.Empty);
                return;
            }

            var builder = new StringBuilder();
            for (int i = 0; i < pendingResults.Count; i++)
            {
                if (i > 0) builder.Append("   ·   ");
                builder.Append(GetDisplayName(pendingResults[i].result));
            }
            SetText(builder.ToString());
        }

        private void SetText(string value)
        {
            if (resultLabel != null)
                resultLabel.text = string.IsNullOrEmpty(value) ? emptyText : value;
        }

        private void EnsureLabel()
        {
            if (resultLabel != null) return;

            resultLabel = GetComponentInChildren<TextMeshProUGUI>(true);
            if (resultLabel != null) return;

            var labelObject = new GameObject("ResultLabel", typeof(RectTransform));
            labelObject.transform.SetParent(transform, false);
            resultLabel = labelObject.AddComponent<TextMeshProUGUI>();
            resultLabel.alignment = TextAlignmentOptions.Center;
            resultLabel.enableAutoSizing = true;
            resultLabel.fontSizeMin = 18f;
            resultLabel.fontSizeMax = 48f;
            resultLabel.color = Color.white;
            resultLabel.raycastTarget = false;

            var rt = resultLabel.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private string GetDisplayName(YutResult result)
        {
            switch (result)
            {
                case YutResult.Do: return "Do";
                case YutResult.Gae: return "Gae";
                case YutResult.Geol: return "Geol";
                case YutResult.Yut: return "Yut";
                case YutResult.Mo: return "Mo";
                case YutResult.BackDo: return "BackDo";
                default: return result.ToString();
            }
        }
    }
}
