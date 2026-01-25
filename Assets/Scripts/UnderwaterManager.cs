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

    [Header("Post FX")]
    [FormerlySerializedAs("controlPostFx")]
    [FormerlySerializedAs("controlWobble")]
    public bool effectsEnabled = true;
    [FormerlySerializedAs("postFxFadeMultiplier")]
    [FormerlySerializedAs("wobbleFadeMultiplier")]
    [Range(0f, 1.5f)] public float effectIntensity = 1f;

    [Header("Fog (distance)")]
    public bool fogEnabled = true;
    [FormerlySerializedAs("underwaterFogColor")]
    public Color fogColor = new Color(0.05f, 0.35f, 0.45f, 1f);
    [FormerlySerializedAs("underwaterFogDensity")]
    [Range(0f, 4f)] public float fogDensity = 0.04f;
    [FormerlySerializedAs("underwaterFogStartDistance")]
    [Min(0f)] public float fogStartDistance = 2f;
    [FormerlySerializedAs("underwaterFogEndDistance")]
    [Min(0.01f)] public float fogEndDistance = 60f;
    [FormerlySerializedAs("underwaterFogPower")]
    [Range(0.1f, 4f)] public float fogPower = 1.1f;

    [Header("Distortion")]
    [Range(0f, 0.02f)] public float distortionStrength = 0.0025f;
    [Range(1f, 30f)] public float distortionScale = 6f;
    [Range(0f, 5f)] public float distortionSpeed = 0.5f;
    [Range(0f, 0.005f)] public float chromaticShift = 0.0005f;

    [Header("God Rays")]
    public bool godRaysEnabled = true;
    public Color godRayColor = new Color(0.7f, 0.9f, 1f, 1f);
    [Range(0f, 2f)] public float godRayIntensity = 0.6f;
    [Range(0f, 1f)] public float godRayDecay = 0.95f;
    [Range(0f, 1f)] public float godRayWeight = 0.3f;
    [Range(0f, 2f)] public float godRayDensity = 0.8f;
    [Range(0f, 2f)] public float godRaySpeed = 0.2f;
    [Range(4, 32)] public int godRaySamples = 12;

    float blend;
    bool isUnderwater;
    Camera activeCamera;

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
        Shader.SetGlobalFloat(UnderwaterBlendId, 0f);
        Shader.SetGlobalFloat(UnderwaterFogEnabledId, 0f);
        Shader.SetGlobalFloat(UnderwaterGodRayEnabledId, 0f);
    }

    void OnBeginCameraRendering(ScriptableRenderContext context, Camera cam)
    {
        if (cam.cameraType != CameraType.Game) return;

        activeCamera = cam;
        UpdateBlend();
        UpdateShaderGlobals();
    }

    void UpdateBlend()
    {
        if (!activeCamera)
        {
            blend = 0f;
            isUnderwater = false;
            return;
        }

        float threshold = waterY + enterOffset;
        isUnderwater = activeCamera.transform.position.y < threshold;
        float target = isUnderwater ? 1f : 0f;
        blend = Mathf.MoveTowards(blend, target, transitionSpeed * Time.deltaTime);
    }

    void UpdateShaderGlobals()
    {
        float effectBlend = (effectsEnabled && isUnderwater)
            ? Mathf.Clamp01(blend * effectIntensity)
            : 0f;
        Shader.SetGlobalFloat(UnderwaterBlendId, effectBlend);

        Shader.SetGlobalFloat(UnderwaterFogEnabledId, (fogEnabled && isUnderwater) ? 1f : 0f);
        Shader.SetGlobalColor(UnderwaterFogColorId, fogColor);

        float fogEnd = Mathf.Max(fogEndDistance, fogStartDistance + 0.01f);
        Shader.SetGlobalVector(
            UnderwaterFogParamsId,
            new Vector4(fogDensity, fogStartDistance, fogEnd, fogPower));

        Shader.SetGlobalVector(
            UnderwaterDistortionParamsId,
            new Vector4(distortionStrength, distortionScale, distortionSpeed, 0f));
        Shader.SetGlobalFloat(UnderwaterChromaticShiftId, chromaticShift);

        Shader.SetGlobalFloat(UnderwaterGodRayEnabledId, (godRaysEnabled && isUnderwater) ? 1f : 0f);
        Shader.SetGlobalColor(UnderwaterGodRayColorId, godRayColor);
        Shader.SetGlobalVector(
            UnderwaterGodRayParamsId,
            new Vector4(godRayIntensity, godRayDecay, godRayWeight, godRayDensity));
        Shader.SetGlobalVector(
            UnderwaterGodRayParams2Id,
            new Vector4(Mathf.Clamp(godRaySamples, 4, 32), godRaySpeed, 0f, 0f));
    }
}
