using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

public class UnderwaterManager : MonoBehaviour
{
    [Header("Water")]
    public float waterY = 100f;
    public float enterOffset = 0f;

    [Header("Transition")]
    public float transitionSpeed = 4f;

    [Header("Underwater Post FX (URP)")]
    [FormerlySerializedAs("controlWobble")]
    public bool controlPostFx = true;
    [FormerlySerializedAs("wobbleFadeMultiplier")]
    public float postFxFadeMultiplier = 1f;

    [Header("Fog (Post FX)")]
    public bool controlFog = true;
    public bool underwaterFogEnabled = true;
    public Color underwaterFogColor = new Color(0.05f, 0.35f, 0.45f, 1f);
    [Range(0f, 0.2f)] public float underwaterFogDensity = 0.03f;
    [Min(0f)] public float underwaterFogStartDistance = 0f;
    [Min(0f)] public float underwaterFogEndDistance = 120f;
    [Range(0.1f, 4f)] public float underwaterFogPower = 1f;

    [Header("Distortion")]
    [Range(0f, 0.05f)] public float distortionStrength = 0.009f;
    [Range(1f, 50f)] public float distortionScale = 12f;
    [Range(0f, 10f)] public float distortionSpeed = 1.5f;
    [Range(0f, 1f)] public float distortionDetail = 0.35f;
    [Range(0f, 0.01f)] public float chromaticShift = 0.002f;

    [Header("God Rays")]
    public bool godRaysEnabled = true;
    public Color godRayColor = new Color(0.6f, 0.85f, 1f, 1f);
    [Range(0f, 5f)] public float godRayIntensity = 0.8f;
    [Range(0f, 1f)] public float godRayDecay = 0.94f;
    [Range(0f, 1f)] public float godRayWeight = 0.35f;
    [Range(0f, 2f)] public float godRayDensity = 0.9f;
    [Range(0f, 2f)] public float godRaySpeed = 0.25f;
    [Range(1, 32)] public int godRaySamples = 12;

    [Header("Ambient")]
    public bool controlAmbient = true;
    public Color aboveAmbient = Color.white;
    public Color underwaterAmbient = new Color(0.12f, 0.2f, 0.25f, 1f);

    [Header("Directional Light (optional)")]
    public bool controlMainLight = false;
    public Light mainDirectionalLight;
    public float aboveLightIntensity = 1f;
    public float underwaterLightIntensity = 0.6f;

    [Header("Audio")]
    public bool controlAudioLowpass = true;
    public float aboveCutoff = 22000f;
    public float underwaterCutoff = 900f;

    float blend;
    bool isUnderwater;
    bool wasUnderwater;
    Camera activeCamera;
    AudioLowPassFilter lowpass;

    bool ambientCached;
    bool lightCached;
    bool audioCached;

    Color cachedAmbient;
    float cachedMainLightIntensity;

    AudioLowPassFilter cachedLowpass;
    float cachedLowpassCutoff;
    bool cachedLowpassEnabled;
    bool cachedLowpassWasAdded;

    static readonly int UnderwaterBlendId = Shader.PropertyToID("_UnderwaterBlend");
    static readonly int UnderwaterFogEnabledId = Shader.PropertyToID("_UnderwaterFogEnabled");
    static readonly int UnderwaterFogColorId = Shader.PropertyToID("_UnderwaterFogColor");
    static readonly int UnderwaterFogParamsId = Shader.PropertyToID("_UnderwaterFogParams");
    static readonly int UnderwaterDistortionParamsId = Shader.PropertyToID("_UnderwaterDistortionParams");
    static readonly int UnderwaterChromaticShiftId = Shader.PropertyToID("_UnderwaterChromaticShift");
    static readonly int UnderwaterGodRayEnabledId = Shader.PropertyToID("_UnderwaterGodRayEnabled");
    static readonly int UnderwaterGodRayColorId = Shader.PropertyToID("_UnderwaterGodRayColor");
    static readonly int UnderwaterGodRayParamsId = Shader.PropertyToID("_UnderwaterGodRayParams");
    static readonly int UnderwaterGodRayParams2Id = Shader.PropertyToID("_UnderwaterGodRayParams2");

