// HoloWall - URP unlit transparent chroma-key shader with hologram effects.
// Made by Maheshwaran. MIT licensed.
//
// Two display modes (driven by _Hologram):
//   1 = Hologram : key colour removed, optional cyan tint, scanlines, flicker and rim glow.
//   0 = Screen   : normal video with a thin glowing frame.
Shader "HoloWall/HoloChromaKey"
{
    Properties
    {
        _MainTex ("Video", 2D) = "black" {}
        [Toggle] _Hologram ("Hologram mode (1) / Screen mode (0)", Float) = 1

        [Header(Chroma key)]
        _KeyColor ("Key colour", Color) = (0, 1, 0, 1)
        _Similarity ("Similarity", Range(0, 1)) = 0.28
        _Smoothness ("Smoothness", Range(0, 0.5)) = 0.12
        _Spill ("Spill suppression", Range(0, 1)) = 0.8

        [Header(Hologram look)]
        [Toggle] _CyanTint ("Cyan tint", Float) = 0
        _TintColor ("Tint colour", Color) = (0.2, 0.9, 1, 1)
        _TintAmount ("Tint amount", Range(0, 1)) = 0.55
        _Scanlines ("Scanline strength", Range(0, 1)) = 0.25
        _ScanCount ("Scanline count", Float) = 180
        _ScanSpeed ("Scanline speed", Float) = 0.6
        _Flicker ("Flicker", Range(0, 0.5)) = 0.08
        _RimGlow ("Rim glow", Range(0, 4)) = 1.2
        _RimColor ("Rim colour", Color) = (0.3, 0.95, 1, 1)
        _Brightness ("Brightness", Range(0, 3)) = 1.15

        [Header(Screen frame)]
        _FrameColor ("Frame colour", Color) = (0.25, 0.9, 1, 1)
        _FrameWidth ("Frame width", Range(0, 0.1)) = 0.012
        _FrameGlow ("Frame glow", Range(0, 3)) = 1.4

        _Opacity ("Opacity", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "HoloUnlit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _MainTex_TexelSize;
                float _Hologram;
                float4 _KeyColor;
                float _Similarity;
                float _Smoothness;
                float _Spill;
                float _CyanTint;
                float4 _TintColor;
                float _TintAmount;
                float _Scanlines;
                float _ScanCount;
                float _ScanSpeed;
                float _Flicker;
                float _RimGlow;
                float4 _RimColor;
                float _Brightness;
                float4 _FrameColor;
                float _FrameWidth;
                float _FrameGlow;
                float _Opacity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                return OUT;
            }

            // Chroma (Cb, Cr) of a colour - ignores brightness so shadows on the green screen still key out.
            float2 ChromaOf(float3 c)
            {
                return float2(
                    -0.168736 * c.r - 0.331264 * c.g + 0.5 * c.b,
                     0.5 * c.r - 0.418688 * c.g - 0.081312 * c.b);
            }

            // 0 = fully keyed out (background), 1 = fully kept.
            float KeyAlpha(float3 rgb)
            {
                float d = distance(ChromaOf(rgb), ChromaOf(_KeyColor.rgb));
                return smoothstep(_Similarity, _Similarity + max(_Smoothness, 0.0001), d);
            }

            float4 frag(Varyings IN) : SV_Target
            {
                float4 video = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                float3 col = video.rgb;

                if (_Hologram > 0.5)
                {
                    float a = KeyAlpha(col);

                    // Spill suppression: pull leftover green fringes towards the other channels.
                    float limit = lerp(col.g, max(col.r, col.b), _Spill);
                    col.g = min(col.g, limit);

                    // Rim glow: where this pixel is kept but its neighbours are keyed out.
                    float2 o = _MainTex_TexelSize.xy * 2.5;
                    float n = KeyAlpha(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(o.x, 0)).rgb)
                            + KeyAlpha(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv - float2(o.x, 0)).rgb)
                            + KeyAlpha(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(0, o.y)).rgb)
                            + KeyAlpha(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv - float2(0, o.y)).rgb);
                    float rim = saturate(a * (1.0 - n * 0.25)) * _RimGlow;

                    // Optional cyan tint keeps the luminance but swaps the hue.
                    float lum = dot(col, float3(0.299, 0.587, 0.114));
                    col = lerp(col, _TintColor.rgb * (0.35 + lum * 1.4), _CyanTint * _TintAmount);

                    // Scanlines + flicker.
                    float scan = 1.0 - _Scanlines * (0.5 + 0.5 * sin(IN.uv.y * _ScanCount * 6.2831853 + _Time.y * _ScanSpeed * 6.2831853));
                    float flick = 1.0 + _Flicker * (sin(_Time.y * 43.0) * sin(_Time.y * 17.0 + 1.3));

                    col = col * scan * flick * _Brightness + _RimColor.rgb * rim * a;
                    return float4(col, a * _Opacity * (0.92 + 0.08 * flick));
                }
                else
                {
                    // Screen mode: plain video with a thin glowing frame inside the quad edge.
                    float2 e = min(IN.uv, 1.0 - IN.uv);
                    float edge = min(e.x, e.y);
                    float frame = 1.0 - smoothstep(_FrameWidth * 0.6, _FrameWidth, edge);
                    float halo = (1.0 - smoothstep(_FrameWidth, _FrameWidth * 4.0, edge)) * 0.25;
                    float3 outCol = col * (1.0 - frame) + _FrameColor.rgb * _FrameGlow * (frame + halo);
                    return float4(outCol, _Opacity);
                }
            }
            ENDHLSL
        }
    }
    Fallback Off
}
