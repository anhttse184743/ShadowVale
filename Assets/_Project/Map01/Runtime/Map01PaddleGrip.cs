using System;
using System.Collections.Generic;
using UnityEngine;
namespace ShadowVale.Map01 {
/// <summary>Reversible grip shapes for the commander's wrist-only hand rig. Original meshes/Avatar stay untouched.</summary>
public sealed class Map01PaddleGrip:IDisposable {
 struct Hand {public Transform bone;public Vector3 fingers,normal,spread,anchor;}
 struct Skin {public SkinnedMeshRenderer renderer;public Mesh original,copy;public int left,right;public float[] originalWeights;}
 readonly List<Skin> skins=new();readonly Dictionary<Transform,Hand> hands=new();
 public Map01PaddleGrip(Animator actor){
  foreach(var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>()){
   var original=skin.sharedMesh;if(original==null)continue;
   var readable=original.isReadable?original:Resources.Load<Mesh>("Cutscenes/Commander_Paddle_Readable");
   if(readable==null||!readable.isReadable||readable.vertexCount!=original.vertexCount)continue;
   Mesh copy=null;int[] shapes={-1,-1};
   for(int side=0;side<2;side++){
    var hand=actor.GetBoneTransform(side==0?HumanBodyBones.LeftHand:HumanBodyBones.RightHand);int index=Array.IndexOf(skin.bones,hand);if(index<0||hand.childCount==0)continue;
    var vertices=readable.vertices;var weights=readable.boneWeights;var bind=readable.bindposes[index];var inverse=bind.inverse;
    var fingers=hand.InverseTransformDirection(hand.GetChild(0).position-hand.position).normalized;
    var points=new List<Vector3>();float length=0;
    for(int i=0;i<vertices.Length;i++)if(Influence(weights[i],index)>.7f){var point=bind.MultiplyPoint3x4(vertices[i]);points.Add(point);length=Mathf.Max(length,Vector3.Dot(point,fingers));}
    if(points.Count<20||length<=.001f)continue;
    // The thin direction of the distal hand is its palm normal, independent of imported bone axes/scale.
    Vector3 normal=Vector3.zero;float best=float.PositiveInfinity;
    foreach(var candidate in new[]{Vector3.right,Vector3.up,Vector3.forward}){
     var n=Vector3.ProjectOnPlane(candidate,fingers).normalized;if(n.sqrMagnitude<.5f)continue;
     float count=0,mean=0,second=0;foreach(var point in points)if(Vector3.Dot(point,fingers)>length*.5f){float v=Vector3.Dot(point,n);count++;mean+=v;second+=v*v;}
     float variance=second/Mathf.Max(1,count)-Mathf.Pow(mean/Mathf.Max(1,count),2);
     if(variance<best){best=variance;normal=n;}
    }
    var spread=Vector3.Cross(normal,fingers).normalized;
    float knuckle=length*.49f,radius=length*.14f;
    var deltas=new Vector3[vertices.Length];var changed=(Vector3[])vertices.Clone();
    for(int i=0;i<vertices.Length;i++){
     float w=Influence(weights[i],index);if(w<.05f)continue;var point=bind.MultiplyPoint3x4(vertices[i]);float y=Vector3.Dot(point,fingers),z=Vector3.Dot(point,spread);
     // Leave the wrist/cuff and the widely spread thumb intact. Curl the four fingers around the wooden shaft.
     if(y<=knuckle||Mathf.Abs(z)>length*.25f)continue;
     float theta=Mathf.Min((y-knuckle)/radius,3.5f);float x=Vector3.Dot(point,normal);
     var curved=point+fingers*(knuckle+radius*Mathf.Sin(theta)-(x*Mathf.Sin(theta))-y)+normal*(radius*(1-Mathf.Cos(theta))+x*(Mathf.Cos(theta)-1));
     float thumbBlend=1-Mathf.SmoothStep(0,1,(Mathf.Abs(z)/length-.20f)/.05f);
     deltas[i]=(inverse.MultiplyPoint3x4(curved)-vertices[i])*w*thumbBlend;changed[i]+=deltas[i];
    }
    if(copy==null){copy=UnityEngine.Object.Instantiate(readable);RemoveOwnedShapes(copy);}
    copy.name=original.name+" (temporary paddle grip)";
    shapes[side]=copy.blendShapeCount;
    var normals=readable.normals;var temp=UnityEngine.Object.Instantiate(readable);temp.vertices=changed;temp.RecalculateNormals();var shapedNormals=temp.normals;UnityEngine.Object.Destroy(temp);
    var normalDeltas=new Vector3[vertices.Length];if(normals.Length==vertices.Length)for(int i=0;i<vertices.Length;i++)normalDeltas[i]=shapedNormals[i]-normals[i];
    copy.AddBlendShapeFrame(side==0?"PaddleGripLeft":"PaddleGripRight",100,deltas,normalDeltas,new Vector3[vertices.Length]);
    hands[hand]=new Hand{bone=hand,fingers=fingers,normal=normal,spread=spread,anchor=fingers*knuckle+normal*radius};
   }
   if(copy!=null){
    var saved=new float[original.blendShapeCount];
    for(int i=0;i<saved.Length;i++)saved[i]=skin.GetBlendShapeWeight(i);
    skin.sharedMesh=copy;
    for(int i=0;i<saved.Length;i++){
     string name=original.GetBlendShapeName(i);int target=copy.GetBlendShapeIndex(name);
     if(target>=0&&!OwnedShape(name))skin.SetBlendShapeWeight(target,saved[i]);
    }
    foreach(int index in shapes)if(index>=0)skin.SetBlendShapeWeight(index,0);
    skins.Add(new Skin{renderer=skin,original=original,copy=copy,left=shapes[0],right=shapes[1],originalWeights=saved});
   }
  }
 }
 static bool OwnedShape(string name)=>name=="PaddleGripLeft"||name=="PaddleGripRight";
 struct Frame {public string name;public float weight;public Vector3[] vertices,normals,tangents;}
 static void RemoveOwnedShapes(Mesh mesh){
  bool found=false;for(int i=0;i<mesh.blendShapeCount;i++)found|=OwnedShape(mesh.GetBlendShapeName(i));
  if(!found)return;
  // A loaded/reused commander may already have our temporary shapes. Unity can
  // append frames only to the last shape, so rebuild only our channels on the copy.
  // Preserve every unrelated facial/clothing shape, including all of its frames.
  var keep=new List<Frame>();
  for(int i=0;i<mesh.blendShapeCount;i++){
   string name=mesh.GetBlendShapeName(i);if(OwnedShape(name))continue;
   for(int f=0;f<mesh.GetBlendShapeFrameCount(i);f++){
    var frame=new Frame{name=name,weight=mesh.GetBlendShapeFrameWeight(i,f),vertices=new Vector3[mesh.vertexCount],normals=new Vector3[mesh.vertexCount],tangents=new Vector3[mesh.vertexCount]};
    mesh.GetBlendShapeFrameVertices(i,f,frame.vertices,frame.normals,frame.tangents);keep.Add(frame);
   }
  }
  mesh.ClearBlendShapes();foreach(var frame in keep)mesh.AddBlendShapeFrame(frame.name,frame.weight,frame.vertices,frame.normals,frame.tangents);
 }
 static float Influence(BoneWeight w,int bone)=>(w.boneIndex0==bone?w.weight0:0)+(w.boneIndex1==bone?w.weight1:0)+(w.boneIndex2==bone?w.weight2:0)+(w.boneIndex3==bone?w.weight3:0);
 public bool TryGrip(Transform hand,out Vector3 point){if(hands.TryGetValue(hand,out var h)){point=hand.TransformPoint(h.anchor);return true;}point=default;return false;}
 public void Orient(Transform hand,Vector3 shaft,Vector3 forearm,float weight){
  if(!hands.TryGetValue(hand,out var h)||weight<=0)return;
  var finger=Vector3.ProjectOnPlane(forearm,shaft).normalized;if(finger.sqrMagnitude<.5f)return;
  var source=Quaternion.LookRotation(h.spread,h.fingers);var best=hand.rotation;float angle=float.PositiveInfinity;
  // Choose the equivalent grasp requiring the least wrist rotation, rather than imposing an FBX-specific twist.
  foreach(float a in new[]{-1f,1f})foreach(float b in new[]{-1f,1f}){
   var rotation=Quaternion.LookRotation(shaft*a,finger*b)*Quaternion.Inverse(source);float delta=Quaternion.Angle(hand.rotation,rotation);
   if(delta<angle){angle=delta;best=rotation;}
  }
  hand.rotation=Quaternion.Slerp(hand.rotation,best,weight);
 }
 public void Set(float left,float right){foreach(var skin in skins)if(skin.renderer!=null){if(skin.left>=0)skin.renderer.SetBlendShapeWeight(skin.left,100*Mathf.Clamp01(left));if(skin.right>=0)skin.renderer.SetBlendShapeWeight(skin.right,100*Mathf.Clamp01(right));}}
 public void Dispose(){foreach(var skin in skins){if(skin.renderer!=null&&skin.renderer.sharedMesh==skin.copy){
 skin.renderer.sharedMesh=skin.original;
 for(int i=0;i<skin.originalWeights.Length;i++)skin.renderer.SetBlendShapeWeight(i,skin.originalWeights[i]);
}if(skin.copy!=null)UnityEngine.Object.Destroy(skin.copy);}skins.Clear();hands.Clear();}
}
}
