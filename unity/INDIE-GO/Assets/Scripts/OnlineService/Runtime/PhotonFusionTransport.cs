using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Fusion;
using Fusion.Photon.Realtime;
using Fusion.Sockets;
using UnityEngine;

namespace YutArena.Online
{
    /// <summary>
    /// Fusion 2.1 Host/Client transport. The caller supplies unused NetworkRunners.
    /// Never creates/destroys GameObjects, components, prefabs or scenes.
    /// The caller also owns released runners' eventual destruction.
    /// </summary>
    public sealed class PhotonFusionTransport : IOnlineTransport, INetworkRunnerCallbacks
    {
        private const int Channel = 0x594C4F42; // YLOB
        private const int MaxMessageBytes = 8192;
        private const string ProtocolProperty = "ig.proto";
        private const string NameProperty = "ig.name";
        private static readonly byte[] ConnectionToken = Encoding.UTF8.GetBytes("INDIEGO:LOBBY:1");
        private readonly Func<NetworkRunner> runnerProvider;
        private readonly OnlineOptions options;
        private readonly HashSet<NetworkRunner> usedRunners = new HashSet<NetworkRunner>();
        private readonly Dictionary<int, (long second, int count)> messageRates = new Dictionary<int, (long, int)>();
        private NetworkRunner runner;
        private Task shutdownTask = Task.CompletedTask;
        private int sequence;

        public bool IsHost => runner != null && runner.IsServer;
        public bool IsConnected => runner != null && (runner.IsCloudReady || runner.IsRunning);
        public int LocalPlayerId => runner != null ? runner.LocalPlayer.RawEncoded : 0;
        public IReadOnlyList<int> ConnectedPlayers => runner == null ? Array.Empty<int>() :
            runner.ActivePlayers.Select(p => p.RawEncoded).ToArray();

        public event Action<IReadOnlyList<OnlineRoomInfo>> RoomsReceived;
        public event Action<int> PlayerJoined;
        public event Action<int> PlayerLeft;
        public event Action<int, bool, LobbyMessage> MessageReceived;
        public event Action<string> Disconnected;
        public event Action<NetworkRunner> RunnerReleased;

        public PhotonFusionTransport(Func<NetworkRunner> runnerProvider, OnlineOptions options = null)
        {
            this.runnerProvider = runnerProvider ?? throw new ArgumentNullException(nameof(runnerProvider));
            this.options = (options ?? new OnlineOptions()).Copy();
            this.options.Validate();
        }

        public async Task BrowseAsync(CancellationToken cancellationToken)
        {
            var settings = AppSettings();
            var current = OpenRunner();
            var result = await current.JoinSessionLobby(SessionLobby.ClientServer, options.LobbyName,
                customAppSettings: settings, cancellationToken: cancellationToken);
            CheckResult(current, result);
        }

        public Task HostAsync(string sessionId, string roomName, int capacity, bool visible, CancellationToken cancellationToken)
        {
            LobbyRules.SessionId(sessionId);
            roomName = LobbyRules.RoomName(roomName);
            LobbyRules.Require(capacity >= 2 && capacity <= LobbyRules.MaxPlayers, "방 정원은 2~4명입니다.");
            return StartAsync(new StartGameArgs
            {
                GameMode = Fusion.GameMode.Host, SessionName = sessionId, PlayerCount = capacity,
                CustomLobbyName = options.LobbyName, IsOpen = true, IsVisible = visible,
                EnableClientSessionCreation = false,
                SessionProperties = new Dictionary<string, SessionProperty>
                {
                    [ProtocolProperty] = LobbyMessage.Protocol, [NameProperty] = roomName
                },
                ConnectionToken = ConnectionToken, DisableNATPunchthrough = options.ForceRelay,
                StartGameCancellationToken = cancellationToken
            });
        }

        public Task JoinAsync(string sessionId, CancellationToken cancellationToken)
        {
            LobbyRules.SessionId(sessionId);
            return StartAsync(new StartGameArgs
            {
                GameMode = Fusion.GameMode.Client, SessionName = sessionId,
                CustomLobbyName = options.LobbyName, EnableClientSessionCreation = false,
                ConnectionToken = ConnectionToken, DisableNATPunchthrough = options.ForceRelay,
                StartGameCancellationToken = cancellationToken
            });
        }

