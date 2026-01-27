using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

public class PointSpawner : MonoBehaviour, IRunResettable
{
    public SplineContainer track;
    public SplineGenerator generator;
    public TrackRunner runner;
    public GameObject pointPrefab;
    public ScoreManager scoreManager;

    [Header("Spawn Range")]
    public float spawnStart = 30f;
    public float spawnUpTo = 160f;
    public float despawnBehind = 40f;

    [Header("Spacing")]
    public float minSpacing = 8f;
    public float maxSpacing = 14f;

    [Header("Safety")]
    public float spawnSafetyMargin = 5f;

    [Header("Avoidance")]
    public ObstacleSpawner[] obstacleSpawners;
    public float obstacleAvoidDistance = 6f;
    public int lanePickTries = 4;

    [Header("Lanes")]
    public int lanes = 2;
    public float laneWidth = 2.2f;
    public float hover = 0.5f;

    class ActivePoint
    {
        public GameObject go;
        public float sGlobal;
        public int lane;
    }

    readonly List<GameObject> pool = new();
    readonly List<ActivePoint> active = new();
    float nextSpawnS;
    bool hasWarnedMissingRefs;

    void Awake()
    {
        ResolveReferences();
    }

    void Start()
    {
        ResolveReferences();
        if (runner) laneWidth = runner.laneWidth;
        nextSpawnS = runner ? runner.DistanceOnTrack + spawnStart : spawnStart;
    }

    void Update()
    {
        if (!track || !generator || !runner || !pointPrefab)
        {
            ResolveReferences();
            if (!track || !generator || !runner || !pointPrefab) return;
        }

        float sPlayer = runner.DistanceOnTrack;
        if (runner) laneWidth = runner.laneWidth;

        float safetyMargin = Mathf.Max(0f, spawnSafetyMargin);
        float lookAhead = generator ? Mathf.Max(0f, generator.lookAhead) : 0f;
        float effectiveSpawnStart = spawnStart;
        float effectiveSpawnUpTo = spawnUpTo;

        if (lookAhead > 0f)
        {
            float maxLookAhead = Mathf.Max(0f, lookAhead - safetyMargin);
            effectiveSpawnStart = Mathf.Min(spawnStart, maxLookAhead);
            effectiveSpawnUpTo = Mathf.Min(spawnUpTo, maxLookAhead);
        }

        if (effectiveSpawnUpTo < effectiveSpawnStart)
            effectiveSpawnUpTo = effectiveSpawnStart;

        if (nextSpawnS < sPlayer + effectiveSpawnStart) nextSpawnS = sPlayer + effectiveSpawnStart;
        float splineLength = track.Spline.GetLength();
        if (splineLength <= 0.01f) return;

        float maxSplineS = generator.RemovedDistance + splineLength - safetyMargin;
        float maxRange = Mathf.Min(sPlayer + effectiveSpawnUpTo, maxSplineS);
        if (maxRange <= nextSpawnS) return;

        float spacingMin = Mathf.Max(1f, Mathf.Min(minSpacing, maxSpacing));
        float spacingMax = Mathf.Max(spacingMin, Mathf.Max(minSpacing, maxSpacing));

        while (nextSpawnS < maxRange)
        {
            int lane = PickLane(nextSpawnS);
            if (lane >= 0)
                SpawnPoint(nextSpawnS, lane);
            nextSpawnS += Random.Range(spacingMin, spacingMax);
        }

        UpdateActive(sPlayer);
    }

    void ResolveReferences()
    {
        if (!runner)
            runner = GetComponentInParent<TrackRunner>() ?? FindObjectOfType<TrackRunner>();

        if (!track)
            track = runner ? runner.track : GetComponentInParent<SplineContainer>();

        if (!track)
            track = FindObjectOfType<SplineContainer>();

        if (!generator)
            generator = runner ? runner.generator : null;

        if (!generator && track)
            generator = track.GetComponent<SplineGenerator>();

        if (!generator)
            generator = GetComponentInParent<SplineGenerator>() ?? FindObjectOfType<SplineGenerator>();

        if (!scoreManager)
            scoreManager = FindObjectOfType<ScoreManager>();

        if (obstacleSpawners == null || obstacleSpawners.Length == 0)
            obstacleSpawners = FindObjectsOfType<ObstacleSpawner>();

        if (!hasWarnedMissingRefs && Application.isPlaying &&
            (!track || !generator || !runner || !pointPrefab))
        {
            hasWarnedMissingRefs = true;
            Debug.LogWarning("PointSpawner is missing references (track/generator/runner/pointPrefab).", this);
        }
    }

