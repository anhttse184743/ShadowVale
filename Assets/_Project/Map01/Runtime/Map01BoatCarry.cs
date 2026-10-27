using System.Collections.Generic;
using UnityEngine;
using ShadowVale.Gameplay.Combat;
namespace ShadowVale.Map01 {
 // The same rifle changes socket; no second weapon is left in the hands during travel.
 public sealed class Map01BoatCarry : MonoBehaviour {
  Animator actor;Transform gun,hand,chest;Vector3 localPosition,localScale,centre,barrel,flat;Quaternion localRotation;
  readonly Dictionary<GameObject,bool> otherWeapons=new();LineRenderer strap;Material strapMaterial;
  public bool Slung { get;private set; }
  public Transform Gun=>gun;
  public static Map01BoatCarry For(Animator body){
   var item=body.GetComponent<Map01BoatCarry>();if(item!=null)return item;
   item=body.gameObject.AddComponent<Map01BoatCarry>();item.Bind(body);return item;
  }
  void Bind(Animator body){
   actor=body;gun=Map01Rifle.Attach(body).weapon.transform;hand=gun.parent;chest=body.GetBoneTransform(HumanBodyBones.Chest);
   localPosition=gun.localPosition;localRotation=gun.localRotation;localScale=gun.localScale;
   Bounds bounds=default;bool measured=false;
   foreach(var r in gun.GetComponentsInChildren<MeshRenderer>(true)){
    var mesh=r.GetComponent<MeshFilter>()?.sharedMesh;if(mesh==null||!r.enabled)continue;var b=mesh.bounds;
    for(int i=0;i<8;i++){var v=gun.InverseTransformPoint(r.transform.TransformPoint(b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1))));if(measured)bounds.Encapsulate(v);else{bounds=new Bounds(v,Vector3.zero);measured=true;}}
   }
   centre=bounds.center;var size=bounds.size;int along=size.x>=size.y&&size.x>=size.z?0:size.y>=size.z?1:2,across=size.x<=size.y&&size.x<=size.z?0:size.y<=size.z?1:2;
   barrel=flat=Vector3.zero;barrel[along]=gun.InverseTransformPoint(body.GetComponent<Map01Rifle>().weapon.Muzzle.position)[along]>=centre[along]?1:-1;flat[across]=1;
   var go=new GameObject("Boat rifle canvas strap");go.transform.SetParent(body.transform,false);strap=go.AddComponent<LineRenderer>();strap.positionCount=3;strap.widthMultiplier=.022f;strap.useWorldSpace=true;
   strapMaterial=new Material(Shader.Find("Universal Render Pipeline/Simple Lit"));strapMaterial.color=new Color(.15f,.13f,.08f);strap.sharedMaterial=strapMaterial;strap.enabled=false;
  }
  public void Stow(){
   if(gun==null)return;Slung=true;
   foreach(var weapon in actor.GetComponentsInChildren<Weapon>(true))if(weapon.IsGun&&weapon.transform!=gun){if(!otherWeapons.ContainsKey(weapon.gameObject))otherWeapons.Add(weapon.gameObject,weapon.gameObject.activeSelf);weapon.gameObject.SetActive(false);}
   gun.gameObject.SetActive(true);gun.SetParent(chest,false);gun.localScale=localScale;
   var right=Vector3.ProjectOnPlane(actor.GetBoneTransform(HumanBodyBones.RightUpperArm).position-actor.GetBoneTransform(HumanBodyBones.LeftUpperArm).position,Vector3.up).normalized;
   var front=Vector3.Cross(right,Vector3.up).normalized;
   gun.rotation=Quaternion.LookRotation((Vector3.up-front*.16f).normalized,right)*Quaternion.Inverse(Quaternion.LookRotation(barrel,flat));
   var anchor=chest.position+right*.29f-front*.25f-Vector3.up*.08f;gun.position+=anchor-gun.TransformPoint(centre);
   strap.enabled=true;strap.SetPosition(0,anchor-Vector3.up*.3f);strap.SetPosition(1,chest.position+right*.24f+Vector3.up*.22f-front*.02f);strap.SetPosition(2,anchor+Vector3.up*.3f);
  }
  public void Draw(){if(gun==null)return;Slung=false;strap.enabled=false;gun.SetParent(hand,false);gun.SetLocalPositionAndRotation(localPosition,localRotation);gun.localScale=localScale;gun.gameObject.SetActive(true);}
  public void Suspend(){if(gun!=null)gun.gameObject.SetActive(false);if(strap!=null)strap.enabled=false;}
  public void Restore(){Draw();foreach(var p in otherWeapons)if(p.Key!=null)p.Key.SetActive(p.Value);otherWeapons.Clear();}
  void OnDestroy(){if(strap!=null)Destroy(strap.gameObject);if(strapMaterial!=null)Destroy(strapMaterial);}
 }
}
