using UnityEngine;
namespace ShadowVale.Map01
{
    /// <summary>
    /// Day 1's bandage: Chị Lành dresses Hùng's upper arm by the stool. The dressing is measured to the sleeve it
    /// wraps and her hands keep contact with it while it is tied.
    /// </summary>
    public sealed partial class Map02ChapterDirector
    {
        static readonly Vector3 BandageStool=new Vector3(-.001f,0,-.026f),BandageLanh=new Vector3(.52f,0,.12f);
        const float BandageRollIn=3.3f,BandageRollOut=19.9f,WrapStart=5f,WrapEnd=18.6f;
        Transform bandageSleeve;Vector3 bandageAt;Quaternion bandageFacing;
        float dressingRadius=.057f;bool dressingMeasured;
        Mesh bandageMesh;readonly Vector3[] wrapVertices=new Vector3[162],wrapNormals=new Vector3[162];
        bool BandageMocap=>DayClip("HungThayBang")!=null&&DayClip("LanhThayBang")!=null;
        void StageBandage()
        {
            var hung=roles["Hùng"];var lanh=roles["Chị Lành"];
            bandageAt=hung.transform.position;bandageFacing=Quaternion.LookRotation(Vector3.ProjectOnPlane(hung.transform.forward,Vector3.up));
            sceneChair.SetPositionAndRotation(bandageAt+bandageFacing*BandageStool,bandageFacing);
            if(!BandageMocap)return;
            dressingMeasured=false;HungNativeBody(true);lanh.StopAt(bandageAt+bandageFacing*BandageLanh);lanh.transform.rotation=bandageFacing*Quaternion.LookRotation(Vector3.left);
            roles["Ông Sáu"].SetVisible(false);
            EnsureDressingArt();
            bandageSleeve.gameObject.SetActive(false);
        }
        void EnsureDressingArt()
        {
            if(bandageSleeve==null)
            {
                var gauze=DayMaterial("Clean bandage",new Color(.9f,.88f,.82f));gauze.SetFloat("_Cull",0);
                bandageSleeve=Group("Dressing on Hùng's injured right upper arm",Vector3.zero);
                bandageMesh=new Mesh{name="Overlapping cloth turns — right upper arm"};bandageMesh.MarkDynamic();dayMeshes.Add(bandageMesh);
                bandageSleeve.gameObject.AddComponent<MeshFilter>().sharedMesh=bandageMesh;
                var render=bandageSleeve.gameObject.AddComponent<MeshRenderer>();render.sharedMaterial=gauze;render.allowOcclusionWhenDynamic=false;
                var props=new MaterialPropertyBlock();props.SetColor("_BaseColor",gauze.color);render.SetPropertyBlock(props);
                var uv=new Vector2[162];var triangles=new int[480];
                for(int i=0;i<=80;i++){uv[i*2]=new Vector2(i/20f,0);uv[i*2+1]=new Vector2(i/20f,1);if(i==80)continue;int k=i*6,a=i*2;triangles[k]=a;triangles[k+1]=a+2;triangles[k+2]=a+1;triangles[k+3]=a+1;triangles[k+4]=a+2;triangles[k+5]=a+3;}
                bandageMesh.vertices=wrapVertices;bandageMesh.uv=uv;bandageMesh.triangles=triangles;
                if(bandageRoll!=null){bandageRoll.localScale=Vector3.one*.55f;var cotton=bandageRoll.Find("Cotton");if(cotton!=null)cotton.localScale=new Vector3(.075f,.045f,.075f);foreach(var r in bandageRoll.GetComponentsInChildren<Renderer>())r.sharedMaterial=gauze;}
            }
        }
        static Transform DressingFinger(Animator actor,bool left)
        {
            var finger=actor.GetBoneTransform(left?HumanBodyBones.LeftMiddleProximal:HumanBodyBones.RightMiddleProximal)??actor.GetBoneTransform(left?HumanBodyBones.LeftIndexProximal:HumanBodyBones.RightIndexProximal);
            var hand=actor.GetBoneTransform(left?HumanBodyBones.LeftHand:HumanBodyBones.RightHand);
            return finger??(hand.childCount>0?hand.GetChild(0):null);
        }
        static Vector3 DressingPalm(Animator actor,bool left)
        {
            var hand=actor.GetBoneTransform(left?HumanBodyBones.LeftHand:HumanBodyBones.RightHand);var lower=actor.GetBoneTransform(left?HumanBodyBones.LeftLowerArm:HumanBodyBones.RightLowerArm);
            var finger=DressingFinger(actor,left);var direction=finger!=null?finger.position-hand.position:hand.position-lower.position;
            return hand.position+direction.normalized*.05f;
        }
        static void DressingContact(Animator actor,bool left,Vector3 palm,float weight)
        {
            var upper=actor.GetBoneTransform(left?HumanBodyBones.LeftUpperArm:HumanBodyBones.RightUpperArm);
            var lower=actor.GetBoneTransform(left?HumanBodyBones.LeftLowerArm:HumanBodyBones.RightLowerArm);
            var hand=actor.GetBoneTransform(left?HumanBodyBones.LeftHand:HumanBodyBones.RightHand);
            var forward=BodyForward(actor,actor.transform);var right=Vector3.Cross(Vector3.up,forward);
            var hint=upper.position+right*(left?-.3f:.3f)+forward*.15f-Vector3.up*.18f;
            for(int i=0;i<3;i++)
            {
                Map01ArmContact.Reach(actor,left,palm-(DressingPalm(actor,left)-hand.position),hint,weight);
                var finger=DressingFinger(actor,left);
                if(finger==null)continue;
                var along=(hand.position-lower.position).normalized;var direction=(finger.position-hand.position).normalized;
                float angle=Vector3.Angle(direction,along);
                if(angle>25)hand.rotation=Quaternion.Slerp(hand.rotation,Quaternion.FromToRotation(direction,along)*hand.rotation,weight*(angle-25)/angle);
            }
        }
        void MeasureDressingRadius()
        {
            if(dressingMeasured)return;dressingMeasured=true;
            var body=roles["Hùng"].Animator;var upper=body.GetBoneTransform(HumanBodyBones.RightUpperArm);var a=upper.position;var b=body.GetBoneTransform(HumanBodyBones.RightLowerArm).position;var axis=b-a;
            var radii=new System.Collections.Generic.List<float>();var baked=new Mesh();
            foreach(var skin in body.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                skin.BakeMesh(baked);
                // Measure the sleeve, not nearby chest/pouches caught by the same axial slice.
                var weights=skin.sharedMesh!=null&&skin.sharedMesh.isReadable?skin.sharedMesh.boneWeights:null;
                var bones=skin.bones;var armBones=new bool[bones.Length];
                for(int i=0;i<bones.Length;i++)armBones[i]=bones[i]!=null&&(bones[i]==upper||bones[i].IsChildOf(upper));
                var vertices=baked.vertices;
                for(int i=0;i<vertices.Length;i++){
                    if(weights!=null&&weights.Length==vertices.Length){var w=weights[i];
                        float influence=(armBones[w.boneIndex0]?w.weight0:0)+(armBones[w.boneIndex1]?w.weight1:0)+(armBones[w.boneIndex2]?w.weight2:0)+(armBones[w.boneIndex3]?w.weight3:0);
                        if(influence<.2f)continue;
                    }
                    var p=skin.transform.TransformPoint(vertices[i]);float along=Vector3.Dot(p-a,axis)/axis.sqrMagnitude;if(along<.3f||along>.8f)continue;
                    float radius=Vector3.Distance(p,a+axis*along);if(radius>.025f&&radius<.115f)radii.Add(radius);
                }
            }
            Destroy(baked);
            if(radii.Count>20){radii.Sort();dressingRadius=Mathf.Clamp(radii[(int)(radii.Count*.85f)]+.004f,.045f,Mathf.Clamp(axis.magnitude*.24f,.055f,.075f))/Mathf.Abs(body.transform.lossyScale.y);}
        }

