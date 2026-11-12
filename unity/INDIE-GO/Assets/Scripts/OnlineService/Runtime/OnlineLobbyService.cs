using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using YutArena.Common;

namespace YutArena.Online
{
    /// <summary>
    /// Call from Unity's main thread. Construction does not connect or create Unity objects.
    /// Public snapshots are copies. Remote changes are confirmed through LobbyChanged/Error.
    /// </summary>
    public sealed class OnlineLobbyService
    {
        private readonly IOnlineTransport transport;
        private readonly int timeoutSeconds;
        private readonly Queue<(int sender, bool fromHost, LobbyMessage message)> earlyMessages = new Queue<(int, bool, LobbyMessage)>();
        private IReadOnlyList<OnlineRoomInfo> rooms = Array.Empty<OnlineRoomInfo>();
        private OnlineLobbySnapshot snapshot;
        private LobbyAuthority authority;
        private TaskCompletionSource<bool> initialLobby;
        private string sessionId;
        private string connectionLost;
        private bool active;
        private bool busy;
        private bool disposed;

        public OnlineState State { get; private set; } = OnlineState.Offline;
        public bool IsHost => active && transport.IsHost;
        public int LocalPlayerId => active ? transport.LocalPlayerId : 0;
        public IReadOnlyList<OnlineRoomInfo> Rooms => rooms;
        public OnlineLobbySnapshot Lobby => snapshot?.Copy();
        public event Action<OnlineState> StateChanged;
        public event Action<IReadOnlyList<OnlineRoomInfo>> RoomsChanged;
        public event Action<OnlineLobbySnapshot> LobbyChanged;
        public event Action<OnlineServiceException> Error;

        public OnlineLobbyService(IOnlineTransport transport, int connectionTimeoutSeconds = 30)
        {
            this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
            LobbyRules.Require(connectionTimeoutSeconds >= 5 && connectionTimeoutSeconds <= 120, "접속 제한 시간은 5~120초입니다.");
            timeoutSeconds = connectionTimeoutSeconds;
            transport.RoomsReceived += OnRooms;
            transport.PlayerJoined += OnPlayerJoined;
            transport.PlayerLeft += OnPlayerLeft;
            transport.MessageReceived += OnMessage;
            transport.Disconnected += OnDisconnected;
        }

        // Photon directory subscription. RoomsChanged receives complete replacement lists.
        public async Task BrowseRoomsAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            BeginConnection();
            using var timeout = CreateTimeout(cancellationToken);
            try
            {
                await ResetTransportAsync();
                SetState(OnlineState.Connecting);
                await transport.BrowseAsync(timeout.Token);
                CheckConnection();
                timeout.Token.ThrowIfCancellationRequested();
                SetState(OnlineState.BrowsingRooms);
            }
            catch (Exception e) { await FailConnectionAsync(e); throw; }
            finally { busy = false; }
        }

        public async Task CreateRoomAsync(CreateOnlineRoom request, CancellationToken cancellationToken = default)
        {
            LobbyRules.Require(request != null, "방 생성 요청이 필요합니다.");
            string name = LobbyRules.RoomName(request.RoomName);
            string nickname = LobbyRules.Nickname(request.Nickname);
            var settings = LobbyRules.ValidateSettings(request.Settings);
            bool visible = request.IsVisible;
            cancellationToken.ThrowIfCancellationRequested();
            BeginConnection();
            using var timeout = CreateTimeout(cancellationToken);
            try
            {
                await ResetTransportAsync();
                sessionId = Guid.NewGuid().ToString("N");
                SetState(OnlineState.CreatingRoom);
                await transport.HostAsync(sessionId, name, settings.playerCount, visible, timeout.Token);
                CheckConnection();
                timeout.Token.ThrowIfCancellationRequested();
                authority = new LobbyAuthority(sessionId, name, settings, transport.LocalPlayerId, nickname);
                foreach (int id in transport.ConnectedPlayers) authority.AddPlayer(id);
                active = true;
                DrainEarlyMessages();
                Broadcast();
                SetState(OnlineState.InLobby);
            }
            catch (Exception e) { await FailConnectionAsync(e); throw; }
            finally { busy = false; }
        }

        public async Task JoinRoomAsync(string roomSessionId, string nickname, CancellationToken cancellationToken = default)
        {
            roomSessionId = LobbyRules.SessionId(roomSessionId);
            nickname = LobbyRules.Nickname(nickname);
            cancellationToken.ThrowIfCancellationRequested();
            BeginConnection();
            using var timeout = CreateTimeout(cancellationToken);
            try
            {
                await ResetTransportAsync();
                sessionId = roomSessionId;
                initialLobby = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                SetState(OnlineState.JoiningRoom);
                await transport.JoinAsync(sessionId, timeout.Token);
                CheckConnection();
                active = true;
                DrainEarlyMessages();
                transport.SendToHost(new LobbyMessage { kind = LobbyMessageKind.Hello, nickname = nickname });
                // A successful Fusion connection alone is not successful lobby registration.
                using (timeout.Token.Register(() => initialLobby.TrySetCanceled()))
                    await initialLobby.Task;
                CheckConnection();
                timeout.Token.ThrowIfCancellationRequested();
                SetState(OnlineState.InLobby);
            }
            catch (Exception e) { await FailConnectionAsync(e); throw; }
            finally { initialLobby = null; busy = false; }
        }

