using UnityEngine;

public class MenuCamOrbit : MonoBehaviour
{
    [SerializeField] Transform target;

    [Header("Orbit")]
    [SerializeField] float radius = 6f;
    [SerializeField] float height = 2f;
    [SerializeField] float turnSpeed = 10f; // deg/sec

    [Header("Look")]
    [SerializeField] float lookHeight = 1.2f;
    [SerializeField] float posSmooth = 6f;
    [SerializeField] float rotSmooth = 10f;

    [Header("Little sky peeks")]
    [SerializeField] float sidePeek = 0.5f;
    [SerializeField] float upPeek = 0.7f;
    [SerializeField] Vector2 peekEvery = new Vector2(3.5f, 7f);
    [SerializeField] Vector2 peekLasts = new Vector2(1.5f, 3f);

    [Range(0f, 1f)]
    [SerializeField] float keepCentered = 0.75f;

    [Header("Keep player visible")]
    [SerializeField] float aimClamp = 18f;

    float angle;
    float nextPeekAt;
    float peekStart;
    float peekTime;

    Vector2 peekNow;
    Vector2 peekFrom;
    Vector2 peekTo;

    void Start()
    {
        if (!target)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p) target = p.transform;
        }

        // start from whatever angle the camera already is at
        if (target)
        {
            var flat = transform.position - target.position;
            flat.y = 0f;
            if (flat.sqrMagnitude > 0.001f)
                angle = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;
        }

        nextPeekAt = Time.time + Random.Range(peekEvery.x, peekEvery.y);
    }

    void LateUpdate()
    {
        if (!target) return;

        angle += turnSpeed * Time.deltaTime;

        if (Time.time >= nextPeekAt && peekTime <= 0f)
            BeginPeek();

        if (peekTime > 0f)
            UpdatePeek();

        var rot = Quaternion.Euler(0f, angle, 0f);
        var desiredPos = target.position + rot * new Vector3(0f, 0f, -radius) + Vector3.up * height;

        // smooth follow (simple lerp feels menu-friendly)
        float p = 1f - Mathf.Exp(-posSmooth * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, desiredPos, p);

        var baseLook = target.position + Vector3.up * lookHeight;

        // drift is applied relative to camera axes so it feels like a gentle "pan"
        var drift = transform.right * peekNow.x + transform.up * peekNow.y;
        var look = Vector3.Lerp(baseLook + drift, baseLook, keepCentered);

        // clamp aim so we don't wander too far from target direction
        var toTarget = (baseLook - transform.position).normalized;
        var toLook = (look - transform.position).normalized;

        float a = Vector3.Angle(toTarget, toLook);
        if (a > aimClamp)
        {
            float t = aimClamp / Mathf.Max(0.001f, a);
            var dir = Vector3.Slerp(toTarget, toLook, t);
            look = transform.position + dir * 10f;
        }

        var wantRot = Quaternion.LookRotation((look - transform.position).normalized, Vector3.up);
        float r = 1f - Mathf.Exp(-rotSmooth * Time.deltaTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, wantRot, r);
    }

    void BeginPeek()
    {
        peekStart = Time.time;
        peekTime = Random.Range(peekLasts.x, peekLasts.y);

        peekFrom = peekNow;
        peekTo = new Vector2(
            Random.Range(-sidePeek, sidePeek),
            Random.Range(upPeek * 0.25f, upPeek) // mostly upward
        );
    }

    void UpdatePeek()
    {
        float u = (Time.time - peekStart) / Mathf.Max(0.001f, peekTime);
        u = Mathf.Clamp01(u);

        // ease: feels nicer than linear but not overexplained
        float e = u * u * (3f - 2f * u);

        if (u < 0.5f)
        {
            float t = e / 0.5f;
            peekNow = Vector2.Lerp(peekFrom, peekTo, t);
        }
        else
        {
            float t = (e - 0.5f) / 0.5f;
            peekNow = Vector2.Lerp(peekTo, Vector2.zero, t);
        }

        if (u >= 1f)
        {
            peekNow = Vector2.zero;
            peekTime = 0f;
            nextPeekAt = Time.time + Random.Range(peekEvery.x, peekEvery.y);
        }
    }
}
