using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace ShadowVale.Map01
{
    /// <summary>Prepare first-use data across frames instead of on the interaction button.</summary>
    public sealed class Map01InteractionWarmup:MonoBehaviour
    {
        static Map01InteractionWarmup current;
        readonly Dictionary<string,Object> assets=new();
        readonly Dictionary<Animator,Map01SkinContact> contacts=new();
        public bool BushReady{get;private set;}
        public bool Ready{get;private set;}
        public double DialogueBeginMilliseconds{get;private set;}
        public double BushBeginMilliseconds{get;private set;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]static void ResetCache(){current=null;}
        public static Map01InteractionWarmup For(Map01Mission mission)=>mission.GetComponent<Map01InteractionWarmup>()??mission.gameObject.AddComponent<Map01InteractionWarmup>();
        void Awake(){current=this;}
        IEnumerator Start()
        {
            var mission=GetComponent<Map01Mission>();
            while(!mission.IsInitialized||mission.ModernCombat==null)yield return null;
            foreach(var cue in new[]{"m1_v2_rescue_approach_03","m1_story_rescue_order_01","m1_ops_supply_order_01","m1_ops_handoff_01","m1_ops_radio_report_01","m1_ops_retreat_01"})yield return DialogueVoice.Preload(cue);
            yield return Load<Map01SkinContactData>("Rescue/SkinContactData");
            Contact(mission.player.GetComponentInChildren<Animator>());yield return null;
            var guard=mission.Enemies.FirstOrDefault(e=>e!=null);
            if(guard!=null)Contact(guard.GetComponentInChildren<Animator>());yield return null;
            yield return Load<AnimationClip>("Rescue/Nam_Shelter_Idle");
            foreach(var cue in new[]{"m1_story_rescue_order_01","m1_story_rescue_order_02","m1_story_rescue_order_03","m1_story_rescue_order_04","m1_story_rescue_order_05"})yield return DialogueVoice.Preload(cue);
            foreach(var who in new[]{"Nam","Enemy"})foreach(var take in new[]{"GrabPull","StruggleLoop","Finish","Escape","Recovery"})yield return Load<AnimationClip>("Rescue/Story/"+who+"_Bush_"+take);
            yield return Load<AnimationClip>("Rescue/Story/Nam_Bush_Approach");yield return Load<Avatar>("Rescue/Story/Nam_Bush_Avatar");
            foreach(var side in new[]{"Left","Right"})yield return Load<Mesh>("Rescue/Story/Nam_Hug_"+side+"Hand");
            BushReady=true;
            foreach(var clip in new[]{"Nam_SupplyCollect","Nam_SupplyHandoff","Nam_RadioPanel"})yield return Load<AnimationClip>("OperationsAnimation/"+clip);
            yield return Load<Avatar>("OperationsAnimation/Nam_SupplyCollectAvatar");
            var lines=Map01OperationLines.SupplyOrder.Concat(Map01OperationLines.SupplyReport).Concat(Map01OperationLines.Handoff).Concat(Map01OperationLines.RadioReport).Concat(Map01OperationLines.CommanderReport).Concat(Map01OperationLines.EarlyCommanderReport).Concat(Map01OperationLines.RetreatReport);
            foreach(var line in lines)yield return DialogueVoice.Preload(line.voice);
            foreach(var cue in new[]{"m1_v2_rescue_approach_01","m1_v2_rescue_approach_02","m1_v2_rescue_approach_03","m1_rescue_alarm","m1_rescue_execute_order","m1_rescue_interrogate","m1_story_11","m1_rescue_hung_defiance"})yield return DialogueVoice.Preload(cue);
            yield return Load<Map01SupplyPresentation>("OperationsAnimation/ShelterSupplies/Presentation");
            Map01SupplyPerformance.Warm();yield return null;
            foreach(var clip in new[]{"Hung_Captive_Recoil","Hung_Bound_Execution","Enemy_Interrogate_Strike","Enemy_Interrogate_Walk","Enemy_Interrogation_Punch","Enemy_Interrogation_Punch_Left","Enemy_Interrogation_Walk","Enemy_Interrogation_Idle"})yield return Load<AnimationClip>("Rescue/Story/"+clip);
            Ready=true;
        }
        IEnumerator Load<T>(string path) where T:Object
        {
            var request=Resources.LoadAsync<T>(path);yield return request;
            if(request.asset!=null)assets[path]=request.asset;yield return null;
        }
        public static T Asset<T>(string path) where T:Object=>current!=null&&current.assets.TryGetValue(path,out var value)?value as T:Resources.Load<T>(path);
        internal static Map01SkinContact Contact(Animator actor)
        {
            if(current==null||actor.gameObject.scene!=current.gameObject.scene)return new Map01SkinContact(actor);
            if(!current.contacts.TryGetValue(actor,out var value)){value=new Map01SkinContact(actor);current.contacts[actor]=value;}return value;
        }
        public static void Record(string kind,double milliseconds){if(current==null)return;if(kind=="dialogue")current.DialogueBeginMilliseconds=milliseconds;else current.BushBeginMilliseconds=milliseconds;}
        void OnDestroy(){if(current==this)current=null;assets.Clear();contacts.Clear();}
    }
}
