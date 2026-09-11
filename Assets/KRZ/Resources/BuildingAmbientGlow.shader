Shader "KRZ/BuildingAmbientGlow"
{
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Off ZWrite Off Blend SrcAlpha One
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct Input { float4 vertex : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct Varyings { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            Varyings vert(Input v)
            {
                Varyings o; o.vertex = UnityObjectToClipPos(v.vertex); o.uv = v.uv; o.color = v.color; return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                float radius = length(i.uv * 2 - 1);
                float glow = pow(saturate(1 - radius), 3);
                return half4(i.color.rgb, i.color.a * glow);
            }
            ENDHLSL
        }
    }
}
