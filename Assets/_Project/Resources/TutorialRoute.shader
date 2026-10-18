Shader "ShadowVale/Tutorial Route"
{
    Properties { _Tint ("Light", Color) = (1, .9, .4, 1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            CBUFFER_START(UnityPerMaterial)
            half4 _Tint;
            CBUFFER_END
            Varyings Vert(Attributes input)
            {
                Varyings output; output.positionCS = TransformObjectToHClip(input.positionOS.xyz); output.uv = input.uv; return output;
            }
            float EdgeDistance(float2 p, float2 a, float2 b)
            {
                float2 segment = b - a;
                return length(p - a - segment * saturate(dot(p - a, segment) / dot(segment, segment)));
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float2 p = (input.uv - .5) * float2(1.9, 1.8);
                const float2 a = float2(-.64, -.38), b = float2(0, .46), c = float2(.64, -.38);
                const float2 d = float2(.44, -.38), e = float2(0, .20), f = float2(-.44, -.38);
                float distance = min(min(EdgeDistance(p, a, b), EdgeDistance(p, b, c)), min(EdgeDistance(p, c, d), EdgeDistance(p, d, e)));
                distance = min(distance, min(EdgeDistance(p, e, f), EdgeDistance(p, f, a)));
                float outerZ = .46 - abs(p.x) * (.84 / .64);
                float innerZ = max(-.38, .20 - abs(p.x) * (.58 / .44));
                bool inside = abs(p.x) <= .64 && p.y <= outerZ && p.y >= innerZ;
                float sdf = inside ? -distance : distance;
                float aa = max(fwidth(sdf), .0006);
                float body = 1 - smoothstep(-aa, aa, sdf);
                float band = saturate((outerZ - p.y) / .26);
                float core = smoothstep(.1, .23, band) * (1 - smoothstep(.72, .9, band));
                half3 face = _Tint.rgb * lerp(half3(.36, .3, .14), half3(1, 1.08, 1.38), core);
                // The bronze edge and narrow bright rim keep the shape legible over bright soil.
                float rim = 1 - smoothstep(.005, .013 + aa, abs(sdf));
                float halo = exp(-max(sdf, 0) * 23) * (1 - body) * .22;
                half4 result = half4(_Tint.rgb * halo * 1.3, halo * .6);
                float fillAlpha = body * .97;
                result = half4(face * fillAlpha, fillAlpha) + result * (1 - fillAlpha);
                float rimAlpha = rim * .8;
                result = half4((_Tint.rgb * .8 + .2) * rimAlpha, rimAlpha) + result * (1 - rimAlpha);
                return result * _Tint.a;
            }
            ENDHLSL
        }
    }
}
