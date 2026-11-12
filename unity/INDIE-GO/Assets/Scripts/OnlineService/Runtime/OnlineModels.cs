using System;
using System.Linq;
using YutArena.Common;

namespace YutArena.Online
{
    public enum OnlineState { Offline, Connecting, BrowsingRooms, CreatingRoom, JoiningRoom, InLobby, Leaving }
    public enum OnlineErrorCode { InvalidRequest, InvalidState, Busy, ConnectionFailed, Disconnected, ProtocolError, Rejected }

    public sealed class OnlineServiceException : Exception
    {
        public OnlineErrorCode Code { get; }
        public OnlineServiceException(OnlineErrorCode code, string message, Exception inner = null)
            : base(message, inner) { Code = code; }
    }

    // The App ID comes from the installed PhotonAppSettings asset, not this object.
    public sealed class OnlineOptions
    {
        public string AppVersion = "indie-go-lobby-v1";
        public string Region = "kr";
        public string LobbyName = "indie-go";
        public int ConnectionTimeoutSeconds = 30;
        public bool ForceRelay;

        internal OnlineOptions Copy() => (OnlineOptions)MemberwiseClone();
        internal void Validate()
        {
            LobbyRules.Require(!string.IsNullOrWhiteSpace(AppVersion) && AppVersion.Length <= 64, "AppVersion이 필요합니다(최대 64자).");
            LobbyRules.Require(!string.IsNullOrWhiteSpace(Region) && Region.Length <= 16, "모든 접속자가 사용할 Photon 리전이 필요합니다.");
            LobbyRules.Require(!string.IsNullOrWhiteSpace(LobbyName) && LobbyName.Length <= 64, "LobbyName이 필요합니다(최대 64자).");
            LobbyRules.Require(ConnectionTimeoutSeconds >= 5 && ConnectionTimeoutSeconds <= 120, "접속 제한 시간은 5~120초입니다.");
        }
    }

    public sealed class CreateOnlineRoom
    {
        public string RoomName;
        public string Nickname;
        public bool IsVisible = true;
        public RoomSettingsData Settings = LobbyRules.DefaultSettings();
    }

    // SessionId is the unique Photon room key. Display names may be duplicated.
    public sealed class OnlineRoomInfo
    {
        public string SessionId { get; }
        public string RoomName { get; }
        public int PlayerCount { get; }
        public int Capacity { get; }
        public bool IsOpen { get; }
        public bool CanJoin => IsOpen && PlayerCount < Capacity;
        public OnlineRoomInfo(string sessionId, string roomName, int playerCount, int capacity, bool isOpen)
        {
            SessionId = sessionId; RoomName = roomName; PlayerCount = playerCount;
            Capacity = capacity; IsOpen = isOpen;
        }
    }

    [Serializable]
    public sealed class OnlineLobbyPlayer
    {
        // Fusion RawEncoded, not the game's 1-based PlayerSlot.
        public int connectionId;
        public int slot;
        public string nickname;
        public bool isHost;
        public bool isRegistered;
        public bool isReady;
        public int team;
        public OnlineLobbyPlayer Copy() => (OnlineLobbyPlayer)MemberwiseClone();
    }

    [Serializable]
    public sealed class OnlineLobbySnapshot
    {
        public string sessionId;
        public string roomName;
        public int revision;
        public int capacity;
        public RoomSettingsData settings;
        public OnlineLobbyPlayer[] players;

        public bool CanStart => settings != null && capacity >= 2 && players != null && players.Length == capacity &&
            players.All(p => p != null && p.isRegistered && (p.isHost || p.isReady)) &&
            (!settings.isTeamMode || (players.Count(p => p.team == 1) == 2 && players.Count(p => p.team == 2) == 2));

        public OnlineLobbySnapshot Copy() => new OnlineLobbySnapshot
        {
            sessionId = sessionId, roomName = roomName, revision = revision, capacity = capacity,
            settings = LobbyRules.CopySettings(settings), players = players.Select(p => p.Copy()).ToArray()
        };

        // Call only after CanStart. This prepares data; it does not load or start a scene.
        public GameStartSettings ToGameStartSettings()
        {
            LobbyRules.Require(CanStart, "모든 인원이 참가하고 준비해야 합니다.");
            return LobbyRules.CopySettings(settings).ToGameStartSettings();
        }
    }

    public enum LobbyMessageKind { Hello = 1, Ready = 2, Team = 3, Sync = 4, Snapshot = 5, Rejection = 6 }

    // Small, versioned lobby messages only. Sender identity always comes from the transport.
    [Serializable]
    public sealed class LobbyMessage
    {
        public const int Protocol = 1;
        public int protocol = Protocol;
        public LobbyMessageKind kind;
        public string nickname;
        public bool ready;
        public int team;
        public OnlineLobbySnapshot snapshot;
        public string error;
    }
}
