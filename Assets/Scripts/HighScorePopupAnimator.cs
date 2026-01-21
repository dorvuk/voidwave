using UnityEngine;
using System.Collections;

public class HighScorePopupAnimator : MonoBehaviour
{
    [Header("Tweens (NumberTweenText components)")]
    public NumberTweenText currentScoreTween;
    public NumberTweenText highScoreTween;

    [Header("Timing")]
    public float delayBeforeHighScoreUpdate = 3f;

    private Coroutine routine;

    public void Play(int currentScore, int oldHigh, int newHigh)
    {
        if (routine != null)
            StopCoroutine(routine);

        routine = StartCoroutine(PlayRoutine(currentScore, oldHigh, newHigh));
    }

    private IEnumerator PlayRoutine(int currentScore, int oldHigh, int newHigh)
    {
        if (highScoreTween != null)
        {
            highScoreTween.SetInstant(oldHigh);
        }
        
        if (currentScoreTween != null)
        {
            currentScoreTween.SetInstant(0);
            yield return new WaitForSecondsRealtime(1f);
            AudioManager.I.PlaySfx(SfxId.CurrentScoreJingle);
            currentScoreTween.SetTarget(currentScore);
        }

        yield return new WaitForSecondsRealtime(delayBeforeHighScoreUpdate);

        if (newHigh > oldHigh && highScoreTween != null)
        {
            highScoreTween.SetInstant(oldHigh);
            AudioManager.I.PlaySfx(SfxId.HighScoreJingle);
            highScoreTween.SetTarget(newHigh);
        }

        routine = null;
    }
}
