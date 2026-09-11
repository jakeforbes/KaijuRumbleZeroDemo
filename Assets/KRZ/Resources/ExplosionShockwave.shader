Shader "KRZ/ExplosionShockwave"
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
            float _Progress;
            float4 _Tint;
            struct Input { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings vert(Input v)
            {
                Varyings o; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.uv; return o;
            }
            float band(float distance, float width) { float d=distance/width; return exp(-d*d); }
            half4 frag(Varyings i) : SV_Target
            {
                float radius = length(i.uv*2-1);
                float travel = 1-pow(1-saturate(_Progress),1.6);
                float front = lerp(.025,.98,travel);
                float width = lerp(.012,.028,travel);
                float ring = band(radius-front,width);
                float halo = band(radius-front,width*3)*.40;
                float trailing = band(radius-front*.78,width*.8)*.35*smoothstep(.06,.22,_Progress);
                float flash = band(radius,.055)*(1-smoothstep(0,.13,_Progress))*.6;
                float fade = 1-smoothstep(.38,1,_Progress);
                float boundary = 1-smoothstep(.98,1,radius);
                float alpha = saturate(ring+halo+trailing+flash)*fade*boundary*_Tint.a;
                float3 colour = lerp(_Tint.rgb,float3(1,1,1),ring*.65);
                return half4(colour,alpha);
            }
            ENDHLSL
        }
    }
}
