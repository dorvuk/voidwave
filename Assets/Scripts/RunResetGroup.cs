using UnityEngine;

public class RunResetGroup : MonoBehaviour, IRunResettable
{
    [Tooltip("Any MonoBehaviours that implement IRunResettable (generator, runner, spawners, score, etc).")]
    public MonoBehaviour[] targets;

    public void ResetRun()
    {
        ResetAll();
    }

    public void ResetAll()
    {
        if (targets == null || targets.Length == 0) return;

        foreach (var mb in targets)
        {
            if (!mb) continue;

            if (mb is IRunResettable resettable)
            {
                resettable.ResetRun();
            }
            else
            {
                Debug.LogWarning($"RunResetGroup target {mb.name} does not implement IRunResettable", this);
            }
        }
    }
}
