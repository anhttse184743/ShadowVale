Shader "ShadowVale/UI/Tutorial Glass"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _Fill ("Fill", Color) = (.055,.065,.05,.38)
        _Frame ("Frame", Color) = (.7,.73,.66,.5)
        _Size ("Size", Vector) = (960,228,0,0)
        _Radius ("Corner radius", Float) = 18
        _Inset ("Glow margin", Float) = 0
        _Border ("Border width", Float) = 1.3
        _Glow ("Glow strength", Float) = 0
        _Glass ("Blurred scene", Float) = 1
        _Blur ("Blur pixels", Float) = 7
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "CanUseSpriteAtlas"="False" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend One OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            struct AppData { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct Varyings { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; float4 screen : TEXCOORD1; float4 local : TEXCOORD2; fixed4 color : COLOR; };
            sampler2D _CameraOpaqueTexture;
            float4 _CameraOpaqueTexture_TexelSize, _Size, _ClipRect;
            fixed4 _Fill, _Frame;
            float _Radius, _Inset, _Border, _Glow, _Glass, _Blur;
            Varyings Vert(AppData input)
            {
                Varyings output;
                output.local = input.vertex; output.vertex = UnityObjectToClipPos(input.vertex);
                output.screen = ComputeScreenPos(output.vertex); output.uv = input.uv; output.color = input.color; return output;
            }
            fixed4 Frag(Varyings input) : SV_Target
            {
                float2 p = (input.uv - .5) * _Size.xy;
                float2 q = abs(p) - (_Size.xy * .5 - _Inset - _Radius);
                float d = length(max(q, 0)) + min(max(q.x, q.y), 0) - _Radius;
                float inside = 1 - smoothstep(-.7, .7, d);
                float edge = 1 - smoothstep(_Border * .45, _Border * .45 + .9, abs(d));
                float halo = exp(-abs(d) * .32) * _Glow * .32;
                float2 uv = input.screen.xy / input.screen.w;
                float3 face = _Fill.rgb;
                if (_Glass > .5) {
                    float2 stepUV = _CameraOpaqueTexture_TexelSize.xy * _Blur;
                    float3 scene = tex2D(_CameraOpaqueTexture, uv).rgb * 4;
                    scene += tex2D(_CameraOpaqueTexture, uv + float2(stepUV.x, 0)).rgb * 2;
                    scene += tex2D(_CameraOpaqueTexture, uv - float2(stepUV.x, 0)).rgb * 2;
                    scene += tex2D(_CameraOpaqueTexture, uv + float2(0, stepUV.y)).rgb * 2;
                    scene += tex2D(_CameraOpaqueTexture, uv - float2(0, stepUV.y)).rgb * 2;
                    scene += tex2D(_CameraOpaqueTexture, uv + stepUV).rgb;
                    scene += tex2D(_CameraOpaqueTexture, uv - stepUV).rgb;
                    scene += tex2D(_CameraOpaqueTexture, uv + float2(stepUV.x, -stepUV.y)).rgb;
                    scene += tex2D(_CameraOpaqueTexture, uv + float2(-stepUV.x, stepUV.y)).rgb;
                    face = lerp(scene / 16, _Fill.rgb, _Fill.a);
                }
                float bodyAlpha = inside * (_Glass > .5 ? 1 : _Fill.a);
                fixed4 result = fixed4(_Frame.rgb * halo, halo);
                result = fixed4(face * bodyAlpha, bodyAlpha) + result * (1 - bodyAlpha);
                float edgeAlpha = edge * _Frame.a;
                result = fixed4(_Frame.rgb * edgeAlpha, edgeAlpha) + result * (1 - edgeAlpha);
                result *= input.color.a;
                #ifdef UNITY_UI_CLIP_RECT
                result *= UnityGet2DClipping(input.local.xy, _ClipRect);
                #endif
                return result;
            }
            ENDCG
        }
    }
}
