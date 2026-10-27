using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace ShadowVale.Map01
{
    public sealed partial class Map02ChapterDirector
    {
        readonly Dictionary<string,AudioClip> villageSounds=new();
        readonly List<AudioSource> villageSources=new();
        AudioSource villageDay,villageNight,villageOars,villageFire,villageTheme;
        readonly List<AudioSource> villageEffects=new();
        bool mealThemeActive,mealLaughPlayed;
        float villageRoosterClock=35,villageCoughClock=50;int villageDelivered=-1;
        IEnumerator LoadChapterSoundscape()
        {
            foreach(var id in new[]{"crickets","rooster","oars","fire","night_river","bamboo","duck","plane","cough","crate","children","hung_theme","hung_laugh"})
            {
                var request=Resources.LoadAsync<AudioClip>("Map02DayOne/"+(id=="crickets"?"village_crickets":id));yield return request;
                if(request.asset is AudioClip clip){clip.LoadAudioData();villageSounds[id]=clip;}
            }
            villageDay=VillageLoop("bamboo",sites["family"].position,false);
            villageNight=VillageLoop(villageSounds.ContainsKey("night_river")?"night_river":"crickets",sites["bamboo"].position,false);
            villageOars=VillageLoop("oars",sites["dock"].position,true);
            villageFire=VillageLoop("fire",DayFamily,true);
            villageTheme=VillageLoop("hung_theme",DayFamily,false);
            villageDelivered=State.Count("D1_04");
        }
        AudioSource VillageLoop(string id,Vector3 position,bool spatial)
        {
            if(!villageSounds.TryGetValue(id,out var clip))return null;
            var obj=new GameObject("Village audio — "+id);obj.transform.SetParent(transform);obj.transform.position=position;
            var source=obj.AddComponent<AudioSource>();source.clip=clip;source.playOnAwake=false;source.loop=true;source.volume=0;
            source.spatialBlend=spatial?1:0;source.minDistance=3;source.maxDistance=26;source.rolloffMode=AudioRolloffMode.Linear;
            source.priority=180;source.Play();villageSources.Add(source);return source;
        }
        void VillageGain(AudioSource source,float gain,bool paused)
        {
            if(source==null)return;
            if(paused){source.Pause();return;}source.UnPause();source.volume=Mathf.MoveTowards(source.volume,gain,Time.unscaledDeltaTime*.15f);
        }
        void VillageOneShot(string id,Vector3 position,float gain=.2f)
        {
            if(!villageSounds.TryGetValue(id,out var clip))return;
            AudioSource source=villageEffects.Find(s=>s!=null&&!s.isPlaying);
            if(source==null){var obj=new GameObject("Village positional effect");obj.transform.SetParent(transform);source=obj.AddComponent<AudioSource>();source.playOnAwake=false;source.spatialBlend=1;source.minDistance=3;source.maxDistance=40;source.rolloffMode=AudioRolloffMode.Linear;source.priority=120;villageEffects.Add(source);}
            source.transform.position=position;source.clip=clip;source.volume=gain;source.Play();
        }
        void TickChapterSoundscape()
        {
            if(!ready)return;bool paused=Mission.Paused||ForestMenu.Visible||Time.timeScale==0;
            foreach(var effect in villageEffects)if(effect!=null){if(paused)effect.Pause();else effect.UnPause();}
            bool day=State.phase==0,night=State.phase==1;float duck=voice!=null&&voice.HasSpeech?.45f:1;
            bool meal=sceneTask?.id=="N1_01";
            if(meal&&!mealThemeActive&&villageTheme!=null)villageTheme.time=0;
            mealThemeActive=meal;if(!meal)mealLaughPlayed=false;
            VillageGain(villageDay,day?.10f*duck:0,paused);VillageGain(villageNight,night&&!meal?.11f*duck:0,paused);
            VillageGain(villageOars,day?.15f*duck:0,paused);VillageGain(villageFire,night?.12f*duck:0,paused);
            VillageGain(villageTheme,meal&&!(lineIndex==lines.Length-1&&voice!=null&&!voice.HasSpeech)?.075f*duck:0,paused);
            if(paused||!day&&!night)return;
            if(meal&&lines[lineIndex].id=="m2v3_promise_08"&&!mealLaughPlayed&&voice!=null&&!voice.HasSpeech){mealLaughPlayed=true;VillageOneShot("hung_laugh",roles["Hùng"].transform.position,.13f);}
            float dt=Time.deltaTime;
            if(day){State.villageDaySeconds+=dt;if(!State.villagePlanePlayed&&State.villageDaySeconds>=480){State.villagePlanePlayed=true;VillageOneShot("plane",Mission.Player.position+Vector3.up*30,.035f);}}
            villageRoosterClock-=dt;villageCoughClock-=dt;
            if(day&&villageRoosterClock<=0){villageRoosterClock=95;VillageOneShot("rooster",sites["family"].position+Vector3.forward*14,.10f);VillageOneShot("children",sites["family"].position+Vector3.right*10,.08f);}
            if(villageCoughClock<=0){villageCoughClock=State.phase==0?70:110;VillageOneShot("cough",roles["Ông Sáu"].transform.position,.16f);}
            int delivered=State.Count("D1_04");if(villageDelivered>=0&&delivered>villageDelivered)VillageOneShot("crate",sites["store"].position,.35f);villageDelivered=delivered;
        }
    }
}
