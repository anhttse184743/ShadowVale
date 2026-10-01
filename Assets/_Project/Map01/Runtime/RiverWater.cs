using System.Collections.Generic;
using UnityEngine;
namespace ShadowVale.Map01
{
    /// <summary>A bounded, flowing height-field river. CPU buoyancy and the shader share wave equations.</summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class RiverWater : MonoBehaviour
    {
        public Vector3[] leftBank = new Vector3[0];
        public Vector3[] rightBank = new Vector3[0];
        [Min(0)] public float waveAmplitude = .055f;
        [Min(0)] public float flowSpeed = .65f;
        public float density = 1000f;
        public Renderer waterRenderer;
        public const int MaxRipples = 16;
        readonly Vector4[] ripples = new Vector4[MaxRipples];
        int nextRipple;
        MaterialPropertyBlock block;
        static readonly int RipplesId = Shader.PropertyToID("_Ripples");
        public float Clock => Application.isPlaying ? Time.time : Time.realtimeSinceStartup;
        public int RippleCount { get; private set; }
        public struct Sample { public float height; public Vector3 velocity; public Vector3 normal; }
        void OnEnable() { for(int i=0;i<MaxRipples;i++)ripples[i]=new Vector4(0,0,-10000,0);UpdateAppearance(); }
        void Update() { UpdateAppearance(); }
        public bool TrySample(Vector3 point, float time, out Sample sample)
        {
            sample=default;
            int count=Mathf.Min(leftBank.Length,rightBank.Length);
            if(count<2)return false;
            float best=float.PositiveInfinity;Vector3 center=default,tangent=default;float width=0;
            var p=new Vector2(point.x,point.z);
            for(int i=0;i<count-1;i++) {
                var a=(leftBank[i]+rightBank[i])*.5f;var b=(leftBank[i+1]+rightBank[i+1])*.5f;
                var av=new Vector2(a.x,a.z);var d=new Vector2(b.x-a.x,b.z-a.z);float length=d.sqrMagnitude;if(length<.00001f)continue;
                float raw=Vector2.Dot(p-av,d)/length;
                // Flat end caps: water does not extend beyond either end of the mesh.
                if(i==0&&raw<0||i==count-2&&raw>1)continue;
                float t=Mathf.Clamp01(raw);float distance=(p-av-d*t).sqrMagnitude;
                if(distance>=best)continue;best=distance;center=Vector3.Lerp(a,b,t);tangent=(b-a).normalized;
                width=Mathf.Lerp(Vector3.Distance(leftBank[i],rightBank[i]),Vector3.Distance(leftBank[i+1],rightBank[i+1]),t)*.5f;
            }
            if(best>width*width||float.IsInfinity(best))return false;
            float a1=point.x*.72f+point.z*.31f-time*1.35f,a2=point.x*-.38f+point.z*1.12f-time*1.85f;
            sample.height=center.y+waveAmplitude*(Mathf.Sin(a1)+.45f*Mathf.Sin(a2));
            float dx=waveAmplitude*(.72f*Mathf.Cos(a1)-.171f*Mathf.Cos(a2)),dz=waveAmplitude*(.31f*Mathf.Cos(a1)+.504f*Mathf.Cos(a2));
            sample.normal=new Vector3(-dx,1,-dz).normalized;
            sample.velocity=-tangent*flowSpeed;sample.velocity.y=0;
            return true;
        }
        public bool EmitRipple(Vector3 position,float strength=1) {
            if(!TrySample(position,Clock,out _))return false;
            ripples[nextRipple]=new Vector4(position.x,position.z,Clock,Mathf.Clamp01(strength));nextRipple=(nextRipple+1)%MaxRipples;RippleCount=Mathf.Min(MaxRipples,RippleCount+1);return true;
        }
        public void UpdateAppearance() {
            if(waterRenderer==null)return;if(block==null)block=new MaterialPropertyBlock();
            waterRenderer.GetPropertyBlock(block);block.SetFloat("_RiverTime",Clock);block.SetFloat("_WaveAmplitude",waveAmplitude);block.SetFloat("_FlowSpeed",flowSpeed);block.SetVectorArray(RipplesId,ripples);waterRenderer.SetPropertyBlock(block);
        }
    }
}
