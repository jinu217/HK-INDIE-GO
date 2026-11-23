using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace YutArena.Online
{
    public sealed class OnlineRoomListView : MonoBehaviour
    {
        [SerializeField] private RectTransform roomRows;
        [SerializeField] private OnlineRoomRowView rowTemplate;
        [SerializeField] private TMP_Text emptyText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_Text roomCountText;
        [SerializeField] private TMP_InputField nicknameInput;
        [SerializeField] private TMP_InputField codeInput;
        [SerializeField] private Button refreshButton;
        [SerializeField] private Button joinSelectedButton;
        [SerializeField] private Button joinCodeButton;
        [SerializeField] private Button backButton;
        private OnlineSessionController session;
        private string selectedId;
        private readonly List<OnlineRoomRowView> rows = new List<OnlineRoomRowView>();

        private void Start()
        {
            session = OnlineSessionController.Instance;
            if (session == null) { statusText.text = "StartScene에서 온라인 플레이를 선택해주세요."; return; }
            nicknameInput.SetTextWithoutNotify(session.Nickname);
            nicknameInput.onValueChanged.AddListener(value => session.Nickname = value);
            codeInput.onValueChanged.AddListener(_ => RefreshState());
            refreshButton.onClick.AddListener(session.RefreshRooms);
            joinSelectedButton.onClick.AddListener(JoinSelected);
            joinCodeButton.onClick.AddListener(() => session.JoinRoom(codeInput.text));
            backButton.onClick.AddListener(session.LeaveToTitle);
            session.Changed += RefreshState;
            session.Service.RoomsChanged += RefreshRooms;
            RefreshRooms(session.Service.Rooms);
            if (session.Service.State != OnlineState.BrowsingRooms) session.RefreshRooms();
            RefreshState();
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                session?.LeaveToTitle();
        }

        private void JoinSelected()
        {
            var room = session.Service.Rooms.FirstOrDefault(item => item.SessionId == selectedId);
            if (room == null) { session.SetStatus("참가할 방을 선택해주세요."); return; }
            if (!room.CanJoin) { session.SetStatus(room.IsOpen ? "방이 가득 찼습니다." : "이미 경기가 시작되었거나 입장이 닫힌 방입니다."); return; }
            session.JoinRoom(room.SessionId);
        }

        private void RefreshRooms(IReadOnlyList<OnlineRoomInfo> rooms)
        {
            foreach (var row in rows) { row.gameObject.SetActive(false); Destroy(row.gameObject); }
            rows.Clear();
            if (!rooms.Any(room => room.SessionId == selectedId)) selectedId = null;
            foreach (var room in rooms)
            {
                var current = room;
                var row = Instantiate(rowTemplate, roomRows);
                row.gameObject.SetActive(true);
                row.Bind(room, selectedId == room.SessionId, () =>
                {
                    selectedId = current.SessionId;
                    RefreshRooms(session.Service.Rooms);
                });
                rows.Add(row);
            }
            emptyText.gameObject.SetActive(rooms.Count == 0);
            roomCountText.text = $"공개 방 {rooms.Count}개";
            RefreshState();
        }

        private void RefreshState()
        {
            if (session == null) return;
            statusText.text = session.Status;
            bool available = !session.Busy;
            refreshButton.interactable = available;
            joinSelectedButton.interactable = available && selectedId != null;
            joinCodeButton.interactable = available && !string.IsNullOrWhiteSpace(codeInput.text);
            nicknameInput.interactable = codeInput.interactable = available;

            foreach (var row in rows) row.GetComponent<Button>().interactable = available;
        }

        private void OnDestroy()
        {
            if (session == null) return;
            session.Changed -= RefreshState;
            session.Service.RoomsChanged -= RefreshRooms;
        }

    }
}
