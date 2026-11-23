using System;
using System.Threading;
using System.Threading.Tasks;
using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;
using YutArena.Common;

namespace YutArena.Online
{
    // UI/application owner. The existing service and transport remain independent of scenes.
    [DefaultExecutionOrder(-1000)]
    public sealed class OnlineSessionController : MonoBehaviour
    {
        public const string RoomListScene = "OnlineRoomListScene";
        public const string WaitingRoomScene = "OnlineWaitingRoomScene";
        [SerializeField] private NetworkRunner runnerPrefab;
        [SerializeField] private string region = "kr";
        public static OnlineSessionController Instance { get; private set; }
        public OnlineLobbyService Service { get; private set; }
        public bool IsConfiguringRoom { get; private set; }
        public bool Busy { get; private set; }
        public string Status { get; private set; } = "온라인 플레이";
        public string Nickname { get; set; } = "플레이어";
        public string RoomName { get; set; } = "함께하는 윷 아레나";
        public event Action Changed;
        private PhotonFusionTransport transport;
        private CancellationTokenSource operation;
        private Task operationTask = Task.CompletedTask;
        private bool leaving;
        private bool quitting;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Nickname = PlayerPrefs.GetString("Online.Nickname", "플레이어");
            transport = new PhotonFusionTransport(AcquireRunner, new OnlineOptions { Region = region });
            transport.RunnerReleased += ReleaseRunner;
            Service = new OnlineLobbyService(transport);
            Service.StateChanged += OnStateChanged;
            Service.LobbyChanged += OnLobbyChanged;
            Service.Error += OnError;
        }

        private NetworkRunner AcquireRunner()
        {
            if (runnerPrefab == null) throw new InvalidOperationException("온라인 Runner 프리팹이 연결되지 않았습니다.");
            var runner = Instantiate(runnerPrefab, transform);
            runner.name = "OnlineLobbyRunner";
            runner.ProvideInput = false;
            return runner;
        }

        private void ReleaseRunner(NetworkRunner runner)
        {
            if (runner != null) Destroy(runner.gameObject);
        }

        public void EnterOnline() => StartOperation(ConnectDirectory, "온라인에 연결하고 있습니다…");

        private async Task ConnectDirectory(CancellationToken token)
        {
            if (Service.State != OnlineState.BrowsingRooms) await Service.BrowseRoomsAsync(token);
            SetStatus("온라인 연결 완료 · 방 만들기 또는 방 참가하기를 선택하세요.");
        }

        public void OpenRoomSettings()
        {
            if (Busy || leaving) return;
            IsConfiguringRoom = true;
            SetStatus("방 조건을 설정한 뒤 설정 완료를 눌러주세요.");
            SceneManager.LoadScene("LocalLobbyScene");
        }

        public void OpenRoomList()
        {
            if (Busy || leaving) return;
            IsConfiguringRoom = false;
            SceneManager.LoadScene(RoomListScene);
        }

        public void RefreshRooms() => StartOperation(async token =>
        {
            await Service.BrowseRoomsAsync(token);
            SetStatus("방 목록을 불러왔습니다. 참가할 방을 선택하세요.");
        }, "방 목록을 불러오고 있습니다…");

        public void CreateRoom(RoomSettingsData source) => StartOperation(async token =>
        {
            Nickname = LobbyRules.Nickname(Nickname);
            RoomName = LobbyRules.RoomName(RoomName);
            var settings = LobbyRules.CopySettings(source);
            // The local Classic mode stores 0 as 'unlimited'; the lobby protocol stores a bounded preset.
            if (settings.gameMode == (int)Common.GameMode.Classic && settings.maxTurnCount == 0)
                settings.maxTurnCount = GameRuleDefine.DefaultMaxTurnCount;
            transport.SetRoomListingDetails(settings);
            await Service.CreateRoomAsync(new CreateOnlineRoom
            {
                RoomName = RoomName, Nickname = Nickname, Settings = settings
            }, token);
            token.ThrowIfCancellationRequested();
            PlayerPrefs.SetString("Online.Nickname", Nickname);
            IsConfiguringRoom = false;
            SetStatus("대기실이 만들어졌습니다. 다른 플레이어를 기다리고 있습니다.");
            SceneManager.LoadScene(WaitingRoomScene);
        }, "방을 만들고 있습니다…");

