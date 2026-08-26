using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YutArena.Common;
using YutArena.Managers;
using YutArena.Managers.GameProgress;

namespace YutArena.UI
{
    // ===================================================================
    // 윷 결과 선택 UI (동적 생성 방식)
    //
    // 던진 결과 개수만큼 버튼을 그때그때 새로 만들었다가, 결과가 바뀌면 전부 지우고
    // 다시 만드는 방식. 말/윷 결과 중 어느 것을 먼저 선택해도, 둘 다 선택되면 자동으로 이동됨.
    //
    // 사용법:
    // 1. 빈 오브젝트(예: YutResultPanel)에 이 스크립트를 붙임
    // 2. Horizontal Layout Group + Content Size Fitter는 되도록 쓰지 말고,
    //    Panel 크기를 고정값으로 직접 지정해서 사용할 것 (레이아웃 자동계산이 계속 꼬였던 이력 있음)
    // 3. Inspector에서:
    //    - turnManager: 씬의 TestTurnManager 연결
    //    - buttonPrefab: 결과 하나당 버튼 하나, 이 프리팹을 복제해서 씀
    // ===================================================================
    public class YutResultSelectorUI : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private TestTurnManager turnManager;
        [SerializeField] private Button buttonPrefab; // 결과 하나당 버튼 하나, 이 프리팹을 복제해서 씀

        [Header("현재 선택 상태")]
        [SerializeField] private int selectedPieceId = -1; // -1: 아직 말을 선택 안 한 상태
        private YutResult? selectedResult = null; // 아직 윷 결과를 선택 안 하면 null

        // 지금 화면에 떠있는 버튼들을 기억해뒀다가, 다음에 결과가 바뀌면 싹 지우고 새로 만들기 위함
        private readonly List<Button> spawnedButtons = new List<Button>();

        private void Awake()
        {
            ResetSelection(); // 시작할 때 선택 상태 확실히 초기화 (인스펙터에 저장된 이전 값 무시)
        }

        private void OnEnable()
        {
            ResetSelection();
            if (turnManager == null)
            {
                Debug.LogError("YutResultSelectorUI: turnManager가 연결 안 됨");
                return;
            }
            // 결과 묶음이 바뀔 때마다(던졌을 때, 이동해서 하나 소비했을 때 등) 버튼 다시 그리기
            turnManager.OnPendingResultsChanged += RefreshButtons;
        }

        private void OnDisable()
        {
            if (turnManager != null)
                turnManager.OnPendingResultsChanged -= RefreshButtons;
        }

        // pendingResults가 바뀔 때마다 호출됨: 기존 버튼 다 지우고, 지금 결과 개수만큼 새로 만듦
        private void RefreshButtons(List<YutThrowData> pendingResults)
        {
            ClearButtons();

            foreach (var throwData in pendingResults)
            {
                Button newButton = Instantiate(buttonPrefab, transform);
                newButton.gameObject.SetActive(true);

                // 복제되면서 혹시 이상한 값이 섞여 들어오지 않게 위치/크기 초기화
                RectTransform rt = newButton.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.localScale = Vector3.one;
                    rt.localPosition = Vector3.zero;
                }

                // 버튼에 결과 이름 표시 (Text 또는 TextMeshProUGUI 둘 다 지원)
                var tmpText = newButton.GetComponentInChildren<TextMeshProUGUI>();
                if (tmpText != null) tmpText.text = GetDisplayName(throwData.result);

                var legacyText = newButton.GetComponentInChildren<Text>();
                if (legacyText != null) legacyText.text = GetDisplayName(throwData.result);

                // 클로저 문제 방지: foreach 변수를 지역변수에 복사해서 캡처
                YutResult capturedResult = throwData.result;
                newButton.onClick.RemoveAllListeners();
                newButton.onClick.AddListener(() => OnResultButtonClicked(capturedResult));

                spawnedButtons.Add(newButton);
            }
        }

        // 윷 결과 버튼 클릭 시
        private void OnResultButtonClicked(YutResult chosenResult)
        {
            selectedResult = chosenResult;
            Debug.Log("[윷 결과 선택됨] " + chosenResult);
            TryExecuteMove();
        }

        // 보드에서 말 클릭 시 호출되는 함수 (InGamePieceDebugController에서 호출)
        public void SetSelectedPieceId(int pieceId)
        {
            selectedPieceId = pieceId;
            Debug.Log("[말 선택됨] Piece ID: " + selectedPieceId);
            TryExecuteMove();
        }

        // 말과 윷 결과가 둘 다 선택되었는지 확인하고 이동 실행 (순서 상관없이 둘 다 채워지면 실행됨)
        private void TryExecuteMove()
        {
            if (selectedPieceId >= 0 && selectedResult.HasValue)
            {
                Debug.Log("[이동 실행] 말: " + selectedPieceId + ", 윷결과: " + selectedResult.Value);
                turnManager.RequestMovePiece(selectedPieceId, selectedResult.Value);
                ResetSelection(); // 이동 실행 후 선택 상태 초기화 (버튼 자체는 RefreshButtons에서 다시 갱신됨)
            }
            else if (!selectedResult.HasValue)
            {
                Debug.Log("이동에 사용할 [윷 결과]를 선택해주세요.");
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

        // 지금 떠있는 버튼들을 전부 지움 (다음 번 그릴 때 겹치지 않게)
        private void ClearButtons()
        {
            foreach (var button in spawnedButtons)
            {
                if (button != null) Destroy(button.gameObject);
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