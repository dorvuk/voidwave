using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

public class GameStateManager : MonoBehaviour
{
    public enum GameState
    {
        MenuIdle,
        TransitionToPlay,
        Playing,
        GameOver,
        TransitionToMenu
    }

    [Header("UI Roots")]
    public GameObject menuUIRoot;
    public GameObject hudUIRoot;
    public GameObject gameOverUIRoot;

    [Header("UI Integration")]
    [Tooltip("Optional: if assigned, UIManager handles UI transitions/fades and GameStateManager will not toggle UI roots directly.")]
    [SerializeField] UIManager uiManager;

    [Header("Player Health")]
    [Tooltip("Optional: if assigned, GameStateManager listens for player death events.")]
    [SerializeField] PlayerHealth playerHealth;

    [Header("Scoring")]
    [SerializeField] ScoreManager scoreManager;

    [Header("Player Animations")]
    [SerializeField] PlayerController playerController;
    [SerializeField] TrackRunner trackRunner;

    [Header("Cameras")]
    public GameObject orbitCameraGO;
    public GameObject gameplayCameraGO;
    public Camera transitionCamera;
    public bool useCameraBlend = true;

    [Header("Camera Focus")]
    public bool focusPlayerDuringBlend = true;
    public Transform cameraFocusTarget;
    public Vector3 cameraFocusOffset = new Vector3(0f, 1.2f, 0f);
    [Tooltip("How quickly the transition camera rotates to face the player (degrees per second).")]
    public float focusRotationSpeed = 360f;

    [Header("Player")]
    public Transform playerRoot;
    public Transform menuPose;

    [Header("Surface Transition")]
    [FormerlySerializedAs("tempOceanHeightOffset")]
    public float surfaceHeightOffset = 2f;

    [Header("Gameplay Systems")]
    [Tooltip("Systems that should be toggled on only during gameplay (runner, generator, spawners, etc).")]
    public Behaviour[] gameplayBehavioursToEnable;
    [Tooltip("Optional: entire GameObjects to toggle instead of individual components.")]
    public MonoBehaviour[] resettableSystems;

    [Header("Timings")]
    [Tooltip("Seconds to dive from menu pose to track start.")]
    public float startDelay = 0.75f;
    public float surfaceDuration = 1.5f;
    [Tooltip("Exponent-style ease for transition movement. 1 = linear.")]
    public float transitionMoveEasing = 1f;
    [Tooltip("Blend time between cameras during transitions.")]
    public float cameraBlendDuration = 1.0f;

    public GameState CurrentState { get; private set; } = GameState.MenuIdle;

    Coroutine transitionRoutine;
    Coroutine cameraBlendRoutine;
    bool isBlendingCamera;
    Vector3 lastDeathPos;

    void OnEnable()
    {
        if (!uiManager)
            uiManager = FindObjectOfType<UIManager>();

        if (!scoreManager)
            scoreManager = FindObjectOfType<ScoreManager>();

        if (!playerController)
        {
            if (playerRoot) playerController = playerRoot.GetComponent<PlayerController>();
            if (!playerController) playerController = FindObjectOfType<PlayerController>();
        }

        if (!trackRunner)
        {
            if (playerRoot) trackRunner = playerRoot.GetComponent<TrackRunner>();
            if (!trackRunner) trackRunner = FindObjectOfType<TrackRunner>();
        }

        if (!cameraFocusTarget && playerRoot)
            cameraFocusTarget = playerRoot;

        HookPlayerHealth();
    }

    void OnDisable()
    {
        UnhookPlayerHealth();
    }

    void Start()
    {
        ForceMenuState();
    }

    public void OnPlayPressed()
    {
        if (CurrentState != GameState.MenuIdle)
        {
            Debug.LogWarning($"Play pressed while in {CurrentState}, ignoring.", this);
            return;
        }

        if (playerController != null)
            playerController.PlayDiveIn();

        if (uiManager != null)
            uiManager.StartGame(false);

        if (transitionRoutine != null) return;
        transitionRoutine = StartCoroutine(TransitionToPlayRoutine());
    }

