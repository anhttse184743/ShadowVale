using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace ShadowVale.Map01
{
    /// <summary>
    /// Day 3 / Night 3 talk: everyone in the conversation keeps a fixed mark for the whole scene (no drifting, no one on top
    /// of Nam), turns to whoever is speaking (the speaker to the one he answers), and every line gets its own shot: over the
    /// listener's shoulder onto the speaker, always from the same side of the line so the cuts read. Action scenes (the attack,
    /// the wound, the crawl, the radio call, the sacrifice, dawn) keep their own staging.
    /// </summary>
    public sealed partial class Map02ChapterDirector
    {
        const float TalkReach=6,TalkGap=1.15f,TalkSpacing=1.6f,TalkTurn=240;
        readonly List<Map02ChapterActor> talkers=new List<Map02ChapterActor>();
        Vector3 talkSide;bool talkStaged;
        public IReadOnlyList<Map02ChapterActor> Talkers=>talkers;
        static bool StagedTalk(Map02ChapterTask t)=>t.phase>=4&&t.id!="N3_00"&&t.id!="N3_WOUND"&&t.id!="N3_06"&&t.id!="N3_09"&&t.id!="DAWN"&&t.kind!="crawl";

        void StageTalk(Map02ChapterTask task)
        {
            talkers.Clear();talkStaged=StagedTalk(task);if(!talkStaged)return;
            var nam=Mission.Player.position;
            foreach(var role in lines.Where(l=>!l.radio&&l.speaker!="Nam").Select(l=>l.speaker).Distinct())
            {
                if(!roles.TryGetValue(role,out var actor)||actor==null||actor.visual==null||!actor.visual.gameObject.activeInHierarchy)continue;
                bool entering=entrances.Any(e=>e.actor==actor);
                if(!entering&&Flat(actor.transform.position,nam)>TalkReach)continue;   // a shout from off (the boat): no mark
                if(!entering)actor.StopAt(TalkMark(actor.transform.position,nam));
                talkers.Add(actor);
            }
            // The side of the line the camera keeps: the side the play camera was on, across Nam and his first partner.
            var first=talkers.Count>0?(entrances.FirstOrDefault(e=>e.actor==talkers[0])?.to??talkers[0].transform.position):nam+Mission.Player.forward;
            var axis=Vector3.ProjectOnPlane(first-nam,Vector3.up);if(axis.sqrMagnitude<.01f)axis=Mission.Player.forward;
            talkSide=Vector3.Cross(Vector3.up,axis.normalized);if(Vector3.Dot(cameraBefore-nam,talkSide)<0)talkSide=-talkSide;
        }
        /// <summary>Where someone stands to talk with Nam: where they are, unless that is on him or on someone else already marked.</summary>
        Vector3 TalkMark(Vector3 at,Vector3 nam)
        {
            bool Crowded(Vector3 p)=>Flat(p,nam)<TalkGap||talkers.Any(t=>Flat(t.transform.position,p)<TalkGap)||entrances.Any(e=>Flat(e.to,p)<TalkGap);   // nor where someone is walking in to
            if(!Crowded(at))return at;
            var outward=Vector3.ProjectOnPlane(at-nam,Vector3.up);if(outward.sqrMagnitude<.01f)outward=Mission.Player.forward;outward.Normalize();
            for(int i=0;i<12;i++)
            {
                int step=(i+1)/2*(i%2==1?1:-1);var p=nam+Quaternion.AngleAxis(step*30,Vector3.up)*outward*TalkSpacing;
                if(!NavMesh.SamplePosition(p,out var floor,.4f,NavMesh.AllAreas)||Crowded(floor.position))continue;
                return floor.position;
            }
            return at;
        }
        Transform Speaker(int index)
        {
            if(index<0||index>=lines.Length)return null;var line=lines[index];
            if(line.radio||line.speaker=="Nam")return Mission.Player;
            return roles.TryGetValue(line.speaker,out var a)&&a!=null&&a.visual!=null&&a.visual.gameObject.activeInHierarchy?a.transform:null;
        }
        /// <summary>Who the line is said to: the last other speaker, else the next, else Nam (or, for Nam, his first partner).</summary>
        Transform Addressee(Transform speaker)
        {
            for(int i=lineIndex-1;i>=0;i--){var s=Speaker(i);if(s!=null&&s!=speaker)return s;}
            for(int i=lineIndex+1;i<lines.Length;i++){var s=Speaker(i);if(s!=null&&s!=speaker)return s;}
            return speaker!=Mission.Player?Mission.Player:talkers.Count>0?talkers[0].transform:null;
        }
        /// <summary>Each frame of a staged talk: listeners turn to the speaker, the speaker to whom he answers.</summary>
        void FaceTalk()
        {
            if(!talkStaged||lines==null)return;
            var speaker=Speaker(lineIndex);if(speaker==null)return;var addressee=Addressee(speaker);
            void Turn(Transform who,Transform to,ref Quaternion rotation)
            {
                if(to==null||to==who)return;var look=Vector3.ProjectOnPlane(to.position-who.position,Vector3.up);if(look.sqrMagnitude<.01f)return;
                rotation=Quaternion.RotateTowards(rotation,Quaternion.LookRotation(look),TalkTurn*Time.deltaTime);
            }
            foreach(var t in talkers)
            {
                if(t==null||t.Travelling||entrances.Any(e=>e.actor==t&&Vector3.Distance(t.transform.position,e.to)>=1.5f))continue;
                var r=t.transform.rotation;Turn(t.transform,t.transform==speaker?addressee:speaker,ref r);t.transform.rotation=r;
            }
            Turn(Mission.Player,speaker==Mission.Player?addressee:speaker,ref lockedRotation);
        }
        /// <summary>The line's shot: over the listener's shoulder onto the speaker's face. A radio voice is heard on Nam.</summary>
        bool TalkCamera(out Vector3 eye,out Vector3 focus)
        {
            eye=focus=default;if(!talkStaged||lines==null)return false;
            // Someone still walking up: a wide two-shot from the open side, Nam and him coming in, before the over-the-shoulders.
            var coming=entrances.FirstOrDefault(e=>Flat(e.actor.transform.position,e.to)>1.2f&&(e.actor.Travelling||!spoken));
            if(coming!=null)
            {
                var nam=Mission.Player.position;var who=coming.actor.transform.position;var mid=Vector3.Lerp(nam,who,.5f);
                var axis=Vector3.ProjectOnPlane(who-nam,Vector3.up);axis=axis.sqrMagnitude>.01f?axis.normalized:Mission.Player.forward;
                eye=mid+talkSide*Mathf.Clamp(Flat(nam,who)*.7f,3,6)-axis*1.2f+Vector3.up*1.6f;focus=mid+Vector3.up*1.3f;return true;
            }
            var speaker=Speaker(lineIndex);if(speaker==null)return false;
            var listener=Addressee(speaker);
            if(lines[lineIndex].radio||listener==null)
            {
                var nam=Mission.Player;focus=nam.position+Vector3.up*Head(nam);
                var front=nam.forward;eye=focus+front*1.7f+talkSide*.6f+Vector3.up*.05f;return true;
            }
            var d=Vector3.ProjectOnPlane(speaker.position-listener.position,Vector3.up);if(d.sqrMagnitude<.01f)return false;d.Normalize();
            var side=Vector3.Cross(Vector3.up,d);if(Vector3.Dot(side,talkSide)<0)side=-side;
            eye=listener.position+Vector3.up*(Head(listener)+.08f)-d*.85f+side*.55f;
            focus=speaker.position+Vector3.up*(Head(speaker)-.05f);
            return true;
        }
        /// <summary>Eye height of a person (a child stands lower), from the drawn body.</summary>
        float Head(Transform who)
        {
            float top=float.NegativeInfinity;
            foreach(var r in who.GetComponentsInChildren<SkinnedMeshRenderer>())if(r.enabled&&r.gameObject.activeInHierarchy)top=Mathf.Max(top,r.bounds.max.y);
            return float.IsNegativeInfinity(top)?1.5f:Mathf.Clamp(top-who.position.y-.12f,.8f,1.75f);
        }
    }
}
