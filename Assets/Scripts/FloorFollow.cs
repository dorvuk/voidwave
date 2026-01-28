using UnityEngine;

public class FloorFollow : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform player;

    [Header("Follow Settings")]
    [SerializeField] private float fixedY = -50f;

    private void LateUpdate()
    {
        if (player == null) return;

        Vector3 p = player.position;
        transform.position = new Vector3(p.x, fixedY, p.z);
    }
}
