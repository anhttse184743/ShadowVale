Shader "ShadowVale/Map 2 Joined Soil"
{
    Properties
    {
        _BaseColor("Tint", Color) = (1,1,1,1)
        _RoadTex("Fine gravel albedo", 2D) = "white" {}
        _SurfaceMask("Road and yard mask", 2D) = "black" {}
        _GravelScale("Gravel repeats per metre", Float) = .5
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Cull Back
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_RoadTex); SAMPLER(sampler_RoadTex);
            TEXTURE2D(_SurfaceMask); SAMPLER(sampler_SurfaceMask);
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _GravelScale;
            CBUFFER_END
            struct A { float4 p:POSITION; float3 n:NORMAL; half4 c:COLOR; };
            struct V { float4 p:SV_POSITION; float3 world:TEXCOORD0; half3 n:TEXCOORD1; half4 c:COLOR; half fog:TEXCOORD2; };
            V Vert(A a)
            {
                V o=(V)0; VertexPositionInputs v=GetVertexPositionInputs(a.p.xyz);
                o.p=v.positionCS;o.world=v.positionWS;o.n=TransformObjectToWorldNormal(a.n);o.c=a.c;o.fog=ComputeFogFactor(o.p.z);return o;
            }
            float Hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
            float Noise(float2 p)
            {
                float2 c=floor(p),f=frac(p);f=f*f*(3-2*f);
                return lerp(lerp(Hash(c),Hash(c+float2(1,0)),f.x),lerp(Hash(c+float2(0,1)),Hash(c+1),f.x),f.y);
            }
            half4 Frag(V i):SV_Target
            {
                float2 p=i.world.xz;
                float2 maskUV=(p+float2(110,100))/float2(220,200);
                half2 mask=SAMPLE_TEXTURE2D(_SurfaceMask,sampler_SurfaceMask,maskUV).rg;
                // A noisy feathered material transition replaces separate raised road strips.
                float irregular=(Noise(p*5.3)-.5)*.24;
                half road=smoothstep(.10,.84,mask.r+irregular);
                half yard=smoothstep(.1,.85,mask.g);
                half3 gravel=SAMPLE_TEXTURE2D(_RoadTex,sampler_RoadTex,p*_GravelScale).rgb;
                half3 other=SAMPLE_TEXTURE2D(_RoadTex,sampler_RoadTex,float2(-p.y,p.x)*_GravelScale*.79+float2(.37,.61)).rgb;
                gravel=lerp(gravel,other,Noise(p*.19)*.32);
                half fine=Noise(p*45);
                half3 ground=i.c.rgb*(.89+.17*Noise(p*6)+.06*fine);
                ground=lerp(ground,half3(.32,.255,.155)*(.9+.17*Noise(p*13)),yard);
                half3 albedo=lerp(ground,gravel,road)*_BaseColor.rgb;
                half3 n=normalize(i.n);
                // Small-scale soil relief, attenuated with distance to avoid sparkle.
                float detail=saturate(1-distance(_WorldSpaceCameraPos,i.world)/45);
                n=normalize(n+half3(Noise(p*39)-.5,0,Noise(p*39+17)-.5)*.13*road*detail);
                Light l=GetMainLight(TransformWorldToShadowCoord(i.world));
                half3 lighting=SampleSH(n)+l.color*(.12+.88*saturate(dot(n,l.direction)))*l.shadowAttenuation;
                return half4(MixFog(albedo*lighting,i.fog),1);
            }
            ENDHLSL
        }
        UsePass "ShadowVale/Map Vertex Color/ShadowCaster"
        UsePass "ShadowVale/Map Vertex Color/DepthOnly"
    }
}
