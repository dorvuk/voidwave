using UnityEngine;

public class FollowCam : MonoBehaviour
{
    public Transform target;
    public Vector3 localOffset = new Vector3(0f, 2.2f, -6.5f);
    public float follow = 7f;
    public float turn = 10f;
    public bool useWorldOffset = false;
    public Vector3 worldOffset = new Vector3(0f, 2.2f, -6.5f);
    public bool lookAtTarget = false;
    public float lookHeight = 1.2f;
    Vector3 posVel;

    void LateUpdate()
    {
        if (!target) return;

        Vector3 offset = useWorldOffset ? worldOffset : localOffset;
        Vector3 wantPos = useWorldOffset
            ? target.position + offset
            : target.TransformPoint(offset);
        float followTime = 1f / Mathf.Max(0.001f, follow);
        transform.position = Vector3.SmoothDamp(transform.position, wantPos, ref posVel, followTime);

        Quaternion wantRot;
        if (lookAtTarget)
        {
            Vector3 lookPos = target.position + Vector3.up * lookHeight;
            Vector3 dir = lookPos - transform.position;
            if (dir.sqrMagnitude < 0.0001f)
                dir = target.forward;
            wantRot = Quaternion.LookRotation(dir.normalized, Vector3.up);
        }
        else
        {
            wantRot = Quaternion.LookRotation(target.forward, target.up);
        }
        float b = 1f - Mathf.Exp(-turn * Time.deltaTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, wantRot, b);
    }
}
