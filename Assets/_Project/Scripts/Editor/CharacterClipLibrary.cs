using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ShadowVale.Editor
{
    /// <summary>
    /// Maps the animation roles the player rig needs onto whatever Mixamo FBX files happen to be
    /// in the Clips folder, by keyword. Matching on keywords rather than exact filenames means a
    /// newly downloaded clip is picked up by re-running the setup, whatever Mixamo called it.
    /// Shared by <see cref="CharacterImportSetup"/> and <see cref="PlayerAnimatorBuilder"/>.
    /// </summary>
    public static class CharacterClipLibrary
    {
        public const string ClipsFolder = "Assets/_Project/Art/Characters/Animations/Clips";

        /// <summary>One animation slot in the player's animator.</summary>
        public readonly struct Role
        {
            public readonly string Name;
            public readonly bool Loop;
            public readonly bool Required;
            public readonly string[] Keywords;

            public Role(string name, bool loop, bool required, params string[] keywords)
            {
                Name = name;
                Loop = loop;
                Required = required;
                Keywords = keywords;
            }
        }

        /// <summary>
        /// Ordered most specific first — a file is claimed by the first role that matches it, so
        /// "Crouched Sneaking Left" goes to Sneak before Walk ever sees it.
        /// </summary>
        public static readonly Role[] Roles =
        {
            new("Sneak", true, false, "sneak", "crouch"),
            new("Die", false, false, "dying", "death", "die", "falling back"),
            new("Punch", false, false, "punch", "jab", "hook", "boxing", "kick"),
            new("Stab", false, false, "stab", "knife", "slash", "sword", "melee"),
            new("Shoot", false, false, "firing", "shoot", "fire", "gunplay"),
            new("AimIdle", true, false, "aiming idle", "rifle aiming", "aim"),
            new("Jump", false, false, "jump"),
            new("Run", true, true, "run", "sprint"),
            new("Walk", true, true, "walk"),
            new("Idle", true, true, "idle"),
        };

        /// <summary>
        /// Scans the Clips folder and assigns each FBX to a role. A file matching nothing is left
        /// out; a role matching nothing is simply absent from the result.
        /// </summary>
        public static Dictionary<string, string> ResolveRoleToPath()
        {
            var result = new Dictionary<string, string>();
            if (!Directory.Exists(ClipsFolder))
            {
                return result;
            }

            var files = new List<string>(Directory.GetFiles(ClipsFolder, "*.fbx"));
            files.Sort(System.StringComparer.OrdinalIgnoreCase); // Stable across machines.

            var claimed = new HashSet<string>();
            foreach (Role role in Roles)
            {
                foreach (string file in files)
                {
                    if (claimed.Contains(file))
                    {
                        continue;
                    }

                    string name = Path.GetFileNameWithoutExtension(file).ToLowerInvariant();
                    if (!MatchesAny(name, role.Keywords))
                    {
                        continue;
                    }

                    claimed.Add(file);
                    result[role.Name] = file.Replace('\\', '/');
                    break;
                }
            }
            return result;
        }

        private static bool MatchesAny(string name, string[] keywords)
        {
            foreach (string keyword in keywords)
            {
                if (name.Contains(keyword))
                {
                    return true;
                }
            }
            return false;
        }

        public static Role FindRole(string name)
        {
            foreach (Role role in Roles)
            {
                if (role.Name == name)
                {
                    return role;
                }
            }
            return default;
        }

        /// <summary>
        /// Whether a role's vertical root travel is baked into the pose rather than left as root
        /// motion. Root motion is off everywhere — the controllers own movement — so anything a
        /// take does to the root's height is otherwise discarded.
        /// <para>
        /// Death needs it: the take lowers the body over its 212 frames, and throwing that away
        /// lays the character down still at standing height, leaving the corpse hovering.
        /// </para>
        /// <para>
        /// Jump and the locomotion takes deliberately do not. CharacterController already lifts
        /// the player, and baking the take's rise on top would raise them twice.
        /// </para>
        /// </summary>
        public static bool BakesRootHeight(string role) => role == "Die";

        /// <summary>Loads the clip a role resolved to, or null when that role has no file.</summary>
        public static AnimationClip LoadClip(Dictionary<string, string> resolved, string role)
        {
            if (!resolved.TryGetValue(role, out string path))
            {
                return null;
            }

            AnimationClip fallback = null;
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is not AnimationClip clip || clip.name.StartsWith("__preview__"))
                {
                    continue;
                }
                if (clip.name == role)
                {
                    return clip; // Renamed during import — the expected case.
                }
                fallback ??= clip;
            }
            return fallback;
        }

        /// <summary>Lists the roles that found no file, for a single actionable log line.</summary>
        public static List<string> MissingRoles(Dictionary<string, string> resolved)
        {
            var missing = new List<string>();
            foreach (Role role in Roles)
            {
                if (!resolved.ContainsKey(role.Name))
                {
                    missing.Add(role.Name);
                }
            }
            return missing;
        }
    }
}
