Shader "KRZ/LabDomeGlob"
{
    Properties { _MainTex ("Dome artwork", 2D) = "white" {} }
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
            sampler2D _MainTex;
            float4 _Dome, _Animation;
            float _GlobSize;
            struct Input { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings vert(Input v)
            {
                Varyings o; o.vertex = UnityObjectToClipPos(v.vertex); o.uv = v.uv; return o;
            }
            float ball(float2 p, float2 centre, float radius)
            {
                float2 d = p - centre;
                return radius * radius / max(dot(d, d), .002);
            }
            half4 frag(Varyings i) : SV_Target
            {
                float2 p = i.uv * 2 - 1;
                float containment = 1 - smoothstep(.76, .98, length(p));
                float t = _Animation.x, s = _Animation.y;
                // Incommensurate oscillations make sharp changes in direction without teleporting.
                float2 centre = float2(.24 * sin(t * 3.7 + s) + .08 * sin(t * 9.1 + s * 2),
                                      .28 * sin(t * 4.3 + s * 1.7) + .07 * cos(t * 10.7 + s));
                float2 stretch = float2(1 + .22 * sin(t * 6.3 + s), 1 + .24 * cos(t * 5.7 + s));
                float2 a = centre + float2(.25 * sin(t * 5.1 + s), .23 * cos(t * 6.7 + s));
                float2 b = centre + float2(.30 * cos(t * 4.7 + s), .25 * sin(t * 7.3 + s));
                float size = clamp(_GlobSize, .5, 1.3);
                float field = ball((p - centre) * stretch, float2(0,0), .23 * size)
                            + ball(p, a, .13 * size) + ball(p, b, .11 * size);
                float body = smoothstep(.87, 1.16, field);
                float hot = smoothstep(1.6, 4.5, field);
                float halo = saturate(field * .22) * .46;
                // Source-aware transmission leaves dark dome ribs visible over the liquid.
                half4 glass = tex2D(_MainTex, _Dome.xy + (i.uv - .5) * _Dome.zw);
                float transmission = smoothstep(.10, .32, dot(glass.rgb, float3(.2126,.7152,.0722))) * glass.a;
                float pulse = .86 + .14 * sin(t * 8 + s);
                float3 colour = lerp(float3(1,.28,.025), float3(1,.94,.46), hot);
                float alpha = (body * .83 + halo) * containment * transmission * pulse * _Animation.z;
                return half4(colour, alpha);
            }
            ENDHLSL
        }
    }
}