    void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
    }

    void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
        RestoreAboveValues();
        wasUnderwater = false;
    }

    void OnBeginCameraRendering(ScriptableRenderContext context, Camera cam)
    {
        if (cam.cameraType != CameraType.Game) return;

        activeCamera = cam;
        UpdateBlend();
        UpdateShaderGlobals();

        if (isUnderwater)
        {
            if (!wasUnderwater)
                CacheAboveValues(cam);

            ApplyEffects();
            SetupAudio(cam);
        }
        else if (wasUnderwater)
        {
            RestoreAboveValues();
        }

        wasUnderwater = isUnderwater;
    }

    void UpdateBlend()
    {
        float threshold = waterY + enterOffset;
        isUnderwater = activeCamera.transform.position.y < threshold;

        float target = isUnderwater ? 1f : 0f;
        blend = Mathf.MoveTowards(blend, target, transitionSpeed * Time.deltaTime);
    }

    void UpdateShaderGlobals()
    {
        float postBlend = 0f;
        if (controlPostFx && isUnderwater)
            postBlend = Mathf.Clamp01(blend * postFxFadeMultiplier);

        Shader.SetGlobalFloat(UnderwaterBlendId, postBlend);

        float fogEnabled = (controlFog && underwaterFogEnabled) ? 1f : 0f;
        float fogEnd = Mathf.Max(underwaterFogEndDistance, underwaterFogStartDistance + 0.01f);
        Shader.SetGlobalFloat(UnderwaterFogEnabledId, fogEnabled);
        Shader.SetGlobalColor(UnderwaterFogColorId, underwaterFogColor);
        Shader.SetGlobalVector(
            UnderwaterFogParamsId,
            new Vector4(underwaterFogDensity, underwaterFogStartDistance, fogEnd, underwaterFogPower));

        Shader.SetGlobalVector(
            UnderwaterDistortionParamsId,
            new Vector4(distortionStrength, distortionScale, distortionSpeed, distortionDetail));
        Shader.SetGlobalFloat(UnderwaterChromaticShiftId, chromaticShift);

        float raysEnabled = godRaysEnabled ? 1f : 0f;
        Shader.SetGlobalFloat(UnderwaterGodRayEnabledId, raysEnabled);
        Shader.SetGlobalColor(UnderwaterGodRayColorId, godRayColor);
        Shader.SetGlobalVector(
            UnderwaterGodRayParamsId,
            new Vector4(godRayIntensity, godRayDecay, godRayWeight, godRayDensity));
        Shader.SetGlobalVector(
            UnderwaterGodRayParams2Id,
            new Vector4(Mathf.Clamp(godRaySamples, 1, 32), godRaySpeed, 0f, 0f));
    }

    void CacheAboveValues(Camera cam)
    {
        ambientCached = false;
        lightCached = false;
        audioCached = false;

        cachedLowpass = null;
        cachedLowpassWasAdded = false;

        if (controlAmbient)
        {
            ambientCached = true;
            cachedAmbient = RenderSettings.ambientLight;
        }

        if (controlMainLight && mainDirectionalLight != null)
        {
            lightCached = true;
            cachedMainLightIntensity = mainDirectionalLight.intensity;
        }

        if (controlAudioLowpass)
        {
            audioCached = true;
            cachedLowpass = cam.GetComponent<AudioLowPassFilter>();
            if (cachedLowpass != null)
            {
                cachedLowpassWasAdded = false;
                cachedLowpassCutoff = cachedLowpass.cutoffFrequency;
                cachedLowpassEnabled = cachedLowpass.enabled;
            }
            else
            {
                cachedLowpassWasAdded = true;
                cachedLowpassCutoff = aboveCutoff;
                cachedLowpassEnabled = true;
            }
        }
    }

    void RestoreAboveValues()
    {
        if (controlAmbient && ambientCached)
            RenderSettings.ambientLight = cachedAmbient;

        if (controlMainLight && lightCached && mainDirectionalLight != null)
            mainDirectionalLight.intensity = cachedMainLightIntensity;

        if (controlAudioLowpass && audioCached)
        {
            if (cachedLowpassWasAdded)
            {
                if (lowpass != null)
                {
                    Destroy(lowpass);
                    lowpass = null;
                }
            }
            else if (cachedLowpass != null)
            {
                cachedLowpass.cutoffFrequency = cachedLowpassCutoff;
                cachedLowpass.enabled = cachedLowpassEnabled;
                lowpass = cachedLowpass;
            }
        }

        ambientCached = false;
        lightCached = false;
        audioCached = false;
        cachedLowpass = null;
    }

    void ApplyEffects()
    {
        if (controlAmbient)
        {
            Color from = ambientCached ? cachedAmbient : RenderSettings.ambientLight;
            RenderSettings.ambientLight = Color.Lerp(from, underwaterAmbient, blend);
        }

        if (controlMainLight && mainDirectionalLight != null)
        {
            float from = lightCached ? cachedMainLightIntensity : mainDirectionalLight.intensity;
            mainDirectionalLight.intensity =
                Mathf.Lerp(from, underwaterLightIntensity, blend);
        }
    }

    void SetupAudio(Camera cam)
    {
        if (!controlAudioLowpass) return;

        if (lowpass == null || lowpass.gameObject != cam.gameObject)
        {
            lowpass = cam.GetComponent<AudioLowPassFilter>();
            if (lowpass == null)
                lowpass = cam.gameObject.AddComponent<AudioLowPassFilter>();
        }

        lowpass.enabled = true;

        float from = audioCached ? cachedLowpassCutoff : aboveCutoff;
        lowpass.cutoffFrequency =
            Mathf.Lerp(from, underwaterCutoff, blend);
    }
}
