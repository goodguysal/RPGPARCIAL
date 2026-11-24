Shader "RPG/Polytope Medieval Weapons URP"
{
    Properties
    {
        _TextureSample2("Base color", 2D) = "white" {}
        _TextureSample9("Color mask", 2D) = "white" {}
        [HideInInspector] _texcoord("", 2D) = "white" {}
        [HideInInspector] _texcoord2("", 2D) = "white" {}
        [HDR]_METAL1COLOR("Metal 1", Color) = (0.726,0.774,0.753,1)
        [HDR]_METAL2COLOR("Metal 2", Color) = (1.678,1.004,0.118,1)
        [HDR]_METAL3COLOR("Metal 3", Color) = (0.597,0.624,0.740,1)
        [HDR]_METAL4COLOR("Metal 4", Color) = (0.879,0.804,0.942,1)
        [HDR]_WOOD1COLOR("Wood 1", Color) = (0.198,0.083,0.063,1)
        [HDR]_WOOD2COLOR("Wood 2", Color) = (0.132,0.065,0.054,1)
        [HDR]_WOOD3COLOR("Wood 3", Color) = (0.104,0.075,0.047,1)
        [HDR]_LEATHER1COLOR("Leather 1", Color) = (0.292,0.130,0.092,1)
        [HDR]_LEATHER2COLOR("Leather 2", Color) = (0.066,0.035,0.031,1)
        [HDR]_LEATHER3COLOR("Leather 3", Color) = (0.132,0.031,0.022,1)
        [HDR]_PAINT1COLOR("Paint 1", Color) = (0.545,0.694,0.698,1)
        [HDR]_PAINT2COLOR("Paint 2", Color) = (0.365,0.557,0.439,1)
        [HDR]_PAINT3COLOR("Paint 3", Color) = (0.585,0.542,0.433,1)
        [HDR]_GEMS1COLOR("Gem 1", Color) = (1,0,0,1)
        [HDR]_GEMS2COLOR("Gem 2", Color) = (0,0.322,0.575,1)
        [HDR]_GEMS3COLOR("Gem 3", Color) = (0,0.472,0.136,1)
        [HDR]_FEATHERS1COLOR("Feather 1", Color) = (0.330,0.124,0.045,1)
        [HDR]_FEATHERS2COLOR("Feather 2", Color) = (0.509,0.426,0.180,1)
        [HDR]_FEATHERS3COLOR("Feather 3", Color) = (0.509,0.257,0.257,1)
        [HDR]_FEATHERS4COLOR("Feather 4", Color) = (0.811,0.210,0.210,1)
        [HDR]_FEATHERS5COLOR("Feather 5", Color) = (0.415,0.262,0.147,1)
        [HDR]_FEATHERS6COLOR("Feather 6", Color) = (0.792,0.744,0.639,1)
        [HDR]_COATOFARMSCOLOR("Coat of arms", Color) = (1,0,0,1)
        _METAL1METALLIC("Metallic", Range(0,1)) = 0.65
        _METAL1SMOOTHNESS("Smoothness", Range(0,1)) = 0.7
        _OCCLUSION("Occlusion", Range(0,1)) = 0.414
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_TextureSample2); SAMPLER(sampler_TextureSample2);
            TEXTURE2D(_TextureSample9); SAMPLER(sampler_TextureSample9);
            CBUFFER_START(UnityPerMaterial)
            float4 _TextureSample2_ST;
            half4 _METAL1COLOR,_METAL2COLOR,_METAL3COLOR,_METAL4COLOR;
            half4 _WOOD1COLOR,_WOOD2COLOR,_WOOD3COLOR,_LEATHER1COLOR,_LEATHER2COLOR,_LEATHER3COLOR;
            half4 _PAINT1COLOR,_PAINT2COLOR,_PAINT3COLOR,_GEMS1COLOR,_GEMS2COLOR,_GEMS3COLOR;
            half4 _FEATHERS1COLOR,_FEATHERS2COLOR,_FEATHERS3COLOR,_FEATHERS4COLOR,_FEATHERS5COLOR,_FEATHERS6COLOR;
            half _METAL1METALLIC,_METAL1SMOOTHNESS,_OCCLUSION;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; float2 uv2:TEXCOORD1; };
            struct Varyings { float4 positionHCS:SV_POSITION; float2 uv:TEXCOORD0; float2 uv2:TEXCOORD1; float3 normalWS:TEXCOORD2; float3 positionWS:TEXCOORD3; half fog:TEXCOORD4; };
            Varyings vert(Attributes v) {
                Varyings o; VertexPositionInputs p=GetVertexPositionInputs(v.positionOS.xyz);
                o.positionHCS=p.positionCS; o.positionWS=p.positionWS; o.normalWS=TransformObjectToWorldNormal(v.normalOS);
                o.uv=TRANSFORM_TEX(v.uv,_TextureSample2); o.uv2=v.uv2; o.fog=ComputeFogFactor(p.positionCS.z); return o;
            }
            half ToLinearChannel(half c) {
                #if defined(UNITY_COLORSPACE_GAMMA)
                    return c;
                #else
                    return c <= 0.04045h ? c / 12.92h : pow((c + 0.055h) / 1.055h, 2.4h);
                #endif
            }
            half3 ToLinear(half3 c) { return half3(ToLinearChannel(c.r),ToLinearChannel(c.g),ToLinearChannel(c.b)); }
            half Mask(half3 m, half3 key) { return saturate(1.0h - distance(m, ToLinear(key)) / 0.045h); }
            half4 frag(Varyings i):SV_Target {
                half4 baseTex=SAMPLE_TEXTURE2D(_TextureSample2,sampler_TextureSample2,i.uv);
                half3 mask=SAMPLE_TEXTURE2D(_TextureSample9,sampler_TextureSample9,i.uv).rgb;
                half3 albedo=baseTex.rgb;
                #define APPLY(KEY,COLOR) albedo=lerp(albedo,baseTex.rgb*(COLOR).rgb,Mask(mask,KEY))
                APPLY(float3(1,0.4980392,0.4980392),_PAINT3COLOR);
                APPLY(float3(0.4980392,0.4980392,0.4980392),_PAINT2COLOR);
                APPLY(float3(0.4980392,0.4980392,0),_PAINT1COLOR);
                APPLY(float3(0,0.4980392,0),_FEATHERS6COLOR);
                APPLY(float3(0,0,0),_FEATHERS5COLOR);
                APPLY(float3(1,1,0),_FEATHERS4COLOR);
                APPLY(float3(0.4980392,0,0),_FEATHERS3COLOR);
                APPLY(float3(1,0.4980392,0),_FEATHERS2COLOR);
                APPLY(float3(1,0,0),_FEATHERS1COLOR);
                APPLY(float3(0,0,1),_WOOD3COLOR);
                APPLY(float3(0,1,1),_WOOD2COLOR);
                APPLY(float3(0,1,0),_WOOD1COLOR);
                APPLY(float3(1,0.4980392,1),_LEATHER3COLOR);
                APPLY(float3(1,0,1),_LEATHER2COLOR);
                APPLY(float3(1,1,0.4980392),_LEATHER1COLOR);
                APPLY(float3(0.4980392,0.4980392,1),_METAL4COLOR);
                APPLY(float3(0,0.4980392,0.4980392),_METAL3COLOR);
                APPLY(float3(0,0,0.4980392),_METAL2COLOR);
                APPLY(float3(0.4980392,0,0.4980392),_METAL1COLOR);
                APPLY(float3(0.4980392,1,1),_GEMS3COLOR);
                APPLY(float3(0.4980392,1,0.4980392),_GEMS2COLOR);
                APPLY(float3(0.4980392,0,1),_GEMS1COLOR);
                half3 n=normalize(i.normalWS); Light light=GetMainLight();
                half ndl=saturate(dot(n,light.direction));
                half3 color=albedo*(SampleSH(n)+light.color*ndl);
                color=MixFog(color,i.fog);
                return half4(color,baseTex.a);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
