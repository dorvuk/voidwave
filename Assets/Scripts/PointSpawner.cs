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

    [Header("Spawn Range")]
    public float spawnStart = 30f;
    public float spawnUpTo = 160f;
    public float despawnBehind = 40f;

    [Header("Spacing")]
    public float minSpacing = 8f;
    public float maxSpacing = 14f;

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

    void Start()
    {
        if (runner) laneWidth = runner.laneWidth;
        nextSpawnS = runner ? runner.DistanceOnTrack + spawnStart : spawnStart;
    }

    void Update()
    {
        if (!track || !generator || !runner || !pointPrefab) return;

        float sPlayer = runner.DistanceOnTrack;
        if (runner) laneWidth = runner.laneWidth;

        if (nextSpawnS < sPlayer + spawnStart) nextSpawnS = sPlayer + spawnStart;
        float maxRange = sPlayer + spawnUpTo;

        float spacingMin = Mathf.Max(1f, Mathf.Min(minSpacing, maxSpacing));
        float spacingMax = Mathf.Max(spacingMin, Mathf.Max(minSpacing, maxSpacing));

        while (nextSpawnS < maxRange)
        {
            SpawnPoint(nextSpawnS, RandomLane());
            nextSpawnS += Random.Range(spacingMin, spacingMax);
        }

        UpdateActive(sPlayer);
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
        for (int i = active.Count - 1; i >= 0; i--)
        {
            var p = active[i];

            // Collected point gets destroyed -> remove
            if (p.go == null)
            {
                active.RemoveAt(i);
                continue;
            }

            // Too far behind -> despawn (pool it)
            if (p.sGlobal < sPlayer - despawnBehind)
            {
                Release(p.go);
                active.RemoveAt(i);
                continue;
            }

            UpdatePointTransform(p);
        }
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

