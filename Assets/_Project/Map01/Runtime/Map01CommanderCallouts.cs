using System;
using System.Collections.Generic;
using ShadowVale.AI.Agents;
using ShadowVale.AI.Boss;
using ShadowVale.Core.Events;
using UnityEngine;

namespace ShadowVale.Map01
{
    /// <summary>
    /// The commander shouts his orders, so the player hears what he is doing: men pushing in on a reload, covering
    /// fire as he moves, a grenade, the flank, a hit. Shown as subtitles (no voice recorded yet) when Nam is within
    /// earshot of him, each kind at most every <see cref="KindCooldownSeconds"/>, any at most every
    /// <see cref="AnyCooldownSeconds"/>, so he does not chatter. Listens to the boss events; changes nothing.
    /// </summary>
    public sealed class Map01CommanderCallouts : MonoBehaviour
    {
        public const float HearingRange = 45f, KindCooldownSeconds = 10f, AnyCooldownSeconds = 3.5f, ShowSeconds = 2.6f;
        public const string Speaker = "Chỉ huy địch: ";

        private Map01Mission _mission;
        private string _bossId;
        private readonly Dictionary<string, float> _lastKind = new Dictionary<string, float>();
        private float _lastAny = float.NegativeInfinity;
        private Action<BossOrderEvent> _onOrder;
        private Action<BossRelocatedEvent> _onMove;
        private Action<GrenadeThrownEvent> _onThrow;
        /// <summary>The last line he shouted (tests, debugging).</summary>
        public string LastLine { get; private set; }

        public static Map01CommanderCallouts Attach(Map01Mission mission, Map01EnemyController commander)
        {
            var c = commander.GetComponent<Map01CommanderCallouts>();
            if (c == null) c = commander.gameObject.AddComponent<Map01CommanderCallouts>();
            c._mission = mission;
            c._bossId = ((IEnemyAgent)commander).Id;
            return c;
        }

        /// <summary>What he shouts for an event kind, or null for none.</summary>
        public static string LineFor(string kind) => kind switch
        {
            "push" => "Nó đang thay đạn — xông lên!",
            "cover_fire" => "Yểm trợ tao!",
            "pincer" => "Vòng sườn! Chặn đường lui của nó!",
            "grenade" => "Lựu đạn!",
            "hurt" => "Tao trúng đạn — giữ chân nó!",
            "suppressed" => "Đạn dày quá — đổi chỗ!",
            "aimed" => "Nó đang ngắm tao — đổi chỗ!",
            _ => null,
        };

        private void OnEnable()
        {
            _onOrder = e => Heard(e.BossId, e.Order);
            _onMove = e => Heard(e.BossId, e.Reason);
            _onThrow = e => Heard(e.ThrowerId, "grenade");
            EventBus.Subscribe(_onOrder);
            EventBus.Subscribe(_onMove);
            EventBus.Subscribe(_onThrow);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe(_onOrder);
            EventBus.Unsubscribe(_onMove);
            EventBus.Unsubscribe(_onThrow);
        }

        private void Heard(string bossId, string kind)
        {
            if (_mission == null || bossId != _bossId) return;
            string line = LineFor(kind);
            if (line == null) return;
            float now = Time.time;
            if (now - _lastAny < AnyCooldownSeconds) return;
            if (_lastKind.TryGetValue(kind, out var last) && now - last < KindCooldownSeconds) return;
            if (Vector3.Distance(transform.position, _mission.player.position) > HearingRange) return;
            _lastKind[kind] = now; _lastAny = now;
            LastLine = Speaker + "\"" + line + "\"";
            _mission.Say(LastLine, ShowSeconds);
        }
    }
}
