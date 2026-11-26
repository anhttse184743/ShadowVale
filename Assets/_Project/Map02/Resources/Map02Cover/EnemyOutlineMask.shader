// First step of the cover-fire outline (see EnemyOutline.shader): marks the marked man's visible pixels in the stencil
// so the red rim is drawn only outside his silhouette. Draws no colour and writes no depth.
Shader "ShadowVale/EnemyOutlineMask"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry+11" }
        Pass
        {
            Name "OutlineMask"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Off
            ZWrite Off
            ZTest LEqual
            Offset -1, -1
            ColorMask 0
            Stencil { Ref 64 WriteMask 64 Comp Always Pass Replace }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings vert (Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 frag (Varyings input) : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
