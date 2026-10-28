Shader "GunDemo/World Space Checker Blockout"
{
    Properties
    {
        _DarkColor ("Dark Gray", Color) = (0.28, 0.31, 0.34, 1)
        _LightColor ("Light Gray", Color) = (0.68, 0.72, 0.76, 1)
        _CheckerSize ("Checker Size (Meters)", Range(0.1, 5)) = 0.5
        _Contrast ("Lighting Contrast", Range(0.5, 2)) = 1.15
        _Metallic ("Metallic", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _DarkColor;
                half4 _LightColor;
                float _CheckerSize;
                half _Contrast;
                half _Metallic;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            // Analytically filtered checker. It integrates the pattern over roughly
            // one screen pixel instead of using a hard frac/floor boundary.
            float CheckerAA(float2 worldPlane, float checkerSize)
            {
                float2 uv = worldPlane / max(checkerSize, 0.0001);
                float2 pixelWidth = max(fwidth(uv), float2(0.0001, 0.0001));
                float2 halfWidth = pixelWidth * 0.5;
                float2 integral = 2.0 *
                    (abs(frac((uv - halfWidth) * 0.5) - 0.5) -
                     abs(frac((uv + halfWidth) * 0.5) - 0.5)) / pixelWidth;
                float checker = 0.5 - 0.5 * integral.x * integral.y;
                float distantFade = saturate(max(pixelWidth.x, pixelWidth.y) - 0.5);
                return lerp(saturate(checker), 0.5, distantFade);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                float3 weights = pow(abs(normalWS), 8.0);
                weights /= max(weights.x + weights.y + weights.z, 0.0001);

                // XZ is used on floors; the other projections keep walls and ramps
                // world-aligned without relying on stretched mesh UVs.
                float checkerX = CheckerAA(input.positionWS.zy, _CheckerSize);
                float checkerY = CheckerAA(input.positionWS.xz, _CheckerSize);
                float checkerZ = CheckerAA(input.positionWS.xy, _CheckerSize);
                float checker = dot(float3(checkerX, checkerY, checkerZ), weights);

                half3 albedo = lerp(_DarkColor.rgb, _LightColor.rgb, checker);
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half diffuse = saturate(dot(normalWS, mainLight.direction)) * mainLight.shadowAttenuation;
                half upLight = saturate(normalWS.y * 0.5h + 0.5h);
                half lighting = saturate((0.26h + upLight * 0.18h + diffuse * 0.62h) * _Contrast);

                // Deliberately matte: Lambert lighting only, no metallic/specular term.
                return half4(albedo * lighting, 1.0h);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
    }
}
