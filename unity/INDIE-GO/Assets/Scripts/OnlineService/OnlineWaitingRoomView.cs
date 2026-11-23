using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace YutArena.Online
{
    public sealed class OnlineWaitingRoomView : MonoBehaviour
    {
        [SerializeField] private TMP_Text roomNameText;
        [SerializeField] private TMP_Text codeText;
        [SerializeField] private TMP_Text modeText;
        [SerializeField] private TMP_Text mapText;
        [SerializeField] private TMP_Text settingsText;
        [SerializeField] private TMP_Text playerCountText;
        [SerializeField] private TMP_Text[] playerNameTexts;
        [SerializeField] private TMP_Text[] playerStateTexts;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_Text readyLabel;
        [SerializeField] private Button readyButton;
        [SerializeField] private Button copyCodeButton;
        [SerializeField] private Button teamOneButton;
        [SerializeField] private Button teamTwoButton;
        [SerializeField] private Button leaveButton;
        private OnlineSessionController session;

        private void Start()
        {
            session = OnlineSessionController.Instance;
            if (session == null) { statusText.text = "StartScene에서 방을 만들거나 참가해주세요."; return; }
            readyButton.onClick.AddListener(ToggleReady);
            copyCodeButton.onClick.AddListener(CopyCode);
            teamOneButton.onClick.AddListener(() => session.SetTeam(1));
            teamTwoButton.onClick.AddListener(() => session.SetTeam(2));
            leaveButton.onClick.AddListener(session.LeaveToTitle);
            session.Changed += Refresh;
            Refresh();
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                session?.LeaveToTitle();
        }

        private void ToggleReady()
        {
            var player = session.Service.Lobby?.players.FirstOrDefault(p => p.connectionId == session.Service.LocalPlayerId);
            if (player != null && !player.isHost) session.SetReady(!player.isReady);
        }

        private void CopyCode()
        {
            var lobby = session.Service.Lobby;
            if (lobby == null) return;
            GUIUtility.systemCopyBuffer = lobby.sessionId;
            session.SetStatus("방 코드를 복사했습니다. 친구에게 전달해주세요.");
        }

        private void Refresh()
        {
            statusText.text = session.Status;
            var lobby = session.Service.Lobby;
            bool connected = lobby != null && session.Service.State == OnlineState.InLobby;
            copyCodeButton.interactable = connected && !session.Busy;
            readyButton.interactable = connected && !session.Busy && !session.Service.IsHost;
            teamOneButton.gameObject.SetActive(connected && lobby.settings.isTeamMode);
            teamTwoButton.gameObject.SetActive(connected && lobby.settings.isTeamMode);
            teamOneButton.interactable = teamTwoButton.interactable = connected && !session.Busy;
            if (!connected)
            {
                roomNameText.text = "대기실 연결 종료";
                readyLabel.text = "연결 종료";
                codeText.text = "—";
                playerCountText.text = "플레이어  0/0";
                modeText.text = mapText.text = "—";
                settingsText.text = "대기실 연결이 종료되었습니다.";
                for (int i = 0; i < playerNameTexts.Length; i++)
                {
                    playerNameTexts[i].text = "—";
                    playerStateTexts[i].text = "연결 종료";
                }
                return;
            }
            roomNameText.text = lobby.roomName;
            codeText.text = lobby.sessionId;
            modeText.text = OnlineLobbyLabels.Mode(lobby.settings.gameMode);
            mapText.text = OnlineLobbyLabels.Map(lobby.settings.mapType);
            var settings = lobby.settings;
            settingsText.text = $"대전 방식     {OnlineLobbyLabels.Composition(settings)}\n\n플레이 인원  {lobby.capacity}명\n\n시작 말 개수 {settings.pieceCountPerPlayer}개\n\n턴 제한       {settings.turnTimeSeconds}초";
            playerCountText.text = $"플레이어  {lobby.players.Length}/{lobby.capacity}";
            for (int i = 0; i < playerNameTexts.Length; i++)
            {
                var player = lobby.players.FirstOrDefault(p => p.slot == i + 1);
                playerNameTexts[i].text = player == null ? (i < lobby.capacity ? "참가 대기 중" : "닫힌 자리") :
                    player.nickname + (player.connectionId == session.Service.LocalPlayerId ? "  (나)" : "");
                playerStateTexts[i].text = player == null ? "—" :
                    (settings.isTeamMode ? $"{player.team}팀 · " : "") +
                    (player.isHost ? "방장" : !player.isRegistered ? "입장 중" : player.isReady ? "준비 완료" : "대기 중");
                playerStateTexts[i].color = player != null && player.isReady ? new Color32(43, 109, 83, 255) : new Color32(108, 112, 113, 255);
            }
            var me = lobby.players.FirstOrDefault(p => p.connectionId == session.Service.LocalPlayerId);
            readyLabel.text = session.Service.IsHost ? (lobby.CanStart ? "모두 준비 완료" : "플레이어 준비 대기") : me != null && me.isReady ? "준비 취소" : "준비";
        }

        private void OnDestroy() { if (session != null) session.Changed -= Refresh; }
    }
}
