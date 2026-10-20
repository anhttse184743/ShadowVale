using System;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace ShadowVale.Map01
{
    /// <summary>A visible reserve guard, separate from the garrison's awake AI and original visual rig.</summary>
    [DefaultExecutionOrder(90)]
    public sealed class Map01C3Sleeper : MonoBehaviour
    {
        public enum Mode { Sleeping, Waking, Awake, Executing, Dead }
        public Mode CurrentMode { get; private set; }
        public bool Sleeping => CurrentMode==Mode.Sleeping;
        public bool BedOwned => CurrentMode!=Mode.Awake;
        public bool Waking => CurrentMode==Mode.Waking;
        public Map01EnemyController Guard { get; private set; }
        public Transform BedFrame { get; private set; }
        public GameObject Visual { get; private set; }
        public Vector3 Approach => BedFrame.TransformPoint(data.approach);
        public Vector3 BodyPoint => BedFrame.TransformPoint(data.sleepingBounds.center);
        public int Index { get; private set; }
        Map01Mission mission;
        Map01C3Mission owner;
        Map01BedAnimationData data;
        Map01CinematicPose pose;
        NavMeshAgent agent;
        Animator originalAnimator;
        bool originalAnimatorEnabled;
        Renderer[] originals;
        bool[] originalVisibility;
        Collider[] colliders;
        bool[] colliderEnabled;
        BoxCollider bedHit;
        float clock;
        bool fight, executed;
        Vector3? search;
        Vector3? wakeThreat;

        public void Bind(Map01Mission m,Map01C3Mission c3,int index,Vector3 floor,Quaternion facing,Map01BedAnimationData asset)
        {
            mission=m;owner=c3;Index=index;data=asset;Guard=GetComponent<Map01EnemyController>();agent=GetComponent<NavMeshAgent>();
            originalAnimator=GetComponentInChildren<Animator>();originalAnimatorEnabled=originalAnimator!=null&&originalAnimator.enabled;
            originals=GetComponentsInChildren<Renderer>(true);originalVisibility=originals.Select(r=>r.forceRenderingOff).ToArray();
            colliders=GetComponentsInChildren<Collider>(true);colliderEnabled=colliders.Select(c=>c.enabled).ToArray();
            BedFrame=new GameObject("C3 bed frame "+(index+1)).transform;BedFrame.SetParent(transform.parent,true);BedFrame.SetPositionAndRotation(floor,facing);
            Visual=Instantiate(data.sleeperPrefab,BedFrame,false);Visual.name="C3 sleeper visual "+(index+1);
            pose=new Map01CinematicPose(Visual.GetComponent<Animator>(),skinGrounding:false);
            var hit=new GameObject("Sleeping guard hit volume");hit.transform.SetParent(transform,false);bedHit=hit.AddComponent<BoxCollider>();
            ResetToSleep();
        }
        void HoldOriginal(bool hold)
        {
            Guard.BedPoseOwned=hold;Guard.enabled=!hold;
            if(originalAnimator!=null)originalAnimator.enabled=!hold&&originalAnimatorEnabled;
            if(agent!=null)agent.enabled=!hold;
            for(int i=0;i<originals.Length;i++)if(originals[i]!=null)originals[i].forceRenderingOff=hold||originalVisibility[i];
            for(int i=0;i<colliders.Length;i++)if(colliders[i]!=null)colliders[i].enabled=!hold&&colliderEnabled[i];
            bedHit.enabled=hold&&Guard.Alive;
            bedHit.transform.SetPositionAndRotation(BedFrame.position,BedFrame.rotation);
            bedHit.center=data.sleepingBounds.center;bedHit.size=Vector3.Max(data.sleepingBounds.size,new Vector3(.35f,.3f,1.1f));
        }
        public void ResetToSleep()
        {
            Guard.ReturnToPost();Guard.gameObject.SetActive(true);mission.RemoveEnemy(Guard);
            transform.SetPositionAndRotation(BedFrame.position,BedFrame.rotation);
            CurrentMode=Mode.Sleeping;clock=Index*.7f;executed=false;search=null;wakeThreat=null;HoldOriginal(true);Visual.SetActive(true);Sample();
        }
        public void Wake(bool attack,bool immediate=false,Vector3? threat=null)
        {
            if(CurrentMode!=Mode.Sleeping&&CurrentMode!=Mode.Waking)return;
            CurrentMode=Mode.Waking;fight=attack;wakeThreat=threat;clock=0;
            if(immediate)FinishWake(true);else Sample();
        }
        public void SearchAfterWake(Vector3 at){search=at;if(CurrentMode==Mode.Awake&&Guard.Alive)Guard.InspectBody(at);}
        void FinishWake(bool restored=false)
        {
            var door=owner.Data.sleepers[Index].door;var target=owner.At(door[0],door[1]);
            var exit=restored?target:BedFrame.TransformPoint(data.wakeExit);
            if(NavMesh.SamplePosition(exit,out var ground,1.1f,NavMesh.AllAreas))exit=ground.position;else exit=target;
            transform.SetPositionAndRotation(exit,BedFrame.rotation);Visual.SetActive(false);CurrentMode=Mode.Awake;
            HoldOriginal(false);if(agent!=null&&agent.isOnNavMesh)agent.Warp(exit);
            Guard.Repost(target,180f,walk:!restored);mission.AddEnemy(Guard);
            if(search.HasValue)Guard.InspectBody(search.Value);
            else if(fight){if(wakeThreat.HasValue)Guard.Alert(wakeThreat.Value);else Guard.InspectBody(target);}
        }
        public void BeginExecution(){CurrentMode=Mode.Executing;clock=0;Visual.SetActive(false);}
        public void CancelExecution(){if(Guard.Alive){CurrentMode=Mode.Sleeping;clock=0;Visual.SetActive(true);Sample();}}
        public void HoldCorpse(bool execution)
        {
            CurrentMode=Mode.Dead;executed=execution;HoldOriginal(true);bedHit.enabled=false;Visual.SetActive(true);mission.AddEnemy(Guard);Sample();
        }
        void Update()
        {
            if(data==null||mission==null)return;
            // Once he has left the bed, the normal guard owns his death and save
            // snapshot. Never pull a combat corpse back into the bunk.
            if(!Guard.Alive&&CurrentMode!=Mode.Awake&&CurrentMode!=Mode.Dead&&CurrentMode!=Mode.Executing){Guard.ResolveSleepingMosinDeath();owner.RecordSleeperDeath(Index,false);HoldCorpse(false);}
            if(mission.Paused||Map01SaveSystem.IsRestoring)return;
            if(CurrentMode==Mode.Sleeping)clock+=Time.deltaTime;
            if(CurrentMode==Mode.Waking){clock+=Time.deltaTime;if(clock>=data.wake.length)FinishWake();}
        }
        void Sample()
        {
            if(CurrentMode==Mode.Sleeping)pose.Sample(data.sleep,clock,true);
            else if(CurrentMode==Mode.Waking)pose.Sample(data.wake,clock);
            else if(CurrentMode==Mode.Dead)pose.Sample(executed?data.execution:data.sleep,executed?data.Duration:0);
            bool held=CurrentMode==Mode.Waking&&clock>=(data.wakePickupFrame-1)/30f||CurrentMode==Mode.Awake;
            foreach(var t in Visual.GetComponentsInChildren<Transform>(true))
                if(t.name=="Dich_AK47"||t.name=="Dich_AK47_BangDan")t.gameObject.SetActive(held);
                else if(t.name=="Sung_DungCot"||t.name=="Sung_DungCot_BangDan")t.gameObject.SetActive(!held&&CurrentMode!=Mode.Dead);
        }
        void LateUpdate(){if(pose!=null&&Visual.activeSelf)Sample();}
        void OnDestroy(){pose?.Dispose();if(BedFrame!=null)Destroy(BedFrame.gameObject);}
    }
}
