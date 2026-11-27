using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YutArena.Common;
using YutArena.Managers;
using YutArena.Managers.GameProgress;
using YutArena.Test;

namespace YutArena.UI
{
    // ===================================================================
    // 윷 결과 표시 UI (표시 전용, 우측 하단 YutResultPanel 에 붙어 있음)
    //
    // 실제 이동 선택(말 클릭 → 목적지 타일 클릭)은 MoveDestinationSelector 가 담당한다.
    //
    // [윷 결과 토큰] (기획: 윷 결과를 가운데에 띄웠다가 작아졌다 커졌다 → 우측 하단 결과 칸으로 뿅 이동)
    // 1. 3D 윷(YutThrowManager)이 다 떨어질 때까지 기다림
    // 2. 화면 가운데에 결과 토큰을 띄워 작아졌다 커지는 팝
    // 3. 결과 칸으로 날아가서 작은 토큰으로 쌓임 (결과를 쓰면 빠짐)
    // - 낙: 가운데에서만 보여주고 사라짐 (턴이 바로 끝나서 칸에 들어갈 결과가 없음)
    // - 전우치 [축지법] 대상 결과는 ×2 토큰
    // 토큰 그림은 Sprites/인게임창/윷토큰/token_{do|gae|geol|yut|mo|backdo|nak}(_x2).png
    // Inspector 의 Token Sprites 에 연결 (에디터에서는 비어 있으면 폴더에서 자동으로 채움)
    // 그림이 없으면 예전처럼 글자로 표시. 오브젝트는 실행할 때 코드로 만들어서 씬은 안 건드림
    // ===================================================================
    public class YutResultSelectorUI : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private TestTurnManager turnManager;

        [Header("표시")]
        [Tooltip("토큰 그림이 없을 때만 쓰는 글자. 비우면 Awake 에서 자식으로 자동 생성합니다.")]
        [SerializeField] private TextMeshProUGUI resultLabel;
        [SerializeField] private string emptyText = "";

        [Header("윷 결과 토큰")]
        [Tooltip("token_do ~ token_nak 그림 13장 (이름으로 찾음). 에디터에서는 비어 있으면 윷토큰 폴더에서 자동으로 채움")]
        [SerializeField] private Sprite[] tokenSprites = new Sprite[0];
        [Tooltip("가운데에 뜰 때 토큰 크기 (캔버스 단위)")]
        [SerializeField, Min(10f)] private float centerTokenSize = 286f;   // 기획 요청(11/25): 처음(260)의 1.1배
        [Tooltip("결과 칸에 쌓이는 토큰 크기 (캔버스 단위). 패널 크기와 상관없이 이 값으로 고정")]
        [SerializeField, Min(10f)] private float slotTokenSize = 200f;
        [Tooltip("결과 칸 토큰 사이 간격")]
        [SerializeField, Min(0f)] private float slotTokenSpacing = 6f;
        [Tooltip("가운데에서 작아졌다 커지는 시간")]
        [SerializeField, Min(0.01f)] private float popDuration = 0.45f;
        [Tooltip("팝 후 가운데에서 멈춰 보여주는 시간")]
        [SerializeField, Min(0f)] private float holdDuration = 0.35f;
        [Tooltip("결과 칸으로 날아가는 시간")]
        [SerializeField, Min(0.01f)] private float flyDuration = 0.25f;
        [Tooltip("낙 토큰이 사라지는 시간")]
        [SerializeField, Min(0.01f)] private float nakFadeDuration = 0.4f;

        // 윷 결과 연출(가운데 토큰)이 시작/끝날 때. 낙은 결과 묶음에 들어가지 않으므로 data 가 null.
        // 채팅 시스템 로그가 연출 시점에 맞춰 찍히도록 사용한다.
        public event System.Action<YutThrowData, YutResult> OnResultPresentationStarted;
        public event System.Action<YutThrowData, YutResult> OnResultPresentationEnded;

        private const string TokenFolder = "Assets/Sprites/인게임창/윷토큰";
        private readonly Dictionary<string, Sprite> spriteByName = new Dictionary<string, Sprite>();

