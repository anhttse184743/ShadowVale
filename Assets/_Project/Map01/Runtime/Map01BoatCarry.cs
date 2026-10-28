using System.Collections.Generic;
using UnityEngine;
using ShadowVale.Gameplay.Combat;
namespace ShadowVale.Map01 {
 // Move the actual gun between hand and shoulder. Never create a second rifle mesh for the sling.
 [DefaultExecutionOrder(1100)]
 public sealed class Map01BoatCarry:MonoBehaviour {
  enum Mode { Inactive, Slung, Drawn }
  Mode mode;Animator actor,visualActor;Transform root,gun,hand;
  Vector3 localPosition,localScale,worldScale,centre,barrel,flat;Quaternion localRotation;
  readonly Dictionary<Weapon,bool> activeGuns=new();
  readonly Dictionary<Renderer,(bool enabled,bool hidden)> visibility=new();
  LineRenderer strap;Material strapMaterial;
  public bool Slung=>mode==Mode.Slung;
  public Transform Gun=>gun;
  public Transform OriginalGun=>gun;
  public int VisibleGunCount {
   get {int n=0;foreach(var w in root.GetComponentsInChildren<Weapon>(true))if(w.IsGun&&Visible(w.transform))n++;return n;}
  }
  static bool Visible(Transform weapon){foreach(var r in weapon.GetComponentsInChildren<Renderer>(true))if(r.enabled&&!r.forceRenderingOff&&r.gameObject.activeInHierarchy)return true;return false;}
  public static Map01BoatCarry For(Animator body){var carry=body.GetComponent<Map01BoatCarry>();if(carry==null){carry=body.gameObject.AddComponent<Map01BoatCarry>();carry.Bind(body);}return carry;}
  void Bind(Animator body){
   actor=visualActor=body;var combat=body.GetComponentInParent<PlayerCombat>();root=combat!=null?combat.transform:body.transform;
   gun=Map01Rifle.Attach(body).weapon.transform;hand=gun.parent;
   localPosition=gun.localPosition;localRotation=gun.localRotation;localScale=gun.localScale;worldScale=gun.lossyScale;
   Bounds bounds=default;bool measured=false;
   foreach(var r in gun.GetComponentsInChildren<Renderer>(true)){
    var mesh=r.GetComponent<MeshFilter>()?.sharedMesh;if(mesh==null&&r is SkinnedMeshRenderer skin)mesh=skin.sharedMesh;if(mesh==null)continue;
    var b=mesh.bounds;for(int i=0;i<8;i++){var v=gun.InverseTransformPoint(r.transform.TransformPoint(b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1))));if(measured)bounds.Encapsulate(v);else{bounds=new Bounds(v,Vector3.zero);measured=true;}}
   }
   centre=bounds.center;var size=bounds.size;int along=size.x>=size.y&&size.x>=size.z?0:size.y>=size.z?1:2,across=size.x<=size.y&&size.x<=size.z?0:size.y<=size.z?1:2;
   barrel=flat=Vector3.zero;barrel[along]=gun.InverseTransformPoint(body.GetComponent<Map01Rifle>().weapon.Muzzle.position)[along]>=centre[along]?1:-1;flat[across]=1;
   if(along==across){barrel=Vector3.forward;flat=Vector3.right;}
   var go=new GameObject("Boat rifle canvas strap");go.transform.SetParent(actor.transform,false);strap=go.AddComponent<LineRenderer>();strap.positionCount=3;strap.widthMultiplier=.022f;strap.useWorldSpace=true;
   strapMaterial=new Material(Shader.Find("Universal Render Pipeline/Simple Lit"));strapMaterial.color=new Color(.15f,.13f,.08f);strap.sharedMaterial=strapMaterial;strap.enabled=false;
  }
  void OwnGuns(){
   foreach(var w in root.GetComponentsInChildren<Weapon>(true))if(w.IsGun){
    if(!activeGuns.ContainsKey(w))activeGuns.Add(w,w.gameObject.activeSelf);
    w.gameObject.SetActive(w.transform==gun);
    foreach(var r in w.GetComponentsInChildren<Renderer>(true)){
     if(!visibility.ContainsKey(r))visibility.Add(r,(r.enabled,r.forceRenderingOff));
     r.forceRenderingOff=w.transform!=gun;
    }
   }
  }
  public void UseVisualActor(Animator body){visualActor=body!=null?body:actor;if(mode==Mode.Slung)Present();}
  public void Stow(){if(gun==null)return;mode=Mode.Slung;Present();}
  public void Draw(){if(gun==null)return;mode=Mode.Drawn;Present();}
  public void Suspend()=>Stow();
  void LateUpdate(){if(mode!=Mode.Inactive)Present();}
  void Present(){
   if(gun==null)return;bool slung=mode==Mode.Slung;OwnGuns();strap.enabled=slung;
   if(!slung){gun.SetParent(hand,false);gun.SetLocalPositionAndRotation(localPosition,localRotation);gun.localScale=localScale;return;}
   var body=visualActor!=null?visualActor:actor;var torso=body.GetBoneTransform(HumanBodyBones.Chest);
   var r=body.GetBoneTransform(HumanBodyBones.RightShoulder)??body.GetBoneTransform(HumanBodyBones.RightUpperArm);var l=body.GetBoneTransform(HumanBodyBones.LeftShoulder)??body.GetBoneTransform(HumanBodyBones.LeftUpperArm);
   var up=(body.GetBoneTransform(HumanBodyBones.Neck).position-body.GetBoneTransform(HumanBodyBones.Spine).position).normalized;
   var right=(r.position-l.position).normalized;var front=Vector3.Cross(right,up).normalized;up=Vector3.Cross(front,right).normalized;
   gun.SetParent(torso,true);var scale=torso.lossyScale;gun.localScale=new Vector3(worldScale.x/scale.x,worldScale.y/scale.y,worldScale.z/scale.z);
   gun.rotation=Quaternion.LookRotation((up-right*.16f-front*.04f).normalized,right)*Quaternion.Inverse(Quaternion.LookRotation(barrel,flat));
   var anchor=torso.position+right*.18f-front*.20f-up*.035f;gun.position+=anchor-gun.TransformPoint(centre);
   strap.SetPosition(0,anchor-up*.30f);strap.SetPosition(1,torso.position+right*.20f+up*.20f-front*.02f);strap.SetPosition(2,anchor+up*.30f);
  }
  public void Restore(){
   if(gun==null)return;mode=Mode.Inactive;strap.enabled=false;visualActor=actor;
   gun.SetParent(hand,false);gun.SetLocalPositionAndRotation(localPosition,localRotation);gun.localScale=localScale;
   foreach(var p in visibility)if(p.Key!=null){p.Key.enabled=p.Value.enabled;p.Key.forceRenderingOff=p.Value.hidden;}visibility.Clear();
   foreach(var p in activeGuns)if(p.Key!=null)p.Key.gameObject.SetActive(p.Value);activeGuns.Clear();
   var combat=root.GetComponent<PlayerCombat>();if(combat!=null)foreach(var w in root.GetComponentsInChildren<Weapon>(true))if(w.IsGun&&combat.WeaponFor(w.Kind)!=w)w.gameObject.SetActive(false);
  }
  void OnDestroy(){if(strap!=null)Destroy(strap.gameObject);if(strapMaterial!=null)Destroy(strapMaterial);}
 }
}
