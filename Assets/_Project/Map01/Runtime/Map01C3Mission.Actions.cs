using ShadowVale.AI.Perception;
using System;
using System.Collections.Generic;
using System.Linq;
using ShadowVale.AI.Agents;
using ShadowVale.AI.Boss;
using ShadowVale.Gameplay.Combat;
using UnityEngine;
using UnityEngine.AI;

namespace ShadowVale.Map01
{
    /// <summary>
    /// Nam's actions at the post: the kẻng, the phone line and the cache, and the ambush.
    /// </summary>
    public sealed partial class Map01C3Mission
    {
        // ---- Nam's actions ---------------------------------------------------------------------------------------------

        /// <summary>[E] at the kẻng or the telephone pole.</summary>
        public bool Use(ForestPoint point)
        {
            if (point == null || point.used) return false;
            if (point == kiengPoint)
            {
                Current.kiengRemoved = true; point.MarkLooted();
                if (striker != null) striker.SetActive(false);
                mission.Say("Đã tháo dùi kẻng. Nếu báo động, trại không gõ kẻng gọi lính ngủ dậy được nữa.", 5);
                Map01NamActions.For(mission)?.Play(Map01NamActions.PickUp, 1.4f, true);
                StartCoroutine(SpeakAfterInteraction(Map01C3Dialogue.MalletRemoved));
                return true;
            }
            if (point == phonePoint)
            {
                Current.phoneCut = true; point.MarkLooted();
                DropWire();
                mission.Say("Đã cắt dây điện thoại. Chỉ huy không gọi được lính ở các chốt đường bến tới nữa.", 5);
                Map01NamActions.For(mission)?.Play(Map01NamActions.PickUp, 1.6f, true);
                StartCoroutine(SpeakAfterInteraction(Map01C3Dialogue.PhoneCut));
                return true;
            }
            return false;
        }
        private System.Collections.IEnumerator SpeakAfterInteraction(string caption)
        {
            var actions = Map01NamActions.For(mission);
            while (actions != null && actions.Busy) yield return null;
            // Do not cut a story line short, or queue a stale remark after a new cinematic.
            float deadline = Time.time + 5f;
            var voice = mission.GetComponent<DialogueVoice>();
            while (voice != null && voice.HasSpeech && Time.time < deadline)
            {
                if (mission.Stopped || mission.Cinematic) yield break;
                yield return null;
            }
            if (!mission.Stopped && !mission.Cinematic && (voice == null || !voice.HasSpeech))
                mission.Say(caption, 4);
        }
        private void DropWire()
        {
            if (hqWire == null) return;
            var below = poleTop; below.y = At(poleTop.x, poleTop.z + 1.2f).y + .05f;
            Sag(hqWire, poleTop, below + Vector3.forward * 1.2f, .1f);
        }

        /// <summary>Nam behind the commander with the knife, before he has seen him: an ambush can start.</summary>
        public bool CanAmbush()
        {
            var boss = Commander;
            if (boss == null || !boss.Alive || boss.Engaged || Current.ambushed || mission.ModernCombat == null
                || mission.ModernCombat.EquippedKind != WeaponKind.Knife || mission.player == null) return false;
            var delta = Vector3.ProjectOnPlane(mission.player.position - boss.transform.position, Vector3.up);
            float reach = mission.Skills != null ? mission.Skills.TakedownRange(1.6f) : 1.6f;
            if (delta.magnitude > reach || Vector3.Angle(boss.transform.forward, delta) < 120f) return false;
            var from = mission.player.position + Vector3.up;
            return !Physics.Linecast(from, boss.transform.position + Vector3.up, out var hit, mission.ObstructionMask, QueryTriggerInteraction.Ignore)
                || hit.transform == boss.transform || hit.transform.IsChildOf(boss.transform);
        }

        /// <summary>The knife from behind: the commander is no man to die quietly. He takes a heavy wound and breaks free.</summary>
        public bool TryAmbush()
        {
            if (!CanAmbush()) return false;
            var boss = Commander;
            if (!Map01NamActions.For(mission).PlayTakedown(boss.transform)) return false;
            Current.ambushed = true;
            var health = boss.GetComponent<Health>();
            health.TakeDamage(health.Max * Data.ambushDamage, boss.transform.position + Vector3.up * 1.2f, mission.player.gameObject);
            mission.Say("Chỉ huy địch vùng thoát khỏi lưỡi dao — hắn bị thương nặng và lao ra ngoài!", 4);
            return true;
        }

        /// <summary>The run's loot seed came back from a save: an unopened cache holds what this run rolls.</summary>
        public void RerollCache()
        {
            if (cachePoint != null && !cachePoint.used) cachePoint.items = Map01Logistics.For(mission).ContainerLoot(CacheId);
        }
    }
}