    public void OnRestartPressed()
    {
        if (CurrentState != GameState.GameOver)
        {
            Debug.LogWarning($"Restart pressed while in {CurrentState}, ignoring.", this);
            return;
        }

        if (uiManager != null)
            uiManager.CancelGameOverAutoReturn();

        if (transitionRoutine != null) return;
        transitionRoutine = StartCoroutine(TransitionToMenuRoutine());
    }

    public void OnPlayerDied()
    {
        if (CurrentState == GameState.GameOver)
            return;

        if (CurrentState != GameState.Playing)
        {
            Debug.LogWarning($"OnPlayerDied called while in {CurrentState}, ignoring.", this);
            return;
        }

        lastDeathPos = playerRoot ? playerRoot.position : Vector3.zero;

        if (scoreManager != null)
            scoreManager.StopScoring();

        SetGameplayBehavioursEnabled(false);
        if (uiManager != null)
            uiManager.GameOver();
        else
            SetUIState(false, false, true);

        if (playerController != null)
            playerController.PlayDeath();

        CurrentState = GameState.GameOver;
    }

    void HookPlayerHealth()
    {
        if (!playerHealth)
            playerHealth = FindObjectOfType<PlayerHealth>();

        if (playerHealth)
            playerHealth.OnDeath += HandlePlayerDeath;
        else if (Application.isPlaying)
            Debug.LogWarning("GameStateManager: No PlayerHealth assigned or found; death will not trigger game over.", this);
    }

    void UnhookPlayerHealth()
    {
        if (playerHealth)
            playerHealth.OnDeath -= HandlePlayerDeath;
    }

    void HandlePlayerDeath()
    {
        OnPlayerDied();
    }

