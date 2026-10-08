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
            Vector3 center=default,tangent=default;bool found=false;
            for(int i=0;i<count-1;i++) {
                if(!InsideTriangle(point,leftBank[i],rightBank[i],leftBank[i+1])&&!InsideTriangle(point,rightBank[i],rightBank[i+1],leftBank[i+1]))continue;
                var a=(leftBank[i]+rightBank[i])*.5f;var b=(leftBank[i+1]+rightBank[i+1])*.5f;
                var direction=new Vector2(b.x-a.x,b.z-a.z);float t=Mathf.Clamp01(Vector2.Dot(new Vector2(point.x-a.x,point.z-a.z),direction)/Mathf.Max(.00001f,direction.sqrMagnitude));
                center=Vector3.Lerp(a,b,t);tangent=(b-a).normalized;found=true;break;
            }
            if(!found)return false;
            float a1=point.x*.72f+point.z*.31f-time*1.35f,a2=point.x*-.38f+point.z*1.12f-time*1.85f;
            sample.height=center.y+waveAmplitude*(Mathf.Sin(a1)+.45f*Mathf.Sin(a2));
            float dx=waveAmplitude*(.72f*Mathf.Cos(a1)-.171f*Mathf.Cos(a2)),dz=waveAmplitude*(.31f*Mathf.Cos(a1)+.504f*Mathf.Cos(a2));
            sample.normal=new Vector3(-dx,1,-dz).normalized;
            sample.velocity=-tangent*flowSpeed;sample.velocity.y=0;
            return true;
        }
        static bool InsideTriangle(Vector3 p,Vector3 a,Vector3 b,Vector3 c) {
            float ab=(b.x-a.x)*(p.z-a.z)-(b.z-a.z)*(p.x-a.x);
            float bc=(c.x-b.x)*(p.z-b.z)-(c.z-b.z)*(p.x-b.x);
            float ca=(a.x-c.x)*(p.z-c.z)-(a.z-c.z)*(p.x-c.x);
            return (ab>=-.0001f&&bc>=-.0001f&&ca>=-.0001f)||(ab<=.0001f&&bc<=.0001f&&ca<=.0001f);
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
