Shader "ShadowVale/Tropical Sky"
{
 Properties { _Zenith("Zenith",Color)=(.14,.39,.67,1) _Horizon("Horizon",Color)=(.70,.82,.86,1) _CloudCover("Cloud cover",Range(0,1))=.48 }
 SubShader {
 Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
 Cull Off ZWrite Off
 Pass {
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "UnityCG.cginc"
 float4 _Zenith,_Horizon;float _CloudCover;
 struct v2f{float4 vertex:SV_POSITION;float3 dir:TEXCOORD0;};
 v2f vert(float4 vertex:POSITION){v2f o;o.vertex=UnityObjectToClipPos(vertex);o.dir=vertex.xyz;return o;}
 float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
 float noise(float2 p){float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);}
 float fbm(float2 p){return noise(p)*.55+noise(p*2.03)*.26+noise(p*4.07)*.13+noise(p*8.13)*.06;}
 half4 frag(v2f i):SV_Target{
 float3 d=normalize(i.dir);float h=saturate(d.y);
 float3 color=lerp(_Horizon.rgb,_Zenith.rgb,pow(h,.5));
 float2 uv=d.xz/(max(.15,d.y)+.28)*2.3+float2(_Time.y*.0018,0);
 float density=fbm(uv);float cover=smoothstep(1-_CloudCover-.08,1-_CloudCover+.15,density)*smoothstep(.005,.18,d.y);
 float3 cloud=lerp(float3(.66,.73,.78),float3(1,.99,.94),saturate((density-.36)*3));
 color=lerp(color,cloud,cover*.94);
 float sun=pow(saturate(dot(d,normalize(float3(-.4,.7,-.35)))),600);color+=float3(1,.87,.63)*sun*1.5;
 return half4(color,1);}
 ENDHLSL
 }
 }
}