        private bool tokensAvailable;
        private Canvas rootCanvas;
        private RectTransform slotRow;
        private YutThrowManager throwManager;
        private Coroutine tokenRoutine;
        private GameObject flyingToken;
        private List<YutThrowData> latestPending = new List<YutThrowData>();
        private readonly Queue<YutThrowData> tokenQueue = new Queue<YutThrowData>();
        private readonly HashSet<YutThrowData> queuedOrFlying = new HashSet<YutThrowData>();
        private readonly Dictionary<YutThrowData, Image> slotIcons = new Dictionary<YutThrowData, Image>();

        private void Awake()
        {
            EnsureLabel();
            SetText(string.Empty);
#if UNITY_EDITOR
            FillTokenSpritesFromFolder();
#endif
            spriteByName.Clear();
            foreach (Sprite sprite in tokenSprites)
                if (sprite != null) spriteByName[sprite.name] = sprite;
            tokensAvailable = LoadSprite("token_do") != null;
            if (tokensAvailable) EnsureSlotRow();
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
            turnManager.OnTurnNotice += HandleTurnNotice;
        }

        private void OnDisable()
        {
            if (turnManager == null) return;
            turnManager.OnPendingResultsChanged -= RefreshDisplay;
            turnManager.OnTurnStarted -= HandleTurnStarted;
            turnManager.OnTurnNotice -= HandleTurnNotice;
            ClearTokens();
        }

        // 턴이 시작되면 pendingResults 는 비워지지만 이벤트가 오지 않으므로 여기서 비운다.
        private void HandleTurnStarted(PlayerSlot _)
        {
            SetText(string.Empty);
            ClearTokens();
        }

        // pendingResults 가 바뀔 때마다(던짐/소비) 아직 안 쓴 결과 목록을 갱신 표시.
        private void RefreshDisplay(List<YutThrowData> pendingResults)
        {
            latestPending = pendingResults != null ? new List<YutThrowData>(pendingResults) : new List<YutThrowData>();

            if (!tokensAvailable)
            {
                // 토큰 연출이 없으면 새 결과가 나온 즉시 연출 시작·끝으로 알림
                foreach (var data in latestPending)
                {
                    if (!queuedOrFlying.Add(data)) continue;
                    OnResultPresentationStarted?.Invoke(data, data.result);
                    OnResultPresentationEnded?.Invoke(data, data.result);
                }
                RefreshText(latestPending);
                return;
            }

            // 쓴 결과는 결과 칸에서 뺌
            var used = new List<YutThrowData>();
            foreach (var pair in slotIcons)
                if (!latestPending.Contains(pair.Key)) used.Add(pair.Key);
            foreach (var data in used)
            {
                if (slotIcons[data] != null) Destroy(slotIcons[data].gameObject);
                slotIcons.Remove(data);
            }
            if (used.Count > 0) ResizeSlotIcons();

            // 새로 나온 결과는 토큰으로 날려서 넣음
            foreach (var data in latestPending)
            {
                if (slotIcons.ContainsKey(data) || queuedOrFlying.Contains(data)) continue;
                queuedOrFlying.Add(data);
                tokenQueue.Enqueue(data);
            }
            if (tokenQueue.Count > 0 && tokenRoutine == null)
                tokenRoutine = StartCoroutine(PlayTokenQueue());
        }

        private void HandleTurnNotice(TurnNotice notice)
        {
            // 판에 말이 없는데 뒷도라서 낙 처리된 건 뒷도 토큰이 이미 떴으니 따로 안 띄움
            if (tokensAvailable && notice.type == TurnNoticeType.Nak && !notice.isBackDoNak && isActiveAndEnabled)
                StartCoroutine(PlayNakToken());
        }

        // ---- 토큰 연출 ----

