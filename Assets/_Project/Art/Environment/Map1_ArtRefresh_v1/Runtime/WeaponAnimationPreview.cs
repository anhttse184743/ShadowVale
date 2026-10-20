using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using ShadowVale.Gameplay.Combat;

namespace ShadowVale.EnvironmentReview
{
    /// <summary>A self-contained art review. No mission, ammunition, damage, inventory or save code runs here.</summary>
    public sealed class WeaponAnimationPreview:MonoBehaviour
    {
        [Serializable] public class Take {public string item,label,action;public AnimationClip clip;public bool loop,newPose;public string magazineState;}
        [Serializable] public class Prop {
            public string item;public Transform root;public Vector3 origin,forward,up,grip,support,magWell,boltKnob,boltAxis,pullRing;
            public Transform slide,magazine,bolt,ring,lever,round;public Vector3 slideHome,magHome,boltHome,ringHome,leverHome,boltContact;public Quaternion magRotation,boltRotation,ringRotation,leverRotation;
        }
        public Animator actor;public Camera reviewCamera;public Take[] takes;public Prop[] props;
        public Quaternion handReference,leftHandReference;public Vector3 palmOffset=new Vector3(0,.045f,0);
        public ReloadMagazineCarrier carrier;public int selected;public bool playing=true;public float seconds,speed=1;
        PlayableGraph graph;AnimationClipPlayable playable;int loaded=-1;GUIStyle title,caption;
        ShadowVale.Map01.ForestMenu previewMenu;
        public Take Current=>takes!=null&&takes.Length>0?takes[Mathf.Clamp(selected,0,takes.Length-1)]:null;
        public Prop CurrentProp=>Array.Find(props,p=>p.item==Current?.item);
        void Start(){Cursor.lockState=CursorLockMode.None;Cursor.visible=true;previewMenu=FindFirstObjectByType<ShadowVale.Map01.ForestMenu>();if(previewMenu!=null)previewMenu.enabled=false;}
        void LateUpdate(){if(Current==null)return;if(playing){seconds+=Time.unscaledDeltaTime*speed;if(seconds>Current.clip.length)seconds=Current.loop?seconds%Current.clip.length:Current.clip.length;}Sample(selected,seconds);}
        public void Sample(int takeIndex,float at)
        {
            selected=Mathf.Clamp(takeIndex,0,takes.Length-1);var take=Current;
            if(loaded!=selected||!graph.IsValid()){
                if(graph.IsValid())graph.Destroy();graph=PlayableGraph.Create("Weapon animation art review");graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                playable=AnimationClipPlayable.Create(graph,take.clip);playable.SetApplyFootIK(false);playable.SetSpeed(0);
                AnimationPlayableOutput.Create(graph,"Pose",actor).SetSourcePlayable(playable);graph.Play();loaded=selected;
            }
            if(carrier!=null)carrier.Evaluate(null,0);
            seconds=Mathf.Clamp(at,0,take.clip.length);playable.SetTime(seconds);graph.Evaluate(0);
            foreach(var p in props)p.root.gameObject.SetActive(p.item==take.item);
            var prop=CurrentProp;float u=take.clip.length>0?seconds/take.clip.length:0;
            if(take.newPose&&prop!=null) {
                PoseProp(prop,take,u);
                // The contact pass runs after the authored clip, as the gameplay radio/AK corrections do.
                // It stabilizes retargeting without copying an AK reload onto a pistol or bolt-action rifle.
                WeaponReviewMotion.Contacts(actor,transform,prop,take.action,u,palmOffset);
                PoseProp(prop,take,u);
            }
            if(carrier!=null&&take.item=="rifle_standard")carrier.Evaluate(take.magazineState,u);
        }
        public void PoseProp(Prop p,Take t,float u)
        {
            var hand=actor.GetBoneTransform(HumanBodyBones.RightHand);
            Quaternion frame=Quaternion.LookRotation(p.forward-p.origin,p.up-p.origin);
            bool fixedGun=t.item=="rifle_mosin"&&(t.action=="bolt"||t.action.StartsWith("reload"));
            Quaternion rotation=hand.rotation*Quaternion.Inverse(handReference)*transform.rotation*Quaternion.Inverse(frame);
            Vector3 seat=hand.TransformPoint(palmOffset);
            if(fixedGun){rotation=transform.rotation*Quaternion.Euler(t.action=="bolt"?0:-12,0,0)*Quaternion.Inverse(frame);seat=transform.TransformPoint(t.action=="bolt"?new Vector3(.18f,1.70f,.12f):new Vector3(.18f,1.39f,.15f));}
            p.root.rotation=rotation;p.root.position+=seat-p.root.TransformPoint(p.grip);
            if(t.action=="draw"||t.action=="holster")ShowRenderers(p.root,t.action=="draw"?u>.12f:u<.88f);
            else ShowRenderers(p.root,true);
            if(t.item=="pistol_k54")Pistol(p,t.action,u);
            if(t.item=="rifle_mosin"){Mosin(p,t.action,u);if(p.round!=null&&p.round.gameObject.activeSelf){p.round.position=actor.GetBoneTransform(HumanBodyBones.RightHand).TransformPoint(palmOffset);p.round.rotation=transform.rotation;}}
            if(t.item=="grenade_f1"||t.item=="smoke_bomb")Throwable(p,t.action,u);
        }
        static float Pulse(float u,float a,float b,float c)=>u<a||u>c?0:u<b?Mathf.SmoothStep(0,1,Mathf.InverseLerp(a,b,u)):1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(b,c,u));
        void Pistol(Prop p,string action,float u)
        {
            Vector3 barrel=(p.forward-p.origin).normalized;
            float back=action=="fire"?Pulse(u,.02f,.10f,.27f):action=="fire_empty"?Mathf.SmoothStep(0,1,u*12):action=="reload_empty"?1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.75f,.85f,u)):0;
            if(p.slide!=null)p.slide.localPosition=p.slideHome-barrel*.027f*back;
            if(p.magazine==null)return;p.magazine.localPosition=p.magHome;p.magazine.localRotation=p.magRotation;
            bool reload=action.StartsWith("reload");bool carrying=reload&&u>.2f&&u<.67f;
            if(carrying){float seat=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.62f,.67f,u));p.magazine.position=Vector3.Lerp(actor.GetBoneTransform(HumanBodyBones.LeftHand).TransformPoint(palmOffset),p.root.TransformPoint(p.magHome),seat);}
            ShowRenderers(p.magazine,!reload||u<.36f||u>.49f);
        }
        static void Mosin(Prop p,string action,float u)
        {
            if(p.bolt==null)return;p.bolt.localPosition=p.boltHome;p.bolt.localRotation=p.boltRotation;
            if(p.round!=null)p.round.gameObject.SetActive(false);
            if(action!="bolt"&&!action.StartsWith("reload"))return;
            float a=action=="bolt"?0:u<.84f?u/.84f:1;
            float lift=Pulse(a,.14f,.27f,.91f);float pull=Pulse(a,.28f,.43f,.77f);
            var axis=(p.forward-p.origin).normalized;
            p.bolt.localRotation=Quaternion.AngleAxis(70*lift,axis)*p.boltRotation;
            p.bolt.localPosition=p.boltHome-axis*.085f*pull;
            if(action.StartsWith("reload")){float open=Pulse(u,.06f,.18f,.92f);p.bolt.localRotation=Quaternion.AngleAxis(70*open,axis)*p.boltRotation;p.bolt.localPosition=p.boltHome-axis*.085f*open;}
            if(p.round!=null)p.round.gameObject.SetActive(action.StartsWith("reload")&&u>.24f&&u<.76f);
        }
        void Throwable(Prop p,string action,float u)
        {
            if(p.ring!=null){p.ring.localPosition=p.ringHome;p.ring.localRotation=p.ringRotation;
                if(action.Contains("throw")&&u>.15f){float pull=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.15f,.27f,u));p.ring.position+=transform.right*(-.10f*pull);ShowRenderers(p.ring,u<.40f);}
                else ShowRenderers(p.ring,true);}
            if(p.lever!=null){p.lever.localPosition=p.leverHome;p.lever.localRotation=p.leverRotation;ShowRenderers(p.lever,true);}
            bool thrown=action.Contains("throw")&&u>.57f;
            if(thrown){float flight=Mathf.Clamp01((u-.57f)/.35f);var from=transform.TransformPoint(new Vector3(.32f,1.55f,.62f));p.root.position=from+transform.forward*(flight*3.2f)+Vector3.up*(2*flight*(1-flight));
                if(p.lever!=null){p.lever.position+=transform.right*flight*.4f+Vector3.up*Mathf.Sin(flight*Mathf.PI)*.3f;p.lever.Rotate(transform.right,flight*440,Space.World);}}
        }
        static void ShowRenderers(Transform root,bool on){foreach(var r in root.GetComponentsInChildren<Renderer>(true))r.enabled=on;}
        public void ReleaseGraph(){if(graph.IsValid())graph.Destroy();loaded=-1;}
        void OnDisable()=>ReleaseGraph();
        void OnDestroy(){ReleaseGraph();if(previewMenu!=null)previewMenu.enabled=true;}
        void OnGUI()
        {
            if(Current==null)return;float scale=Mathf.Min(Screen.width/1600f,Screen.height/900f);var old=GUI.matrix;GUI.matrix=Matrix4x4.Scale(Vector3.one*scale);
            title??=new GUIStyle(GUI.skin.label){fontSize=27,fontStyle=FontStyle.Bold};caption??=new GUIStyle(GUI.skin.label){fontSize=17,wordWrap=true};
            GUI.Box(new Rect(22,18,1556,96),"");GUI.Label(new Rect(42,30,1500,40),"SHADOWVALE / DUYỆT ANIMATION VŨ KHÍ",title);
            GUI.Label(new Rect(42,73,1500,30),Current.label+"  •  "+(Current.newPose?"Động tác thiết kế cho bản xem thử":"Clip hiện có trong game"),caption);
            var ids=new[]{"rifle_standard","pistol_k54","rifle_mosin","knife","grenade_f1","smoke_bomb"};var labels=new[]{"AK","K54","Mosin PU","Dao","Lựu đạn F1","Bom khói"};
            for(int i=0;i<ids.Length;i++)if(GUI.Button(new Rect(42+i*170,126,160,36),labels[i])){selected=Array.FindIndex(takes,t=>t.item==ids[i]);seconds=0;playing=true;}
            GUI.Box(new Rect(22,719,1556,162),"");
            if(GUI.Button(new Rect(42,737,150,36),"◀ Động tác trước")){selected=(selected+takes.Length-1)%takes.Length;seconds=0;}
            if(GUI.Button(new Rect(202,737,120,36),playing?"Tạm dừng":"Phát"))playing=!playing;
            if(GUI.Button(new Rect(332,737,150,36),"Động tác sau ▶")){selected=(selected+1)%takes.Length;seconds=0;}
            if(GUI.Button(new Rect(492,737,130,36),"Góc trước"))View(0);if(GUI.Button(new Rect(632,737,130,36),"Góc bên"))View(1);if(GUI.Button(new Rect(772,737,130,36),"Cận tay"))View(2);
            speed=GUI.HorizontalSlider(new Rect(960,752,180,22),speed,.15f,1.5f);GUI.Label(new Rect(1160,738,320,34),$"Tốc độ {speed:0.00}× | {seconds:0.00}/{Current.clip.length:0.00}s",caption);
            float seek=GUI.HorizontalSlider(new Rect(42,792,1490,22),seconds,0,Current.clip.length);if(Mathf.Abs(seek-seconds)>.001f){seconds=seek;playing=false;}
            GUI.Label(new Rect(42,828,1490,46),"BẢN DUYỆT ART • K54/Mosin chưa mở chiến đấu. Tốc độ clip mới là đề xuất hình ảnh; không đổi sát thương, đạn, bán kính hay thời gian khói/nổ trong game.",caption);GUI.matrix=old;
        }
        public void View(int index){Vector3 eye=index==0?new Vector3(2.5f,1.6f,3.8f):index==1?new Vector3(3.4f,1.45f,.55f):new Vector3(1.7f,1.7f,2.1f);reviewCamera.transform.SetPositionAndRotation(transform.TransformPoint(eye),Quaternion.LookRotation(transform.TransformPoint(new Vector3(0,index==2?1.35f:.95f,.20f))-transform.TransformPoint(eye)));reviewCamera.fieldOfView=index==2?31:38;}
    }

    /// <summary>Art choreography only. The baker records these poses into independent Humanoid clips.</summary>
    public static class WeaponReviewMotion
    {
        static float S(float a,float b,float u)=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(a,b,u));
        public static void Targets(string item,string action,float u,out Vector3 right,out Vector3 left,out float tilt)
        {
            bool pistol=item=="pistol_k54",thrown=item=="grenade_f1"||item=="smoke_bomb";
            right=pistol?new Vector3(.20f,1.13f,.32f):new Vector3(.20f,1.24f,.31f);left=pistol?new Vector3(-.20f,1.04f,.10f):new Vector3(.02f,1.23f,.64f);tilt=18;
            if(action.Contains("aim")||action.StartsWith("fire")||action=="bolt"){right=pistol?new Vector3(.07f,1.70f,.36f):new Vector3(.18f,1.70f,.12f);left=pistol?new Vector3(.015f,1.69f,.31f):new Vector3(.10f,1.67f,.46f);tilt=0;}
            if(action.StartsWith("fire")){float kick=Mathf.Sin(Mathf.Clamp01(u/.30f)*Mathf.PI);right+=new Vector3(0,.018f,-.025f)*kick;tilt-=6*kick;}
            if(action=="draw"||action=="holster"){float t=action=="draw"?S(.12f,.86f,u):1-S(.12f,.86f,u);right=Vector3.Lerp(new Vector3(.26f,.93f,.04f),right,t);tilt=Mathf.Lerp(64,tilt,t);}
            if(action.StartsWith("reload")&&pistol){right=new Vector3(.18f,1.26f,.36f);tilt=-15;left=Vector3.Lerp(new Vector3(.16f,1.15f,.34f),new Vector3(-.23f,.94f,.04f),S(.23f,.38f,u)*(1-S(.48f,.68f,u)));}
            if(thrown){right=new Vector3(.22f,1.33f,.32f);left=new Vector3(.16f,1.36f,.34f);tilt=0;
                if(action.Contains("throw")){float wind=S(.30f,.46f,u),release=S(.49f,.59f,u),recover=S(.65f,.92f,u);right=Vector3.Lerp(right,new Vector3(.34f,1.62f,-.08f),wind);right=Vector3.Lerp(right,new Vector3(.30f,1.51f,.65f),release);right=Vector3.Lerp(right,new Vector3(.24f,1.10f,.32f),recover);left=Vector3.Lerp(left,new Vector3(-.18f,1.18f,.18f),S(.15f,.32f,u));}}
            if(action.Contains("crouch")){right.y-=.46f;left.y-=.46f;}
        }
        public static void Contacts(Animator a,Transform actor,WeaponAnimationPreview.Prop p,string action,float u,Vector3 palm)
        {
            if((p.item=="grenade_f1"||p.item=="smoke_bomb")&&action.Contains("throw")&&u<.31f){var h=a.GetBoneTransform(HumanBodyBones.LeftHand);var ring=p.root.TransformPoint(p.pullRing)-actor.right*(.10f*S(.15f,.27f,u));Arm(a,false,ring-h.rotation*palm,h.rotation);}
            if(p.item=="pistol_k54"&&!action.StartsWith("reload"))Arm(a,false,p.root.TransformPoint(p.support)-a.GetBoneTransform(HumanBodyBones.LeftHand).rotation*palm,a.GetBoneTransform(HumanBodyBones.LeftHand).rotation);
            if(p.item=="pistol_k54"&&action.StartsWith("reload")){
                var h=a.GetBoneTransform(HumanBodyBones.LeftHand);Vector3 well=p.root.TransformPoint(p.magHome),pocket=actor.TransformPoint(new Vector3(-.23f,.94f,.04f));
                float dip=S(.22f,.36f,u)*(1-S(.49f,.64f,u));var target=Vector3.Lerp(well,pocket,dip);
                if(action=="reload_empty"&&u>.69f&&u<.88f)target=p.root.TransformPoint(p.origin+(p.up-p.origin)*1.1f-(p.forward-p.origin)*.74f);
                float reach=S(0,.18f,u)*(1-S(.88f,.99f,u));Arm(a,false,Vector3.Lerp(h.position,target-h.rotation*palm,reach),h.rotation);
            }
            if(p.item=="rifle_mosin"){
                Arm(a,false,p.root.TransformPoint(p.support)-a.GetBoneTransform(HumanBodyBones.LeftHand).rotation*palm,a.GetBoneTransform(HumanBodyBones.LeftHand).rotation);
                if(action=="bolt"||action.StartsWith("reload")){
                    var h=a.GetBoneTransform(HumanBodyBones.RightHand);Vector3 target=p.bolt!=null?p.bolt.TransformPoint(p.boltContact):p.root.TransformPoint(p.boltKnob);
                    if(action.StartsWith("reload")&&u>.24f&&u<.76f){float round=Mathf.Repeat((u-.24f)/.52f*5,1);target=Vector3.Lerp(actor.TransformPoint(new Vector3(.27f,1.15f,.08f)),p.root.TransformPoint(p.magWell)+Vector3.up*.04f,Mathf.Sin(round*Mathf.PI));}
                    var home=actor.TransformPoint(action=="bolt"?new Vector3(.18f,1.70f,.12f):new Vector3(.18f,1.39f,.15f));float blend=S(.07f,.18f,u)*(1-S(.83f,.96f,u));Arm(a,true,Vector3.Lerp(home,target-h.rotation*palm,blend),h.rotation);
                }
            }
        }
        public static void Arm(Animator a,bool right,Vector3 target,Quaternion rotation)
        {
            var upper=a.GetBoneTransform(right?HumanBodyBones.RightUpperArm:HumanBodyBones.LeftUpperArm);var lower=a.GetBoneTransform(right?HumanBodyBones.RightLowerArm:HumanBodyBones.LeftLowerArm);var hand=a.GetBoneTransform(right?HumanBodyBones.RightHand:HumanBodyBones.LeftHand);
            float x=Vector3.Distance(upper.position,lower.position),y=Vector3.Distance(lower.position,hand.position);var delta=target-upper.position;float d=Mathf.Clamp(delta.magnitude,.04f,x+y-.002f);var aim=delta.normalized;
            Vector3 hint=a.transform.TransformDirection(new Vector3(right?1:-1,-.65f,-.45f));Vector3 bend=Vector3.ProjectOnPlane(hint,aim).normalized;float along=(x*x-y*y+d*d)/(2*d);float height=Mathf.Sqrt(Mathf.Max(0,x*x-along*along));var elbow=upper.position+aim*along+bend*height;
            upper.rotation=Quaternion.FromToRotation(lower.position-upper.position,elbow-upper.position)*upper.rotation;lower.rotation=Quaternion.FromToRotation(hand.position-lower.position,target-lower.position)*lower.rotation;hand.rotation=rotation;
        }
    }
}
