using System;
using UnityEngine;
using UnityEngine.Splines;

[RequireComponent(typeof(CharacterController))]
public class TrackRunner : MonoBehaviour, IRunResettable
{
    public SplineContainer track;
    public SplineGenerator generator;

    public float speed = 18f;

    [Header("Speed Ramp (Soft Cap)")]
    public float startSpeed = 18f;
    public float maxSpeed = 38f;
    public float rampRate = 0.12f;
    public bool useRampCurve = true;
    public float rampDuration = 60f;
    public AnimationCurve rampCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Reset")]
    [Tooltip("If false, ResetRun will reset internal state without snapping the transform to the track start.")]
    public bool snapToStartOnReset = true;

    public float laneWidth = 2.2f;

    public float steer = 8f;
    public float steerDamp = 10f;

    public float rollMax = 22f;
    public float rollFromCurve = 0.6f;

    public bool loopIfClosed = true;
    public float verticalSmooth = 8f;
    public float hover = 0.25f;
    public float ySmooth = 12f;
    public float jumpHeight = 1.6f;
    public float gravity = 30f;
    public float laneSwitchSmooth = 10f;
    public float rotationSmooth = 12f;

    float jumpVel;
    float jumpOffset;
    int lane = 0;
    float yVel;
    [Range(0f, 1f)] public float alignToTrack = 1f;

    CharacterController cc;

    float s;
    float x;
    float xVel;

    float runTime;

    public event Action<Obstacle> ObstacleHit;

    Vector3 lastUp = Vector3.up;

    void Awake()
    {
        cc = GetComponent<CharacterController>();
    }

    void Start()
    {
        if (!track) return;
        if (!generator) generator = track.GetComponent<SplineGenerator>();
        s = 0f;
        lane = 0;
        x = LaneOffset(lane);

        runTime = 0f;
        speed = startSpeed;
    }

    void Update()
    {
        if (!track) return;
        if (!generator) generator = track.GetComponent<SplineGenerator>();

        float len = track.Spline.GetLength();
        if (len <= 0.01f) return;

        runTime += Time.deltaTime;
        speed = EvaluateSpeed(runTime);

        s += speed * Time.deltaTime;

        bool doLoop = loopIfClosed && track.Spline.Closed;
        if (doLoop) s = Mathf.Repeat(s, len);

        float removed = generator ? generator.RemovedDistance : 0f;
        float sLocal = Mathf.Max(0f, s - removed);

        Vector3 pos = default, fwd = default, up = default, right = default;
        TrackSample.At(track, sLocal, doLoop, ref pos, ref fwd, ref up, ref right, ref lastUp);

        HandleLaneInput();
        float targetX = LaneOffset(lane);
        float laneSmooth = laneSwitchSmooth > 0f ? laneSwitchSmooth : steerDamp;
        x = Mathf.SmoothDamp(x, targetX, ref xVel, 1f / Mathf.Max(0.001f, laneSmooth));

        Vector3 wantPos = pos + right * x + up * hover;
        UpdateJump();
        wantPos += up * jumpOffset;

        float steerRoll = Mathf.Clamp(x / Mathf.Max(0.001f, laneWidth), -1f, 1f) * rollMax;

        float ahead = 1.0f;
        float s2 = doLoop ? Mathf.Repeat(s + ahead, len) : Mathf.Max(0f, s + ahead - removed);

        Vector3 pos2 = default, fwd2 = default, up2 = default, right2 = default;
        Vector3 tmpUp = lastUp;
        TrackSample.At(track, s2, doLoop, ref pos2, ref fwd2, ref up2, ref right2, ref tmpUp);

        float turnSign = Mathf.Sign(Vector3.Dot(Vector3.Cross(fwd, fwd2), up));
        float curveAmount = Vector3.Angle(fwd, fwd2);
        float curveRoll = turnSign * curveAmount * rollFromCurve;

        float align = Mathf.Clamp01(alignToTrack);
        Vector3 blendedUp = Vector3.Slerp(Vector3.up, up, align);

        Quaternion look = Quaternion.LookRotation(fwd, blendedUp);
        Quaternion roll = Quaternion.AngleAxis(steerRoll + curveRoll, Vector3.forward);
        Quaternion targetRot = look * roll;

        Vector3 cur = transform.position;
        float y = Mathf.SmoothDamp(cur.y, wantPos.y, ref yVel, 1f / Mathf.Max(0.001f, ySmooth));
        Vector3 targetPos = new Vector3(wantPos.x, y, wantPos.z);

        Vector3 delta = targetPos - transform.position;
        cc.Move(delta);

        float rotLerp = 1f - Mathf.Exp(-Mathf.Max(0.001f, rotationSmooth) * Time.deltaTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotLerp);
    }

