using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
namespace ShadowVale.Map01
{
    /// <summary>
    /// After the three families (D3_04), straight on from Hùng's "Vâng. Thế ông đi, cháu đỡ.", a short cutscene: Hùng walks
    /// Ông Sáu down to the south dock steps and they try them, the support Night 3 uses again (script: "Hùng đỡ ông Sáu đi
    /// thử xuống bậc bến"). The two of them setting off from the yard; going down the steps, seen from the water; on the bottom
    /// step the old man looks out at the river and nods, satisfied; they turn and Hùng leads him back up towards the village,
    /// seen from the bank; then dark, and they are home for supper. Hùng walks slowly at the old man's left, his right hand
    /// under the old man's elbow and his left holding his wrist (arm contact, no paired clip); the old man limps at Hùng's
    /// pace. Chú Bảy steps out of their way first if he stands on it. Esc held skips it.
    /// </summary>
    public sealed partial class Map02ChapterDirector
    {
        const float StepsPace=.9f,StepsYardShot=4.5f,StepsCutAhead=7,StepsPause=1,StepsNod=2.2f,StepsTurn=1.2f,StepsBack=5.5f,StepsFade=.8f,StepsFadeBack=.6f;
        /// <summary>Where the old man walks against Hùng: close on his right, level with him.</summary>
        static readonly Vector3 SupportSide=new Vector3(.5f,0,.02f);
        Vector3 bayWasAt;bool bayMoved;
        bool stepsCut;float stepsClock,stepsWalkClock,stepsSkip,stepsFadeBack,stepsJump,stepsRun;bool stepsMoving;
        Vector3[] stepsPath=System.Array.Empty<Vector3>();Vector3 stepsStartDir,stepsCameraFrom;Quaternion stepsCameraRotation;float stepsCameraFov;
        public bool StepsRehearsal=>stepsCut;
        public Vector3 DockSteps=>sites["dock"].position+Vector3.left*2.4f;   // the top of the steps, clear of chú Bảy at the boats
        /// <summary>The bottom step, at the water's edge.</summary>
        public Vector3 StepsBottom
        {
            get
            {
                var top=DockSteps;var water=Vector3.ProjectOnPlane(WaterPoint("boatDock")-top,Vector3.up);water=water.sqrMagnitude>.01f?water.normalized:Vector3.back;
                var p=top+water*2.6f;return Physics.Raycast(p+Vector3.up*3,Vector3.down,out var hit,6,~0,QueryTriggerInteraction.Ignore)?hit.point:p;
            }
        }
        Vector3 SauYard=>sites["family"].position+Vector3.right*2.4f+Vector3.forward*1.2f;
        static float Flat(Vector3 a,Vector3 b)=>Vector2.Distance(new Vector2(a.x,a.z),new Vector2(b.x,b.z));
        Vector3 SupportSpot(Transform hung)=>hung.position+hung.rotation*SupportSide;
        /// <summary>How close the walk to the steps passes <paramref name="p"/>.</summary>
        float Clearance(Vector3 p){float best=float.PositiveInfinity;for(int i=1;i<stepsPath.Length;i++)best=Mathf.Min(best,FlatSegmentDistance(stepsPath[i-1],stepsPath[i],p));return best;}
        float StepsArrive=>StepsYardShot+(stepsRun-stepsJump)/StepsPace;
        float StepsNodFrom=>StepsArrive+StepsPause;
        float StepsTurnFrom=>StepsNodFrom+StepsNod;
        float StepsBackFrom=>StepsTurnFrom+StepsTurn;
        float StepsEnd=>StepsBackFrom+StepsBack;   // the dark comes down over the last of the walk back

