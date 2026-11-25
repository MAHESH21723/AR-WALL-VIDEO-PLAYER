// HoloWall - subtle grid for detected AR planes. Uses plane-local metres, so no UVs are needed.
// Made by Maheshwaran. MIT licensed.
Shader "HoloWall/PlaneGrid"
{
    Properties
    {
        _Color ("Grid colour", Color) = (0.25, 0.9, 1, 1)
        _Fill ("Fill alpha", Range(0, 0.3)) = 0.06
        _Cell ("Cell size (m)", Float) = 0.25
        _Line ("Line width", Range(0.001, 0.2)) = 0.03
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-10" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "PlaneGrid"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Fill;
                float _Cell;
                float _Line;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 local : TEXCOORD0; };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.local = IN.positionOS.xz;
                return OUT;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                float2 g = frac(IN.local / max(_Cell, 0.01));
                float2 d = min(g, 1.0 - g);
                float gridLine = 1.0 - smoothstep(_Line * 0.5, _Line, min(d.x, d.y));
                float a = _Fill + gridLine * 0.35;
                return float4(_Color.rgb, a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