        private IEnumerator PlayTokenQueue()
        {
            while (tokenQueue.Count > 0)
            {
                yield return WaitForThrowLanding();
                YutThrowData data = tokenQueue.Dequeue();
                if (!latestPending.Contains(data))
                {
                    queuedOrFlying.Remove(data); // 날아가기 전에 이미 씀
                    OnResultPresentationStarted?.Invoke(data, data.result);
                    OnResultPresentationEnded?.Invoke(data, data.result);
                    continue;
                }

                Sprite sprite = GetTokenSprite(data);
                RectTransform token = CreateCenterToken(sprite);
                flyingToken = token.gameObject;
                OnResultPresentationStarted?.Invoke(data, data.result);
                yield return Pop(token);
                if (holdDuration > 0f) yield return new WaitForSeconds(holdDuration);

                // 결과 칸에 자리를 먼저 만들고(투명) 그 자리로 날아감
                Image slot = AddSlotIcon(data, sprite);
                yield return FlyTo(token, slot.rectTransform);

                Destroy(token.gameObject);
                flyingToken = null;
                queuedOrFlying.Remove(data);
                if (slot != null) slot.color = Color.white;
                OnResultPresentationEnded?.Invoke(data, data.result);
            }
            tokenRoutine = null;
        }

        private IEnumerator PlayNakToken()
        {
            yield return WaitForThrowLanding();
            RectTransform token = CreateCenterToken(LoadSprite("token_nak"));
            OnResultPresentationStarted?.Invoke(null, YutResult.Nak);
            yield return Pop(token);
            if (holdDuration > 0f) yield return new WaitForSeconds(holdDuration);

            Image image = token.GetComponent<Image>();
            for (float t = 0f; t < nakFadeDuration; t += Time.deltaTime)
            {
                image.color = new Color(1f, 1f, 1f, 1f - t / nakFadeDuration);
                yield return null;
            }
            Destroy(token.gameObject);
            OnResultPresentationEnded?.Invoke(null, YutResult.Nak);
        }

        // 3D 윷이 다 떨어질 때까지 기다림 (윷 던지기 연출이 없는 씬이면 바로 진행)
        private IEnumerator WaitForThrowLanding()
        {
            if (throwManager == null) throwManager = FindFirstObjectByType<YutThrowManager>();
            while (throwManager != null && throwManager.isActiveAndEnabled && throwManager.IsThrowing)
                yield return null;
        }

        // 가운데에서 작아졌다가 커졌다가 원래 크기로 (1 → 0.75 → 1.15 → 1)
        private IEnumerator Pop(RectTransform token)
        {
            for (float t = 0f; t < popDuration; t += Time.deltaTime)
            {
                float p = t / popDuration;
                float scale = p < 0.35f ? Mathf.Lerp(1f, 0.75f, p / 0.35f)
                    : p < 0.75f ? Mathf.Lerp(0.75f, 1.15f, (p - 0.35f) / 0.4f)
                    : Mathf.Lerp(1.15f, 1f, (p - 0.75f) / 0.25f);
                token.localScale = Vector3.one * scale;
                yield return null;
            }
            token.localScale = Vector3.one;
        }

        // 결과 칸 자리로 빠르게 이동하면서 칸 크기로 줄어듦 (점점 빨라지게)
        private IEnumerator FlyTo(RectTransform token, RectTransform target)
        {
            Vector3 startPosition = token.position;
            Vector3 startScale = token.localScale;
            for (float t = 0f; t < flyDuration; t += Time.deltaTime)
            {
                if (target == null) yield break; // 날아가는 중에 결과를 씀
                float p = t / flyDuration;
                float eased = p * p;
                token.position = Vector3.Lerp(startPosition, target.position, eased);
                token.localScale = Vector3.Lerp(startScale, Vector3.one * GetSlotScale(token, target), eased);
                yield return null;
            }
        }

        // 결과 칸 토큰 크기를 가운데 토큰 기준 배율로 (캔버스 배율이 달라도 맞게 실제 크기로 비교)
        private float GetSlotScale(RectTransform token, RectTransform target)
        {
            float tokenWorld = centerTokenSize * token.parent.lossyScale.x;
            float targetWorld = target.rect.width * target.lossyScale.x;
            return tokenWorld > 0f ? targetWorld / tokenWorld : 1f;
        }

