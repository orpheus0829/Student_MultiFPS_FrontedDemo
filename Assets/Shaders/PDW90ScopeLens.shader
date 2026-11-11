Shader "GunDemo/Weapons/PDW90ScopeLens"
{
    Properties
    {
        _ScopeTex ("Scope View", 2D) = "black" {}
        _DotColor ("Red Dot", Color) = (2.5, 0.01, 0.002, 1)
        _DotRadius ("Dot Radius", Range(0.002, 0.08)) = 0.027
        _EdgeSoftness ("Lens Edge Softness", Range(0.001, 0.2)) = 0.035
        _AimWeight ("Aim Weight", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+20"
            "RenderPipeline" = "UniversalPipeline"
        }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "ScopeLens"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

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

            TEXTURE2D(_ScopeTex);
            SAMPLER(sampler_ScopeTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _DotColor;
                float _DotRadius;
                float _EdgeSoftness;
                float _AimWeight;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 centered = input.uv * 2.0 - 1.0;
                float radius = length(centered);
                float lensMask = 1.0 - smoothstep(
                    1.0 - _EdgeSoftness,
                    1.0,
                    radius);
                clip(lensMask - 0.001);

                half3 scopeColor = SAMPLE_TEXTURE2D(
                    _ScopeTex,
                    sampler_ScopeTex,
                    input.uv).rgb;
                float dotCore = 1.0 - smoothstep(
                    _DotRadius * 0.55,
                    _DotRadius,
                    radius);
                float dotGlow = 1.0 - smoothstep(
                    _DotRadius,
                    _DotRadius * 3.2,
                    radius);
                float dotWeight = saturate(_AimWeight) * _DotColor.a;
                half3 finalColor = lerp(
                    scopeColor,
                    _DotColor.rgb,
                    dotCore * dotWeight);
                finalColor += _DotColor.rgb * dotGlow * dotWeight * 0.22;
                return half4(finalColor, lensMask);
            }
            ENDHLSL
        }
    }
}
