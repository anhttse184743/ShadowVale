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
        AnimationClip ordinaryWalk,namCalmIdle;   // read once: a scene's pose takes Nam's controller away while it plays
        Animator dayNamAnimator;
        Transform medicineArt,hammerArt,mealRoot,restMat;
        Transform sceneChair,bandageRoll,fishBowl,pot,dayLampFlame;
        Vector3 daySceneHungOrigin,daySceneLanhOrigin;
        Quaternion daySceneHungRotation,daySceneLanhRotation;
        bool dayStaging;
        Vector3[] dayNamPath=System.Array.Empty<Vector3>(),dayHungPath=System.Array.Empty<Vector3>();
        float dayApproachDuration;float[] mealDurations;bool mealBegan;
        float DaySceneMinimum=>sceneTask?.id=="N1_04"?dayApproachDuration+12:sceneTask?.id=="N1_01"?dayApproachDuration+(mealDurations?.Sum()??90):sceneTask?.seconds??0;
        // Silence after each supper line: the rice-bowl exchange, "u tôi", the plan, the joke and "Nghe hết đó nha" (Hùng ducks,
        // Nam laughs), "Quê tôi... không còn ai đợi" (Hùng puts the chopsticks down), and the last line held 3 s with the crickets.
        void PrepareMealTiming(){float[] pauses={.8f,.8f,1,2.5f,.8f,1,1,.6f,.9f,1.6f,.7f,2.2f,4,2.5f,3,3.5f};mealDurations=lines.Select((line,i)=>(Map02DialogueCatalog.HasApprovedRecording(line)?DialogueVoice.Length(line.id):Mathf.Max(2.8f,line.text.Length/11f))+(i<pauses.Length?pauses[i]:1)).ToArray();float extra=Mathf.Max(0,90-mealDurations.Sum());float weights=pauses.Take(mealDurations.Length-1).Sum();for(int i=0;i<mealDurations.Length-1;i++)mealDurations[i]+=extra*pauses[i]/weights;mealBegan=false;}
        float DayLineDuration(float normal)=>sceneTask?.id=="N1_01"&&mealDurations!=null&&lineIndex<mealDurations.Length?mealDurations[lineIndex]:normal;
        AudioSource mealCrickets;bool mealCricketsPaused;
        // A quiet field ambience fills only the final hold; it never runs under the spoken scene or survives a skip/load.
        void TickMealCrickets()
        {
            if(sceneTask?.id!="N1_01"||!mealBegan||lineIndex!=lines.Length-1||voice.HasSpeech)return;
            float afterVoice=lineClock-DialogueVoice.Length(lines[lineIndex].RecordingId);
            if(afterVoice<=0)return;
            if(mealCrickets==null)
            {
                var clip=Resources.Load<AudioClip>("Map02DayOne/crickets");if(clip==null)return;
                var ambience=new GameObject("Porch crickets — Lời hứa");ambience.transform.SetParent(transform,false);
                mealCrickets=ambience.AddComponent<AudioSource>();mealCrickets.playOnAwake=false;mealCrickets.spatialBlend=0;mealCrickets.loop=true;mealCrickets.clip=clip;
            }
            float remaining=DayLineDuration(0)+PauseAfter(lineIndex)-lineClock;
            mealCrickets.volume=.18f*Mathf.Min(Mathf.Clamp01(afterVoice/.6f),Mathf.Clamp01(remaining/.4f));
            if(!mealCrickets.isPlaying)mealCrickets.Play();
        }
        void PauseMealCrickets(bool paused)
        {
            if(mealCrickets==null)return;
            if(paused&&!mealCricketsPaused&&mealCrickets.isPlaying){mealCrickets.Pause();mealCricketsPaused=true;}
            else if(!paused&&mealCricketsPaused){mealCrickets.UnPause();mealCricketsPaused=false;}
        }
        void StopMealCrickets(){if(mealCrickets!=null){mealCrickets.Stop();mealCrickets.volume=0;}mealCricketsPaused=false;}
        bool HoldMealVoice=>sceneTask?.id=="N1_01"&&!mealBegan&&sceneClock<dayApproachDuration;
        void TickMealVoice(){if(sceneTask?.id=="N1_01"&&!mealBegan&&sceneClock>=dayApproachDuration){mealBegan=true;Speak();}}
        Vector3[] DayPath(Vector3 start,Vector3 goal){var path=new NavMeshPath();if(NavMesh.SamplePosition(goal,out var h,1.5f,NavMesh.AllAreas)&&NavMesh.CalculatePath(start,h.position,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete)return path.corners;Debug.LogError("Map 2 day-one seating path is blocked. Adjust the performance anchor.",this);return new[]{start};}
        static float PathDuration(Vector3[] path){float length=0;for(int i=1;i<path.Length;i++)length+=Vector3.Distance(path[i-1],path[i]);return length/1.25f;}
        void MoveDaySceneRoots()
        {
            if(sceneTask==null||dayNamPath.Length==0)return;lockedPosition=Along(dayNamPath,sceneClock*1.25f);var ahead=Along(dayNamPath,(sceneClock+.15f)*1.25f)-lockedPosition;if(ahead.sqrMagnitude>.001f)lockedRotation=Quaternion.LookRotation(Vector3.ProjectOnPlane(ahead,Vector3.up));if(sceneClock>=PathDuration(dayNamPath))lockedRotation=sceneTask.id=="N1_01"?Quaternion.LookRotation(mealRoot.forward):Quaternion.identity;
            var hung=roles["Hùng"];hung.SceneMove(Along(dayHungPath,sceneClock*1.25f),Along(dayHungPath,(sceneClock+.2f)*1.25f),sceneClock<dayApproachDuration?1.25f:0);if(sceneClock>=dayApproachDuration)hung.transform.rotation=sceneTask.id=="N1_01"?Quaternion.LookRotation(-mealRoot.forward):Quaternion.identity;
        }
        AnimationClip DayClip(string id)=>dayArt!=null?dayArt.Find(id):null;
        AnimationClip HungMapOneWalk=>hungMapOneWalk;
        AnimationClip hungMapOneWalk;
        void LoadDayPerformances()
        {
            dayArt=Resources.Load<Map02DayOneArt>("Story/Map02DayOneArt");dayNamAnimator=Mission.Player.GetComponentInChildren<Animator>();ordinaryWalk=dayNamAnimator.runtimeAnimatorController.animationClips.FirstOrDefault(c=>c.name=="Walk_Tuned");namCalmIdle=dayNamAnimator.runtimeAnimatorController.animationClips.FirstOrDefault(c=>c.name=="Idle_Tuned");
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
            // "Lời hứa" (doc): Nam and Hùng alone at a tray on chị Lành's porch. Local z runs along the doorway, local x out to the yard.
            FindPorch();mealRoot=Group("Supper on Lành's porch",PorchMeal);mealRoot.rotation=Quaternion.LookRotation(porchAlong);
            Part(mealRoot,"Woven floor mat",new Vector3(0,.03f,0),new Vector3(1.5f,.025f,2.2f),cloth);Part(mealRoot,"Low tray",new Vector3(0,.42f,0),new Vector3(.85f,.045f,.65f),wood);
            fishBowl=Part(mealRoot,"Shared fish bowl",new Vector3(-.15f,.5f,0),new Vector3(.19f,.06f,.17f),cloth,PrimitiveType.Sphere);pot=Part(mealRoot,"Sour fish soup pot",new Vector3(.15f,.5f,0),new Vector3(.3f,.12f,.3f),iron,PrimitiveType.Cylinder);
            foreach(float z in new[]{-MealSeat,MealSeat})
            {
                var stool=Group("Meal wooden stool",Vector3.zero);stool.SetParent(mealRoot,false);stool.localPosition=new Vector3(0,0,z);
                Part(stool,"Seat",new Vector3(0,.43f,0),new Vector3(.5f,.055f,.42f),wood);
                foreach(float x in new[]{-.18f,.18f})foreach(float legZ in new[]{-.15f,.15f})Part(stool,"Leg",new Vector3(x,.215f,legZ),new Vector3(.045f,.43f,.045f),wood);
            }
            // A small shielded lamp on the mat between the doorway and the tray (doc: low, lighting the tray and little else).
            var lampPost=Group("Shielded porch oil lamp",Vector3.zero);lampPost.SetParent(mealRoot,false);lampPost.localPosition=new Vector3(-.42f,.04f,.05f);
            Part(lampPost,"Oil reservoir",new Vector3(0,.05f,0),new Vector3(.15f,.05f,.15f),iron,PrimitiveType.Cylinder);
            Part(lampPost,"Lamp shade",new Vector3(0,.3f,0),new Vector3(.19f,.035f,.19f),iron,PrimitiveType.Cylinder);
            var wickMaterial=new Material(cloth){name="Map2 oil lamp wick"};wickMaterial.color=new Color(1,.6f,.15f);if(wickMaterial.HasProperty("_EmissionColor")){wickMaterial.EnableKeyword("_EMISSION");wickMaterial.SetColor("_EmissionColor",new Color(1,.4f,.05f)*2);}
            dayMaterials.Add(wickMaterial);dayLampFlame=Part(lampPost,"Flame",new Vector3(0,.17f,0),new Vector3(.025f,.08f,.025f),wickMaterial,PrimitiveType.Sphere);
            BuildFamilyFurniture(wood,cloth);
            // Hùng is the signaller: the set rides on his back through Day 1 and the night patrol (Tí stares at it; "vác máy").
            hungRadio=Group("Hùng's radio set — Day 1 to Night 1",Vector3.zero);Part(hungRadio,"Set",Vector3.zero,new Vector3(.28f,.27f,.11f),iron);
            Part(hungRadio,"Whip antenna",new Vector3(-.1f,.58f,0),new Vector3(.012f,.45f,.012f),iron,PrimitiveType.Cylinder);hungRadio.gameObject.SetActive(false);
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
                // Two seats facing each other along the doorway; Nam on the near side of the tray, Hùng beyond it.
                mealRoot.SetPositionAndRotation(PorchMeal,Quaternion.LookRotation(porchAlong));fishBowl.localPosition=new Vector3(-.15f,.5f,0);
                dayNamPath=DayPath(Mission.Player.position,mealRoot.position-mealRoot.forward*MealSeat);dayHungPath=DayPath(hung.transform.position,mealRoot.position+mealRoot.forward*MealSeat);dayApproachDuration=Mathf.Max(PathDuration(dayNamPath),PathDuration(dayHungPath));PrepareMealTiming();
            }
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
                if(hungArrived&&task.id=="N1_01")hung.transform.rotation=Quaternion.LookRotation(-mealRoot.forward);return;
            }
            if(task.id=="N1_01")
            {
                namPose?.Sample(DayClip("MealNam")??namIdle,sceneClock,true);hung.SamplePerformance(DayClip("MealHung"),sceneClock,true);
                // Short seated approach happens under the start of the wide shot.
                hung.transform.rotation=Quaternion.LookRotation(-mealRoot.forward);
                fishBowl.localPosition=new Vector3(-.15f,.5f,Mathf.Lerp(0,.18f,Mathf.SmoothStep(0,1,(sceneClock-dayApproachDuration)/4)));   // pushed once both are seated
            }
            if(task.id=="N1_04")
            {
                hung.SamplePerformance(DayClip("RestHung"),Mathf.Min(sceneClock-dayApproachDuration,4),false);
                namPose?.Sample(DayClip("RestNam")??namIdle,Mathf.Min(sceneClock-dayApproachDuration,4),false);
            }
        }
        // Chị Lành's veranda lies between the front posts and the doorway of the house beside the family site.
        // Read from that house's own door jambs, so the supper follows the house if the village is re-laid.
        Vector3 porchDoor,porchAlong,porchOut;bool porchKnown;Transform hungRadio;
        // The middle of the veranda in front of the doorway, a little off the curtain that hangs on the doorway's north jamb.
        Vector3 PorchMeal=>porchDoor+porchOut+porchAlong*.28f;
        const float MealSeat=.7f;   // each stool from the middle of the tray
        void FindPorch()
        {
            if(porchKnown)return;porchKnown=true;
            porchDoor=new Vector3(-46.99f,1.21f,-35.08f);porchAlong=new Vector3(.393f,0,-.919f);porchOut=new Vector3(-.919f,0,-.393f);   // surveyed 23/10
            var jambs=FindObjectsByType<Collider>(FindObjectsSortMode.None).Where(c=>c.name=="Door jamb"&&Vector3.Distance(c.bounds.center,DayFamily)<8).OrderBy(c=>Vector3.Distance(c.bounds.center,DayFamily)).Take(2).ToArray();
            if(jambs.Length<2)return;
            var along=Vector3.ProjectOnPlane(jambs[1].bounds.center-jambs[0].bounds.center,Vector3.up).normalized;var outward=Vector3.Cross(Vector3.up,along);
            if(Vector3.Dot(outward,DayFamily-jambs[0].bounds.center)<0)outward=-outward;
            var door=(jambs[0].bounds.center+jambs[1].bounds.center)*.5f;
            if(!Physics.Raycast(door+outward,Vector3.down,out var deck,4,~0,QueryTriggerInteraction.Ignore))return;
            porchDoor=new Vector3(door.x,deck.point.y,door.z);porchAlong=along;porchOut=outward;
        }
        // "Lời hứa" is framed from inside the house, through the doorway (doc), with two closer shots: the shoulder bump and
        // the joke, and Nam's "Quê tôi... không còn ai đợi". Every shot stays on the house side of the line through the two men.
        void MealShot(out Vector3 eye,out Vector3 focus,out float fov)
        {
            string id=sceneClock<dayApproachDuration||lines==null?"":lines[lineIndex].id;
            if(id=="m2v3_promise_05"||id=="m2v3_promise_05b"||id=="m2v3_promise_07"){eye=PorchMeal-porchOut*2.3f+Vector3.up*1.1f;focus=mealRoot.position+Vector3.up*.75f;fov=44;return;}
            if(id=="m2v3_promise_11"){var nam=Mission.Player.position;eye=nam+mealRoot.forward*1.2f-porchOut*.85f+Vector3.up*1.05f;focus=nam+Vector3.up*1.0f;fov=44;return;}
            eye=PorchMeal-porchOut*4.6f+Vector3.up*1.15f;focus=mealRoot.position+Vector3.up*.6f;fov=34;
        }
        void UpdateHungRadio()
        {
            if(hungRadio==null)return;var hung=roles["Hùng"];
            bool worn=State.phase<=1&&sceneTask?.id!="N1_01"&&sceneTask?.id!="N1_04"&&hung.visual!=null&&hung.visual.gameObject.activeInHierarchy;   // off for supper and for sleep
            if(hungRadio.gameObject.activeSelf!=worn)hungRadio.gameObject.SetActive(worn);if(!worn)return;
            var animator=hung.visual.GetComponentInChildren<Animator>();var chest=animator!=null&&animator.isHuman?animator.GetBoneTransform(HumanBodyBones.Chest)??animator.GetBoneTransform(HumanBodyBones.Spine):null;
            var back=chest!=null?chest.position:hung.transform.position+Vector3.up*1.25f;var facing=BodyForward(animator,hung.transform);
            if(radioBack<0)radioBack=BackDepth(hung.visual,back,facing);
            hungRadio.SetPositionAndRotation(back-facing*(radioBack+.065f)+Vector3.down*.04f,Quaternion.LookRotation(facing));
        }
        float radioBack=-1;
        /// <summary>How far behind the chest bone the model's back surface lies, measured once on the skinned mesh.</summary>
        static float BackDepth(Transform model,Vector3 chest,Vector3 facing)
        {
            float depth=.14f;var baked=new Mesh();
            foreach(var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                skin.BakeMesh(baked);foreach(var v in baked.vertices){var p=skin.transform.TransformPoint(v);if(Mathf.Abs(p.y-chest.y)<.12f&&Vector3.ProjectOnPlane(p-chest,Vector3.up).magnitude<.4f)depth=Mathf.Max(depth,-Vector3.Dot(p-chest,facing));}
            }
            Destroy(baked);return Mathf.Min(depth,.3f);
        }
        /// <summary>Which way the body itself faces, from the shoulders: an actor slot and its model need not agree.</summary>
        public static Vector3 BodyForward(Animator body,Transform fallback)
        {
            var left=body!=null&&body.isHuman?body.GetBoneTransform(HumanBodyBones.LeftUpperArm):null;var right=body!=null&&body.isHuman?body.GetBoneTransform(HumanBodyBones.RightUpperArm):null;
            var facing=left!=null&&right!=null?Vector3.Cross(right.position-left.position,Vector3.up):Vector3.zero;
            return facing.sqrMagnitude>.0001f?facing.normalized:Vector3.ProjectOnPlane(fallback.forward,Vector3.up).normalized;
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
            StopMealCrickets();
            if(!dayStaging)return;
            if(sceneTask?.id=="D1_02"){
                roles["Chị Lành"].StopAt(daySceneLanhOrigin);roles["Chị Lành"].transform.rotation=daySceneLanhRotation;
                // Clear the seat before returning Hùng to standing gameplay.
                roles["Hùng"].StopAt(ClearStandingPoint(daySceneHungOrigin,Mission.Player.position));
            }
            if(sceneTask?.id=="N1_01"){
                // Standing must clear the stools and the shared tray, not pop through their seats.
                var namExit=ClearStandingPoint(mealRoot.position-mealRoot.forward*MealSeat,mealRoot.position);
                Mission.Player.position=namExit;lockedPosition=namExit;
                roles["Hùng"].StopAt(ClearStandingPoint(mealRoot.position+mealRoot.forward*MealSeat,namExit));
            }
            dayStaging=false;dayNamPath=dayHungPath=System.Array.Empty<Vector3>();foreach(var actor in roles.Values)actor.ReleasePerformance();
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
