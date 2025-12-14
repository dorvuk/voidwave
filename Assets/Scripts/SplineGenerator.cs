using UnityEngine;
using UnityEngine.Splines;

[RequireComponent(typeof(SplineContainer))]
public class SplineGenerator : MonoBehaviour
{
    public Transform player;

    [Header("Spacing")]
    public float step = 8f;              // distance between knots
    public int startPoints = 30;

    [Header("Style (mimic reference)")]
    public float turnDegrees = 70f;      // max yaw swing (bigger = wider S curves)
    public float turnScale = 0.015f;     // smaller = slower changes (longer arcs)
    public float heightAmplitude = 8f;   // vertical range
    public float heightScale = 0.008f;   // smaller = slower hills
    public float heightFollow = 0.08f;   // low-pass on vertical

    [Header("Smoothness")]
    public float tangentScale = 0.6f;    // tangent length relative to step

    [Header("Runtime")]
    public float lookAhead = 240f;       // how much track ahead
    public float trimBehind = 120f;      // how much behind to keep
    public int safetyMinKnots = 10;

    Spline spline;
    float seedA, seedB;
    float nextDistance;                  // cumulative distance used for noise
    float ySmoothed;

    public float RemovedDistance { get; private set; }

    void Awake()
    {
        spline = GetComponent<SplineContainer>().Spline;
    }

    void Start()
    {
        if (!player)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p) player = p.transform;
        }

        seedA = Random.Range(0f, 9999f);
        seedB = Random.Range(0f, 9999f);

        BuildInitial();
    }

    void BuildInitial()
    {
        spline.Clear();
        RemovedDistance = 0f;
        nextDistance = 0f;
        ySmoothed = 0f;

        Vector3 p = Vector3.zero;
        AddKnot(p, nextDistance);
        nextDistance += step;

        for (int i = 1; i < startPoints; i++)
        {
            p = NextPoint(p, nextDistance);
            AddKnot(p, nextDistance);
            nextDistance += step;
        }
    }

    void Update()
    {
        if (!player) return;

        var runner = player.GetComponent<TrackRunner>();
        if (!runner) return;

        float sPlayer = runner.DistanceOnTrack;

        ExtendAhead(sPlayer);
        TrimBehind(sPlayer);
    }

    void ExtendAhead(float sPlayer)
    {
        while (RemovedDistance + spline.GetLength() < sPlayer + lookAhead)
        {
            Vector3 from = spline[^1].Position;
            Vector3 next = NextPoint(from, nextDistance);
            AddKnot(next, nextDistance);

            nextDistance += step;
        }
    }

    void TrimBehind(float sPlayer)
    {
        if (spline.Count <= safetyMinKnots) return;

        float localDistance = sPlayer - RemovedDistance;
        if (localDistance <= trimBehind) return;

        int removeCount = Mathf.FloorToInt((localDistance - trimBehind) / Mathf.Max(0.001f, step));
        removeCount = Mathf.Min(removeCount, Mathf.Max(0, spline.Count - safetyMinKnots));

        if (removeCount <= 0) return;

        float beforeLength = spline.GetLength();
        for (int i = 0; i < removeCount; i++) spline.RemoveAt(0);

        float afterLength = spline.GetLength();
        RemovedDistance += Mathf.Max(0f, beforeLength - afterLength);
    }

    Vector3 NextPoint(Vector3 from, float s)
    {
        // smooth heading and height based on distance
        float yaw = (Mathf.PerlinNoise(seedA, s * turnScale) * 2f - 1f) * turnDegrees;
        float h = (Mathf.PerlinNoise(seedB, s * heightScale) * 2f - 1f) * heightAmplitude;

        Vector3 dir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
        Vector3 p = from + dir.normalized * step;
        ySmoothed = Mathf.Lerp(ySmoothed, h, heightFollow);
        p.y = ySmoothed;
        return p;
    }

    void AddKnot(Vector3 pos, float s)
    {
        float yaw = (Mathf.PerlinNoise(seedA, s * turnScale) * 2f - 1f) * turnDegrees;
        Vector3 dir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;

        Vector3 tan = dir.normalized * (step * tangentScale);

        var k = new BezierKnot(pos)
        {
            TangentIn = -tan,
            TangentOut = tan
        };
        spline.Add(k);
    }
}