    void SpawnPoint(float sGlobal, int lane)
    {
        var go = GetFromPool();
        go.transform.SetParent(transform, false);
        go.SetActive(true);

        var p = new ActivePoint { go = go, sGlobal = sGlobal, lane = lane };
        active.Add(p);

        UpdatePointTransform(p);
    }

    void UpdateActive(float sPlayer)
    {
        bool missedAny = false;

        for (int i = active.Count - 1; i >= 0; i--)
        {
            var p = active[i];

            // Collected point gets disabled or destroyed -> remove
            if (p.go == null)
            {
                active.RemoveAt(i);
                continue;
            }

            if (!p.go.activeSelf)
            {
                Release(p.go);
                active.RemoveAt(i);
                continue;
            }

            // Too far behind -> despawn (pool it)
            if (p.sGlobal < sPlayer - despawnBehind)
            {
                missedAny = true;
                Release(p.go);
                active.RemoveAt(i);
                continue;
            }

            UpdatePointTransform(p);
        }

        if (missedAny && scoreManager != null)
            scoreManager.ResetCounter();
    }

    void UpdatePointTransform(ActivePoint p)
    {
        float sLocal = Mathf.Max(0f, p.sGlobal - generator.RemovedDistance);

        Vector3 pos = default, fwd = default, up = default, right = default;
        Vector3 tmpUp = Vector3.up;

        TrackSample.At(track, sLocal, track.Spline.Closed, ref pos, ref fwd, ref up, ref right, ref tmpUp);

        float offset = LaneOffset(p.lane);
        Vector3 worldPos = pos + right * offset + up * hover;

        Quaternion rot = Quaternion.LookRotation(fwd, up);
        p.go.transform.SetPositionAndRotation(worldPos, rot);
    }

    float LaneOffset(int laneIndex)
    {
        float center = (lanes - 1) * 0.5f;
        return (laneIndex - center) * laneWidth;
    }

    int RandomLane()
    {
        if (lanes <= 1) return 0;
        return Random.Range(0, lanes);
    }

    int PickLane(float sGlobal)
    {
        int availableLanes = Mathf.Max(1, lanes);
        if (availableLanes == 1)
            return IsLaneBlocked(sGlobal, 0) ? -1 : 0;

        int tries = Mathf.Max(1, lanePickTries);
        for (int i = 0; i < tries; i++)
        {
            int lane = Random.Range(0, availableLanes);
            if (!IsLaneBlocked(sGlobal, lane))
                return lane;
        }

        for (int lane = 0; lane < availableLanes; lane++)
        {
            if (!IsLaneBlocked(sGlobal, lane))
                return lane;
        }

        return -1;
    }

    bool IsLaneBlocked(float sGlobal, int lane)
    {
        float avoid = Mathf.Max(0f, obstacleAvoidDistance);
        if (avoid <= 0f) return false;

        if (obstacleSpawners == null || obstacleSpawners.Length == 0)
            return false;

        foreach (var spawner in obstacleSpawners)
        {
            if (!spawner) continue;
            if (spawner.IsObstacleNear(sGlobal, lane, avoid)) return true;
        }

        return false;
    }

    GameObject GetFromPool()
    {
        if (pool.Count > 0)
        {
            int last = pool.Count - 1;
            var go = pool[last];
            pool.RemoveAt(last);
            return go;
        }

        var inst = Instantiate(pointPrefab);
        inst.SetActive(false);
        return inst;
    }

    void Release(GameObject go)
    {
        if (go == null) return;
        go.SetActive(false);
        pool.Add(go);
    }

    public void ResetRun()
    {
        for (int i = active.Count - 1; i >= 0; i--)
        {
            var p = active[i];
            if (p.go != null) Release(p.go);
            active.RemoveAt(i);
        }

        if (runner) laneWidth = runner.laneWidth;
        nextSpawnS = runner ? runner.DistanceOnTrack + spawnStart : spawnStart;
    }
}

