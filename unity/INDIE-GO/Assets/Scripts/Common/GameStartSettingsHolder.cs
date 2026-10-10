using UnityEngine;
using UnityEngine.InputSystem;

namespace YutArena.Common
{
    // One shared device plus optional player-specific controllers survive scene changes.
    public static class LocalSelectionInput
    {
        public static bool SharedGamepadSelected { get; private set; }
        private static Gamepad sharedGamepad;
        private static readonly Gamepad[] pads = new Gamepad[8];
        private static readonly int[] joinedFrames = new int[8];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            SharedGamepadSelected = false;
            sharedGamepad = null;
            System.Array.Clear(pads, 0, pads.Length);
            for (int i = 0; i < joinedFrames.Length; i++) joinedFrames[i] = -1;
        }

        public static void SelectSharedKeyboard()
        {
            SharedGamepadSelected = false;
            sharedGamepad = null;
        }

        public static bool SelectSharedGamepad(Gamepad pad)
        {
            if (pad == null || !pad.added) return false;
            for (int i = 0; i < pads.Length; i++)
                if (pads[i] == pad) pads[i] = null;
            sharedGamepad = pad;
            SharedGamepadSelected = true;
            return true;
        }

        public static void SelectSharedGamepadMode()
        {
            SharedGamepadSelected = true;
            if (sharedGamepad != null && sharedGamepad.added) return;
            sharedGamepad = null;
            foreach (Gamepad pad in Gamepad.all)
            {
                if (System.Array.IndexOf(pads, pad) >= 0) continue;
                SelectSharedGamepad(pad);
                return;
            }
            if (Gamepad.all.Count > 0) SelectSharedGamepad(Gamepad.all[0]);
        }

        public static bool SharedInputAvailable => SharedGamepadSelected
            ? sharedGamepad != null && sharedGamepad.added
            : Keyboard.current != null;
        public static bool IsSharedGamepad(Gamepad pad) => SharedGamepadSelected && sharedGamepad == pad;

        public static Gamepad GetInputGamepad(int playerIndex)
        {
            if (!SharedInputAvailable) return null;
            Gamepad assigned = GetGamepad(playerIndex);
            if (assigned != null) return assigned;
            return SharedGamepadSelected && SharedInputAvailable ? sharedGamepad : null;
        }

        public static bool UsesKeyboard(int playerIndex) => SharedInputAvailable &&
            GetGamepad(playerIndex) == null && !SharedGamepadSelected;

        public static bool JoinedThisFrame(int playerIndex) => playerIndex >= 0 &&
            playerIndex < joinedFrames.Length && joinedFrames[playerIndex] == Time.frameCount;

        public static Gamepad GetGamepad(int playerIndex)
        {
            if (playerIndex < 0 || playerIndex >= pads.Length) return null;
            Gamepad pad = pads[playerIndex];
            if (pad != null && !pad.added) pads[playerIndex] = null;
            return pads[playerIndex];
        }

        public static void Poll(int playerCount = 4)
        {
            if (SharedGamepadSelected && sharedGamepad == null)
            {
                foreach (Gamepad pad in Gamepad.all)
                {
                    SelectSharedGamepad(pad);
                    break;
                }
            }
            if (SharedGamepadSelected && sharedGamepad != null && !sharedGamepad.added)
            {
                Gamepad match = null;
                int matches = 0;
                foreach (Gamepad candidate in Gamepad.all)
                {
                    if (System.Array.IndexOf(pads, candidate) >= 0) continue;
                    bool sameDevice = !string.IsNullOrEmpty(sharedGamepad.description.serial)
                        ? candidate.description.serial == sharedGamepad.description.serial
                        : candidate.description.product == sharedGamepad.description.product &&
                          candidate.description.manufacturer == sharedGamepad.description.manufacturer;
                    if (!sameDevice) continue;
                    match = candidate;
                    matches++;
                }
                if (matches == 1) sharedGamepad = match;
            }
            for (int i = 0; i < pads.Length; i++) GetGamepad(i);
            foreach (Gamepad pad in Gamepad.all)
            {
                bool assigned = pad == sharedGamepad || System.Array.IndexOf(pads, pad) >= 0;
                if (!assigned && pad.buttonSouth.wasPressedThisFrame)
                {
                    bool assignedToSlot = false;
                    for (int i = 1; i < Mathf.Min(playerCount, pads.Length); i++)
                        if (pads[i] == null)
                        {
                            pads[i] = pad;
                            joinedFrames[i] = Time.frameCount;
                            assignedToSlot = true;
                            break;
                        }
                    if (!assignedToSlot && pads[0] == null)
                    {
                        pads[0] = pad;
                        joinedFrames[0] = Time.frameCount;
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
