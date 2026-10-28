using System.Collections.Generic;
using UnityEngine;
using ShadowVale.Gameplay.Combat;
namespace ShadowVale.Map01 {
 // Cinematic visibility owns the full player root, including weapons outside the Animator hierarchy.
 // A render-only sling leaves gameplay sockets alone; drawing and the final handoff restore them once.
 [DefaultExecutionOrder(1100)]
 public sealed class Map01BoatCarry:MonoBehaviour {
  enum Mode { Inactive, Slung, Drawn }
  Mode mode;Animator actor,visualActor;Transform root,gun,hand,chest,display;
  Vector3 localPosition,localScale,centre,barrel,flat;Quaternion localRotation;
  readonly Dictionary<Renderer,bool> visibility=new();readonly List<Mesh> bakedMeshes=new();
  LineRenderer strap;Material strapMaterial;
  public bool Slung=>mode==Mode.Slung;
  public Transform Gun=>Slung&&display!=null?display:gun;
  public Transform OriginalGun=>gun;
  public int VisibleGunCount {
   get {int n=display!=null&&display.gameObject.activeInHierarchy?1:0;foreach(var w in root.GetComponentsInChildren<Weapon>(true))if(w.IsGun&&Visible(w.transform))n++;return n;}
  }
  static bool Visible(Transform weapon){foreach(var r in weapon.GetComponentsInChildren<Renderer>(true))if(r.enabled&&!r.forceRenderingOff&&r.gameObject.activeInHierarchy)return true;return false;}
  public static Map01BoatCarry For(Animator body){var carry=body.GetComponent<Map01BoatCarry>();if(carry==null){carry=body.gameObject.AddComponent<Map01BoatCarry>();carry.Bind(body);}return carry;}
  void Bind(Animator body){
   actor=visualActor=body;var combat=body.GetComponentInParent<PlayerCombat>();root=combat!=null?combat.transform:body.transform;
   gun=Map01Rifle.Attach(body).weapon.transform;hand=gun.parent;chest=body.GetBoneTransform(HumanBodyBones.Chest);
   localPosition=gun.localPosition;localRotation=gun.localRotation;localScale=gun.localScale;
   display=new GameObject("Boat rifle — single render-only sling").transform;display.SetParent(actor.transform,false);display.localScale=localScale;
   Bounds bounds=default;bool measured=false;
   foreach(var r in gun.GetComponentsInChildren<Renderer>(true)){
    bool part=r.enabled;for(var t=r.transform;t!=gun&&t!=null;t=t.parent)part&=t.gameObject.activeSelf;if(!part)continue;
    Mesh mesh=r.GetComponent<MeshFilter>()?.sharedMesh;
    if(r is SkinnedMeshRenderer skin){mesh=new Mesh();skin.BakeMesh(mesh);bakedMeshes.Add(mesh);}if(mesh==null)continue;
    var copy=new GameObject(r.name);copy.transform.SetParent(display,false);copy.transform.localPosition=gun.InverseTransformPoint(r.transform.position);copy.transform.localRotation=Quaternion.Inverse(gun.rotation)*r.transform.rotation;
    var scale=r.transform.lossyScale;var parent=gun.lossyScale;copy.transform.localScale=new Vector3(scale.x/parent.x,scale.y/parent.y,scale.z/parent.z);
    copy.AddComponent<MeshFilter>().sharedMesh=mesh;copy.AddComponent<MeshRenderer>().sharedMaterials=r.sharedMaterials;
    var b=mesh.bounds;for(int i=0;i<8;i++){var v=gun.InverseTransformPoint(r.transform.TransformPoint(b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1))));if(measured)bounds.Encapsulate(v);else{bounds=new Bounds(v,Vector3.zero);measured=true;}}
   }
   centre=bounds.center;var size=bounds.size;int along=size.x>=size.y&&size.x>=size.z?0:size.y>=size.z?1:2,across=size.x<=size.y&&size.x<=size.z?0:size.y<=size.z?1:2;
   barrel=flat=Vector3.zero;barrel[along]=gun.InverseTransformPoint(body.GetComponent<Map01Rifle>().weapon.Muzzle.position)[along]>=centre[along]?1:-1;flat[across]=1;
   if(along==across){barrel=Vector3.forward;flat=Vector3.right;}
   var go=new GameObject("Boat rifle canvas strap");go.transform.SetParent(actor.transform,false);strap=go.AddComponent<LineRenderer>();strap.positionCount=3;strap.widthMultiplier=.022f;strap.useWorldSpace=true;
   strapMaterial=new Material(Shader.Find("Universal Render Pipeline/Simple Lit"));strapMaterial.color=new Color(.15f,.13f,.08f);strap.sharedMaterial=strapMaterial;strap.enabled=false;display.gameObject.SetActive(false);
  }
  void HideGameplayGuns(bool drawn){
   foreach(var w in root.GetComponentsInChildren<Weapon>(true))if(w.IsGun)foreach(var r in w.GetComponentsInChildren<Renderer>(true)){
    if(!visibility.ContainsKey(r))visibility.Add(r,r.forceRenderingOff);r.forceRenderingOff=!(drawn&&w.transform==gun);
   }
  }
  public void UseVisualActor(Animator body){visualActor=body!=null?body:actor;}
  public void Stow(){if(gun==null)return;mode=Mode.Slung;Present();}
  public void Draw(){if(gun==null)return;mode=Mode.Drawn;Present();}
  // The landing used to turn the gun off while swapping Nam's body. Keep one sling on the replacement instead.
  public void Suspend()=>Stow();
  void LateUpdate(){if(mode!=Mode.Inactive)Present();}
  void Present(){
   if(gun==null)return;bool slung=mode==Mode.Slung;HideGameplayGuns(!slung);display.gameObject.SetActive(slung);strap.enabled=slung;
   if(!slung){gun.gameObject.SetActive(true);gun.SetParent(hand,false);gun.SetLocalPositionAndRotation(localPosition,localRotation);gun.localScale=localScale;return;}
   var body=visualActor!=null?visualActor:actor;var torso=body.GetBoneTransform(HumanBodyBones.Chest);
   var r=body.GetBoneTransform(HumanBodyBones.RightShoulder)??body.GetBoneTransform(HumanBodyBones.RightUpperArm);var l=body.GetBoneTransform(HumanBodyBones.LeftShoulder)??body.GetBoneTransform(HumanBodyBones.LeftUpperArm);
   var right=Vector3.ProjectOnPlane(r.position-l.position,Vector3.up).normalized;var front=Vector3.Cross(right,Vector3.up).normalized;
   display.rotation=Quaternion.LookRotation((Vector3.up-right*.30f-front*.08f).normalized,right)*Quaternion.Inverse(Quaternion.LookRotation(barrel,flat));
   var anchor=torso.position+right*.20f-front*.32f-Vector3.up*.12f;display.position+=anchor-display.TransformPoint(centre);
   strap.SetPosition(0,anchor-Vector3.up*.30f);strap.SetPosition(1,torso.position+right*.20f+Vector3.up*.20f-front*.02f);strap.SetPosition(2,anchor+Vector3.up*.30f);
  }
  public void Restore(){
   if(gun==null)return;mode=Mode.Inactive;display.gameObject.SetActive(false);strap.enabled=false;visualActor=actor;
   gun.SetParent(hand,false);gun.SetLocalPositionAndRotation(localPosition,localRotation);gun.localScale=localScale;
   foreach(var p in visibility)if(p.Key!=null)p.Key.forceRenderingOff=p.Value;visibility.Clear();
   var combat=root.GetComponent<PlayerCombat>();if(combat!=null)foreach(var w in root.GetComponentsInChildren<Weapon>(true))if(w.IsGun&&combat.WeaponFor(w.Kind)!=w)w.gameObject.SetActive(false);
  }
  void OnDestroy(){if(display!=null)Destroy(display.gameObject);if(strap!=null)Destroy(strap.gameObject);if(strapMaterial!=null)Destroy(strapMaterial);foreach(var m in bakedMeshes)if(m!=null)Destroy(m);}
 }
}