        void DressingFrame(out Vector3 a,out Vector3 b,out Quaternion rotation,out float radius)
        {
            var body=roles["Hùng"].Animator;var shoulder=body.GetBoneTransform(HumanBodyBones.RightUpperArm).position;var elbow=body.GetBoneTransform(HumanBodyBones.RightLowerArm).position;
            a=Vector3.Lerp(shoulder,elbow,.77f);b=Vector3.Lerp(shoulder,elbow,.30f);
            var up=(b-a).normalized;var outward=Vector3.ProjectOnPlane(BodyForward(body,roles["Hùng"].transform),up).normalized;
            rotation=Quaternion.LookRotation(outward,up);radius=dressingRadius*Mathf.Abs(body.transform.lossyScale.y);
        }
        void SampleBandage()
        {
            var hung=roles["Hùng"];var lanh=roles["Chị Lành"];float t=sceneClock;
            hung.transform.SetPositionAndRotation(bandageAt,bandageFacing);lanh.transform.SetPositionAndRotation(bandageAt+bandageFacing*BandageLanh,bandageFacing*Quaternion.LookRotation(Vector3.left));
            hung.BlendPerformance(DayClip("InjuredIdle"),DayClip("HungThayBang"),t,Mathf.SmoothStep(0,1,t/.4f));
            lanh.SamplePerformance(DayClip("LanhThayBang"),t,false);lanh.GroundPerformance(bandageAt.y);if(t>=4)MeasureDressingRadius();
            float contact=Mathf.SmoothStep(0,1,(t-3.3f)/1.7f)*(1-Mathf.SmoothStep(0,1,(t-20f)/2f));
            var body=hung.Animator;var hands=lanh.Animator;
            if(body!=null&&hands!=null&&body.isHuman&&hands.isHuman)
            {
                var elbow=body.GetBoneTransform(HumanBodyBones.RightLowerArm).position;var wrist=body.GetBoneTransform(HumanBodyBones.RightHand).position;
                DressingContact(body,true,Vector3.Lerp(elbow,wrist,.7f)+Vector3.down*.045f,contact);
                DressingFrame(out var a,out var b,out var rotation,out float radius);
                float wrapped=Mathf.Clamp01((t-WrapStart)/(WrapEnd-WrapStart));float phase=wrapped*3.2f;
                float cycle=Mathf.Repeat(phase,1),rightAngle=(cycle<.5f?cycle:1-cycle)*Mathf.PI*2;
                float leftAngle=(cycle<.5f?1-cycle:cycle)*Mathf.PI*2;
                var centre=Vector3.Lerp(a,b,wrapped);
                Vector3 Point(float angle)=>centre+rotation*new Vector3(Mathf.Cos(angle)*(radius+.014f),0,Mathf.Sin(angle)*(radius+.014f));
                var axis=(b-a).normalized;
                DressingContact(hands,false,Point(rightAngle)+axis*.025f,contact);
                DressingContact(hands,true,Point(leftAngle)-axis*.025f,contact);
                if(bandageRoll!=null)
                {
                    bool held=t>=BandageRollIn&&t<=BandageRollOut;bandageRoll.gameObject.SetActive(held);
                    if(held){bool left=cycle>=.5f;var palm=DressingPalm(hands,left);bandageRoll.SetPositionAndRotation(palm,rotation*Quaternion.Euler(0,phase*360,0));}
                }
            }
            DressUpperArm(Mathf.Clamp01((t-WrapStart)/(WrapEnd-WrapStart)),t>=WrapStart+.2f);
        }
        void DressUpperArm(float wrapped,bool shown)
        {
            if(bandageSleeve==null)return;var body=roles["Hùng"].Animator;
            if(!shown||body==null||!body.isHuman||!roles["Hùng"].visual.gameObject.activeInHierarchy){bandageSleeve.gameObject.SetActive(false);return;}
            DressingFrame(out var a,out var b,out var rotation,out float radius);
            bandageSleeve.gameObject.SetActive(true);bandageSleeve.SetPositionAndRotation(a,rotation);bandageSleeve.localScale=Vector3.one;
            float length=Vector3.Distance(a,b),width=.048f*Mathf.Abs(body.transform.lossyScale.y);
            for(int i=0;i<=80;i++)
            {
                float fraction=i/80f*wrapped,angle=fraction*3.2f*Mathf.PI*2;var normal=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
                float y=fraction*length;wrapVertices[i*2]=normal*radius+Vector3.up*(y-width*.5f);wrapVertices[i*2+1]=normal*radius+Vector3.up*(y+width*.5f);
                wrapNormals[i*2]=wrapNormals[i*2+1]=normal;
            }
            bandageMesh.vertices=wrapVertices;bandageMesh.normals=wrapNormals;bandageMesh.RecalculateBounds();
        }
        void UpdateBandageSleeve()
        {
            if(sceneTask?.id=="D1_02")return;
            bool shown=State.phase<=1&&State.Done("D1_02")&&BandageMocap;
            if(shown){EnsureDressingArt();MeasureDressingRadius();}
            DressUpperArm(1,shown);
        }
    }
}
