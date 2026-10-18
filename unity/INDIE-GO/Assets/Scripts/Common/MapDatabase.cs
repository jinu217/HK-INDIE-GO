using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace YutArena.Common
{
    [Serializable]
    public class MapEntry
    {
        [Min(0)] public int index;
        public Sprite image;
        public string mapName;
        [TextArea(3, 8)] public string description;
        [Tooltip("게임 시작 시 전달할 실제 맵 종류")]
        public MapType mapType;
    }

    [CreateAssetMenu(fileName = "MapDatabase", menuName = "Map/Map Database")]
    public class MapDatabase : ScriptableObject
    {
        [SerializeField] private List<MapEntry> maps = new List<MapEntry>
        {
            new MapEntry { index = 0, mapName = "Random", mapType = MapType.Random },
            new MapEntry { index = 1, mapName = "Basic", mapType = MapType.Basic },
            new MapEntry { index = 2, mapName = "Korean", mapType = MapType.Korean },
            new MapEntry { index = 3, mapName = "Grassland", mapType = MapType.Grassland }
        };

        public MapEntry[] GetOrderedMaps()
        {
            return maps == null ? Array.Empty<MapEntry>()
                : maps.Where(map => map != null).OrderBy(map => map.index).ToArray();
        }
    }
}
