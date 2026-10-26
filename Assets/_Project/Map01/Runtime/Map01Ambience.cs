using System.Collections;
using UnityEngine;
namespace ShadowVale.Map01
{
    /// <summary>Quiet environmental audio only: never enters the guards' noise system.</summary>
    [DisallowMultipleComponent]
    public sealed class Map01Ambience : MonoBehaviour
    {
        public const float WindLevel = .20f, BirdLevel = .13f;
        Map01Mission mission;
        AudioSource wind, birds;
        readonly AudioClip[] calls = new AudioClip[3];
        DialogueVoice[] voices = new DialogueVoice[0];
        float nextBird = 10, scanIn, mix, birdGain = 1;
        int lastCall = -1;
        bool ready, paused, suspended, released;
        public bool Ready => ready;
        public float WindVolume => wind != null ? wind.volume : 0;
        public static Map01Ambience Attach(Map01Mission owner)
        {
            var sound = owner.GetComponent<Map01Ambience>();
            return sound != null ? sound : owner.gameObject.AddComponent<Map01Ambience>();
        }
        IEnumerator Start()
        {
            mission = GetComponent<Map01Mission>();
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
            bool stop=suspended || Time.timeScale<=0 || (mission!=null&&mission.Paused) || ForestMenu.Visible;
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
            float target=speaking?.28f:(mission!=null&&mission.Cinematic?.5f:1f);
            mix=Mathf.MoveTowards(mix,target,dt*(target<mix?2.5f:.35f));
            wind.volume=WindLevel*mix;
            birds.volume=BirdLevel*mix*birdGain;
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
