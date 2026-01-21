using UnityEngine;

public class Obstacle : MonoBehaviour
{
    public float SGlobal { get; private set; }
    public int Lane { get; private set; }

    public void Init(float sGlobal, int lane)
    {
        SGlobal = sGlobal;
        Lane = lane;
    }
}
