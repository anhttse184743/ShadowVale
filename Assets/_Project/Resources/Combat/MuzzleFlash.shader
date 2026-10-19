Shader "ShadowVale/MuzzleFlash"
{
    Properties { _Intensity ("Intensity", Float) = 1 _Seed ("Variation", Float) = 0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Input { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varying { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; };
            CBUFFER_START(UnityPerMaterial)
                float _Intensity;
                float _Seed;
            CBUFFER_END
            Varying Vert(Input i) { Varying o; o.positionCS=TransformObjectToHClip(i.positionOS.xyz); o.uv=i.uv; return o; }
            half4 Frag(Varying i):SV_Target
            {
                float t=saturate(i.uv.y),x=abs(i.uv.x*2-1);
                float noise=.5+.25*sin(t*47+_Seed+x*10)+.25*sin(t*89+x*22+_Seed*3);
                float width=(1-t)*.3+sin(t*3.14159)*.55+(noise-.5)*.18;
                float core=1-smoothstep(width*.24,max(.015,width),x);
                float fade=saturate(t*28)*pow(1-t,1.35);
                half3 color=lerp(half3(1,.12,.005),half3(1.9,1.1,.38),core*core*(1-t*.65));
                return half4(color,core*fade*_Intensity*.6);
            }
            ENDHLSL
        }
    }
}
