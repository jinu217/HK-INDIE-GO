#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using YutArena.Managers;
using YutArena.Managers.GameProgress;
using YutArena.Common;

// Cached status is drawn in separate panels; Console retains exceptional actions.
public sealed class QaPlayingDebugWindow : EditorWindow
{
    private sealed class Block
    {
        public string Text;
        public float Height;
    }
    private PlayerDataDebugTester monitor;
    private Vector2 scroll;
    private double nextRefresh;
    private string filter = "", cachedFilter, cachedSnapshot, cachedEvents;
    private bool showEvents = true, showOptions;
    private float measuredWidth;
    private bool measureDirty = true;
    private GUIStyle textStyle;
    private readonly List<Block> statusBlocks = new List<Block>();
    private readonly List<Block> eventBlocks = new List<Block>();
    internal string CachedStatus => cachedSnapshot;
    internal int VisibleStatusBlockCount => statusBlocks.Count;
    [MenuItem("Tools/QA/Playing Debug %#q")]
    public static void Open() => ShowWindow(true);
    internal static void ShowWindow(bool focus)
    {
        Type dockTarget = typeof(EditorWindow).Assembly.GetType("UnityEditor.ConsoleWindow");
        QaPlayingDebugWindow previous = null;
        foreach (var existing in Resources.FindObjectsOfTypeAll<EditorWindow>())
        {
            if (existing.GetType().Name == "ConsoleWindow") dockTarget = existing.GetType();
            if (existing is QaPlayingDebugWindow qa) previous = qa;
        }
        // GetWindow keeps an existing floating instance; recreate it when migrating to the Console dock.
        if (previous != null && !previous.docked && dockTarget != null) previous.Close();
        var window = dockTarget != null
            ? GetWindow<QaPlayingDebugWindow>("Playing Debug", focus, dockTarget)
            : GetWindow<QaPlayingDebugWindow>(false, "Playing Debug", focus);
        window.minSize = new Vector2(680, 360);
        if (!window.docked)
        {
            Rect main = EditorGUIUtility.GetMainWindowPosition();
            Rect area = window.position;
            area.width = Mathf.Min(Mathf.Max(area.width, 960), Mathf.Max(680, main.width - 60));
            area.height = Mathf.Min(Mathf.Max(area.height, 640), Mathf.Max(360, main.height - 100));
            area.x = Mathf.Clamp(area.x, main.x, Mathf.Max(main.x, main.xMax - area.width));
            area.y = Mathf.Clamp(area.y, main.y + 40, Mathf.Max(main.y + 40, main.yMax - area.height));
            window.position = area;
        }
        window.ShowTab();
    }
    private void OnEnable() => EditorApplication.update += Refresh;
    private void OnDisable() => EditorApplication.update -= Refresh;
    private void Refresh()
    {
        if (EditorApplication.timeSinceStartup < nextRefresh) return;
        nextRefresh = EditorApplication.timeSinceStartup + 0.5;
        if (monitor == null)
        {
            monitor = FindFirstObjectByType<PlayerDataDebugTester>();
            cachedSnapshot = null; cachedEvents = null;
            scroll = Vector2.zero;
        }
        if (monitor != null && (cachedSnapshot != monitor.CurrentSnapshot || cachedEvents != monitor.RecentEvents || cachedFilter != filter))
        {
            cachedSnapshot = monitor.CurrentSnapshot; cachedEvents = monitor.RecentEvents; cachedFilter = filter;
            RebuildBlocks();
        }
        Repaint();
    }
    private void RebuildBlocks()
    {
        statusBlocks.Clear(); eventBlocks.Clear();
        if (cachedSnapshot != null)
        {
            var sections = cachedSnapshot.Replace("\r\n", "\n").Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < sections.Length; i++)
                if (i == 0 || Matches(sections[i])) statusBlocks.Add(new Block { Text = sections[i].TrimEnd() });
        }
        if (cachedEvents != null)
            foreach (string entry in cachedEvents.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries))
                if (Matches(entry)) eventBlocks.Add(new Block { Text = entry.TrimEnd() });
        measureDirty = true;
    }
    private bool Matches(string value) => string.IsNullOrWhiteSpace(filter) || value.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
    private void OnGUI()
    {
        if (textStyle == null) textStyle = new GUIStyle(EditorStyles.textArea) { wordWrap = true, fontSize = 12 };
        if (!EditorApplication.isPlaying)
        {
            EditorGUILayout.HelpBox("StartScene에서 Play → 로비 → 캐릭터 선택 → 인게임으로 진행하면 실제 상태가 여기에 표시됩니다.", MessageType.Info);
            return;
        }
        if (monitor == null)
        {
            EditorGUILayout.HelpBox("인게임 진입 대기 중입니다. 로비에서 저장한 설정과 생성된 플레이어를 자동으로 연결합니다.", MessageType.Info);
            return;
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(EditorApplication.isPaused ? "게임 계속" : "게임 일시정지"))
            {
                monitor.PollNow();
                EditorApplication.isPaused = !EditorApplication.isPaused;
            }
            if (GUILayout.Button("상태 + 행동 복사")) GUIUtility.systemCopyBuffer = monitor.CurrentSnapshot + "\n" + monitor.RecentEvents;
            if (GUILayout.Button("행동 기록 비우기")) monitor.ClearEventHistory();
            var gameManager = FindFirstObjectByType<TestGameManager>();
            using (new EditorGUI.DisabledScope(gameManager == null ||
                       gameManager.Session == null || gameManager.Session.phase != GamePhase.InGame))
            {
                if (GUILayout.Button(new GUIContent("게임 종료 (테스트)", "턴 진행 종료 및 결과 화면 표시")))
                    EndGameForTest(gameManager);
            }
        }
        filter = EditorGUILayout.TextField("플레이어/상태 검색", filter);
        showOptions = EditorGUILayout.Foldout(showOptions, "수집 / Console 설정");
        if (showOptions)
        {
            if (GUILayout.Button("현재 상태 Console 출력")) monitor.LogAllPieceData();
            monitor.enabled = EditorGUILayout.Toggle("QA 수집 활성화", monitor.enabled);
            monitor.LogGameplayEvents = EditorGUILayout.Toggle("Console 특별 행동", monitor.LogGameplayEvents);
            monitor.LogStateChanges = EditorGUILayout.Toggle("상태 변경도 Console에 누적", monitor.LogStateChanges);
            if (monitor.LogStateChanges)
                EditorGUILayout.HelpBox("값이 바뀐 로그는 Unity Console의 Collapse로 합쳐지지 않습니다. 상시 상태는 아래 고정 패널에서 확인하세요.", MessageType.Warning);
            monitor.RefreshInterval = EditorGUILayout.Slider("갱신 간격(초)", monitor.RefreshInterval, 0.25f, 10f);
            showEvents = EditorGUILayout.Toggle("특별 행동 표시 (최대 64개)", showEvents);
            EditorGUILayout.LabelField($"조회 {monitor.PollCount}회 / Console 출력 {monitor.ConsoleBatchCount}회");
        }
        float width = Mathf.Max(100, position.width - 36);
        if (measureDirty || !Mathf.Approximately(measuredWidth, width))
        {
            foreach (var block in statusBlocks) block.Height = Mathf.Max(22, textStyle.CalcHeight(new GUIContent(block.Text), width));
            foreach (var block in eventBlocks) block.Height = Mathf.Max(22, textStyle.CalcHeight(new GUIContent(block.Text), width));
            measuredWidth = width; measureDirty = false;
        }
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.LabelField("상시 STATUS — 설정 / 턴 흐름 / 플레이어별 보드 위 말", EditorStyles.boldLabel);
        foreach (var block in statusBlocks)
            EditorGUILayout.SelectableLabel(block.Text, textStyle, GUILayout.Width(width), GUILayout.Height(block.Height));
        if (showEvents)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("──── 특별 행동 ────", EditorStyles.boldLabel);
            if (eventBlocks.Count == 0) EditorGUILayout.LabelField("아직 기록된 특별 행동이 없습니다.");
            foreach (var block in eventBlocks)
                EditorGUILayout.SelectableLabel(block.Text, textStyle, GUILayout.Width(width), GUILayout.Height(block.Height));
        }
        EditorGUILayout.EndScrollView();
    }

    private static void EndGameForTest(TestGameManager gameManager)
    {
        if (!EditorApplication.isPlaying || gameManager == null ||
            gameManager.Session == null || gameManager.Session.phase != GamePhase.InGame) return;
        // TODO: 결과 화면 테스트 종료 후 삭제할 임시 랜덤 결과 생성
        var settings = gameManager.Settings;
        if (settings == null) return;
        var ranking = new List<TeamSlot>();
        for (int person = 1; person <= settings.playerCount; person++)
        {
            var team = MatchCompositionRule.GetPersonTeamSlot(settings, (PlayerSlot)person);
            if (team != TeamSlot.None && !ranking.Contains(team)) ranking.Add(team);
        }
        if (ranking.Count == 0) return;
        for (int i = ranking.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (ranking[i], ranking[j]) = (ranking[j], ranking[i]);
        }
        PlayerSlot winner = PlayerSlot.None;
        if (!settings.isTeamMode)
            for (int person = 1; person <= settings.playerCount; person++)
                if (MatchCompositionRule.GetPersonTeamSlot(settings, (PlayerSlot)person) == ranking[0])
                {
                    winner = (PlayerSlot)person;
                    break;
                }
        var result = new GameResultData
        {
            resultType = GameResultType.TeamWin,
            winningTeam = ranking[0],
            winningPlayer = winner,
            finalRanking = ranking
        };
        var turnManager = FindFirstObjectByType<TestTurnManager>();
        if (turnManager != null && turnManager.CurrentTurn != null) turnManager.NotifyGameEnded();
        gameManager.EndGame(result);
        EditorApplication.isPaused = false;
    }
}