        private async Task StartAsync(StartGameArgs args)
        {
            args.CustomPhotonAppSettings = AppSettings();
            var current = OpenRunner();
            current.ProvideInput = false; // Lobby-only; gameplay input is outside this service.
            var result = await current.StartGame(args);
            CheckResult(current, result);
        }

        public Task LeaveAsync()
        {
            if (runner == null) return shutdownTask;
            var current = runner;
            runner = null; // Ignore stale callbacks immediately, including OnShutdown.
            current.RemoveCallbacks(this);
            messageRates.Clear();
            shutdownTask = ShutdownAsync(current);
            return shutdownTask;
        }

        private async Task ShutdownAsync(NetworkRunner current)
        {
            try
            {
                // This service does not own the caller's GameObject.
                await current.Shutdown(destroyGameObject: false);
            }
            finally { Release(current); }
        }

        private void Release(NetworkRunner current)
        {
            if (RunnerReleased == null) return;
            foreach (Action<NetworkRunner> handler in RunnerReleased.GetInvocationList())
                try { handler(current); } catch (Exception e) { Trace.TraceError(e.ToString()); }
        }

        private NetworkRunner OpenRunner()
        {
            if (runner != null || !shutdownTask.IsCompleted)
                throw new OnlineServiceException(OnlineErrorCode.Busy, "기존 Fusion 연결을 먼저 종료해주세요.");
            var supplied = runnerProvider();
            if (supplied == null || supplied.IsRunning || supplied.IsStarting || !usedRunners.Add(supplied))
                throw new OnlineServiceException(OnlineErrorCode.InvalidState, "사용하지 않은 NetworkRunner를 외부에서 전달해주세요. 종료된 Runner는 재사용할 수 없습니다.");
            runner = supplied;
            runner.AddCallbacks(this);
            return runner;
        }

        private FusionAppSettings AppSettings()
        {
            var global = PhotonAppSettings.Global;
            if (global == null || !Guid.TryParse(global.AppSettings.AppIdFusion, out var appId) || appId == Guid.Empty)
                throw new OnlineServiceException(OnlineErrorCode.InvalidRequest, "PhotonAppSettings에 Fusion 2 App ID를 설정해주세요.");
            var settings = global.AppSettings.GetCopy();
            settings.AppVersion = options.AppVersion;
            settings.FixedRegion = options.Region;
            return settings;
        }

        private void CheckResult(NetworkRunner current, StartGameResult result)
        {
            if (runner != current || !result.Ok)
                throw new OnlineServiceException(OnlineErrorCode.ConnectionFailed, $"Photon 접속 실패: {result.ShutdownReason}");
        }

        public void SendToHost(LobbyMessage message)
        {
            if (runner == null || !runner.IsRunning || runner.IsServer)
                throw new OnlineServiceException(OnlineErrorCode.InvalidState, "참가자의 대기실 연결이 필요합니다.");
            runner.SendReliableDataToServer(NextKey(), Encode(message));
        }

        public void SendToPlayer(int playerId, LobbyMessage message)
        {
            if (runner == null || !runner.IsRunning || !runner.IsServer)
                throw new OnlineServiceException(OnlineErrorCode.InvalidState, "방장의 대기실 연결이 필요합니다.");
            runner.SendReliableDataToPlayer(PlayerRef.FromEncoded(playerId), NextKey(), Encode(message));
        }

        private ReliableKey NextKey() => ReliableKey.FromInts(Channel, LobbyMessage.Protocol, unchecked(++sequence), 0);
        private static byte[] Encode(LobbyMessage message)
        {
            LobbyRules.Require(message != null && message.protocol == LobbyMessage.Protocol, "올바른 대기실 메시지가 필요합니다.");
            var data = Encoding.UTF8.GetBytes(JsonUtility.ToJson(message));
            LobbyRules.Require(data.Length <= MaxMessageBytes, "대기실 메시지 크기를 초과했습니다.");
            return data;
        }

        public void OnReliableDataReceived(NetworkRunner current, PlayerRef player, ReliableKey key, ReadOnlySpan<byte> data)
        {
            if (current != runner) return;
            key.GetInts(out int channel, out int protocol, out _, out _);
            if (channel != Channel || protocol != LobbyMessage.Protocol || data.Length == 0 || data.Length > MaxMessageBytes) return;
            if (current.IsServer)
            {
                // Limit requests before JSON decoding and authoritative broadcasts.
                long second = Stopwatch.GetTimestamp() / Stopwatch.Frequency;
                messageRates.TryGetValue(player.RawEncoded, out var rate);
                rate = rate.second == second ? (second, rate.count + 1) : (second, 1);
                messageRates[player.RawEncoded] = rate;
                if (rate.count > 16 || data.Length > 1024) return;
            }
            LobbyMessage message;
            try { message = JsonUtility.FromJson<LobbyMessage>(Encoding.UTF8.GetString(data.ToArray())); }
            catch (ArgumentException) { return; }
            // In Host/Client topology clients receive reliable streams only from the server.
            // The server receives the authenticated originating PlayerRef, including for requests.
            MessageReceived?.Invoke(player.RawEncoded, !current.IsServer, message);
        }

