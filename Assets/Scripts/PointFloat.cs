using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PointFloat : MonoBehaviour
{
    public float floatHeight = 0.25f;
    public float floatSpeed = 2f;
    public float rotateSpeed = 60f;

    Vector3 startLocalPos;

    void Start()
    {
        startLocalPos = transform.localPosition;
    }

    void Update()
    {
        float y = Mathf.Sin(Time.time * floatSpeed) * floatHeight;
        transform.localPosition = startLocalPos + Vector3.up * y;

        transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime, Space.World);
    }
}

