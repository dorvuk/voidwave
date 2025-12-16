using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

public class ObstacleSpawner : MonoBehaviour
{
    public SplineContainer track;
    public SplineGenerator generator;
    public TrackRunner runner;
    public Obstacle obstaclePrefab;

    [Header("Spawn Range")]
    public float spawnStart = 40f;
    public float spawnUpTo = 160f;
    public float despawnBehind = 40f;

    [Header("Spacing")]
    public float minSpacing = 14f;
    public float maxSpacing = 22f;

    [Header("Lanes")]
    public int lanes = 2;
    public float laneWidth = 2.2f;
    public float hover = 0.15f;

    readonly List<Obstacle> pool = new();
    readonly List<Obstacle> active = new();
    float nextSpawnS;

    void Start()
    {
        if (runner) laneWidth = runner.laneWidth;
        nextSpawnS = runner ? runner.DistanceOnTrack + spawnStart : spawnStart;
    }

    void Update()
    {
        if (!track || !generator || !runner || !obstaclePrefab) return;

        float sPlayer = runner.DistanceOnTrack;
        if (runner) laneWidth = runner.laneWidth;

        if (nextSpawnS < sPlayer + spawnStart) nextSpawnS = sPlayer + spawnStart;
        float maxRange = sPlayer + spawnUpTo;

        float spacingMin = Mathf.Max(1f, Mathf.Min(minSpacing, maxSpacing));
        float spacingMax = Mathf.Max(spacingMin, Mathf.Max(minSpacing, maxSpacing));

        while (nextSpawnS < maxRange)
        {
            SpawnObstacle(nextSpawnS, RandomLane());
            float spacing = Random.Range(spacingMin, spacingMax);
            nextSpawnS += spacing;
        }

        UpdateActive(sPlayer);
    }

    void SpawnObstacle(float sGlobal, int lane)
    {
        var obs = GetFromPool();
        obs.Init(sGlobal, lane);
        obs.gameObject.SetActive(true);
        active.Add(obs);
        UpdateObstacleTransform(obs);
    }

    void UpdateActive(float sPlayer)
    {
        for (int i = active.Count - 1; i >= 0; i--)
        {
            var obs = active[i];

            if (obs.SGlobal < sPlayer - despawnBehind)
            {
                Release(obs);
                active.RemoveAt(i);
                continue;
            }

            UpdateObstacleTransform(obs);
        }
    }

    void UpdateObstacleTransform(Obstacle obs)
    {
        float sLocal = Mathf.Max(0f, obs.SGlobal - generator.RemovedDistance);

        Vector3 pos = default, fwd = default, up = default, right = default;
        Vector3 tmpUp = Vector3.up;
        TrackSample.At(track, sLocal, track.Spline.Closed, ref pos, ref fwd, ref up, ref right, ref tmpUp);

        float offset = LaneOffset(obs.Lane);
        Vector3 worldPos = pos + right * offset + up * hover;
        Quaternion rot = Quaternion.LookRotation(fwd, up);

        obs.transform.SetPositionAndRotation(worldPos, rot);
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

    Obstacle GetFromPool()
    {
        if (pool.Count > 0)
        {
            int last = pool.Count - 1;
            var obs = pool[last];
            pool.RemoveAt(last);
            return obs;
        }

        var inst = Instantiate(obstaclePrefab, transform);
        inst.gameObject.SetActive(false);
        return inst;
    }

    void Release(Obstacle obs)
    {
        obs.gameObject.SetActive(false);
        pool.Add(obs);
    }
}
