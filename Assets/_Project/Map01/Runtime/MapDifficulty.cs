using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ShadowVale.Content;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShadowVale.Map01
{
    public sealed class MapDifficulty
    {
        public const string OriginalPreset = "original";
        public static readonly MapDifficulty Default = new MapDifficulty(100, 100, OriginalPreset);
        public int DamagePercent { get; }
        public int VisionPercent { get; }
        public string PresetId { get; }

        private MapDifficulty(int damage, int vision, string preset)
        {
            DamagePercent = damage;
            VisionPercent = vision;
            PresetId = preset;
        }

        public float Damage(float baseline) => baseline * (DamagePercent / 100f);
        public float Vision(float baseline) => baseline * (VisionPercent / 100f);

        public static MapDifficulty Read(JObject rules)
        {
            if (!(rules?["difficulty"] is JObject difficulty) ||
                difficulty["active_level"]?.Type != JTokenType.String) return Default;
            var level = difficulty.Value<string>("active_level");
            if (level != "easy" && level != "normal" && level != "hard") return Default;
            if (!(difficulty["levels"] is JObject levels) || !(levels[level] is JObject settings)) return Default;
            if (!TryPercent(settings["guard_damage_percent"], out var damagePercent) ||
                !TryPercent(settings["guard_vision_percent"], out var visionPercent)) return Default;
            var preset = settings["layout_preset_id"];
            return new MapDifficulty(damagePercent, visionPercent,
                preset?.Type == JTokenType.String ? preset.Value<string>() : OriginalPreset);
        }

        private static bool TryPercent(JToken token, out int percent)
        {
            percent = 100;
            if (token?.Type != JTokenType.Integer && token?.Type != JTokenType.Float) return false;
            if (!double.TryParse(token.ToString(Formatting.None), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ||
                value < 50 || value > 150 || value != Math.Truncate(value)) return false;
            percent = (int)value;
            return true;
        }

        public static MapDifficulty ForScene(Scene scene, string mapCode)
        {
            if (!ContentService.TryGetPublished(out var content)) return Default;
            var map = content.Maps.FirstOrDefault(entry => entry.Id == mapCode && entry.SceneName == scene.name);
            return map == null ? Default : Read(map.GameRules);
        }

        public int ApplyLayout(Scene scene, string mapCode)
        {
            if (PresetId == OriginalPreset || !scene.IsValid()) return 0;
            var catalog = Resources.Load<TextAsset>("MapLayoutPresets");
            if (catalog == null) return 0;
            var registry = JsonUtility.FromJson<Registry>(catalog.text);
            var map = registry.maps.FirstOrDefault(entry => entry.map_code == mapCode && entry.scene == scene.name);
            var preset = map?.presets.FirstOrDefault(entry => entry.id == PresetId);
            if (preset?.hidden == null || preset.hidden.Length == 0) return 0;

            var names = new HashSet<string>(preset.hidden.Select(entry => entry.name), StringComparer.Ordinal);
            var targets = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Where(target => names.Contains(target.name)).ToLookup(target => target.name);
            var groups = new List<MeshRenderer[]>();
            foreach (var marker in preset.hidden)
            {
                var position = new Vector3(marker.x, marker.y, marker.z);
                var matches = targets[marker.name].Where(target => (target.localPosition - position).sqrMagnitude < .000001f).Take(2).ToArray();
                if (matches.Length != 1 || matches[0].GetComponentInChildren<Collider>(true) != null ||
                    matches[0].GetComponentInChildren<MonoBehaviour>(true) != null)
                    return 0;
                var renderers = matches[0].GetComponentsInChildren<MeshRenderer>(true);
                if (renderers.Length == 0) return 0;
                groups.Add(renderers);
            }
            foreach (var group in groups)
                foreach (var renderer in group)
                    renderer.forceRenderingOff = true;
            return groups.Count;
        }

        [Serializable] private sealed class Registry { public MapPresets[] maps = Array.Empty<MapPresets>(); }
        [Serializable] private sealed class MapPresets
        {
            public string map_code, scene;
            public Preset[] presets = Array.Empty<Preset>();
        }
        [Serializable] private sealed class Preset { public string id; public Marker[] hidden = Array.Empty<Marker>(); }
        [Serializable] private sealed class Marker { public string name; public float x, y, z; }
    }
}
