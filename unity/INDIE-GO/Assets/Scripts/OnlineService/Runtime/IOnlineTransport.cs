using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace YutArena.Online
{
    // Implementations authenticate SenderId/FromHost; message contents never establish authority.
    public interface IOnlineTransport
    {
        bool IsHost { get; }
        bool IsConnected { get; }
        int LocalPlayerId { get; }
        IReadOnlyList<int> ConnectedPlayers { get; }
        event Action<IReadOnlyList<OnlineRoomInfo>> RoomsReceived;
        event Action<int> PlayerJoined;
        event Action<int> PlayerLeft;
        event Action<int, bool, LobbyMessage> MessageReceived;
        event Action<string> Disconnected;

        Task BrowseAsync(CancellationToken cancellationToken);
        Task HostAsync(string sessionId, string roomName, int capacity, bool visible, CancellationToken cancellationToken);
        Task JoinAsync(string sessionId, CancellationToken cancellationToken);
        Task LeaveAsync();
        void SendToHost(LobbyMessage message);
        void SendToPlayer(int playerId, LobbyMessage message);
    }
}
