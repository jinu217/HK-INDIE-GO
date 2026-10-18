using System;
using System.Collections.Generic;
using UnityEngine;

namespace YutArena.Common
{
    [Serializable]
    public class GameModeEntry
    {
        [Min(0)] public int index;
        public Sprite image;
        public string modeName;
        [TextArea(3, 8)] public string description;
        [Tooltip("게임 시작 설정에 전달할 모드")]
        public GameMode gameMode;
    }

    [CreateAssetMenu(fileName = "GameModeDatabase", menuName = "Game Mode/Game Mode Database")]
    public class GameModeDatabase : ScriptableObject
    {
        [SerializeField] private List<GameModeEntry> modes = new List<GameModeEntry>
        {
            new GameModeEntry { index = 0, modeName = "Classic", gameMode = GameMode.Classic },
            new GameModeEntry { index = 1, modeName = "Escape", gameMode = GameMode.Escape },
            new GameModeEntry { index = 2, modeName = "Kill The King", gameMode = GameMode.KillTheKing }
        };

        public GameModeEntry FindByMode(GameMode mode)
        {
            return modes?.Find(entry => entry != null && entry.gameMode == mode);
        }
    }
}
