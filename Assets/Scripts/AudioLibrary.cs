using UnityEngine;

public enum MusicTrack
{
    MainMenuTheme,
    GameplayTheme,
    EndGameTheme
}

public enum SfxId
{
    ButtonHover,
    ButtonClick,
    PointCollect,
    Surf, //TODO
    Move, //TODO = in gameplay
    Jump, //TODO = Silence surf, Play sound, Wait (to land), Play surf.
    ObstacleHit,
    Death,
    CurrentScoreJingle,
    HighScoreJingle,
    Submerge,
    Surface,
    LoseStreak
}

[CreateAssetMenu(menuName = "Audio/Game Audio Library", fileName = "GameAudioLibrary")]
public class GameAudioLibrary : ScriptableObject
{
    [Header("Music")]
    public AudioClip mainMenuTheme;
    public AudioClip gameplayTheme;
    public AudioClip endGameTheme;

    [Header("UI")]
    public AudioClip[] buttonHover;
    public AudioClip[] buttonClick;

    [Header("Gameplay")]
    public AudioClip[] pointCollect;
    public AudioClip[] surf;
    public AudioClip[] move;
    public AudioClip[] jump;
    public AudioClip[] obstacleHit;
    public AudioClip[] death;
    public AudioClip[] submerge;
    public AudioClip[] surface;
    public AudioClip[] loseStreak;

    [Header("Jingles")]
    public AudioClip[] currentScoreJingle;
    public AudioClip[] highScoreJingle;
}
