using System.Linq;
using YutArena.Common;

namespace YutArena.Online
{
    // Host-only state machine. No Unity objects, scene access or network calls.
    public sealed class LobbyAuthority
    {
        private readonly OnlineLobbySnapshot state;
        public OnlineLobbySnapshot Snapshot => state.Copy();

        public LobbyAuthority(string sessionId, string roomName, RoomSettingsData settings, int hostId, string nickname)
        {
            LobbyRules.Require(hostId > 0, "방장 연결 식별자가 필요합니다.");
            var validated = LobbyRules.ValidateSettings(settings);
            state = new OnlineLobbySnapshot
            {
                sessionId = LobbyRules.SessionId(sessionId), roomName = LobbyRules.RoomName(roomName),
                capacity = validated.playerCount, revision = 1, settings = validated,
                players = new[] { new OnlineLobbyPlayer
                {
                    connectionId = hostId, slot = 1, nickname = LobbyRules.Nickname(nickname),
                    isHost = true, isRegistered = true, isReady = true,
                    team = validated.isTeamMode ? validated.playerTeams[0] : 1
                } }
            };
        }

        public bool AddPlayer(int id)
        {
            if (state.players.Any(p => p.connectionId == id)) return false;
            LobbyRules.Require(id > 0 && state.players.Length < state.capacity, "방이 가득 찼거나 잘못된 참가자입니다.");
            int slot = Enumerable.Range(1, state.capacity).First(s => state.players.All(p => p.slot != s));
            int team = slot;
            if (state.settings.isTeamMode)
            {
                team = state.settings.playerTeams[slot - 1];
                if (state.players.Count(p => p.team == team) >= 2) team = team == 1 ? 2 : 1;
                state.settings.playerTeams[slot - 1] = team;
            }
            state.players = state.players.Concat(new[] { new OnlineLobbyPlayer
            {
                connectionId = id, slot = slot, nickname = $"Player {slot}", team = team
            } }).OrderBy(p => p.slot).ToArray();
            InvalidateReady();
            return true;
        }

        public bool RemovePlayer(int id)
        {
            var p = state.players.SingleOrDefault(x => x.connectionId == id);
            if (p == null) return false;
            LobbyRules.Require(!p.isHost, "방장 퇴장은 세션 종료로 처리합니다.");
            state.players = state.players.Where(x => x.connectionId != id).ToArray();
            InvalidateReady();
            return true;
        }

        public void Register(int id, string nickname)
        {
            var p = Find(id);
            p.nickname = LobbyRules.Nickname(nickname);
            p.isRegistered = true;
            state.revision++;
        }

        public void SetReady(int id, bool ready)
        {
            var p = Find(id);
            LobbyRules.Require(p.isRegistered, "대기실 등록이 완료되지 않았습니다.");
            p.isReady = p.isHost || ready;
            state.revision++;
        }

        public void SetTeam(int id, int team)
        {
            var p = Find(id);
            LobbyRules.Require(p.isRegistered && state.settings.isTeamMode && (team == 1 || team == 2), "팀전에서 1팀 또는 2팀을 선택할 수 있습니다.");
            if (p.team == team) return;
            LobbyRules.Require(state.players.Count(x => x.team == team) < 2, "해당 팀이 가득 찼습니다.");
            p.team = team;
            state.settings.playerTeams[p.slot - 1] = team;
            InvalidateReady();
        }

        public void UpdateSettings(int requester, RoomSettingsData settings)
        {
            LobbyRules.Require(Find(requester).isHost, "방장만 방 설정을 변경할 수 있습니다.");
            var next = LobbyRules.ValidateSettings(settings);
            LobbyRules.Require(next.playerCount == state.capacity, "방 정원은 생성 후 변경할 수 없습니다.");
            LobbyRules.Require(!next.isTeamMode ||
                (state.players.Count(p => next.playerTeams[p.slot - 1] == 1) <= 2 &&
                 state.players.Count(p => next.playerTeams[p.slot - 1] == 2) <= 2), "팀 정원은 각각 2명입니다.");
            state.settings = next;
            foreach (var p in state.players) p.team = next.isTeamMode ? next.playerTeams[p.slot - 1] : p.slot;
            InvalidateReady();
        }

        private OnlineLobbyPlayer Find(int id)
        {
            var p = state.players.SingleOrDefault(x => x.connectionId == id);
            LobbyRules.Require(p != null, "참가자가 대기실에 없습니다.");
            return p;
        }
        private void InvalidateReady()
        {
            foreach (var p in state.players) p.isReady = p.isHost;
            state.revision++;
        }
    }
}
