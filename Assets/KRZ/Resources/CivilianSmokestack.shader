Shader "KRZ/CivilianSmokestack"
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
            float4 _Motion, _Fade;
            struct Input { float4 vertex : POSITION; float2 uv : TEXCOORD0; float2 phase : TEXCOORD1; };
            struct Varyings { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; float phase : TEXCOORD1; };
            Varyings vert(Input v)
            {
                Varyings o; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.uv; o.phase=v.phase.x; return o;
            }
            float ellipse(float2 p, float2 centre, float2 size)
            { return 1-smoothstep(.92,1.04,length((p-centre)/size)); }
            half4 frag(Varyings i) : SV_Target
            {
                float2 p = i.uv*2-1;
                float seed = _Motion.y+i.phase;
                float smoke = 0;
                // Overlapping expanding puffs: local to the chimney, with no spawned objects.
                [unroll] for(int n=0;n<7;n++)
                {
                    float life = frac(_Motion.x*.24 + seed + n/7.0);
                    float x = .24*life + sin(life*6+seed+n*.7)*.10*life;
                    float y = -.2 + life*1.18;
                    float radius = .13 + life*.40;
                    float2 d = (p-float2(x,y))/float2(radius,radius/3);
                    float puff = pow(saturate(1-dot(d,d)),2);
                    float envelope = smoothstep(0,.1,life)*(1-smoothstep(.55,1,life));
                    smoke += puff*envelope;
                }
                float smokeAlpha = saturate(smoke)*_Motion.z;
                float body = (1-smoothstep(.18,.205,abs(p.x))) * smoothstep(-.91,-.885,p.y) * (1-smoothstep(-.24,-.225,p.y));
                float base = ellipse(p,float2(0,-.89),float2(.31,.045));
                float rim = ellipse(p,float2(0,-.235),float2(.255,.045));
                float opening = ellipse(p,float2(0,-.229),float2(.178,.023));
                float stack = max(body,max(base,rim));
                float highlight = pow(saturate(1-abs(p.x+.065)*4),2);
                float3 metal = lerp(float3(.07,.11,.15),float3(.30,.39,.46),highlight);
                float bands = (1-smoothstep(.008,.018,abs(p.y+.47))) + (1-smoothstep(.008,.018,abs(p.y+.77)));
                metal = lerp(metal,float3(.11,.17,.22),saturate(bands)*body);
                metal = lerp(metal,float3(.35,.43,.48),rim);
                metal = lerp(metal,float3(.014,.021,.028),opening);
                float alpha = stack + smokeAlpha*(1-stack);
                float3 smokeColour = float3(.44,.49,.53);
                float3 colour = (metal*stack + smokeColour*smokeAlpha*(1-stack))/max(alpha,.001);
                return half4(colour*_Fade.rgb,alpha*_Fade.a);
            }
            ENDHLSL
        }
    }
}
