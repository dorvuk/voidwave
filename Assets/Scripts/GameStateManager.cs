using System;
using System.Collections;
using UnityEngine;

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

    [Header("Cameras")]
    public GameObject orbitCameraGO;
    public GameObject gameplayCameraGO;

    [Header("Player")]
    public Transform playerRoot;
    public Transform menuPose;

    [Header("Temporary Ocean")]
    public GameObject tempOceanPrefab;
    public float tempOceanHeightOffset = 2f;
    public float tempOceanLifetimeSafety = 10f;

    [Header("Gameplay Systems")]
    [Tooltip("Systems that should be toggled on only during gameplay (runner, generator, spawners, etc).")]
    public Behaviour[] gameplayBehavioursToEnable;
    [Tooltip("Optional: entire GameObjects to toggle instead of individual components (use if the inspector keeps picking the wrong component).")]
    public MonoBehaviour[] resettableSystems;

    [Header("Timings")]
    public float startDelay = 0.75f;
    public float surfaceDuration = 1.5f;
    [Tooltip("Exponent-style ease for transition movement. 1 = linear.")]
    public float transitionMoveEasing = 1f;

    const float startDiveOffset = -0.6f;

    public GameState CurrentState { get; private set; } = GameState.MenuIdle;

    GameObject tempOceanInstance;
    Coroutine transitionRoutine;
    Vector3 lastDeathPos;

    void Start()
    {
        ForceMenuState();
    }

    void OnDisable()
    {
        CleanupTempOcean();
    }

    public void OnPlayPressed()
    {
        if (CurrentState != GameState.MenuIdle)
        {
            Debug.LogWarning($"Play pressed while in {CurrentState}, ignoring.", this);
            return;
        }

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

        if (transitionRoutine != null) return;
        transitionRoutine = StartCoroutine(TransitionToMenuRoutine());
    }

    public void OnPlayerDied()
    {
        if (CurrentState != GameState.Playing)
        {
            Debug.LogWarning($"OnPlayerDied called while in {CurrentState}, ignoring.", this);
            return;
        }

        lastDeathPos = playerRoot ? playerRoot.position : Vector3.zero;

        SetGameplayBehavioursEnabled(false);
        SetUIState(false, false, true);

        CurrentState = GameState.GameOver;
    }

    IEnumerator TransitionToPlayRoutine()
    {
        CurrentState = GameState.TransitionToPlay;

        SwitchToGameplayCamera();
        SetUIState(false, false, false);

        ResetAllSystems();

        float delay = Mathf.Max(0f, startDelay);
        if (playerRoot && delay > 0f)
        {
            Vector3 from = playerRoot.position;
            Vector3 to = from + Vector3.up * startDiveOffset;

            float t = 0f;
            while (t < delay)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / delay);
                playerRoot.position = Vector3.Lerp(from, to, u);
                yield return null;
            }

            playerRoot.position = to;
        }
        else if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        SetGameplayBehavioursEnabled(true);
        SetUIState(false, true, false);

        CurrentState = GameState.Playing;
        transitionRoutine = null;
    }

    IEnumerator TransitionToMenuRoutine()
    {
        CurrentState = GameState.TransitionToMenu;
        SetUIState(false, false, false);

        SwitchToGameplayCamera(); // keep gameplay view during the swim up

        Vector3 surfacePos = lastDeathPos + Vector3.up * tempOceanHeightOffset;

        SpawnTempOcean(surfacePos);

        float duration = Mathf.Max(0.001f, surfaceDuration);
        Vector3 startPos = playerRoot ? playerRoot.position : surfacePos;
        Quaternion startRot = playerRoot ? playerRoot.rotation : Quaternion.identity;
        Quaternion targetRot = menuPose ? menuPose.rotation : Quaternion.identity;

        try
        {
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
        }
        finally
        {
            CleanupTempOcean();
        }

        HardResetToMenu();
        transitionRoutine = null;
    }

    void HardResetToMenu()
    {
        SetGameplayBehavioursEnabled(false);
        ResetAllSystems();
        ResetPlayerToMenuPose();

        SwitchToOrbitCamera();
        SetUIState(true, false, false);

        CurrentState = GameState.MenuIdle;
    }

    void ForceMenuState()
    {
        if (transitionRoutine != null)
        {
            StopCoroutine(transitionRoutine);
            transitionRoutine = null;
        }

        HardResetToMenu();
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
        if (resettableSystems == null || resettableSystems.Length == 0) return;

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
    }

    void SetUIState(bool showMenu, bool showHud, bool showGameOver)
    {
        if (menuUIRoot) menuUIRoot.SetActive(showMenu);
        if (hudUIRoot) hudUIRoot.SetActive(showHud);
        if (gameOverUIRoot) gameOverUIRoot.SetActive(showGameOver);
    }

    void SwitchToOrbitCamera()
    {
        SetCameraActive(orbitCameraGO, true);
        SetCameraActive(gameplayCameraGO, false);
    }

    void SwitchToGameplayCamera()
    {
        SetCameraActive(orbitCameraGO, false);
        SetCameraActive(gameplayCameraGO, true);
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

    void SpawnTempOcean(Vector3 surfacePos)
    {
        CleanupTempOcean();

        if (tempOceanPrefab)
        {
            tempOceanInstance = Instantiate(tempOceanPrefab, surfacePos, Quaternion.identity);
        }
        else
        {
            tempOceanInstance = GameObject.CreatePrimitive(PrimitiveType.Plane);
            tempOceanInstance.name = "TempOcean";
            tempOceanInstance.transform.SetPositionAndRotation(surfacePos, Quaternion.identity);
            tempOceanInstance.transform.localScale = Vector3.one;

            var col = tempOceanInstance.GetComponent<Collider>();
            if (col) col.enabled = false;
        }

        if (tempOceanInstance && tempOceanLifetimeSafety > 0f)
        {
            Destroy(tempOceanInstance, tempOceanLifetimeSafety);
        }
    }

    void CleanupTempOcean()
    {
        if (tempOceanInstance)
        {
            Destroy(tempOceanInstance);
            tempOceanInstance = null;
        }
    }

    float EaseMove(float t)
    {
        t = Mathf.Clamp01(t);
        if (Mathf.Approximately(transitionMoveEasing, 1f)) return t;

        float p = Mathf.Max(0.001f, transitionMoveEasing);
        return Mathf.Pow(t, p);
    }
}
