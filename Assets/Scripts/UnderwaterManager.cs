using UnityEngine;

public class UnderwaterManager : MonoBehaviour
{
    [Header("Water")]
    public float waterY = 100f;
    public float enterOffset = 0f;

    [Header("Transition")]
    public float transitionSpeed = 4f;

    [Header("Fog")]
    public bool controlFog = true;

    public bool underwaterFogEnabled = true;
    public Color underwaterFogColor = new Color(0.05f, 0.35f, 0.45f, 1f);
    [Range(0f, 0.1f)] public float underwaterFogDensity = 0.03f;

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

    [Header("Underwater Wobble (screen distortion)")]
    public bool controlWobble = true;
    public float wobbleFadeMultiplier = 1f;

    float blend;
    bool isUnderwater;
    bool wasUnderwater;
    Camera activeCamera;
    AudioLowPassFilter lowpass;

    bool fogCached;
    bool ambientCached;
    bool lightCached;
    bool audioCached;
    bool wobbleCached;

    bool cachedFog;
    Color cachedFogColor;
    float cachedFogDensity;
    FogMode cachedFogMode;
    Color cachedAmbient;
    float cachedMainLightIntensity;

    AudioLowPassFilter cachedLowpass;
    float cachedLowpassCutoff;
    bool cachedLowpassEnabled;
    bool cachedLowpassWasAdded;

    UnderwaterWobbleEffect cachedWobble;
    float cachedWobbleFade;
    bool cachedWobbleWasAdded;
    Camera cachedCamera;

    // original scene values
    bool startFog;
    Color startFogColor;
    float startFogDensity;
    Color startAmbient;
    FogMode startFogMode;

    void OnEnable()
    {
        CacheStartValues();
        Camera.onPreRender += OnCameraPreRender;
    }

    void OnDisable()
    {
        Camera.onPreRender -= OnCameraPreRender;
        RestoreAboveValues();
        RestoreStartValues();
        wasUnderwater = false;
    }

    void CacheStartValues()
    {
        startFog = RenderSettings.fog;
        startFogColor = RenderSettings.fogColor;
        startFogDensity = RenderSettings.fogDensity;
        startAmbient = RenderSettings.ambientLight;
        startFogMode = RenderSettings.fogMode;
    }

    void RestoreStartValues()
    {
        RenderSettings.fog = startFog;
        RenderSettings.fogColor = startFogColor;
        RenderSettings.fogDensity = startFogDensity;
        RenderSettings.ambientLight = startAmbient;
        RenderSettings.fogMode = startFogMode;
    }

    void CacheAboveValues(Camera cam)
    {
        cachedCamera = cam;

        fogCached = false;
        ambientCached = false;
        lightCached = false;
        audioCached = false;
        wobbleCached = false;

        cachedLowpass = null;
        cachedWobble = null;
        cachedLowpassWasAdded = false;
        cachedWobbleWasAdded = false;

        if (controlFog)
        {
            fogCached = true;
            cachedFog = RenderSettings.fog;
            cachedFogColor = RenderSettings.fogColor;
            cachedFogDensity = RenderSettings.fogDensity;
            cachedFogMode = RenderSettings.fogMode;
        }

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

        if (controlWobble)
        {
            wobbleCached = true;
            cachedWobble = cam.GetComponent<UnderwaterWobbleEffect>();
            if (cachedWobble != null)
            {
                cachedWobbleWasAdded = false;
                cachedWobbleFade = cachedWobble.fade;
            }
            else
            {
                cachedWobbleWasAdded = true;
                cachedWobbleFade = 0f;
            }
        }
    }

    void RestoreAboveValues()
    {
        if (controlFog && fogCached)
        {
            RenderSettings.fog = cachedFog;
            RenderSettings.fogColor = cachedFogColor;
            RenderSettings.fogDensity = cachedFogDensity;
            RenderSettings.fogMode = cachedFogMode;
        }

        if (controlAmbient && ambientCached)
        {
            RenderSettings.ambientLight = cachedAmbient;
        }

        if (controlMainLight && lightCached && mainDirectionalLight != null)
        {
            mainDirectionalLight.intensity = cachedMainLightIntensity;
        }

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

        if (controlWobble && wobbleCached)
        {
            if (cachedWobbleWasAdded)
            {
                if (cachedCamera != null)
                {
                    var wobble = cachedCamera.GetComponent<UnderwaterWobbleEffect>();
                    if (wobble != null)
                        Destroy(wobble);
                }
            }
            else if (cachedWobble != null)
            {
                cachedWobble.fade = cachedWobbleFade;
            }
        }

        fogCached = false;
        ambientCached = false;
        lightCached = false;
        audioCached = false;
        wobbleCached = false;
        cachedLowpass = null;
        cachedWobble = null;
        cachedCamera = null;
    }

    void OnCameraPreRender(Camera cam)
    {
        if (cam.cameraType != CameraType.Game) return;

        activeCamera = cam;
        UpdateBlend();

        if (isUnderwater)
        {
            if (!wasUnderwater)
                CacheAboveValues(cam);

            ApplyEffects();
            SetupAudio(cam);
            SetupWobble(cam);
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

    void ApplyEffects()
    {
        if (controlFog)
        {
            if (underwaterFogEnabled)
            {
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.Exponential;
                RenderSettings.fogColor = underwaterFogColor;
                RenderSettings.fogDensity = Mathf.Lerp(0f, underwaterFogDensity, blend);
            }
            else
            {
                RenderSettings.fog = false;
            }
        }

        if (controlAmbient)
        {
            Color from = ambientCached ? cachedAmbient : RenderSettings.ambientLight;
            RenderSettings.ambientLight =
                Color.Lerp(from, underwaterAmbient, blend);
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

    void SetupWobble(Camera cam)
    {
        if (!controlWobble) return;

        var wobble = cam.GetComponent<UnderwaterWobbleEffect>();
        if (wobble == null)
            wobble = cam.gameObject.AddComponent<UnderwaterWobbleEffect>();

        wobble.fade = Mathf.Clamp01(blend * wobbleFadeMultiplier);
    }
}
