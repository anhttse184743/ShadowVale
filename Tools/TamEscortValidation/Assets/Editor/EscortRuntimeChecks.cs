using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using ShadowVale.Map01;
using ShadowVale.Gameplay.Combat;
using ShadowVale.Gameplay.Player;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Object=UnityEngine.Object;

public static class EscortRuntimeChecks
{
    static readonly List<string> results=new();
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);results.Add("PASS "+message);}
    static void Set(object target,string name,object value)=>target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(target,value);
    static void Property(object target,string name,object value)=>target.GetType().GetProperty(name).SetValue(target,value);
    public static void Run()
    {
        ScoutRadioWarning();TamClips();StateRoundTrip();ReloadAndEquipment();SpawnPositions();VisualStates();
        File.WriteAllLines("runtime-checks.txt",results);Debug.Log(string.Join("\n",results));
    }
    static void ScoutRadioWarning()
    {
        const string id="m2v3_scoutescort_warning_01";
        var line=Map02ChapterDefinition.Load().entries.Single(e=>e.id==id);
        Check(line.radio&&line.speaker=="Anh Thành"&&line.text=="Đồng chí đưa thương binh về cẩn thận, địch đã phát hiện và bố trí lực lượng đánh chặn","Thành radio warning uses exact requested text");
        Check(Map02DialogueCatalog.HasApprovedRecording(line),"new warning resolves exact caption, radio channel and imported audio");
        Check(DialogueVoice.Length(id)>5&&DialogueVoice.Length(id)<8,"new warning has valid spoken duration");
        var root=new GameObject("Scout radio test");try
        {
            var chapter=root.AddComponent<Map02ChapterDirector>();chapter.enabled=false;
            var queue=typeof(Map02ChapterDirector).GetMethod("QueueScoutWarning",BindingFlags.Instance|BindingFlags.NonPublic);
            Check((bool)queue.Invoke(chapter,null)&&chapter.HasBark(id)&&!chapter.Busy,"rescue warning queues without blocking movement");
            Check(!(bool)queue.Invoke(chapter,null),"rescue warning queues only once");
            var voice=DialogueVoice.For(root);if(root.GetComponent<AudioSource>()==null)typeof(DialogueVoice).GetMethod("Awake",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(voice,null);Set(chapter,"voice",voice);
            typeof(Map02ChapterDirector).GetMethod("TickBark",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(chapter,new object[]{.01f});
            Check(chapter.CurrentBark?.id==id&&voice.CurrentId==id,"rescue radio bark starts the new voice clip with its caption");
            var saved=JsonUtility.FromJson<Map02ChapterState>(JsonUtility.ToJson(chapter.State));
            Property(chapter,"State",saved);Check(!(bool)queue.Invoke(chapter,null),"save/load does not repeat rescue radio warning");
        }finally{Object.DestroyImmediate(root);}
    }
    public static void RenderPose()
    {
        var nam=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Characters/Player/Player.fbx"));
        var tam=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Characters/NPCs/Anh Tâm.fbx"));
        Resources.Load<AnimationClip>("Story/TamEscort/NamIdle").SampleAnimation(nam,0);
        Resources.Load<AnimationClip>("Story/TamEscort/TamIdle").SampleAnimation(tam,0);
        nam.transform.position=Vector3.down*.115f;tam.transform.position=Vector3.left*.48f+Vector3.forward*.02f+Vector3.down*.015f;
        foreach(var pair in new[]{(nam,new Color(.25f,.32f,.22f)),(tam,new Color(.39f,.45f,.33f))})
            foreach(var renderer in pair.Item1.GetComponentsInChildren<Renderer>())
            {var material=new Material(Shader.Find("Standard"));material.color=pair.Item2;renderer.sharedMaterials=renderer.sharedMaterials.Select(_=>material).ToArray();}
        var floor=GameObject.CreatePrimitive(PrimitiveType.Plane);floor.transform.localScale=Vector3.one*3;
        var floorMat=new Material(Shader.Find("Standard"));floorMat.color=new Color(.18f,.2f,.17f);floor.GetComponent<Renderer>().sharedMaterial=floorMat;
        RenderSettings.ambientLight=new Color(.65f,.65f,.65f);var light=new GameObject("Light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.6f;light.transform.rotation=Quaternion.Euler(45,-30,0);
        var camera=new GameObject("Review camera").AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=1.15f;camera.backgroundColor=new Color(.16f,.18f,.16f);camera.clearFlags=CameraClearFlags.SolidColor;
        var texture=new RenderTexture(850,850,24);camera.targetTexture=texture;
        foreach(var item in new[]{("unity-front.png",new Vector3(2,1.8f,4)),("unity-back.png",new Vector3(-2,1.9f,-4))})
        {camera.transform.position=item.Item2;camera.transform.LookAt(new Vector3(-.24f,.9f,0));camera.Render();RenderTexture.active=texture;var png=new Texture2D(850,850,TextureFormat.RGB24,false);png.ReadPixels(new Rect(0,0,850,850),0,0);png.Apply();File.WriteAllBytes(item.Item1,png.EncodeToPNG());Object.DestroyImmediate(png);}
        RenderTexture.active=null;camera.targetTexture=null;Object.DestroyImmediate(texture);
        Debug.Log("PASS native Unity front/back pose render");
    }
    static void TamClips()
    {
        foreach(string who in new[]{"Nam","Tam"})foreach(string take in new[]{"Idle","Walk"})
        {
            var clip=Resources.Load<AnimationClip>("Story/TamEscort/"+who+take);Check(clip!=null,who+take+" loaded from runtime Resources");
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(who=="Nam"?"Assets/_Project/Art/Characters/Player/Player.fbx":"Assets/_Project/Art/Characters/NPCs/Anh Tâm.fbx");
            var actor=Object.Instantiate(prefab);try
            {
                var bones=actor.GetComponentsInChildren<Transform>().ToDictionary(t=>t.name,t=>t);
                float shin=Vector3.Distance(bones["LeftLeg"].position,bones["LeftFoot"].position);
                for(int i=0;i<=18;i++)
                {
                    clip.SampleAnimation(actor,clip.length*i/18);
                    float length=Vector3.Distance(bones["LeftLeg"].position,bones["LeftFoot"].position);
                    Check(Mathf.Abs(length-shin)<.005f,who+take+" shin length preserved at sample "+i);
                    Check(bones["Head"].position.y>1&&bones["Head"].position.y<2,who+take+" head at human height");
                }
                Debug.Log(who+take+" left/right shoulders "+bones["LeftArm"].position+" "+bones["RightArm"].position+" toes="+bones["LeftToeBase"].position);
            }finally{Object.DestroyImmediate(actor);}
        }
    }
    static void StateRoundTrip()
    {
        var definition=Map02ChapterDefinition.Load();var state=new Map02ChapterState{phase=2,activeTask="D2_04",trackedTask="D2_04",scoutPistolIssued=true,scoutAmbushSpawned=3};
        state.completed=definition.tasks.Where(t=>t.phase<2||new[]{"D2_01","D2_02","D2_03"}.Contains(t.id)).Select(t=>t.id).ToList();
        state.enemies=new List<Map02ChapterState.UnitSnapshot>{new(){id="a",group=Map02ChapterDirector.ScoutAmbushGroup,health=0},new(){id="b",group=Map02ChapterDirector.ScoutAmbushGroup,health=0},new(){id="c",group=Map02ChapterDirector.ScoutAmbushGroup,health=100}};
        var saved=JsonUtility.FromJson<Map02ChapterState>(JsonUtility.ToJson(state));saved.Validate();
        Check(saved.scoutPistolIssued&&saved.scoutAmbushSpawned==3&&saved.enemies.Count(e=>e.health>0)==1,"save/load retains issued K54, two deaths and exactly one living ambush enemy");
        for(int n=0;n<5;n++){Check(Map02ChapterDirector.CanSpawnScoutAmbush(n,false),"next soldier allowed after kill "+n);Check(!Map02ChapterDirector.CanSpawnScoutAmbush(n,true),"living soldier prevents another spawn "+n);}
        Check(!Map02ChapterDirector.CanSpawnScoutAmbush(5,false),"no sixth soldier");
        saved.scoutAmbushSpawned=6;bool rejected=false;try{saved.Validate();}catch(InvalidDataException){rejected=true;}Check(rejected,"invalid ambush save rejected");
        var delayed=new Map02ChapterState{scoutRescueOriginSet=true,scoutRescueOrigin=new Vector3(10,1,20)};
        Check(!delayed.TryStartScoutAmbush(new Vector3(10,25,20)),"vertical displacement never starts ambush");
        Check(!delayed.TryStartScoutAmbush(new Vector3(13.99f,1,20)),"no ambush inside rescue radius of 4 metres");
        delayed=JsonUtility.FromJson<Map02ChapterState>(JsonUtility.ToJson(delayed));
        Check(!delayed.TryStartScoutAmbush(new Vector3(12.8f,1,22.8f)),"save/load inside 4 metre radius stays unarmed");
        Check(delayed.TryStartScoutAmbush(new Vector3(14,1,20)),"first ambush starts at horizontal displacement of 4 metres");
        delayed=JsonUtility.FromJson<Map02ChapterState>(JsonUtility.ToJson(delayed));
        Check(delayed.TryStartScoutAmbush(delayed.scoutRescueOrigin),"activated ambush remains active across save/load and return to rescue point");
    }
    static void ReloadAndEquipment()
    {
        Check(Mathf.Abs(Map02ChapterDirector.ScoutAuthoredSpeed-2.5f)<.00001f,"source gait keeps its authored distance calibration");
        Check(Map02ScoutEscortVisual.ReloadLowering(0)==0&&Map02ScoutEscortVisual.ReloadLowering(.5f)==1&&Map02ScoutEscortVisual.ReloadLowering(1)==0,"reload lowers, holds, raises on normal normalized magazine progress");
        var root=new GameObject("Equipment test");var pawn=new GameObject("Pawn");pawn.transform.SetParent(root.transform);
        try
        {
            var mission=root.AddComponent<Map02Mission>();mission.enabled=false;var motor=pawn.AddComponent<PlayerController>();var health=pawn.AddComponent<Health>();health.SetMaxHealth(100);
            var combat=pawn.AddComponent<PlayerCombat>();typeof(PlayerCombat).GetMethod("Awake",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(combat,null);
            Set(mission,"player",motor);Set(mission,"combat",combat);Set(mission,"health",health);Property(mission,"Ready",true);
            var gun=new GameObject("K54");gun.transform.SetParent(pawn.transform);var weapon=gun.AddComponent<Weapon>();weapon.Configure(WeaponKind.Pistol,25,35,3,false,gun.transform);
            combat.RegisterWeapon(weapon,new WeaponTuning{magazineCapacity=8,reloadSeconds=2.4f});
            var bag=root.AddComponent<Map01Inventory>();typeof(Map01Inventory).GetMethod("Awake",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(bag,null);
            bag.PrepareScoutPistol();bag.PrepareScoutPistol();Check(bag.Count("pistol_k54")==1&&combat.EquippedKind==WeaponKind.Pistol,"quest equips one K54 and repeated preparation never duplicates it");
            combat.EquipAllowed=k=>k==WeaponKind.Pistol;Check(!combat.Equip(WeaponKind.Rifle)&&!combat.Equip(WeaponKind.Knife),"weapon gate rejects rifle and knife");
            Check(Mathf.Abs(combat.BaseReloadSeconds-2.4f)<.0001f,"escort presentation preserves configured normal reload duration");
            combat.ReserveRounds=()=>24;Check(combat.TryReload()&&combat.IsReloading,"normal magazine reload still starts");combat.CancelReload();
        }finally{Object.DestroyImmediate(root);}
    }
    static void SpawnPositions()
    {
        Check(!Map02ChapterDirector.ScoutAmbushDistanceClear(new Vector3(20.1f,0,0),Vector3.zero,new Vector3(.48f,0,0)),"spawn outside 20 m of Nam is rejected if too close to Tam");
        Check(Map02ChapterDirector.ScoutAmbushDistanceClear(new Vector3(20.5f,0,0),Vector3.zero,new Vector3(.48f,0,0)),"spawn requires at least 20 m from both Nam and Tam");
        Check(!Map02ChapterDirector.ScoutAmbushDistanceClear(new Vector3(19,100,0),Vector3.zero,Vector3.zero),"spawn separation uses horizontal ground distance");
        var settings=NavMesh.GetSettingsByIndex(0);var data=NavMeshBuilder.BuildNavMeshData(settings,new List<NavMeshBuildSource>{new(){shape=NavMeshBuildSourceShape.Box,size=new Vector3(100,.1f,100),transform=Matrix4x4.TRS(Vector3.down*.05f,Quaternion.identity,Vector3.one)}},new Bounds(Vector3.zero,new Vector3(100,10,100)),Vector3.zero,Quaternion.identity);var instance=NavMesh.AddNavMeshData(data);
        var root=new GameObject("Spawn positions");var pawn=new GameObject("Pawn");try
        {
            var motor=pawn.AddComponent<PlayerController>();var mission=root.AddComponent<Map02Mission>();mission.enabled=false;Set(mission,"player",motor);
            var chapter=root.AddComponent<Map02ChapterDirector>();Property(chapter,"Mission",mission);Set(chapter,"player",motor);
            var sites=(Dictionary<string,Transform>)typeof(Map02ChapterDirector).GetField("sites",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(chapter);
            foreach(var item in new[]{("command",new Vector3(0,0,35)),("scout",new Vector3(0,0,-25)),("bridge",new Vector3(0,0,5))}){var site=new GameObject(item.Item1);site.transform.SetParent(root.transform);site.transform.position=item.Item2;sites[item.Item1]=site.transform;}
            var method=typeof(Map02ChapterDirector).GetMethod("TryScoutAmbushPoint",BindingFlags.Instance|BindingFlags.NonPublic);
            var deck=new GameObject("Walkable bridge deck");deck.transform.SetParent(root.transform);deck.transform.position=new Vector3(0,0,20);deck.transform.rotation=Quaternion.Euler(0,90,0);deck.AddComponent<BoxCollider>().size=new Vector3(12,.2f,4);
            var fixedPoints=new List<Vector3>();
            for(int i=0;i<5;i++){object[] args={i,Vector3.zero};Check((bool)method.Invoke(chapter,args),"valid reachable spawn point "+i);var p=(Vector3)args[1];fixedPoints.Add(p);Check(p.z<12,"spawn stays on field bank before bridge "+i);Check(i%2==0?p.x<= -19.5f:p.x>=19.5f,"field side alternates left/right for soldier "+i);if(i>0)Check(p.z>fixedPoints[i-1].z,"ambush points progress towards camp "+i);}

            pawn.transform.position=new Vector3(20,0,20);
            for(int i=0;i<5;i++){object[] args={i,Vector3.zero};Check((bool)method.Invoke(chapter,args)&&Vector3.Distance((Vector3)args[1],fixedPoints[i])<.001f,"moving player cannot move fixed spawn "+i);}
            var saved=JsonUtility.FromJson<Map02ChapterState>(JsonUtility.ToJson(chapter.State));
            Check(saved.scoutAmbushPoints.SequenceEqual(fixedPoints),"save/load retains all five fixed ambush points");
            var cross=typeof(Map02ChapterDirector).GetMethod("UpdateScoutBridgeCrossing",BindingFlags.Instance|BindingFlags.NonPublic);
            Check(!(bool)cross.Invoke(chapter,new object[]{new Vector3(0,0,13)}),"field approach keeps ambush available before bridge");
            Check((bool)cross.Invoke(chapter,new object[]{new Vector3(0,0,15)})&&chapter.State.scoutAmbushPassedBridge,"stepping onto bridge permanently stops further ambush spawns");
            Check(!(bool)cross.Invoke(chapter,new object[]{new Vector3(0,0,0)})&&chapter.State.scoutAmbushPassedBridge,"returning to field does not restart stopped ambush");
            saved=JsonUtility.FromJson<Map02ChapterState>(JsonUtility.ToJson(chapter.State));Check(saved.scoutAmbushPassedBridge,"bridge cutoff persists through save/load");
        }finally{Object.DestroyImmediate(root);Object.DestroyImmediate(pawn);instance.Remove();Object.DestroyImmediate(data);}
    }
    static void VisualStates()
    {
        var root=new GameObject("Paired visual states");var pawn=new GameObject("Pawn");pawn.transform.SetParent(root.transform);
        try
        {
            var mission=root.AddComponent<Map02Mission>();mission.enabled=false;
            var motor=pawn.AddComponent<PlayerController>();motor.enabled=false;var health=pawn.AddComponent<Health>();health.SetMaxHealth(100);
            var combat=pawn.AddComponent<PlayerCombat>();typeof(PlayerCombat).GetMethod("Awake",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(combat,null);
            Set(mission,"player",motor);Set(mission,"combat",combat);Set(mission,"health",health);Property(mission,"Ready",true);
            var nam=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Characters/Player/Player.fbx"),pawn.transform);
            NestedAnimator(nam); // Reproduce the live rig: Animator below the model root, renderers beside it.
            nam.transform.localPosition=Vector3.down*.95f; // Reproduce a pre-existing model-root ground offset from another animation.
            var actorRoot=new GameObject("Tâm");actorRoot.transform.SetParent(root.transform);var actor=actorRoot.AddComponent<Map02ChapterActor>();actor.role="Tâm";actor.PairedEscort=true;
            var tamModel=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Characters/NPCs/Anh Tâm.fbx"));NestedAnimator(tamModel);actor.Bind(tamModel.transform);
            tamModel.transform.localPosition=Vector3.down*.7f;
            var chapter=root.AddComponent<Map02ChapterDirector>();chapter.enabled=false;Property(chapter,"Mission",mission);Property(mission,"Chapter",chapter);Set(chapter,"player",motor);Set(chapter,"combat",combat);Set(chapter,"escort",actor);chapter.State.activeTask="D2_04";
            Set(motor,"walkSpeed",2.75f);Check(Mathf.Abs(chapter.ScoutEscortSpeed-1.8f)<.0001f,"escort uses requested 1.8 m/s independently of configured walk speed");
            var roles=(Dictionary<string,Map02ChapterActor>)typeof(Map02ChapterDirector).GetField("roles",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(chapter);roles["Tâm"]=actor;
            var bones=nam.GetComponentsInChildren<Transform>().ToDictionary(t=>t.name,t=>t);
            var gun=new GameObject("Pistol muzzle");gun.transform.SetParent(bones["RightHand"],false);var weapon=gun.AddComponent<Weapon>();weapon.Configure(WeaponKind.Pistol,25,35,3,false,gun.transform);
            combat.RegisterWeapon(weapon,new WeaponTuning{magazineCapacity=8,reloadSeconds=2.4f});combat.Equip(WeaponKind.Pistol);
            var bag=root.AddComponent<Map01Inventory>();typeof(Map01Inventory).GetMethod("Awake",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(bag,null);bag.PrepareScoutPistol();
            Check(!bag.DropSlot(Map01Inventory.Slot.Pistol)&&!bag.Unequip(Map01Inventory.Slot.Pistol),"K54 cannot be dropped or stowed during paired escort");
            var view=new GameObject("Aim camera");view.transform.SetParent(root.transform);view.tag="MainCamera";var camera=view.AddComponent<Camera>();view.transform.rotation=Quaternion.Euler(-17,9,0);
            var follow=view.AddComponent<ThirdPersonCamera>();Set(follow,"target",pawn.transform);Set(mission,"cameraRig",follow);
            Property(mission,"CharacterSettings",new ForestSettings{playerHP=100,medkitHeal=45});
            health.RestoreHealth(30);bag.Receive("medkit_small",2);int medkits=bag.Count("medkit_small");
            Check(mission.CharacterGearLocked&&mission.CharacterCanHeal,"escort allows healing while keeping weapon changes locked");
            Check(bag.UseItem("medkit_small")&&Mathf.Abs(health.Current-75)<.001f&&bag.Count("medkit_small")==medkits-1,"medkit heals normally and consumes exactly one while escorting");
            Check(!bag.UseItem("medkit_small")&&bag.Count("medkit_small")==medkits-1,"healing cooldown prevents repeated immediate consumption");
            var visual=root.AddComponent<Map02ScoutEscortVisual>();visual.Bind(chapter);
            var tick=typeof(Map02ScoutEscortVisual).GetMethod("LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic);
            tick.Invoke(visual,null);
            foreach(var model in new[]{nam,tamModel})
            {
                var joints=model.GetComponentsInChildren<Transform>().ToDictionary(t=>t.name,t=>t);
                foreach(var side in new[]{"Left","Right"})
                {
                    float bend=Vector3.Angle(joints[side+"Leg"].position-joints[side+"UpLeg"].position,joints[side+"Foot"].position-joints[side+"Leg"].position);
                    Check(bend<20,model.name+" "+side+" standing knee is nearly straight: "+bend);
                }
            }
            Check(Mathf.Abs(tamModel.GetComponentsInChildren<Transform>().First(t=>t.name=="Hips").localScale.x-Map02EscortPoseData.Load("Tam").visualScale)<.001f,"Tâm height adjustment applied only to escort visual");
            var support=bones["LeftHand"].position;var lowered=bones["RightArm"].rotation;
            var tamData=Map02EscortPoseData.Load("Tam");var tamNodes=tamModel.GetComponentsInChildren<Transform>().ToDictionary(t=>t.name,t=>t);
            Check(Mathf.Abs(bones["Head"].position.y-tamNodes["Head"].position.y)<.06f,"Nam and Tâm escort head heights match within 6 cm: "+bones["Head"].position.y+" / "+tamNodes["Head"].position.y);
            int upper=Array.IndexOf(tamData.names,"RightArm");Check(Quaternion.Angle(tamNodes["RightArm"].localRotation,tamData.idle.bones[upper].rotation)<.1f,"approved shoulder pose binds with nested Animator root");
            var kneeStart=tamNodes["LeftUpLeg"].localRotation;pawn.transform.position+=Vector3.forward*.25f;tick.Invoke(visual,null);
            Check(Quaternion.Angle(kneeStart,tamNodes["LeftUpLeg"].localRotation)>5,"real displacement advances Tâm leg animation even when PlanarSpeed is zero");
            pawn.transform.position=Vector3.zero;Set(visual,"clock",0f);Set(visual,"lastPlayerPosition",Vector3.zero);tick.Invoke(visual,null);support=bones["LeftHand"].position;
            Check(Mathf.Abs(Lowest(nam)-.005f)<.01f&&Mathf.Abs(Lowest(tamModel)-.005f)<.01f,"both characters stand on ground despite deeply offset incoming model roots: Nam="+Lowest(nam)+" Tam="+Lowest(tamModel));
            var terrain=GameObject.CreatePrimitive(PrimitiveType.Cube);terrain.transform.SetParent(root.transform);terrain.transform.position=new Vector3(0,.9f,0);terrain.transform.localScale=new Vector3(20,.2f,20);pawn.transform.position=Vector3.up;Physics.SyncTransforms();
            tick.Invoke(visual,null);Check(Mathf.Abs(Lowest(nam)-1.005f)<.01f&&Mathf.Abs(Lowest(tamModel)-1.005f)<.01f,"both soles follow raised road surface without sinking");
            support=bones["LeftHand"].position;
            Set(combat,"_aiming",true);Set(visual,"gunWeight",1f);tick.Invoke(visual,null);
            Check(Vector3.Angle(gun.transform.forward,camera.transform.forward)<.1f,"aim raises right arm and aligns muzzle with camera pitch/yaw");
            Check(Vector3.Distance(support,bones["LeftHand"].position)<.005f,"left supporting hand does not move while aiming");
            Check(Quaternion.Angle(lowered,bones["RightArm"].rotation)>5,"carry and aim have different right arm poses");
            combat.ReserveRounds=()=>24;combat.RestoreMagazine(0);combat.TryReload();var magazine=(Magazine)typeof(PlayerCombat).GetField("_magazine",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(combat);magazine.Tick(1.2f);
            Check(Mathf.Abs(combat.ReloadProgress-.5f)<.001f,"half of normal reload time corresponds to half animation progress");
            tick.Invoke(visual,null);Check(Vector3.Distance(support,bones["LeftHand"].position)<.005f,"left supporting hand stays planted at midpoint of reload");
            magazine.Tick(1.2f);Check(!combat.IsReloading&&combat.MagazineRounds(WeaponKind.Pistol)==8,"reload completes at normal time with normal magazine refill");
            var body=actorRoot.AddComponent<CapsuleCollider>();body.height=1.8f;body.radius=.3f;body.center=Vector3.up*.9f;Physics.SyncTransforms();
            var clear=typeof(ThirdPersonCamera).GetMethod("ClearDistance",BindingFlags.Instance|BindingFlags.NonPublic);var eye=pawn.transform.position+Vector3.up*1.6f;
            float free=(float)clear.Invoke(follow,new object[]{eye,Vector3.left,2f,.1f});Check(Mathf.Abs(free-2)<.001f,"Tâm collider does not retract normal aim camera framing");
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.SetParent(root.transform);wall.transform.position=eye+Vector3.left*1.6f;wall.transform.localScale=new Vector3(.15f,3,3);Physics.SyncTransforms();
            float blocked=(float)clear.Invoke(follow,new object[]{eye,Vector3.left,2f,.1f});Check(blocked>0&&blocked<1.6f,"camera still collides with real world walls");
            visual.Release();Check(Vector3.Distance(tamModel.transform.localScale,Vector3.one)<.001f,"Tâm normal height restored after escort");Check(follow.IgnoredCompanion==null,"camera companion exception removed after escort");
        }finally{Object.DestroyImmediate(root);}
    }
    public static Animator NestedAnimator(GameObject model)
    {
        foreach(var old in model.GetComponentsInChildren<Animator>())Object.DestroyImmediate(old);
        var hips=model.GetComponentsInChildren<Transform>().First(t=>t.name=="Hips");var root=hips.parent;
        if(root==model.transform){root=new GameObject("LiveAnimatorRoot").transform;root.SetParent(model.transform,false);hips.SetParent(root,true);}
        return root.gameObject.AddComponent<Animator>();
    }
    static float Lowest(GameObject model)
    {
        var mesh=new Mesh();float result=float.PositiveInfinity;
        foreach(var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>()){skin.BakeMesh(mesh);foreach(var vertex in mesh.vertices)result=Mathf.Min(result,skin.transform.TransformPoint(vertex).y);}
        Object.DestroyImmediate(mesh);return result;
    }
}
