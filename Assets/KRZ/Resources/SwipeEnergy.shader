Shader "KRZ/SwipeEnergy"
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
            float _Progress, _HalfArc;
            float4 _EnergyColour;
            struct Input { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings vert(Input v)
            {
                Varyings o; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.uv; return o;
            }
            float gaussian(float x) { return exp(-x*x); }
            half4 frag(Varyings i) : SV_Target
            {
                float2 p = i.uv*2-1;
                float radius = length(p);
                float angle = atan2(p.y,p.x);
                float along = (angle + _HalfArc) / max(2*_HalfArc,.001);
                float sector = step(0,along)*step(along,1);
                float boundary = (1-smoothstep(.97,1,radius))*sector;
                // Leading edge crosses the hit sector, leaving a curved, tapering wake.
                float travel = saturate(_Progress/.72);
                float head = lerp(.18,1,1-pow(1-travel,2));
                float behind = head-along;
                float wake = smoothstep(0,.045,behind)*(1-smoothstep(.05,.64,behind));
                float ends = smoothstep(0,.045,along)*(1-smoothstep(.965,1,along));
                float centre = .91 - .15*saturate(behind/.64);
                float width = .045 + .065*saturate(behind/.45);
                float edge = abs(radius-centre);
                float core = gaussian(edge/(width*.32));
                float glow = gaussian(edge/(width*1.8))*.47;
                float innerStreak = gaussian((radius-centre+.15)/.016)*.34;
                float outerStreak = gaussian((radius-centre-.052)/.01)*.5;
                float energy = (core + glow + innerStreak + outerStreak)*wake*ends;
                float sparks = 0;
                [unroll] for(int n=0;n<7;n++)
                {
                    float birth = n*.072;
                    float age = _Progress-birth;
                    float a = lerp(-_HalfArc,_HalfArc,.12+n*.12);
                    float r = min(.97,.77+max(age,0)*.30);
                    float2 pos = float2(cos(a),sin(a))*r;
                    float2 d = p-pos;
                    float spark = exp(-dot(d,d)/(.00012 + max(age,0)*.0004));
                    sparks += spark*step(0,age)*(1-smoothstep(.02,.32,age));
                }
                float fade = 1-smoothstep(.64,1,_Progress);
                float alpha = saturate(energy+sparks)*boundary*fade*_EnergyColour.a;
                float hot = saturate(core*wake+sparks);
                float3 colour = lerp(_EnergyColour.rgb,float3(.9,1,1),hot);
                return half4(colour,alpha);
            }
            ENDHLSL
        }
    }
}
