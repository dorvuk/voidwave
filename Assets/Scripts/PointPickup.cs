using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PointPickup : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        var score = FindFirstObjectByType<ScoreManager>();
        if (score != null)
            score.PickUpPoint();

        gameObject.SetActive(false);
    }
}

