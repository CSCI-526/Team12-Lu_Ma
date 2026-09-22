// GameManager.cs
// ---------------------------------------------------------------------------
// The "brain" of the game. It owns:
//   - the game state (Start -> Playing -> Won or Lost)
//   - score, combo and player health
//   - starting the game and restarting (reloading the scene)
//   - pausing (Esc) and resuming with a 3-2-1 countdown. While paused,
//     Time.timeScale is 0, so everything that uses game time (movement, spawn
//     timers, boss attacks) freezes; the countdown uses real time.
//
// Other scripts reach it through GameManager.Instance, for example:
//   GameManager.Instance.AddKill();
//   if (GameManager.Instance.State == GameState.Playing) { ... }
//
// The references below (hud, spawner) are wired automatically by
// Tools > Blast Radius > Build Prototype Scene.
// ---------------------------------------------------------------------------
using UnityEngine;
using UnityEngine.SceneManagement;

// The four states the game can be in. Most scripts only do their work while Playing.
public enum GameState
{
    Start,    // Start panel is showing, waiting for the Start button / Enter
    Playing,  // rail moves, zombies spawn, typing works
    Paused,   // Esc was pressed: pause panel, or the 3-2-1 countdown before play resumes
    Won,     // all waves cleared, "You survived" panel is showing
    Lost      // health reached 0, "You died" panel is showing
}

public class GameManager : MonoBehaviour
{
    // The one GameManager in the scene. Set in Awake.
    public static GameManager Instance { get; private set; }

    [Header("Tuning")]
    [SerializeField] private int startingHealth = 100;

    [Header("References (wired by the scene builder)")]
    [SerializeField] private HUD hud;
    [SerializeField] private WaveSpawner spawner;

    // Each kill is worth PointsPerKill x the current combo (x the multi-kill
    // multiplier when one shot kills several, see AddKills).
    private const int PointsPerKill = 10;

    public GameState State { get; private set; }
    public int Score { get; private set; }
    public int Combo { get; private set; }
    public int Health { get; private set; }

    // Restart reloads the scene, which would normally show the Start panel again.
    // This flag survives the reload (static fields are not part of the scene) and
    // tells the fresh GameManager to begin right away. That is safe because the
    // browser already gave us keyboard focus the first time Start was clicked.
    private static bool startImmediatelyAfterReload;

    private void Awake()
    {
        Instance = this;
        State = GameState.Start;
        Time.timeScale = 1f; // never start frozen (timeScale survives a scene reload)
    }

    private void Start()
    {
        Score = 0;
        Combo = 1;
        Health = startingHealth;

        hud.SetScore(Score);
        hud.SetCombo(Combo);
        hud.SetHealth(Health, startingHealth);
        hud.ShowStartPanel();

        if (startImmediatelyAfterReload)
        {
            startImmediatelyAfterReload = false;
            StartGame();
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    // Called by the Start button (wired in the scene) and by StartOrRestart().
    public void StartGame()
    {
        if (State != GameState.Start)
        {
            return;
        }

        State = GameState.Playing;
        hud.HideAllPanels();
        spawner.BeginWaves();
    }

    // Called by the Restart buttons (wired in the scene) and by StartOrRestart().
    public void RestartGame()
    {
        Time.timeScale = 1f; // in case we restart from the pause panel
        startImmediatelyAfterReload = true;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // Called by TypingController when Enter is pressed outside of play.
    public void StartOrRestart()
    {
        if (State == GameState.Start)
        {
            StartGame();
        }
        else if (State == GameState.Won || State == GameState.Lost)
        {
            RestartGame();
        }
    }

    // ---- Pause ----

    private const int ResumeCountdownSeconds = 3;
    private Coroutine resumeCountdown; // running while the 3-2-1 countdown is on screen

    // Called by TypingController when Esc is pressed.
    //   Playing          -> pause
    //   paused (panel)   -> start the 3-2-1 countdown
    //   during countdown -> back to the pause panel
    public void OnPauseKey()
    {
        if (State == GameState.Playing)
        {
            PauseGame();
        }
        else if (State == GameState.Paused)
        {
            if (resumeCountdown != null)
            {
                PauseGame();
            }
            else
            {
                ResumeGame();
            }
        }
    }

    public void PauseGame()
    {
        if (State != GameState.Playing && State != GameState.Paused)
        {
            return;
        }

        if (resumeCountdown != null)
        {
            StopCoroutine(resumeCountdown);
            resumeCountdown = null;
        }

        State = GameState.Paused;
        Time.timeScale = 0f;
        hud.HideCountdown();
        hud.ShowPausePanel();
    }

    // Called by the Resume button (built by HUD) and by OnPauseKey().
    public void ResumeGame()
    {
        if (State != GameState.Paused || resumeCountdown != null)
        {
            return;
        }

        hud.HidePausePanel();
        resumeCountdown = StartCoroutine(ResumeCountdown());
    }

    private System.Collections.IEnumerator ResumeCountdown()
    {
        for (int number = ResumeCountdownSeconds; number >= 1; number--)
        {
            hud.ShowCountdown(number.ToString());
            yield return new WaitForSecondsRealtime(1f); // real time: game time is frozen
        }

        hud.HideCountdown();
        resumeCountdown = null;
        State = GameState.Playing;
        Time.timeScale = 1f;
    }

    // Called for a single kill (a normal zombie, a boss part, the boss).
    public void AddKill()
    {
        AddKills(1);
    }

    // Called with every enemy killed by ONE shot at once (an explosive zombie's
    // blast can kill several). Each kill is worth PointsPerKill x combo, and the
    // combo goes up by 1 per kill as usual. MULTI-KILL: when one shot kills
    // 2 or more, every one of those kills is also multiplied by that number
    // (3 kills at once = x3 points each), and the HUD shows "TRIPLE KILL!".
    public void AddKills(int count)
    {
        if (count <= 0)
        {
            return;
        }

        int multiplier = count >= 2 ? count : 1;
        int gained = 0;
        for (int i = 0; i < count; i++)
        {
            gained += PointsPerKill * Combo * multiplier;
            Combo += 1;
        }

        Score += gained;
        hud.SetScore(Score);
        hud.SetCombo(Combo);

        if (count >= 2)
        {
            hud.ShowMultiKill(count, gained);
        }
    }

    // Called on a wrong key, and when the player takes damage.
    public void ResetCombo()
    {
        Combo = 1;
        hud.SetCombo(Combo);
    }

    // Called by a zombie that reached the player.
    public void TakeDamage(int amount)
    {
        if (State != GameState.Playing)
        {
            return;
        }

        Health -= amount;
        if (Health < 0)
        {
            Health = 0;
        }
        hud.SetHealth(Health, startingHealth);
        ResetCombo();

        if (Health <= 0)
        {
            State = GameState.Lost;
            spawner.StopWaves();
            hud.ShowLostPanel(Score);
        }
    }

    // Called by WaveSpawner when the last wave has been cleared.
    public void WinGame()
    {
        if (State != GameState.Playing)
        {
            return;
        }

        State = GameState.Won;
        hud.ShowWonPanel(Score);
    }
}
