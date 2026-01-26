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

            float Hash12(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float Noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);

                float a = Hash12(i);
                float b = Hash12(i + float2(1, 0));
                float c = Hash12(i + float2(0, 1));
                float d = Hash12(i + float2(1, 1));

                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

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

                float noiseMod = lerp(0.75, 1.25, Noise(uv * 6.0 + time * speed));
                float scatter = ray * weight * intensity * noiseMod;
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
                float2 distortedUV = saturate(uv + offset);

                float3 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_BlitTexture, distortedUV).rgb;

                float chroma = _UnderwaterChromaticShift * fade;
                if (chroma > 0.0001)
                {
                    float2 shiftDir = normalize(offset + 0.0001);
                    float2 shift = shiftDir * chroma;

                    float2 uvR = saturate(distortedUV + shift);
                    float2 uvG = distortedUV;
                    float2 uvB = saturate(distortedUV - shift);

                    float r = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_BlitTexture, uvR).r;
                    float g = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_BlitTexture, uvG).g;
                    float b = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_BlitTexture, uvB).b;
                    color = float3(r, g, b);
                }

                float depth = SampleSceneDepth(uv);
                float linearDepth = LinearEyeDepth(depth, _ZBufferParams);

                float fogFactor = 0.0;
                if (_UnderwaterFogEnabled > 0.5)
                {
                    fogFactor = ComputeFog(linearDepth) * fade;
                    float3 fogColor = _UnderwaterFogColor.rgb;
                    color = 1.0 - (1.0 - color) * (1.0 - fogColor * fogFactor);
                }

                float3 rays = ComputeGodRays(uv, max(fogFactor, fade * 0.25), time) * fade;
                color += rays;

                return float4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
