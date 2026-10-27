using System;
using System.Collections.Generic;
using System.Linq;
using ShadowVale.Gameplay.Player;
using ShadowVale.Gameplay.Combat;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShadowVale.Map01
{
    /// <summary>Space at the ladder consumes jump input and takes exclusive traversal control.</summary>
    [DefaultExecutionOrder(160)]
    public sealed class Map02UndergroundStore : MonoBehaviour
    {
        public const float Depth=7,RoomLength=8,RoomWidth=5,RoomHeight=4;
        const float TopLatchDepth=1.1f;
        public Map02ChapterDirector Owner {get;private set;}
        public bool IsClimbing {get;private set;}
        public bool Inside=>Owner!=null&&Owner.State.night2InsideStore;
        public bool IsOpen=>Owner!=null&&Owner.State.night2HatchOpen;
        public Vector3 TopStand=>transform.TransformPoint(new Vector3(-1.8f,0,0));
        public Vector3 BottomStand=>transform.TransformPoint(new Vector3(-.28f,-Depth,0));
        Vector3 BottomLanding=>transform.TransformPoint(new Vector3(.5f,-Depth,0));
        public Vector3 InspectionPoint=>transform.TransformPoint(new Vector3(3.1f,-Depth,2.05f));
        public BoxCollider FallGuard {get;private set;}
        Transform hinge;Quaternion closedRotation;float hatchAngle;
        CharacterController body;PlayerController motor;PlayerCombat combat;ThirdPersonCamera cameraRig;
        Animator actor;Map01CinematicPose climbPose;AnimationClip idle;WeaponKind priorWeapon;
        readonly Dictionary<Transform,Quaternion> climbRest=new();
        Func<bool> previousJumpInteraction,jumpHandler;
        bool bodyWasEnabled;float depth,transition;Vector3 transitionFrom;int entrySide; // 1 surface, -1 room
        bool exiting;Vector3 exitTarget;
        Mesh sourceTerrain,cutTerrain;MeshCollider terrainCollider;MeshFilter terrainFilter;
        readonly List<(VillageRiceInstances field,Vector4[] plants,float[] sizes)> crops=new();
        public bool ContainsAuthorizedPlayer(Vector3 world)
        {
            if(!Inside&&!IsClimbing)return false;
            var p=transform.InverseTransformPoint(world);
            return p.x>=-1.3f&&p.x<=7.3f&&p.z>=-1.8f&&p.z<=3.8f&&p.y>=-7.4f&&p.y<2;
        }
        public bool CanInspect=>Inside&&!IsClimbing&&Vector3.Distance(Owner.Mission.Player.position,InspectionPoint)<1.9f;
        public void Initialize(Map02ChapterDirector owner)
        {
            Owner=owner;var pawn=owner.Mission.Player;body=pawn.GetComponent<CharacterController>();motor=pawn.GetComponent<PlayerController>();combat=pawn.GetComponent<PlayerCombat>();
            previousJumpInteraction=motor.JumpInteraction;jumpHandler=()=>TryEnterLadder()||(previousJumpInteraction?.Invoke()??false);motor.JumpInteraction=jumpHandler;
            cameraRig=FindObjectsByType<ThirdPersonCamera>(FindObjectsSortMode.None).FirstOrDefault(c=>c.GetComponent<Camera>()?.enabled==true);
            var authoredHinge=GetComponentsInChildren<Transform>(true).First(t=>t.name=="HatchPivot");
            hinge=new GameObject("HatchControl").transform;hinge.SetParent(transform,false);hinge.localPosition=new Vector3(1,.14f,0);
            foreach(var child in authoredHinge.Cast<Transform>().ToArray())child.SetParent(hinge,true);
            closedRotation=hinge.localRotation;
            BuildCollision();CutTerrainOpening();ClearMouthPlants();SyncState();Physics.SyncTransforms();
        }
        void ClearMouthPlants()
        {
            foreach(var field in FindObjectsByType<VillageRiceInstances>(FindObjectsSortMode.None))
            {
                var kept=new List<Vector4>();var sizes=new List<float>();
                for(int i=0;i<field.plants.Length;i++){var p=field.plants[i];var world=field.transform.TransformPoint(new Vector3(p.x,p.y,p.z));var local=transform.InverseTransformPoint(world);if(Mathf.Abs(local.x)<1.3f&&Mathf.Abs(local.z)<1.8f||Vector3.ProjectOnPlane(world-TopStand,Vector3.up).sqrMagnitude<.55f*.55f)continue;kept.Add(p);sizes.Add(i<field.sizes.Length?field.sizes[i]:1);}
                if(kept.Count==field.plants.Length)continue;crops.Add((field,field.plants,field.sizes));field.plants=kept.ToArray();field.sizes=sizes.ToArray();field.Rebuild();
            }
        }
        BoxCollider Box(string name,Vector3 center,Vector3 size,Transform parent=null)
        {
            var go=new GameObject(name);go.transform.SetParent(parent!=null?parent:transform,false);go.transform.localPosition=center;var c=go.AddComponent<BoxCollider>();c.size=size;return c;
        }
        void BuildCollision()
        {
            Box("Earth floor",new Vector3(3,-7.15f,1),new Vector3(8.2f,.3f,5.2f));
            Box("Earth ceiling main",new Vector3(4,-2.85f,0),new Vector3(6,.3f,3));
            Box("Earth ceiling back",new Vector3(3,-2.85f,2.5f),new Vector3(8,.3f,2));
            Box("West shaft wall",new Vector3(-1.13f,-3.5f,0),new Vector3(.25f,7,3.1f));
            Box("West room wall",new Vector3(-1.13f,-5,2.5f),new Vector3(.25f,4,2));
            Box("East room wall",new Vector3(7.13f,-5,1),new Vector3(.25f,4,5.2f));
            Box("Front room wall",new Vector3(3,-5,-1.63f),new Vector3(8.2f,4,.25f));
            Box("Back room wall",new Vector3(3,-5,3.63f),new Vector3(8.2f,4,.25f));
            Box("Shaft east",new Vector3(1.13f,-1.5f,0),new Vector3(.25f,3,3.2f));
            Box("Shaft front",new Vector3(0,-1.5f,-1.63f),new Vector3(2.2f,3,.25f));
            Box("Shaft back",new Vector3(0,-1.5f,1.63f),new Vector3(2.2f,3,.25f));
            Box("Supply shelf front",new Vector3(3.1f,-5.9f,-1.08f),new Vector3(3.8f,2.2f,.66f));
            Box("Supply shelf back",new Vector3(3.1f,-5.9f,3.08f),new Vector3(3.8f,2.2f,.66f));
            Box("Supply shelf east",new Vector3(6.58f,-5.9f,1),new Vector3(.66f,2.2f,2.8f));
            // Never disabled: the controlled traversal temporarily disables only Nam's capsule.
            FallGuard=Box("Space ladder entry only — permanent fall guard",new Vector3(0,-.06f,0),new Vector3(2,.20f,3));
            FallGuard.gameObject.layer=2;
            Box("Single wooden hatch leaf",new Vector3(-1,0,0),new Vector3(2,.12f,3),hinge);
            for(int i=0;i<2;i++)
            {
                var go=new GameObject("Ceiling oil lantern light "+(i+1));go.transform.SetParent(transform,false);go.transform.localPosition=new Vector3(i==0?2.2f:5.2f,-3.65f,1);
                var light=go.AddComponent<Light>();light.type=LightType.Point;light.color=new Color(1,.56f,.23f);light.range=6.5f;light.intensity=2.2f;light.shadows=LightShadows.Soft;
            }
        }
        public bool TryDoorInteraction()
        {
            if(IsClimbing||Inside||Vector3.Distance(Owner.Mission.Player.position,TopStand)>2)return false;
            if(Owner.State.phase<3||!Owner.State.Done("N2_02")){Owner.Mission.Say("Quan sát tổ tuần tra và báo bộ đàm trước khi vào kho.");return true;}
            Owner.State.night2HatchOpen=!IsOpen;Owner.Mission.RequireInteractionRelease();Owner.Mission.StoreCheckpoint();
            Owner.Mission.Say(IsOpen?"Nắp đã mở. Đứng tại điểm cạnh miệng hầm, nhấn Space bám thang. S xuống · W lên.":"Đã đóng nắp hầm.");return true;
        }
        bool AtBottomApproach(Vector3 world)
        {
            var p=transform.InverseTransformPoint(world);
            return Mathf.Abs(p.y+Depth)<=.4f&&p.x>=-.78f&&p.x<=1.3f&&Mathf.Abs(p.z)<=.85f;
        }
        bool HasFootSupport()
        {
            if(motor.IsGrounded)return true;
            return Physics.RaycastAll(Owner.Mission.Player.position+Vector3.up*.16f,Vector3.down,.4f,~0,QueryTriggerInteraction.Ignore)
                .Any(h=>h.normal.y>.5f&&!h.transform.IsChildOf(Owner.Mission.Player)&&h.transform!=Owner.Mission.Player);
        }
        public bool TryEnterLadder()
        {
            if(IsClimbing||!IsOpen||hatchAngle<95||Owner.Mission.InputBlocked||motor.IsProne||!HasFootSupport())return false;
            bool below=AtBottomApproach(Owner.Mission.Player.position);
            if(!below&&(Inside||Vector3.Distance(Owner.Mission.Player.position,TopStand)>.8f))return false;
            if(below)Owner.State.night2InsideStore=true;
            entrySide=below?-1:1;depth=below?Depth:TopLatchDepth;IsClimbing=true;exiting=false;transition=0;transitionFrom=Owner.Mission.Player.position;
            bodyWasEnabled=body.enabled;body.enabled=false;motor.RestoreMotion(false);
            if(combat!=null){priorWeapon=combat.EquippedKind;combat.CancelReload();combat.Equip(WeaponKind.Unarmed);combat.SetHandsOccupied(true);}
            actor=Owner.Mission.Player.GetComponentInChildren<Animator>();
            if(actor!=null&&actor.isHuman){idle=actor.runtimeAnimatorController?.animationClips.FirstOrDefault(c=>c.name.IndexOf("idle",StringComparison.OrdinalIgnoreCase)>=0);climbRest.Clear();foreach(var t in actor.GetComponentsInChildren<Transform>())climbRest[t]=t.localRotation;climbPose=new Map01CinematicPose(actor,skinGrounding:false);}
            return true;
        }
        void Update()
        {
            if(Owner==null)return;
            if(IsClimbing&&(Owner.Mission.World.story.failed||Owner.Mission.Player.GetComponent<Health>()?.IsDead==true)){CancelTraversal();return;}
            bool pause=Owner.Mission.Paused||ForestMenu.Visible||Owner.Mission.Panel!=0||Owner.Mission.World.story.failed||Time.timeScale<=0;
            if(pause)return;
            hatchAngle=Mathf.MoveTowards(hatchAngle,IsOpen?105:0,Time.deltaTime*100);hinge.localRotation=closedRotation*Quaternion.AngleAxis(-hatchAngle,Vector3.forward);
            var kb=Keyboard.current;
            if(!IsClimbing)
            {
                if(kb?.spaceKey.wasPressedThisFrame==true&&TryEnterLadder())return;
                return;
            }
            if(Owner.Mission.Player.GetComponent<Health>()?.IsDead==true){CancelTraversal();return;}
            StepTraversal(Mathf.Min(Time.deltaTime,.1f),kb?.sKey.isPressed==true,kb?.wKey.isPressed==true);
        }
        public void StepTraversal(float dt,bool down,bool up)
        {
            if(!IsClimbing)return;
            transition=Mathf.Min(1,transition+dt/.5f);
            if(exiting)
            {
                Owner.Mission.Player.position=Vector3.Lerp(transitionFrom,exitTarget,Mathf.SmoothStep(0,1,transition));
                if(transition>=1)FinishTraversal(entrySide<0);
                return;
            }
            var onLadder=transform.TransformPoint(new Vector3(-.32f,-depth,0));
            Owner.Mission.Player.position=Vector3.Lerp(transitionFrom,onLadder,Mathf.SmoothStep(0,1,transition));Owner.Mission.Player.rotation=Quaternion.LookRotation(-transform.right);
            if(transition<1)return;
            if(down!=up)depth=Mathf.Clamp(depth+(down?1:-1)*dt,TopLatchDepth,Depth);
            if(depth>=Depth-.001f&&down){BeginExit(BottomLanding,true);return;}
            if(depth<=TopLatchDepth+.001f&&up){BeginExit(TopStand,false);return;}
        }
        void BeginExit(Vector3 target,bool intoRoom)
        {
            exiting=true;transition=0;transitionFrom=Owner.Mission.Player.position;exitTarget=target;entrySide=intoRoom?-1:1;
        }
        void FinishTraversal(bool intoRoom)
        {
            Owner.State.night2InsideStore=intoRoom;ReleasePose();IsClimbing=false;body.enabled=bodyWasEnabled;motor.RestoreMotion(false);Owner.UpdateMarkers();Owner.Mission.RequireInteractionRelease();Owner.Mission.StoreCheckpoint();
        }
        void LateUpdate()
        {
            if(IsClimbing&&cameraRig!=null){var focus=Owner.Mission.Player.position+Vector3.up*1.05f;var eye=Owner.Mission.Player.position+transform.right*.95f+transform.forward*1.05f+Vector3.up*1.35f;cameraRig.SetCinematicView(eye,Quaternion.LookRotation(focus-eye),65,1);}
            if(!IsClimbing||actor==null||!actor.isHuman)return;
            if(idle!=null)climbPose?.Sample(idle,0);else foreach(var pair in climbRest)if(pair.Key!=null)pair.Key.localRotation=pair.Value;
            float phase=depth*Mathf.PI*2.5f;
            Bone(HumanBodyBones.LeftUpperArm,new Vector3(-105+Mathf.Sin(phase)*17,0,-12));Bone(HumanBodyBones.RightUpperArm,new Vector3(-105-Mathf.Sin(phase)*17,0,12));
            Bone(HumanBodyBones.LeftLowerArm,new Vector3(-42,0,0));Bone(HumanBodyBones.RightLowerArm,new Vector3(-42,0,0));
            Bone(HumanBodyBones.LeftUpperLeg,new Vector3(-25-Mathf.Sin(phase)*20,0,-5));Bone(HumanBodyBones.RightUpperLeg,new Vector3(-25+Mathf.Sin(phase)*20,0,5));
            Bone(HumanBodyBones.LeftLowerLeg,new Vector3(45+Mathf.Sin(phase)*20,0,0));Bone(HumanBodyBones.RightLowerLeg,new Vector3(45-Mathf.Sin(phase)*20,0,0));
            var chest=actor.GetBoneTransform(HumanBodyBones.Chest)?.position??Owner.Mission.Player.position+Vector3.up*1.2f;
            for(int i=0;i<2;i++)
            {
                float y=-depth+1.4f+(i==0?1:-1)*Mathf.Sin(phase)*.15f;y=Mathf.Clamp(-.06f+Mathf.Round((y+.06f)/.29f)*.29f,-6.73f,-.06f);
                var hand=transform.TransformPoint(new Vector3(-.72f,y,i==0?-.29f:.29f));
                Map01ArmContact.Reach(actor,i==0,hand,chest+transform.right*.35f+transform.forward*(i==0?-.5f:.5f),1);
            }
        }
        void Bone(HumanBodyBones b,Vector3 angles){var t=actor.GetBoneTransform(b);if(t!=null)t.localRotation*=Quaternion.Euler(angles);}
        void ReleasePose(){foreach(var pair in climbRest)if(pair.Key!=null)pair.Key.localRotation=pair.Value;climbRest.Clear();climbPose?.Dispose();climbPose=null;cameraRig?.ClearCinematicView();if(combat!=null){combat.SetHandsOccupied(false);combat.Equip(priorWeapon);}}
        public void CancelTraversal()
        {
            if(!IsClimbing)return;Owner.Mission.Player.position=Inside?BottomLanding:TopStand;ReleasePose();IsClimbing=false;body.enabled=bodyWasEnabled;motor.RestoreMotion(false);
        }
        public void SyncState()
        {
            CancelTraversal();hatchAngle=IsOpen?105:0;hinge.localRotation=closedRotation*Quaternion.AngleAxis(-hatchAngle,Vector3.forward);
            if(Inside&&!ContainsAuthorizedPlayer(Owner.Mission.Player.position)){Owner.State.night2InsideStore=false;}
        }
        public void DrawGuide()
        {
            if(Owner==null||Owner.Mission.Paused||ForestMenu.Visible)return;
            var stand=Inside?BottomStand:TopStand;
            if(IsOpen&&!IsClimbing&&cameraRig!=null&&Vector3.Distance(Owner.Mission.Player.position,stand)<6)
            {
                var point=cameraRig.GetComponent<Camera>().WorldToScreenPoint(stand+Vector3.up*.15f);
                if(point.z>0)GUI.Box(new Rect(point.x-90,Screen.height-point.y-30,180,30),"◇ Space · Bám thang");
            }
            string text=null;
            if(IsClimbing)text="S · Xuống     W · Lên";
            else if(IsOpen&&hatchAngle<95&&Vector3.Distance(Owner.Mission.Player.position,TopStand)<3)text="Đang mở nắp hầm…";
            else if(Vector3.Distance(Owner.Mission.Player.position,Inside?BottomStand:TopStand)<1.8f)
                text=IsOpen?(AtBottomApproach(Owner.Mission.Player.position)||Vector3.Distance(Owner.Mission.Player.position,TopStand)<=.8f?"Space · Bám thang     S · Xuống     W · Lên":"Tới sát chân thang · Space để bám thang") : "E · Mở nắp hầm";
            if(text==null)return;
            GUI.Box(new Rect(Screen.width*.5f-260,Screen.height-150,520,58),text,new GUIStyle(GUI.skin.box){fontSize=19,alignment=TextAnchor.MiddleCenter});
        }
        // Clip only triangles touching the 2 x 3 m aperture, preserving the original soil UVs/colours.
        struct V {public Vector3 p,n;public Vector2 uv;public Color c;public static V Lerp(V a,V b,float t)=>new V{p=Vector3.Lerp(a.p,b.p,t),n=Vector3.Lerp(a.n,b.n,t),uv=Vector2.Lerp(a.uv,b.uv,t),c=Color.Lerp(a.c,b.c,t)};}
        void CutTerrainOpening()
        {
            var go=GameObject.Find("Continuous ground with meandering canal");if(go==null)return;
            terrainCollider=go.GetComponent<MeshCollider>();terrainFilter=go.GetComponent<MeshFilter>();if(terrainCollider==null||terrainFilter==null)return;
            sourceTerrain=terrainFilter.sharedMesh;var pos=sourceTerrain.vertices;var normals=sourceTerrain.normals;var uv=sourceTerrain.uv;var colors=sourceTerrain.colors;
            var verts=new List<Vector3>(pos);var ns=new List<Vector3>(normals.Length==pos.Length?normals:new Vector3[pos.Length]);var us=new List<Vector2>(uv.Length==pos.Length?uv:new Vector2[pos.Length]);var cs=new List<Color>(colors.Length==pos.Length?colors:Enumerable.Repeat(Color.white,pos.Length));
            var result=new List<int>();Vector3 origin=transform.position;
            float[] bounds={origin.x-1,origin.x+1,origin.z-1.5f,origin.z+1.5f};
            V Read(int i)=>new V{p=terrainFilter.transform.TransformPoint(pos[i]),n=ns[i],uv=us[i],c=cs[i]};
            void Emit(List<V> poly){for(int i=1;i<poly.Count-1;i++)foreach(var v in new[]{poly[0],poly[i],poly[i+1]}){result.Add(verts.Count);verts.Add(terrainFilter.transform.InverseTransformPoint(v.p));ns.Add(v.n);us.Add(v.uv);cs.Add(v.c);}}
            float D(V v,int side)=>side==0?v.p.x-bounds[0]:side==1?bounds[1]-v.p.x:side==2?v.p.z-bounds[2]:bounds[3]-v.p.z;
            List<V> Clip(List<V> poly,int side,bool inside)
            {
                var output=new List<V>();if(poly.Count==0)return output;
                var a=poly[poly.Count-1];float da=D(a,side)*(inside?1:-1);
                foreach(var b in poly){float db=D(b,side)*(inside?1:-1);if((da>=0)!=(db>=0))output.Add(V.Lerp(a,b,da/(da-db)));if(db>=0)output.Add(b);a=b;da=db;}return output;
            }
            var triangles=sourceTerrain.triangles;
            for(int i=0;i<triangles.Length;i+=3)
            {
                var a=Read(triangles[i]);var b=Read(triangles[i+1]);var c=Read(triangles[i+2]);
                if(Mathf.Max(a.p.x,Mathf.Max(b.p.x,c.p.x))<bounds[0]||Mathf.Min(a.p.x,Mathf.Min(b.p.x,c.p.x))>bounds[1]||Mathf.Max(a.p.z,Mathf.Max(b.p.z,c.p.z))<bounds[2]||Mathf.Min(a.p.z,Mathf.Min(b.p.z,c.p.z))>bounds[3]){result.Add(triangles[i]);result.Add(triangles[i+1]);result.Add(triangles[i+2]);continue;}
                var polygon=new List<V>{a,b,c};for(int side=0;side<4&&polygon.Count>0;side++){Emit(Clip(polygon,side,false));polygon=Clip(polygon,side,true);}
            }
            cutTerrain=new Mesh{name="Map 2 soil — exact 2 x 3 m store opening",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};cutTerrain.SetVertices(verts);cutTerrain.SetNormals(ns);cutTerrain.SetUVs(0,us);cutTerrain.SetColors(cs);cutTerrain.SetTriangles(result,0);cutTerrain.RecalculateBounds();terrainFilter.sharedMesh=cutTerrain;terrainCollider.sharedMesh=cutTerrain;
        }
        void OnDestroy()
        {
            if(motor!=null&&motor.JumpInteraction==jumpHandler)motor.JumpInteraction=previousJumpInteraction;
            foreach(var entry in crops)if(entry.field!=null){entry.field.plants=entry.plants;entry.field.sizes=entry.sizes;entry.field.Rebuild();}
            CancelTraversal();if(cutTerrain!=null){if(terrainFilter!=null)terrainFilter.sharedMesh=sourceTerrain;if(terrainCollider!=null)terrainCollider.sharedMesh=sourceTerrain;Destroy(cutTerrain);}
        }
    }
}
