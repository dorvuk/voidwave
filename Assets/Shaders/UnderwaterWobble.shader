Shader "Hidden/UnderwaterWobble"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Name "UnderwaterPostFx"
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            SAMPLER(sampler_BlitTexture);

            float _UnderwaterBlend;
            float _UnderwaterFogEnabled;
            float4 _UnderwaterFogColor;
            float4 _UnderwaterFogParams; // density, start, end, power
            float4 _UnderwaterDistortionParams; // strength, scale, speed, unused
            float _UnderwaterChromaticShift;
            float _UnderwaterGodRayEnabled;
            float4 _UnderwaterGodRayColor;
            float4 _UnderwaterGodRayParams; // intensity, decay, weight, density
            float4 _UnderwaterGodRayParams2; // samples, speed, unused, unused

            float2 DistortionOffset(float2 uv, float time)
            {
                float strength = _UnderwaterDistortionParams.x;
                float scale = _UnderwaterDistortionParams.y;
                float speed = _UnderwaterDistortionParams.z;

                float2 wave = float2(
                    sin((uv.y * scale) + time * speed),
                    cos((uv.x * scale) - time * speed * 1.1));

                return wave * strength;
            }

            float ComputeFog(float linearDepth)
            {
                float density = _UnderwaterFogParams.x;
                float start = _UnderwaterFogParams.y;
                float end = _UnderwaterFogParams.z;
                float power = _UnderwaterFogParams.w;

                float range = saturate((linearDepth - start) / max(0.001, end - start));
                float expFog = 1.0 - exp(-linearDepth * density);
                float fog = saturate(range * expFog);
                return pow(fog, power);
            }

            float2 GetSunUV(float3 lightDir)
            {
                float3 sunPosWS = _WorldSpaceCameraPos + lightDir * 1000.0;
                float4 sunPosCS = TransformWorldToHClip(sunPosWS);
                float2 sunNdc = sunPosCS.xy / max(sunPosCS.w, 0.0001);
                return sunNdc * 0.5 + 0.5;
            }

            float3 ComputeGodRays(float2 uv, float fogFactor, float time)
            {
                if (_UnderwaterGodRayEnabled <= 0.5)
                    return 0.0;

                Light mainLight = GetMainLight();
                float3 lightDir = -mainLight.direction;
                float2 sunUv = GetSunUV(lightDir);

                float intensity = _UnderwaterGodRayParams.x;
                float decay = _UnderwaterGodRayParams.y;
                float weight = _UnderwaterGodRayParams.z;
                float density = _UnderwaterGodRayParams.w;

                int samples = (int)_UnderwaterGodRayParams2.x;
                float speed = _UnderwaterGodRayParams2.y;

                float2 delta = (sunUv - uv) * (density / max(1, samples));
                float2 coord = uv;
                float illuminationDecay = 1.0;
                float ray = 0.0;

                UNITY_LOOP
                for (int i = 0; i < samples; i++)
                {
                    coord += delta;
                    coord = saturate(coord);

                    float depthSample = SampleSceneDepth(coord);
                    float linearDepth = LinearEyeDepth(depthSample, _ZBufferParams);
                    float occlusion = exp(-linearDepth * _UnderwaterFogParams.x);

                    ray += occlusion * illuminationDecay;
                    illuminationDecay *= decay;
                }

                float shimmer = lerp(0.85, 1.15, sin((uv.x + uv.y + time * speed) * 6.2831) * 0.5 + 0.5);
                float scatter = ray * weight * intensity * shimmer;
                return _UnderwaterGodRayColor.rgb * scatter * fogFactor;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float fade = saturate(_UnderwaterBlend);
                float2 uv = UnityStereoTransformScreenSpaceTex(i.texcoord);

                if (fade <= 0.001)
                    return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_BlitTexture, uv);

                float time = _Time.y;
                float2 offset = DistortionOffset(uv, time) * fade;
                float2 distortedUV = uv + offset;

                float3 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_BlitTexture, distortedUV).rgb;

                float chroma = _UnderwaterChromaticShift * fade;
                if (chroma > 0.0001)
                {
                    float2 shift = normalize(offset + 0.0001) * chroma;
                    float r = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_BlitTexture, distortedUV + shift).r;
                    float g = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_BlitTexture, distortedUV).g;
                    float b = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_BlitTexture, distortedUV - shift).b;
                    color = float3(r, g, b);
                }

                float depth = SampleSceneDepth(uv);
                float linearDepth = LinearEyeDepth(depth, _ZBufferParams);

                float fogFactor = 0.0;
                if (_UnderwaterFogEnabled > 0.5)
                {
                    fogFactor = ComputeFog(linearDepth) * fade;
                    color = lerp(color, _UnderwaterFogColor.rgb, fogFactor);
                }

                float3 rays = ComputeGodRays(uv, max(fogFactor, fade * 0.25), time) * fade;
                color += rays;

                return float4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
