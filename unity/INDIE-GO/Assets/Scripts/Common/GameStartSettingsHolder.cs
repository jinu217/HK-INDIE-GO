using UnityEngine;
using UnityEngine.InputSystem;

namespace YutArena.Common
{
    // Lobby and pick scene share device assignments without changing in-game input.
    public static class LocalSelectionInput
    {
        private static readonly Gamepad[] pads = new Gamepad[8];
        private static readonly int[] joinedFrames = new int[8];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            System.Array.Clear(pads, 0, pads.Length);
            for (int i = 0; i < joinedFrames.Length; i++) joinedFrames[i] = -1;
        }

        public static bool JoinedThisFrame(int playerIndex) => playerIndex > 0 &&
            playerIndex < joinedFrames.Length && joinedFrames[playerIndex] == Time.frameCount;

        public static Gamepad GetGamepad(int playerIndex)
        {
            if (playerIndex <= 0 || playerIndex >= pads.Length) return null;
            Gamepad pad = pads[playerIndex];
            if (pad != null && !pad.added) pads[playerIndex] = null;
            return pads[playerIndex];
        }

        public static void Poll(int playerCount = 4)
        {
            for (int i = 1; i < pads.Length; i++) GetGamepad(i);
            foreach (Gamepad pad in Gamepad.all)
            {
                bool assigned = System.Array.IndexOf(pads, pad) >= 0;
                if (!assigned && pad.buttonSouth.wasPressedThisFrame)
                {
                    for (int i = 1; i < Mathf.Min(playerCount, pads.Length); i++)
                        if (pads[i] == null)
                        {
                            pads[i] = pad;
                            joinedFrames[i] = Time.frameCount;
                            break;
                        }
                }
            }
        }
    }

    public static class GameStartSettingsHolder
    {
        public static GameStartSettings Current { get; set; }
        public static RoomSettingsData CurrentRoomSettings { get; set; }
    }

    public static class LocalPlayerJoinState
    {
        private const int MaxPlayerCount = 4;

        private static readonly bool[] joinedPlayers = new bool[MaxPlayerCount];

        public static int MaxPlayers
        {
            get { return MaxPlayerCount; }
        }

        public static int JoinedPlayerCount
        {
            get
            {
                int count = 0;

                for (int i = 0; i < joinedPlayers.Length; i++)
                {
                    if (joinedPlayers[i])
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public static void SetJoined(int playerIndex, bool joined)
        {
            if (!IsValidPlayerIndex(playerIndex))
            {
                return;
            }

            joinedPlayers[playerIndex] = joined;
        }

        public static bool IsJoined(int playerIndex)
        {
            return IsValidPlayerIndex(playerIndex) && joinedPlayers[playerIndex];
        }

        public static bool[] GetJoinedPlayers()
        {
            bool[] copy = new bool[joinedPlayers.Length];

            for (int i = 0; i < joinedPlayers.Length; i++)
            {
                copy[i] = joinedPlayers[i];
            }

            return copy;
        }

        public static void Clear()
        {
            for (int i = 0; i < joinedPlayers.Length; i++)
            {
                joinedPlayers[i] = false;
            }
        }

        private static bool IsValidPlayerIndex(int playerIndex)
        {
            return playerIndex >= 0 && playerIndex < joinedPlayers.Length;
        }
    }
}
