using UnityEngine;

[DisallowMultipleComponent]
public class UnderwaterWobbleEffect : MonoBehaviour
{
    [Header("Shader")]
    public Shader wobbleShader;

    [Header("Look")]
    [Range(0f, 0.05f)] public float strength = 0.009f;
    [Range(1f, 50f)] public float scale = 12f;
    [Range(0f, 10f)] public float speed = 1.5f;
    [Range(0f, 0.01f)] public float colorShift = 0f;

    [Header("Runtime")]
    [Range(0f, 1f)] public float fade = 0f; // manager drives this (0 above, 1 underwater)

    Material mat;

    void OnEnable()
    {
        if (wobbleShader == null)
            wobbleShader = Shader.Find("Hidden/UnderwaterWobble");

        if (wobbleShader == null)
        {
            enabled = false;
            return;
        }

        if (mat == null)
        {
            mat = new Material(wobbleShader);
            mat.hideFlags = HideFlags.HideAndDontSave;
        }
    }

    void OnDisable()
    {
        if (mat != null)
        {
            DestroyImmediate(mat);
            mat = null;
        }
    }

    void OnRenderImage(RenderTexture src, RenderTexture dest)
    {
        if (mat == null || fade <= 0.001f)
        {
            Graphics.Blit(src, dest);
            return;
        }

        mat.SetFloat("_Strength", strength);
        mat.SetFloat("_Scale", scale);
        mat.SetFloat("_Speed", speed);
        mat.SetFloat("_ColorShift", colorShift);
        mat.SetFloat("_Fade", fade);

        Graphics.Blit(src, dest, mat);
    }
}
