Shader "ShadowVale/Map 2 Physical River"
{
 Properties {
  _ShallowColor("Shallow jade",Color)=(.18,.35,.24,1)
  _DeepColor("Deep river",Color)=(.035,.15,.145,1)
  _WaveAmplitude("Wave amplitude",Float)=.055
  _FlowSpeed("Flow speed",Float)=.65
  _RiverTime("Simulation clock",Float)=0
 }
 SubShader {
  Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent"}
  Pass {
   Name "FlowingWater"
   Tags {"LightMode"="UniversalForward"}
   Blend SrcAlpha OneMinusSrcAlpha
   ZWrite Off
   Cull Off
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma multi_compile_fog
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
   #pragma multi_compile_fragment _ _SHADOWS_SOFT
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
   CBUFFER_START(UnityPerMaterial)
    half4 _ShallowColor, _DeepColor;
    float _WaveAmplitude, _FlowSpeed, _RiverTime;
   CBUFFER_END
   float4 _Ripples[16];
   struct A {float4 p:POSITION;float2 uv:TEXCOORD0;};
   struct V {float4 p:SV_POSITION;float3 world:TEXCOORD0;float2 uv:TEXCOORD1;half fog:TEXCOORD2;};
   float Hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
   float Noise(float2 p){float2 c=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(Hash(c),Hash(c+float2(1,0)),f.x),lerp(Hash(c+float2(0,1)),Hash(c+1),f.x),f.y);}
   V Vert(A a){V o;float3 w=TransformObjectToWorld(a.p.xyz);float t=_RiverTime;
    w.y+=_WaveAmplitude*(sin(w.x*.72+w.z*.31-t*1.35)+.45*sin(w.x*-.38+w.z*1.12-t*1.85));
    o.world=w;o.p=TransformWorldToHClip(w);o.uv=a.uv;o.fog=ComputeFogFactor(o.p.z);return o;
   }
   half4 Frag(V i):SV_Target {
    float t=_RiverTime;float2 p=i.world.xz;
    float a=p.x*.72+p.y*.31-t*1.35,b=p.x*-.38+p.y*1.12-t*1.85;
    float2 slope=_WaveAmplitude*float2(.72*cos(a)-.171*cos(b),.31*cos(a)+.504*cos(b));
    float fine=sin(p.x*5.1+p.y*2.7+t*2.1)*.014;
    slope+=float2(fine,sin(p.y*7.7-p.x*1.9+t*2.7)*.014);
    float ringFoam=0;
    [unroll] for(int k=0;k<16;k++){
     float age=t-_Ripples[k].z;
     if(age>0&&age<3.5){float2 delta=p-_Ripples[k].xy;float radius=length(delta);float band=radius-age*1.6;float fade=exp(-abs(band)*3.8)*saturate(1-age/3.5)*_Ripples[k].w;
      slope+=delta/max(radius,.05)*cos(band*12)*fade*.18;ringFoam+=fade*.12;}
    }
    half3 normal=normalize(half3(-slope.x,1,-slope.y));half3 view=GetWorldSpaceNormalizeViewDir(i.world);
    float2 screenUV=GetNormalizedScreenSpaceUV(i.p);
    float depth=SampleSceneDepth(screenUV);
    #if !UNITY_REVERSED_Z
      depth=lerp(UNITY_NEAR_CLIP_VALUE,1,depth);
    #endif
    float3 opaqueWorld=ComputeWorldSpacePosition(screenUV,depth,UNITY_MATRIX_I_VP);
    float thickness=max(0,distance(opaqueWorld,i.world));
    half shore=saturate(1-thickness/.65);
    float2 bend=normal.xz*.012*saturate(thickness*2);
    half3 refracted=SampleSceneColor(saturate(screenUV+bend));
    half3 tint=lerp(_ShallowColor.rgb,_DeepColor.rgb,saturate(thickness/2.8));
    half3 water=lerp(refracted*tint*1.5,tint,.68);
    half fresnel=.035+.65*pow(1-saturate(dot(normal,view)),5);
    half3 reflection=GlossyEnvironmentReflection(reflect(-view,normal),.13,1);
    water=lerp(water,reflection,fresnel);
    Light sun=GetMainLight(TransformWorldToShadowCoord(i.world));half sparkle=pow(saturate(dot(normal,normalize(sun.direction+view))),180)*sun.shadowAttenuation;
    water+=sun.color*sparkle*.75;
    float streak=Noise(float2(i.uv.x*43,i.uv.y*.8+t*_FlowSpeed))*Noise(float2(i.uv.x*19,i.uv.y*1.7+t*_FlowSpeed*1.4));
    float bank=pow(abs(i.uv.x*2-1),18);
    half foam=saturate((bank*.40+shore*.3)*smoothstep(.24,.67,streak)+ringFoam);
    water=lerp(water,half3(.69,.77,.64),foam);water+=streak*.025;
    water*=.78+.22*sun.shadowAttenuation;
    return half4(MixFog(water,i.fog),lerp(.68,.96,saturate(thickness*1.5)));
   }
   ENDHLSL
  }
 }
}
