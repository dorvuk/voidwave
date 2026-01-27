using UnityEngine;
using TMPro;
using System.Collections;

public class ScoreManager : MonoBehaviour
{
    [Header("Scoring Rules")]
    public int pointsPerPickup = 10;
    public float multiplierStep = 0.05f;
    public int pointsPerSecond = 10;

    [Header("UI Text Fields")]
    public TextMeshProUGUI uiScoreText;
    public TextMeshProUGUI multScoreText;
    public TextMeshProUGUI multResultText;
    public NumberTweenText totalScoreTween;
    public NumberTweenText multResultTween;

    [Header("ScoreList Fade")]
    public CanvasGroup scoreListGroup;
    public float scoreListFade = 0.25f;
    public float scoreListHoldSeconds = 2.5f;
    public float scoreListShowThreshold = 1.5f;

    [Header("High Score Persistence")]
    public string highScoreKey = "HighScore";
    public int CurrentScore { get; private set; }
    public int HighScore { get; private set; }
    private int bankPoints;
    private float passiveRemainder;
    private float multiplier;
    private bool ticking;
    private Coroutine tickCoroutine;
    private Coroutine scoreListCoroutine;
    private int streakCount = 0;

    void Awake()
    {
        HighScore = PlayerPrefs.GetInt(highScoreKey, 0);
        CurrentScore = 0;

        multiplier = 1;
        bankPoints = 0;

        if (scoreListGroup != null)
        {
            scoreListGroup.alpha = 0f;
            scoreListGroup.interactable = false;
            scoreListGroup.blocksRaycasts = false;
            scoreListGroup.gameObject.SetActive(false);
        }

        RefreshAllUI();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.G))
        {
            PlayerPrefs.DeleteKey(highScoreKey);
            PlayerPrefs.Save();

            HighScore = 0;
            RefreshScoreUI();
        }
    }


    // Gameplay Hooks
    public void StartScoring()
    {
        if (ticking) return;
        ticking = true;
        tickCoroutine = StartCoroutine(SecondTick());
    }
    public void StopScoring()
    {
        ticking = false;
        if (tickCoroutine != null)
        {
            StopCoroutine(tickCoroutine);
            tickCoroutine = null;
        }
    }
    public void PickUpPoint()
    {
        FlushPassiveRemainder();

        bankPoints += pointsPerPickup;
        multiplier += multiplierStep;

        streakCount++;
        streakCount = Mathf.Min(7, streakCount);
        AudioManager.I.PlayPointCollectTier(streakCount);

        RefreshMultiplierUI();
    }
    public void ResetCounter()
    {
        FlushPassiveRemainder();

        if (streakCount > 0)
        {
            AudioManager.I.PlaySfx(SfxId.LoseStreak);
        } 
        streakCount = 0;
        int bankedScore = Mathf.RoundToInt(bankPoints * multiplier);
        if (bankedScore > 0)
        {
            AddToCurrentScore(bankedScore);
        }

        if (scoreListGroup != null && bankedScore > 0 && multiplier > scoreListShowThreshold)
        {
            ShowScoreList(bankedScore);
        }

        bankPoints = 0;
        multiplier = 1;
        passiveRemainder = 0f;

        RefreshAllUI();
    }

    public void ResetAllScores()
    {
        streakCount = 0;
        CurrentScore = 0;
        bankPoints = 0;
        multiplier = 1;
        passiveRemainder = 0f;

        RefreshAllUI();
    }

    // Helpers
    IEnumerator SecondTick()
    {
        while (ticking)
        {
            float delta = Time.deltaTime;
            if (delta > 0f && pointsPerSecond > 0)
            {
                passiveRemainder += pointsPerSecond * delta;
                int add = Mathf.FloorToInt(passiveRemainder);
                if (add > 0)
                {
                    bankPoints += add;
                    passiveRemainder -= add;
                    RefreshMultiplierUI();
                }
            }

            yield return null;
        }
    }

    void AddToCurrentScore(int amount)
    {
        CurrentScore += amount;
        totalScoreTween.Add(amount);
    }

    void RefreshAllUI()
    {
        RefreshScoreUI();
        RefreshMultiplierUI();
    }

    void RefreshScoreUI()
    {
        if (uiScoreText != null)
            uiScoreText.text = CurrentScore.ToString();
    }

    void RefreshMultiplierUI()
    {
        if (multScoreText != null)
        {
            multScoreText.text = $"{multiplier:0.00}x {bankPoints}";
        }
    }

    
    void ShowScoreList(int bankedScore)
    {
        if (scoreListCoroutine != null)
            StopCoroutine(scoreListCoroutine);

        scoreListCoroutine = StartCoroutine(ScoreListFadeSequence(bankedScore));
    }

    IEnumerator ScoreListFadeSequence(int bankedScore)
    {
        scoreListGroup.gameObject.SetActive(true);

        yield return FadeCanvasGroup(scoreListGroup, scoreListGroup.alpha, 1f, scoreListFade);

        if (multResultTween != null)
        {
            multResultTween.SetInstant(0);
            multResultTween.SetTarget(bankedScore);
        }

        yield return new WaitForSeconds(scoreListHoldSeconds);

        yield return FadeCanvasGroup(scoreListGroup, scoreListGroup.alpha, 0f, scoreListFade);

        scoreListGroup.gameObject.SetActive(false);
        scoreListCoroutine = null;
    }

    IEnumerator FadeCanvasGroup(CanvasGroup cg, float from, float to, float duration)
    {
        if (duration <= 0f)
        {
            cg.alpha = to;
            yield break;
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            cg.alpha = Mathf.Lerp(from, to, t / duration);
            yield return null;
        }
        cg.alpha = to;
    }

    public bool CommitHighScore()
    {
        if (CurrentScore <= HighScore)
            return false;

        HighScore = CurrentScore;
        PlayerPrefs.SetInt(highScoreKey, HighScore);
        PlayerPrefs.Save(); // not strictly required, but removes ambiguity

        RefreshScoreUI();   // updates highScorePopupHighText too
        return true;
    }

    public void FinalizeRunScore()
    {
        FlushPassiveRemainder();

        int bankedScore = Mathf.RoundToInt(bankPoints * multiplier);
        if (bankedScore > 0)
            AddToCurrentScore(bankedScore);

        streakCount = 0;
        bankPoints = 0;
        multiplier = 1;
        passiveRemainder = 0f;
        RefreshAllUI();
    }

    void FlushPassiveRemainder()
    {
        if (passiveRemainder <= 0f) return;
        int add = Mathf.FloorToInt(passiveRemainder);
        if (add > 0)
        {
            bankPoints += add;
            passiveRemainder -= add;
        }
    }

}
