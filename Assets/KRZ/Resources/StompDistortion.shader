Shader "KRZ/StompDistortion"
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
            int _WaveCount;
            float4 _Waves[8];
            float _WaveAges[8];
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float2 offset = 0;
                [loop] for(int n=0;n<_WaveCount;n++)
                {
                    float t=saturate(_WaveAges[n]);
                    // WorldToViewportPoint is bottom-up; render-target sampling can be top-down.
                    float2 centre=_Waves[n].xy;
                    #if UNITY_UV_STARTS_AT_TOP
                    centre.y=1-centre.y;
                    #endif
                    float2 q=(uv-centre)/_Waves[n].zw;
                    float r=length(q);
                    float front=lerp(.025,.98,1-pow(1-t,1.6));
                    float d=(r-front)/.065;
                    float pressure=d*exp(-d*d)*(1-smoothstep(.38,1,t));
                    offset+=q/max(r,.001)*_Waves[n].zw*pressure*.045;
                }
                return SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,saturate(uv+offset));
            }
            ENDHLSL
        }
    }
}
