using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class TrackRibbon : MonoBehaviour
{
    public SplineContainer track;

    public float width = 6f;
    public float thickness = 0.03f;
    public float metersPerSample = 1.0f;
    public int minSamples = 64;

    public float rebuildEvery = 0.2f;
    public bool flipFaces = false;

    Mesh mesh;
    float timer;
    Vector3 lastUp = Vector3.up;

    void Awake()
    {
        mesh = new Mesh();
        mesh.name = "TrackRibbon";
        GetComponent<MeshFilter>().sharedMesh = mesh;
    }

    void OnEnable()
    {
        timer = rebuildEvery;
        if (track != null && track.Spline.GetLength() >= 0.01f)
            Build();
    }

    void LateUpdate()
    {
        if (!track) return;
        timer += Time.deltaTime;
        if (timer >= rebuildEvery)
        {
            timer = 0f;
            Build();
        }
    }

    void Build()
    {
        float len = track.Spline.GetLength();
        if (len < 0.01f) return;

        int samples = Mathf.Max(minSamples, Mathf.CeilToInt(len / Mathf.Max(0.1f, metersPerSample)));
        float stepT = 1f / (samples - 1);

        var verts = new List<Vector3>(samples * 2);
        var uvs = new List<Vector2>(samples * 2);
        var tris = new List<int>((samples - 1) * 6);

        lastUp = Vector3.up;

        for (int i = 0; i < samples; i++)
        {
            float t = i * stepT;
            float s = t * len;

            Vector3 pos = default, fwd = default, up = default, right = default;
            TrackSample.At(track, s, track.Spline.Closed, ref pos, ref fwd, ref up, ref right, ref lastUp);

            pos -= up * thickness;

            Vector3 leftW = pos - right * (width * 0.5f);
            Vector3 rightW = pos + right * (width * 0.5f);

            // IMPORTANT: mesh vertices must be in THIS object's local space
            verts.Add(transform.InverseTransformPoint(leftW));
            verts.Add(transform.InverseTransformPoint(rightW));

            float v = t * (len / Mathf.Max(0.001f, width));
            uvs.Add(new Vector2(0f, v));
            uvs.Add(new Vector2(1f, v));

            if (i < samples - 1)
            {
                int b = i * 2;

                if (!flipFaces)
                {
                    tris.Add(b);
                    tris.Add(b + 1);
                    tris.Add(b + 2);

                    tris.Add(b + 1);
                    tris.Add(b + 3);
                    tris.Add(b + 2);
                }
                else
                {
                    tris.Add(b);
                    tris.Add(b + 2);
                    tris.Add(b + 1);

                    tris.Add(b + 1);
                    tris.Add(b + 2);
                    tris.Add(b + 3);
                }
            }
        }

        mesh.Clear();
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }
}