        public async Task LeaveAsync()
        {
            Begin();
            try
            {
                SetState(OnlineState.Leaving);
                await transport.LeaveAsync();
            }
            finally { Clear(); SetState(OnlineState.Offline); busy = false; }
        }

        public void SetReady(bool ready)
        {
            RequireLobby();
            if (IsHost) { authority.SetReady(LocalPlayerId, ready); Broadcast(); }
            else transport.SendToHost(new LobbyMessage { kind = LobbyMessageKind.Ready, ready = ready });
        }

        public void SetTeam(int team)
        {
            RequireLobby();
            LobbyRules.Require(snapshot.settings.isTeamMode && (team == 1 || team == 2), "팀전에서 1팀 또는 2팀을 선택할 수 있습니다.");
            if (IsHost) { authority.SetTeam(LocalPlayerId, team); Broadcast(); }
            else transport.SendToHost(new LobbyMessage { kind = LobbyMessageKind.Team, team = team });
        }

        public void SetNickname(string nickname)
        {
            RequireLobby();
            nickname = LobbyRules.Nickname(nickname);
            if (IsHost) { authority.Register(LocalPlayerId, nickname); Broadcast(); }
            else transport.SendToHost(new LobbyMessage { kind = LobbyMessageKind.Hello, nickname = nickname });
        }

        public void UpdateRoomSettings(RoomSettingsData settings)
        {
            RequireLobby();
            if (!IsHost) throw new OnlineServiceException(OnlineErrorCode.Rejected, "방장만 방 설정을 변경할 수 있습니다.");
            authority.UpdateSettings(LocalPlayerId, settings);
            Broadcast();
        }

        public void RequestLobbySync()
        {
            RequireLobby();
            if (IsHost) Broadcast();
            else transport.SendToHost(new LobbyMessage { kind = LobbyMessageKind.Sync });
        }

        public async Task DisposeAsync()
        {
            if (disposed) return;
            // An in-flight operation must first be cancelled by its caller and awaited.
            if (busy) throw new OnlineServiceException(OnlineErrorCode.Busy, "진행 중인 접속 작업을 취소하고 기다린 뒤 종료해주세요.");
            try { await LeaveAsync(); }
            finally
            {
                disposed = true;
                transport.RoomsReceived -= OnRooms;
                transport.PlayerJoined -= OnPlayerJoined;
                transport.PlayerLeft -= OnPlayerLeft;
                transport.MessageReceived -= OnMessage;
                transport.Disconnected -= OnDisconnected;
            }
        }

