using UnityEngine;
using UnityEngine.Rendering.Universal;

public class UnderwaterManager : MonoBehaviour
{
    [Header("Water")]
    public float waterY = 100f;
    public float enterOffset = 0f;

    [Header("Height Source")]
    [SerializeField] Transform heightSource;

    [Header("Renderer Feature")]
    [Tooltip("Assign the URP renderer feature named 'Underwater Effects' from the pipeline renderer asset.")]
    [SerializeField] ScriptableRendererFeature underwaterRendererFeature;
    bool isUnderwater;

    void OnEnable()
    {
        ResolveHeightSource();
    }

    void Update()
    {
        UpdateBlend();
        UpdateRendererFeature();
    }

    void UpdateBlend()
    {
        if (!heightSource)
        {
            isUnderwater = false;
            return;
        }

        float threshold = waterY + enterOffset;
        isUnderwater = heightSource.position.y < threshold;
    }

    void UpdateRendererFeature()
    {
        if (underwaterRendererFeature == null)
            return;

        if (underwaterRendererFeature.isActive != isUnderwater)
            underwaterRendererFeature.SetActive(isUnderwater);
    }

    void ResolveHeightSource()
    {
        if (!heightSource)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player) heightSource = player.transform;
        }
    }

    public void SetHeightSource(Transform source)
    {
        heightSource = source;
    }
}
