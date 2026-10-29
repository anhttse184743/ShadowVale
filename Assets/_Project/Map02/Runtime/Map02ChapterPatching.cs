using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
namespace ShadowVale.Map01
{
    /// <summary>
    /// Patching the boats' leaks with chú Bảy (D3_03). Leaks appear on the hull as circles, a ring closing on each; a click as
    /// the ring meets its circle seats the patch. The closer the timing, the more of the boat's 0–100 bar it fills, so careful
    /// work finishes the boat sooner. A click well before the ring arrives wastes that leak; a ring that runs past is missed.
    /// </summary>
    public sealed partial class Map02ChapterDirector
    {
        public const float PatchApproach=1.1f,PatchLate=.28f,PatchRadius=46,PatchGoal=100;
        public sealed class Leak{public Vector2 at;public float age;}
        sealed class PatchMark{public Vector2 at;public string text;public Color tint;public float age;}
        readonly List<Leak> leaks=new List<Leak>();readonly List<PatchMark> patchMarks=new List<PatchMark>();
        float patchSpawn,patchPause;int patchJudged,patchBoat;float patchEarned;Vector2 lastLeak;AudioSource patchAudio;
        static Texture2D patchRing,patchDisc,patchHull;static AudioClip patchKnock;
        public bool Patching=>doingWork&&IsPatch(workTask);
        public float PatchProgress {get;private set;}
        public IReadOnlyList<Leak> Leaks=>leaks;
        static bool IsPatch(Map02ChapterTask t)=>t!=null&&t.kind=="repair"&&t.scene=="boats";
        // The second boat is the first photograph mirrored, the water on the other side.
        bool Mirrored=>patchBoat%2==1;

