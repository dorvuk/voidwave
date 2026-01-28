using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [SerializeField] Animator animator;
    [SerializeField] PlayerHealth playerHealth;
    [SerializeField] Transform modelRoot;
    [SerializeField] TrackRunner trackRunner;
    [SerializeField] bool useRootMotion = false;

    [Header("Surfboard")]
    [SerializeField] GameObject surfboard;
    [SerializeField] float surfboardFadeDuration = 0.15f;
    [SerializeField] bool hideSurfboardInDive = true;
    [SerializeField] bool hideSurfboardInDeath = true;
    [SerializeField] bool hideSurfboardInSwim = true;
    
    [Header("Animation States")]
    public string idleState = "Idle";
    public string diveState = "JumpOff";
    public string surfState = "Surfing";
    public string deathState = "Death";
    public string swimState = "Swimming";
    public string hitState = "GetHit";

    [Header("Animation Triggers")]
    public string diveTrigger = "";
    public string hitTrigger = "getshit";

    [Header("Swim Rotation")]
    public Vector3 swimRotationOffset = new Vector3(-90f, 0f, 0f);

    [Header("Hit Timing")]
    public float hitCooldown = 0.15f;

    Quaternion modelBaseRotation;
    Vector3 modelBaseLocalPosition;
    float lastHitTime;
    Coroutine surfboardFadeRoutine;
    readonly HashSet<string> missingStateWarnings = new HashSet<string>();
    
    void Awake()
    {
        if (!animator) animator = GetComponent<Animator>();
        if (animator) animator.applyRootMotion = useRootMotion;
        if (!modelRoot) modelRoot = animator ? animator.transform : transform;
        CacheModelPose();
    }

    void OnEnable()
    {
        if (!playerHealth) playerHealth = GetComponent<PlayerHealth>();
        if (!playerHealth) playerHealth = FindObjectOfType<PlayerHealth>();
        if (!trackRunner) trackRunner = GetComponent<TrackRunner>();
        if (!trackRunner) trackRunner = FindObjectOfType<TrackRunner>();

        if (playerHealth != null)
        {
            playerHealth.OnDamaged += HandleDamaged;
            playerHealth.OnDeath += HandleDeath;
        }

        if (trackRunner != null)
            trackRunner.ObstacleHit += HandleObstacleHit;
    }

    void OnDisable()
    {
        if (playerHealth != null)
        {
            playerHealth.OnDamaged -= HandleDamaged;
            playerHealth.OnDeath -= HandleDeath;
        }

        if (trackRunner != null)
            trackRunner.ObstacleHit -= HandleObstacleHit;
    }

    void Start()
    {
    }
    
    public void StartGame()
    {
        PlayDiveIn();
    }
    
    public void HitObstacle()
    {
        PlayHit();
    }
    
    public void Die()
    {
        PlayDeath();
    }

    void HandleDamaged()
    {
        TryPlayHit();
    }

    void HandleDeath()
    {
        PlayDeath();
    }

    void HandleObstacleHit(Obstacle _)
    {
        TryPlayHit();
    }

    void TryPlayHit()
    {
        if (hitCooldown > 0f && Time.time - lastHitTime < hitCooldown) return;
        lastHitTime = Time.time;
        PlayHit();
    }

    public void PlayIdle()
    {
        SetModelPose(swim: false);
        PlayState(idleState);
        ApplySurfboardVisibility(show: true);
    }

    public void PlayDiveIn()
    {
        SetModelPose(swim: false);
        if (!TryTrigger(diveTrigger))
            PlayState(diveState);
        if (hideSurfboardInDive)
            ApplySurfboardVisibility(show: false);
    }

    public void PlaySurfing()
    {
        SetModelPose(swim: false);
        PlayState(surfState);
        ApplySurfboardVisibility(show: true);
    }

    public void PlayDeath()
    {
        SetModelPose(swim: false);
        PlayState(deathState);
        if (hideSurfboardInDeath)
            ApplySurfboardVisibility(show: false);
    }

    public void PlaySwimSurface()
    {
        SetModelPose(swim: true);
        PlayState(swimState);
        if (hideSurfboardInSwim)
            ApplySurfboardVisibility(show: false);
    }

    public void PlayHit()
    {
        if (!TryTrigger(hitTrigger))
            PlayState(hitState);
            AudioManager.I.PlaySfx(SfxId.ObstacleHit);
    }

    void CacheModelPose()
    {
        if (!modelRoot) return;
        modelBaseLocalPosition = modelRoot.localPosition;
        modelBaseRotation = modelRoot.localRotation;
    }

    void SetModelPose(bool swim)
    {
        if (!modelRoot) return;
        modelRoot.localPosition = modelBaseLocalPosition;
        modelRoot.localRotation = swim
            ? Quaternion.Euler(swimRotationOffset) * modelBaseRotation
            : modelBaseRotation;
    }

    void PlayState(string stateName)
    {
        if (!animator) return;
        if (string.IsNullOrWhiteSpace(stateName)) return;
        if (TryCrossFadeAnyLayer(stateName)) return;

        if (!stateName.Contains("."))
        {
            int layerCount = animator.layerCount;
            for (int i = 0; i < layerCount; i++)
            {
                string candidate = animator.GetLayerName(i) + "." + stateName;
                if (TryCrossFadeLayer(candidate, i)) return;
            }
        }

        if (!missingStateWarnings.Contains(stateName))
        {
            missingStateWarnings.Add(stateName);
            Debug.LogWarning($"PlayerController: Animator state '{stateName}' not found on {animator.name}.", this);
        }
    }

    bool TryTrigger(string triggerName)
    {
        if (!animator) return false;
        if (string.IsNullOrWhiteSpace(triggerName)) return false;
        if (!HasParameter(triggerName, AnimatorControllerParameterType.Trigger)) return false;
        animator.SetTrigger(triggerName);
        return true;
    }

    void ApplySurfboardVisibility(bool show)
    {
        if (!surfboard) return;

        if (surfboardFadeRoutine != null)
            StopCoroutine(surfboardFadeRoutine);

        surfboardFadeRoutine = StartCoroutine(FadeSurfboard(show));
    }

    IEnumerator FadeSurfboard(bool show)
    {
        var renderers = surfboard.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            surfboard.SetActive(show);
            yield break;
        }

        surfboard.SetActive(true);

        float duration = Mathf.Max(0.01f, surfboardFadeDuration);
        float t = 0f;
        float startAlpha = show ? 0f : 1f;
        float endAlpha = show ? 1f : 0f;

        while (t < duration)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / duration);
            float a = Mathf.Lerp(startAlpha, endAlpha, u);
            SetSurfboardAlpha(renderers, a);
            yield return null;
        }

        SetSurfboardAlpha(renderers, endAlpha);

        if (show)
            ClearSurfboardOverrides(renderers);

        if (!show)
            surfboard.SetActive(false);
    }

    void SetSurfboardAlpha(Renderer[] renderers, float alpha)
    {
        var block = new MaterialPropertyBlock();
        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (!r) continue;

            var mats = r.sharedMaterials;
            for (int m = 0; m < mats.Length; m++)
            {
                var mat = mats[m];
                if (!mat) continue;

                bool hasBaseColor = mat.HasProperty("_BaseColor");
                bool hasColor = mat.HasProperty("_Color");
                if (!hasBaseColor && !hasColor) continue;

                block.Clear();
                r.GetPropertyBlock(block, m);

                if (hasBaseColor)
                {
                    Color c = mat.GetColor("_BaseColor");
                    c.a *= alpha;
                    block.SetColor("_BaseColor", c);
                }

                if (hasColor)
                {
                    Color c = mat.GetColor("_Color");
                    c.a *= alpha;
                    block.SetColor("_Color", c);
                }

                r.SetPropertyBlock(block, m);
            }
        }
    }

    void ClearSurfboardOverrides(Renderer[] renderers)
    {
        var block = new MaterialPropertyBlock();
        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (!r) continue;

            var mats = r.sharedMaterials;
            for (int m = 0; m < mats.Length; m++)
            {
                var mat = mats[m];
                if (!mat) continue;

                bool hasBaseColor = mat.HasProperty("_BaseColor");
                bool hasColor = mat.HasProperty("_Color");
                if (!hasBaseColor && !hasColor) continue;

                block.Clear();
                r.SetPropertyBlock(block, m);
            }
        }
    }

    bool HasParameter(string name, AnimatorControllerParameterType type)
    {
        foreach (var p in animator.parameters)
        {
            if (p.type == type && p.name == name) return true;
        }
        return false;
    }

    bool TryCrossFadeAnyLayer(string stateName)
    {
        int layerCount = animator.layerCount;
        int hash = Animator.StringToHash(stateName);
        for (int i = 0; i < layerCount; i++)
        {
            if (!animator.HasState(i, hash)) continue;
            animator.CrossFade(hash, 0.05f, i);
            return true;
        }
        return false;
    }

    bool TryCrossFadeLayer(string stateName, int layer)
    {
        int hash = Animator.StringToHash(stateName);
        if (!animator.HasState(layer, hash)) return false;
        animator.CrossFade(hash, 0.05f, layer);
        return true;
    }
}
