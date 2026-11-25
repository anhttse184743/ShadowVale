// The red outline round a marked man in the Map 2 cover fire (N3_08), in two steps that do not depend on which way the
// model's faces wind (some character rigs are mirrored):
//   EnemyOutlineMask (queue Geometry+11) marks the man's own visible pixels in the stencil, drawing no colour;
//   this shader (queue Geometry+12) draws the body pushed out along its normals, only where the stencil is not marked,
//   so just a thin rim outside the silhouette shows. The push grows with distance to keep the rim readable far off.
// Both live in Resources so builds keep them.
Shader "ShadowVale/EnemyOutline"
{
    Properties
    {
        _Color ("Color", Color) = (1, 0.08, 0.05, 1)
        _Width ("Width per metre of distance", Float) = 0.0016
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry+12" }
        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Off
            ZWrite Off
            ZTest LEqual
            Stencil { Ref 64 ReadMask 64 Comp NotEqual }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Width;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings vert (Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = normalize(TransformObjectToWorldNormal(input.normalOS));
                float distanceToCamera = max(1.0, distance(GetCameraPositionWS(), positionWS));
                positionWS += normalWS * _Width * distanceToCamera;
                output.positionCS = TransformWorldToHClip(positionWS);
                return output;
            }

            half4 frag (Varyings input) : SV_Target { return _Color; }
            ENDHLSL
        }
    }
}
