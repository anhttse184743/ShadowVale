using ShadowVale.AI.Perception;
using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace ShadowVale.Map01
{
    public sealed partial class Map01Story
    {
        bool TrySpawnResponse()
        {
            if(Progress.radioResponseSpawned||Layout==null)return false;
            var sites=(Layout.responseSites??Array.Empty<Transform>()).Where(t=>t!=null&&Vector3.Distance(t.position,mission.player.position)>=Layout.responseClearance&&
                NavMesh.SamplePosition(t.position,out _,.5f,NavMesh.AllAreas)&&CompletePath(t.position,Layout.radioApproach.position)&&!Visible(t.position)).ToArray();
            if(sites.Length<3)return false;
            for(int i=0;i<3;i++)SpawnResponse(i,sites[i].position,true);
            var leader=mission.Enemies.First(e=>e!=null&&e.name=="operation_radio_response_0");
            mission.Say("Lính phản ứng: Báo động ở trạm đài! Tới kiểm tra ngay!",4);PlayOperationCue("m1_ops_radio_alarm",leader);
            Progress.radioResponseSpawned=true;Checkpoint();return true;
        }
        bool Visible(Vector3 p)
        {
            var v=mission.gameCamera.WorldToViewportPoint(p+Vector3.up);
            if(v.z<=0||v.x<-.08f||v.x>1.08f||v.y<-.08f||v.y>1.08f)return false;
            return !Physics.Linecast(mission.gameCamera.transform.position,p+Vector3.up,mission.ObstructionMask,QueryTriggerInteraction.Ignore);
        }
        static bool CompletePath(Vector3 from,Vector3 to){var path=new NavMeshPath();return NavMesh.CalculatePath(from,to,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete;}
        Map01EnemyController SpawnResponse(int index,Vector3 at,bool investigate)
        {
            string id="operation_radio_response_"+index;
            var existing=mission.Enemies.FirstOrDefault(e=>e!=null&&e.name==id);if(existing!=null)return existing;
            var template=Guards(2).First(e=>e!=null);var instance=Instantiate(template.gameObject,at,Quaternion.identity,Layout.transform);instance.name=id;
            var oldLoot=instance.GetComponent<ForestPoint>();if(oldLoot!=null){mission.UnregisterPoint(oldLoot);Destroy(oldLoot);}
            var enemy=instance.GetComponent<Map01EnemyController>();enemy.carriesSupplyKey=false;enemy.suppressOperationLoot=true;enemy.operationIndex=0;
            enemy.InitializePursuer(mission,at,false);mission.AddEnemy(enemy);
            if(investigate)enemy.Hear(Layout.radio.position,100,NoiseKind.Other);return enemy;
        }
        public void RestoreOperationActors(Map01EnemyController.Snapshot[] states)
        {
            foreach(var data in states??Array.Empty<Map01EnemyController.Snapshot>()){
                if(data.id==null)continue;string name=data.id.Substring(data.id.LastIndexOf('/')+1);
                if(!name.StartsWith("operation_radio_response_",StringComparison.Ordinal))continue;
                if(int.TryParse(name.Substring("operation_radio_response_".Length),out int slot)&&slot>=0&&slot<3)SpawnResponse(slot,data.position,false);
            }
        }
        IEnumerator AnimateSupply(bool delivering)
        {
            var actor=mission.player.GetComponentInChildren<Animator>();var clips=actor.runtimeAnimatorController.animationClips;
            var open=clips.FirstOrDefault(c=>c.name=="Nam_OpenChest");var take=clips.FirstOrDefault(c=>c.name=="Nam_PickUp");
            var idle=UnityEngine.Resources.Load<AnimationClip>("Rescue/Nam_Shelter_Idle");
            var authored=Resources.Load<AnimationClip>("OperationsAnimation/"+(delivering?"Nam_SupplyHandoff":"Nam_SupplyCollect"));
            var stand=delivering?Layout.handoff:Layout.chestApproach;
            var focus=delivering?stand.position+stand.forward*.7f+Vector3.up*.65f:Layout.supply.position+Vector3.up*.55f;
            using(var session=new Map01OperationSession(mission)){
                var path=new NavMeshPath();
                if(stand!=null&&NavMesh.CalculatePath(mission.player.position,stand.position,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete){
                    var walk=clips.FirstOrDefault(c=>c.name=="Nam_Walk_F")??clips.FirstOrDefault(c=>c.name.Contains("Walk")&&!c.name.Contains("Crouch"));
                    float travel=0;
                    foreach(var corner in path.corners){
                        while(Vector3.ProjectOnPlane(mission.player.position-corner,Vector3.up).magnitude>.035f){
                            if(!mission.Paused&&!ForestMenu.Visible){
                                var goal=corner;goal.y=mission.player.position.y;
                                Vector3 next=Map01OperationSession.GroundPoint(Vector3.MoveTowards(mission.player.position,goal,1.25f*Time.deltaTime),mission.player);var direction=Vector3.ProjectOnPlane(next-mission.player.position,Vector3.up);
                                if(direction.sqrMagnitude>.00001f)mission.player.rotation=Quaternion.Slerp(mission.player.rotation,Quaternion.LookRotation(direction),Time.deltaTime*8);
                                mission.player.position=next;travel+=Time.deltaTime;session.Sample(walk,walk!=null?Mathf.Repeat(travel*1.25f/1.8f,walk.length):travel,focus,travel/.6f);
                            }yield return null;
                        }
                    }
                }
                float elapsed=0;bool committed=false,openSound=false,clothSound=false;
                var facing=stand!=null?stand.rotation:mission.player.rotation;var initial=mission.player.rotation;
                if(delivering&&Layout.supplyProps!=null)Layout.supplyProps.HoldBag(actor);
                while(elapsed<6.8f){
                    if(!mission.Paused&&!ForestMenu.Visible){
                        elapsed+=Time.deltaTime;mission.player.rotation=Quaternion.Slerp(initial,facing,Mathf.SmoothStep(0,1,elapsed/.6f));
                        if(elapsed>=1&&!openSound){openSound=true;Map01OperationAudio.Play("chest_open",focus);}
                        if(elapsed>=3.3f&&!clothSound){clothSound=true;Map01OperationAudio.Play("bag_cloth",focus);}
                        AnimationClip clip=elapsed<.5f?idle:elapsed<3?open:take;float t=clip==null?0:elapsed<.5f?elapsed:elapsed<3?(elapsed-.5f)/2.5f*clip.length:(elapsed-3)/3.8f*clip.length;
                        if(authored!=null){clip=authored;t=elapsed;}
                        session.Sample(clip,t,focus,elapsed/.7f);
                        if(Layout.supplyProps!=null){
                            if(!delivering)Layout.supplyProps.OpenLid(Mathf.SmoothStep(0,1,(elapsed-1)/1.5f));
                            if(elapsed>=3.3f&&elapsed<5.4f)Layout.supplyProps.HoldBag(actor);
                        }
                        if(elapsed>=5.4f&&!committed){
                            committed=true;
                            if(delivering)Layout.supplyProps?.PlaceBag(stand.position+stand.forward*.65f);
                            else {Progress.supplies=true;var bag=GetComponent<Map01Inventory>();if(bag.Count(CargoItem)==0)bag.Add(CargoItem,1);quest.SuppliesRecovered();Layout.supplyProps?.CarryBag(actor);}
                        }
                    }yield return null;
                }
            }
        }
    }
}
