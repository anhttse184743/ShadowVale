using System;
using UnityEngine;

namespace ShadowVale.Map01
{
    /// <summary>
    /// What each Map 1 item looks like in the world (lying on the ground, or on a chest's tray), and the
    /// camp chest itself. Filled by ShadowVale > Map 1 > Nhập mô hình vật phẩm from Art/Environment/Map01Items
    /// (built in Blender by SourceArt/Map01_Items/build_map01_items.py) and the game's own rifle, knife and
    /// pebble prefabs. Each model rests with its origin at its bottom centre.
    /// </summary>
    public sealed class Map01ItemCatalog : ScriptableObject
    {
        public const string ResourcePath = "Items/Map01ItemCatalog";

        [Serializable]
        public struct Entry
        {
            public string id;
            public GameObject model;
            [Tooltip("Size it is shown at; 0 means 1. The stone reuses a pebble prefab made for bigger piles.")]
            public float scale;
        }

        public GameObject chest;
        public Entry[] items = Array.Empty<Entry>();

        public float Scale(string id)
        {
            foreach (var entry in items) if (entry.id == id) return entry.scale > 0 ? entry.scale : 1f;
            return 1f;
        }

        public GameObject Model(string id)
        {
            foreach (var entry in items) if (entry.id == id) return entry.model;
            return null;
        }

        private static Map01ItemCatalog loaded;
        public static Map01ItemCatalog Load() => loaded != null ? loaded : loaded = Resources.Load<Map01ItemCatalog>(ResourcePath);
    }
}
