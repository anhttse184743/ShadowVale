using System;
using System.Linq;
using ShadowVale.Gameplay.Combat;
using ShadowVale.Gameplay.Player;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Animations;
using UnityEngine.InputSystem;
using UnityEngine.Playables;

namespace ShadowVale.Map01
{
    /// <summary>
    /// The story films around Hùng: the intro at the jetty and the execution, with their beats, rounds,
    /// camera lenses and sound.
    /// </summary>
    public sealed partial class Map01RescueCinematic
    {
        DialogueVoice Voice(){
            if(storyVoice==null){var host=new GameObject("Rescue cinematic dialogue");host.transform.SetParent(transform,false);storyVoice=DialogueVoice.For(host);}
            storyVoice.CanSpeak=()=>IsPlaying&&!mission.Paused&&!ForestMenu.Visible;return storyVoice;
        }
        void OwnStoryActors(){
            DialogueVoice.StopAll();storyBeat=0;beatAt=impactAt=0;shot=false;introReturnAt=0;introImpact=introRightImpact=false;introImpactAt=introRightImpactAt=0;leftContactError=rightContactError=-1;finalBurstAt=dyingReleaseAt=-1;executionRounds=0;lastRoundAt=-10;InterrogationContactError=-1;
            storyPlayerEnabled=mission.ModernPlayer.enabled;storyCombatEnabled=mission.ModernCombat.enabled;storySneaking=mission.Crouched;
            var cc=mission.player.GetComponent<CharacterController>();storyCCEnabled=cc.enabled;cc.enabled=false;
            storyActions=Map01NamActions.For(mission);storyActionsEnabled=storyActions.enabled;storyActions.enabled=false;
            mission.ModernPlayer.enabled=mission.ModernCombat.enabled=false;
            captiveVisual=mission.hung.GetComponentInChildren<Map01HungVisual>();captiveVisual.BeginCaptivePresentation();
            var guardActor=enemy.GetComponentInChildren<Animator>();
            executionAim=guardActor.runtimeAnimatorController?.animationClips.FirstOrDefault(c=>c.name=="Enemy_Rifle_AimIdle");
            boundExecution=CurrentMode==Mode.Execution?Map01CinematicPose.Clip("Hung_Bound_Execution"):null;
            captivePose=new Map01CinematicPose(captiveVisual.actor);interrogatorPose=new Map01CinematicPose(guardActor);
            if(CurrentMode==Mode.Intro){
                interrogationPunch=Map01CinematicPose.Clip("Enemy_Interrogation_Punch");
                interrogationLeftPunch=Map01CinematicPose.Clip("Enemy_Interrogation_Punch_Left");
                interrogationWalk=Map01CinematicPose.Clip("Enemy_Interrogation_Walk");
                interrogationIdle=Map01CinematicPose.Clip("Enemy_Interrogation_Idle");
                interrogatorPose.Sample(executionAim,0);
                var standingBones=new[]{HumanBodyBones.Hips,HumanBodyBones.LeftUpperLeg,HumanBodyBones.LeftLowerLeg,HumanBodyBones.LeftFoot,HumanBodyBones.LeftToes,HumanBodyBones.RightUpperLeg,HumanBodyBones.RightLowerLeg,HumanBodyBones.RightFoot,HumanBodyBones.RightToes};
                interrogationStanding=standingBones.Select(guardActor.GetBoneTransform).Where(t=>t!=null).Select(t=>(t,t.localPosition,t.localRotation)).ToArray();
                interrogationSling=new Map01InterrogationSling(guardActor,enemy.transform);
            }
            if(CurrentMode==Mode.Intro || CurrentMode==Mode.Execution){
                introNamPosition=mission.player.position;introNamRotation=mission.player.rotation;
                introRadioAt=introReleaseAt=0;
                var actor=mission.player.GetComponentInChildren<Animator>();
                introNamIdle=ownerIdle();
                introNamPose=new Map01CinematicPose(actor);
                introNamPose.Sample(introNamIdle,0,true);
                var lower=new[]{HumanBodyBones.Hips,HumanBodyBones.LeftUpperLeg,HumanBodyBones.LeftLowerLeg,HumanBodyBones.LeftFoot,HumanBodyBones.LeftToes,
                    HumanBodyBones.RightUpperLeg,HumanBodyBones.RightLowerLeg,HumanBodyBones.RightFoot,HumanBodyBones.RightToes};
                introLowerBody=lower.Select(actor.GetBoneTransform).Where(t=>t!=null).Select(t=>(t,t.localPosition,t.localRotation)).ToArray();
                foreach(var weapon in mission.player.GetComponentsInChildren<Weapon>(true))if(CurrentMode==Mode.Intro&&weapon.gameObject.activeSelf){introHiddenWeapons.Add(weapon.gameObject);weapon.gameObject.SetActive(false);}
            }
            RenderStory();
        }
        AnimationClip ownerIdle()=>rescue.Layout.namIdle??Resources.Load<AnimationClip>("Rescue/Nam_Shelter_Idle");
        public bool ShowIntro(Map01Rescue owner,Map01EnemyController overseer){
            if(IsPlaying||Map01CinematicPose.Clip("Hung_Captive_Recoil")==null||Map01CinematicPose.Clip("Enemy_Interrogation_Punch")==null||Map01CinematicPose.Clip("Enemy_Interrogation_Punch_Left")==null)return false;
            enemy=overseer;Begin(owner,Mode.Intro,24);
            interrogatorStart=enemy.transform.position;
            var direction=Vector3.ProjectOnPlane(interrogatorStart-mission.hung.position,Vector3.up).normalized;
            var desired=mission.hung.position+direction*1.02f;
            canApproach=NavMesh.SamplePosition(desired,out var contact,.18f,NavMesh.AllAreas)&&!NavMesh.Raycast(interrogatorStart,contact.position,out _,NavMesh.AllAreas);
            interrogatorClose=canApproach?contact.position:interrogatorStart;
            interrogatorWalk=Map01CinematicPose.Clip("Enemy_Interrogate_Walk")
                ??enemy.GetComponentInChildren<Animator>().runtimeAnimatorController?.animationClips.FirstOrDefault(c=>c.name=="Enemy_Patrol_Walk")
                ??enemy.GetComponentInChildren<Animator>().runtimeAnimatorController?.animationClips.FirstOrDefault(c=>c.name.Contains("Walk"));
            OwnStoryActors();
            CalibrateInterrogationContact(direction);
            var hungChest=captiveVisual.actor.GetBoneTransform(HumanBodyBones.Chest).position;
            var guardChest=enemy.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.Chest).position;
            introCaptiveFocus=Vector3.Lerp(hungChest,guardChest,.5f);
            var side=Vector3.Cross(Vector3.up,direction);
            float distance=Mathf.Clamp(3+Vector3.Distance(hungChest,guardChest)*.5f,3.6f,7);
            introCaptiveView=FindStoryLens(introCaptiveFocus,side,distance*1.45f,.55f);
            bool found=false;
            foreach(float scale in new[]{1f,1.2f,1.45f}){
                foreach(float turn in new[]{0f,25f,-25f,50f,-50f,180f,90f,-90f}){
                    var candidate=introCaptiveFocus+Quaternion.Euler(0,turn,0)*side*distance*scale+Vector3.up*.55f;
                    if(IntroFrameContains(candidate,hungChest)&&IntroFrameContains(candidate,guardChest)
                        &&ShelterCameraClear(hungChest,candidate)&&ShelterCameraClear(guardChest,candidate)
                        &&!Physics.CheckSphere(candidate,.16f,mission.ObstructionMask,QueryTriggerInteraction.Ignore)){
                        introCaptiveView=candidate;found=true;break;
                    }
                }
                if(found)break;
            }
            owner.MarkIntroSeen();return true;
        }
        bool IntroFrameContains(Vector3 lens,Vector3 point){
            var rotation=Quaternion.LookRotation(introCaptiveFocus-lens);
            var local=Quaternion.Inverse(rotation)*(point-lens);
            if(local.z<.1f)return false;
            float halfHeight=local.z*Mathf.Tan(23*Mathf.Deg2Rad);
            return Mathf.Abs(local.x)<halfHeight*mission.gameCamera.aspect*.72f&&Mathf.Abs(local.y)<halfHeight*.72f;
        }
        public void ExecuteHostage(Map01Rescue owner,Map01EnemyController overseer,Map01EnemyController reporter=null){
            if(IsPlaying)return;
            enemy=overseer;Begin(owner,Mode.Execution,30);
            // The cinematic pose owns and detaches the controller; collect its
            // locomotion clips before that handoff, never from the detached actor.
            var actor=mission.player.GetComponentInChildren<Animator>();var clips=actor.runtimeAnimatorController!=null?actor.runtimeAnimatorController.animationClips:Array.Empty<AnimationClip>();
            OwnStoryActors();storyBeat=1;nextHeartbeat=0;
            bool knife=mission.ModernCombat.EquippedKind==WeaponKind.Knife;
            alarmCrouch=knife?Map01CinematicPose.Clip("Nam_Bush_Recovery"):clips.FirstOrDefault(c=>c.name=="Nam_Crouch_Idle");
            alarmCrouch??=introNamIdle;
            alarmWalk=clips.FirstOrDefault(c=>c.name.Contains(storySneaking?"Crouch_Walk_B":"Rifle_Walk_B"));alarmReverseWalk=false;
            if(alarmWalk==null&&storySneaking){alarmWalk=clips.FirstOrDefault(c=>c.name=="Nam_Crouch_Walk_F");alarmReverseWalk=alarmWalk!=null;}
            alarmWalk??=clips.FirstOrDefault(c=>c.name.Contains("Walk_B"))??introNamIdle;
            alarmRetreat=introNamPosition;
            foreach(float side in new[]{0f,.55f,-.55f}){
                var direction=(-mission.player.forward+mission.player.right*side).normalized;
                var desired=introNamPosition+direction*.75f;
                if(NavMesh.SamplePosition(introNamPosition,out var from,.4f,NavMesh.AllAreas)&&NavMesh.SamplePosition(desired,out var to,.25f,NavMesh.AllAreas)
                    &&!NavMesh.Raycast(from.position,to.position,out _,NavMesh.AllAreas)){
                    var delta=to.position-from.position;
                    bool blocked=Physics.CapsuleCastAll(from.position+Vector3.up*.45f,from.position+Vector3.up*1.3f,.22f,delta.normalized,delta.magnitude,mission.ObstructionMask,QueryTriggerInteraction.Ignore)
                        .Any(h=>!h.transform.IsChildOf(mission.player)&&h.transform.GetComponentInParent<Health>()==null);
                    if(!blocked){alarmRetreat=Map01OperationSession.GroundPoint(to.position,mission.player);break;}
                }
            }
            Voice().Play(reporter!=null&&reporter!=overseer&&reporter.Alive?"m1_rescue_alarm":"m1_rescue_execute_order");
            overseer.FaceImmediate(mission.hung.position);
            var axis=Vector3.ProjectOnPlane(enemy.transform.position-mission.hung.position,Vector3.up).normalized;
            if(axis.sqrMagnitude<.01f)axis=enemy.transform.forward;
            var lensSide=Vector3.Cross(Vector3.up,axis);
            executionFocus=Vector3.Lerp(mission.hung.position,enemy.transform.position,.35f)+Vector3.up*.82f;
            if(Vector3.Dot(lensSide,cameraStart-executionFocus)<0)lensSide=-lensSide;
            executionView=FindStoryLens(executionFocus,lensSide,3.1f,.65f);
            var namFocus=introNamPosition+Vector3.up*1.1f;
            if(!ShelterCameraClear(namFocus,cameraStart)||Physics.CheckSphere(cameraStart,.16f,mission.ObstructionMask,QueryTriggerInteraction.Ignore)){
                cameraStart=FindStoryLens(namFocus,-mission.player.forward+mission.player.right*.5f,2.5f,.6f);
                cameraRotation=Quaternion.LookRotation(namFocus-cameraStart);
            }
            reactionView=lastWordsView=Vector3.zero; // resolved after the posed actor is sampled
        }
        void UpdateStory(){
            if(CurrentMode==Mode.Intro){
                // The hostile exchange belongs to the captive shot. Nam's radio
                // report starts only after the lens has returned to him.
                if(storyBeat==0&&Elapsed>=IntroWalkAt){Voice().Play("m1_rescue_interrogate");storyBeat=1;}
                if(!introImpact&&Elapsed>=IntroHitAt){introImpact=true;introImpactAt=Elapsed;if(canApproach)PlayContactThud();}
                if(!introRightImpact&&Elapsed>=IntroRightHitAt+Mathf.Max(0,introImpactAt-IntroHitAt)){introRightImpact=true;introRightImpactAt=Elapsed;if(canApproach)PlayContactThud();}
                if(storyBeat==1&&Elapsed>=4.1f&&!Voice().HasSpeech){Voice().Play("m1_story_11");storyBeat=6;}
                else if(storyBeat==6&&Elapsed>=9.35f&&!Voice().HasSpeech){introReturnAt=Elapsed;storyBeat=7;}
                else if(storyBeat==7&&Elapsed>=IntroCutOut+.3f){introRadioAt=Elapsed;Voice().Play("m1_v2_rescue_approach_01");storyBeat=2;}
                else if(storyBeat==2&&!Voice().HasSpeech){Voice().Play("m1_v2_rescue_approach_02");storyBeat=3;}
                else if(storyBeat==3&&!Voice().HasSpeech){Voice().Play("m1_v2_rescue_approach_03");storyBeat=4;}
                else if(storyBeat==4&&!Voice().HasSpeech){introReleaseAt=Elapsed;storyBeat=5;}
                else if(storyBeat==5&&Elapsed>=introReleaseAt+.8f)Finish();
                return;
            }
            UpdateExecutionSound();
            if(storyBeat==1&&!Voice().HasSpeech&&Elapsed>=1.4f){storyBeat=2;beatAt=Elapsed;}
            else if(storyBeat==2&&Elapsed-beatAt>=Mathf.Max(1.6f,rescue.Layout.executionAimSeconds)){
                shot=true;impactAt=Elapsed;storyBeat=3;
            }
            if(storyBeat==3){
                FireExecutionBurst(OpeningRounds,impactAt,0);
                if(executionRounds==OpeningRounds.Length&&Elapsed-impactAt>=1.75f){finalBurstAt=Elapsed;storyBeat=5;}
            }
            if(storyBeat==5){
                FireExecutionBurst(ClosingRounds,finalBurstAt,OpeningRounds.Length);
                // All reports end, then the whole bound collapse completes.
                // Hung is still alive when his exhausted last words begin.
                if(executionRounds==OpeningRounds.Length+ClosingRounds.Length&&Elapsed-finalBurstAt>=2.65f){
                    Voice().Play("m1_rescue_hung_defiance");storyBeat=4;beatAt=Elapsed;
                }
            }else if(storyBeat==4&&!Voice().HasSpeech&&Elapsed-beatAt>=DialogueVoice.Length("m1_rescue_hung_defiance",4)+.35f){
                dyingReleaseAt=Elapsed;storyBeat=6;rescue.CompleteExecution();
            }else if(storyBeat==6&&Elapsed-dyingReleaseAt>=.65f+ReactionViewSeconds+FailureFadeSeconds)Finish();
        }
        void FireExecutionBurst(float[] rounds,float started,int offset){
            int index=executionRounds-offset;
            // At most one round per frame. Slow frames lengthen a burst instead
            // of repaying several overdue reports/flashes in the same frame.
            if(index<0||index>=rounds.Length||Elapsed-started<rounds[index]||Elapsed-lastRoundAt<.10f)return;
            var chest=captiveVisual.actor.GetBoneTransform(HumanBodyBones.Chest).position;
            enemy.FireAtHostage(chest,0,true,true);executionRounds++;lastRoundAt=Elapsed;
        }
        void RenderStory(){
            RenderIntroNam();
            enemy.FaceImmediate(mission.hung.position);
            float captiveTime=CurrentMode==Mode.Execution?(shot?(finalBurstAt<0?Mathf.Min(.55f,(Elapsed-impactAt)*.6f):.55f+(Elapsed-finalBurstAt)*1.1f):0)
                :.8f;
            // Keep a single bound wrist binding from the first shot to the body.
            captivePose.Sample(CurrentMode==Mode.Execution?boundExecution:Map01CinematicPose.Clip("Hung_Captive_Recoil"),captiveTime);
            if(CurrentMode==Mode.Intro){
                RenderInterrogation();
                if(introImpact){
                    float age=Elapsed-(introRightImpact?introRightImpactAt:introImpactAt);
                    float fold=Mathf.SmoothStep(0,1,(age-.045f)/.10f)*(1-Mathf.SmoothStep(0,1,(age-.32f)/.62f));
                    var actor=captiveVisual.actor;
                    // Belly impact: the torso folds and the head follows. Wrists
                    // remain bound behind the back, no early face recoil.
                    var foldAxis=Vector3.Cross(Vector3.up,Vector3.ProjectOnPlane(enemy.transform.position-mission.hung.position,Vector3.up).normalized);
                    foreach(var pair in new[]{(HumanBodyBones.Spine,25f),(HumanBodyBones.Chest,20f),(HumanBodyBones.Neck,10f),(HumanBodyBones.Head,10f)}){
                        var bone=actor.GetBoneTransform(pair.Item1);if(bone!=null)bone.rotation=Quaternion.AngleAxis(pair.Item2*fold,foldAxis)*bone.rotation;
                    }
                    var chest=actor.GetBoneTransform(HumanBodyBones.Chest);
                    chest.rotation=Quaternion.AngleAxis((introRightImpact?-4:4)*fold,Vector3.up)*chest.rotation;
                }
            }
            else {
                if(executionAim!=null)interrogatorPose.Sample(executionAim,Elapsed-beatAt,true);
                AimExecution();
                float kick=Mathf.Exp(-Mathf.Max(0,Elapsed-lastRoundAt)*25)*3;
                var actor=enemy.GetComponentInChildren<Animator>();
                var shoulder=actor.GetBoneTransform(HumanBodyBones.RightUpperArm);
                if(shoulder!=null)shoulder.rotation=Quaternion.AngleAxis(-kick,enemy.transform.right)*shoulder.rotation;
                actor.GetComponent<Map01EnemyGrip>()?.Apply();
                // Hùng gathers his breath without standing or unbinding his hands.
                var chest=captiveVisual.actor.GetBoneTransform(HumanBodyBones.Chest);
                var head=captiveVisual.actor.GetBoneTransform(HumanBodyBones.Head);
                float held=shot&&finalBurstAt<0?Mathf.SmoothStep(0,1,(Elapsed-impactAt)/.6f):0;
                if(chest!=null)chest.rotation=Quaternion.AngleAxis((2+Mathf.Sin(Elapsed*3)*.7f)*held,mission.hung.right)*chest.rotation;
                if(head!=null)head.rotation=Quaternion.AngleAxis(-7*held,mission.hung.right)*head.rotation;
                if(HungSpeakingLastWords&&chest!=null){
                    // A shallow breath in the collapsed pose, never lifting him
                    // back to a kneeling speaking idle.
                    float breath=Mathf.Sin((Elapsed-beatAt)*2.8f)*.65f;
                    chest.rotation=Quaternion.AngleAxis(breath,mission.hung.right)*chest.rotation;
                }
            }
            captivePose.Ground(mission.hung.position.y);interrogatorPose.Ground(enemy.transform.position.y);captiveVisual.SampleCaptiveContacts();
            if(CurrentMode==Mode.Intro&&interrogationSling!=null)interrogationSling.Sample(interrogationSling.Weight,false);
            if(IntroShowsCaptive){
                var lens=Vector3.MoveTowards(introCaptiveView,introCaptiveFocus,.12f*Mathf.SmoothStep(0,1,(Elapsed-IntroCutIn)/4));
                var look=Quaternion.LookRotation(introCaptiveFocus-lens);
                if(introReturnAt>0){
                    float turn=Mathf.SmoothStep(0,1,(Elapsed-introReturnAt)/.85f);
                    look=Quaternion.Slerp(look,Quaternion.LookRotation(introNamPosition+Vector3.up-introCaptiveView),turn*.65f);
                }
                SetStoryView(lens,look,46);return;
            }
            if(CurrentMode==Mode.Intro&&Elapsed>=IntroPanAt&&Elapsed<IntroCutIn){
                var look=Quaternion.LookRotation(introCaptiveFocus-cameraStart);
                SetStoryView(cameraStart,Quaternion.Slerp(cameraRotation,look,Mathf.SmoothStep(0,1,(Elapsed-IntroPanAt)/(IntroCutIn-IntroPanAt))),cameraFov);return;
            }
            if(HungSpeakingLastWords){
                var head=captiveVisual.actor.GetBoneTransform(HumanBodyBones.Head).position;
                var chest=captiveVisual.actor.GetBoneTransform(HumanBodyBones.Chest).position;
                var focus=Vector3.Lerp(chest,head,.7f);
                if(lastWordsView==Vector3.zero)lastWordsView=FindStoryLens(focus,mission.hung.right+mission.hung.forward*.35f,1.9f,.3f);
                SetStoryView(lastWordsView,Quaternion.LookRotation(focus-lastWordsView),42);return;
            }
            if(ExecutionShowsCaptive){
                float push=Mathf.SmoothStep(0,1,(Elapsed-beatAt)/4.5f)*.18f;
                var lens=Vector3.MoveTowards(executionView,executionFocus,push);
                SetStoryView(lens,Quaternion.LookRotation(executionFocus-lens),44);return;
            }
            if(ExecutionShowsReaction){
                var target=mission.player.position+Vector3.up*1.03f;
                if(reactionView==Vector3.zero)reactionView=FindStoryLens(target,mission.player.forward+mission.player.right*.5f,2.05f,.45f);
                SetStoryView(reactionView,Quaternion.LookRotation(target-reactionView),42);return;
            }
            float tension=ExecutionTension;
            var sway=Quaternion.Euler(tension*Mathf.Sin(Elapsed*4.3f)*.08f,tension*Mathf.Sin(Elapsed*3.1f)*.07f,0);
            var follow=CurrentMode==Mode.Execution?mission.player.position-introNamPosition:Vector3.zero;
            SetStoryView(cameraStart+follow,cameraRotation*sway,cameraFov-5*tension);
        }

