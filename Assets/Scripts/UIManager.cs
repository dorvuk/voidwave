using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class UIManager : MonoBehaviour
{
    [Header("Canvases")]
    public CanvasGroup mainMenu;
    public CanvasGroup gameplay;
    public CanvasGroup gameOver;
    public CanvasGroup pause;

    [Header("Main Menu Elements")]
    public CanvasGroup titleText;
    public CanvasGroup pressSpaceText;
    public CanvasGroup creditsButton;

    [Header("Panels")]
    public CanvasGroup creditsPanel;
    public CanvasGroup quitConfirmPanel;

    [Header("Fade Durations")]
    public float slowFadeDuration = 2.5f;
    public float fastFadeDuration = 0.25f;

    [Header("Timings")]
    public float pressSpaceDelay = 2f;
    public float autoFadeOutDelay = 10f;

    [Header("High Score Popup")]
    public ScoreManager scoreManager;
    public HighScorePopupAnimator highScorePopupAnimator;

    [Header("Gameplay")]
    [SerializeField] private GameplaySimulator gameplaySimulator;
    [SerializeField] private GameStateManager gameStateManager;

    [Header("Onboarding")]
    public CanvasGroup onboarding;
    private readonly string onboardingSeenKey = "OnboardingSeen";

    // FSM
    private enum UIState
    {
        Boot,
        MainMenu_Intro,
        MainMenu_IdleVisible,
        MainMenu_IdleHidden,

        Credits_Open,
        QuitConfirm_Open,

        Transition_ToGameplay,
        Gameplay,

        Pause,

        Transition_ToGameOver,
        GameOver
    }

    [SerializeField] private UIState state = UIState.Boot;

    private Coroutine stateRoutine;
    private Coroutine autoFadeCoroutine;
    private Coroutine gameOverReturnCoroutine;

    // Fade System
    private readonly Dictionary<CanvasGroup, Coroutine> activeFades = new();

    void Start()
    {
        InitCanvas(mainMenu, true);
        InitCanvas(gameplay, false);
        InitCanvas(gameOver, false);
        InitCanvas(pause, false);

        InitCanvas(titleText, false);
        InitCanvas(pressSpaceText, false);
        InitCanvas(creditsButton, false);

        InitCanvas(creditsPanel, false);
        InitCanvas(quitConfirmPanel, false);

        InitCanvas(onboarding, false);

        TransitionTo(UIState.MainMenu_Intro);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            OnEscape();


        // Bad and hacky. Should have a hook for when lane is changed.
        if ((state == UIState.Gameplay) && (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.D) ||
            Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow)))
        {
            AudioManager.I.PlaySfx(SfxId.Move);
        }

        if ((state == UIState.Gameplay) && Input.GetKeyDown(KeyCode.Space))
        {
            AudioManager.I.PlaySfx(SfxId.Jump);
        }

        if ((state == UIState.MainMenu_Intro ||
             state == UIState.MainMenu_IdleVisible ||
             state == UIState.MainMenu_IdleHidden) &&
            Input.anyKeyDown)
        {
            OnAnyKey();
        }
    }

    // State Machine Core

    private void TransitionTo(UIState next)
    {
        if (state == next) return;

        if (stateRoutine != null)
        {
            StopCoroutine(stateRoutine);
            stateRoutine = null;
        }

        StopAutoFade();

        if (next == UIState.Transition_ToGameplay || next == UIState.Gameplay || next == UIState.Pause || next == UIState.MainMenu_Intro ||
            state == UIState.GameOver || state == UIState.Transition_ToGameOver)
        {
            StopGameOverReturn();
        }

        if (state == UIState.Credits_Open && next != UIState.Credits_Open)
            FadeOut(creditsPanel, fastFadeDuration, disableOnZero: true);

        if (state == UIState.QuitConfirm_Open && next != UIState.QuitConfirm_Open)
            FadeOut(quitConfirmPanel, fastFadeDuration, disableOnZero: true);

        if (state == UIState.Pause && next != UIState.Pause)
            FadeOut(pause, fastFadeDuration, disableOnZero: true);

        state = next;
        stateRoutine = StartCoroutine(StateEnter(next));
    }

    private IEnumerator StateEnter(UIState s)
    {
        switch (s)
        {
            case UIState.MainMenu_Intro:
                yield return Enter_MainMenuIntro();
                break;

            case UIState.MainMenu_IdleVisible:
                Enter_MainMenuVisible();
                break;

            case UIState.MainMenu_IdleHidden:
                Enter_MainMenuHidden();
                break;

            case UIState.Credits_Open:
                yield return Enter_CreditsOpen();
                break;

            case UIState.QuitConfirm_Open:
                yield return Enter_QuitConfirmOpen();
                break;

            case UIState.Transition_ToGameplay:
                yield return Enter_TransitionToGameplay();
                break;

            case UIState.Gameplay:
                Enter_Gameplay();
                break;

            case UIState.Pause:
                yield return Enter_Pause();
                break;

            case UIState.Transition_ToGameOver:
                yield return Enter_TransitionToGameOver();
                break;

            case UIState.GameOver:
                yield return Enter_GameOver();
                break;
        }
    }

    private void StopGameOverReturn()
    {
        if (gameOverReturnCoroutine != null)
        {
            StopCoroutine(gameOverReturnCoroutine);
            gameOverReturnCoroutine = null;
        }
    }

    // Input routing 

    private void OnAnyKey()
    {
        switch (state)
        {
            case UIState.MainMenu_Intro:
            case UIState.MainMenu_IdleVisible:
                if (Input.GetKeyDown(KeyCode.Space))
                {
                    StartGame();
                    return;
                }
                break;

            case UIState.MainMenu_IdleHidden:
                ShowMainMenuUI();
                TransitionTo(UIState.MainMenu_IdleVisible);
                break;
        }
    }

    private void OnEscape()
    {
        switch (state)
        {
            case UIState.MainMenu_Intro:
            case UIState.MainMenu_IdleVisible:
            case UIState.MainMenu_IdleHidden:
                TransitionTo(UIState.QuitConfirm_Open);
                break;

            case UIState.Credits_Open:
                FadeOut(creditsPanel, fastFadeDuration, disableOnZero: true);
                TransitionTo(UIState.QuitConfirm_Open);
                break;

            case UIState.QuitConfirm_Open:
                TransitionTo(UIState.MainMenu_IdleVisible);
                break;

            case UIState.Pause:
                ResumeFromPause();
                break;
        }
    }

    //  GamePlay API

    public void NotifyPaused()
    {
        if (state == UIState.Gameplay) {
            AudioManager.I.SetPausedAudio(true);
            TransitionTo(UIState.Pause);
        }
    }

    public void NotifyResumed()
    {
        if (state == UIState.Pause) {
            AudioManager.I.SetPausedAudio(false);
            TransitionTo(UIState.Gameplay);
        }
    }

    // Game hooks 

    public void StartGame(bool notifyGameState = true)
    {
        if (state == UIState.Gameplay || state == UIState.Transition_ToGameplay || state == UIState.Pause)
            return;

        StopGameOverReturn();

        ForceDisableCanvas(mainMenu);
        TransitionTo(UIState.Transition_ToGameplay);

        if (notifyGameState && gameStateManager != null)
        {
            gameStateManager.OnPlayPressed();
        }
        else if (notifyGameState)
        {
            Debug.LogWarning("GameStateManager reference missing on UIManager.", this);
        }
    }

    public void GameOver()
    {
        if (state == UIState.GameOver || state == UIState.Transition_ToGameOver)
            return;

        TransitionTo(UIState.Transition_ToGameOver);
    }

    public void ReturnToMainMenu()
    {
        StopGameOverReturn();

        SetCanvasActive(gameOver, false);
        SetCanvasActive(gameplay, false);
        SetCanvasActive(pause, false);

        InitCanvas(titleText, false);
        InitCanvas(pressSpaceText, false);
        InitCanvas(creditsButton, false);
        InitCanvas(creditsPanel, false);
        InitCanvas(quitConfirmPanel, false);

        SetCanvasActive(mainMenu, true);
        TransitionTo(UIState.MainMenu_Intro);
    }

    public void RequestReturnToMenu()
    {
        AudioManager.I.SetPausedAudio(false);
        AudioManager.I?.PlaySfx(SfxId.Surface);
        
        if (gameStateManager != null)
            gameStateManager.OnRestartPressed();
        else
            ReturnToMainMenu();
    }

    // Pause buttons

    public void ResumeFromPause()
    {
        //gameplaySimulator?.SendMessage("ResumeFromPause", SendMessageOptions.DontRequireReceiver);
        gameplaySimulator.ResumeFromPause();
    }

    public void QuitToMenuFromPause()
    {
        gameplaySimulator?.EndGameplay();
        ReturnToMainMenu();
    }

    // Buttons (menu) 

    public void OpenCredits()
    {
        if (state == UIState.Credits_Open) return;

        if (state != UIState.MainMenu_Intro &&
            state != UIState.MainMenu_IdleVisible &&
            state != UIState.MainMenu_IdleHidden)
            return;

        TransitionTo(UIState.Credits_Open);
    }

    public void CloseCredits()
    {
        if (state != UIState.Credits_Open) return;
        TransitionTo(UIState.MainMenu_IdleVisible);
    }

    public void OpenQuitConfirm()
    {
        if (state == UIState.QuitConfirm_Open) return;
        TransitionTo(UIState.QuitConfirm_Open);
    }

    public void CloseQuitConfirm()
    {
        if (state != UIState.QuitConfirm_Open) return;
        TransitionTo(UIState.MainMenu_IdleVisible);
    }

    public void QuitGame()
    {
        Application.Quit();
    }

    // State entry implementations

    private IEnumerator Enter_MainMenuIntro()
    {
        yield return new WaitForSecondsRealtime(1f);
        AudioManager.I?.PlayMusic(MusicTrack.MainMenuTheme);

        SetCanvasActive(gameplay, false);
        SetCanvasActive(gameOver, false);
        SetCanvasActive(pause, false);
        SetCanvasActive(mainMenu, true);

        InitCanvas(creditsPanel, false);
        InitCanvas(quitConfirmPanel, false);

        yield return FadeIn(titleText, slowFadeDuration);
        yield return new WaitForSecondsRealtime(pressSpaceDelay);
        yield return FadeIn(pressSpaceText, slowFadeDuration);
        yield return FadeIn(creditsButton, fastFadeDuration);

        TransitionTo(UIState.MainMenu_IdleVisible);
    }

    private void Enter_MainMenuVisible()
    {
        RestartAutoFade();
    }

    private void Enter_MainMenuHidden()
    {
        // NOP
    }

    private IEnumerator Enter_CreditsOpen()
    {
        yield return FadeIn(creditsPanel, fastFadeDuration);
    }

    private IEnumerator Enter_QuitConfirmOpen()
    {
        yield return FadeIn(quitConfirmPanel, fastFadeDuration);
    }

    private IEnumerator Enter_TransitionToGameplay()
    {
        HideMainMenuUI();
        yield return new WaitForSecondsRealtime(slowFadeDuration);
        
        AudioManager.I?.PlaySfx(SfxId.Submerge); 
        AudioManager.I?.PlayMusic(MusicTrack.GameplayTheme);

        SetCanvasActive(mainMenu, false);
        SetCanvasActive(gameplay, true);
        gameplay.alpha = 0f;

        yield return FadeIn(gameplay, fastFadeDuration);

        TransitionTo(UIState.Gameplay);
    }

    private void Enter_Gameplay()
    {
        StopGameOverReturn();
        SetCanvasActive(pause, false);
        TryShowOnboardingOnce();
    }

    private IEnumerator Enter_Pause()
    {
        if (pause == null)
            yield break;

        pause.gameObject.SetActive(true);
        pause.blocksRaycasts = true;
        pause.interactable = true;

        yield return FadeIn(pause, fastFadeDuration);
    }

    private IEnumerator Enter_TransitionToGameOver()
    {
        AudioManager.I?.PlayMusic(MusicTrack.EndGameTheme);
        AudioManager.I?.PlaySfx(SfxId.Death);

        yield return new WaitForSecondsRealtime(4f);

        SetCanvasActive(pause, false);
        SetCanvasActive(gameplay, false);
        SetCanvasActive(gameOver, true);

        if (scoreManager != null)
        {
            scoreManager.FinalizeRunScore();

            int runScore = scoreManager.CurrentScore;
            int oldHigh = scoreManager.HighScore;
            int newHigh = runScore > oldHigh ? runScore : oldHigh;

            if (highScorePopupAnimator != null)
                highScorePopupAnimator.Play(runScore, oldHigh, newHigh);

            scoreManager.CommitHighScore();
        }

        if (gameplaySimulator != null)
            gameplaySimulator.EndGameplay();
        else
            FindObjectOfType<GameplaySimulator>()?.EndGameplay();

        TransitionTo(UIState.GameOver);
        yield break;
    }

    private IEnumerator Enter_GameOver()
    {
        StopGameOverReturn();
        gameOverReturnCoroutine = StartCoroutine(ReturnToMenuAfterDelay());
        yield break;
    }

    private IEnumerator ReturnToMenuAfterDelay()
    {
        yield return new WaitForSecondsRealtime(10f);

        if (state == UIState.GameOver)
        {
            if (gameStateManager != null)
                gameStateManager.OnRestartPressed();
            else
                ReturnToMainMenu();
        }

        gameOverReturnCoroutine = null;
    }

    public void CancelGameOverAutoReturn()
    {
        StopGameOverReturn();
    }

    // Auto fade

    private void RestartAutoFade()
    {
        StopAutoFade();
        autoFadeCoroutine = StartCoroutine(AutoFadeOutUI());
    }

    private void StopAutoFade()
    {
        if (autoFadeCoroutine != null)
            StopCoroutine(autoFadeCoroutine);
        autoFadeCoroutine = null;
    }

    private IEnumerator AutoFadeOutUI()
    {
        yield return new WaitForSecondsRealtime(autoFadeOutDelay);

        if (state == UIState.MainMenu_IdleVisible)
        {
            HideMainMenuUI();
            TransitionTo(UIState.MainMenu_IdleHidden);
        }
    }

    // UI Visibility

    private void HideMainMenuUI()
    {
        FadeOut(titleText, slowFadeDuration);
        FadeOut(pressSpaceText, slowFadeDuration);
        FadeOut(creditsButton, fastFadeDuration);
    }

    private void ShowMainMenuUI()
    {
        FadeIn(titleText, slowFadeDuration);
        FadeIn(pressSpaceText, slowFadeDuration);
        FadeIn(creditsButton, fastFadeDuration);
    }

    // Fade System

    private void KillFade(CanvasGroup cg)
    {
        if (cg == null) return;

        if (activeFades.TryGetValue(cg, out var co) && co != null)
            StopCoroutine(co);

        activeFades.Remove(cg);
    }

    private Coroutine FadeTo(CanvasGroup cg, float targetAlpha, float duration, bool disableGameObjectOnZero, bool allowInteractionWhileVisible)
    {
        if (cg == null) return null;

        KillFade(cg);

        if (targetAlpha > 0f && !cg.gameObject.activeSelf)
            cg.gameObject.SetActive(true);

        if (targetAlpha <= 0f)
        {
            cg.interactable = false;
            cg.blocksRaycasts = false;
        }
        else
        {
            cg.interactable = allowInteractionWhileVisible;
            cg.blocksRaycasts = allowInteractionWhileVisible;
        }

        var co = StartCoroutine(FadeRoutine_Cancelable(cg, targetAlpha, duration, disableGameObjectOnZero));
        activeFades[cg] = co;
        return co;
    }

    private IEnumerator FadeRoutine_Cancelable(CanvasGroup cg, float to, float duration, bool disableOnZero)
    {
        float from = cg.alpha;

        if (duration <= 0f)
        {
            cg.alpha = to;
            if (disableOnZero && to <= 0f)
                cg.gameObject.SetActive(false);

            activeFades.Remove(cg);
            yield break;
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime; // UI should ignore timeScale
            float k = Mathf.Clamp01(t / duration);
            cg.alpha = Mathf.Lerp(from, to, k);
            yield return null;
        }

        cg.alpha = to;

        if (disableOnZero && to <= 0f)
            cg.gameObject.SetActive(false);

        activeFades.Remove(cg);
    }

    private Coroutine FadeIn(CanvasGroup cg, float duration, bool allowInteractionWhileVisible = true)
    {
        return FadeTo(cg, 0.9f, duration, disableGameObjectOnZero: false, allowInteractionWhileVisible: allowInteractionWhileVisible);
    }

    private Coroutine FadeOut(CanvasGroup cg, float duration, bool disableOnZero = false)
    {
        return FadeTo(cg, 0f, duration, disableGameObjectOnZero: disableOnZero, allowInteractionWhileVisible: false);
    }

    // Utilities 

    void InitCanvas(CanvasGroup cg, bool visible)
    {
        if (cg == null) return;

        KillFade(cg);

        cg.alpha = visible ? 0.9f : 0f;
        cg.interactable = visible;
        cg.blocksRaycasts = visible;
        cg.gameObject.SetActive(true);

        if (!visible)
            cg.gameObject.SetActive(false);
    }

    public void SetCanvasActive(CanvasGroup cg, bool toActivate)
    {
        if (cg == null) return;

        if (toActivate)
        {
            cg.gameObject.SetActive(true);
            FadeIn(cg, fastFadeDuration, allowInteractionWhileVisible: true);
        }
        else
        {
            FadeOut(cg, fastFadeDuration, disableOnZero: true);
        }
    }

    void ForceDisableCanvas(CanvasGroup cg)
    {
        if (cg == null) return;

        KillFade(cg);

        cg.alpha = 0f;
        cg.interactable = false;
        cg.blocksRaycasts = false;
        cg.gameObject.SetActive(false);
    }

        public void RestartRun()
{
    StopGameOverReturn();

    Time.timeScale = 1f;

    gameplaySimulator.EndGameplay();

    SetCanvasActive(pause, false);
    SetCanvasActive(gameOver, false);

    SetCanvasActive(mainMenu, false);
    SetCanvasActive(gameplay, true);

    TransitionTo(UIState.Transition_ToGameplay);

    AudioManager.I.PlaySfx(SfxId.Submerge);

    gameplaySimulator.BeginGameplay();
}

    private void TryShowOnboardingOnce()
    {
        if (onboarding == null) return;

        if (PlayerPrefs.GetInt(onboardingSeenKey, 0) == 1)
            return;

        PlayerPrefs.SetInt(onboardingSeenKey, 1);
        PlayerPrefs.Save();

        StartCoroutine(OnboardingRoutine());
    }

    private IEnumerator OnboardingRoutine()
    {
        onboarding.gameObject.SetActive(true);
        onboarding.blocksRaycasts = false;
        onboarding.interactable = false;

        FadeIn(onboarding, fastFadeDuration, allowInteractionWhileVisible: false);

        yield return new WaitForSecondsRealtime(6f);

        FadeOut(onboarding, fastFadeDuration, disableOnZero: true);
    }
}
