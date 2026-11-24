Shader "RPG/Polytope Modular NPC URP"
{
    Properties
    {
        [HideInInspector] _TextureSample0("Color Mask", 2D) = "white" {}
        [HideInInspector] _TextureSample2("Base Texture", 2D) = "white" {}
        _FEATHERS2COLOR("Feathers 2", Color) = (1,0,0,1)
        _FEATHERS1COLOR("Feathers 1", Color) = (0.77,0.49,0.49,1)
        _CLOTH1COLOR("Cloth 1", Color) = (0,0.18,0.05,1)
        _CLOTH2COLOR("Cloth 2", Color) = (0.61,0.39,0.39,1)
        _CLOTH3COLOR("Cloth 3", Color) = (0.35,0.11,0,1)
        _CLOTH4COLOR("Cloth 4", Color) = (0.4,0.4,0.4,1)
        _LEATHER1COLOR("Leather 1", Color) = (0.3,0.03,0,1)
        _LEATHER2COLOR("Leather 2", Color) = (0.19,0.03,0,1)
        _LEATHER3COLOR("Leather 3", Color) = (0.85,0.32,0,1)
        _LEATHER4COLOR("Leather 4", Color) = (0.53,0.23,0,1)
        _METAL1COLOR("Metal 1", Color) = (0.88,0.99,1,1)
        _METAL2COLOR("Metal 2", Color) = (0.81,0.81,0.85,1)
        _METAL3COLOR("Metal 3", Color) = (0.45,0.45,0.45,1)
        _METAL4COLOR("Metal 4", Color) = (0.35,0.35,0.35,1)
        _OTHERCOLOR("Other", Color) = (0.95,0.83,0.76,1)
        _LIPSCOLOR("Lips", Color) = (0.7,0.23,0.19,1)
        _SCLERACOLOR("Sclera", Color) = (0.91,0.82,0.82,1)
        _EYESCOLOR("Eyes", Color) = (0.004,0.009,0.002,1)
        _HAIRCOLOR("Hair", Color) = (0.25,0.07,0.006,1)
        _SKINCOLOR("Skin", Color) = (0.84,0.57,0.49,1)
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_TextureSample0);
            SAMPLER(sampler_TextureSample0);
            TEXTURE2D(_TextureSample2);
            SAMPLER(sampler_TextureSample2);
            CBUFFER_START(UnityPerMaterial)
                float4 _TextureSample0_ST;
                float4 _TextureSample2_ST;
                float4 _FEATHERS2COLOR, _FEATHERS1COLOR;
                float4 _CLOTH1COLOR, _CLOTH2COLOR, _CLOTH3COLOR, _CLOTH4COLOR;
                float4 _LEATHER1COLOR, _LEATHER2COLOR, _LEATHER3COLOR, _LEATHER4COLOR;
                float4 _METAL1COLOR, _METAL2COLOR, _METAL3COLOR, _METAL4COLOR;
                float4 _OTHERCOLOR, _LIPSCOLOR, _SCLERACOLOR, _EYESCOLOR, _HAIRCOLOR, _SKINCOLOR;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; float2 uv : TEXCOORD2; half fog : TEXCOORD3; };

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = p.positionCS;
                output.positionWS = p.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                output.fog = ComputeFogFactor(p.positionCS.z);
                return output;
            }

            float MaskWeight(float3 sampled, float3 key)
            {
                return 1.0 - smoothstep(0.025, 0.12, distance(sampled, key));
            }

            float GammaToLinearChannel(float value)
            {
                return value <= 0.04045 ? value / 12.92 : pow((value + 0.055) / 1.055, 2.4);
            }

            float3 HalfMaskKey(float3 gammaKey)
            {
                #if defined(UNITY_COLORSPACE_GAMMA)
                    return gammaKey;
                #else
                    return float3(GammaToLinearChannel(gammaKey.r), GammaToLinearChannel(gammaKey.g), GammaToLinearChannel(gammaKey.b));
                #endif
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uvMask = TRANSFORM_TEX(input.uv, _TextureSample0);
                float2 uvBase = TRANSFORM_TEX(input.uv, _TextureSample2);
                float3 mask = SAMPLE_TEXTURE2D(_TextureSample0, sampler_TextureSample0, uvMask).rgb;
                float3 baseColor = SAMPLE_TEXTURE2D(_TextureSample2, sampler_TextureSample2, uvBase).rgb;
                float3 albedo = 0;
                float w;

                w = MaskWeight(mask, HalfMaskKey(float3(0.4980392, 1, 1))); albedo = lerp(albedo, baseColor * _FEATHERS2COLOR.rgb, w);
                w = MaskWeight(mask, HalfMaskKey(float3(0.4980392, 1, 0.4980392))); albedo = lerp(albedo, baseColor * _FEATHERS1COLOR.rgb, w);
                w = MaskWeight(mask, float3(0, 0, 1)); albedo = lerp(albedo, baseColor * _CLOTH4COLOR.rgb, w);
                w = MaskWeight(mask, float3(0, 1, 1)); albedo = lerp(albedo, baseColor * _CLOTH3COLOR.rgb, w);
                w = MaskWeight(mask, float3(0, 1, 0)); albedo = lerp(albedo, baseColor * _CLOTH2COLOR.rgb, w);
                w = MaskWeight(mask, HalfMaskKey(float3(0, 0.4980392, 0))); albedo = lerp(albedo, baseColor * _CLOTH1COLOR.rgb, w);
                w = MaskWeight(mask, HalfMaskKey(float3(1, 0.4980392, 0.4980392))); albedo = lerp(albedo, baseColor * _LEATHER4COLOR.rgb, w);
                w = MaskWeight(mask, HalfMaskKey(float3(1, 1, 0.4980392))); albedo = lerp(albedo, baseColor * _LEATHER3COLOR.rgb, w);
                w = MaskWeight(mask, float3(1, 0, 1)); albedo = lerp(albedo, baseColor * _LEATHER2COLOR.rgb, w);
                w = MaskWeight(mask, HalfMaskKey(float3(1, 0.4980392, 1))); albedo = lerp(albedo, baseColor * _LEATHER1COLOR.rgb, w);
                w = MaskWeight(mask, HalfMaskKey(float3(0.4980392, 0.4980392, 1))); albedo = lerp(albedo, baseColor * _METAL4COLOR.rgb, w);
                w = MaskWeight(mask, HalfMaskKey(float3(0, 0.4980392, 0.4980392))); albedo = lerp(albedo, baseColor * _METAL3COLOR.rgb, w);
                w = MaskWeight(mask, HalfMaskKey(float3(0, 0, 0.4980392))); albedo = lerp(albedo, baseColor * _METAL2COLOR.rgb, w);
                w = MaskWeight(mask, HalfMaskKey(float3(0.4980392, 0, 0.4980392))); albedo = lerp(albedo, baseColor * _METAL1COLOR.rgb, w);
                w = MaskWeight(mask, float3(1, 1, 0)); albedo = lerp(albedo, baseColor * _OTHERCOLOR.rgb, w);
                w = MaskWeight(mask, HalfMaskKey(float3(0.4980392, 0.4980392, 0))); albedo = lerp(albedo, baseColor * _LIPSCOLOR.rgb, w);
                w = MaskWeight(mask, HalfMaskKey(float3(0.4980392, 0.4980392, 0.4980392))); albedo = lerp(albedo, baseColor * _SCLERACOLOR.rgb, w);
                w = MaskWeight(mask, float3(1, 0, 0)); albedo = lerp(albedo, baseColor * _EYESCOLOR.rgb, w);
                w = MaskWeight(mask, HalfMaskKey(float3(1, 0.4980392, 0))); albedo = lerp(albedo, baseColor * _HAIRCOLOR.rgb, w);
                w = MaskWeight(mask, HalfMaskKey(float3(0.4980392, 0, 0))); albedo = lerp(albedo, baseColor * _SKINCOLOR.rgb, w);

                float3 normalWS = normalize(input.normalWS);
                Light mainLight = GetMainLight();
                half ndotl = saturate(dot(normalWS, mainLight.direction));
                half3 lighting = SampleSH(normalWS) + mainLight.color * ndotl;
                half3 color = MixFog(albedo * lighting, input.fog);
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
