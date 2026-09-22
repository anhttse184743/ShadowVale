Shader "ShadowVale/Tropical River"
{
 Properties { _DeepColor("River color",Color)=(.065,.22,.20,1) _ShallowColor("Bank tint",Color)=(.25,.34,.23,1) }
 SubShader {
 Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry+10" }
 Pass {
 Tags { "LightMode"="UniversalForward" }
 Cull Off
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile_fog
 #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
 #pragma multi_compile_fragment _ _SHADOWS_SOFT
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
 CBUFFER_START(UnityPerMaterial)
 half4 _DeepColor,_ShallowColor;
 CBUFFER_END
 struct A{float4 p:POSITION;};struct V{float4 p:SV_POSITION;float3 world:TEXCOORD0;half fog:TEXCOORD1;};
 V vert(A a){V o;VertexPositionInputs v=GetVertexPositionInputs(a.p.xyz);o.p=v.positionCS;o.world=v.positionWS;o.fog=ComputeFogFactor(o.p.z);return o;}
  float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
 float noise(float2 p){float2 a=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(a),hash(a+float2(1,0)),f.x),lerp(hash(a+float2(0,1)),hash(a+1),f.x),f.y);}
 half4 frag(V i):SV_Target{
 float2 p=i.world.xz;float t=_Time.y;
 float warp=noise(p*.8+float2(0,t*.03))*5;
 float a=p.x*3.5+p.y*1.2+t*1.2+warp,b=p.y*6-p.x*1.8-t*1.6+warp,c=p.x*12+p.y*9+t*2;
 float3 n=normalize(float3(cos(a)*.027+cos(c)*.01,1,cos(b)*.022+cos(c)*.008));
 float3 view=GetWorldSpaceNormalizeViewDir(i.world);Light sun=GetMainLight(TransformWorldToShadowCoord(i.world));
 float fresnel=.035+.46*pow(1-saturate(dot(n,view)),4);
 float rx=8+12*sin(p.y*.041)+4*sin(p.y*.105);float bank=smoothstep(2.3,4.5,abs(p.x-rx));
 half3 base=lerp(_DeepColor.rgb,_ShallowColor.rgb,bank)*(.5+sun.shadowAttenuation*.5);
 float3 reflected=reflect(-view,n);float3 sky=lerp(float3(.63,.76,.80),float3(.17,.39,.60),saturate(reflected.y));
 float glint=pow(saturate(dot(n,normalize(sun.direction+view))),160)*sun.shadowAttenuation;
 half3 color=lerp(base,sky,fresnel)+sun.color*glint*.65;
 color+=bank*.028*(.5+.5*sin(a+b));return half4(MixFog(color,i.fog),1);}
 ENDHLSL
 }
 }
}

