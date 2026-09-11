Shader "KRZ/CantileverEquipment"
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
            struct Input { float4 vertex : POSITION; float2 uv : TEXCOORD0; float2 kind : TEXCOORD1; };
            struct Varyings { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; float2 kind : TEXCOORD1; };
            Varyings vert(Input v)
            {
                Varyings o; o.vertex = UnityObjectToClipPos(v.vertex); o.uv = v.uv; o.kind = v.kind; return o;
            }
            float disc(float2 p, float r) { return 1 - smoothstep(r - .025, r + .025, length(p)); }
            float segmentMask(float2 p, float2 a, float2 b, float w)
            {
                float2 ab = b - a;
                float d = length(p - a - ab * saturate(dot(p-a, ab) / max(dot(ab,ab),.001)));
                return 1 - smoothstep(w, w + .015, d);
            }
            half4 frag(Varyings i) : SV_Target
            {
                float2 p = i.uv * 2 - 1;
                float3 colour;
                float alpha;
                if (i.kind.x < .5)
                {
                    float r = length(p);
                    alpha = disc(p,.96);
                    float rim = smoothstep(.74,.79,r);
                    float light = saturate(.55 + p.y*.24 - p.x*.2);
                    colour = lerp(float3(.025,.044,.063), float3(.19,.28,.39) * light, rim);
                    float angle = atan2(p.y,p.x) - _Motion.x * (9 + i.kind.y*.35) - _Motion.z;
                    float blade = smoothstep(.08,.28,sin(angle * 5 + r * 2.2)) * disc(p,.73) * (1-disc(p,.17));
                    colour = lerp(colour, float3(.31,.43,.53) * (.6 + .4*light), blade);
                    float lip = smoothstep(.85,.88,r) * (1-smoothstep(.91,.94,r));
                    colour = lerp(colour, float3(.37,.51,.62), lip * light);
                    colour = lerp(colour, float3(.16,.29,.37), disc(p,.19));
                    colour = lerp(colour, float3(.32,.82,.94), disc(p,.065));
                    // Stationary guard bars give the spinning blades a physical enclosure.
                    float guard = max(segmentMask(p,float2(-.68,0),float2(.68,0),.022),segmentMask(p,float2(0,-.68),float2(0,.68),.022));
                    colour = lerp(colour,float3(.12,.19,.25),guard*.8);
                }
                else
                {
                    float yaw = _Motion.y * .65 + _Motion.z;
                    float facing = cos(yaw);
                    float bowlWidth = .22 + .5 * abs(facing);
                    float2 bowlCentre = float2(.09*sin(yaw),.22);
                    float2 bowl = (p - bowlCentre) / float2(bowlWidth,.46);
                    float bowlRadius = length(bowl);
                    float bowlMask = 1-smoothstep(.96,1.02,bowlRadius);
                    float baseMask = disc((p-float2(0,-.76))/float2(1,.3),.37);
                    float mast = segmentMask(p,float2(0,-.74),float2(0,.04),.065);
                    alpha = max(baseMask,max(mast,bowlMask));
                    colour = float3(.12,.21,.29);
                    float3 dishColour = lerp(float3(.14,.23,.32),float3(.48,.61,.7),saturate(.65+bowl.y*.3-bowl.x*.15));
                    if(facing < 0) dishColour *= .63;
                    float rim = smoothstep(.85,.94,bowlRadius);
                    dishColour = lerp(dishColour,float3(.64,.76,.83),rim);
                    colour = lerp(colour,dishColour,bowlMask);
                    float ribs = max(segmentMask(bowl,float2(-.85,0),float2(.85,0),.012),segmentMask(bowl,float2(0,-.85),float2(0,.85),.012));
                    colour = lerp(colour,float3(.25,.38,.47),ribs*bowlMask*.6);
                    // The feed arm changes sides and projected length as the dish turns.
                    float2 feed = bowlCentre + float2(.53*sin(yaw),.40);
                    float arm = segmentMask(p,bowlCentre-float2(0,.22),feed,.021);
                    float receiver = disc(p-feed,.055);
                    colour = lerp(colour,float3(.31,.43,.53),arm);
                    colour = lerp(colour,float3(.37,.82,.95),receiver);
                    alpha = max(alpha,max(arm,receiver));
                }
                return half4(colour * _Fade.rgb, alpha * _Fade.a);
            }
            ENDHLSL
        }
    }
}
