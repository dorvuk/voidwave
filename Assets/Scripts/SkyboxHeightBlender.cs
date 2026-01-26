using UnityEngine;

public class SkyboxHeightBlender : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform heightSource;

    [Header("Skybox Material")]
    [SerializeField] private Material blendedSkyboxMaterial;
    [SerializeField] private string blendProperty = "_Blend";

    [Header("Height Range")]
    [SerializeField] private float startY = 0f;   // blend = 0 at/below this
    [SerializeField] private float endY = 100f;   // blend = 1 at/above this

    [Header("Smoothing")]
    [SerializeField] private float smoothTime = 0.6f;

    [Header("Reflection / GI Updates")]
    [SerializeField] private bool updateEnvironmentLighting = true;
    [SerializeField] private float giUpdateThreshold = 0.01f; // update only when blend changes enough

    private float _currentBlend;
    private float _blendVelocity;
    private float _lastGIUpdateBlend;

    private void Reset()
    {
        blendedSkyboxMaterial = RenderSettings.skybox;
    }

    private void OnEnable()
    {
        if (blendedSkyboxMaterial != null)
            RenderSettings.skybox = blendedSkyboxMaterial;
    }

    private void Update()
    {
        if (blendedSkyboxMaterial == null || heightSource == null) return;

        float y = heightSource.position.y;

        float targetBlend = Mathf.InverseLerp(startY, endY, y);
        _currentBlend = Mathf.SmoothDamp(_currentBlend, targetBlend, ref _blendVelocity, smoothTime);

        blendedSkyboxMaterial.SetFloat(blendProperty, _currentBlend);

        if (updateEnvironmentLighting && Mathf.Abs(_currentBlend - _lastGIUpdateBlend) >= giUpdateThreshold)
        {
            _lastGIUpdateBlend = _currentBlend;
            DynamicGI.UpdateEnvironment();
        }
    }
    
    public void SetHeightSource(Transform newSource)
    {
        heightSource = newSource;
    }
}
