using System;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Video;

namespace ShadowVale.Map01
{
    /// <summary>Bundled intro, shown only before a new tutorial; never on save/resume.</summary>
    [DisallowMultipleComponent]
    public sealed class NewGameIntro : MonoBehaviour
    {
        public static string FilePath => Path.Combine(Application.streamingAssetsPath,"Video/ShadowVale_Intro.mp4");
        public bool Active { get; private set; }
        public bool Prepared => player!=null&&player.isPrepared;
        public bool Paused { get; private set; }
        public double PlaybackTime => player!=null?player.time:0;
        VideoPlayer player;
        AudioSource sound;
        GameObject host;
        RenderTexture picture;
        Action completed;
        Action<string> failed;
        bool ending, suspended;
        float began, firstFrameAt=-1, finishAt, escapeHeld, lastProgressAt;
        long lastFrame=-1;
        GUIStyle label;

        public void Play(Action onCompleted, Action<string> onError)
        {
            if(Active)return;
            completed=onCompleted;failed=onError;Active=true;Paused=ending=suspended=false;
            began=lastProgressAt=Time.realtimeSinceStartup;firstFrameAt=-1;escapeHeld=0;lastFrame=-1;
            if(!File.Exists(FilePath)){Fail("Không tìm thấy video giới thiệu.");return;}
            host=new GameObject("New game intro playback");host.transform.SetParent(transform,false);
            bool hasListener=false;
            foreach(var listener in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))if(listener.isActiveAndEnabled)hasListener=true;
            if(!hasListener)host.AddComponent<AudioListener>();
            sound=host.AddComponent<AudioSource>();sound.playOnAwake=false;sound.spatialBlend=0;sound.volume=0;sound.priority=16;
            picture=new RenderTexture(1920,1080,0,RenderTextureFormat.ARGB32){name="Intro frame"};picture.Create();
            player=host.AddComponent<VideoPlayer>();player.playOnAwake=false;player.isLooping=false;
            player.source=VideoSource.Url;player.url=new Uri(FilePath).AbsoluteUri;
            player.renderMode=VideoRenderMode.RenderTexture;player.targetTexture=picture;
            player.aspectRatio=VideoAspectRatio.FitInside;player.waitForFirstFrame=true;player.skipOnDrop=true;
            player.timeUpdateMode=VideoTimeUpdateMode.UnscaledGameTime;
            player.audioOutputMode=VideoAudioOutputMode.AudioSource;player.controlledAudioTrackCount=1;
            player.prepareCompleted+=OnPrepared;player.loopPointReached+=OnEnded;player.errorReceived+=OnError;
            player.Prepare();
        }
        void OnPrepared(VideoPlayer video)
        {
            if(!Active||ending)return;
            if(video.audioTrackCount>0){video.EnableAudioTrack(0,true);video.SetTargetAudioSource(0,sound);}
            if(!Paused&&!suspended)video.Play();
        }
        void OnEnded(VideoPlayer video)=>Skip();
        void OnError(VideoPlayer video,string error)=>Fail("Không thể phát video giới thiệu: "+error);
        void Fail(string reason){var callback=failed;Cleanup();callback?.Invoke(reason);}
        public void Skip()
        {
            if(!Active||ending)return;
            ending=true;finishAt=Time.realtimeSinceStartup+.35f;
        }
        public void TogglePause()
        {
            if(!Active||ending||!Prepared)return;
            Paused=!Paused;
            if(Paused)player.Pause();else if(!suspended)player.Play();
            lastProgressAt=Time.realtimeSinceStartup;
        }
        void Update()
        {
            if(!Active)return;
            float now=Time.realtimeSinceStartup;
            if(ending)
            {
                if(sound!=null)sound.volume=.85f*Mathf.Clamp01((finishAt-now)/.35f);
                if(now>=finishAt){var callback=completed;Cleanup();callback?.Invoke();}
                return;
            }
            if(!Prepared&&now-began>20){Fail("Video chưa tải được. Hãy thử lại.");return;}
            if(player!=null&&player.frame>=0)
            {
                if(firstFrameAt<0)firstFrameAt=now;
                sound.volume=.85f*Mathf.Clamp01((now-firstFrameAt)/.35f);
                if(player.frame!=lastFrame){lastFrame=player.frame;lastProgressAt=now;}
            }
            if(Prepared&&!Paused&&!suspended&&now-lastProgressAt>20){Fail("Video đã ngừng phát. Hãy thử lại.");return;}
            var keys=Keyboard.current;
            if(keys==null||now-began<.75f)return;
            if(keys.spaceKey.wasPressedThisFrame)TogglePause();
            escapeHeld=keys.escapeKey.isPressed?escapeHeld+Time.unscaledDeltaTime:0;
            if(escapeHeld>=1)Skip();
        }
        void OnGUI()
        {
            if(!Active)return;
            int depth=GUI.depth;Color color=GUI.color;var matrix=GUI.matrix;GUI.matrix=Matrix4x4.identity;GUI.depth=-500;GUI.color=Color.white;
            GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),Texture2D.blackTexture);
            if(picture!=null&&firstFrameAt>=0)
            {
                float fade=ending?Mathf.Clamp01((finishAt-Time.realtimeSinceStartup)/.35f):Mathf.Clamp01((Time.realtimeSinceStartup-firstFrameAt)/.35f);
                GUI.color=new Color(1,1,1,fade);GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),picture,ScaleMode.ScaleToFit);GUI.color=Color.white;
            }
            if(label==null)label=new GUIStyle(GUI.skin.label){fontSize=20,alignment=TextAnchor.MiddleCenter,normal={textColor=Color.white}};
            if(firstFrameAt<0)GUI.Label(new Rect(0,Screen.height*.45f,Screen.width,50),"Đang mở video giới thiệu…",label);
            if(Paused)GUI.Label(new Rect(0,Screen.height*.45f,Screen.width,50),"Tạm dừng",label);
            if(!ending)GUI.Label(new Rect(0,Screen.height-48,Screen.width,36),"Space: Tạm dừng / tiếp tục     Giữ Esc 1 giây: Bỏ qua",label);
            GUI.matrix=matrix;GUI.color=color;GUI.depth=depth;
        }
        void OnApplicationPause(bool value)
        {
            suspended=value;lastProgressAt=Time.realtimeSinceStartup;
            if(!Active||!Prepared)return;
            if(value)player.Pause();else if(!Paused&&!ending)player.Play();
        }
        public void Cancel()=>Cleanup();
        void Cleanup()
        {
            Active=false;Paused=false;completed=null;failed=null;
            if(player!=null){player.prepareCompleted-=OnPrepared;player.loopPointReached-=OnEnded;player.errorReceived-=OnError;player.Stop();player.targetTexture=null;}
            if(sound!=null)sound.Stop();
            if(host!=null)Destroy(host);host=null;player=null;sound=null;
            if(picture!=null){picture.Release();Destroy(picture);picture=null;}
        }
        void OnDisable(){if(Active)Cleanup();}
        void OnDestroy()=>Cleanup();
    }
}
