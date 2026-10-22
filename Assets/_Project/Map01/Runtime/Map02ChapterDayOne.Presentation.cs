using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ShadowVale.Gameplay.Combat;
namespace ShadowVale.Map01
{
    public sealed partial class Map02ChapterDirector
    {
        Transform elderChair;bool familyAtMeal;
        readonly Dictionary<string,Vector3> mealOrigins=new();
        readonly Dictionary<string,Quaternion> mealRotations=new();
        readonly Dictionary<Renderer,bool> workWeaponRenderers=new();
        GameObject slungWorkRifle;readonly List<Mesh> slingMeshes=new();
        public bool WorkRifleSlung=>slungWorkRifle!=null&&slungWorkRifle.activeSelf;
        static readonly string[] MealFamily={"Chị Lành","Ông Sáu","Bé Tí"};
        static readonly Vector3[] MealOffsets={new Vector3(-1.1f,0,0),new Vector3(1.1f,0,.3f),new Vector3(.85f,0,-.95f)};
        void Stool(Transform owner,Vector3 offset,Material wood)
        {
            Part(owner,"Family stool seat",offset+Vector3.up*.43f,new Vector3(.46f,.055f,.42f),wood);
            foreach(float x in new[]{-.17f,.17f})foreach(float z in new[]{-.15f,.15f})Part(owner,"Family stool leg",offset+new Vector3(x,.215f,z),new Vector3(.04f,.43f,.04f),wood);
        }
        void BuildFamilyFurniture(Material wood,Material cloth)
        {
            elderChair=Group("Ông Sáu resting chair",DayFamily+new Vector3(1.1f,0,-.8f));Stool(elderChair,Vector3.zero,wood);
            Part(elderChair,"Chair back",new Vector3(0,.8f,-.22f),new Vector3(.46f,.55f,.055f),wood);
            for(int i=0;i<MealOffsets.Length;i++)if(i!=1)Stool(mealRoot,MealOffsets[i],wood);
            for(int i=0;i<5;i++){float a=i*Mathf.PI*2/5;Part(mealRoot,"Individual rice bowl",new Vector3(Mathf.Cos(a)*.46f,.49f,Mathf.Sin(a)*.4f),new Vector3(.12f,.055f,.12f),cloth,PrimitiveType.Sphere);}
        }
        void ArrangeDayFamily()
        {
            if(elderChair==null||familyAtMeal)return;
            bool rest=State.phase<=1&&!State.Done("N3_02");roles["Ông Sáu"].SetSeatedRest(rest?DayClip("MealNam"):null);
            if(rest){roles["Chị Lành"].StopAt(DayFamily+new Vector3(-1.7f,0,.6f));roles["Chị Lành"].transform.rotation=Quaternion.Euler(0,110,0);roles["Ông Sáu"].StopAt(elderChair.position);roles["Ông Sáu"].transform.rotation=Quaternion.LookRotation(-MealOffsets[1]);elderChair.rotation=roles["Ông Sáu"].transform.rotation;roles["Bé Tí"].StopAt(DayFamily+new Vector3(2.2f,0,1.1f));roles["Bé Tí"].transform.rotation=Quaternion.Euler(0,225,0);}
        }
        void BeginFamilyMeal()
        {
            familyAtMeal=true;mealOrigins.Clear();mealRotations.Clear();
            for(int i=0;i<MealFamily.Length;i++){var actor=roles[MealFamily[i]];mealOrigins[actor.role]=actor.transform.position;mealRotations[actor.role]=actor.transform.rotation;actor.StopAt(mealRoot.position+MealOffsets[i]);actor.transform.rotation=Quaternion.LookRotation(-MealOffsets[i]);}
        }
        void SampleFamilyMeal()
        {
            for(int i=0;i<MealFamily.Length;i++){var actor=roles[MealFamily[i]];actor.SamplePerformance(DayClip("MealNam"),sceneClock-dayApproachDuration,true);}
        }
        void EndFamilyMeal()
        {
            if(!familyAtMeal)return;familyAtMeal=false;
            foreach(var role in MealFamily){var actor=roles[role];actor.ReleasePerformance();if(mealOrigins.TryGetValue(role,out var position))actor.StopAt(position);if(mealRotations.TryGetValue(role,out var rotation))actor.transform.rotation=rotation;}
            mealOrigins.Clear();mealRotations.Clear();
        }
        static bool ActiveWeaponPart(Transform part,Transform root){for(var t=part;t!=null&&t!=root;t=t.parent)if(!t.gameObject.activeSelf)return false;return true;}
        void UpdateWorkWeapon()
        {
            bool occupied=State.carry=="D1_04"||escort!=null||DayActivityActive;
            if(!occupied){ReleaseWorkWeapon();return;}
            if(slungWorkRifle==null)
            {
                var gun=combat.WeaponFor(WeaponKind.Rifle);
                var animator=Mission.Player.GetComponentInChildren<Animator>();
                if(gun!=null&&animator!=null&&animator.isHuman)
                {
                    var chest=animator.GetBoneTransform(HumanBodyBones.Chest)??animator.GetBoneTransform(HumanBodyBones.Spine);
                    slungWorkRifle=new GameObject("Work rifle — slung over shoulder, no gameplay scripts");slungWorkRifle.transform.SetParent(chest,false);
                    slungWorkRifle.transform.localPosition=new Vector3(.26f,.08f,-.22f);slungWorkRifle.transform.localRotation=Quaternion.Euler(90,0,15);
                    slungWorkRifle.transform.localScale=gun.transform.localScale;
                    foreach(var renderer in gun.GetComponentsInChildren<Renderer>(true))
                    {
                        if(!renderer.enabled||!ActiveWeaponPart(renderer.transform,gun.transform))continue;
                        Mesh mesh=null;var filter=renderer.GetComponent<MeshFilter>();if(filter!=null)mesh=filter.sharedMesh;
                        if(renderer is SkinnedMeshRenderer skin){mesh=new Mesh();skin.BakeMesh(mesh);slingMeshes.Add(mesh);}
                        if(mesh==null)continue;
                        var go=new GameObject(renderer.name);go.transform.SetParent(slungWorkRifle.transform,false);go.transform.localPosition=gun.transform.InverseTransformPoint(renderer.transform.position);go.transform.localRotation=Quaternion.Inverse(gun.transform.rotation)*renderer.transform.rotation;
                        var scale=renderer.transform.lossyScale;var parent=gun.transform.lossyScale;go.transform.localScale=new Vector3(scale.x/parent.x,scale.y/parent.y,scale.z/parent.z);
                        go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterials=renderer.sharedMaterials;
                    }
                }
            }
            if(slungWorkRifle!=null)slungWorkRifle.transform.SetPositionAndRotation(Mission.Player.position+Vector3.up*1.05f+Mission.Player.right*.3f-Mission.Player.forward*.28f,Mission.Player.rotation*Quaternion.Euler(90,0,15));
            foreach(var weapon in Mission.Player.GetComponentsInChildren<Weapon>(true))foreach(var renderer in weapon.GetComponentsInChildren<Renderer>(true)){if(!workWeaponRenderers.ContainsKey(renderer))workWeaponRenderers.Add(renderer,renderer.forceRenderingOff);renderer.forceRenderingOff=true;}
        }
        void ReleaseWorkWeapon()
        {
            foreach(var pair in workWeaponRenderers)if(pair.Key!=null)pair.Key.forceRenderingOff=pair.Value;workWeaponRenderers.Clear();
            if(slungWorkRifle!=null){Destroy(slungWorkRifle);slungWorkRifle=null;}foreach(var mesh in slingMeshes)if(mesh!=null)Destroy(mesh);slingMeshes.Clear();
        }
    }
}