        public void JoinRoom(string code) => StartOperation(async token =>
        {
            Nickname = LobbyRules.Nickname(Nickname);
            if (!Guid.TryParse(code?.Trim(), out var id))
                throw new ArgumentException("복사한 방 코드를 입력해주세요.");
            try
            {
                await Service.JoinRoomAsync(id.ToString("N"), Nickname, token);
            }
            catch (Exception error) when (!token.IsCancellationRequested)
            {
                // Joining shuts down the directory runner. Restore the live list after failure.
                try { await Service.BrowseRoomsAsync(token); }
                catch (Exception) when (!token.IsCancellationRequested) { }
                throw new InvalidOperationException(FriendlyError(error), error);
            }
            PlayerPrefs.SetString("Online.Nickname", Nickname);
            SetStatus("대기실에 입장했습니다.");
            token.ThrowIfCancellationRequested();
            SceneManager.LoadScene(WaitingRoomScene);
        }, "선택한 방에 참가하고 있습니다…");

        public void SetReady(bool ready) => Send(() => Service.SetReady(ready));
        public void SetTeam(int team) => Send(() => Service.SetTeam(team));

        private void Send(Action send)
        {
            if (Busy || leaving) return;
            try { send(); }
            catch (Exception error) { SetStatus(FriendlyError(error)); }
        }

        public void LeaveToTitle() => LeaveToScene("StartScene");
        public void EnterLocalPlay(string sceneName) => LeaveToScene(sceneName);

        private async void LeaveToScene(string sceneName)
        {
            if (leaving) return;
            leaving = true;
            Busy = true;
            SetStatus("온라인 연결을 종료하고 있습니다…");
            operation?.Cancel();
            try
            {
                await operationTask;
                await Service.LeaveAsync();
            }
            catch (Exception error) { if (!quitting) SetStatus(FriendlyError(error)); }
            finally
            {
                IsConfiguringRoom = false;
                Busy = false;
                leaving = false;
                if (!quitting && this != null) SceneManager.LoadScene(sceneName);
            }
        }

        private void StartOperation(Func<CancellationToken, Task> action, string status)
        {
            if (Busy || leaving || quitting) return;
            Busy = true;
            operation = new CancellationTokenSource();
            SetStatus(status);
            operationTask = RunOperation(action, operation);
        }

        private async Task RunOperation(Func<CancellationToken, Task> action, CancellationTokenSource current)
        {
            try { await action(current.Token); }
            catch (OperationCanceledException) { if (!leaving) SetStatus("접속이 취소되었거나 응답 시간이 초과되었습니다. 다시 시도해주세요."); }
            catch (Exception error) { SetStatus(FriendlyError(error)); }
            finally
            {
                if (operation == current) operation = null;
                current.Dispose();
                if (!leaving) Busy = false;
                Changed?.Invoke();
            }
        }

        private void OnStateChanged(OnlineState state) => Changed?.Invoke();
        private void OnLobbyChanged(OnlineLobbySnapshot lobby) => Changed?.Invoke();
        private void OnError(OnlineServiceException error) => SetStatus(FriendlyError(error));
        public void SetStatus(string value) { Status = value; Changed?.Invoke(); }

        private static string FriendlyError(Exception error)
        {
            if (error is OnlineServiceException serviceError && serviceError.Code == OnlineErrorCode.Disconnected)
                return "온라인 연결이 종료되었습니다. 방장이 퇴장했거나 연결이 끊겼습니다.";
            if (error is OperationCanceledException) return "접속 응답 시간이 초과되었습니다. 다시 시도해주세요.";
            var message = error.Message;
            if (message.Contains("GameIsFull")) return "방이 가득 찼습니다. 다른 방을 선택해주세요.";
            if (message.Contains("GameNotFound")) return "방이 사라졌습니다. 목록을 새로고침해주세요.";
            if (message.Contains("GameClosed")) return "이미 경기가 시작되었거나 입장이 닫힌 방입니다.";
            if (message.Contains("ConnectionRefused")) return "방에 참가할 수 없습니다. 방 상태를 확인해주세요.";
            return message;
        }

        private void OnApplicationQuit() { quitting = true; operation?.Cancel(); }
        private async void OnDestroy()
        {
            if (Instance != this) return;
            Instance = null;
            quitting = true;
            operation?.Cancel();
            if (Service == null) return;
            Service.StateChanged -= OnStateChanged;
            Service.LobbyChanged -= OnLobbyChanged;
            Service.Error -= OnError;
            try { await operationTask; await Service.DisposeAsync(); }
            catch (Exception) { /* Unity may already be shutting down its network objects. */ }
        }
    }
}
