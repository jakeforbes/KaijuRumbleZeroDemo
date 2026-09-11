Shader "KRZ/CityAtmosphere"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            ZWrite Off ZTest Always Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            float4 _Atmosphere, _WorldView;
            float hash21(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float noise21(float2 p)
            {
                float2 i=floor(p), f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(hash21(i),hash21(i+float2(1,0)),f.x),lerp(hash21(i+float2(0,1)),hash21(i+1),f.x),f.y);
            }
            float3 sourceColour(float2 uv) { return SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,saturate(uv)).rgb; }
            float3 bright(float2 uv)
            {
                float3 c=sourceColour(uv);
                float peak=max(c.r,max(c.g,c.b));
                return c*smoothstep(.48,1.1,peak);
            }
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv=input.texcoord;
                float3 c=sourceColour(uv);
                float peak=max(c.r,max(c.g,c.b));
                // Preserve luminous windows, projectiles and the established pickup colours.
                float highlight=smoothstep(.38,.95,peak);
                float3 night=c*float3(.18,.29,.44);
                c=lerp(c,night,_Atmosphere.x*(1-highlight*.85));
                float2 texel=_BlitTexture_TexelSize.xy;
                float3 glow=0;
                [unroll] for(int i=0;i<8;i++)
                {
                    float angle=i*.785398;
                    float2 direction=float2(cos(angle),sin(angle));
                    glow+=bright(uv+direction*texel*4)*.075;
                    glow+=bright(uv+direction*texel*12)*.05;
                }
                c+=glow*_Atmosphere.y;
                float2 worldUv=uv;
                #if UNITY_UV_STARTS_AT_TOP
                worldUv.y=1-worldUv.y;
                #endif
                float2 world=_WorldView.xy+(worldUv*2-1)*_WorldView.zw;
                float2 drift=float2(_Atmosphere.w*.035,-_Atmosphere.w*.018);
                float mist=noise21(world*float2(.11,.19)+drift)*.65+noise21(world*.29-drift*.7)*.35;
                mist=smoothstep(.43,.72,mist)*_Atmosphere.z;
                c=lerp(c,float3(.035,.12,.21),mist*(1-highlight*.7));
                float vignette=smoothstep(.28,.85,length((uv-.5)*float2(1,.85)));
                c*=1-vignette*.15*_Atmosphere.x;
                return half4(c,1);
            }
            ENDHLSL
        }
    }
}
