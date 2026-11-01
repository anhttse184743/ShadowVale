using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShadowVale.Map01
{
    /// <summary>One character-to-voice contract shared by both maps and the voice build checks.</summary>
    public static class DialogueCasting
    {
        [Serializable] sealed class Role { public string speaker, voice, provider; public string[] aliases; }
        [Serializable] sealed class Cast { public Role[] roles; }
        static Dictionary<string, Role> roles;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() => roles = null;

        static Role For(string speaker)
        {
            if (roles == null)
            {
                roles = new Dictionary<string, Role>(StringComparer.Ordinal);
                var asset = Resources.Load<TextAsset>("Dialogue/voice-cast");
                if (asset == null) { Debug.LogError("Missing shared dialogue voice cast."); return null; }
                var cast = JsonUtility.FromJson<Cast>(asset.text);
                foreach (var role in cast.roles ?? Array.Empty<Role>())
                {
                    roles.Add(role.speaker, role);
                    foreach (var alias in role.aliases ?? Array.Empty<string>()) roles.Add(alias, role);
                }
            }
            return speaker != null && roles.TryGetValue(speaker, out var result) ? result : null;
        }

        public static bool IsAllowed(string speaker, string voice, string provider)
        {
            var role = For(speaker);
            return role != null && role.voice == voice && role.provider == provider;
        }

        public static bool SameCharacter(string first, string second)
        {
            var role = For(first);
            return role != null && role == For(second);
        }
    }
}
