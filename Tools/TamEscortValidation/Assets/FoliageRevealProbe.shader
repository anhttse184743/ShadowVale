Shader "Hidden/ShadowVale/FoliageRevealProbe" {
 Properties { _BaseColor("Tint",Color)=(1,1,1,1) _SrcBlend("Src",Float)=1 _DstBlend("Dst",Float)=0 _ZWrite("Depth",Float)=1 _InstanceTone("Tone",Float)=1 }
 SubShader { Pass { Blend [_SrcBlend] [_DstBlend] ZWrite [_ZWrite]
 CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "UnityCG.cginc"
 float4 _BaseColor;
 float4 vert(float4 p:POSITION):SV_POSITION{return UnityObjectToClipPos(p);}
 float4 frag():SV_Target{return _BaseColor;}
 ENDCG
 } }
}