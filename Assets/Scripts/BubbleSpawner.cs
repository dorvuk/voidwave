using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

public class BubbleSpawner : MonoBehaviour, IRunResettable
{
    public SplineContainer track;
    public SplineGenerator generator;
    public TrackRunner runner;
    public ParticleSystem bubblePrefab;

    [Header("Spawn Range")]
    public float spawnStart = 40f;
    public float spawnUpTo = 180f;
    public float despawnBehind = 60f;
    public float spawnSafetyMargin = 5f;

    [Header("Spacing")]
    public float minSpacing = 30f;
    public float maxSpacing = 50f;

    [Header("Ring Around Track")]
    public Vector2 radiusRange = new Vector2(3f, 8f);
    [Range(0f, 1f)] public float verticalScale = 1f;
    public Vector2 angleRange = new Vector2(0f, 360f);

    [Header("Orientation")]
    public bool alignToTrack = true;
    public bool alignXAxisToTrackForward = true;
    public float randomYaw = 0f;

    [Header("Runtime")]
    public bool updateEveryFrame = true;

    class ActiveBubble
    {
        public ParticleSystem ps;
        public float sGlobal;
        public float angleRad;
        public float radius;
        public float yaw;
    }

    readonly List<ParticleSystem> pool = new();
    readonly List<ActiveBubble> active = new();
    float nextSpawnS;
    bool hasWarnedMissingRefs;

    void Awake()
    {
        ResolveReferences();
    }

    void Start()
    {
        ResolveReferences();
        nextSpawnS = runner ? runner.DistanceOnTrack + spawnStart : spawnStart;
    }

    void Update()
    {
        if (!track || !generator || !runner || !bubblePrefab)
        {
            ResolveReferences();
            if (!track || !generator || !runner || !bubblePrefab) return;
        }

        float sPlayer = runner.DistanceOnTrack;

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
            SpawnBubble(nextSpawnS);
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

        if (!hasWarnedMissingRefs && Application.isPlaying &&
            (!track || !generator || !runner || !bubblePrefab))
        {
            hasWarnedMissingRefs = true;
            Debug.LogWarning("BubbleSpawner is missing references (track/generator/runner/bubblePrefab).", this);
        }
    }

    void SpawnBubble(float sGlobal)
    {
        var ps = GetFromPool();
        ps.transform.SetParent(transform, false);
        ps.gameObject.SetActive(true);
        ps.Clear(true);
        ps.Play(true);

        float angleMin = Mathf.Min(angleRange.x, angleRange.y);
        float angleMax = Mathf.Max(angleRange.x, angleRange.y);
        float angleDeg = Random.Range(angleMin, angleMax);
        float radius = Random.Range(Mathf.Min(radiusRange.x, radiusRange.y), Mathf.Max(radiusRange.x, radiusRange.y));
        float yaw = randomYaw > 0f ? Random.Range(-randomYaw, randomYaw) : 0f;

        var bubble = new ActiveBubble
        {
            ps = ps,
            sGlobal = sGlobal,
            angleRad = angleDeg * Mathf.Deg2Rad,
            radius = radius,
            yaw = yaw
        };

        active.Add(bubble);
        UpdateBubbleTransform(bubble);
    }

    void UpdateActive(float sPlayer)
    {
        for (int i = active.Count - 1; i >= 0; i--)
        {
            var bubble = active[i];

            if (bubble.ps == null)
            {
                active.RemoveAt(i);
                continue;
            }

            if (bubble.sGlobal < sPlayer - despawnBehind)
            {
                Release(bubble.ps);
                active.RemoveAt(i);
                continue;
            }

            if (updateEveryFrame)
                UpdateBubbleTransform(bubble);
        }
    }

    void UpdateBubbleTransform(ActiveBubble bubble)
    {
        float sLocal = Mathf.Max(0f, bubble.sGlobal - generator.RemovedDistance);

        Vector3 pos = default, fwd = default, up = default, right = default;
        Vector3 tmpUp = Vector3.up;
        TrackSample.At(track, sLocal, track.Spline.Closed, ref pos, ref fwd, ref up, ref right, ref tmpUp);

        float c = Mathf.Cos(bubble.angleRad);
        float s = Mathf.Sin(bubble.angleRad) * Mathf.Clamp01(verticalScale);
        Vector3 offset = right * (c * bubble.radius) + up * (s * bubble.radius);
        Vector3 worldPos = pos + offset;

        Quaternion rot = Quaternion.identity;
        if (alignToTrack)
        {
            if (alignXAxisToTrackForward)
            {
                Vector3 rightAxis = fwd.normalized;
                Vector3 upAxis = up.normalized;
                Vector3 forwardAxis = Vector3.Cross(rightAxis, upAxis).normalized;
                if (forwardAxis.sqrMagnitude < 0.0001f)
                    forwardAxis = Vector3.Cross(rightAxis, Vector3.up).normalized;
                rot = Quaternion.LookRotation(forwardAxis, upAxis);
            }
            else
            {
                rot = Quaternion.LookRotation(fwd, up);
            }

            if (Mathf.Abs(bubble.yaw) > 0.01f)
                rot = Quaternion.AngleAxis(bubble.yaw, up) * rot;
        }

        bubble.ps.transform.SetPositionAndRotation(worldPos, rot);
    }

    ParticleSystem GetFromPool()
    {
        if (pool.Count > 0)
        {
            int last = pool.Count - 1;
            var ps = pool[last];
            pool.RemoveAt(last);
            return ps;
        }

        var inst = Instantiate(bubblePrefab, transform);
        inst.gameObject.SetActive(false);
        return inst;
    }

    void Release(ParticleSystem ps)
    {
        if (ps == null) return;
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ps.gameObject.SetActive(false);
        pool.Add(ps);
    }

    public void ResetRun()
    {
        for (int i = active.Count - 1; i >= 0; i--)
        {
            var bubble = active[i];
            if (bubble.ps != null) Release(bubble.ps);
            active.RemoveAt(i);
        }

        nextSpawnS = runner ? runner.DistanceOnTrack + spawnStart : spawnStart;
    }
}
