using UnityEngine;

public class DeathRelay : MonoBehaviour
{
    public GameStateManager gameStateManager;

    public void Die()
    {
        if (gameStateManager)
        {
            gameStateManager.OnPlayerDied();
        }
        else
        {
            Debug.LogWarning("DeathRelay has no GameStateManager assigned.", this);
        }
    }
}
