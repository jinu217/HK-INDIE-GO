using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YutArena.Common;
using YutArena.Managers;
using YutArena.Managers.GameProgress;

namespace YutArena.UI
{
    public class YutResultSelectorUI : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private TestTurnManager turnManager;
        [SerializeField] private Button buttonPrefab;

        [Header("현재 선택 상태")]
        [SerializeField] private int selectedPieceId = -1;
        private YutResult? selectedResult = null; // 선택된 윷 결과 (null: 미선택)

        private readonly List<Button> spawnedButtons = new List<Button>();
        private RectTransform panelRect;

        private void Awake()
        {
            panelRect = GetComponent<RectTransform>();
            ResetSelection();
        }

        private void OnEnable()
        {
            ResetSelection();
            if (turnManager == null)
            {
                Debug.LogError("YutResultSelectorUI: turnManager가 연결되지 않았습니다.");
                return;
            }
            turnManager.OnPendingResultsChanged += RefreshButtons;
        }

        private void OnDisable()
        {
            if (turnManager != null)
                turnManager.OnPendingResultsChanged -= RefreshButtons;
        }

        private void RefreshButtons(List<YutThrowData> pendingResults)
        {
            ClearButtons();
            ResetSelection();

            if (pendingResults == null || pendingResults.Count == 0) return;

            foreach (var throwData in pendingResults)
            {
                Button newButton = Instantiate(buttonPrefab, transform);
                newButton.gameObject.SetActive(true);

                RectTransform rt = newButton.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.localScale = Vector3.one;
                    rt.localPosition = Vector3.zero;
                }

                // 텍스트 설정
                var tmpText = newButton.GetComponentInChildren<TextMeshProUGUI>();
                if (tmpText != null) tmpText.text = GetDisplayName(throwData.result);
                else
                {
                    var legacyText = newButton.GetComponentInChildren<Text>();
                    if (legacyText != null) legacyText.text = GetDisplayName(throwData.result);
                }

                // 버튼 클릭 시 윷 결과 선택 함수 호출
                YutResult capturedResult = throwData.result;
                newButton.onClick.AddListener(() => OnResultButtonClicked(capturedResult));

                spawnedButtons.Add(newButton);
            }

            if (panelRect != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(panelRect);
            }
        }

        // [윷 결과 버튼 클릭 시]
        private void OnResultButtonClicked(YutResult chosenResult)
        {
            selectedResult = chosenResult;
            Debug.Log($"[윷 결과 선택됨] {chosenResult}");

            // 만약 이미 말이 선택되어 있었다면 즉시 이동 실행
            TryExecuteMove();
        }

        // [보드에서 말 클릭 시 호출되는 함수]
        public void SetSelectedPieceId(int pieceId)
        {
            selectedPieceId = pieceId;
            Debug.Log($"[말 선택됨] Piece ID: {selectedPieceId}");

            // 만약 이미 윷 결과가 선택되어 있었다면 즉시 이동 실행
            TryExecuteMove();
        }

        // 말과 윷 결과가 둘 다 선택되었는지 확인하고 이동 실행
        private void TryExecuteMove()
        {
            if (selectedPieceId >= 0 && selectedResult.HasValue)
            {
                Debug.Log($"[이동 실행] 말: {selectedPieceId}, 윷결과: {selectedResult.Value}");
                turnManager.RequestMovePiece(selectedPieceId, selectedResult.Value);

                // 이동 실행 후 선택 상태 초기화
                ResetSelection();
            }
            else if (!selectedResult.HasValue)
            {
                Debug.Log("이동에 사용할 [윷 결과 버튼]을 선택해주세요.");
            }
            else if (selectedPieceId < 0)
            {
                Debug.Log("이동시킬 [말]을 보드에서 클릭해주세요.");
            }
        }

        private void ResetSelection()
        {
            selectedPieceId = -1;
            selectedResult = null;
        }

        private void ClearButtons()
        {
            foreach (var button in spawnedButtons)
            {
                if (button != null)
                {
                    button.gameObject.SetActive(false);
                    button.transform.SetParent(null);
                    Destroy(button.gameObject);
                }
            }
            spawnedButtons.Clear();
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