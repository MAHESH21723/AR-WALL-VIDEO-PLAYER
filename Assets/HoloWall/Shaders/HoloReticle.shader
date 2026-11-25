// HoloWall - procedural placement reticle (animated ring + corner ticks). Made by Maheshwaran. MIT licensed.
Shader "HoloWall/Reticle"
{
    Properties
    {
        _Color ("Colour", Color) = (0.25, 0.9, 1, 1)
        _Color2 ("Second colour", Color) = (0.55, 0.36, 1, 1)
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+10" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "Reticle"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _Color2;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                float2 p = IN.uv * 2.0 - 1.0;
                float r = length(p);
                float pulse = 0.5 + 0.5 * sin(_Time.y * 3.0);

                // Outer ring.
                float ring = smoothstep(0.06, 0.0, abs(r - 0.82));
                // Inner dot.
                float core = smoothstep(0.14, 0.0, r);
                // Rotating inner arc segments.
                float ang = atan2(p.y, p.x) + _Time.y * 1.5;
                float seg = step(0.0, sin(ang * 3.0)) * smoothstep(0.05, 0.0, abs(r - 0.5));

                float3 c = _Color.rgb * (ring + core) + _Color2.rgb * seg;
                float a = saturate(ring * (0.7 + 0.3 * pulse) + core + seg * 0.8);
                return float4(c, a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
