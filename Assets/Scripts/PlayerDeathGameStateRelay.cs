using UnityEngine;

/// <summary>
/// Bridges PlayerHealth.OnDeath to GameStateManager.OnPlayerDied for easy inspector wiring.
/// </summary>
public class PlayerDeathGameStateRelay : MonoBehaviour
{
    [SerializeField] PlayerHealth health;
    [SerializeField] GameStateManager gameStateManager;

    void OnEnable()
    {
        if (!health) health = GetComponent<PlayerHealth>();
        if (health) health.OnDeath += HandleDeath;
        else Debug.LogWarning("PlayerDeathGameStateRelay: No PlayerHealth assigned.", this);
    }

    void OnDisable()
    {
        if (health) health.OnDeath -= HandleDeath;
    }

    void HandleDeath()
    {
        if (gameStateManager) gameStateManager.OnPlayerDied();
        else Debug.LogWarning("PlayerDeathGameStateRelay: No GameStateManager assigned.", this);
    }
}
