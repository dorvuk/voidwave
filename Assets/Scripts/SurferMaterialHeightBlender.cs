using System.Collections.Generic;
using UnityEngine;

public class SurferMaterialHeightBlender : MonoBehaviour
{
    [System.Serializable]
    public class BlendSlot
    {
        public Renderer renderer;
        [Min(0)] public int materialIndex = 0;
        [Tooltip("Material used at/below startY.")]
        public Material lowMaterial;
        [Tooltip("Material used at/above endY.")]
        public Material highMaterial;
    }

    [Header("Target")]
    [SerializeField] Transform heightSource;

    [Header("Height Range")]
    [SerializeField] float startY = 0f;
    [SerializeField] float endY = 100f;

    [Header("Smoothing")]
    [SerializeField] float smoothTime = 0.35f;

    [Header("Material Slots")]
    [SerializeField] BlendSlot[] slots;

    float currentBlend;
    float blendVelocity;

    class RuntimeSlot
    {
        public Renderer renderer;
        public int materialIndex;
        public Material runtimeMaterial;
        public Material lowMaterial;
        public Material highMaterial;
    }

    readonly List<RuntimeSlot> runtimeSlots = new List<RuntimeSlot>();
    readonly HashSet<Renderer> warnedRenderers = new HashSet<Renderer>();

    void Awake()
    {
        if (!heightSource) heightSource = transform;
        BuildRuntimeSlots();
    }

    void OnEnable()
    {
        if (runtimeSlots.Count == 0)
            BuildRuntimeSlots();
    }

    void OnDestroy()
    {
        for (int i = 0; i < runtimeSlots.Count; i++)
        {
            var rt = runtimeSlots[i].runtimeMaterial;
            if (rt) Destroy(rt);
        }
        runtimeSlots.Clear();
    }

    void Update()
    {
        if (heightSource == null || runtimeSlots.Count == 0) return;

        float targetBlend = Mathf.InverseLerp(startY, endY, heightSource.position.y);
        currentBlend = Mathf.SmoothDamp(currentBlend, targetBlend, ref blendVelocity, smoothTime);

        for (int i = 0; i < runtimeSlots.Count; i++)
        {
            var s = runtimeSlots[i];
            if (!s.runtimeMaterial || !s.lowMaterial || !s.highMaterial) continue;
            s.runtimeMaterial.Lerp(s.lowMaterial, s.highMaterial, currentBlend);
        }
    }

    void BuildRuntimeSlots()
    {
        runtimeSlots.Clear();

        if (slots == null || slots.Length == 0) return;

        for (int i = 0; i < slots.Length; i++)
        {
            var slot = slots[i];
            if (slot.renderer == null || slot.lowMaterial == null || slot.highMaterial == null)
                continue;

            var materials = slot.renderer.sharedMaterials;
            if (slot.materialIndex < 0 || slot.materialIndex >= materials.Length)
            {
                if (!warnedRenderers.Contains(slot.renderer))
                {
                    warnedRenderers.Add(slot.renderer);
                    Debug.LogWarning($"SurferMaterialHeightBlender: material index {slot.materialIndex} is out of range on {slot.renderer.name}.", slot.renderer);
                }
                continue;
            }

            var runtimeMat = new Material(slot.lowMaterial);
            materials[slot.materialIndex] = runtimeMat;
            slot.renderer.sharedMaterials = materials;

            runtimeSlots.Add(new RuntimeSlot
            {
                renderer = slot.renderer,
                materialIndex = slot.materialIndex,
                runtimeMaterial = runtimeMat,
                lowMaterial = slot.lowMaterial,
                highMaterial = slot.highMaterial
            });
        }
    }
}