        private void Begin()
        {
            if (disposed) throw new ObjectDisposedException(nameof(OnlineLobbyService));
            if (busy) throw new OnlineServiceException(OnlineErrorCode.Busy, "다른 온라인 작업이 진행 중입니다.");
            busy = true;
        }
        private void BeginConnection()
        {
            if (State == OnlineState.InLobby) throw new OnlineServiceException(OnlineErrorCode.InvalidState, "현재 방에서 먼저 퇴장해주세요.");
            Begin();
        }
        private void RequireLobby()
        {
            if (disposed) throw new ObjectDisposedException(nameof(OnlineLobbyService));
            if (busy || !active || State != OnlineState.InLobby || snapshot == null)
                throw new OnlineServiceException(OnlineErrorCode.InvalidState, "대기실 접속이 완료된 뒤 사용할 수 있습니다.");
        }
        private CancellationTokenSource CreateTimeout(CancellationToken token)
        {
            var source = CancellationTokenSource.CreateLinkedTokenSource(token);
            source.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
            return source;
        }
        private async Task ResetTransportAsync()
        {
            await transport.LeaveAsync();
            Clear();
            connectionLost = null;
        }
        private void CheckConnection()
        {
            if (connectionLost != null || !transport.IsConnected)
                throw new OnlineServiceException(OnlineErrorCode.ConnectionFailed, connectionLost ?? "Photon 접속이 완료되지 않았습니다.");
        }
        private async Task FailConnectionAsync(Exception error)
        {
            // Preserve the original error even if cleanup also fails.
            try { await transport.LeaveAsync(); }
            catch (Exception cleanup) { Trace.TraceError(cleanup.ToString()); }
            Clear();
            SetState(OnlineState.Offline);
            if (!(error is OperationCanceledException))
                Notify(Error, error as OnlineServiceException ?? new OnlineServiceException(OnlineErrorCode.ConnectionFailed, error.Message, error));
        }
        private void Clear()
        {
            active = false; authority = null; snapshot = null; sessionId = null;
            earlyMessages.Clear();
            rooms = Array.AsReadOnly(Array.Empty<OnlineRoomInfo>());
            Notify(RoomsChanged, rooms);
            Notify(LobbyChanged, (OnlineLobbySnapshot)null);
        }
        private void SetState(OnlineState state) { State = state; Notify(StateChanged, state); }
        private void OnRooms(IReadOnlyList<OnlineRoomInfo> list)
        {
            if (State != OnlineState.Connecting && State != OnlineState.BrowsingRooms) return;
            rooms = Array.AsReadOnly(list.OrderBy(r => r.RoomName, StringComparer.Ordinal).ThenBy(r => r.SessionId).ToArray());
            Notify(RoomsChanged, rooms);
        }
        private void OnPlayerJoined(int id)
        {
            if (active && IsHost && authority.AddPlayer(id)) Broadcast();
        }
        private void OnPlayerLeft(int id)
        {
            if (active && IsHost && id != LocalPlayerId && authority.RemovePlayer(id)) Broadcast();
        }
        private void OnDisconnected(string reason)
        {
            if (State == OnlineState.Leaving || disposed) return;
            connectionLost = reason;
            var error = new OnlineServiceException(OnlineErrorCode.Disconnected, reason);
            initialLobby?.TrySetException(error);
            Clear();
            SetState(OnlineState.Offline);
            if (!busy) Notify(Error, error);
        }
        private void DrainEarlyMessages()
        {
            while (earlyMessages.Count > 0)
            {
                var item = earlyMessages.Dequeue();
                OnMessage(item.sender, item.fromHost, item.message);
            }
        }
        private void OnMessage(int sender, bool fromHost, LobbyMessage message)
        {
            if (!active)
            {
                if ((State == OnlineState.CreatingRoom || State == OnlineState.JoiningRoom) && earlyMessages.Count < 32)
                    earlyMessages.Enqueue((sender, fromHost, message));
                return;
            }
            try
            {
                LobbyRules.Require(message != null && message.protocol == LobbyMessage.Protocol, "대기실 통신 버전이 일치하지 않습니다.");
                if (IsHost)
                {
                    if (fromHost || !transport.ConnectedPlayers.Contains(sender)) return;
                    authority.AddPlayer(sender);
                    switch (message.kind)
                    {
                        case LobbyMessageKind.Hello: authority.Register(sender, message.nickname); break;
                        case LobbyMessageKind.Ready: authority.SetReady(sender, message.ready); break;
                        case LobbyMessageKind.Team: authority.SetTeam(sender, message.team); break;
                        case LobbyMessageKind.Sync: break;
                        default: throw new OnlineServiceException(OnlineErrorCode.ProtocolError, "허용되지 않는 대기실 요청입니다.");
                    }
                    Broadcast();
                }
                else if (fromHost)
                {
                    if (message.kind == LobbyMessageKind.Rejection)
                    {
                        var error = new OnlineServiceException(OnlineErrorCode.Rejected, message.error ?? "요청이 거절되었습니다.");
                        if (State == OnlineState.JoiningRoom) initialLobby?.TrySetException(error);
                        else Notify(Error, error);
                    }
                    else if (message.kind == LobbyMessageKind.Snapshot)
                    {
                        LobbyRules.ValidateSnapshot(message.snapshot, sessionId);
                        LobbyRules.Require(message.snapshot.players.Any(p => p.connectionId == LocalPlayerId), "대기실에 자신의 참가 정보가 없습니다.");
                        if (snapshot != null && message.snapshot.revision <= snapshot.revision) return;
                        snapshot = message.snapshot.Copy();
                        Notify(LobbyChanged, snapshot.Copy());
                        if (snapshot.players.Any(p => p.connectionId == LocalPlayerId && p.isRegistered)) initialLobby?.TrySetResult(true);
                    }
                }
            }
            catch (OnlineServiceException error)
            {
                if (IsHost && !fromHost && transport.ConnectedPlayers.Contains(sender))
                    transport.SendToPlayer(sender, new LobbyMessage { kind = LobbyMessageKind.Rejection, error = error.Message });
                else if (!IsHost && fromHost)
                {
                    if (State == OnlineState.JoiningRoom) initialLobby?.TrySetException(error);
                    else Notify(Error, error);
                }
            }
        }
        private void Broadcast()
        {
            snapshot = authority.Snapshot;
            var message = new LobbyMessage { kind = LobbyMessageKind.Snapshot, snapshot = snapshot.Copy() };
            foreach (var p in snapshot.players)
                if (p.connectionId != LocalPlayerId) transport.SendToPlayer(p.connectionId, message);
            Notify(LobbyChanged, snapshot.Copy());
        }

        // UI subscribers cannot interrupt the networking state machine by throwing.
        private static void Notify<T>(Action<T> handlers, T value)
        {
            if (handlers == null) return;
            foreach (Action<T> handler in handlers.GetInvocationList())
                try { handler(value); } catch (Exception error) { Trace.TraceError(error.ToString()); }
        }
    }
}
