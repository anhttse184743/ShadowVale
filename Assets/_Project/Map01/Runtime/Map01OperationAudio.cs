using System.Collections.Generic;
using ShadowVale.Gameplay.Audio;
using UnityEngine;
namespace ShadowVale.Map01
{
    public static class Map01OperationAudio
    {
        static readonly Dictionary<string,AudioClip> Clips=new();
        public static void Play(string name,Vector3 at,float volume=.5f)
        {
            if(!Clips.TryGetValue(name,out var clip)||clip==null){clip=Resources.Load<AudioClip>("OperationsAudio/"+name);Clips[name]=clip;}
            if(clip!=null)PooledAudioService.Resolve().PlayOneShot(clip,at,volume,1,0,.8f);
        }
    }
}