        void BeginPatch()
        {
            leaks.Clear();patchMarks.Clear();PatchProgress=0;patchSpawn=.5f;patchPause=0;patchJudged=0;patchEarned=0;patchBoat=State.Count(workTask.id);lastLeak=new Vector2(800,600);Cursor.visible=true;Cursor.lockState=CursorLockMode.None;
            patchAudio??=gameObject.AddComponent<AudioSource>();patchAudio.spatialBlend=0;patchAudio.playOnAwake=false;
        }
        void EndPatch()
        {
            leaks.Clear();patchMarks.Clear();if(patchAudio!=null){Destroy(patchAudio);patchAudio=null;}
            Cursor.visible=false;Cursor.lockState=CursorLockMode.Locked;if(Mission.Bag!=null)Mission.Bag.SuppressFire=true;   // the last click must not fire the rifle
        }
        void TickPatch(float dt)
        {
            Cursor.visible=true;Cursor.lockState=CursorLockMode.None;   // a pause menu in between locks it again
            foreach(var mark in patchMarks)mark.age+=dt;patchMarks.RemoveAll(m=>m.age>.8f);
            if(patchPause>0)
            {
                patchPause-=dt;if(patchPause>0)return;
                if(State.Count(workTask.id)>=workTask.count){EndWork(true);return;}
                PatchProgress=0;patchSpawn=.5f;patchJudged=0;patchEarned=0;patchBoat=State.Count(workTask.id);return;
            }
            foreach(var leak in leaks)leak.age+=dt;
            foreach(var late in leaks.Where(l=>l.age>PatchApproach+PatchLate).ToArray()){leaks.Remove(late);Judge(late.at,"TRƯỢT",0,new Color(.9f,.35f,.3f));}
            patchSpawn-=dt;if(patchSpawn<=0&&leaks.Count<3){leaks.Add(new Leak{at=NextLeak()});patchSpawn=.7f;}
            var mouse=Mouse.current;
            if(mouse!=null&&mouse.leftButton.wasPressedThisFrame){var p=mouse.position.ReadValue();PatchAt(new Vector2(p.x/Screen.width*1600,(1-p.y/Screen.height)*900));}
        }
        /// <summary>A click at a point of the 1600×900 overlay. True when it landed on a leak.</summary>
        public bool PatchAt(Vector2 point)
        {
            if(!Patching||patchPause>0)return false;
            var leak=leaks.Where(l=>Vector2.Distance(l.at,point)<=PatchRadius*1.25f).OrderByDescending(l=>l.age).FirstOrDefault();if(leak==null)return false;
            leaks.Remove(leak);float error=Mathf.Abs(leak.age-PatchApproach);
            if(error<=.07f)Judge(leak.at,"CHUẨN",20,new Color(1,.86f,.35f));
            else if(error<=.16f)Judge(leak.at,"TỐT",13,new Color(.6f,.92f,.5f));
            else if(error<=.3f)Judge(leak.at,"TẠM",7,new Color(.85f,.85f,.8f));
            else Judge(leak.at,leak.age<PatchApproach?"SỚM QUÁ":"MUỘN",0,new Color(.9f,.35f,.3f));
            return true;
        }
        void Judge(Vector2 at,string text,float points,Color tint)
        {
            patchMarks.Add(new PatchMark{at=at,text=text,tint=tint});patchJudged++;patchEarned+=points;
            if(points>0&&patchAudio!=null){patchAudio.pitch=Random.Range(.9f,1.1f);patchAudio.PlayOneShot(Knock(),.35f+points/40);}
            PatchProgress=Mathf.Min(PatchGoal,PatchProgress+points);if(PatchProgress<PatchGoal)return;
            leaks.Clear();State.workCount=State.Increment(workTask.id);patchPause=1.4f;
            Mission.Say($"Thuyền {State.workCount}/{workTask.count} đã kín nước · độ chuẩn {Accuracy*100:0}%.");
        }
        /// <summary>How close the clicks were, over every leak judged on this boat (a miss counts as nothing).</summary>
        public float Accuracy=>patchJudged==0?0:patchEarned/(patchJudged*20f);
        Vector2 NextLeak()
        {
            // Along the planking: clear of the water, the gunwale and the bars at the top and bottom of the screen.
            for(int tries=0;;tries++)
            {
                float u=Random.Range(.12f,.95f),v=Random.Range(.44f,Mathf.Min(.82f,.62f+(u-.1f)*.45f));
                var at=HullPoint(new Vector2(u,v));
                if(tries>30||Vector2.Distance(at,lastLeak)>220&&leaks.All(l=>Vector2.Distance(l.at,at)>2.4f*PatchRadius)){lastLeak=at;return at;}
            }
        }
        // Photograph coordinates (0–1, from the top left) to the overlay, cropped to fill 16:9 as DrawPatch draws it.
        Vector2 HullPoint(Vector2 uv)
        {
            var crop=HullCrop();if(Mirrored)uv.x=1-uv.x;
            return new Vector2((uv.x-crop.x)/crop.width*1600,(uv.y-crop.y)/crop.height*900);
        }
        static Rect HullCrop()
        {
            float aspect=Hull().width/(float)Hull().height,screen=16/9f;
            return aspect<screen?new Rect(0,(1-aspect/screen)/2,1,aspect/screen):new Rect((1-screen/aspect)/2,0,screen/aspect,1);
        }

