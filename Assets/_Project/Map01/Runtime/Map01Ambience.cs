using System.Collections;
using UnityEngine;
namespace ShadowVale.Map01
{
    /// <summary>Quiet environmental audio only: never enters the guards' noise system.</summary>
    [DisallowMultipleComponent]
    public sealed class Map01Ambience : MonoBehaviour
    {
        public const float WindLevel = .15f, BirdLevel = .095f;
        Map01Mission mission;
        AudioSource wind, birds;
        BoxCollider shelterFloor, shelterRoof;
        readonly AudioClip[] calls = new AudioClip[3];
        DialogueVoice[] voices = new DialogueVoice[0];
        float nextBird = 10, scanIn, mix, birdGain = 1;
        int lastCall = -1;
        bool ready, paused, suspended, released;
        public bool Ready => ready;
        public bool InsideShelter { get; private set; }
        public float WindVolume => wind != null ? wind.volume : 0;
        public static Map01Ambience Attach(Map01Mission owner)
        {
            var sound = owner.GetComponent<Map01Ambience>();
            return sound != null ? sound : owner.gameObject.AddComponent<Map01Ambience>();
        }
        IEnumerator Start()
        {
            mission = GetComponent<Map01Mission>();
            // Use the authored room, not a radius around the commander: the surface above
            // this underground shelter is outdoors and must still have wind and birds.
            var shell=GameObject.Find("ShelterShell");
            if(shell!=null)foreach(var part in shell.GetComponentsInChildren<BoxCollider>())
            {
                if(part.name=="Floor")shelterFloor=part;
                else if(part.name=="Earth covered roof")shelterRoof=part;
            }
            wind = Layer("Map 1 — soft leaves", true);
            birds = Layer("Map 1 — distant birds", false);
            var bed = Resources.LoadAsync<AudioClip>("Map01Ambience/wind_leaves");
            var requests = new ResourceRequest[3];
            for (int i=0;i<3;i++) requests[i]=Resources.LoadAsync<AudioClip>("Map01Ambience/birds_"+(i+1));
            yield return bed;
            wind.clip = bed.asset as AudioClip;
            for (int i=0;i<3;i++) { yield return requests[i]; calls[i]=requests[i].asset as AudioClip; }
            if (released) yield break;
            if (wind.clip == null || System.Array.Exists(calls, c=>c==null))
            { Debug.LogWarning("Map 1 ambience is missing an audio clip.",this); yield break; }
            wind.time=Random.Range(0,wind.clip.length*.8f);
            wind.Play(); ready=true;
        }
        AudioSource Layer(string label, bool loop)
        {
            var host=new GameObject(label); host.transform.SetParent(transform,false);
            var source=host.AddComponent<AudioSource>(); source.playOnAwake=false;
            source.loop=loop; source.volume=0; source.spatialBlend=0; source.dopplerLevel=0;
            source.priority=220; return source;
        }
        void Update()
        {
            if (!ready || released) return;
            InsideShelter=mission!=null&&mission.player!=null&&IsInRoom(mission.player.position);
            bool quietRoom=InsideShelter&&mix<=.0001f;
            bool stop=quietRoom || suspended || Time.timeScale<=0 || (mission!=null&&mission.Paused) || ForestMenu.Visible;
            if(stop!=paused)
            {
                if(stop){wind.Pause();birds.Pause();}
                else {wind.UnPause();birds.UnPause();}
                paused=stop;
            }
            if(stop)return;
            float dt=Time.unscaledDeltaTime;
            scanIn-=dt;
            if(scanIn<=0){voices=FindObjectsByType<DialogueVoice>(FindObjectsSortMode.None);scanIn=.3f;}
            bool speaking=System.Array.Exists(voices,v=>v!=null&&v.HasSpeech);
            // Keep the bed present under cinematics and lower still under speech.
            float target=InsideShelter?0:(speaking?.28f:(mission!=null&&mission.Cinematic?.5f:1f));
            mix=Mathf.MoveTowards(mix,target,dt*(target<mix?2.5f:.35f));
            wind.volume=WindLevel*mix;
            birds.volume=BirdLevel*mix*birdGain;
            if(InsideShelter)
            {
                // No ongoing call or new bird countdown inside. Wind fades out, then
                // the common pause gate suspends its playback until Nam leaves again.
                birds.Stop();
                return;
            }
            nextBird-=dt;
            if(nextBird<=0&&!birds.isPlaying)
            {
                // Wait for speech to end rather than starting a call over an important line.
                if(speaking || (mission!=null&&mission.Cinematic)){nextBird=3;return;}
                int index=Random.Range(0,2);if(index>=lastCall)index++;
                lastCall=index;birds.clip=calls[index];birdGain=Random.Range(.7f,1f);
                birds.panStereo=Random.Range(-.55f,.55f);birds.volume=BirdLevel*mix*birdGain;birds.Play();
                nextBird=Random.Range(18f,34f);
            }
        }
        bool IsInRoom(Vector3 at)
        {
            if(shelterFloor==null||shelterRoof==null)return false;
            var local=shelterFloor.transform.InverseTransformPoint(at)-shelterFloor.center;
            var half=shelterFloor.size*.5f;
            return Mathf.Abs(local.x)<=half.x && Mathf.Abs(local.z)<=half.z
                && at.y>=shelterFloor.bounds.min.y-.5f && at.y<shelterRoof.bounds.min.y;
        }
        public void StopForTransition()
        {
            released=true;ready=false;
            if(wind!=null)wind.Stop();if(birds!=null)birds.Stop();
        }
        void OnApplicationPause(bool value){suspended=value;}
        void OnDisable(){if(wind!=null)wind.Pause();if(birds!=null)birds.Pause();paused=true;}
        void OnDestroy(){StopForTransition();}
    }
}
