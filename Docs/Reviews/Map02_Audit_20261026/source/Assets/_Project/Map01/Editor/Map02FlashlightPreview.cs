using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace ShadowVale.Map01.Editor
{
    // Isolated, disposable render only. Does not create assets or alter patrols.
    [InitializeOnLoad]
    public static class Map02FlashlightPreview
    {
        const string Request="Temp/Map02FlashlightPreview.request";
        const string Output="Logs/Map02Chapter/FlashlightPreview";
        static Map02FlashlightPreview(){EditorApplication.update+=Tick;}
        static void Tick()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating||!File.Exists(Request))return;
            File.Delete(Request);
            try{Render();}catch(Exception e){Directory.CreateDirectory(Output);File.WriteAllText(Output+"/error.txt",e.ToString());Debug.LogException(e);}
        }
        [MenuItem("ShadowVale/Map 2/Xem trước lính cầm đèn (chưa áp dụng)")]
        public static void Render()
        {
            var scene=EditorSceneManager.NewPreviewScene();
            var materials=new List<Material>();var meshes=new List<Mesh>();
            var previous=RenderTexture.active;
            try
            {
                var root=new GameObject("Disposable flashlight preview");SceneManager.MoveGameObjectToScene(root,scene);
                var visual=Object.Instantiate(Resources.Load<GameObject>("Characters/EnemyCommanderVisual"),root.transform);
                var actor=visual.GetComponentInChildren<Animator>();
                actor.runtimeAnimatorController=Map01ActorAssets.Load().enemyController;
                actor.Rebind();actor.Update(0);
                foreach(var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>()){skin.updateWhenOffscreen=true;skin.localBounds=new Bounds(Vector3.zero,Vector3.one*20);}
                var clip=actor.runtimeAnimatorController.animationClips.First(c=>c.name.StartsWith("Enemy_Rifle_AimIdle")||c.name.StartsWith("Nam_Rifle_AimIdle"));
                var grip=Map01EnemyGrip.BindPreview(actor);
                using(var pose=new Map01CinematicPose(actor))
                {
                    pose.Sample(clip,0);grip.Apply();pose.Ground(0);
                    var head=actor.GetBoneTransform(HumanBodyBones.Head);
                    var chest=actor.GetBoneTransform(HumanBodyBones.Chest);
                    var hand=actor.GetBoneTransform(HumanBodyBones.LeftHand);
                    var forward=actor.transform.forward;var right=actor.transform.right;
                    var rightHand=actor.GetBoneTransform(HumanBodyBones.RightHand);
                    Map01ArmContact.Reach(actor,false,chest.position+right*.25f-Vector3.up*.33f+forward*.12f,chest.position+right*.4f-Vector3.up*.2f,1);
                    var weapon=actor.GetComponent<Map01Rifle>().weapon.transform;
                    var gunGrip=weapon.GetComponentsInChildren<Renderer>().First(r=>r.name.StartsWith("bl_grip_low"));
                    var gripLocal=weapon.InverseTransformPoint(gunGrip.bounds.center);
                    weapon.rotation=Quaternion.LookRotation(forward*.35f-Vector3.up,-right);
                    var rightEnd=rightHand.Cast<Transform>().FirstOrDefault(t=>t.name.ToLowerInvariant().Contains("end"));
                    weapon.position+=(rightEnd!=null?Vector3.Lerp(rightHand.position,rightEnd.position,.43f):rightHand.position)-weapon.TransformPoint(gripLocal);
                    var target=head.position-right*.25f+forward*.32f-Vector3.up*.12f;
                    Map01ArmContact.Reach(actor,true,target,chest.position-right*.36f+forward*.12f-Vector3.up*.3f,1);
                    // Share pronation between the forearm and wrist so the cuff
                    // does not collapse. Both rotations preserve elbow/wrist positions.
                    var forearm=actor.GetBoneTransform(HumanBodyBones.LeftLowerArm);
                    var rollAxis=(hand.position-forearm.position).normalized;
                    forearm.rotation=Quaternion.AngleAxis(-30,rollAxis)*forearm.rotation;
                    hand.rotation=Quaternion.AngleAxis(-60,rollAxis)*hand.rotation;
                    var black=Mat(new Color(.035f,.042f,.049f),.65f,.48f,materials);
                    var ring=Mat(new Color(.09f,.105f,.12f),.8f,.6f,materials);
                    var lens=Mat(new Color(.55f,.8f,1),.1f,.85f,materials);
                    lens.EnableKeyword("_EMISSION");lens.SetColor("_EmissionColor",new Color(.5f,.8f,1)*2);
                    var torch=new GameObject("Temporary torch");torch.transform.SetParent(root.transform);
                    var end=hand.Cast<Transform>().FirstOrDefault(t=>t.name.ToLowerInvariant().Contains("end"));
                    var palm=end!=null?Vector3.Lerp(hand.position,end.position,.68f):hand.position;
                    var fingerBase=actor.GetBoneTransform(HumanBodyBones.LeftMiddleProximal);var fingerTip=actor.GetBoneTransform(HumanBodyBones.LeftMiddleDistal);
                    if(fingerBase!=null&&fingerTip!=null)palm=Vector3.Lerp(fingerBase.position,fingerTip.position,.55f);
                    torch.transform.position=palm+forward*.035f;torch.transform.rotation=Quaternion.LookRotation(forward);
                    Cylinder(torch.transform,"Handle",0,.135f,.033f,black);
                    Cylinder(torch.transform,"Head",.09f,.055f,.049f,black);
                    Cylinder(torch.transform,"Lens",.119f,.003f,.038f,lens);
                    Cylinder(torch.transform,"Tail",-.082f,.024f,.037f,ring);
                    for(int i=0;i<10;i++)Cylinder(torch.transform,"Grip ring",-.06f+i*.012f,.0022f,.035f,ring);
                    Cylinder(torch.transform,"Head rim",.117f,.006f,.05f,ring);
                    var clipGo=GameObject.CreatePrimitive(PrimitiveType.Cube);clipGo.transform.SetParent(torch.transform,false);
                    clipGo.transform.localPosition=new Vector3(.021f,0,-.024f);clipGo.transform.localScale=new Vector3(.004f,.009f,.105f);clipGo.GetComponent<Renderer>().sharedMaterial=ring;
                    var spot=new GameObject("Torch light").AddComponent<Light>();spot.transform.SetParent(torch.transform,false);spot.transform.localPosition=Vector3.forward*.125f;spot.type=LightType.Spot;spot.range=12;spot.spotAngle=32;spot.intensity=5;spot.color=new Color(.7f,.85f,1);
                    var floor=GameObject.CreatePrimitive(PrimitiveType.Plane);floor.transform.SetParent(root.transform,false);floor.transform.position=new Vector3(0,-.015f,0);floor.GetComponent<Renderer>().sharedMaterial=Mat(new Color(.12f,.15f,.14f),0,.1f,materials);
                    Light(root.transform,new Vector3(45,-35,0),2.4f,Color.white);
                    Light(root.transform,new Vector3(20,145,0),1.5f,new Color(.65f,.8f,1));
                    var camera=new GameObject("Preview camera").AddComponent<Camera>();camera.transform.SetParent(root.transform,false);camera.scene=scene;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.045f,.06f,.075f);camera.fieldOfView=33;camera.nearClipPlane=.05f;camera.farClipPlane=40;
                    Directory.CreateDirectory(Output);
                    File.WriteAllText(Output+"/pose.txt","forward "+forward+" head "+head.position+" hand "+hand.position+" upper "+actor.GetBoneTransform(HumanBodyBones.LeftUpperArm).position+" elbow "+actor.GetBoneTransform(HumanBodyBones.LeftLowerArm).position+"\nHand basis "+hand.right+" "+hand.up+" "+hand.forward+"\n"+string.Join("\n",hand.GetComponentsInChildren<Transform>().Select(t=>t.name+" "+hand.InverseTransformPoint(t.position)))+"\nSkins\n"+string.Join("\n",actor.GetComponentsInChildren<SkinnedMeshRenderer>().Select(s=>s.name)));
                    // Bake after manual posing so editor skinning cannot display a cached frame.
                    foreach(var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>())
                    {var mesh=new Mesh();skin.BakeMesh(mesh);meshes.Add(mesh);var g=new GameObject("Posed mesh");g.transform.SetParent(skin.transform,false);g.AddComponent<MeshFilter>().sharedMesh=mesh;g.AddComponent<MeshRenderer>().sharedMaterials=skin.sharedMaterials;skin.enabled=false;}
                    Capture(camera,forward*4.4f-right*2.5f+Vector3.up*1.65f,new Vector3(0,1,0),Output+"/soldier-flashlight-approved.png");
                    Capture(camera,forward*2f-right*1.25f+Vector3.up*1.7f,head.position-Vector3.up*.18f,Output+"/hand-detail-approved.png");
                }
            }
            finally{RenderTexture.active=previous;EditorSceneManager.ClosePreviewScene(scene);foreach(var m in materials)Object.DestroyImmediate(m);foreach(var m in meshes)Object.DestroyImmediate(m);}
        }
        static Material Mat(Color color,float metal,float smooth,List<Material> list)
        {var m=new Material(Shader.Find("Universal Render Pipeline/Lit"));m.SetColor("_BaseColor",color);m.SetFloat("_Metallic",metal);m.SetFloat("_Smoothness",smooth);list.Add(m);return m;}
        static void Cylinder(Transform parent,string name,float z,float length,float diameter,Material mat)
        {var g=GameObject.CreatePrimitive(PrimitiveType.Cylinder);g.name=name;g.transform.SetParent(parent,false);g.transform.localPosition=new Vector3(0,0,z);g.transform.localRotation=Quaternion.Euler(90,0,0);g.transform.localScale=new Vector3(diameter,length*.5f,diameter);g.GetComponent<Renderer>().sharedMaterial=mat;Object.DestroyImmediate(g.GetComponent<Collider>());}
        static void Light(Transform root,Vector3 angle,float intensity,Color color)
        {var l=new GameObject("Studio light").AddComponent<Light>();l.transform.SetParent(root,false);l.transform.rotation=Quaternion.Euler(angle);l.type=LightType.Directional;l.intensity=intensity;l.color=color;}
        static void Capture(Camera camera,Vector3 from,Vector3 target,string path)
        {
            var rt=new RenderTexture(1200,1200,24);var image=new Texture2D(1200,1200,TextureFormat.RGB24,false);
            try{camera.transform.position=from;camera.transform.LookAt(target);camera.targetTexture=rt;camera.Render();camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1200,1200),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());}
            finally{camera.targetTexture=null;RenderTexture.active=null;Object.DestroyImmediate(rt);Object.DestroyImmediate(image);}
        }
    }
}