        void SetStoryView(Vector3 position,Quaternion rotation,float fov){
            rig.SetCinematicView(position,rotation,fov,1);
            // This director runs after the gameplay camera's LateUpdate. Apply
            // the cut now as well, so a gunshot cannot precede its camera cut.
            mission.gameCamera.transform.SetPositionAndRotation(position,rotation);mission.gameCamera.fieldOfView=fov;
        }
        Vector3 FindStoryLens(Vector3 focus,Vector3 preferred,float distance,float height){
            preferred=Vector3.ProjectOnPlane(preferred,Vector3.up).normalized;
            foreach(float turn in new[]{0f,30f,-30f,60f,-60f,90f,-90f,180f}){
                var candidate=focus+Quaternion.Euler(0,turn,0)*preferred*distance+Vector3.up*height;
                if(ShelterCameraClear(focus,candidate)&&!Physics.CheckSphere(candidate,.16f,mission.ObstructionMask,QueryTriggerInteraction.Ignore))return candidate;
            }
            return ClearStoryView(focus,distance);
        }
        void RenderIntroNam(){
            if(introNamPose==null||introNamIdle==null)return;
            float step=CurrentMode==Mode.Execution?Mathf.SmoothStep(0,1,(Elapsed-.2f)/.9f):0;
            mission.player.SetPositionAndRotation(Vector3.Lerp(introNamPosition,alarmRetreat,step),introNamRotation);
            float idleTime=Mathf.Repeat(Elapsed,Mathf.Max(.01f,introNamIdle.length));
            if(CurrentMode==Mode.Execution&&Elapsed>.2f){
                float walkTime=Mathf.Repeat(Vector3.Distance(introNamPosition,mission.player.position)/1.4f,Mathf.Max(.01f,alarmWalk.length));
                if(alarmReverseWalk)walkTime=alarmWalk.length-walkTime;
                introNamPose.SampleBlend(alarmWalk,walkTime,alarmCrouch,Mathf.Max(0,Elapsed-1.1f),Mathf.SmoothStep(0,1,(Elapsed-.85f)/.65f));
            }else introNamPose.Sample(introNamIdle,idleTime,true);
            if(CurrentMode==Mode.Intro&&introRadioAt>0){
                float weight=storyBeat==5?1-Mathf.SmoothStep(0,1,(Elapsed-introReleaseAt)/.8f):Mathf.SmoothStep(0,1,(Elapsed-introRadioAt)/.7f);
                Map01ArmContact.Radio(mission.player.GetComponentInChildren<Animator>(),weight);
            }
            if(CurrentMode==Mode.Execution){
                // A restrained look toward the alarm, distributed through the
                // upper body. Hands, hips and planted feet keep the authored pose.
                var actor=mission.player.GetComponentInChildren<Animator>();
                if(shot){
                    float shock=Mathf.Exp(-Mathf.Max(0,Elapsed-impactAt)*7);
                    var spine=actor.GetBoneTransform(HumanBodyBones.Spine);if(spine!=null)spine.rotation=Quaternion.AngleAxis(5*shock,mission.player.right)*spine.rotation;
                }
                var direction=Vector3.ProjectOnPlane(mission.hung.position-mission.player.position,Vector3.up);
                if(direction.sqrMagnitude>.01f){
                    var turn=Quaternion.RotateTowards(Quaternion.identity,Quaternion.FromToRotation(mission.player.forward,direction.normalized),30);
                    float reaction=Mathf.SmoothStep(0,1,Elapsed/.9f);
                    foreach(var part in new[]{(HumanBodyBones.Chest,.15f),(HumanBodyBones.Neck,.35f),(HumanBodyBones.Head,.5f)}){
                        var bone=actor.GetBoneTransform(part.Item1);if(bone!=null)bone.rotation=Quaternion.Slerp(Quaternion.identity,turn,reaction*part.Item2)*bone.rotation;
                    }
                }
            }
            introNamPose.Ground(mission.player.position.y);
        }
        void UpdateExecutionSound(){
            if(tensionSource==null){
                tensionSource=gameObject.AddComponent<AudioSource>();tensionSource.playOnAwake=false;tensionSource.spatialBlend=0;tensionSource.volume=.12f;tensionSource.priority=80;
                var samples=new float[12000];
                for(int i=0;i<samples.Length;i++){
                    float t=i/24000f;
                    float Pulse(float at)=>at<0?0:Mathf.Sin(at*2*Mathf.PI*65)*Mathf.Exp(-at*32);
                    samples[i]=(Pulse(t)+.6f*Pulse(t-.16f))*.7f;
                }
                heartbeat=AudioClip.Create("Rescue tension heartbeat",samples.Length,1,24000,false);heartbeat.SetData(samples,0);tensionSource.clip=heartbeat;
            }
            if(!shot&&Elapsed>=nextHeartbeat){tensionSource.Play();nextHeartbeat=Elapsed+Mathf.Lerp(.9f,.6f,ExecutionTension);}
            if(shot)tensionSource.Stop();
        }
        void AimExecution(){
            var actor=enemy.GetComponentInChildren<Animator>();
            var rifle=actor.GetComponent<Map01Rifle>();var grip=actor.GetComponent<Map01EnemyGrip>();
            if(rifle==null||rifle.weapon==null||grip==null)return;
            grip.Apply();
            var target=captiveVisual.actor.GetBoneTransform(HumanBodyBones.Chest).position;
            var muzzle=rifle.weapon.Muzzle;var upper=actor.GetBoneTransform(HumanBodyBones.RightUpperArm);
            if(muzzle!=null&&upper!=null){
                var neutral=upper.rotation;
                // Rotating the arm also moves the muzzle: solve again after each
                // grip update instead of leaving the barrel pointed past Hung.
                for(int pass=0;pass<5;pass++){
                    var correction=Quaternion.FromToRotation(muzzle.forward,(target-muzzle.position).normalized);
                    upper.rotation=Quaternion.RotateTowards(neutral,correction*upper.rotation,75);
                    grip.Apply();
                    if(Vector3.Angle(muzzle.forward,target-muzzle.position)<1)break;
                }
            }
        }
        Vector3 ClearStoryView(Vector3 target,float radius){
            foreach(var side in new[]{new Vector3(-1,.65f,-1),new Vector3(1,.65f,-1),new Vector3(-1,.7f,1),new Vector3(1,.7f,1)}){
                var candidate=target+side*radius;
                var delta=candidate-target;
                if(!Physics.SphereCastAll(target,.15f,delta.normalized,delta.magnitude,mission.ObstructionMask,QueryTriggerInteraction.Ignore)
                    .Any(h=>h.transform.GetComponentInParent<Health>()==null))return candidate;
            }
            return target+new Vector3(-radius,radius*1.4f,-radius);
        }
    }
}
