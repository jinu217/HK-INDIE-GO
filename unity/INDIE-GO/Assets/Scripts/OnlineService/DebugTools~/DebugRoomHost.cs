using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Fusion;
using UnityEditor;
using UnityEngine;
using YutArena.Common;

namespace YutArena.Online.DebugTools
{
    // Copied into the isolated host project's Editor folder by Start-DebugRoom.ps1.
    // The enclosing DebugTools~ folder prevents this from loading in the main project.
    [InitializeOnLoad]
    public static class DebugRoomHost
    {
        private const string EnabledKey = "INDIEGO.DebugRoomHost.Enabled";
        private static OnlineLobbyService service;
        private static NetworkRunner hostRunner;
        private static bool started;
        private static bool stopping;
        private static double nextStatus;
        private static Task operation;
        private static Status status = new Status();
        private static string Root => Directory.GetParent(Directory.GetParent(Application.dataPath).FullName).FullName;
        private static string StatusPath => Path.Combine(Root, "room-status.json");

        [Serializable]
        private sealed class Status
        {
            public string phase;
            public string roomName;
            public string sessionId;
            public string region = "kr";
            public string lobby = "indie-go";
            public int processId;
            public int playerCount;
            public int capacity = 4;
            public int frame;
            public string updatedUtc;
            public string error;
            public string[] nicknames;
            public bool directoryVerified;
            public string actualSessionId;
            public bool cloudReady;
        }

        static DebugRoomHost()
        {
            if (SessionState.GetBool(EnabledKey, false)) Subscribe();
        }

        public static void Launch()
        {
            SessionState.SetBool(EnabledKey, true);
            Subscribe();
            status.roomName = File.ReadAllText(Path.Combine(Root, "room-name.txt")).Trim();
            status.phase = "EnteringPlayMode";
            WriteStatus();
            EditorApplication.isPlaying = true;
        }

        private static void Subscribe()
        {
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            if (stopping) return;
            if (File.Exists(Path.Combine(Root, "stop.request")))
            {
                stopping = true;
                operation = Stop();
                return;
            }
            if (!started && Application.isPlaying && Time.frameCount > 0)
            {
                started = true;
                operation = Host();
            }
            if (EditorApplication.timeSinceStartup < nextStatus) return;
            nextStatus = EditorApplication.timeSinceStartup + 2;
            if (started) WriteStatus();
        }

        private static async Task Host()
        {
            try
            {
                Application.runInBackground = true;
                Application.targetFrameRate = 30;
                // Only the isolated debug project is affected by these in-memory settings.
                Fusion.Photon.Realtime.PhotonAppSettings.Global.AppSettings.Protocol = Photon.Client.ConnectionProtocol.WebSocketSecure;
                NetworkProjectConfig.Global.PeerMode = NetworkProjectConfig.PeerModes.Single;
                status.roomName = File.ReadAllText(Path.Combine(Root, "room-name.txt")).Trim();
                status.phase = "Connecting";
                WriteStatus();
                var transport = new PhotonFusionTransport(() =>
                {
                    var root = new GameObject("DebugRoomHostRunner");
                    UnityEngine.Object.DontDestroyOnLoad(root);
                    var runner = root.AddComponent<NetworkRunner>();
                    root.AddComponent<NetworkSceneManagerDefault>();
                    hostRunner = runner;
                    return runner;
                }, new OnlineOptions { Region = "kr", ForceRelay = true });
                transport.RunnerReleased += runner =>
                {
                    if (runner != null) UnityEngine.Object.Destroy(runner.gameObject);
                };
                service = new OnlineLobbyService(transport);
                service.LobbyChanged += _ => WriteStatus();
                service.Error += error => { if (!stopping) Fail(error); };
                var settings = LobbyRules.DefaultSettings(4);
                settings.gameMode = (int)YutArena.Common.GameMode.Classic;
                settings.mapType = (int)MapType.Basic;
                transport.SetRoomListingDetails(settings);
                using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60)))
                    await service.CreateRoomAsync(new CreateOnlineRoom
                    {
                        RoomName = status.roomName, Nickname = "디버그 방장",
                        Settings = settings, IsVisible = true
                    }, timeout.Token);
                status.phase = "Verifying";
                WriteStatus();
                await VerifyDirectory();
                status.phase = "Hosting";
                WriteStatus();
                UnityEngine.Debug.Log("Debug room is hosting: " + service.Lobby.sessionId);
            }
            catch (Exception error) { if (!stopping) Fail(error); }
        }

        private static async Task VerifyDirectory()
        {
            var transport = new PhotonFusionTransport(() =>
            {
                var root = new GameObject("DebugRoomVerificationRunner");
                UnityEngine.Object.DontDestroyOnLoad(root);
                var runner = root.AddComponent<NetworkRunner>();
                root.AddComponent<NetworkSceneManagerDefault>();
                return runner;
            }, new OnlineOptions { Region = "kr", ForceRelay = true });
            transport.RunnerReleased += runner =>
            {
                if (runner != null) UnityEngine.Object.Destroy(runner.gameObject);
            };
            var probe = new OnlineLobbyService(transport);
            try
            {
                string roomId = service.Lobby.sessionId;
                var found = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                probe.RoomsChanged += rooms =>
                {
                    if (rooms.Any(room => room.SessionId == roomId && room.CanJoin)) found.TrySetResult(true);
                };
                using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45)))
                {
                    await probe.BrowseRoomsAsync(timeout.Token);
                    using (timeout.Token.Register(() => found.TrySetCanceled())) await found.Task;
                    status.directoryVerified = true;
                    WriteStatus();
                }
            }
            finally { await probe.DisposeAsync(); }
        }

        private static async Task Stop()
        {
            try
            {
                status.phase = "Stopping";
                WriteStatus();
                if (service != null) await service.DisposeAsync();
                status.phase = "Stopped";
                WriteStatus();
                SessionState.SetBool(EnabledKey, false);
                EditorApplication.Exit(0);
            }
            catch (Exception error) { Fail(error); }
        }

        private static void Fail(Exception error)
        {
            stopping = true;
            status.phase = "Failed";
            status.error = error.Message;
            WriteStatus();
            UnityEngine.Debug.LogException(error);
            SessionState.SetBool(EnabledKey, false);
            EditorApplication.Exit(1);
        }

        private static void WriteStatus()
        {
            status.processId = Process.GetCurrentProcess().Id;
            status.updatedUtc = DateTime.UtcNow.ToString("O");
            status.frame = Time.frameCount;
            if (hostRunner != null)
            {
                status.actualSessionId = hostRunner.SessionInfo?.Name;
                status.cloudReady = hostRunner.IsCloudReady;
            }
            var snapshot = service?.Lobby;
            if (snapshot != null)
            {
                status.sessionId = snapshot.sessionId;
                status.playerCount = snapshot.players.Length;
                status.nicknames = Array.ConvertAll(snapshot.players, p => p.nickname);
            }
            File.WriteAllText(StatusPath, JsonUtility.ToJson(status, true));
        }
    }
}