        void BeginStepsRehearsal()
        {
            var hung=roles["Hùng"];var start=hung.transform.position;var path=new NavMeshPath();
            var corners=NavMesh.CalculatePath(start,DockSteps,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete?path.corners:new[]{start,DockSteps};
            stepsPath=corners.Concat(new[]{StepsBottom}).ToArray();stepsRun=Length(stepsPath);
            stepsJump=Mathf.Clamp(stepsRun-2.6f-StepsCutAhead,StepsYardShot*StepsPace,stepsRun);   // the cut lands a few metres short of the top
            stepsStartDir=Vector3.ProjectOnPlane(Along(stepsPath,1)-start,Vector3.up);stepsStartDir=stepsStartDir.sqrMagnitude>.01f?stepsStartDir.normalized:hung.transform.forward;
            roles["Ông Sáu"].SetLocomotion(DayClip("InjuredIdle"),DayClip("InjuredWalk"));
            // Chú Bảy keeps clear of the way down if he stands on it.
            bayMoved=false;
            if(roles.TryGetValue("Chú Bảy",out var bay)&&bay!=null&&Clearance(bay.transform.position)<2.2f)
            {
                bayWasAt=bay.transform.position;
                for(int i=0;i<16;i++)
                {
                    var p=bayWasAt+Quaternion.Euler(0,i*45,0)*Vector3.forward*(2.5f+i/8*1.5f);
                    if(!NavMesh.SamplePosition(p,out var hit,.6f,NavMesh.AllAreas)||Clearance(hit.position)<2.2f)continue;
                    bay.StopAt(hit.position);var look=Vector3.ProjectOnPlane(stepsPath[stepsPath.Length-1]-hit.position,Vector3.up);if(look.sqrMagnitude>.01f)bay.transform.rotation=Quaternion.LookRotation(look);
                    bayMoved=true;break;
                }
            }
            var cam=rig.GetComponent<Camera>();stepsCameraFrom=cam.transform.position;stepsCameraRotation=cam.transform.rotation;stepsCameraFov=cam.fieldOfView;
            stepsCut=true;stepsClock=stepsWalkClock=stepsSkip=0;
        }
        void TickStepsRehearsal(float dt)
        {
            if(!stepsCut){if(stepsFadeBack>0)stepsFadeBack=Mathf.Max(0,stepsFadeBack-dt);return;}
            if(State.phase!=4){EndStepsRehearsal();return;}
            stepsClock+=dt;stepsSkip=Keyboard.current?.escapeKey.isPressed==true?stepsSkip+Time.unscaledDeltaTime:0;
            if(stepsSkip>=1&&stepsClock<StepsEnd-StepsFade)stepsClock=StepsEnd-StepsFade;
            if(stepsClock>=StepsEnd){EndStepsRehearsal();return;}
            var hung=roles["Hùng"];
            float t=stepsClock;bool back=t>=StepsBackFrom;
            float d=t<StepsYardShot?t*StepsPace:t<StepsArrive?Mathf.Min(stepsRun,stepsJump+(t-StepsYardShot)*StepsPace):!back?stepsRun:Mathf.Max(0,stepsRun-(t-StepsBackFrom)*StepsPace);
            stepsMoving=t<StepsArrive&&d<stepsRun-.02f||back;if(stepsMoving)stepsWalkClock+=dt;
            var at=Along(stepsPath,d);
            // Out along the path; at the bottom, out at the river; turning, and back up the way they came.
            var ahead=t<StepsArrive?Along(stepsPath,Mathf.Min(stepsRun,d+.6f))-at:t<StepsTurnFrom?WaterPoint("boatDock")-at:Along(stepsPath,Mathf.Max(0,d-.6f))-at;
            var flat=Vector3.ProjectOnPlane(ahead,Vector3.up);
            hung.transform.position=at;if(flat.sqrMagnitude>.001f)hung.transform.rotation=Quaternion.RotateTowards(hung.transform.rotation,Quaternion.LookRotation(flat),200*dt);
        }
        /// <summary>After the animators: the old man at Hùng's side, both in their walk (or stand), and the shot.</summary>
        void HoldStepsPair()
        {
            if(!stepsCut)return;
            if(!roles.TryGetValue("Hùng",out var hung)||!roles.TryGetValue("Ông Sáu",out var sau)||hung==null||sau==null)return;
            sau.transform.SetPositionAndRotation(SupportSpot(hung.transform),hung.transform.rotation);
            var lead=stepsMoving?ordinaryWalk:namCalmIdle;if(lead!=null)hung.SamplePerformance(lead,stepsWalkClock*StepsPace/1.35f);   // his own walk, slowed to the old man's pace
            var limp=stepsMoving?DayClip("InjuredWalk"):DayClip("InjuredIdle");if(limp!=null)sau.SamplePerformance(limp,stepsWalkClock);
            HoldTheOldMansArm(hung,sau);
            if(stepsClock>=StepsNodFrom&&stepsClock<StepsTurnFrom)
            {
                // He nods at the river: twice, slowly, satisfied.
                var animator=sau.visual!=null?sau.visual.GetComponentInChildren<Animator>():null;var head=animator!=null&&animator.isHuman?animator.GetBoneTransform(HumanBodyBones.Head):null;
                float u=(stepsClock-StepsNodFrom)/StepsNod;if(head!=null)head.rotation=Quaternion.AngleAxis(Mathf.Sin(u*Mathf.PI*4)*Mathf.Sin(u*Mathf.PI)*14,sau.transform.right)*head.rotation;
            }
            var pair=Vector3.Lerp(hung.transform.position,sau.transform.position,.5f);Vector3 eye,focus;
            var bottom=StepsBottom;var water=Vector3.ProjectOnPlane(bottom-DockSteps,Vector3.up);water=water.sqrMagnitude>.01f?water.normalized:Vector3.back;var across=Vector3.Cross(Vector3.up,water);
            if(stepsClock<StepsYardShot)
            {
                // Setting off from the yard: from behind and to the side, the camera turning to keep them.
                var start=stepsPath[0];var side=Vector3.Cross(Vector3.up,stepsStartDir);
                eye=start-stepsStartDir*4+side*2.5f+Vector3.up*1.7f;focus=pair+Vector3.up*1.2f;
            }
            else if(stepsClock<StepsArrive)
            {
                // Down the steps, seen low from the water.
                eye=bottom+water*4.5f+across*1.8f+Vector3.up*1.1f;focus=pair+Vector3.up*1.1f;
            }
            else if(stepsClock<StepsBackFrom)
            {
                // On the bottom step: close beside them, on the old man's face as he looks out and nods.
                var sauHead=sau.transform.position+Vector3.up*1.45f;
                eye=bottom+across*2.2f-water*.3f+Vector3.up*1.4f;focus=Vector3.Lerp(pair+Vector3.up*1.25f,sauHead,stepsClock<StepsTurnFrom?.6f:.2f);
            }
            else
            {
                // Back up the steps towards the village, seen from the bank they come up to.
                eye=DockSteps-water*3.5f+across*1.5f+Vector3.up*1.6f;focus=pair+Vector3.up*1.2f;
            }
            float blend=Mathf.SmoothStep(0,1,stepsClock/.6f);
            rig.SetCinematicView(Vector3.Lerp(stepsCameraFrom,eye,blend),Quaternion.Slerp(stepsCameraRotation,Quaternion.LookRotation(focus-eye),blend),Mathf.Lerp(stepsCameraFov,50,blend),1);
        }
        /// <summary>Hùng's right hand under the old man's left elbow, his left on the old man's wrist.</summary>
        void HoldTheOldMansArm(Map02ChapterActor hung,Map02ChapterActor sau)
        {
            var helper=hung.visual!=null?hung.visual.GetComponentInChildren<Animator>():null;var old=sau.visual!=null?sau.visual.GetComponentInChildren<Animator>():null;
            if(helper==null||old==null||!helper.isHuman||!old.isHuman)return;
            var elbow=old.GetBoneTransform(HumanBodyBones.LeftLowerArm);var wrist=old.GetBoneTransform(HumanBodyBones.LeftHand);
            var right=helper.GetBoneTransform(HumanBodyBones.RightUpperArm);var left=helper.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            var body=hung.transform;
            if(elbow!=null&&right!=null)Map01ArmContact.Reach(helper,false,elbow.position-body.up*.04f-body.right*.03f,right.position+body.right*.25f-body.up*.35f-body.forward*.1f,1);
            if(wrist!=null&&left!=null)Map01ArmContact.Reach(helper,true,wrist.position+body.forward*.03f,left.position-body.right*.1f-body.up*.3f+body.forward*.25f,1);
        }
        /// <summary>The cutscene's dark: going down at its end, coming back up once they are home.</summary>
        float StepsBlack=>stepsCut?Mathf.Clamp01((stepsClock-(StepsEnd-StepsFade))/StepsFade):Mathf.Clamp01(stepsFadeBack/StepsFadeBack);
        /// <summary>Home for supper, the walk done; or put where the night has them.</summary>
        void EndStepsRehearsal()
        {
            if(!stepsCut)return;stepsCut=false;rig.ClearCinematicView();
            if(!roles.TryGetValue("Hùng",out var hung)||!roles.TryGetValue("Ông Sáu",out var sau)||hung==null||sau==null)return;
            hung.ReleasePerformance();sau.ReleasePerformance();HungLocomotion();sau.SetLocomotion(null,null);
            if(bayMoved&&roles.TryGetValue("Chú Bảy",out var bay)&&bay!=null){bay.StopAt(bayWasAt);bayMoved=false;}   // back to the boats, unseen in the dark
            if(State.phase==4){hung.StopAt(HungAtSupper);sau.StopAt(SauYard);stepsFadeBack=StepsFadeBack;}
            ForestMenu.SuppressKeysAfterCutscene();
        }
    }
}
