Shader "GunDemo/DeathKillerOutline"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (1, 0.015, 0.01, 1)
        _RimThreshold ("Rim Threshold", Range(0, 1)) = 0.72
    }
    SubShader
    {
        Tags { "Queue"="Overlay+100" "RenderType"="Transparent" }
        Pass
        {
            Name "DeathKillerOutline"
            Cull Off
            ZWrite Off
            ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _OutlineColor;
            float _RimThreshold;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 worldNormal : TEXCOORD0;
                float3 viewDirection : TEXCOORD1;
            };

            v2f vert(appdata input)
            {
                v2f output;
                float3 worldPosition = mul(unity_ObjectToWorld, input.vertex).xyz;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.worldNormal = UnityObjectToWorldNormal(input.normal);
                output.viewDirection = _WorldSpaceCameraPos.xyz - worldPosition;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float facing = abs(dot(
                    normalize(input.worldNormal), normalize(input.viewDirection)));
                float rim = 1.0 - facing;
                clip(rim - _RimThreshold);
                return _OutlineColor;
            }
            ENDCG
        }
    }
}