    IEnumerator TransitionToPlayRoutine()
    {
        CurrentState = GameState.TransitionToPlay;

        if (useCameraBlend && transitionCamera != null)
            yield return BlendCamera(orbitCameraGO, gameplayCameraGO, cameraBlendDuration);
        else
            SwitchToGameplayCamera();
        SetUIState(false, false, false);

        if (trackRunner != null)
            trackRunner.snapToStartOnReset = false;

        ResetAllSystems();

        Vector3 targetPos = playerRoot ? playerRoot.position : Vector3.zero;
        Quaternion targetRot = playerRoot ? playerRoot.rotation : Quaternion.identity;
        if (TryGetTrackStartPose(out var pos, out var rot))
        {
            targetPos = pos;
            targetRot = rot;
        }

        float diveDuration = Mathf.Max(0f, startDelay);
        if (playerRoot && diveDuration > 0f)
        {
            Vector3 fromPos = playerRoot.position;
            Quaternion fromRot = playerRoot.rotation;

            float t = 0f;
            while (t < diveDuration)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / diveDuration);
                float eased = EaseMove(u);
                playerRoot.SetPositionAndRotation(
                    Vector3.Lerp(fromPos, targetPos, eased),
                    Quaternion.Slerp(fromRot, targetRot, eased));
                yield return null;
            }
        }

        if (playerRoot)
            playerRoot.SetPositionAndRotation(targetPos, targetRot);

        if (trackRunner != null)
            trackRunner.snapToStartOnReset = true;

        SetGameplayBehavioursEnabled(true);
        SetUIState(false, true, false);

        if (scoreManager != null)
        {
            scoreManager.ResetAllScores();
            scoreManager.StartScoring();
        }

        if (playerController != null)
            playerController.PlaySurfing();

        CurrentState = GameState.Playing;
        transitionRoutine = null;
    }

    IEnumerator TransitionToMenuRoutine()
    {
        CurrentState = GameState.TransitionToMenu;
        SetUIState(false, false, false);

        if (scoreManager != null)
            scoreManager.StopScoring();

        if (playerController != null)
            playerController.PlaySwimSurface();

        // Keep gameplay view during the swim up (no blend on return).
        SwitchToGameplayCamera();

        Vector3 surfacePos = lastDeathPos + Vector3.up * surfaceHeightOffset;

        float duration = Mathf.Max(0.001f, surfaceDuration);
        Vector3 startPos = playerRoot ? playerRoot.position : surfacePos;
        Quaternion startRot = playerRoot ? playerRoot.rotation : Quaternion.identity;
        Quaternion targetRot = menuPose ? menuPose.rotation : Quaternion.identity;

        if (playerRoot)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / duration);
                u = EaseMove(u);
                playerRoot.position = Vector3.Lerp(startPos, surfacePos, u);
                playerRoot.rotation = Quaternion.Slerp(startRot, targetRot, u);
                yield return null;
            }

            playerRoot.position = surfacePos;
            playerRoot.rotation = targetRot;
        }
        else
        {
            yield return new WaitForSeconds(duration);
        }

        HardResetToMenu();
        transitionRoutine = null;
    }

    void HardResetToMenu()
    {
        bool shouldSyncUI = uiManager != null && CurrentState != GameState.MenuIdle;

        SetGameplayBehavioursEnabled(false);
        ResetAllSystems();
        ResetPlayerToMenuPose();

        SwitchToOrbitCamera();
        if (uiManager != null)
        {
            if (shouldSyncUI)
                uiManager.ReturnToMainMenu();
        }
        else
        {
            SetUIState(true, false, false);
        }

        if (playerController != null)
            playerController.PlayIdle();

        CurrentState = GameState.MenuIdle;
    }

    void ForceMenuState()
    {
        if (transitionRoutine != null)
        {
            StopCoroutine(transitionRoutine);
            transitionRoutine = null;
        }

        if (scoreManager != null)
            scoreManager.StopScoring();

        HardResetToMenu();
    }

    bool TryGetTrackStartPose(out Vector3 pos, out Quaternion rot)
    {
        pos = playerRoot ? playerRoot.position : Vector3.zero;
        rot = playerRoot ? playerRoot.rotation : Quaternion.identity;

        if (!trackRunner || !trackRunner.track) return false;

        Vector3 p = default, fwd = default, up = default, right = default;
        Vector3 lastUp = Vector3.up;
        TrackSample.At(trackRunner.track, 0f, trackRunner.track.Spline.Closed, ref p, ref fwd, ref up, ref right, ref lastUp);

        float half = trackRunner.laneWidth * 0.5f;
        pos = p + right * (-half) + up * trackRunner.hover;
        rot = Quaternion.LookRotation(fwd, up);
        return true;
    }

    void ResetPlayerToMenuPose()
    {
        if (!playerRoot)
        {
            Debug.LogWarning("Player root is not assigned; cannot snap to menu pose.", this);
            return;
        }

        if (!menuPose)
        {
            Debug.LogWarning("Menu pose is not assigned; cannot snap player for menu state.", this);
            return;
        }

        var cc = playerRoot.GetComponent<CharacterController>();
        if (cc)
        {
            bool wasEnabled = cc.enabled;
            cc.enabled = false;
            playerRoot.SetPositionAndRotation(menuPose.position, menuPose.rotation);
            cc.enabled = wasEnabled;
        }
        else
        {
            playerRoot.SetPositionAndRotation(menuPose.position, menuPose.rotation);
        }
    }

    void SetGameplayBehavioursEnabled(bool enabled)
    {
        if (gameplayBehavioursToEnable != null)
        {
            foreach (var b in gameplayBehavioursToEnable)
            {
                if (!b) continue;
                b.enabled = enabled;
            }
        }
    }

    void ResetAllSystems()
    {
        if (resettableSystems != null && resettableSystems.Length > 0)
        {
            foreach (var mb in resettableSystems)
            {
                if (!mb) continue;

                if (mb is IRunResettable resettable)
                {
                    try
                    {
                        resettable.ResetRun();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"ResetRun threw on {mb.name}: {ex}", this);
                    }
                }
                else
                {
                    Debug.LogWarning($"Reset list contains {mb.name} but it does not implement IRunResettable.", this);
                }
            }

            return;
        }

        var resettables = FindObjectsOfType<MonoBehaviour>(true);
        foreach (var mb in resettables)
        {
            if (mb is IRunResettable resettable)
            {
                try
                {
                    resettable.ResetRun();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"ResetRun threw on {mb.name}: {ex}", this);
                }
            }
        }
    }

    void SetUIState(bool showMenu, bool showHud, bool showGameOver)
    {
        if (uiManager != null)
            return;

        if (menuUIRoot) menuUIRoot.SetActive(showMenu);
        if (hudUIRoot) hudUIRoot.SetActive(showHud);
        if (gameOverUIRoot) gameOverUIRoot.SetActive(showGameOver);
    }

    void SwitchToOrbitCamera()
    {
        if (isBlendingCamera)
            return;
        SetCameraActive(orbitCameraGO, true);
        SetCameraActive(gameplayCameraGO, false);
    }

    void SwitchToGameplayCamera()
    {
        if (isBlendingCamera)
            return;
        SetCameraActive(orbitCameraGO, false);
        SetCameraActive(gameplayCameraGO, true);
    }

    IEnumerator BlendCamera(GameObject fromCamGO, GameObject toCamGO, float duration)
    {
        if (transitionCamera == null)
            yield break;

        if (fromCamGO == null || toCamGO == null)
            yield break;

        var fromCam = fromCamGO.GetComponent<Camera>();
        var toCam = toCamGO.GetComponent<Camera>();

        if (fromCam == null || toCam == null)
            yield break;

        if (cameraBlendRoutine != null)
            StopCoroutine(cameraBlendRoutine);

        cameraBlendRoutine = StartCoroutine(BlendRoutine(fromCam, toCam, duration));
        yield return cameraBlendRoutine;
        cameraBlendRoutine = null;
    }

    IEnumerator BlendRoutine(Camera fromCam, Camera toCam, float duration)
    {
        isBlendingCamera = true;
        SetCameraActive(fromCam.gameObject, false);
        SetCameraActive(toCam.gameObject, false);
        SetCameraActive(transitionCamera.gameObject, true);

        Transform tCam = transitionCamera.transform;
        tCam.position = fromCam.transform.position;
        tCam.rotation = fromCam.transform.rotation;
        transitionCamera.fieldOfView = fromCam.fieldOfView;

        float t = 0f;
        float d = Mathf.Max(0.001f, duration);
        while (t < d)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / d);
            float eased = EaseMove(u);

            tCam.position = Vector3.Lerp(fromCam.transform.position, toCam.transform.position, eased);
            if (focusPlayerDuringBlend && cameraFocusTarget != null)
            {
                Vector3 focusPos = cameraFocusTarget.position + cameraFocusOffset;
                Vector3 dir = focusPos - tCam.position;
                if (dir.sqrMagnitude > 0.0001f)
                {
                    Quaternion look = Quaternion.LookRotation(dir.normalized, Vector3.up);
                    float step = Mathf.Max(0f, focusRotationSpeed) * Time.deltaTime;
                    tCam.rotation = Quaternion.RotateTowards(tCam.rotation, look, step);
                }
            }
            else
            {
                tCam.rotation = Quaternion.Slerp(fromCam.transform.rotation, toCam.transform.rotation, eased);
            }
            transitionCamera.fieldOfView = Mathf.Lerp(fromCam.fieldOfView, toCam.fieldOfView, eased);

            yield return null;
        }

        SetCameraActive(transitionCamera.gameObject, false);
        SetCameraActive(toCam.gameObject, true);
        isBlendingCamera = false;
    }

    void SetCameraActive(GameObject camGO, bool active)
    {
        if (!camGO)
        {
            Debug.LogWarning("Camera GameObject reference missing.", this);
            return;
        }

        camGO.SetActive(active);
    }

    float EaseMove(float t)
    {
        t = Mathf.Clamp01(t);
        if (Mathf.Approximately(transitionMoveEasing, 1f)) return t;

        float p = Mathf.Max(0.001f, transitionMoveEasing);
        return Mathf.Pow(t, p);
    }
}
