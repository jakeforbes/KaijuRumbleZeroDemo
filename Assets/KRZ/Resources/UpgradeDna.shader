Shader "KRZ/UpgradeDna"
{
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 _Tint;
            float _Phase;
            struct Input { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings vert(Input v)
            {
                Varyings o; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.uv; return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                float2 p = i.uv*2-1;
                float theta = p.y*6.7 + _Phase;
                float x = .62*sin(theta);
                float slope = .62*6.7*cos(theta);
                // Distance to each helical strand, corrected for its projected slope.
                float correction = sqrt(1 + .75*.75*slope*slope);
                float dA = abs(p.x-x)*.75/correction;
                float dB = abs(p.x+x)*.75/correction;
                float cap = 1-smoothstep(.79,.87,abs(p.y));
                float aa = max(fwidth(p.y),.009);
                float strandA = (1-smoothstep(.024,.024+aa,dA))*cap;
                float strandB = (1-smoothstep(.024,.024+aa,dB))*cap;
                float nearest = min(dA,dB);
                float halo = exp(-nearest*nearest/.004)*cap*.17;
                float rungMask = 0;
                [unroll] for(int n=0;n<10;n++)
                {
                    float y = -.75 + n/9.0*1.5;
                    float endX = abs(.62*sin(y*6.7+_Phase));
                    float2 d = float2(max(abs(p.x)-endX,0)*.75,p.y-y);
                    float rung = 1-smoothstep(.015,.015+aa,length(d));
                    rungMask = max(rungMask,rung);
                }
                float depthA = .66 + .34*(cos(theta)*.5+.5);
                float depthB = .66 + .34*(-cos(theta)*.5+.5);
                float3 colour = _Tint.rgb*.67;
                // Paint the nearer strand last where the two cross.
                if(cos(theta)>0)
                {
                    colour=lerp(colour,_Tint.rgb*depthB,strandB);
                    colour=lerp(colour,_Tint.rgb*depthA,strandA);
                }
                else
                {
                    colour=lerp(colour,_Tint.rgb*depthA,strandA);
                    colour=lerp(colour,_Tint.rgb*depthB,strandB);
                }
                float solid = max(rungMask,max(strandA,strandB));
                float alpha = (solid + halo*(1-solid))*_Tint.a;
                // A dark edge keeps pale upgrade colors readable on bright terrain.
                float outline=(1-smoothstep(.045,.045+aa,nearest))*cap;
                colour=lerp(_Tint.rgb*.14,colour,saturate(solid+halo));
                alpha=max(alpha,outline*.8*_Tint.a);
                return half4(colour,alpha);
            }
            ENDHLSL
        }
    }
}
