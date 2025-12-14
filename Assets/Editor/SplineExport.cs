using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

public static class SplineExport
{
    [MenuItem("Tools/Voidwave/Export Selected Spline JSON")]
    static void Export()
    {
        var go = Selection.activeGameObject;
        if (!go) { Debug.LogError("Select a GameObject with SplineContainer."); return; }

        var c = go.GetComponent<SplineContainer>();
        if (!c) { Debug.LogError("Selected object has no SplineContainer."); return; }

        var sp = c.Spline;

        var path = EditorUtility.SaveFilePanel("Export Spline", Application.dataPath, "spline", "json");
        if (string.IsNullOrEmpty(path)) return;

        using var w = new StreamWriter(path);
        w.WriteLine("{");
        w.WriteLine($"  \"closed\": {(sp.Closed ? "true" : "false")},");
        w.WriteLine("  \"knots\": [");

        for (int i = 0; i < sp.Count; i++)
        {
            var k = sp[i];
            string comma = i == sp.Count - 1 ? "" : ",";
            w.WriteLine(
                $"    {{\"p\":[{k.Position.x:F6},{k.Position.y:F6},{k.Position.z:F6}],\"tin\":[{k.TangentIn.x:F6},{k.TangentIn.y:F6},{k.TangentIn.z:F6}],\"tout\":[{k.TangentOut.x:F6},{k.TangentOut.y:F6},{k.TangentOut.z:F6}]}}{comma}"
            );
        }

        w.WriteLine("  ]");
        w.WriteLine("}");
        Debug.Log("Exported spline to: " + path);
    }
}
