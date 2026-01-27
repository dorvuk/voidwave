using UnityEngine;

public class GameplaySimulator : MonoBehaviour
{
    public ScoreManager scoreManager;
    public UIManager uiManager;

    private bool paused;
    private bool gameplayActive;

    void Start()
    {
        paused = false;
        gameplayActive = false;
    }

    void Update()
    {
        // if (!gameplayActive)
        //     return;

        // if (Input.GetKeyDown(KeyCode.A))
        //     SimulatePickup();

        // if (Input.GetKeyDown(KeyCode.S))
        //     SimulateMiss();

        // if (Input.GetKeyDown(KeyCode.D))
        //     SimulateDeath();

        // if (Input.GetKeyDown(KeyCode.F))
        //     TogglePause();
    }

    // Hooks called by UI

    public void BeginGameplay()
    {
        gameplayActive = true;

        if (paused)
            ResumeFromPause();
        else
            Time.timeScale = 1f;

        scoreManager.ResetAllScores();
        scoreManager.StartScoring();
    }

    public void EndGameplay()
    {
        gameplayActive = false;

        if (paused)
            ResumeFromPause();

        scoreManager.StopScoring();
    }

    // Simulated events

    void SimulatePickup()
    {
        if (paused) return;
        scoreManager.PickUpPoint();
    }

    void SimulateMiss()
    {
        if (paused) return;
        scoreManager.ResetCounter();
    }

    void SimulateDeath()
    {
        EndGameplay();

        scoreManager.ResetCounter();
        uiManager.GameOver();
    }

    // Pause

    void TogglePause()
    {
        if (!gameplayActive) return;

        if (paused)
            ResumeFromPause();
        else
            EnterPause();
    }

    void EnterPause()
    {
        paused = true;
        scoreManager.StopScoring();
        Time.timeScale = 0f;

        uiManager.NotifyPaused();
    }

    public void ResumeFromPause()
    {
        paused = false;
        Time.timeScale = 1f;

        if (gameplayActive)
            scoreManager.StartScoring();

        uiManager.NotifyResumed();
    }

}
