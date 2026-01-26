using UnityEngine;

[ExecuteAlways]
public class InfiniteOceanFollow : MonoBehaviour
{
    [Header("References")]
    public Transform target;

    [Header("Ocean Level")]
    public float waterY = 100f;

    [Header("Tiling / Snapping")]
    public float snapSize = 25f;

    [Header("Follow Settings")]
    public bool followInPlayMode = true;
    public bool followInEditMode = true;

    void LateUpdate()
    {
        if (target == null) return;

        bool shouldFollow = Application.isPlaying ? followInPlayMode : followInEditMode;
        if (!shouldFollow) return;

        Vector3 p = target.position;

        // snap to grid so the plane doesn't constantly move by tiny amounts
        float x = snapSize > 0f ? Mathf.Floor(p.x / snapSize) * snapSize : p.x;
        float z = snapSize > 0f ? Mathf.Floor(p.z / snapSize) * snapSize : p.z;

        transform.position = new Vector3(x, waterY, z);
    }
}
