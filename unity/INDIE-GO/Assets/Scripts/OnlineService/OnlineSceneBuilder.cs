#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using YutArena.UI;
using Object = UnityEngine.Object;

namespace YutArena.Online
{
    // Explicit editor tool only; no automatic scene rebuilds or runtime UI generation.
    public static class OnlineSceneBuilder
    {
        private const string Root = "Assets/Scripts/OnlineService/";
        private static readonly Color Navy = Hex("19334B"), Blue = Hex("2C6088"), Cream = Hex("F3EFE3"),
            Paper = Hex("FFFCF5"), Gold = Hex("BC955C"), Ink = Hex("203E59"), Muted = Hex("66716F"), Soft = Hex("EEE8DB");
        private static TMP_FontAsset font;
        private static GameObject sessionPrefab;

        [MenuItem("Tools/INDIE-GO/Build Online Lobby Scenes")]
        public static void Build()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("열린 씬을 먼저 저장해주세요.");
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before building online scenes.");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            const string fontPath = Root + "OnlineLobbyFont.asset";
            if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontPath) == null)
                AssetDatabase.CopyAsset("Assets/Fonts/NEXON Lv2 Gothic SDF.asset", fontPath);
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontPath);
            if (font == null) throw new InvalidOperationException("Project Korean TMP font is missing.");
            BuildPrefabs();
            BuildTitle();
            BuildRoomSettings();
            BuildRoomList();
            BuildWaitingRoom();
            var scenes = EditorBuildSettings.scenes.ToList();
            foreach (string name in new[] { OnlineSessionController.RoomListScene, OnlineSessionController.WaitingRoomScene })
            {
                string path = Root + name + ".unity";
                int index = scenes.FindIndex(s => s.path == path);
                if (index < 0) scenes.Add(new EditorBuildSettingsScene(path, true));
                else scenes[index] = new EditorBuildSettingsScene(path, true);
            }
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            if (setup.Length > 0 && setup.All(s => !string.IsNullOrEmpty(s.path))) EditorSceneManager.RestoreSceneManagerSetup(setup);
            Debug.Log("Online scenes generated: title popup, reused room settings, room list, waiting room.");
        }

        private static void BuildPrefabs()
        {
            var runnerObject = new GameObject("OnlineLobbyRunner");
            var runner = runnerObject.AddComponent<NetworkRunner>();
            runner.ProvideInput = false;
            runnerObject.AddComponent<NetworkSceneManagerDefault>();
            var runnerPrefab = PrefabUtility.SaveAsPrefabAsset(runnerObject, Root + "OnlineLobbyRunner.prefab");
            Object.DestroyImmediate(runnerObject);
            var sessionObject = new GameObject("OnlineSession");
            var session = sessionObject.AddComponent<OnlineSessionController>();
            Set(session, "runnerPrefab", runnerPrefab.GetComponent<NetworkRunner>());
            sessionPrefab = PrefabUtility.SaveAsPrefabAsset(sessionObject, Root + "OnlineSession.prefab");
            Object.DestroyImmediate(sessionObject);
        }

        private static void AddSession()
        {
            if (Object.FindFirstObjectByType<OnlineSessionController>() == null)
                PrefabUtility.InstantiatePrefab(sessionPrefab);
        }

        private static void BuildTitle()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/StartScene.unity");
            var controller = Object.FindFirstObjectByType<StartSceneUIController>();
            var group = Get<GameObject>(controller, "onlineGameButtons");
            var create = Get<UnityEngine.UI.Button>(controller, "onlineCreateGameButton");
            var join = Get<UnityEngine.UI.Button>(controller, "onlineJoinGameButton");
            var canvas = group.GetComponentInParent<Canvas>().GetComponent<RectTransform>();
            var rt = group.GetComponent<RectTransform>();
            foreach (var layout in group.GetComponents<LayoutGroup>()) layout.enabled = false;
            rt.SetParent(canvas, false);
            Place(rt, 1060, 350, 600, 500);
            var image = group.GetComponent<UnityEngine.UI.Image>() ?? group.AddComponent<UnityEngine.UI.Image>();
            image.sprite = null;
            image.color = Paper;
            Border(group);
            RemoveChild(group.transform, "OnlineHeader");
            RemoveChild(group.transform, "OnlineStatus");
            RemoveChild(group.transform, "OnlineBack");
            RemoveChild(group.transform, "OnlineReconnect");
            var header = Panel("OnlineHeader", rt, 0, 0, 600, 78, Navy);
            Label("Title", header, "온라인 플레이", 28, 12, 540, 52, 30, Paper, true);
            Place(create.GetComponent<RectTransform>(), 32, 106, 536, 72);
            Place(join.GetComponent<RectTransform>(), 32, 194, 536, 72);
            RestyleButton(create, "방 만들기");
            RestyleButton(join, "방 참가하기");
            var status = Label("OnlineStatus", rt, "온라인에 연결하고 있습니다…", 32, 282, 536, 95, 22, Muted);
            var reconnect = Button("OnlineReconnect", rt, "다시 연결", 32, 398, 240, 60, false);
            var back = Button("OnlineBack", rt, "뒤로가기", 296, 398, 272, 60, false);
            var view = group.GetComponent<OnlineTitleMenuView>() ?? group.AddComponent<OnlineTitleMenuView>();
            Set(view, "statusText", status); Set(view, "createButton", create); Set(view, "joinButton", join);
            Set(view, "reconnectButton", reconnect); Set(view, "backButton", back);
            group.SetActive(false);
            AddSession();
            EditorSceneManager.SaveScene(scene);
        }

        private static void RestyleButton(UnityEngine.UI.Button button, string value)
        {
            button.image.sprite = null;
            button.image.color = Blue;
            var existing = button.GetComponentsInChildren<TMP_Text>(true);
            if (existing.Length > 0)
            {
                existing[0].text = value; existing[0].color = Paper; existing[0].font = font;
                existing[0].fontSize = 28; existing[0].enableAutoSizing = false;
                Stretch(existing[0].rectTransform);
                existing[0].alignment = TextAlignmentOptions.Center;
            }
            else Label("OnlineButtonLabel", button.transform, value, 0, 0, 536, 72, 28, Paper, true, TextAlignmentOptions.Center);
            Tint(button);
        }

        private static void BuildRoomSettings()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/LocalLobbyScene.unity");
            var controller = Object.FindFirstObjectByType<LobbySceneUIController>();
            var start = Get<UnityEngine.UI.Button>(controller, "startGameButton");
            var canvas = start.GetComponentInParent<Canvas>().transform;
            RemoveChild(canvas, "OnlineRoomSetup");
            var root = Panel("OnlineRoomSetup", canvas, 540, 12, 1290, 102, Paper);
            Border(root.gameObject);
            Label("RoomLabel", root, "방 이름", 20, 10, 100, 30, 20, Ink, true);
            var roomName = Input("RoomName", root, "방 이름을 입력하세요", 126, 8, 580, 46, 40);
            Label("NicknameLabel", root, "닉네임", 734, 10, 100, 30, 20, Ink, true);
            var nickname = Input("Nickname", root, "닉네임", 840, 8, 426, 46, 20);
            var status = Label("Status", root, "설정 완료를 누르면 온라인 대기실이 생성됩니다.", 20, 62, 1246, 30, 19, Muted);
            var view = root.gameObject.AddComponent<OnlineRoomSettingsView>();
            Set(view, "roomNameInput", roomName); Set(view, "nicknameInput", nickname); Set(view, "statusText", status);
            RemoveChild(start.transform, "OnlineStartSurface");
            var startLabel = start.GetComponentInChildren<TMP_Text>(true);
            if (startLabel == null) throw new InvalidOperationException("기존 시작 버튼의 TMP 문구를 찾을 수 없습니다.");
            Set(view, "startButtonLabel", startLabel);
            var localObjects = new List<GameObject>();
            foreach (string property in new[] { "sharedInputLeftButton", "sharedInputRightButton", "sharedInputLabel" })
            {
                var component = Get<Component>(controller, property);
                if (component != null) localObjects.Add(component.gameObject);
            }
            var joinInput = Object.FindFirstObjectByType<LobbyControllerJoinInput>();
            if (joinInput != null)
            {
                var joinSerialized = new SerializedObject(joinInput);
                var icons = joinSerialized.FindProperty("playerControllerImages");
                for (int i = 0; icons != null && i < icons.arraySize; i++)
                {
                    var icon = icons.GetArrayElementAtIndex(i).objectReferenceValue as Component;
                    if (icon != null) localObjects.Add(icon.gameObject);
                }
                SetArray(view, "localOnlyBehaviours", new Object[] { joinInput });
            }
            SetArray(view, "localOnlyObjects", localObjects.ToArray());
            EditorSceneManager.SaveScene(scene);
        }

        private static RectTransform NewScreen(string title, string subtitle)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Cream;
            camera.orthographic = true; camera.transform.position = new Vector3(0, 0, -10);
            var go = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            var rt = go.GetComponent<RectTransform>();
            var bg = Panel("Background", rt, 0, 0, 1920, 1080, Cream);
            Stretch(bg);
            Label("Breadcrumb", rt, "윷 아레나    /    온라인 플레이", 180, 48, 1100, 34, 23, Muted);
            Label("PageTitle", rt, title, 180, 94, 1200, 62, 45, Ink, true);
            Label("Subtitle", rt, subtitle, 180, 992, 1560, 42, 21, Muted);
            var eventSystem = new GameObject("EventSystem");
            eventSystem.SetActive(false);
            eventSystem.AddComponent<EventSystem>();
            var module = eventSystem.AddComponent<InputSystemUIInputModule>();
            const string actionsPath = "Packages/com.unity.inputsystem/InputSystem/Plugins/PlayerInput/DefaultInputActions.inputactions";
            module.actionsAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(actionsPath);
            var references = AssetDatabase.LoadAllAssetsAtPath(actionsPath).OfType<InputActionReference>().ToArray();
            InputActionReference Action(string action) => references.First(r => r.action.actionMap.name == "UI" && r.action.name == action);
            module.point = Action("Point"); module.leftClick = Action("Click"); module.rightClick = Action("RightClick");
            module.middleClick = Action("MiddleClick"); module.scrollWheel = Action("ScrollWheel");
            module.move = Action("Navigate"); module.submit = Action("Submit"); module.cancel = Action("Cancel");
            eventSystem.SetActive(true);
            AddSession();
            return rt;
        }

        private static RectTransform Card(RectTransform canvas, string title)
        {
            var shadow = Panel("PanelShadow", canvas, 184, 186, 1560, 770, Hex("DED7C7"));
            var card = Panel("MainPanel", canvas, 180, 180, 1560, 770, Paper);
            Border(card.gameObject);
            var header = Panel("Header", card, 0, 0, 1560, 90, Navy);
            Label("Title", header, title, 36, 20, 1200, 52, 34, Paper, true);
            Label("LobbyHint", header, "온라인 로비", 1280, 26, 240, 38, 21, Hex("CCD5D9"), false, TextAlignmentOptions.Right);
            Panel("HeaderRule", card, 0, 88, 1560, 2, Gold);
            return card;
        }

        private static void BuildRoomList()
        {
            var canvas = NewScreen("방 참가와 온라인 대기실", "공개 방을 선택하거나, 친구에게 받은 방 코드를 입력해 참가하세요.");
            var card = Card(canvas, "공개 방 목록");
            var view = card.gameObject.AddComponent<OnlineRoomListView>();
            Label("NicknameLabel", card, "닉네임", 36, 116, 100, 45, 23, Ink, true);
            var nickname = Input("Nickname", card, "닉네임", 150, 112, 470, 54, 20);
            var count = Label("RoomCount", card, "공개 방 0개", 700, 116, 400, 45, 23, Muted);
            var refresh = Button("Refresh", card, "새로고침", 1284, 112, 240, 54, false);
            var headings = Panel("ColumnHeadings", card, 36, 196, 1488, 56, Soft);
            foreach (var column in new[] { ("방 이름", 24f, 640f), ("모드", 700f, 240f), ("대전 방식", 990f, 200f), ("인원", 1230f, 230f) })
                Label("Heading", headings, column.Item1, column.Item2, 8, column.Item3, 40, 21, Muted, true);
            var viewport = Panel("RoomViewport", card, 36, 254, 1488, 280, Paper);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = new GameObject("RoomRows", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(viewport, false); content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0, 1); content.sizeDelta = new Vector2(0, 0);
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childControlHeight = true; layout.childControlWidth = true; layout.childForceExpandHeight = false;
            layout.spacing = 4;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>(); fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = viewport.gameObject.AddComponent<ScrollRect>(); scroll.viewport = viewport;
            scroll.content = content; scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 38;
            var row = Panel("RoomRowTemplate", content, 0, 0, 1488, 68, Paper);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 68;
            var rowButton = row.gameObject.AddComponent<UnityEngine.UI.Button>(); rowButton.targetGraphic = row.GetComponent<UnityEngine.UI.Image>(); Tint(rowButton);
            var rowView = row.gameObject.AddComponent<OnlineRoomRowView>();
            Set(rowView, "selectButton", rowButton); Set(rowView, "background", row.GetComponent<UnityEngine.UI.Image>());
            Set(rowView, "nameText", Label("RoomName", row, "방 이름", 24, 12, 640, 44, 25, Ink, true));
            Set(rowView, "modeText", Label("Mode", row, "—", 700, 12, 250, 44, 23, Ink));
            Set(rowView, "compositionText", Label("Composition", row, "—", 990, 12, 210, 44, 23, Ink));
            Set(rowView, "countText", Label("Count", row, "0/4", 1230, 12, 250, 44, 23, Ink));
            row.gameObject.SetActive(false);
            var empty = Label("Empty", card, "아직 열린 방이 없습니다.\n친구의 방 코드를 입력하거나 직접 방을 만들어보세요.", 70, 300, 1420, 160, 26, Muted, false, TextAlignmentOptions.Center);
            var selected = Button("JoinSelected", card, "선택한 방 참가", 1184, 548, 340, 58);
            Panel("Divider", card, 36, 627, 1488, 2, Gold);
            Label("CodeLabel", card, "방 코드", 36, 654, 120, 56, 24, Ink, true);
            var code = Input("RoomCode", card, "공유받은 방 코드 입력", 170, 654, 924, 56, 36);
            var joinCode = Button("JoinByCode", card, "코드로 참가", 1120, 654, 404, 56);
            var back = Button("Back", canvas, "타이틀로", 180, 966, 230, 62, false);
            var status = Label("Status", card, "방 목록에 연결하고 있습니다…", 36, 720, 1488, 38, 21, Muted);
            Set(view, "roomRows", content); Set(view, "rowTemplate", rowView); Set(view, "emptyText", empty);
            Set(view, "statusText", status); Set(view, "roomCountText", count); Set(view, "nicknameInput", nickname);
            Set(view, "codeInput", code); Set(view, "refreshButton", refresh); Set(view, "joinSelectedButton", selected);
            Set(view, "joinCodeButton", joinCode); Set(view, "backButton", back);
            // Footer copy stays to the right of the back button.
            var subtitle = canvas.Find("Subtitle").GetComponent<RectTransform>(); Place(subtitle, 450, 982, 1290, 50);
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), Root + OnlineSessionController.RoomListScene + ".unity");
        }

        private static void BuildWaitingRoom()
        {
            var canvas = NewScreen("온라인 대기실", "방 코드를 공유해 친구를 초대하고 함께 준비해주세요.");
            var card = Card(canvas, "온라인 대기실");
            var view = card.gameObject.AddComponent<OnlineWaitingRoomView>();
            var roomTitle = Label("RoomName", card, "대기실 연결 중", 36, 108, 1450, 52, 32, Ink, true);
            Label("CodeLabel", card, "방 코드", 36, 178, 110, 42, 22, Muted, true);
            var code = Label("RoomCode", card, "—", 160, 178, 850, 42, 26, Ink, true);
            var copy = Button("CopyCode", card, "복사", 1040, 170, 160, 54, false);
            Label("InviteHint", card, "복사한 코드를 친구에게 전달하세요.", 1220, 166, 304, 64, 21, Muted);
            var modeCard = Panel("GameMode", card, 36, 252, 726, 118, Soft);
            Label("Caption", modeCard, "게임 모드", 24, 14, 650, 35, 21, Muted);
            var mode = Label("Value", modeCard, "—", 24, 54, 650, 50, 31, Ink, true);
            var mapCard = Panel("Map", card, 786, 252, 738, 118, Soft);
            Label("Caption", mapCard, "맵 선택", 24, 14, 680, 35, 21, Muted);
            var map = Label("Value", mapCard, "—", 24, 54, 680, 50, 31, Ink, true);
            var players = Panel("Players", card, 36, 394, 726, 248, Soft);
            var playerCount = Label("PlayerCount", players, "플레이어  0/4", 24, 12, 670, 34, 23, Ink, true);
            var names = new List<TMP_Text>(); var states = new List<TMP_Text>();
            for (int i = 0; i < 4; i++)
            {
                var dot = Panel("SlotColor" + (i + 1), players, 26, 65 + i * 44, 12, 12,
                    i == 0 ? Hex("AF5546") : i == 1 ? Hex("407498") : Hex("A4AA9A"));
                names.Add(Label("Player" + (i + 1), players, "참가 대기 중", 54, 52 + i * 44, 365, 40, 23, Ink));
                states.Add(Label("State" + (i + 1), players, "—", 436, 52 + i * 44, 266, 40, 21, Muted, false, TextAlignmentOptions.Right));
            }
            var settingsCard = Panel("RoomConditions", card, 786, 394, 738, 248, Soft);
            Label("Caption", settingsCard, "대전 규칙", 24, 12, 680, 34, 23, Ink, true);
            var settings = Label("Settings", settingsCard, "방 조건을 불러오고 있습니다.", 24, 56, 680, 182, 23, Ink);
            var leave = Button("Leave", card, "나가기", 36, 676, 220, 60, false);
            var teamOne = Button("TeamOne", card, "1팀", 290, 676, 170, 60, false);
            var teamTwo = Button("TeamTwo", card, "2팀", 476, 676, 170, 60, false);
            var ready = Button("Ready", card, "준비", 1004, 676, 520, 60);
            var status = Label("Status", canvas, "대기실을 불러오고 있습니다…", 180, 968, 1560, 38, 22, Muted);
            Place(canvas.Find("Subtitle").GetComponent<RectTransform>(), 180, 1018, 1560, 32);
            Set(view, "roomNameText", roomTitle); Set(view, "codeText", code); Set(view, "modeText", mode);
            Set(view, "mapText", map); Set(view, "settingsText", settings); Set(view, "playerCountText", playerCount);
            SetArray(view, "playerNameTexts", names.ToArray()); SetArray(view, "playerStateTexts", states.ToArray());
            Set(view, "statusText", status); Set(view, "readyLabel", ready.GetComponentInChildren<TMP_Text>());
            Set(view, "readyButton", ready); Set(view, "copyCodeButton", copy); Set(view, "teamOneButton", teamOne);
            Set(view, "teamTwoButton", teamTwo); Set(view, "leaveButton", leave);
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), Root + OnlineSessionController.WaitingRoomScene + ".unity");
        }

        private static Color Hex(string hex) { ColorUtility.TryParseHtmlString("#" + hex, out var color); return color; }
        private static void Place(RectTransform rt, float x, float y, float width, float height)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, -y); rt.sizeDelta = new Vector2(width, height);
            rt.localScale = Vector3.one;
        }
        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
        private static RectTransform Panel(string name, Transform parent, float x, float y, float width, float height, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image)); go.layer = 5;
            var rt = go.GetComponent<RectTransform>(); rt.SetParent(parent, false); Place(rt, x, y, width, height);
            go.GetComponent<UnityEngine.UI.Image>().color = color;
            return rt;
        }
        private static void Border(GameObject go)
        {
            var border = go.GetComponent<Outline>() ?? go.AddComponent<Outline>();
            border.effectColor = Gold; border.effectDistance = new Vector2(1.5f, -1.5f); border.useGraphicAlpha = true;
        }
        private static TMP_Text Label(string name, Transform parent, string value, float x, float y, float width,
            float height, float size, Color color, bool bold = false, TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)); go.layer = 5;
            var rt = go.GetComponent<RectTransform>(); rt.SetParent(parent, false); Place(rt, x, y, width, height);
            var text = go.GetComponent<TextMeshProUGUI>(); text.font = font; text.fontSize = size; text.text = value;
            text.color = color; text.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            text.alignment = align; text.raycastTarget = false; text.richText = false;
            text.textWrappingMode = TextWrappingModes.Normal;
            return text;
        }
        private static UnityEngine.UI.Button Button(string name, Transform parent, string label, float x, float y,
            float width, float height, bool primary = true)
        {
            var rt = Panel(name, parent, x, y, width, height, primary ? Blue : Paper);
            Border(rt.gameObject);
            var button = rt.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = rt.GetComponent<UnityEngine.UI.Image>();
            Tint(button);
            Label("Label", rt, label, 12, 4, width - 24, height - 8, 24, primary ? Paper : Ink, true, TextAlignmentOptions.Center);
            return button;
        }
        private static void Tint(UnityEngine.UI.Button button)
        {
            var colors = button.colors; colors.normalColor = Color.white;
            colors.highlightedColor = new Color(.87f, .92f, 1); colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color(.7f, .78f, .86f); colors.disabledColor = new Color(.7f, .7f, .7f, .62f);
            colors.fadeDuration = .12f; button.colors = colors;
        }
        private static TMP_InputField Input(string name, Transform parent, string placeholder, float x, float y,
            float width, float height, int limit)
        {
            var rt = Panel(name, parent, x, y, width, height, Color.white); Border(rt.gameObject);
            var input = rt.gameObject.AddComponent<TMP_InputField>(); input.targetGraphic = rt.GetComponent<UnityEngine.UI.Image>();
            var area = new GameObject("TextArea", typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
            area.SetParent(rt, false); Place(area, 14, 4, width - 28, height - 8);
            var value = Label("Text", area, "", 0, 0, width - 28, height - 8, 23, Ink);
            var hint = Label("Placeholder", area, placeholder, 0, 0, width - 28, height - 8, 21, Muted);
            input.textViewport = area; input.textComponent = (TextMeshProUGUI)value; input.placeholder = hint;
            input.characterLimit = limit; input.lineType = TMP_InputField.LineType.SingleLine; input.richText = false;
            return input;
        }
        private static T Get<T>(Object owner, string name) where T : Object
        {
            return new SerializedObject(owner).FindProperty(name).objectReferenceValue as T;
        }
        private static void Set(Object owner, string name, Object value)
        {
            var serialized = new SerializedObject(owner); serialized.FindProperty(name).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        private static void SetArray(Object owner, string name, Object[] values)
        {
            var serialized = new SerializedObject(owner); var property = serialized.FindProperty(name); property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        private static void RemoveChild(Transform parent, string name)
        {
            var child = parent.Find(name); if (child != null) Object.DestroyImmediate(child.gameObject);
        }
    }
}
#endif
