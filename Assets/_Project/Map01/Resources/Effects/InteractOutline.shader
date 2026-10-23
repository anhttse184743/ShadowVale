// The faint outline InteractHighlight draws round what [E] would use: the mesh again, back faces only,
// pushed out along its normals by a fixed number of pixels, in a soft warm colour that breathes slowly.
Shader "ShadowVale/InteractOutline"
{
    Properties
    {
        _Color ("Color", Color) = (1, 0.92, 0.68, 0.75)
        _Width ("Width (pixels)", Float) = 2.2
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+20" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "Outline"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Front ZWrite Off ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha
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
                float4 clip = TransformObjectToHClip(input.positionOS.xyz);
                float3 normalCS = TransformWorldToHClipDir(TransformObjectToWorldNormal(input.normalOS));
                float2 push = normalize(normalCS.xy + 1e-5) * _Width * 2 / _ScreenParams.xy;
                clip.xy += push * clip.w;   // the same width in pixels near or far
                output.positionCS = clip;
                return output;
            }
            half4 frag (Varyings input) : SV_Target
            {
                half4 colour = _Color;
                colour.a *= 0.72 + 0.28 * sin(_Time.y * 3.2);
                return colour;
            }
            ENDHLSL
        }
    }
}