        private RectTransform CreateCenterToken(Sprite sprite)
        {
            if (rootCanvas == null)
            {
                Canvas canvas = GetComponentInParent<Canvas>();
                rootCanvas = canvas != null ? canvas.rootCanvas : null;
            }
            var go = new GameObject("YutResultToken", typeof(RectTransform), typeof(Image), typeof(Canvas));
            var rt = (RectTransform)go.transform;
            rt.SetParent(rootCanvas != null ? rootCanvas.transform : transform, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(centerTokenSize, centerTokenSize);
            rt.SetAsLastSibling();

            // 다른 UI 위에 보이게
            var canvasComponent = go.GetComponent<Canvas>();
            canvasComponent.overrideSorting = true;
            canvasComponent.sortingOrder = 500;

            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return rt;
        }

        // ---- 결과 칸 ----

        private void EnsureSlotRow()
        {
            if (slotRow != null) return;
            var go = new GameObject("YutResultSlots", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            slotRow = (RectTransform)go.transform;
            slotRow.SetParent(transform, false);
            slotRow.anchorMin = Vector2.zero;
            slotRow.anchorMax = Vector2.one;
            slotRow.offsetMin = Vector2.zero;
            slotRow.offsetMax = Vector2.zero;

            var layout = go.GetComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = slotTokenSpacing;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
        }

        // 자리를 투명하게 만들어 둠 (토큰이 도착하면 보이게)
        private Image AddSlotIcon(YutThrowData data, Sprite sprite)
        {
            EnsureSlotRow();
            var go = new GameObject("YutResultSlotToken", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(slotRow, false);
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.color = new Color(1f, 1f, 1f, 0f);
            slotIcons[data] = image;
            ResizeSlotIcons();
            return image;
        }

        // 결과 칸 토큰은 패널 크기와 상관없이 slotTokenSize 로 고정 (Inspector 에서 조절)
        private void ResizeSlotIcons()
        {
            if (slotRow == null) return;
            if (slotRow.childCount == 0) return;
            foreach (RectTransform child in slotRow)
                child.sizeDelta = new Vector2(slotTokenSize, slotTokenSize);
            LayoutRebuilder.ForceRebuildLayoutImmediate(slotRow);
        }

        private void ClearTokens()
        {
            if (tokenRoutine != null)
            {
                StopCoroutine(tokenRoutine);
                tokenRoutine = null;
            }
            if (flyingToken != null) Destroy(flyingToken);
            flyingToken = null;
            tokenQueue.Clear();
            queuedOrFlying.Clear();
            foreach (var icon in slotIcons.Values)
                if (icon != null) Destroy(icon.gameObject);
            slotIcons.Clear();
            latestPending.Clear();
        }

        // ---- 토큰 그림 ----

        private Sprite GetTokenSprite(YutThrowData data)
        {
            string name = "token_" + GetTokenName(data.result);
            if (CHAR_002_Status.IsDoubledResult(data))
            {
                Sprite doubled = LoadSprite(name + "_x2");
                if (doubled != null) return doubled;
            }
            return LoadSprite(name);
        }

        private static string GetTokenName(YutResult result)
        {
            switch (result)
            {
                case YutResult.Do: return "do";
                case YutResult.Gae: return "gae";
                case YutResult.Geol: return "geol";
                case YutResult.Yut: return "yut";
                case YutResult.Mo: return "mo";
                case YutResult.BackDo: return "backdo";
                default: return "nak";
            }
        }

        private Sprite LoadSprite(string name)
        {
            return spriteByName.TryGetValue(name, out Sprite sprite) ? sprite : null;
        }

#if UNITY_EDITOR
        // 에디터 전용: Token Sprites 가 비어 있으면 윷토큰 폴더의 그림으로 채움 (씬 저장 없이 테스트 가능)
        private void FillTokenSpritesFromFolder()
        {
            if (tokenSprites != null && tokenSprites.Length > 0) return;
            var found = new List<Sprite>();
            foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:Sprite token_", new[] { TokenFolder }))
            {
                Sprite sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
                if (sprite != null) found.Add(sprite);
            }
            tokenSprites = found.ToArray();
        }
#endif

        // ---- 글자 표시 (토큰 그림이 없을 때) ----

        private void RefreshText(List<YutThrowData> pendingResults)
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
