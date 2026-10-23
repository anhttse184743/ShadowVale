using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace ShadowVale.EnvironmentReview
{
    /// <summary>Standalone replay of the authored Blender art. Does not equip or fire game weapons.</summary>
    public sealed class NativeWeaponPreviewPlayer:MonoBehaviour
    {
        [Serializable] public sealed class Take {public string item,action,label;public AnimationClip clip;public bool loop;}
        public Animator actor;public Camera reviewCamera;public Take[] takes=Array.Empty<Take>();
        public int selected;public float seconds,speed=1;public bool playing=true;
        PlayableGraph graph;AnimationClipPlayable clipPlayer;int loaded=-1;SkinnedMeshRenderer[] reviewSkins;
        ShadowVale.Map01.ForestMenu suppressedMenu;
        public Take Current=>takes.Length==0?null:takes[Mathf.Clamp(selected,0,takes.Length-1)];
        void Start(){Cursor.lockState=CursorLockMode.None;Cursor.visible=true;suppressedMenu=FindFirstObjectByType<ShadowVale.Map01.ForestMenu>();if(suppressedMenu!=null){suppressedMenu.CancelInvoke();suppressedMenu.enabled=false;}}
        void LateUpdate(){if(!Application.isPlaying||Current==null)return;if(playing){seconds+=Time.unscaledDeltaTime*speed;if(seconds>Current.clip.length)seconds=Current.loop?seconds%Current.clip.length:Current.clip.length;}Sample(selected,seconds);}
        public void Sample(int index,float at)
        {
            if(takes.Length==0||actor==null)return;selected=Mathf.Clamp(index,0,takes.Length-1);
            if(loaded!=selected||!graph.IsValid()){
                Release();graph=PlayableGraph.Create("Blender weapon art review");graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                clipPlayer=AnimationClipPlayable.Create(graph,Current.clip);clipPlayer.SetApplyFootIK(false);clipPlayer.SetSpeed(0);
                AnimationPlayableOutput.Create(graph,"Complete Blender hierarchy",actor).SetSourcePlayable(clipPlayer);graph.Play();loaded=selected;
            }
            seconds=Mathf.Clamp(at,0,Current.clip.length);clipPlayer.SetTime(seconds);graph.Evaluate(0);
            if(reviewSkins==null){reviewSkins=actor.GetComponentsInChildren<SkinnedMeshRenderer>();foreach(var skin in reviewSkins)skin.forceMatrixRecalculationPerRender=true;}
        }
        public void Select(string item){int n=Array.FindIndex(takes,t=>t.item==item);if(n>=0){seconds=0;Sample(n,0);}}
        public void View(int n)
        {
            Vector3 target=new Vector3(0,n==2?1.40f:1.03f,.18f);
            Vector3 eye=n==0?new Vector3(2.7f,2.0f,4.6f):n==1?new Vector3(3.2f,1.9f,1.0f):new Vector3(1.9f,1.95f,2.7f);
            reviewCamera.transform.SetPositionAndRotation(transform.TransformPoint(eye),Quaternion.LookRotation(transform.TransformPoint(target)-transform.TransformPoint(eye)));
            reviewCamera.fieldOfView=n==2?30:33;
        }
        public void Release(){if(graph.IsValid())graph.Destroy();loaded=-1;}
        void OnDisable()=>Release();
        void OnDestroy(){Release();if(suppressedMenu!=null)suppressedMenu.enabled=true;}
        void OnGUI()
        {
            if(Current==null)return;float scale=Mathf.Min(Screen.width/1600f,Screen.height/900f);var old=GUI.matrix;GUI.matrix=Matrix4x4.Scale(Vector3.one*scale);
            var title=new GUIStyle(GUI.skin.label){fontSize=25,fontStyle=FontStyle.Bold};var text=new GUIStyle(GUI.skin.label){fontSize=17,wordWrap=true};
            GUI.Box(new Rect(20,16,1560,96),"");GUI.Label(new Rect(40,25,1520,37),"SHADOWVALE / BẢN BLENDER TRONG UNITY",title);
            GUI.Label(new Rect(40,66,1520,34),Current.label+" • "+(selected+1)+"/"+takes.Length+" • Cảnh xem art độc lập",text);
            var ids=new[]{"ak47","knife","pistol_k54","rifle_mosin","grenade_f1","smoke_bomb"};var labels=new[]{"AK","Dao","K54","Mosin","Lựu đạn F1","Bom khói"};
            for(int i=0;i<ids.Length;i++)if(GUI.Button(new Rect(40+i*175,125,165,36),labels[i]))Select(ids[i]);
            GUI.Box(new Rect(20,726,1560,155),"");
            if(GUI.Button(new Rect(40,743,145,35),"◀ Động tác trước")){seconds=0;Sample((selected+takes.Length-1)%takes.Length,0);}
            if(GUI.Button(new Rect(195,743,120,35),playing?"Tạm dừng":"Phát"))playing=!playing;
            if(GUI.Button(new Rect(325,743,145,35),"Động tác sau ▶")){seconds=0;Sample((selected+1)%takes.Length,0);}
            if(GUI.Button(new Rect(490,743,135,35),"Toàn thân"))View(0);if(GUI.Button(new Rect(635,743,135,35),"Góc bên"))View(1);if(GUI.Button(new Rect(780,743,135,35),"Cận tay"))View(2);
            speed=GUI.HorizontalSlider(new Rect(970,758,175,24),speed,.15f,1.5f);GUI.Label(new Rect(1165,744,355,32),$"{speed:0.00}× | {seconds:0.00}/{Current.clip.length:0.00}s",text);
            float seek=GUI.HorizontalSlider(new Rect(40,798,1485,25),seconds,0,Current.clip.length);if(Mathf.Abs(seek-seconds)>.001f){playing=false;Sample(selected,seek);}
            GUI.Label(new Rect(40,830,1490,38),"BẢN DUYỆT ART • Chưa thay animation controller của game, chưa mở chiến đấu K54/Mosin hoặc đổi thông số vũ khí.",text);GUI.matrix=old;
        }
    }
}