    float EvaluateSpeed(float elapsedSeconds)
    {
        float t = Mathf.Max(0f, elapsedSeconds);

        if (useRampCurve && rampDuration > 0f)
        {
            float u = Mathf.Clamp01(t / rampDuration);
            float k = Mathf.Clamp01(rampCurve != null ? rampCurve.Evaluate(u) : u);
            return Mathf.Lerp(startSpeed, maxSpeed, k);
        }

        return maxSpeed - (maxSpeed - startSpeed) * Mathf.Exp(-rampRate * t);
    }

    public float DistanceOnTrack => s;

    float LaneOffset(int laneIndex)
    {
        float half = laneWidth * 0.5f;
        return laneIndex == 0 ? -half : half;
    }

    void HandleLaneInput()
    {
        float h = Input.GetAxisRaw("Horizontal");
        if (h > 0.5f) lane = 1;
        else if (h < -0.5f) lane = 0;
    }

    void UpdateJump()
    {
        bool wantJump = Input.GetButtonDown("Jump");

        if (wantJump && Mathf.Approximately(jumpOffset, 0f))
        {
            jumpVel = Mathf.Sqrt(2f * gravity * Mathf.Max(0.001f, jumpHeight));
        }

        jumpVel += -gravity * Time.deltaTime;
        jumpOffset += jumpVel * Time.deltaTime;

        if (jumpOffset < 0f)
        {
            jumpOffset = 0f;
            jumpVel = 0f;
        }
    }

    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        var obstacle = hit.collider.GetComponentInParent<Obstacle>();
        if (obstacle && obstacle.TryConsumeHit())
        {
            ObstacleHit?.Invoke(obstacle);
            Debug.Log($"Hit obstacle at s={obstacle.SGlobal:F1} lane={obstacle.Lane}");
        }
    }

    void OnTriggerEnter(Collider other)
    {
        var obstacle = other.GetComponentInParent<Obstacle>();
        if (obstacle && obstacle.TryConsumeHit())
        {
            ObstacleHit?.Invoke(obstacle);
            Debug.Log($"Hit obstacle at s={obstacle.SGlobal:F1} lane={obstacle.Lane}");
        }
    }

    public void ResetRun()
    {
        if (!track) return;
        if (!cc) cc = GetComponent<CharacterController>();
        if (!generator) generator = track.GetComponent<SplineGenerator>();

        s = 0f;
        runTime = 0f;
        speed = startSpeed;

        lane = 0;
        x = LaneOffset(lane);
        xVel = 0f;
        yVel = 0f;
        jumpVel = 0f;
        jumpOffset = 0f;
        lastUp = Vector3.up;

        Vector3 pos = default, fwd = default, up = default, right = default;
        TrackSample.At(track, 0f, track.Spline.Closed, ref pos, ref fwd, ref up, ref right, ref lastUp);

        Vector3 startPos = pos + right * x + up * hover;
        Quaternion startRot = Quaternion.LookRotation(fwd, up);

        if (snapToStartOnReset)
        {
            if (cc)
            {
                bool wasEnabled = cc.enabled;
                cc.enabled = false;
                transform.SetPositionAndRotation(startPos, startRot);
                cc.enabled = wasEnabled;
            }
            else
            {
                transform.SetPositionAndRotation(startPos, startRot);
            }
        }
    }
}
