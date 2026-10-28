using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor.Media;
using UnityEngine;
namespace ShadowVale.Map01.Tests
{
    /// <summary>Opt-in evidence only. Renders the actual arrival camera after pose contacts; no scene or retail save edits.</summary>
    internal sealed class Map02ArrivalMisstepFilm:IDisposable
    {
        const int Width=1920,Height=1080,Fps=60;
        const string Path="Docs/Reviews/C1_Unity_20261026/C1_v4_Unity_1080p60.mp4";
        readonly RenderTexture target=new RenderTexture(Width,Height,24,RenderTextureFormat.ARGB32);
        readonly Texture2D image=new Texture2D(Width,Height,TextureFormat.RGBA32,false);
        readonly MediaEncoder encoder;
        readonly List<string> samples=new(){"time;hungHipX;hungHipY;hungHipZ;hungHeadX;hungHeadY;hungHeadZ;hungSpineX;hungSpineY;hungSpineZ;hungSpineW;namHipX;namHipY;namHipZ;namSpineX;namSpineY;namSpineZ;namSpineW"};
        int frames,poseFrames;bool disposed;
        public Map02ArrivalMisstepFilm()
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
            encoder=new MediaEncoder(Path,new VideoTrackAttributes{width=Width,height=Height,frameRate=new MediaRational(Fps),includeAlpha=false});
        }
        public bool Shoot(Map02Arrival arrival)
        {
            if(disposed||arrival==null||!arrival.IsPlaying||arrival.MisstepClock>11.5f)return false;
            int wanted=Mathf.CeilToInt(Mathf.Max(0,arrival.MisstepClock)*Fps);
            if(wanted<=frames)return true;
            var camera=UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).First(c=>c.enabled&&c.targetTexture==null&&c.gameObject.activeInHierarchy);
            var previous=camera.targetTexture;var active=RenderTexture.active;
            try
            {
                camera.targetTexture=target;camera.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,Width,Height),0,0);image.Apply();poseFrames++;
                var hung=arrival.MisstepActor(2);var nam=arrival.MisstepActor(0);
                var hip=hung.GetBoneTransform(HumanBodyBones.Hips).position;var head=hung.GetBoneTransform(HumanBodyBones.Head).position;var spine=hung.GetBoneTransform(HumanBodyBones.Spine).localRotation;
                var nh=nam.GetBoneTransform(HumanBodyBones.Hips).position;var ns=nam.GetBoneTransform(HumanBodyBones.Spine).localRotation;
                samples.Add(FormattableString.Invariant($"{arrival.MisstepClock:F5};{hip.x:F5};{hip.y:F5};{hip.z:F5};{head.x:F5};{head.y:F5};{head.z:F5};{spine.x:F6};{spine.y:F6};{spine.z:F6};{spine.w:F6};{nh.x:F5};{nh.y:F5};{nh.z:F5};{ns.x:F6};{ns.y:F6};{ns.z:F6};{ns.w:F6}"));
                // Keep the scene clock's real duration even if capturing the GPU frame costs more than 1/60 s. Repeats
                // are explicit: this is a 60-fps video container, not a claim that the game ran at 60 unique poses/s.
                while(frames<wanted){encoder.AddFrame(image);frames++;}
            }
            finally{camera.targetTexture=previous;RenderTexture.active=active;}
            return true;
        }
        public void Dispose()
        {
            if(disposed)return;disposed=true;encoder.Dispose();UnityEngine.Object.Destroy(target);UnityEngine.Object.Destroy(image);
            File.WriteAllText(Path+".info.txt",$"Unity camera capture after LateUpdate contacts. {Width}x{Height}; {frames} encoded frames / {Fps} fps; {poseFrames} distinct rendered samples. Natural scene time retained with repeated frames when recording is slow. No audio or GUI overlay. Not an FPS benchmark.");
            File.WriteAllLines(Path+".contacts.csv",samples);
        }
    }
}
