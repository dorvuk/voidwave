using UnityEngine;

public class FollowCam : MonoBehaviour
{
    public Transform target;
    public Vector3 localOffset = new Vector3(0f, 2.2f, -6.5f);
    public float follow = 7f;
    public float turn = 10f;
    Vector3 posVel;

    void LateUpdate()
    {
        if (!target) return;

        Vector3 wantPos = target.TransformPoint(localOffset);
        float followTime = 1f / Mathf.Max(0.001f, follow);
        transform.position = Vector3.SmoothDamp(transform.position, wantPos, ref posVel, followTime);

        Quaternion wantRot = Quaternion.LookRotation(target.forward, target.up);
        float b = 1f - Mathf.Exp(-turn * Time.deltaTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, wantRot, b);
    }
}
