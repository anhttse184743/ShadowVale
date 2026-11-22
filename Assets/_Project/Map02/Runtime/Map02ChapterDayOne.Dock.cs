using UnityEngine;
namespace ShadowVale.Map01
{
    public sealed partial class Map02ChapterDirector
    {
        // Each press starts one two-handed blow; progress is committed at contact.
        const float StakeStand=.76f;
        bool dockObserverStaged;Vector3 dockObserverOrigin;Quaternion dockObserverRotation;
        float strikeClock=9;Transform twoHandMallet;bool stakeStaged,strikePending,strikeCheckpointPending;
        int strikeStake,stagedStake=-1;float strikeSurfaceY;
        bool StrikeRecovering=>strikeClock<.8f;
        bool DockHammering=>Day.stakeHits<6||StrikeRecovering;
        int VisibleStakeIndex=>StrikeRecovering?strikeStake:Mathf.Min(Day.stakeHits/3,1);
        bool StakeMocap=>namCalmIdle!=null&&dayNamAnimator!=null&&dayNamAnimator.isHuman;
        Transform Stake=>physicalStakes.Count>0?physicalStakes[Mathf.Min(VisibleStakeIndex,physicalStakes.Count-1)]:null;
        void ResetStakeStrike(){strikeClock=9;strikePending=strikeCheckpointPending=false;stagedStake=-1;}
        void StrikeStake(bool good){strikeStake=Mathf.Min(Day.stakeHits/3,1);strikeClock=0;strikePending=good;strikeSurfaceY=Stake!=null?Stake.position.y+1.08f:0;}
        void TickStakeCommit()
        {
            if(dayActivity!=DayActivity.Dock)return;
            if(strikePending&&strikeClock>=.12f){
                strikePending=false;Day.Strike(.5f);dayClock=0;dayAttemptCycle=-1;
                SyncRepairCount();SyncDayProps();PlayDaySound("crate_place",.25f,Stake.position);
                strikeCheckpointPending=Day.stakeHits%3==0;
                dayFeedback=Day.stakeHits==6?"✓ Hai cọc đã chắc. Chờ búa nâng lên rồi chọn ván.":"✓ Cọc xuống thêm một nhịp.";
            }
            if(strikeCheckpointPending&&!StrikeRecovering){strikeCheckpointPending=false;SaveDayProgress();}
        }
        void SelectDockBoard(int card)
        {
            if(card<0||card>=Day.boardOrder.Length)return;
            int board=Day.boardOrder[card];
            if(System.Array.Exists(Day.boards,p=>p.x==board)){dayFeedback="Tấm này đã kê. Chọn tấm chưa dùng.";return;}
            selectedBoard=board;selectionRotated=false;dockOffset=.24f;
            dayFeedback=board==3?"Quan sát vết nứt giữa thân ván; chọn tấm chịu lực tốt hơn.":board==4?"Tấm ván này ngắn hơn khoảng cách hai thanh đỡ.":"Căn hai đầu lên thanh đỡ bằng thanh trượt hoặc A/D; E để kê.";
        }
        // The post is fixed in its bearing rails. Keep the supervisor beside the
        // spare boards, clear of the mallet and the overhead plank workspace.
        void StageDockObserver()
        {
            if(dockObserverStaged)return;
            var bay=roles["Chú Bảy"];dockObserverOrigin=bay.transform.position;dockObserverRotation=bay.transform.rotation;dockObserverStaged=true;
            bay.ReleasePerformance();bay.StopAt(sites["dock"].position+new Vector3(2.5f,0,-.75f));
            bay.transform.rotation=Quaternion.LookRotation(Vector3.ProjectOnPlane(sites["dock"].position+Vector3.forward*1.25f-bay.transform.position,Vector3.up));
        }
        void TickDockObserver()
        {
            if(!dockObserverStaged||dayActivity!=DayActivity.Dock)return;
            var bay=roles["Chú Bảy"];bay.SamplePerformance(namCalmIdle,Time.time,true);bay.GroundPerformance(sites["dock"].position.y);
        }
        void ReleaseDockObserver()
        {
            if(!dockObserverStaged)return;dockObserverStaged=false;
            var bay=roles["Chú Bảy"];bay.ReleasePerformance();bay.StopAt(dockObserverOrigin);bay.transform.rotation=dockObserverRotation;
        }
        void StageStake()
        {
            var stake=Stake;if(stake==null)return;stakeStaged=true;stagedStake=VisibleStakeIndex;var at=new Vector3(stake.position.x,sites["dock"].position.y,stake.position.z);
            var cc=Mission.Player.GetComponent<CharacterController>();if(cc!=null)cc.enabled=false;
            Mission.Player.SetPositionAndRotation(at-Vector3.forward*StakeStand,Quaternion.LookRotation(Vector3.forward));if(cc!=null)cc.enabled=true;
            StageDockObserver();
            if(twoHandMallet==null)
            {
                var wood=dayMaterials.Find(m=>m!=null&&m.name=="Weathered wood");var iron=dayMaterials.Find(m=>m!=null&&m.name=="Iron latch");
                twoHandMallet=Group("Two-handed dock mallet",Vector3.zero);Part(twoHandMallet,"Handle",new Vector3(0,.31f,0),new Vector3(.035f,.62f,.035f),wood);Part(twoHandMallet,"Head",new Vector3(0,.6f,0),new Vector3(.22f,.12f,.12f),wood??iron);
            }
        }
        // The cycle starts and finishes at the same ready pose. Contact follows the
        // actual hammer head (0.49 m from the grip centre), including a sinking post.
        static float MalletSwing(float t)=>t<.12f?Mathf.Pow(Mathf.SmoothStep(0,1,t/.12f),1.6f):t<.23f?1:1-Mathf.SmoothStep(0,1,(t-.23f)/.57f);
        float StakeSurface(float t,float top)=>StrikeRecovering?Mathf.Lerp(strikeSurfaceY,top,Mathf.SmoothStep(0,1,(t-.12f)/.11f)):top;
        void MalletPose(float t,float stakeTop,out Vector3 hands,out Vector3 along)
        {
            float k=MalletSwing(t);var nam=Mission.Player;
            float surface=StakeSurface(t,stakeTop);
            var contact=nam.InverseTransformPoint(new Vector3(Stake.position.x,surface+.06f,Stake.position.z));
            var hitDirection=new Vector3(0,-.32f,1).normalized;
            var ready=new Vector3(0,1.64f,.30f);
            var local=Vector3.Lerp(ready,contact-hitDirection*.49f,k);
            hands=nam.TransformPoint(local);
            along=nam.rotation*Vector3.Slerp(new Vector3(0,.94f,.34f).normalized,hitDirection,k).normalized;
        }
        void HoldMallet(bool left,Vector3 grip,Vector3 along)
        {
            var body=dayNamAnimator;var hand=body.GetBoneTransform(left?HumanBodyBones.LeftHand:HumanBodyBones.RightHand);
            var index=SceneFinger(body,left?HumanBodyBones.LeftIndexProximal:HumanBodyBones.RightIndexProximal);
            var little=SceneFinger(body,left?HumanBodyBones.LeftLittleProximal:HumanBodyBones.RightLittleProximal);
            if(hand==null||index==null||little==null)return;
            var chest=body.GetBoneTransform(HumanBodyBones.Chest).position;
            var hint=chest+Mission.Player.right*(left?-.38f:.38f)+Mission.Player.forward*.20f;
            // Place the palm, rather than the wrist joint, on the wooden handle.
            // Explicit elbow hints keep the two arms apart across retargeted rigs.
            for(int i=0;i<4;i++){
                Map01ArmContact.Reach(body,left,grip-(ScenePalm(body,left)-hand.position),hint,1);
                var lower=body.GetBoneTransform(left?HumanBodyBones.LeftLowerArm:HumanBodyBones.RightLowerArm);
                var side=(index.position-little.position).normalized;
                var target=along*(left?-1:1);
                hand.rotation=Quaternion.FromToRotation(side,target)*hand.rotation;
                var finger=Vector3.ProjectOnPlane(SceneMiddle(body,left).position-hand.position,along);
                var forearm=Vector3.ProjectOnPlane(hand.position-lower.position,along);
                if(finger.sqrMagnitude>.00001f&&forearm.sqrMagnitude>.00001f)
                    hand.rotation=Quaternion.AngleAxis(Vector3.SignedAngle(finger,forearm,along),along)*hand.rotation;
            }
            SceneFingers(body,left,false,1,true);
        }
        void ReleaseStake(){if(!stakeStaged)return;stakeStaged=false;if(twoHandMallet!=null)twoHandMallet.gameObject.SetActive(false);}
        void SampleStake(float dt)
        {
            if(!stakeStaged||stagedStake!=VisibleStakeIndex)StageStake();var stake=Stake;if(stake==null)return;if(!dayHelpVisible)strikeClock+=dt;
            // The older swing clip twists the new rig's left shoulder behind the
            // torso. Start from its gameplay neutral pose and lean toward the work.
            FreeNamPose(namCalmIdle,0,false);
            var spine=dayNamAnimator.GetBoneTransform(HumanBodyBones.Spine);
            float surface=StakeSurface(Mathf.Min(strikeClock,.8f),stake.position.y+1.08f)-1.08f;
            float depth=Mathf.Clamp01((sites["dock"].position.y-surface)/.24f);
            if(spine!=null)spine.rotation=Quaternion.AngleAxis((15+20*depth)*MalletSwing(Mathf.Min(strikeClock,.8f)),Mission.Player.right)*spine.rotation;
            // Both palms follow the handle while the head follows the sinking post.
            MalletPose(Mathf.Min(strikeClock,.8f),stake.position.y+1.08f,out var hands,out var along);
            HoldMallet(false,hands-along*.07f,along);HoldMallet(true,hands+along*.08f,along);
            twoHandMallet.gameObject.SetActive(true);twoHandMallet.SetPositionAndRotation(hands-along*.11f,Quaternion.FromToRotation(Vector3.up,along));

        }
    }
}
