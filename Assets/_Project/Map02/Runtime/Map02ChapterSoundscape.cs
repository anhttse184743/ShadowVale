using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace ShadowVale.Map01
{
    public sealed partial class Map02ChapterDirector
    {
        readonly Dictionary<string,AudioClip> villageSounds=new();
        readonly List<AudioSource> villageEffects=new();
        float villageCoughClock=50;int villageDelivered=-1;
        IEnumerator LoadChapterSoundscape()
        {
            foreach(var id in new[]{"cough","crate"})
            {
                var request=Resources.LoadAsync<AudioClip>("Map02DayOne/"+id);yield return request;
                if(request.asset is AudioClip clip){clip.LoadAudioData();villageSounds[id]=clip;}
            }
            villageDelivered=State.Count("D1_04");
        }
        void VillageOneShot(string id,Vector3 position,float gain=.2f)
        {
            if(!villageSounds.TryGetValue(id,out var clip))return;
            AudioSource source=villageEffects.Find(s=>s!=null&&!s.isPlaying);
            if(source==null){var obj=new GameObject("Village positional effect");obj.transform.SetParent(transform);source=obj.AddComponent<AudioSource>();source.playOnAwake=false;source.spatialBlend=1;source.minDistance=3;source.maxDistance=24;source.rolloffMode=AudioRolloffMode.Linear;source.priority=120;villageEffects.Add(source);}
            source.transform.position=position;source.clip=clip;source.volume=gain*(DialogueVoice.AnySpeech?.3f:1);source.Play();
        }
        void TickChapterSoundscape()
        {
            if(!ready)return;bool paused=Mission.Paused||ForestMenu.Visible||Time.timeScale==0;
            foreach(var effect in villageEffects)if(effect!=null){if(paused)effect.Pause();else effect.UnPause();}
            // Continuous beds, music and the single saved flyover belong to TickAmbience/TickSupperSound.
            // Keep only positional work/character effects here so no second water or music layer can stack.
            if(paused||State.phase>1)return;
            villageCoughClock-=Time.deltaTime;
            if(villageCoughClock<=0){villageCoughClock=State.phase==0?70:110;VillageOneShot("cough",roles["Ông Sáu"].transform.position,.16f);}
            int delivered=State.Count("D1_04");if(villageDelivered>=0&&delivered>villageDelivered)VillageOneShot("crate",sites["store"].position,.35f);villageDelivered=delivered;
        }
    }
}