        void DrawPatch(GUIStyle label,GUIStyle title)
        {
            var crop=HullCrop();var uv=new Rect(crop.x,1-crop.yMax,crop.width,crop.height);if(Mirrored){uv.x+=uv.width;uv.width=-uv.width;}
            GUI.DrawTextureWithTexCoords(new Rect(0,0,1600,900),Hull(),uv);
            var tint=GUI.color;GUI.color=new Color(0,0,0,.55f);GUI.DrawTexture(new Rect(0,0,1600,112),Texture2D.whiteTexture);GUI.DrawTexture(new Rect(0,840,1600,60),Texture2D.whiteTexture);GUI.color=tint;
            int boat=patchBoat+1;
            GUI.Label(new Rect(40,22,560,40),$"Vá thuyền {boat}/{workTask.count}",title);
            GUI.Label(new Rect(40,64,560,34),patchJudged==0?"Cùng chú Bảy trám chỗ rò":$"Độ chuẩn {Accuracy*100:0}%",label);
            GUI.Box(new Rect(620,38,820,36),GUIContent.none);GUI.color=new Color(.86f,.62f,.3f);GUI.DrawTexture(new Rect(623,41,814*PatchProgress/PatchGoal,30),Texture2D.whiteTexture);GUI.color=tint;
            GUI.Label(new Rect(1452,36,140,40),$"{PatchProgress:0}/{PatchGoal:0}",title);
            foreach(var leak in leaks)
            {
                float t=leak.age/PatchApproach,ring=t<1?1+2*(1-t):1-.35f*(leak.age-PatchApproach)/PatchLate,fade=Mathf.Clamp01(leak.age/.2f);
                bool ripe=Mathf.Abs(leak.age-PatchApproach)<=.16f;
                GUI.color=new Color(.08f,.05f,.03f,.55f*fade);GUI.DrawTexture(Square(leak.at,PatchRadius),Disc());
                GUI.color=new Color(1,.86f,.45f,fade);GUI.DrawTexture(Square(leak.at,PatchRadius),Ring());
                GUI.color=ripe?new Color(.6f,1,.55f,fade):new Color(1,1,1,.85f*fade);GUI.DrawTexture(Square(leak.at,PatchRadius*ring),Ring());
            }
            var big=new GUIStyle(title){alignment=TextAnchor.MiddleCenter,fontSize=30};
            foreach(var mark in patchMarks){GUI.color=new Color(mark.tint.r,mark.tint.g,mark.tint.b,1-mark.age/.8f);GUI.Label(new Rect(mark.at.x-110,mark.at.y-85-mark.age*40,220,44),mark.text,big);}
            GUI.color=tint;
            if(patchPause>0)GUI.Label(new Rect(400,400,800,80),$"Thuyền {patchBoat+1} đã kín nước",new GUIStyle(big){fontSize=44});
            GUI.Label(new Rect(40,852,1520,40),"Bấm chuột trái vào chỗ rò khi vòng ngoài thu khít vòng trong · càng chuẩn càng nhanh xong · Esc dừng",new GUIStyle(label){fontSize=22,alignment=TextAnchor.MiddleCenter});
        }
        static Rect Square(Vector2 at,float radius)=>new Rect(at.x-radius,at.y-radius,radius*2,radius*2);
        static Texture2D Hull()=>patchHull!=null?patchHull:patchHull=Resources.Load<Texture2D>("Hud/BoatHullRepair")??Texture2D.grayTexture;
        static Texture2D Ring()=>patchRing!=null?patchRing:patchRing=Circle(.88f);
        static Texture2D Disc()=>patchDisc!=null?patchDisc:patchDisc=Circle(0);
        // A white circle, antialiased; `inner` is the hole as a fraction of the radius (0: a filled disc).
        static Texture2D Circle(float inner)
        {
            const int size=128;var texture=new Texture2D(size,size,TextureFormat.RGBA32,false){wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.HideAndDontSave};
            var pixels=new Color32[size*size];float edge=2f/size;
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float d=new Vector2((x+.5f)/size*2-1,(y+.5f)/size*2-1).magnitude;
                float a=Mathf.Clamp01((1-d)/edge)*(inner<=0?1:Mathf.Clamp01((d-inner)/edge));
                pixels[y*size+x]=new Color32(255,255,255,(byte)(a*255));
            }
            texture.SetPixels32(pixels);texture.Apply();return texture;
        }
        // A short knock of mallet on caulking, made here rather than shipped.
        static AudioClip Knock()
        {
            if(patchKnock!=null)return patchKnock;
            const int rate=44100;var samples=new float[rate*16/100];var noise=new System.Random(7);
            for(int i=0;i<samples.Length;i++)
            {
                float t=i/(float)rate;
                samples[i]=Mathf.Exp(-t*38)*(.6f*Mathf.Sin(2*Mathf.PI*190*t)+.3f*Mathf.Sin(2*Mathf.PI*410*t))+Mathf.Exp(-t*90)*.4f*((float)noise.NextDouble()*2-1);
            }
            patchKnock=AudioClip.Create("Patch knock",samples.Length,1,rate,false);patchKnock.SetData(samples,0);return patchKnock;
        }
    }
}
