using System.Linq;
using UnityEngine;
using UnityEngine.AI;
namespace ShadowVale.Map01
{
    public sealed partial class Map02ChapterDirector
    {
        Map02DayOneArt dayArt;
        Map01CinematicPose dayNamPose;
        Map02UpperBodyPose cargoPose;
        AnimationClip ordinaryWalk;
        Animator dayNamAnimator;
        Transform medicineArt,hammerArt,mealRoot,restMat;
        Transform sceneChair,bandageRoll,fishBowl,pot,dayLampFlame;
        Vector3 daySceneHungOrigin,daySceneLanhOrigin;
        Quaternion daySceneHungRotation,daySceneLanhRotation;
        bool dayStaging;
        Vector3[] dayNamPath=System.Array.Empty<Vector3>(),dayHungPath=System.Array.Empty<Vector3>();
        float dayApproachDuration;float[] mealDurations;bool mealBegan;
        float DaySceneMinimum=>sceneTask?.id=="N1_04"?dayApproachDuration+12:sceneTask?.id=="N1_01"?dayApproachDuration+(mealDurations?.Sum()??90):sceneTask?.seconds??0;
        void PrepareMealTiming(){float[] pauses={.8f,.8f,1,2.5f,.8f,1,1,1.4f,.8f,.7f,2.2f,4,2.5f,3,3};mealDurations=lines.Select((line,i)=>(Map02DialogueCatalog.HasApprovedRecording(line)?DialogueVoice.Length(line.id):Mathf.Max(2.8f,line.text.Length/11f))+(i<pauses.Length?pauses[i]:1)).ToArray();float extra=Mathf.Max(0,90-mealDurations.Sum());float weights=pauses.Take(mealDurations.Length-1).Sum();for(int i=0;i<mealDurations.Length-1;i++)mealDurations[i]+=extra*pauses[i]/weights;mealBegan=false;}
        float DayLineDuration(float normal)=>sceneTask?.id=="N1_01"&&mealDurations!=null&&lineIndex<mealDurations.Length?mealDurations[lineIndex]:normal;
        bool HoldMealVoice=>sceneTask?.id=="N1_01"&&!mealBegan&&sceneClock<dayApproachDuration;
        void TickMealVoice(){if(sceneTask?.id=="N1_01"&&!mealBegan&&sceneClock>=dayApproachDuration){mealBegan=true;Speak();}}
        Vector3[] DayPath(Vector3 start,Vector3 goal){var path=new NavMeshPath();if(NavMesh.SamplePosition(goal,out var h,1.5f,NavMesh.AllAreas)&&NavMesh.CalculatePath(start,h.position,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete)return path.corners;Debug.LogError("Map 2 day-one seating path is blocked. Adjust the performance anchor.",this);return new[]{start};}
        static float PathDuration(Vector3[] path){float length=0;for(int i=1;i<path.Length;i++)length+=Vector3.Distance(path[i-1],path[i]);return length/1.25f;}
        void MoveDaySceneRoots()
        {
            if(sceneTask==null||dayNamPath.Length==0)return;lockedPosition=Along(dayNamPath,sceneClock*1.25f);var ahead=Along(dayNamPath,(sceneClock+.15f)*1.25f)-lockedPosition;if(ahead.sqrMagnitude>.001f)lockedRotation=Quaternion.LookRotation(Vector3.ProjectOnPlane(ahead,Vector3.up));if(sceneClock>=PathDuration(dayNamPath))lockedRotation=Quaternion.identity;
            var hung=roles["Hùng"];hung.SceneMove(Along(dayHungPath,sceneClock*1.25f),Along(dayHungPath,(sceneClock+.2f)*1.25f),sceneClock<dayApproachDuration?1.25f:0);if(sceneClock>=dayApproachDuration)hung.transform.rotation=sceneTask.id=="N1_01"?Quaternion.Euler(0,180,0):Quaternion.identity;
        }
        AnimationClip DayClip(string id)=>dayArt!=null?dayArt.Find(id):null;
        AnimationClip HungMapOneWalk=>hungMapOneWalk;
        AnimationClip hungMapOneWalk;
        void LoadDayPerformances()
        {
            dayArt=Resources.Load<Map02DayOneArt>("Story/Map02DayOneArt");dayNamAnimator=Mission.Player.GetComponentInChildren<Animator>();ordinaryWalk=dayNamAnimator.runtimeAnimatorController.animationClips.FirstOrDefault(c=>c.name=="Walk_Tuned");
            hungMapOneWalk=Resources.Load<Map01HungVisual>("Characters/HungVisual")?.actor?.runtimeAnimatorController?.animationClips.FirstOrDefault(clip=>clip.name.Contains("DiKhapKhieng"))??dayArt?.Find("InjuredWalk");
            if(dayArt==null)Debug.LogWarning("Map 2 Day 1 performance asset is missing. Run ShadowVale.Map01.Editor.Map02DayOneBuilder.Build.",this);
        }
        void FreeNamPose(AnimationClip clip,float clock,bool loop=true)
        {if(clip==null)return;dayNamPose??=new Map01CinematicPose(dayNamAnimator,null,false);dayNamPose.Sample(clip,clock,loop);}
        void ReleaseDayFreePose()
        {
            bool held=dayNamPose!=null||cargoPose!=null;dayNamPose?.Dispose();dayNamPose=null;cargoPose?.Dispose();cargoPose=null;
            if(held)RestoreNamAnimator();   // releasing a pose rebinds Nam's animator, which forgets the weapon in his hands
        }
        void CreateStoryProps(Material wood,Material cloth,Material iron)
        {
            var family=DayFamily;
            sceneChair=Group("Bandaging chair",family+Vector3.right*1.4f);
            foreach(float x in new[]{-.21f,.21f})foreach(float z in new[]{-.2f,.2f})Part(sceneChair,"Leg",new Vector3(x,.22f,z),new Vector3(.045f,.44f,.045f),wood);
            Part(sceneChair,"Seat",new Vector3(0,.45f,0),new Vector3(.52f,.055f,.5f),wood);
            bandageRoll=Group("Clean bandage roll",family+new Vector3(.8f,.6f,.3f));Part(bandageRoll,"Cotton",Vector3.zero,new Vector3(.075f,.1f,.075f),cloth,PrimitiveType.Cylinder);
            medicineArt=Group("Two reserved medicine packets",family);Part(medicineArt,"Wrapped medicine",Vector3.zero,new Vector3(.15f,.09f,.1f),cloth);medicineArt.gameObject.SetActive(false);
            hammerArt=Group("Dock repair hammer",sites["dock"].position);Part(hammerArt,"Handle",new Vector3(0,.1f,0),new Vector3(.035f,.32f,.035f),wood);Part(hammerArt,"Head",new Vector3(0,.26f,0),new Vector3(.21f,.075f,.065f),iron);hammerArt.gameObject.SetActive(false);
            mealRoot=Group("Meal on Lành porch",family+new Vector3(0,0,-1.1f));Part(mealRoot,"Woven floor mat",new Vector3(0,.03f,0),new Vector3(2,.025f,1.8f),cloth);Part(mealRoot,"Low tray",new Vector3(0,.42f,0),new Vector3(.85f,.045f,.65f),wood);
            fishBowl=Part(mealRoot,"Shared fish bowl",new Vector3(-.15f,.5f,0),new Vector3(.19f,.06f,.17f),cloth,PrimitiveType.Sphere);pot=Part(mealRoot,"Sour fish soup pot",new Vector3(.15f,.5f,0),new Vector3(.3f,.12f,.3f),iron,PrimitiveType.Cylinder);
            foreach(float z in new[]{-.75f,.75f})
            {
                var stool=Group("Meal wooden stool",Vector3.zero);stool.SetParent(mealRoot,false);stool.localPosition=new Vector3(0,0,z);
                Part(stool,"Seat",new Vector3(0,.43f,0),new Vector3(.5f,.055f,.42f),wood);
                foreach(float x in new[]{-.18f,.18f})foreach(float legZ in new[]{-.15f,.15f})Part(stool,"Leg",new Vector3(x,.215f,legZ),new Vector3(.045f,.43f,.045f),wood);
            }
            var lampPost=Group("Shielded porch oil lamp",family+new Vector3(-.55f,0,-1.5f));
            Part(lampPost,"Support",new Vector3(0,.3f,0),new Vector3(.065f,.6f,.065f),wood);
            Part(lampPost,"Oil reservoir",new Vector3(0,.7f,0),new Vector3(.15f,.06f,.15f),iron,PrimitiveType.Cylinder);
            Part(lampPost,"Lamp shade",new Vector3(0,.96f,0),new Vector3(.19f,.035f,.19f),iron,PrimitiveType.Cylinder);
            var wickMaterial=new Material(cloth){name="Map2 oil lamp wick"};wickMaterial.color=new Color(1,.6f,.15f);if(wickMaterial.HasProperty("_EmissionColor")){wickMaterial.EnableKeyword("_EMISSION");wickMaterial.SetColor("_EmissionColor",new Color(1,.4f,.05f)*2);}
            dayMaterials.Add(wickMaterial);dayLampFlame=Part(lampPost,"Flame",new Vector3(0,.84f,0),new Vector3(.025f,.08f,.025f),wickMaterial,PrimitiveType.Sphere);
            BuildFamilyFurniture(wood,cloth);
            restMat=Group("Night watch sleeping mat",sites["nightOneRest"].position);Part(restMat,"Woven reed mat",new Vector3(0,.035f,0),new Vector3(.8f,.035f,1.9f),cloth);Part(restMat,"Folded pillow",new Vector3(0,.09f,.7f),new Vector3(.42f,.13f,.28f),cloth);
        }
        void PrepareDayScene(Map02ChapterTask task)
        {
            dayNamPath=dayHungPath=System.Array.Empty<Vector3>();dayApproachDuration=0;
            if(task.phase>1)return;ReleaseDayFreePose();dayStaging=true;
            var hung=roles["Hùng"];daySceneHungOrigin=hung.transform.position;daySceneHungRotation=hung.transform.rotation;daySceneLanhOrigin=roles["Chị Lành"].transform.position;daySceneLanhRotation=roles["Chị Lành"].transform.rotation;
            if(task.id=="D1_02")sceneChair.position=hung.transform.position;
            if(task.id=="N1_01")
            {
                // Fixed two-seat porch layout, away from the traversal corridor.
                mealRoot.position=DayFamily+new Vector3(0,0,-1.1f);mealRoot.rotation=Quaternion.identity;
                dayNamPath=DayPath(Mission.Player.position,mealRoot.position+Vector3.back*.75f);dayHungPath=DayPath(hung.transform.position,mealRoot.position+Vector3.forward*.75f);dayApproachDuration=Mathf.Max(PathDuration(dayNamPath),PathDuration(dayHungPath));PrepareMealTiming();
            }
            if(task.id=="N1_01")BeginFamilyMeal();
            if(task.id=="N1_04"){dayNamPath=DayPath(Mission.Player.position,restMat.position+Vector3.left*1.0f+Vector3.back*.9f);dayHungPath=DayPath(hung.transform.position,restMat.position);dayApproachDuration=Mathf.Max(PathDuration(dayNamPath),PathDuration(dayHungPath));}
        }
        void SampleDayScene()
        {
            if(!dayStaging||sceneTask==null)return;var task=sceneTask;var hung=roles["Hùng"];var lanh=roles["Chị Lành"];
            if(task.id=="D1_02")
            {
                hung.BlendPerformance(DayClip("InjuredIdle"),DayClip("BandageHung"),sceneClock,Mathf.SmoothStep(0,1,sceneClock/2));lanh.SamplePerformance(DayClip("BandageLanh"),sceneClock,true);
                // Approach before touching; no changing root position after contact.
                var beside=daySceneHungOrigin+hung.transform.right*.75f+hung.transform.forward*.05f;
                lanh.transform.position=Vector3.Lerp(daySceneLanhOrigin,beside,Mathf.SmoothStep(0,1,sceneClock/2));lanh.transform.rotation=Quaternion.LookRotation(Vector3.ProjectOnPlane(daySceneHungOrigin-beside,Vector3.up));
                lanh.GroundPerformance(daySceneHungOrigin.y);
                if(bandageRoll!=null){bandageRoll.gameObject.SetActive(sceneClock>3&&sceneClock<19);var animator=lanh.visual?.GetComponentInChildren<Animator>();if(animator!=null&&animator.isHuman){var hand=animator.GetBoneTransform(HumanBodyBones.RightHand);bandageRoll.position=hand.position;}}
            }
            if(task.id=="D1_03"||task.id=="N1_03")
            {
                namPose?.Sample(DayClip("Medicine")??namIdle,Mathf.Min(sceneClock,4),false);
                medicineArt.gameObject.SetActive(sceneClock>=1.3f&&sceneClock<3.3f);if(medicineArt.gameObject.activeSelf){var hand=dayNamAnimator.GetBoneTransform(HumanBodyBones.RightHand);medicineArt.position=hand.position;medicineArt.rotation=hand.rotation;}
                if(sceneClock>=3.3f)Day.GiveMedicine(task.id=="D1_03"?0:1);
            }
            if((task.id=="N1_01"||task.id=="N1_04")&&sceneClock<dayApproachDuration){
                bool namArrived=sceneClock>=PathDuration(dayNamPath),hungArrived=sceneClock>=PathDuration(dayHungPath);
                namPose?.Sample(namArrived?(task.id=="N1_01"?DayClip("MealNam"):namIdle):ordinaryWalk,sceneClock,true);
                hung.SamplePerformance(hungArrived?(task.id=="N1_01"?DayClip("MealHung"):DayClip("InjuredIdle")):HungMapOneWalk,sceneClock,true);
                if(hungArrived&&task.id=="N1_01")hung.transform.rotation=Quaternion.Euler(0,180,0);SampleFamilyMeal();return;
            }
            if(task.id=="N1_01")
            {
                namPose?.Sample(DayClip("MealNam")??namIdle,sceneClock,true);hung.SamplePerformance(DayClip("MealHung"),sceneClock,true);
                SampleFamilyMeal();
                // Short seated approach happens under the start of the wide shot.
                hung.transform.rotation=Quaternion.LookRotation(-mealRoot.forward);
                fishBowl.localPosition=new Vector3(-.15f,.5f,Mathf.Lerp(0,.18f,Mathf.SmoothStep(0,1,sceneClock/4)));
            }
            if(task.id=="N1_04")
            {
                hung.SamplePerformance(DayClip("RestHung"),Mathf.Min(sceneClock-dayApproachDuration,4),false);
                namPose?.Sample(DayClip("RestNam")??namIdle,Mathf.Min(sceneClock-dayApproachDuration,4),false);
            }
        }
        Vector3 ClearStandingPoint(Vector3 origin,Vector3 avoid)
        {
            Vector3 best=origin;float score=float.NegativeInfinity;
            for(int i=0;i<8;i++){
                var direction=Quaternion.Euler(0,i*45,0)*Vector3.forward;
                if(!NavMesh.SamplePosition(origin+direction*1.25f,out var hit,.45f,NavMesh.AllAreas))continue;
                float candidate=Vector3.Distance(hit.position,avoid);
                foreach(var actor in roles.Values)if(actor.role!="Hùng")candidate=Mathf.Min(candidate,Vector3.Distance(hit.position,actor.transform.position));
                if(candidate>score){score=candidate;best=hit.position;}
            }
            return best;
        }
        void ReleaseDayScene()
        {
            if(!dayStaging)return;
            if(sceneTask?.id=="D1_02"){
                roles["Chị Lành"].StopAt(daySceneLanhOrigin);roles["Chị Lành"].transform.rotation=daySceneLanhRotation;
                // Clear the seat before returning Hùng to standing gameplay.
                roles["Hùng"].StopAt(ClearStandingPoint(daySceneHungOrigin,Mission.Player.position));
            }
            if(sceneTask?.id=="N1_01"){
                // Standing must clear the stools and the shared tray, not pop through their seats.
                var namExit=ClearStandingPoint(mealRoot.position+Vector3.back*.75f,mealRoot.position);
                Mission.Player.position=namExit;lockedPosition=namExit;
                roles["Hùng"].StopAt(ClearStandingPoint(mealRoot.position+Vector3.forward*.75f,namExit));
            }
            EndFamilyMeal();dayStaging=false;dayNamPath=dayHungPath=System.Array.Empty<Vector3>();foreach(var actor in roles.Values)actor.ReleasePerformance();
            if(medicineArt!=null)medicineArt.gameObject.SetActive(false);if(bandageRoll!=null)bandageRoll.gameObject.SetActive(false);
        }
        void DayLateUpdate()
        {
            if(!ready||Mission.Paused||ForestMenu.Visible)return;
            if(sceneTask!=null)return;
            bool carry=State.carry=="D1_04";bool hammer=dayActivity==DayActivity.Dock&&Day.stakeHits<6;
            if(carry)
            {
                dayNamPose?.Dispose();dayNamPose=null;
                cargoPose??=new Map02UpperBodyPose(dayNamAnimator,DayClip("Carry"));
                cargoPose.Sample(player.IsMoving?ordinaryWalk:dayNamAnimator.runtimeAnimatorController.animationClips.FirstOrDefault(c=>c.name=="Idle_Tuned"),Time.time);
                player.SetFaceCameraYaw(false);
                int i=State.Count("D1_04");if(i<physicalCrates.Count){var crate=physicalCrates[i];crate.gameObject.SetActive(true);var l=dayNamAnimator.GetBoneTransform(HumanBodyBones.LeftHand);var r=dayNamAnimator.GetBoneTransform(HumanBodyBones.RightHand);crate.position=(l.position+r.position)*.5f-Vector3.up*.25f+Mission.Player.forward*.15f;crate.rotation=Mission.Player.rotation;}
            }
            else if(hammer)
            {
                FreeNamPose(DayClip("Hammer"),dayClock);var hand=dayNamAnimator.GetBoneTransform(HumanBodyBones.RightHand);hammerArt.gameObject.SetActive(true);hammerArt.SetPositionAndRotation(hand.position,hand.rotation);
            }
            else{ReleaseDayFreePose();if(hammerArt!=null)hammerArt.gameObject.SetActive(false);}
        }
    }
}
