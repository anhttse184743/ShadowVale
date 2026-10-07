using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ShadowVale.Map01 {
    /// <summary>One spoken character at a time. Radio colour is baked only into the two radio clips.</summary>
    public sealed class DialogueVoice : MonoBehaviour {
        [Serializable] public sealed class Line {
            public string id, caption, match, resource, language;
            public bool radio;
            public float duration;
            [NonSerialized] public AudioClip clip;
        }
        [Serializable] sealed class Catalog { public Line[] entries; }
        static Catalog catalog;
        static DialogueVoice active;
        readonly Queue<Line> pending=new();
        readonly HashSet<string> cinematicCues=new();
        AudioSource source;
        bool paused;
        float gap;
        public Func<bool> CanSpeak;
        public Action<Line> LineStarted;
        public string CurrentId {get;private set;}
        public string CurrentCaption {get;private set;}
        public bool HasSpeech=>pending.Count>0 || source!=null && (source.isPlaying || paused);
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset(){catalog=null;active=null;}
        public static Line Find(string id) {
            if(catalog==null) {
                var json=Resources.Load<TextAsset>("Dialogue/voice-catalog");
                catalog=json!=null?JsonUtility.FromJson<Catalog>(json.text):new Catalog{entries=Array.Empty<Line>()};
            }
            var line=catalog.entries.FirstOrDefault(e=>e.id==id);
            if(line!=null && line.clip==null)line.clip=Resources.Load<AudioClip>(line.resource);
            return line;
        }
        public static float Length(string id,float fallback=0)=>Find(id)?.clip?.length??fallback;
        public static string Caption(string id)=>Find(id)?.caption??"";
        public static DialogueVoice For(GameObject host) {
            var voice=host.GetComponent<DialogueVoice>();return voice!=null?voice:host.AddComponent<DialogueVoice>();
        }
        void Awake() {
            source=gameObject.AddComponent<AudioSource>();source.playOnAwake=false;
            source.spatialBlend=0;source.volume=.88f;source.priority=16;source.loop=false;
        }
        public float SpeakText(string text) {
            Find("");if(string.IsNullOrEmpty(text))return 0;
            var matches=catalog.entries.Where(e=>!e.id.StartsWith("m1_open_") && !string.IsNullOrEmpty(e.match) && text.Contains(e.match))
                .OrderBy(e=>text.IndexOf(e.match,StringComparison.Ordinal)).Select(e=>Find(e.id)).ToArray();
            if(matches.Length==0)return 0;Begin(matches);
            return matches.Sum(e=>e.clip!=null?e.clip.length+.18f:0);
        }
        public void Play(string id){var line=Find(id);if(line?.clip!=null)Begin(new[]{line});}
        public void PlayOnce(string id) {
            var line=Find(id);
            if(line?.clip==null || !cinematicCues.Add(id))return;
            Begin(new[]{line});
        }
        void Begin(IEnumerable<Line> lines) {
            StopAll();active=this;
            foreach(var line in lines)if(line.clip!=null)pending.Enqueue(line);
            gap=0;Update();
        }
        public static void StopAll(){if(active!=null)active.Stop();}
        public void Stop(){pending.Clear();if(source!=null)source.Stop();paused=false;CurrentId=CurrentCaption=null;gap=0;if(active==this)active=null;}
        void Update() {
            if(active!=this)return;
            bool allowed=Time.timeScale>0 && (CanSpeak==null || CanSpeak());
            if(!allowed){if(source.isPlaying){source.Pause();paused=true;}return;}
            if(paused){source.UnPause();paused=false;}
            if(source.isPlaying)return;
            gap-=Time.unscaledDeltaTime;if(gap>0)return;
            if(pending.Count==0){CurrentId=CurrentCaption=null;active=null;return;}
            var line=pending.Dequeue();source.clip=line.clip;CurrentId=line.id;CurrentCaption=line.caption;
            source.Play();LineStarted?.Invoke(line);
            // Gap counts only after playback. AudioSource is the authoritative completion clock.
            gap=.18f;
        }
        void OnDisable(){Stop();}
        void OnDestroy(){if(source!=null)Destroy(source);}
    }
}
