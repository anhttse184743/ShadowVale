Shader "ShadowVale/Granite Rock"
{
 Properties { _BaseColor("Stone",Color)=(.39,.36,.3,1) _Moss("Moss coverage",Range(0,1))=.2 _Seed("Variation",Float)=0 }
 SubShader {
 Tags {"RenderType"="Opaque" "RenderPipeline"="UniversalPipeline"}
 Pass {
 Name "ForwardLit" Tags {"LightMode"="UniversalForward"}
 HLSLPROGRAM
 #pragma vertex Vert
 #pragma fragment Frag
 #pragma multi_compile_instancing
 #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
 #pragma multi_compile_fragment _ _SHADOWS_SOFT
 #pragma multi_compile_fog
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
 CBUFFER_START(UnityPerMaterial)
 float4 _BaseColor; float _Moss; float _Seed;
 CBUFFER_END
 struct A {float4 p:POSITION;float3 n:NORMAL;UNITY_VERTEX_INPUT_INSTANCE_ID};
 struct V {float4 p:SV_POSITION;float3 world:TEXCOORD0;float3 local:TEXCOORD1;float3 n:TEXCOORD2;float fog:TEXCOORD3;UNITY_VERTEX_INPUT_INSTANCE_ID};
 V Vert(A a){V o=(V)0;UNITY_SETUP_INSTANCE_ID(a);UNITY_TRANSFER_INSTANCE_ID(a,o);o.world=TransformObjectToWorld(a.p.xyz);o.local=a.p.xyz;o.p=TransformWorldToHClip(o.world);o.n=TransformObjectToWorldNormal(a.n);o.fog=ComputeFogFactor(o.p.z);return o;}
 float Hash(float3 p){p=frac(p*.1031);p+=dot(p,p.yzx+33.33);return frac((p.x+p.y)*p.z);}
 float Noise(float3 p){float3 a=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(lerp(Hash(a),Hash(a+float3(1,0,0)),f.x),lerp(Hash(a+float3(0,1,0)),Hash(a+float3(1,1,0)),f.x),f.y),lerp(lerp(Hash(a+float3(0,0,1)),Hash(a+float3(1,0,1)),f.x),lerp(Hash(a+float3(0,1,1)),Hash(a+1),f.x),f.y),f.z);}
 float Relief(float3 p){return Noise(p*4)*.55+Noise(p*13)*.3+Noise(p*35)*.15;}
 half4 Frag(V i):SV_Target {
 UNITY_SETUP_INSTANCE_ID(i);
 float3 p=i.local+_Seed*7.13;float3 n=normalize(i.n);
 float broad=Noise(p*1.3), grain=Noise(p*45),middle=Relief(p);
 float vein=1-smoothstep(.018,.06,abs(Noise(p*2.1+float3(0,Noise(p*4),0))-.5));
 float moss=_Moss*smoothstep(.42,.7,Noise(p*1.8))*smoothstep(-.2,.75,n.y);
 float damp=(1-smoothstep(0,.65,i.local.y))*.17;
 float3 col=_BaseColor.rgb*lerp(.6,1.35,broad)*lerp(.76,1.2,middle);
 col=lerp(col,float3(.62,.58,.48),smoothstep(.73,.9,grain)*.12);
 col*=1-vein*.18;col*=1-damp;
 col=lerp(col,float3(.14,.18,.08)*lerp(.7,1.4,grain),moss);
 // Object-space stone grain perturbs the surface normal without a painted texture seam.
 float h=Noise(p*13)*.008;
 float3 dpdx=ddx(i.world),dpdy=ddy(i.world);
 float3 r1=cross(dpdy,n),r2=cross(n,dpdx);float det=dot(dpdx,r1);
 n=normalize(abs(det)*n-sign(det)*(ddx(h)*r1+ddy(h)*r2));
 Light l=GetMainLight(TransformWorldToShadowCoord(i.world));
 float3 illumination=SampleSH(n)+l.color*saturate(dot(n,l.direction))*l.shadowAttenuation;
 return half4(MixFog(col*illumination,i.fog),1);
 }
 ENDHLSL
 }
 UsePass "Universal Render Pipeline/Lit/ShadowCaster"
 UsePass "Universal Render Pipeline/Lit/DepthOnly"
 }
}