// Opens once when the real in-game monitor appears, including scene transitions from StartScene.
[InitializeOnLoad]
internal static class QaPlayingDebugAutoOpen
{
    private static bool opened;
    private static double nextScan;
    static QaPlayingDebugAutoOpen()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode) { opened = false; nextScan = 0; }
            if (state == PlayModeStateChange.ExitingPlayMode) opened = true;
        };
        EditorApplication.update += TryOpen;
    }
    private static void TryOpen()
    {
        if (!EditorApplication.isPlaying || opened || EditorApplication.timeSinceStartup < nextScan) return;
        nextScan = EditorApplication.timeSinceStartup + 0.5;
        if (UnityEngine.Object.FindFirstObjectByType<PlayerDataDebugTester>() == null) return;
        opened = true;
        QaPlayingDebugWindow.ShowWindow(false);
    }
}
[CustomEditor(typeof(PlayerDataDebugTester))]
internal sealed class PlayerDataDebugTesterEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        if (GUILayout.Button("플레이 디버그 창 열기")) QaPlayingDebugWindow.Open();
        EditorGUILayout.HelpBox("상시 상태는 Playing Debug에서 갱신합니다. Console은 특별 행동을 기록합니다.", MessageType.Info);
        if (EditorApplication.isPlaying && GUILayout.Button("현재 상태 Console 한 번 출력"))
            ((PlayerDataDebugTester)target).LogAllPieceData();
    }
}
#endif

