using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using YutArena.Common;
using YutArena.Online;

// Behavioral tests use an in-memory transport. The real adapter is compiled against
// the installed SDK. No Unity objects, scenes or live Photon rooms are created.
internal static class Program
{
    private static async Task<int> Main()
    {
        var tests = new (string name, Func<Task> run)[]
        {
            ("2/3/4-player rules and invalid input", Rules),
            ("Construction does not connect", NoAutomaticConnection),
            ("Room lists replace, sort and clear", RoomLists),
            ("Host and client registration synchronize", Registration),
            ("All guests must ready before CanStart", Ready),
            ("Guest settings edits and forged snapshots rejected", Authority),
            ("Ready affects only authenticated sender", SenderIdentity),
            ("Settings changes reset readiness", SettingsReset),
            ("Both team modes preserve custom game mapping", TeamModes),
            ("Team capacity rejects overbooking", TeamCapacity),
            ("Vacant team presets rebalance on join", TeamVacancy),
            ("Departures keep slots stable; rejoin fills vacancy", LeaveAndRejoin),
            ("External copies cannot mutate authority", DefensiveCopies),
            ("Out-of-order snapshots cannot roll back state", RevisionOrdering),
            ("Malformed snapshots cannot replace valid state", InvalidSnapshot),
            ("Unknown protocol and invalid requests rejected", InvalidMessages),
            ("Host loss clears client state", HostLoss),
            ("Connection failures clean up and allow retry", FailureRetry),
            ("Full rooms fail without creating replacement", FullRoom),
            ("Join cancellation removes partially joined player", Cancellation),
            ("Concurrent operations report Busy", ConcurrentOperations),
            ("Join awaits authoritative registration", WaitForRegistration),
            ("Disposal detaches transport events", Disposal),
            ("Subscriber exceptions do not break state", SubscriberExceptions),
            ("Early requests replay after host initialization", EarlyRequests),
            ("Premature readiness cannot register a player", PrematureReady)
        };
        int failed = 0;
        foreach (var t in tests)
            try { await t.run(); Console.WriteLine($"PASS {t.name}"); }
            catch (Exception e) { failed++; Console.WriteLine($"FAIL {t.name}: {e}"); }
        Console.WriteLine($"{tests.Length - failed}/{tests.Length} passed. No Unity objects or live rooms created.");
        return failed == 0 ? 0 : 1;
    }
    private static Task Rules()
    {
        foreach (int n in new[] { 2, 3, 4 }) Check(LobbyRules.ValidateSettings(LobbyRules.DefaultSettings(n)).playerCount == n);
        var bad = LobbyRules.DefaultSettings(); bad.playerCount = 5; Throws(() => LobbyRules.ValidateSettings(bad));
        bad = LobbyRules.DefaultSettings(); bad.isTeamMode = true; Throws(() => LobbyRules.ValidateSettings(bad));
        bad = LobbyRules.DefaultSettings(); bad.turnTimeSeconds = 0; Throws(() => LobbyRules.ValidateSettings(bad));
        Throws(() => LobbyRules.Nickname("\n")); Throws(() => LobbyRules.RoomName(new string('x', 41)));
        Check(!new OnlineLobbySnapshot().CanStart); return Task.CompletedTask;
    }
    private static Task NoAutomaticConnection()
    {
        var t = new Fake(new Hub()); var s = new OnlineLobbyService(t);
        Check(t.Calls == 0 && s.State == OnlineState.Offline && s.Lobby == null); return Task.CompletedTask;
    }
    private static async Task RoomLists()
    {
        var t = new Fake(new Hub()); var s = new OnlineLobbyService(t); await s.BrowseRoomsAsync();
        t.Rooms(new[] { new OnlineRoomInfo("b", "Zulu", 4, 4, true), new OnlineRoomInfo("a", "Alpha", 1, 2, true) });
        Check(s.Rooms.Count == 2 && s.Rooms[0].RoomName == "Alpha" && !s.Rooms[1].CanJoin);
        t.Rooms(Array.Empty<OnlineRoomInfo>()); Check(s.Rooms.Count == 0);
        await s.LeaveAsync(); Check(s.State == OnlineState.Offline);
    }
    private static async Task Registration()
    {
        var (h, host) = await Host(); var g = await Join(h, "손님");
        Check(host.IsHost && !g.IsHost && host.State == OnlineState.InLobby && g.State == OnlineState.InLobby);
        Check(g.Lobby.players.Single(p => !p.isHost).nickname == "손님");
        Check(host.Lobby.revision == g.Lobby.revision && g.Lobby.players.All(p => p.isRegistered));
        Check(h.Id.Length == 32 && host.Lobby.capacity == 4);
    }
    private static async Task Ready()
    {
        var (h, host) = await Host(); var a = await Join(h); var b = await Join(h); var c = await Join(h);
        a.SetReady(true); b.SetReady(true); Check(!host.Lobby.CanStart);
        c.SetReady(true); Check(host.Lobby.CanStart && a.Lobby.CanStart);
        Check(host.Lobby.ToGameStartSettings().playerCount == 4);
        c.SetReady(false); Check(!host.Lobby.CanStart);
    }
    private static async Task Authority()
    {
        var (h, host) = await Host(); var g = await Join(h);
        Throws(() => g.UpdateRoomSettings(LobbyRules.DefaultSettings()), OnlineErrorCode.Rejected);
        int revision = host.Lobby.revision;
        h.Guests[0].SendToHost(new LobbyMessage { kind = LobbyMessageKind.Snapshot, snapshot = g.Lobby });
        Check(host.Lobby.revision == revision);
        var forged = g.Lobby; forged.revision += 100;
        h.Guests[0].Receive(900, false, new LobbyMessage { kind = LobbyMessageKind.Snapshot, snapshot = forged });
        Check(g.Lobby.revision == revision);
    }
    private static async Task SenderIdentity()
    {
        var (h, host) = await Host(); var a = await Join(h); var b = await Join(h); a.SetReady(true);
        Check(host.Lobby.players.Single(p => p.connectionId == a.LocalPlayerId).isReady);
        Check(!host.Lobby.players.Single(p => p.connectionId == b.LocalPlayerId).isReady);
    }
    private static async Task SettingsReset()
    {
        var (h, host) = await Host(2); var g = await Join(h); g.SetReady(true); Check(host.Lobby.CanStart);
        var next = host.Lobby.settings; next.mapType = (int)MapType.Grassland; host.UpdateRoomSettings(next);
        Check(!host.Lobby.CanStart && g.Lobby.settings.mapType == (int)MapType.Grassland);
        next.playerCount = 3; Throws(() => host.UpdateRoomSettings(next));
    }
    private static async Task TeamModes()
    {
        foreach (var mode in new[] { TeamMode.TeamMode1, TeamMode.TeamMode2 })
        {
            var (h, host) = await Host(4, true); var a = await Join(h); var b = await Join(h); var c = await Join(h);
            var next = host.Lobby.settings; next.teamMode = mode; next.playerTeams = new[] { 1, 2, 1, 2 };
            host.UpdateRoomSettings(next); a.SetReady(true); b.SetReady(true); c.SetReady(true);
            var game = host.Lobby.ToGameStartSettings();
            Check(MatchCompositionRule.GetPersonTeamSlot(game, PlayerSlot.Player2) == TeamSlot.TeamB);
            Check(MatchCompositionRule.GetControlledCharacter(game, PlayerSlot.Player3) ==
                (mode == TeamMode.TeamMode1 ? PlayerSlot.Player1 : PlayerSlot.Player3));
            Check(MatchCompositionRule.GetCharacterCount(game) == (mode == TeamMode.TeamMode1 ? 2 : 4));
        }
    }
    private static async Task TeamCapacity()
    {
        var (h, host) = await Host(4, true); await Join(h); var b = await Join(h); await Join(h);
        int errors = 0; b.Error += _ => errors++; b.SetTeam(1);
        Check(errors == 1 && host.Lobby.players.Single(p => p.connectionId == b.LocalPlayerId).team == 2);
    }
    private static async Task TeamVacancy()
    {
        var (h, host) = await Host(4, true); var a = await Join(h); a.SetTeam(2);
        var b = await Join(h); var c = await Join(h);
        Check(host.Lobby.players.Count(p => p.team == 1) == 2 && host.Lobby.players.Count(p => p.team == 2) == 2);
        a.SetReady(true); b.SetReady(true); c.SetReady(true); Check(host.Lobby.CanStart);
    }
    private static async Task LeaveAndRejoin()
    {
        var (h, host) = await Host(); var a = await Join(h); var b = await Join(h); var c = await Join(h);
        int oldId = a.LocalPlayerId; a.SetReady(true); b.SetReady(true); c.SetReady(true); await a.LeaveAsync();
        Check(b.Lobby.players.Single(p => p.connectionId == b.LocalPlayerId).slot == 3 && !b.Lobby.CanStart);
        Check(!b.Lobby.players.Single(p => p.connectionId == b.LocalPlayerId).isReady);
        var replacement = await Join(h);
        Check(replacement.LocalPlayerId != oldId && replacement.Lobby.players.Single(p => p.connectionId == replacement.LocalPlayerId).slot == 2);
    }
    private static async Task DefensiveCopies()
    {
        var (h, host) = await Host(4, true); var g = await Join(h);
        var external = host.Lobby; external.players[0].nickname = "overwrite"; external.settings.playerTeams[0] = 2;
        Check(host.Lobby.players[0].nickname == "방장" && g.Lobby.players[0].nickname == "방장" && host.Lobby.settings.playerTeams[0] == 1);
        var input = host.Lobby.settings; host.UpdateRoomSettings(input); input.playerTeams[0] = 2;
        Check(host.Lobby.settings.playerTeams[0] == 1);
    }
    private static async Task RevisionOrdering()
    {
        var (h, host) = await Host(); var g = await Join(h); var old = g.Lobby; g.SetReady(true); int revision = g.Lobby.revision;
        h.Guests[0].Receive(1, true, new LobbyMessage { kind = LobbyMessageKind.Snapshot, snapshot = old }); Check(g.Lobby.revision == revision);
    }
    private static async Task InvalidSnapshot()
    {
        var (h, host) = await Host(); var g = await Join(h); int errors = 0; g.Error += _ => errors++;
        var bad = g.Lobby; bad.revision++; bad.players[1].slot = bad.players[0].slot;
        h.Guests[0].Receive(1, true, new LobbyMessage { kind = LobbyMessageKind.Snapshot, snapshot = bad });
        Check(errors == 1 && g.Lobby.revision == host.Lobby.revision);
        bad = g.Lobby; bad.sessionId = Guid.NewGuid().ToString("N"); bad.revision++;
        h.Guests[0].Receive(1, true, new LobbyMessage { kind = LobbyMessageKind.Snapshot, snapshot = bad });
        Check(errors == 2 && g.Lobby.sessionId == h.Id);
    }
    private static async Task InvalidMessages()
    {
        var (h, host) = await Host(); var g = await Join(h); int errors = 0; g.Error += _ => errors++;
        h.Guests[0].SendToHost(new LobbyMessage { protocol = 99, kind = LobbyMessageKind.Ready, ready = true });
        h.Guests[0].SendToHost(new LobbyMessage { kind = LobbyMessageKind.Hello, nickname = "" });
        Check(errors == 2 && !host.Lobby.players.Single(p => !p.isHost).isReady);
    }
    private static async Task HostLoss()
    {
        var (h, host) = await Host(); var g = await Join(h); int errors = 0; g.Error += _ => errors++; await host.LeaveAsync();
        Check(g.State == OnlineState.Offline && g.Lobby == null && !g.IsHost && errors == 1);
    }
    private static async Task FailureRetry()
    {
        var t = new Fake(new Hub()) { FailNext = true }; var s = new OnlineLobbyService(t);
        await ThrowsAsync(() => s.CreateRoomAsync(Request()), OnlineErrorCode.ConnectionFailed);
        Check(s.State == OnlineState.Offline && s.Lobby == null); await s.CreateRoomAsync(Request()); Check(s.State == OnlineState.InLobby);
        await s.LeaveAsync(); await s.BrowseRoomsAsync(); Check(s.State == OnlineState.BrowsingRooms);
    }
    private static async Task FullRoom()
    {
        var (h, host) = await Host(2); await Join(h); var s = new OnlineLobbyService(new Fake(h));
        await ThrowsAsync(() => s.JoinRoomAsync(h.Id, "추가"), OnlineErrorCode.ConnectionFailed);
        Check(s.State == OnlineState.Offline && host.Lobby.players.Length == 2 && h.HostStarts == 1);
    }
    private static async Task Cancellation()
    {
        var (h, host) = await Host(); var t = new Fake(h) { HoldSnapshots = true }; var s = new OnlineLobbyService(t);
        using var cancel = new CancellationTokenSource(); var pending = s.JoinRoomAsync(h.Id, "취소", cancel.Token);
        Check(!pending.IsCompleted); cancel.Cancel(); await Cancelled(pending);
        Check(s.State == OnlineState.Offline && s.Lobby == null && host.Lobby.players.Length == 1);
    }
    private static async Task ConcurrentOperations()
    {
        var (h, host) = await Host(); var s = new OnlineLobbyService(new Fake(h) { HoldSnapshots = true });
        using var cancel = new CancellationTokenSource(); var pending = s.JoinRoomAsync(h.Id, "대기", cancel.Token);
        await ThrowsAsync(() => s.BrowseRoomsAsync(), OnlineErrorCode.Busy); cancel.Cancel(); await Cancelled(pending);
    }
    private static async Task WaitForRegistration()
    {
        var (h, host) = await Host(); var t = new Fake(h) { HoldSnapshots = true }; var s = new OnlineLobbyService(t);
        var pending = s.JoinRoomAsync(h.Id, "확인"); Check(!pending.IsCompleted && s.State == OnlineState.JoiningRoom);
        t.HoldSnapshots = false; t.Receive(1, true, new LobbyMessage { kind = LobbyMessageKind.Snapshot, snapshot = host.Lobby });
        await pending; Check(s.State == OnlineState.InLobby);
    }
    private static async Task Disposal()
    {
        var t = new Fake(new Hub()); var s = new OnlineLobbyService(t); await s.DisposeAsync(); int events = 0; s.RoomsChanged += _ => events++;
        t.Rooms(new[] { new OnlineRoomInfo("id", "name", 1, 4, true) }); Check(events == 0);
        try { await s.BrowseRoomsAsync(); throw new Exception("Expected disposed"); } catch (ObjectDisposedException) { }
    }
    private static async Task SubscriberExceptions()
    {
        var s = new OnlineLobbyService(new Fake(new Hub())); s.StateChanged += _ => throw new Exception("subscriber");
        s.LobbyChanged += _ => throw new Exception("subscriber"); await s.CreateRoomAsync(Request()); Check(s.State == OnlineState.InLobby && s.Lobby != null);
    }
    private static async Task EarlyRequests()
    {
        var h = new Hub(); var t = new Fake(h); var host = new OnlineLobbyService(t);
        t.BeforeHostReturn = () =>
        {
            var early = new Fake(h); early.JoinAsync(h.Id, default).GetAwaiter().GetResult();
            early.SendToHost(new LobbyMessage { kind = LobbyMessageKind.Hello, nickname = "빠른 참가" });
        };
        await host.CreateRoomAsync(Request()); Check(host.Lobby.players.Length == 2 && host.Lobby.players[1].isRegistered && host.Lobby.players[1].nickname == "빠른 참가");
    }
    private static async Task PrematureReady()
    {
        var (h, host) = await Host(); var early = new Fake(h); await early.JoinAsync(h.Id, default);
        early.SendToHost(new LobbyMessage { kind = LobbyMessageKind.Ready, ready = true });
        Check(!host.Lobby.players.Single(p => !p.isHost).isReady && !host.Lobby.players.Single(p => !p.isHost).isRegistered);
    }
    private static CreateOnlineRoom Request(int count = 4, bool team = false) => new CreateOnlineRoom
    { RoomName = "테스트 방", Nickname = "방장", Settings = LobbyRules.DefaultSettings(count, team) };
    private static async Task<(Hub, OnlineLobbyService)> Host(int count = 4, bool team = false)
    { var h = new Hub(); var s = new OnlineLobbyService(new Fake(h)); await s.CreateRoomAsync(Request(count, team)); return (h, s); }
    private static async Task<OnlineLobbyService> Join(Hub h, string name = "손님")
    { var s = new OnlineLobbyService(new Fake(h)); await s.JoinRoomAsync(h.Id, name); return s; }
    private static void Check(bool condition) { if (!condition) throw new Exception("Assertion failed"); }
    private static void Throws(Action call, OnlineErrorCode? code = null)
    { try { call(); } catch (OnlineServiceException e) { Check(code == null || e.Code == code); return; } throw new Exception("Expected OnlineServiceException"); }
    private static async Task ThrowsAsync(Func<Task> call, OnlineErrorCode? code = null)
    { try { await call(); } catch (OnlineServiceException e) { Check(code == null || e.Code == code); return; } throw new Exception("Expected OnlineServiceException"); }
    private static async Task Cancelled(Task task)
    { try { await task; } catch (OperationCanceledException) { return; } throw new Exception("Expected cancellation"); }

