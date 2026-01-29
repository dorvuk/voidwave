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
        Paused,
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
    [SerializeField] FollowCam gameplayFollowCam;

    [Header("Ocean Fade")]
    [SerializeField] GameObject oceanRoot;
    public float oceanFadeOutDuration = 1.0f;
    public float oceanFadeInDuration = 1.0f;
    [Tooltip("Start fading the ocean back in when the player is this close to the surface height.")]
    public float surfaceFadeStartDistance = 15f;
    [Range(0f, 1f)]
    public float surfaceFadeStartNormalized = 0.75f;

    [Header("Camera Focus")]
    public bool focusPlayerDuringBlend = true;
    public Transform cameraFocusTarget;
    public Vector3 cameraFocusOffset = new Vector3(0f, 1.2f, 0f);
    [Tooltip("How quickly the transition camera rotates to face the player (degrees per second).")]
    public float focusRotationSpeed = 360f;

    [Header("Surface Camera")]
    public bool overrideSurfaceCamera = true;
    public Vector3 surfaceCameraOffset = new Vector3(0f, 2.2f, -6.5f);
    public float surfaceCameraFollow = 10f;
    public float surfaceCameraTurn = 12f;
    public float surfaceCameraLookHeight = 1.2f;

    [Header("Player")]
    public Transform playerRoot;
    public Transform menuPose;
    [Header("Player VFX")]
    [SerializeField] GameObject playerTrailRoot;

    [Header("Surface Transition")]
    [FormerlySerializedAs("tempOceanHeightOffset")]
    public float surfaceHeightOffset = 2f;

    [Header("Return To Surface")]
    public bool playDeathBeforeSurface = true;
    public float returnToSurfaceDelay = 1.0f;

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

    [Header("Pause")]
    public bool allowPause = true;

    public GameState CurrentState { get; private set; } = GameState.MenuIdle;

    Coroutine transitionRoutine;
    Coroutine cameraBlendRoutine;
    bool isBlendingCamera;
    Vector3 lastDeathPos;
    FollowCamState followCamState;
    bool followCamOverridden;
    Coroutine oceanFadeRoutine;
    Renderer[] oceanRenderers;
    float oceanAlpha = 1f;

    struct FollowCamState
    {
        public bool useWorldOffset;
        public bool lookAtTarget;
        public Vector3 worldOffset;
        public Vector3 localOffset;
        public float follow;
        public float turn;
        public float lookHeight;
    }

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

        ResolveTrailRoot();
        CacheFollowCam();
        CacheOcean();
        HookPlayerHealth();
    }

    void OnDisable()
    {
        UnhookPlayerHealth();
        RestoreSurfaceCamera();
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

    public void ReturnToSurface()
    {
        if (CurrentState == GameState.GameOver)
        {
            OnRestartPressed();
            return;
        }

        if (CurrentState != GameState.Playing && CurrentState != GameState.Paused)
        {
            Debug.LogWarning($"Return to surface pressed while in {CurrentState}, ignoring.", this);
            return;
        }

        if (transitionRoutine != null) return;
        transitionRoutine = StartCoroutine(ReturnToSurfaceRoutine());
    }

    IEnumerator ReturnToSurfaceRoutine()
    {
        if (CurrentState == GameState.Paused)
        {
            Time.timeScale = 1f;
            if (uiManager != null)
                uiManager.NotifyResumed(true);
        }

        CurrentState = GameState.TransitionToMenu;

        if (scoreManager != null)
            scoreManager.StopScoring();

        SetTrailActive(false);
        SetGameplayBehavioursEnabled(false);

        if (uiManager != null)
        {
            uiManager.SetCanvasActive(uiManager.gameplay, false);
            uiManager.SetCanvasActive(uiManager.pause, false);
            uiManager.SetCanvasActive(uiManager.gameOver, false);
        }
        else
        {
            SetUIState(false, false, false);
        }

        lastDeathPos = playerRoot ? playerRoot.position : Vector3.zero;

        if (playerController != null && playDeathBeforeSurface)
            playerController.PlayDeath();

        float delay = Mathf.Max(0f, returnToSurfaceDelay);
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        yield return TransitionToMenuRoutine();
    }

    public void TogglePause()
    {
        if (!allowPause) return;

        if (CurrentState == GameState.Playing)
            PauseGame();
        else if (CurrentState == GameState.Paused)
            ResumeGame();
    }

    public void PauseGame()
    {
        if (CurrentState != GameState.Playing) return;

        CurrentState = GameState.Paused;
        Time.timeScale = 0f;

        if (scoreManager != null)
            scoreManager.StopScoring();

        if (uiManager != null)
            uiManager.NotifyPaused(true);
    }

    public void ResumeGame()
    {
        if (CurrentState != GameState.Paused) return;

        CurrentState = GameState.Playing;
        Time.timeScale = 1f;

        if (scoreManager != null)
            scoreManager.StartScoring();

        if (uiManager != null)
            uiManager.NotifyResumed(true);
    }

    public void ReturnToMenuImmediate()
    {
        Time.timeScale = 1f;
        ForceMenuState();
    }

    public void OnPlayerDied()
    {
        if (CurrentState == GameState.GameOver)
            return;

        Time.timeScale = 1f;

        if (CurrentState != GameState.Playing)
        {
            Debug.LogWarning($"OnPlayerDied called while in {CurrentState}, ignoring.", this);
            return;
        }

        lastDeathPos = playerRoot ? playerRoot.position : Vector3.zero;

        if (scoreManager != null)
            scoreManager.StopScoring();

        SetTrailActive(false);
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

        SetTrailActive(false);
        FadeOceanTo(0f, oceanFadeOutDuration);

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

        SetTrailActive(true);
        CurrentState = GameState.Playing;
        transitionRoutine = null;
    }

    IEnumerator TransitionToMenuRoutine()
    {
        CurrentState = GameState.TransitionToMenu;
        SetUIState(false, false, false);

        SetTrailActive(false);
        if (scoreManager != null)
            scoreManager.StopScoring();

        if (playerController != null)
            playerController.PlaySwimSurface();

        ApplySurfaceCamera();
        // Keep gameplay view during the swim up (no blend on return).
        SwitchToGameplayCamera();

        Vector3 surfacePos = lastDeathPos + Vector3.up * surfaceHeightOffset;

        float duration = Mathf.Max(0.001f, surfaceDuration);
        Vector3 startPos = playerRoot ? playerRoot.position : surfacePos;
        Quaternion startRot = playerRoot ? playerRoot.rotation : Quaternion.identity;
        Quaternion targetRot = menuPose ? menuPose.rotation : Quaternion.identity;
        float fadeStartTime = duration * Mathf.Clamp01(surfaceFadeStartNormalized);
        float fadeStartHeight = surfacePos.y - Mathf.Max(0f, surfaceFadeStartDistance);
        bool oceanFadeStarted = false;

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
                if (!oceanFadeStarted)
                {
                    bool byTime = t >= fadeStartTime;
                    bool byHeight = surfaceFadeStartDistance > 0f && playerRoot.position.y >= fadeStartHeight;
                    if (byTime || byHeight)
                    {
                        FadeOceanTo(1f, oceanFadeInDuration);
                        oceanFadeStarted = true;
                    }
                }
                yield return null;
            }

            playerRoot.position = surfacePos;
            playerRoot.rotation = targetRot;
        }
        else
        {
            if (fadeStartTime > 0f)
                yield return new WaitForSeconds(fadeStartTime);
            FadeOceanTo(1f, oceanFadeInDuration);
            oceanFadeStarted = true;
            float remaining = Mathf.Max(0f, duration - fadeStartTime);
            if (remaining > 0f)
                yield return new WaitForSeconds(remaining);
        }

        if (!oceanFadeStarted)
            FadeOceanTo(1f, oceanFadeInDuration);
        RestoreSurfaceCamera();
        HardResetToMenu();
        transitionRoutine = null;
    }

    void HardResetToMenu()
    {
        bool shouldSyncUI = uiManager != null && CurrentState != GameState.MenuIdle;

        RestoreSurfaceCamera();
        SetOceanAlpha(1f);
        SetTrailActive(false);
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

        Time.timeScale = 1f;
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

    void CacheFollowCam()
    {
        if (!gameplayFollowCam && gameplayCameraGO)
            gameplayFollowCam = gameplayCameraGO.GetComponentInParent<FollowCam>();
        if (!gameplayFollowCam)
            gameplayFollowCam = FindObjectOfType<FollowCam>();
    }

    void CacheOcean()
    {
        if (!oceanRoot)
        {
            var oceanFollow = FindObjectOfType<InfiniteOceanFollow>();
            if (oceanFollow)
                oceanRoot = oceanFollow.gameObject;
            else
                oceanRoot = GameObject.Find("Ocean");
        }

        if (oceanRoot)
            oceanRenderers = oceanRoot.GetComponentsInChildren<Renderer>(true);
    }

    void ApplySurfaceCamera()
    {
        if (!overrideSurfaceCamera) return;
        if (!gameplayFollowCam) return;
        if (followCamOverridden) return;

        followCamState = new FollowCamState
        {
            useWorldOffset = gameplayFollowCam.useWorldOffset,
            lookAtTarget = gameplayFollowCam.lookAtTarget,
            worldOffset = gameplayFollowCam.worldOffset,
            localOffset = gameplayFollowCam.localOffset,
            follow = gameplayFollowCam.follow,
            turn = gameplayFollowCam.turn,
            lookHeight = gameplayFollowCam.lookHeight
        };

        gameplayFollowCam.useWorldOffset = true;
        gameplayFollowCam.lookAtTarget = true;
        gameplayFollowCam.worldOffset = surfaceCameraOffset;
        gameplayFollowCam.follow = surfaceCameraFollow;
        gameplayFollowCam.turn = surfaceCameraTurn;
        gameplayFollowCam.lookHeight = surfaceCameraLookHeight;
        followCamOverridden = true;
    }

    void RestoreSurfaceCamera()
    {
        if (!followCamOverridden) return;
        if (!gameplayFollowCam) return;

        gameplayFollowCam.useWorldOffset = followCamState.useWorldOffset;
        gameplayFollowCam.lookAtTarget = followCamState.lookAtTarget;
        gameplayFollowCam.worldOffset = followCamState.worldOffset;
        gameplayFollowCam.localOffset = followCamState.localOffset;
        gameplayFollowCam.follow = followCamState.follow;
        gameplayFollowCam.turn = followCamState.turn;
        gameplayFollowCam.lookHeight = followCamState.lookHeight;
        followCamOverridden = false;
    }

    void FadeOceanTo(float targetAlpha, float duration)
    {
        if (oceanRoot == null || oceanRenderers == null || oceanRenderers.Length == 0)
            CacheOcean();
        if (oceanRoot == null || oceanRenderers == null || oceanRenderers.Length == 0)
            return;

        if (oceanFadeRoutine != null)
            StopCoroutine(oceanFadeRoutine);

        oceanFadeRoutine = StartCoroutine(FadeOceanRoutine(Mathf.Clamp01(targetAlpha), duration));
    }

    IEnumerator FadeOceanRoutine(float targetAlpha, float duration)
    {
        float startAlpha = oceanAlpha;
        float t = 0f;
        float d = Mathf.Max(0.01f, duration);

        while (t < d)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / d);
            float a = Mathf.Lerp(startAlpha, targetAlpha, u);
            SetOceanAlpha(a);
            yield return null;
        }

        SetOceanAlpha(targetAlpha);
        oceanFadeRoutine = null;
    }

    void SetOceanAlpha(float alpha)
    {
        oceanAlpha = Mathf.Clamp01(alpha);

        if (oceanRoot == null || oceanRenderers == null || oceanRenderers.Length == 0)
            return;

        var block = new MaterialPropertyBlock();
        for (int i = 0; i < oceanRenderers.Length; i++)
        {
            var r = oceanRenderers[i];
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
                    c.a *= oceanAlpha;
                    block.SetColor("_BaseColor", c);
                }

                if (hasColor)
                {
                    Color c = mat.GetColor("_Color");
                    c.a *= oceanAlpha;
                    block.SetColor("_Color", c);
                }

                r.SetPropertyBlock(block, m);
            }
        }

        if (oceanAlpha >= 0.999f)
            ClearOceanOverrides();
    }

    void ClearOceanOverrides()
    {
        if (oceanRenderers == null) return;

        var block = new MaterialPropertyBlock();
        for (int i = 0; i < oceanRenderers.Length; i++)
        {
            var r = oceanRenderers[i];
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

    void ResolveTrailRoot()
    {
        if (playerTrailRoot || !playerRoot) return;

        var found = FindChildByName(playerRoot, "VFX_Trail_Ice");
        if (found)
            playerTrailRoot = found.gameObject;
    }

    void SetTrailActive(bool active)
    {
        if (!playerTrailRoot) return;
        if (playerTrailRoot.activeSelf == active) return;
        playerTrailRoot.SetActive(active);
    }

    Transform FindChildByName(Transform root, string childName)
    {
        if (!root) return null;

        int count = root.childCount;
        for (int i = 0; i < count; i++)
        {
            var child = root.GetChild(i);
            if (child.name == childName)
                return child;

            var found = FindChildByName(child, childName);
            if (found)
                return found;
        }

        return null;
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
