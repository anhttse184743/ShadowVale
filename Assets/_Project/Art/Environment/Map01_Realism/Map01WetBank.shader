Shader "ShadowVale/Map 1 Wet Bank"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent-10" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Offset -1,-1
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            struct A { float4 p:POSITION; float2 uv:TEXCOORD0; };
            struct V { float4 p:SV_POSITION; float3 world:TEXCOORD0; float wet:TEXCOORD1; half fog:TEXCOORD2; };
            V Vert(A a) { V o; VertexPositionInputs p=GetVertexPositionInputs(a.p.xyz); o.p=p.positionCS; o.world=p.positionWS; o.wet=a.uv.x; o.fog=ComputeFogFactor(p.positionCS.z); return o; }
            half4 Frag(V i):SV_Target
            {
                float grain=.5+.5*sin(i.world.x*3.7+sin(i.world.z*2.3))*sin(i.world.z*4.1);
                Light sun=GetMainLight(TransformWorldToShadowCoord(i.world));
                half3 mud=lerp(half3(.085,.075,.042),half3(.16,.14,.085),grain);
                mud*=max(SampleSH(float3(0,1,0)),half3(.15,.15,.15))*.7+sun.color*.4*lerp(.5,1,sun.shadowAttenuation);
                float fade=1-smoothstep(.05,1,i.wet);
                return half4(MixFog(mud,i.fog),fade*.48);
            }
            ENDHLSL
        }
    }
}