    private sealed class Hub
    { public Fake Host; public List<Fake> Guests = new List<Fake>(); public string Id; public int Capacity, NextId = 1, HostStarts; }
    private sealed class Fake : IOnlineTransport
    {
        private readonly Hub hub;
        public bool IsHost { get; private set; }
        public bool IsConnected { get; private set; }
        public int LocalPlayerId { get; private set; }
        public bool FailNext, HoldSnapshots;
        public Action BeforeHostReturn;
        public int Calls;
        public IReadOnlyList<int> ConnectedPlayers => hub.Host == null ? Array.Empty<int>() :
            new[] { hub.Host.LocalPlayerId }.Concat(hub.Guests.Select(g => g.LocalPlayerId)).ToArray();
        public event Action<IReadOnlyList<OnlineRoomInfo>> RoomsReceived;
        public event Action<int> PlayerJoined;
        public event Action<int> PlayerLeft;
        public event Action<int, bool, LobbyMessage> MessageReceived;
        public event Action<string> Disconnected;
        public Fake(Hub h) { hub = h; }
        private void Begin(CancellationToken token)
        { Calls++; token.ThrowIfCancellationRequested(); if (FailNext) { FailNext = false; throw new OnlineServiceException(OnlineErrorCode.ConnectionFailed, "Simulated failure"); } }
        public Task BrowseAsync(CancellationToken token) { Begin(token); IsConnected = true; Rooms(Array.Empty<OnlineRoomInfo>()); return Task.CompletedTask; }
        public Task HostAsync(string id, string name, int capacity, bool visible, CancellationToken token)
        {
            Begin(token); hub.Host = this; hub.Id = id; hub.Capacity = capacity; hub.HostStarts++;
            LocalPlayerId = hub.NextId++; IsHost = true; IsConnected = true;
            PlayerJoined?.Invoke(LocalPlayerId); BeforeHostReturn?.Invoke(); return Task.CompletedTask;
        }
        public Task JoinAsync(string id, CancellationToken token)
        {
            Begin(token); if (hub.Host == null || id != hub.Id || hub.Guests.Count + 1 >= hub.Capacity)
                throw new OnlineServiceException(OnlineErrorCode.ConnectionFailed, "Room unavailable");
            LocalPlayerId = hub.NextId++; IsConnected = true; hub.Guests.Add(this);
            hub.Host.PlayerJoined?.Invoke(LocalPlayerId); return Task.CompletedTask;
        }
        public Task LeaveAsync()
        {
            Calls++;
            if (IsConnected)
            {
                IsConnected = false;
                if (IsHost)
                {
                    hub.Host = null;
                    foreach (var g in hub.Guests.ToArray()) { g.IsConnected = false; g.Disconnected?.Invoke("Host disconnected"); }
                    hub.Guests.Clear();
                }
                else if (hub.Guests.Remove(this)) hub.Host?.PlayerLeft?.Invoke(LocalPlayerId);
            }
            IsHost = false; return Task.CompletedTask;
        }
        public void SendToHost(LobbyMessage message) => hub.Host.Receive(LocalPlayerId, false, message);
        public void SendToPlayer(int id, LobbyMessage message) => hub.Guests.SingleOrDefault(g => g.LocalPlayerId == id)?.Receive(LocalPlayerId, true, message);
        public void Rooms(IReadOnlyList<OnlineRoomInfo> list) => RoomsReceived?.Invoke(list);
        public void Receive(int sender, bool host, LobbyMessage message)
        {
            if (HoldSnapshots && message.kind == LobbyMessageKind.Snapshot) return;
            MessageReceived?.Invoke(sender, host, new LobbyMessage
            { protocol = message.protocol, kind = message.kind, nickname = message.nickname, ready = message.ready,
                team = message.team, error = message.error, snapshot = message.snapshot?.Copy() });
        }
    }
}
