Shader "KRZ/HomingMissile"
{
    Properties { _Accent ("Accent", Color) = (1,.45,.12,1) _Opacity ("Opacity", Float) = 1 }
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
            float _FlamePhase;
            float4 _Accent;
            float _Opacity;
            struct Input { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings vert(Input v)
            {
                Varyings o; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.uv; return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                float2 p = float2(i.uv.x*1.85-1.3,(i.uv.y-.5)*.75);
                float aa = max(fwidth(p.y),.004);
                float nose = saturate((.55-p.x)/.26);
                float halfWidth = .09*nose;
                float hull = smoothstep(-.425,-.41,p.x)*(1-smoothstep(.545,.55,p.x))
                           * (1-smoothstep(halfWidth-aa,halfWidth+aa,abs(p.y)));
                // Swept tail fins: wide at the engine and tucked against the fuselage ahead.
                float finWidth = lerp(.235,.085,saturate((p.x+.42)/.30));
                float fins = smoothstep(-.435,-.42,p.x)*(1-smoothstep(-.135,-.12,p.x))
                           * (1-smoothstep(finWidth-aa,finWidth+aa,abs(p.y)));
                float silhouette = max(hull,fins);
                float3 colour = float3(.16,.23,.28);
                float finEdge = smoothstep(finWidth-.025,finWidth,abs(p.y));
                colour = lerp(float3(.40,.50,.56),colour,finEdge);
                float sheen = saturate(1-abs(p.y-.023)/.13);
                float3 shell = lerp(float3(.34,.41,.46),float3(.85,.90,.92),sheen);
                float orange = max(smoothstep(.235,.26,p.x),1-smoothstep(.026,.035,abs(p.x+.055)));
                shell = lerp(shell,_Accent.rgb*(.65+.35*sheen),orange);
                float edge = smoothstep(max(0,halfWidth-.018),halfWidth,abs(p.y));
                shell = lerp(shell,float3(.10,.15,.19),edge);
                colour = lerp(colour,shell,hull);
                // The nozzle and orange-white exhaust remain behind the pointed nose.
                float nozzle = (1-smoothstep(.023,.031,abs(p.x+.395)))*(1-smoothstep(.06,.075,abs(p.y)));
                colour = lerp(colour,float3(.12,.16,.19),nozzle);
                float flameLength = .58 + .12*sin(_FlamePhase) + .045*sin(_FlamePhase*2.7);
                float tail = (-p.x-.415)/flameLength;
                float flameWidth = .072*pow(saturate(1-tail),.65);
                float wobble = .014*sin(_FlamePhase*.7+tail*12)*saturate(tail);
                float cross = abs(p.y-wobble)/max(flameWidth,.001);
                float flame = (1-smoothstep(.55,1.25,cross))*step(0,tail)*(1-smoothstep(.75,1,tail));
                float hot = (1-smoothstep(.1,.65,cross))*(1-smoothstep(.15,.7,tail));
                float3 exhaustColour = lerp(_Accent.rgb,lerp(_Accent.rgb,float3(1,1,1),.85),hot);
                float alpha = silhouette + flame*(1-silhouette);
                colour = (colour*silhouette+exhaustColour*flame*(1-silhouette))/max(alpha,.001);
                return half4(colour,alpha*_Opacity);
            }
            ENDHLSL
        }
    }
}
