using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;
using ShadowVale.Data.Telemetry;

namespace ShadowVale.Telemetry
{
    /// <summary>
    /// A finished session as the web backend's <c>SessionUpload</c> (PUT /api/game/sessions/{id}, camelCase;
    /// ShadowVale.Contracts/Telemetry/SessionUpload.cs of ShadowVale_web, docs/game-integration.md §4).
    /// Every event and plan row gets its id here, once, so the queued file sends the same ids on every retry
    /// and the backend counts a resend as duplicates. Times are the session start plus game seconds.
    /// </summary>
    public static class SessionUploadMapper
    {
        // Backend limits (GameSessionValidator): over them the whole batch is refused
        public const int MaxEvents = 5000, MaxCoordinationResults = 2000, MaxEncounters = 200, MaxPayloadChars = 4000;

        /// <param name="weaponCode">Turns the game's weapon names (rifle, pistol…) into bundle item codes; null keeps them.</param>
        public static JObject ToUpload(TelemetrySessionDto dto, TelemetrySession session, Func<string, string> weaponCode = null)
        {
            var context = session.Context;
            var start = new DateTimeOffset(DateTime.SpecifyKind(context.StartedAtUtc, DateTimeKind.Utc));
            string At(double seconds) => start.AddSeconds(Math.Max(0, seconds)).ToString("o", CultureInfo.InvariantCulture);
            string Code(string weapon) => weaponCode?.Invoke(weapon) ?? weapon;
            JObject ByWeapon(Dictionary<string, int> counts)
            {
                var o = new JObject();
                foreach (var group in counts.GroupBy(kv => Code(kv.Key)))
                    o[group.Key] = group.Sum(kv => kv.Value);
                return o;
            }

            var encounters = new JArray(dto.Encounters.Take(MaxEncounters).Select(e => (JToken)new JObject
            {
                ["index"] = e.Index,
                ["outcome"] = EncounterOutcome(e.Result),
                ["startedAt"] = At(e.StartT),
                ["endedAt"] = At(e.EndT),
                ["numAgents"] = e.Agents,
                ["numReplans"] = e.Replans,
                ["numFallbacks"] = e.Fallbacks,
                ["coordinationScore"] = e.Replans > 0 ? (JToken)e.CoordinationScore : JValue.CreateNull(),
            }));

            var events = new JArray(dto.Events.Take(MaxEvents).Select(e =>
            {
                var payload = JObject.FromObject(e.Payload ?? new Dictionary<string, object>());
                if (payload.ToString(Newtonsoft.Json.Formatting.None).Length > MaxPayloadChars) payload = new JObject { ["truncated"] = true };
                var row = new JObject
                {
                    ["clientEventId"] = Guid.NewGuid().ToString(),
                    ["eventType"] = e.Type,
                    ["occurredAt"] = At(e.T),
                    ["mapCode"] = e.MapId,
                    ["payload"] = payload,
                };
                // Heatmap position: world x and z in metres
                if (payload["x"]?.Type is JTokenType.Float or JTokenType.Integer && payload["z"]?.Type is JTokenType.Float or JTokenType.Integer)
                {
                    row["posX"] = payload["x"];
                    row["posY"] = payload["z"];
                }
                return (JToken)row;
            }));

            var results = new JArray(session.Plans.Take(MaxCoordinationResults).Select(p => (JToken)new JObject
            {
                ["id"] = p.Id,
                ["variant"] = p.Variant,
                ["usedFallback"] = p.UsedFallback,
                ["taskType"] = p.TaskType,
                ["squadTag"] = p.Group != null && p.Group.Length > 50 ? p.Group.Substring(0, 50) : p.Group,
                ["mapCode"] = context.MapId,
                ["numAgents"] = p.Agents,
                ["numNodes"] = p.Nodes,
                ["objectiveValue"] = p.Objective,
                ["solveLatencyMs"] = Math.Max(0, p.LatencyMs),
                ["withinBudget"] = p.WithinBudget,
                ["assignment"] = JObject.FromObject(p.Assignment),
                ["coordinationScore"] = p.CoordinationScore,
                ["triggeredAt"] = At(p.T),
            }));

            var summary = dto.Summary ?? new TelemetrySummaryDto();
            return new JObject
            {
                ["installId"] = context.InstallId,
                ["source"] = context.Source,
                ["contentVersionId"] = context.ContentVersionId,
                ["mapCode"] = context.MapId,
                ["clientVersion"] = Truncate(context.BuildVersion, 50),
                ["platform"] = Truncate(context.Platform, 20),
                ["startedAt"] = At(0),
                ["endedAt"] = At(dto.DurationSeconds),
                ["outcome"] = Outcome(dto.MissionResult),
                ["stats"] = new JObject
                {
                    ["eventCounts"] = JObject.FromObject(summary.EventCounts ?? new Dictionary<string, int>()),
                    ["shotsByWeapon"] = ByWeapon(session.ShotsByWeapon),
                    ["killsByWeapon"] = ByWeapon(session.KillsByWeapon),
                    ["takedowns"] = session.Takedowns,
                    ["weaponKills"] = session.WeaponKills,
                    // Each fight starts with the player being detected
                    ["timesDetected"] = dto.Encounters.Count,
                    ["eventsDropped"] = summary.EventsDropped + Math.Max(0, dto.Events.Count - MaxEvents),
                    ["encounters"] = encounters,
                },
                ["events"] = events,
                ["coordinationResults"] = results,
            };
        }

        /// <summary>completed | died/failed | crashed → the backend's SessionOutcome; anything else (quit) is Quit.</summary>
        public static string Outcome(string missionResult) => missionResult switch
        {
            "completed" => "Completed",
            "died" or "failed" => "Died",
            "crashed" => "Crashed",
            _ => "Quit",
        };

        public static string EncounterOutcome(string result) => result switch
        {
            "captured" => "PlayerCaptured",
            "escaped" => "PlayerEscaped",
            "cleared" => "SquadEliminated",
            _ => "Aborted",
        };

        private static string Truncate(string value, int max) => value == null || value.Length <= max ? value : value.Substring(0, max);
    }
}
