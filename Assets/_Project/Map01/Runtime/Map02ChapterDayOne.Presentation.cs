using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ShadowVale.Gameplay.Combat;
namespace ShadowVale.Map01
{
    public sealed partial class Map02ChapterDirector
    {
        Transform elderChair;
        readonly Dictionary<Renderer,bool> workWeaponRenderers=new();
        GameObject slungWorkRifle;readonly List<Mesh> slingMeshes=new();
        public bool WorkRifleSlung=>slungWorkRifle!=null&&slungWorkRifle.activeSelf;
        // Night 1 the family is indoors: Lành cooks, Ông Sáu and Tí are asleep. Supper on the porch is Nam and Hùng's (doc, "Lời hứa").
        static readonly string[] Family={"Chị Lành","Ông Sáu","Bé Tí"};
        void Stool(Transform owner,Vector3 offset,Material wood)
        {
            Part(owner,"Family stool seat",offset+Vector3.up*.43f,new Vector3(.46f,.055f,.42f),wood);
            foreach(float x in new[]{-.17f,.17f})foreach(float z in new[]{-.15f,.15f})Part(owner,"Family stool leg",offset+new Vector3(x,.215f,z),new Vector3(.04f,.43f,.04f),wood);
        }
        void BuildFamilyFurniture(Material wood,Material cloth)
        {
            elderChair=Group("Ông Sáu resting chair",DayFamily+new Vector3(1.1f,0,-.8f));Stool(elderChair,Vector3.zero,wood);
            Part(elderChair,"Chair back",new Vector3(0,.8f,-.22f),new Vector3(.46f,.55f,.055f),wood);   // the supper's own bowls come from its track
        }
        void ArrangeDayFamily()
        {
            if(elderChair==null)return;
            bool rest=State.phase<=1&&!State.Done("N3_02");roles["Ông Sáu"].SetSeatedRest(rest?DayClip("SauNgoiNghi")??DayClip("RestNam"):null);   // seated, hands on his lap; MealNam reaches for a bowl
            if(rest){roles["Chị Lành"].StopAt(DayFamily+new Vector3(-1.7f,0,.6f));roles["Chị Lành"].transform.rotation=Quaternion.Euler(0,110,0);roles["Ông Sáu"].StopAt(elderChair.position);roles["Ông Sáu"].transform.rotation=Quaternion.LookRotation(new Vector3(-1.1f,0,-.3f));elderChair.rotation=roles["Ông Sáu"].transform.rotation;roles["Bé Tí"].StopAt(DayFamily+new Vector3(2.2f,0,1.1f));roles["Bé Tí"].transform.rotation=Quaternion.Euler(0,225,0);}
            foreach(var person in Family)roles[person].SetVisible(State.phase!=1);
            elderChair.gameObject.SetActive(State.phase!=1);   // his chair goes in with him; it would stand behind the supper tray
            if(restMat!=null)restMat.gameObject.SetActive(State.phase==1&&State.Done("N1_01"));   // the watch mat is laid out after supper
            ArrangeGuards();
        }
        // The bamboo post's own watch (doc Night 1: "Nam đến chốt và hỏi tổ gác"); anh Lực keeps the village gate all night.
        // There is no militia model yet: Lực's, darkened, stands in until one is made (Layout.militiaArt).
        public const string Guard="Tổ gác",SeatedGuard="Tổ gác 2";
        Transform guardStool;
        GameObject GuardArt=>Layout?.militiaArt!=null?Layout.militiaArt:Layout?.actors?.FirstOrDefault(a=>a.role=="Anh Lực")?.prefab;
        Map02ChapterActor PostKeeper(string task)=>roles[task=="N1_02"?Guard:"Anh Lực"];
        void TintGuards()
        {
            foreach(var (role,shade) in new[]{(Guard,.72f),(SeatedGuard,.6f)})
            {
                var visual=roles[role].visual;if(visual==null)continue;
                foreach(var renderer in visual.GetComponentsInChildren<Renderer>(true))
                {
                    var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block);var source=renderer.sharedMaterial;
                    var colour=source!=null&&source.HasProperty("_BaseColor")?source.GetColor("_BaseColor"):Color.white;
                    block.SetColor("_BaseColor",new Color(colour.r*shade,colour.g*shade,colour.b*shade,colour.a));renderer.SetPropertyBlock(block);
                }
            }
        }
        void ArrangeGuards()
        {
            var post=sites["bamboo"].position;bool watch=State.phase==1;   // lamp at post (-2, 0, 1); the fields lie to +z
            roles[Guard].SetVisible(watch);roles[SeatedGuard].SetVisible(watch);
            if(guardStool==null){guardStool=Group("Bamboo post watch stool",post+new Vector3(-2.55f,0,.15f));var wood=dayMaterials.FirstOrDefault(m=>m!=null&&m.name=="Weathered wood");if(wood!=null)Stool(guardStool,Vector3.zero,wood);}
            guardStool.gameObject.SetActive(watch);if(!watch)return;
            roles[Guard].StopAt(post+new Vector3(.7f,0,.3f));roles[Guard].transform.rotation=Quaternion.identity;
            roles[SeatedGuard].SetSeatedRest(DayClip("RestNam"));roles[SeatedGuard].StopAt(guardStool.position);roles[SeatedGuard].transform.rotation=Quaternion.identity;
        }
        static bool ActiveWeaponPart(Transform part,Transform root){for(var t=part;t!=null&&t!=root;t=t.parent)if(!t.gameObject.activeSelf)return false;return true;}
        // Slung for work: hanging off the right shoulder behind the arm, muzzle up, outside the pack.
        // The anchor is in Nam's own frame from his chest (right, up, forward); the barrel and the gun's
        // flat side come from the gun itself, so the bone axes of the rig never decide where it points.
        static readonly Vector3 SlingAnchor=new Vector3(.22f,.03f,-.2f);   // muzzle about head height, butt at the hip
        Vector3 slingBarrel,slingSide,slingCentre;
        void UpdateWorkWeapon()
        {
            bool occupied=State.carry=="D1_04"||LeadingWounded||DayActivityActive;   // Tâm's and Night 3's rescue escorts keep the rifle in hand
            if(!occupied){ReleaseWorkWeapon();return;}
            if(slungWorkRifle==null)
            {
                var gun=combat.WeaponFor(WeaponKind.Rifle);
                var animator=Mission.Player.GetComponentInChildren<Animator>();
                if(gun!=null&&animator!=null&&animator.isHuman)
                {
                    var chest=animator.GetBoneTransform(HumanBodyBones.Chest)??animator.GetBoneTransform(HumanBodyBones.Spine);
                    slungWorkRifle=new GameObject("Work rifle — slung over shoulder, no gameplay scripts");slungWorkRifle.transform.SetParent(chest,false);
                    slungWorkRifle.transform.localScale=gun.transform.localScale;
                    var extent=new Bounds();bool measured=false;
                    foreach(var renderer in gun.GetComponentsInChildren<Renderer>(true))
                    {
                        if(!renderer.enabled||!ActiveWeaponPart(renderer.transform,gun.transform))continue;
                        Mesh mesh=null;var filter=renderer.GetComponent<MeshFilter>();if(filter!=null)mesh=filter.sharedMesh;
                        if(renderer is SkinnedMeshRenderer skin){mesh=new Mesh();skin.BakeMesh(mesh);slingMeshes.Add(mesh);}
                        if(mesh==null)continue;
                        var go=new GameObject(renderer.name);go.transform.SetParent(slungWorkRifle.transform,false);go.transform.localPosition=gun.transform.InverseTransformPoint(renderer.transform.position);go.transform.localRotation=Quaternion.Inverse(gun.transform.rotation)*renderer.transform.rotation;
                        var scale=renderer.transform.lossyScale;var parent=gun.transform.lossyScale;go.transform.localScale=new Vector3(scale.x/parent.x,scale.y/parent.y,scale.z/parent.z);
                        go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterials=renderer.sharedMaterials;
                        var b=mesh.bounds;
                        for(int c=0;c<8;c++)
                        {
                            var corner=gun.transform.InverseTransformPoint(renderer.transform.TransformPoint(b.center+Vector3.Scale(b.extents,new Vector3((c&1)==0?-1:1,(c&2)==0?-1:1,(c&4)==0?-1:1))));
                            if(measured)extent.Encapsulate(corner);else{extent=new Bounds(corner,Vector3.zero);measured=true;}
                        }
                    }
                    var muzzle=new GameObject("Muzzle").transform;muzzle.SetParent(slungWorkRifle.transform,false);muzzle.localPosition=gun.transform.InverseTransformPoint(gun.Muzzle.position);
                    // Longest extent is the barrel (signed towards the muzzle); thinnest is the side that lies against Nam.
                    var size=extent.size;int along=size.x>=size.y&&size.x>=size.z?0:size.y>=size.z?1:2,across=size.x<=size.y&&size.x<=size.z?0:size.y<=size.z?1:2;
                    slingBarrel=slingSide=Vector3.zero;slingBarrel[along]=muzzle.localPosition[along]>=extent.center[along]?1:-1;slingSide[across]=1;slingCentre=extent.center;
                    if(!measured||along==across){slingBarrel=Vector3.forward;slingSide=Vector3.right;}
                }
            }
            if(slungWorkRifle!=null)
            {
                var nam=Mission.Player;var sling=slungWorkRifle.transform;
                var barrel=(Vector3.up-nam.forward*.15f+nam.right*.05f).normalized;
                sling.rotation=Quaternion.LookRotation(barrel,nam.right)*Quaternion.Inverse(Quaternion.LookRotation(slingBarrel,slingSide));
                var anchor=sling.parent.position+nam.right*SlingAnchor.x+Vector3.up*SlingAnchor.y+nam.forward*SlingAnchor.z;
                sling.position+=anchor-sling.TransformPoint(slingCentre);
            }
            foreach(var weapon in Mission.Player.GetComponentsInChildren<Weapon>(true))foreach(var renderer in weapon.GetComponentsInChildren<Renderer>(true)){if(!workWeaponRenderers.ContainsKey(renderer))workWeaponRenderers.Add(renderer,renderer.forceRenderingOff);renderer.forceRenderingOff=true;}
        }
        void ReleaseWorkWeapon()
        {
            foreach(var pair in workWeaponRenderers)if(pair.Key!=null)pair.Key.forceRenderingOff=pair.Value;workWeaponRenderers.Clear();
            if(slungWorkRifle!=null){Destroy(slungWorkRifle);slungWorkRifle=null;}foreach(var mesh in slingMeshes)if(mesh!=null)Destroy(mesh);slingMeshes.Clear();
        }
    }
}
