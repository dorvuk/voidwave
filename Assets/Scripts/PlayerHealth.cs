using System;
using System.Collections;
using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    public int maxHealth = 2;
    public int currentHealth = 2;

    public float invulnerabilityDuration = 1.0f;
    public float regenDelay = 1.5f;
    public float regenDuration = 3.5f;

    public event Action<int, int, float> OnHealthChanged;
    public event Action OnDamaged;
    public event Action OnDeath;
    public event Action OnRegenStarted;
    public event Action OnRegenCompleted;

    public float RegenProgress { get; private set; }
    public bool IsInvulnerable => Time.time < invulnerableUntil;

    TrackRunner runner;
    Coroutine regenRoutine;
    float invulnerableUntil;

    void Awake()
    {
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        runner = GetComponent<TrackRunner>();
    }

    void Start()
    {
        OnHealthChanged?.Invoke(currentHealth, maxHealth, 0f);
    }

    void OnEnable()
    {
        if (!runner) runner = GetComponent<TrackRunner>();
        if (runner) runner.ObstacleHit += HandleObstacleHit;
    }

    void OnDisable()
    {
        if (runner) runner.ObstacleHit -= HandleObstacleHit;
    }

    void HandleObstacleHit(Obstacle _)
    {
        TakeDamage();
    }

    public void TakeDamage(int amount = 1)
    {
        if (amount <= 0) return;
        if (IsInvulnerable) return;
        if (currentHealth <= 0) return;

        currentHealth = Mathf.Max(0, currentHealth - amount);
        invulnerableUntil = Time.time + invulnerabilityDuration;
        RegenProgress = 0f;

        OnDamaged?.Invoke();
        OnHealthChanged?.Invoke(currentHealth, maxHealth, RegenProgress);

        if (currentHealth <= 0)
        {
            OnDeath?.Invoke();
        }
        else
        {
            RestartRegen();
        }
    }

    void RestartRegen()
    {
        if (regenRoutine != null) StopCoroutine(regenRoutine);
        regenRoutine = StartCoroutine(Regen());
    }

    IEnumerator Regen()
    {
        OnRegenStarted?.Invoke();
        float startDelay = Mathf.Max(0f, regenDelay);
        if (startDelay > 0f) yield return new WaitForSeconds(startDelay);

        float duration = Mathf.Max(0.001f, regenDuration);
        float t = 0f;
        while (t < duration && currentHealth < maxHealth)
        {
            t += Time.deltaTime;
            RegenProgress = Mathf.Clamp01(t / duration);
            OnHealthChanged?.Invoke(currentHealth, maxHealth, RegenProgress);
            yield return null;
        }

        if (currentHealth < maxHealth)
        {
            currentHealth = Mathf.Min(maxHealth, currentHealth + 1);
            OnHealthChanged?.Invoke(currentHealth, maxHealth, 0f);
            OnRegenCompleted?.Invoke();
        }

        RegenProgress = 0f;
        regenRoutine = null;
    }
}