        public void OnSessionListUpdated(NetworkRunner current, List<SessionInfo> sessions)
        {
            if (current != runner) return;
            var list = new List<OnlineRoomInfo>();
            foreach (var session in sessions)
            {
                if (!session.IsVisible || session.MaxPlayers < 2 || session.MaxPlayers > LobbyRules.MaxPlayers ||
                    !Guid.TryParseExact(session.Name, "N", out _) ||
                    !session.Properties.TryGetValue(ProtocolProperty, out var version) || !version.IsInt || (int)version != LobbyMessage.Protocol ||
                    !session.Properties.TryGetValue(NameProperty, out var name) || !name.IsString) continue;
                list.Add(new OnlineRoomInfo(session.Name, (string)name, session.PlayerCount, session.MaxPlayers, session.IsOpen));
            }
            RoomsReceived?.Invoke(list.AsReadOnly());
        }

        public void OnPlayerJoined(NetworkRunner current, PlayerRef player)
        {
            if (current == runner) PlayerJoined?.Invoke(player.RawEncoded);
        }
        public void OnPlayerLeft(NetworkRunner current, PlayerRef player)
        {
            if (current != runner) return;
            messageRates.Remove(player.RawEncoded);
            PlayerLeft?.Invoke(player.RawEncoded);
        }
        public void OnConnectRequest(NetworkRunner current, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)
        {
            // The token is a protocol marker, not account authentication or a password.
            if (current == runner && current.IsServer && current.SessionInfo.IsOpen &&
                token != null && token.SequenceEqual(ConnectionToken)) request.Accept();
            else request.Refuse();
        }
        private void ConnectionEnded(NetworkRunner current, string reason, bool alreadyShutdown = false)
        {
            if (current != runner) return;
            runner = null;
            current.RemoveCallbacks(this);
            messageRates.Clear();
            if (alreadyShutdown) { shutdownTask = Task.CompletedTask; Release(current); }
            else shutdownTask = ShutdownAsync(current);
            Disconnected?.Invoke(reason);
        }
        public void OnShutdown(NetworkRunner current, ShutdownReason reason) => ConnectionEnded(current, $"Photon 연결 종료: {reason}", true);
        public void OnDisconnectedFromServer(NetworkRunner current, NetDisconnectReason reason) => ConnectionEnded(current, $"방장 또는 서버와 연결이 끊겼습니다: {reason}");
        public void OnConnectFailed(NetworkRunner current, NetAddress address, NetConnectFailedReason reason) => ConnectionEnded(current, $"Photon 연결에 실패했습니다: {reason}");
        public void OnHostMigration(NetworkRunner current, HostMigrationToken token) => ConnectionEnded(current, "방장이 퇴장했습니다. 현재 대기실은 방장 이전을 지원하지 않습니다.");

        public void OnConnectedToServer(NetworkRunner current) { }
        public void OnInput(NetworkRunner current, NetworkInput input) { }
        public void OnInputMissing(NetworkRunner current, PlayerRef player, NetworkInput input) { }
        public void OnObjectEnterAOI(NetworkRunner current, NetworkObject obj, PlayerRef player) { }
        public void OnObjectExitAOI(NetworkRunner current, NetworkObject obj, PlayerRef player) { }
        public void OnReliableDataProgress(NetworkRunner current, PlayerRef player, ReliableKey key, float progress) { }
        public void OnSceneLoadDone(NetworkRunner current) { }
        public void OnSceneLoadStart(NetworkRunner current) { }
        public void OnCustomAuthenticationResponse(NetworkRunner current, Dictionary<string, object> data) { }
#pragma warning disable CS0618 // Fusion 2.1 still requires this obsolete callback in its interface.
        public void OnUserSimulationMessage(NetworkRunner current, SimulationMessagePtr message) { }
#pragma warning restore CS0618
    }
}
