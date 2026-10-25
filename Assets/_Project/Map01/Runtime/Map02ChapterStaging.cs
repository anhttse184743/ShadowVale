using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace ShadowVale.Map01
{
    /// <summary>Lines heard while Nam keeps control, people walking into a scene, and lines that wait for them.</summary>
    public sealed partial class Map02ChapterDirector
    {
        // Radio calls and shouts during play. Never saved, never block input; a cutscene drops them.
        readonly Queue<Map02DialogueCatalog.Entry> barks=new Queue<Map02DialogueCatalog.Entry>();
        Map02DialogueCatalog.Entry bark;float barkClock,crawlCall;
        public Map02DialogueCatalog.Entry CurrentBark=>bark;
        public bool HasBark(string lineId)=>bark?.id==lineId||barks.Any(b=>b.id==lineId);
        public void Bark(string lineId)
        {
            var line=Map02ChapterDefinition.Load().entries.FirstOrDefault(e=>e.id==lineId);
            if(line==null||HasBark(lineId))return;
            barks.Enqueue(line);
        }
        void DropBarks(){barks.Clear();if(bark!=null)voice?.Stop();bark=null;}
        void TickBark(float dt)
        {
            if(bark==null)
            {
                if(barks.Count==0)return;
                bark=barks.Dequeue();barkClock=0;
                if(Map02DialogueCatalog.HasApprovedRecording(bark))voice.Play(bark.RecordingId);
            }
            barkClock+=dt;
            if(barkClock>=LineSeconds(bark)&&!voice.HasSpeech)bark=null;
        }
        static float LineSeconds(Map02DialogueCatalog.Entry line)=>Map02DialogueCatalog.HasApprovedRecording(line)?DialogueVoice.Length(line.RecordingId):Mathf.Max(2.5f,line.text.Length/12f);
        void TickCrawlCall(float dt)
        {
            // Hùng keeps calling while Nam crawls to cover; the same recording, not a new line.
            if(State.Done("N3_CRAWL")||!State.Done("N3_WOUND"))return;
            crawlCall-=dt;if(crawlCall>0)return;
            crawlCall=4;Bark("m2v3_wound_05");
        }
        void DrawBark(GUIStyle label)
        {
            if(bark==null||sceneTask!=null)return;
            var tint=GUI.color;GUI.color=new Color(0,0,0,.6f);GUI.DrawTexture(new Rect(300,690,1000,66),Texture2D.whiteTexture);GUI.color=tint;
            GUI.Label(new Rect(320,697,960,56),bark.Caption,new GUIStyle(label){fontSize=21});
        }

        // Someone who must be in frame walks in. Far away, they start just behind the camera;
        // the line they speak waits for them, at most seven seconds, then they are placed.
        sealed class Entrance {public Map02ChapterActor actor;public Vector3 to;public string startLine,gateLine;}
        readonly List<Entrance> entrances=new List<Entrance>();
        Func<bool> lineGate;float gateUntil;bool spoken;
        public bool WaitingForLine=>sceneTask!=null&&!spoken;
        public string CurrentLineId=>sceneTask!=null&&lines!=null?lines[lineIndex].id:null;
        void PlanEntrances(Map02ChapterTask task)
        {
            ClearEntrances();
            var nam=Mission.Player;
            if(task.id=="D3_02")Enter("Hùng",sites["ditch"].position+Vector3.right*1.4f,"m2v3_demolition_01","m2v3_demolition_02");
            if(task.id=="D3_04"){Enter("Hùng",nam.position+nam.right*1.5f,"m2v3_families_01","m2v3_families_01");Enter("Ông Sáu",nam.position+nam.forward*2.2f,"m2v3_families_01","m2v3_families_02");}
            if(task.id=="D3_05"){Enter("Hùng",HungAtSupper,"m2v3_supper_01","m2v3_supper_01");Enter("Anh Thành",SupperYard,"m2v3_supper_05","m2v3_lastorder_01");}
        }
        void Enter(string role,Vector3 to,string startLine,string gateLine)
        {
            if(!lines.Any(l=>l.id==startLine)||!lines.Any(l=>l.id==gateLine))return;
            if(NavMesh.SamplePosition(to,out var hit,3,NavMesh.AllAreas))to=hit.position;
            var actor=roles[role];float distance=Vector3.Distance(actor.transform.position,to);
            if(distance<2)return;
            if(distance>8||WalkClearance(actor.transform.position,to)<1.3f)actor.StopAt(EntryPoint(to));
            entrances.Add(new Entrance{actor=actor,to=to,startLine=startLine,gateLine=gateLine});
        }
        Vector3 BehindCamera()
        {
            // The scene camera sits behind Nam, so a point further back is out of frame.
            var point=Mission.Player.position-Mission.Player.forward*7;
            return NavMesh.SamplePosition(point,out var hit,4,NavMesh.AllAreas)?hit.position:point;
        }
        /// <summary>Where a far-off entrant starts: out of shot (behind the camera if it can be), on a walk that goes round Nam
        /// rather than through him.</summary>
        Vector3 EntryPoint(Vector3 to)
        {
            var nam=Mission.Player.position;var look=Vector3.ProjectOnPlane(rig.transform.forward,Vector3.up).normalized;
            Vector3 best=BehindCamera();float bestScore=float.NegativeInfinity;
            for(int i=0;i<16;i++)
            {
                var dir=Quaternion.Euler(0,i*22.5f,0)*Vector3.forward;
                if(!NavMesh.SamplePosition(nam+dir*7,out var hit,3,NavMesh.AllAreas))continue;
                if(WalkClearance(hit.position,to)<1.3f)continue;
                float score=-Vector3.Dot(dir,look);if(score>bestScore){bestScore=score;best=hit.position;}
            }
            return best;
        }
        /// <summary>How close a walk from <paramref name="from"/> to <paramref name="to"/> passes Nam (0 if it cannot be walked).</summary>
        float WalkClearance(Vector3 from,Vector3 to)
        {
            var path=new NavMeshPath();if(!NavMesh.CalculatePath(from,to,NavMesh.AllAreas,path)||path.status!=NavMeshPathStatus.PathComplete)return 0;
            float clear=float.PositiveInfinity;var c=path.corners;var nam=Mission.Player.position;
            for(int k=1;k<c.Length;k++)clear=Mathf.Min(clear,FlatSegmentDistance(c[k-1],c[k],nam));
            return clear;
        }
        static float FlatSegmentDistance(Vector3 a,Vector3 b,Vector3 p)
        {
            var ab=new Vector2(b.x-a.x,b.z-a.z);var ap=new Vector2(p.x-a.x,p.z-a.z);
            float t=ab.sqrMagnitude>1e-6f?Mathf.Clamp01(Vector2.Dot(ap,ab)/ab.sqrMagnitude):0;return (ap-ab*t).magnitude;
        }
        void ClearEntrances(){foreach(var e in entrances)e.actor.SceneDriven=false;entrances.Clear();}
        /// <summary>Starts walks cued by the current line, then speaks it unless it must wait for someone or something.</summary>
        void StartLine()
        {
            var id=lines[lineIndex].id;spoken=false;lineClock=0;
            foreach(var e in entrances.Where(e=>e.startLine==id)){e.actor.SceneDriven=true;e.actor.GoTo(e.to,1.6f);}
            var waiting=entrances.Where(e=>e.gateLine==id).ToArray();
            if(waiting.Length>0){lineGate=()=>waiting.All(e=>Vector3.Distance(e.actor.transform.position,e.to)<1.5f);gateUntil=sceneClock+7;}
            else if(sceneTask.id=="N3_00"&&lineIndex==0){lineGate=()=>sceneClock>=1.2f;gateUntil=float.PositiveInfinity;} // One second of black before the flare.
            else if(sceneTask.id=="N3_07"&&lineIndex==0){lineGate=()=>sceneClock>=BoatLineAt;gateUntil=float.PositiveInfinity;} // The tank, its gun, then the boat it is aimed at.
            else if(sceneTask.id=="N3_WOUND"&&lineIndex==0){lineGate=()=>sceneClock>=BreachLineAt;gateUntil=float.PositiveInfinity;} // The tank drives in and stands before anyone shouts.
            else if(sceneTask.id=="N3_09"){float at=SacrificeLineAt(id);lineGate=()=>sceneClock>=at;gateUntil=float.PositiveInfinity;} // The echo after the blast, then the guns, then the counter-attack.
            else lineGate=null;
            TickGate();
        }
        void TickGate()
        {
            if(spoken)return;
            bool late=sceneClock>=gateUntil;
            if(lineGate!=null&&!lineGate()&&!late)return;
            if(late)foreach(var e in entrances.Where(e=>e.gateLine==lines[lineIndex].id))e.actor.StopAt(e.to);
            lineGate=null;spoken=true;lineClock=0;Speak();LineCue(lines[lineIndex].id);
        }
        Vector3 HungAtSupper=>sites["family"].position+Vector3.right*1.4f;
        /// <summary>Extra silence after a line, before the next one starts.</summary>
        float PauseAfter(int index)=>index+1<lines.Length&&lines[index+1].id=="m2v3_lastorder_07"?2f:0f; // "nhìn Nam một lúc lâu"
    }
}
