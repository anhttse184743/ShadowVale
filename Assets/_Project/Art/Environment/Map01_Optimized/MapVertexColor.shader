Shader "ShadowVale/Map Vertex Color"
{
    Properties { _BaseColor("Tint", Color) = (1,1,1,1) [PerRendererData] _InstanceTone("Instance tone", Float) = 1 }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Cull Off
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
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
            half4 _BaseColor;
            CBUFFER_END
            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float, _InstanceTone)
            UNITY_INSTANCING_BUFFER_END(Props)
            struct A { float4 p:POSITION; float3 n:NORMAL; half4 c:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 p:SV_POSITION; float3 world:TEXCOORD0; half3 n:TEXCOORD1; half4 c:COLOR; half fog:TEXCOORD2; UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO };
            V Vert(A a)
            {
                V o=(V)0; UNITY_SETUP_INSTANCE_ID(a); UNITY_TRANSFER_INSTANCE_ID(a,o); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                VertexPositionInputs v=GetVertexPositionInputs(a.p.xyz); o.p=v.positionCS; o.world=v.positionWS;
                o.n=TransformObjectToWorldNormal(a.n); o.c=a.c*_BaseColor*UNITY_ACCESS_INSTANCED_PROP(Props,_InstanceTone); o.fog=ComputeFogFactor(o.p.z); return o;
            }
            half Noise(float2 p)
            {
                float2 cell=floor(p),f=frac(p);f=f*f*(3-2*f);
                half a=frac(sin(dot(cell,float2(127.1,311.7)))*43758.5453);
                half b=frac(sin(dot(cell+float2(1,0),float2(127.1,311.7)))*43758.5453);
                half c=frac(sin(dot(cell+float2(0,1),float2(127.1,311.7)))*43758.5453);
                half d=frac(sin(dot(cell+1,float2(127.1,311.7)))*43758.5453);
                return lerp(lerp(a,b,f.x),lerp(c,d,f.x),f.y);
            }
            half4 Frag(V i, FRONT_FACE_TYPE front:FRONT_FACE_SEMANTIC):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                half3 n=normalize(i.n)*IS_FRONT_VFACE(front,1,-1);
                Light l=GetMainLight(TransformWorldToShadowCoord(i.world));
                half ndl=saturate(dot(n,l.direction));
                half3 light=SampleSH(n)+l.color*(.16h+.84h*ndl)*l.shadowAttenuation;
                half grain=.86h+.20h*Noise(i.world.xz*2.5+i.world.y*.7);
                return half4(MixFog(i.c.rgb*light*grain,i.fog),1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection;
            struct A { float4 p:POSITION; float3 n:NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            float4 ShadowVert(A a):SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(a);
                float3 p=TransformObjectToWorld(a.p.xyz);float3 n=TransformObjectToWorldNormal(a.n);
                float4 c=TransformWorldToHClip(ApplyShadowBias(p,n,_LightDirection));
                #if UNITY_REVERSED_Z
                c.z=min(c.z,UNITY_NEAR_CLIP_VALUE*c.w);
                #else
                c.z=max(c.z,UNITY_NEAR_CLIP_VALUE*c.w);
                #endif
                return c;
            }
            half4 ShadowFrag():SV_Target{return 0;}
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A { float4 p:POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            float4 DepthVert(A a):SV_POSITION { UNITY_SETUP_INSTANCE_ID(a);return TransformObjectToHClip(a.p.xyz); }
            half4 DepthFrag():SV_Target {return 0;}
            ENDHLSL
        }
    }
}
