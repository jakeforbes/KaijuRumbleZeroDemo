Shader "KRZ/EnergyBeam"
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
            float4 _Tint;
            float _Progress, _Aspect;
            struct Input { float4 vertex:POSITION; float2 uv:TEXCOORD0; };
            struct Varyings { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; };
            Varyings vert(Input v) { Varyings o; o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;return o; }
            float glow(float x,float w) { x/=w;return exp(-x*x); }
            half4 frag(Varyings i):SV_Target
            {
                float x=i.uv.x,y=i.uv.y*2-1;
                float fade=1-smoothstep(.36,1,_Progress);
                float thickness=lerp(1,.32,_Progress);
                float core=glow(y,.10*thickness);
                float mantle=glow(y,.30*thickness);
                float halo=glow(y,.69*thickness)*.40;
                float flow=sin(x*34-_Progress*32)*.045;
                float filaments=glow(y-.23-flow,.023)*.40+glow(y+.23+flow,.023)*.40;
                float cap=smoothstep(0,.018,x)*(1-smoothstep(.975,1,x));
                float muzzle=glow(x*max(_Aspect,1),.55)*glow(y,.7)*.65;
                float impact=glow((1-x)*max(_Aspect,1),.4)*glow(y,.6)*.5;
                float energy=(core+mantle*.6+halo+filaments)*cap+muzzle+impact;
                float boundary=1-smoothstep(.85,1,abs(y));
                return half4(lerp(_Tint.rgb,float3(1,1,1),core*.86),saturate(energy)*fade*boundary*_Tint.a);
            }
            ENDHLSL
        }
    }
}
