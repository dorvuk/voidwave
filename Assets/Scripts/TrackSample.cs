using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;

public static class TrackSample
{
    static Vector3 V(float3 v) => new Vector3(v.x, v.y, v.z);

    public static float ToT(SplineContainer c, float s, bool loop)
    {
        float len = c.Spline.GetLength();
        if (len <= 0.0001f) return 0f;

        if (loop && c.Spline.Closed) s = Mathf.Repeat(s, len);
        else s = Mathf.Clamp(s, 0f, Mathf.Max(0f, len - 0.0001f));

        return s / len; // normalized 0..1
    }

    public static void At(SplineContainer c, float s, bool loop,
        ref Vector3 pos, ref Vector3 fwd, ref Vector3 up, ref Vector3 right, ref Vector3 lastUp)
    {
        float t = ToT(c, s, loop);

        c.Spline.Evaluate(t, out float3 p, out float3 tan, out float3 upHint);

        pos = V(p);

        Vector3 forward = V(tan);
        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
        fwd = forward.normalized;

        Vector3 hinted = V(upHint);
        Vector3 projectedHint = Vector3.ProjectOnPlane(hinted, fwd);

        const float eps = 0.0001f;
        Vector3 baseUp = projectedHint.sqrMagnitude > eps ? projectedHint.normalized : Vector3.zero;

        if (baseUp == Vector3.zero)
        {
            Vector3 fallback = lastUp == Vector3.zero ? Vector3.up : lastUp;
            Vector3 projected = Vector3.ProjectOnPlane(fallback, fwd);
            baseUp = projected.sqrMagnitude > eps ? projected.normalized : Vector3.up;
        }

        right = Vector3.Cross(baseUp, fwd);
        if (right.sqrMagnitude < eps)
        {
            baseUp = Vector3.up;
            right = Vector3.Cross(baseUp, fwd);
        }

        right = right.normalized;
        up = Vector3.Cross(fwd, right).normalized;

        lastUp = up;
    }
}
