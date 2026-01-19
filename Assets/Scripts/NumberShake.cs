using UnityEngine;
using TMPro;
using System.Collections;

[RequireComponent(typeof(TMP_Text))]
public class NumberTweenText : MonoBehaviour
{
    [Header("Animation")]
    public float tweenDuration = 0.5f;

    public float shakeAmplitude = 3f;

    public float shakeFrequency = 9f;

    [Header("Formatting")]
    [Tooltip("If true, values are rounded to int for display.")]
    public bool displayAsInt = true;

    private TMP_Text tmp;
    private RectTransform rect;

    private float displayedValue;
    private float targetValue;

    private Coroutine animCoroutine;
    private Vector2 baseAnchoredPos;
    private bool hasBasePos;

    void Awake()
    {
        tmp = GetComponent<TMP_Text>();
        rect = transform as RectTransform;

        if (rect != null)
        {
            baseAnchoredPos = rect.anchoredPosition;
            hasBasePos = true;
        }

        displayedValue = 0f;
        targetValue = 0f;
        ApplyText(displayedValue);
    }

    void OnDisable()
    {
        if (rect != null && hasBasePos)
            rect.anchoredPosition = baseAnchoredPos;

        animCoroutine = null;
    }

    public void SetInstant(float value)
    {
        targetValue = value;
        displayedValue = value;

        if (rect != null && hasBasePos)
            rect.anchoredPosition = baseAnchoredPos;

        ApplyText(displayedValue);
    }

    public void Add(float delta)
    {
        SetTarget(targetValue + delta);
    }

    public void SetTarget(float value)
    {
        targetValue = value;

        if (animCoroutine == null && gameObject.activeInHierarchy)
            animCoroutine = StartCoroutine(AnimateToTarget());
    }

    IEnumerator AnimateToTarget()
    {
        while (true)
        {
            float startValue = displayedValue;
            float endValue = targetValue;

            if (Mathf.Approximately(startValue, endValue))
                break;

            float t = 0f;

            while (t < tweenDuration)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / tweenDuration);
                float eased = EaseOutCubic(u);

                displayedValue = Mathf.Lerp(startValue, endValue, eased);
                ApplyText(displayedValue);

                ApplyShake(u);

                yield return null;

                if (!Mathf.Approximately(endValue, targetValue))
                    break;
            }

            if (Mathf.Approximately(endValue, targetValue))
            {
                displayedValue = targetValue;
                ApplyText(displayedValue);
            }

        }

        if (rect != null && hasBasePos)
            rect.anchoredPosition = baseAnchoredPos;

        animCoroutine = null;
    }

    void ApplyShake(float normalizedTime)
    {
        if (rect == null || !hasBasePos)
            return;

        float fade = 1f - normalizedTime;

        float phase = Time.unscaledTime * shakeFrequency * Mathf.PI * 2f;
        float x = Mathf.Sin(phase) * shakeAmplitude * fade;

        rect.anchoredPosition = baseAnchoredPos + new Vector2(x, 0f);
    }

    void ApplyText(float value)
    {
        if (tmp == null) return;

        if (displayAsInt)
        {
            int v = Mathf.RoundToInt(value);
            tmp.text = $"{v}";
        }
        else
        {
            tmp.text = $"{value:0.00}";
        }
    }

    static float EaseOutCubic(float x)
    {
        // 1 - (1 - x)^3
        float a = 1f - x;
        return 1f - a * a * a;
    }
}
