using System;
using System.IO;
using System.Linq;
using UnityEngine;
namespace ShadowVale.Map01.Tests
{
    /// <summary>P9 (decided 2026-10-23, done 2026-10-25): Map 1's lines for Nam, Hùng and the commander were re-voiced with
    /// Map 2's VieNeu presets (SourceArt/Dialogue/Map01Map2Voice). The older manifests stay as the record of the earlier
    /// takes; a test reading one of them takes the new resource and length from here.</summary>
    static class Map01Revoice
    {
        [Serializable] public sealed class Line { public string id,resource,cleanResource,voice;public bool radio;public float duration; }
        [Serializable] sealed class Manifest { public Line[] lines; }
        static Line[] lines;
        public static Line Find(string id)
        {
            lines??=JsonUtility.FromJson<Manifest>(File.ReadAllText("SourceArt/Dialogue/Map01Map2Voice/voice-manifest.json")).lines;
            return lines.FirstOrDefault(l=>l.id==id);
        }
    }
}
