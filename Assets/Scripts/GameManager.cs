// GameManager.cs
// ---------------------------------------------------------------------------
// The "brain" of the game. It owns:
//   - the game state (Start -> Playing -> Won or Lost)
//   - score, combo and player health
//   - the run's statistics (accuracy, speed, best combo...) for the end screen
//   - starting the game and restarting (reloading the scene)
//   - pausing (Esc) and resuming with a 3-2-1 countdown. While paused,
//     Time.timeScale is 0, so everything that uses game time (movement, spawn
//     timers, boss attacks) freezes; the countdown uses real time.
//
// THE COMBO: +1 for every kill, -1 on a wrong key, back to 1 when you get hurt.
// Each kill is worth 10 x combo points, and every 5 kills in a row earn a
// power charge (lure bomb or freeze, see Powers). So clean typing pays twice.
//
// Other scripts reach it through GameManager.Instance, for example:
//   GameManager.Instance.AddKill();
//   if (GameManager.Instance.State == GameState.Playing) { ... }
//
// The references below (hud, spawner, powers) are wired in the scene.
// ---------------------------------------------------------------------------
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// The states the game can be in. Most scripts only do their work while Playing.
public enum GameState
{
    Start,    // Start panel is showing, waiting for the Start button / Enter
    Playing,  // riding, fighting, typing
    Paused,   // Esc was pressed: pause panel, or the 3-2-1 countdown before play resumes
    Tip,      // a first-time tip box is showing (game frozen); Enter or OK continues
    Won,      // the last area was cleared, "You survived" panel is showing (Enter / Continue: leaderboard)
    Lost,     // health reached 0, "You died" panel is showing
    Scores    // the leaderboard page after Won / Lost: type a name, then Play again or Quit
}

public class GameManager : MonoBehaviour
{
    // The one GameManager in the scene. Set in Awake.
    public static GameManager Instance { get; private set; }

    [Header("Tuning")]
    [SerializeField] private int startingHealth = 100;

    [Header("References (wired in the scene)")]
    [SerializeField] private HUD hud;
    [SerializeField] private WaveSpawner spawner;
    [SerializeField] private Powers powers;

    // Each kill is worth PointsPerKill x the current combo (x the multi-kill
    // multiplier when one blast kills several, see AddKills).
    private const int PointsPerKill = 10;

    public GameState State { get; private set; }
    public int Score { get; private set; }
    public int Combo { get; private set; }
    public int Health { get; private set; }

    // The numbers shown on the end screen.
    public RunStats Stats { get; private set; }

    // The player's powers (lure bomb, freeze).
    public Powers Powers
    {
        get { return powers; }
    }

    private void Awake()
    {
        Instance = this;
        State = GameState.Start;
        Time.timeScale = 1f; // never start frozen (timeScale survives a scene reload)
        Stats = new RunStats();
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
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Update()
    {
        if (State != GameState.Playing)
        {
            return;
        }

        Stats.Seconds += Time.deltaTime;
    }

    // Tips are opened here, at the END of the frame, so everything that was
    // already happening this frame (bullets landing, a chain of kills) finishes first.
    private void LateUpdate()
    {
        if (State == GameState.Playing && pendingTips.Count > 0)
        {
            ShowNextTip();
        }
    }

    // ---- First-time tips ----
    // Something new the first time (the first lure bomb, the first freeze, the
    // first WORD CHAIN pair) pauses the game with a box that explains it (built
    // by HUD). Enter or the OK button continues. Static, like Tutorial: after a
    // Restart they are not shown again.

    private struct PendingTip
    {
        public string Title;
        public string Text;
        public Color Color;
    }

    private static readonly HashSet<string> tipsShown = new HashSet<string>();
    private readonly Queue<PendingTip> pendingTips = new Queue<PendingTip>();

    // Shows the tip called key once (it opens at the end of this frame).
    public void RequestTip(string key, string title, string text, Color titleColor)
    {
        if (!tipsShown.Add(key))
        {
            return; // already explained
        }
        PendingTip tip;
        tip.Title = title;
        tip.Text = text;
        tip.Color = titleColor;
        pendingTips.Enqueue(tip);
    }

    // Called by Powers when a charge is gained. Only the first one of each kind shows a tip.
    public void RequestPowerTip(PowerKind kind, string title, string text)
    {
        RequestTip("power " + kind, title, text, kind == PowerKind.Lure ? Palette.LureCrate : Palette.FreezeCrate);
    }

    private void ShowNextTip()
    {
        PendingTip tip = pendingTips.Dequeue();
        State = GameState.Tip;
        Time.timeScale = 0f; // everything waits for the player
        hud.ShowPowerTip(tip.Title, tip.Text, tip.Color);
    }

    // Called by the tip box's OK button and by Enter (TypingController).
    public void ConfirmTip()
    {
        if (State != GameState.Tip)
        {
            return;
        }

        hud.HidePowerTip();
        if (pendingTips.Count > 0)
        {
            ShowNextTip(); // got both powers at once: explain the next one too
            return;
        }
        State = GameState.Playing;
        Time.timeScale = 1f;
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

        // Before anything happens, explain what the combo buys (a tip box; the
        // game waits for Enter / OK). Shown once, not again after a Restart.
        RequestTip("combo weapons", "COMBO WEAPONS",
            "Type without mistakes to build your COMBO (top of the screen), then SPEND it on a weapon:\n\n"
            + "<color=#FF4D33>[3] FRENZY</color> - costs " + Powers.FrenzyCost + " combo: for 5 seconds every enemy word\n"
            + "turns into a TINY red word (1 to 4 letters - even a single letter!).\n"
            + "<color=#FF8C1A>[4] RPG</color> - costs " + Powers.RocketCost + " combo: your next finished word fires a rocket\n"
            + "that blows up everything around its target (it hurts the boss too).\n\n"
            + "Press 3 / 4 to GET a weapon: it is kept bottom-left (2 slots each) until you want it.\n"
            + "Press the key again (or click its icon) to USE it. Weaker against the boss!\n"
            + "One weapon per wave. A wrong key costs 1 combo; a bite resets it.\n"
            + "Lure bombs [1] and freezes [2] come from supply crates.",
            Palette.BlastOrange);
    }

    // Spends combo on a weapon (Powers): the combo goes down by 'amount' (never below 1).
    public void SpendCombo(int amount)
    {
        Combo = Mathf.Max(1, Combo - amount);
        hud.SetCombo(Combo);
        powers.OnComboChanged(Combo);
    }

    // Called by the pause panel's Restart and the leaderboard's Play again (and
    // Enter there): reloads the scene, which shows the Start panel, the same
    // screen as when the game is first opened.
    public void RestartGame()
    {
        Time.timeScale = 1f; // in case we restart from the pause panel
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // Called by TypingController when Enter is pressed outside of play
    // (Start panel, results screens, or a first-time power tip).
    public void StartOrRestart()
    {
        if (State == GameState.Start)
        {
            StartGame();
        }
        else if (State == GameState.Won || State == GameState.Lost)
        {
            OpenLeaderboard();
        }
        else if (State == GameState.Tip)
        {
            ConfirmTip();
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

    // ---- Typing statistics (called by TypingController) ----

    public void OnCorrectKey()
    {
        Stats.CorrectKeys += 1;
    }

    // A wrong key: counts against accuracy and costs one step of combo.
    public void OnWrongKey()
    {
        Stats.WrongKeys += 1;
        LoseComboStep();
    }

    // A wrong key costs one step of combo (never below 1). Getting hurt still
    // resets it completely (ResetCombo).
    public void LoseComboStep()
    {
        if (Combo <= 1)
        {
            return;
        }
        Combo -= 1;
        hud.SetCombo(Combo);
        powers.OnComboChanged(Combo);
    }

    // ---- Kills and score ----

    // WORD CHAIN (called by TypingController): one run of typing finished
    // 'words' words, e.g. typing "hunter" finished "hunt" on the way. Every one
    // of those kills already counts as usual (score, combo +1 each) when its
    // bullet lands; the chain adds a bonus of PointsPerKill x combo x words.
    public void AddWordChain(int words)
    {
        if (words < 2 || State == GameState.Lost)
        {
            return;
        }

        int bonus = PointsPerKill * Combo * words;
        Score += bonus;
        hud.SetScore(Score);
        hud.ShowWordChain(words, bonus);
    }

    // Called for a single kill (a normal zombie, a boss part, an orb...).
    // Returns the points it was worth.
    public int AddKill()
    {
        return AddKills(1);
    }

    // Called with every enemy killed by ONE shot at once (an explosion or a
    // chain reaction can kill several). Each kill is worth PointsPerKill x combo,
    // and the combo goes up by 1 per kill as usual. MULTI-KILL: when one shot
    // kills 2 or more, every one of those kills is also multiplied by that number
    // (3 kills at once = x3 points each), and the HUD shows "TRIPLE KILL!".
    // Returns the points gained.
    public int AddKills(int count)
    {
        if (count <= 0 || State == GameState.Lost)
        {
            return 0;
        }

        int multiplier = count >= 2 ? count : 1;
        int gained = 0;
        for (int i = 0; i < count; i++)
        {
            gained += PointsPerKill * Combo * multiplier;
            Combo += 1;
            powers.OnComboChanged(Combo); // every 5 in a row earns a power
        }

        Score += gained;
        Stats.Kills += count;
        Stats.BestCombo = Mathf.Max(Stats.BestCombo, Combo);
        Stats.BiggestMultiKill = Mathf.Max(Stats.BiggestMultiKill, count);
        hud.SetScore(Score);
        hud.SetCombo(Combo);

        if (count >= 2)
        {
            hud.ShowMultiKill(count, gained);
        }
        return gained;
    }

    // Called when the player takes damage (a wrong key only costs one step, see LoseComboStep).
    public void ResetCombo()
    {
        if (Combo == 1)
        {
            return;
        }
        Combo = 1;
        hud.SetCombo(Combo);
        powers.OnComboChanged(Combo);
    }

    // ---- Health ----

    // Gives health back (never above the starting health). Used by supply crates.
    public void Heal(int amount)
    {
        Health = Mathf.Min(startingHealth, Health + amount);
        hud.SetHealth(Health, startingHealth);
    }

    // Called by a zombie that reached the player, and by a boss orb that hit.
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
        hud.FlashRed();
        CameraDirector.Shake(0.45f);
        ResetCombo();

        if (Health <= 0)
        {
            EndGame(false);
        }
    }

    // ---- The end ----

    // Called by WaveSpawner when the last area has been cleared.
    public void WinGame()
    {
        EndGame(true);
    }

    private void EndGame(bool won)
    {
        if (State != GameState.Playing)
        {
            return;
        }

        State = won ? GameState.Won : GameState.Lost;
        if (!won)
        {
            spawner.StopWaves();
        }

        Stats.Score = Score;
        gameWon = won;
        hud.ShowResults(won, Stats);
    }

    // ---- Leaderboard (after the results panel) ----
    // Continue (or Enter) on the results panel opens the leaderboard page: the
    // player types a name (Enter saves it with the score, see Leaderboard),
    // sees their rank among the six best, then plays again or quits.

    private bool gameWon;
    private string typedName = "";
    private bool nameSaved;

    // Called by the results panel's Continue button (HUD) and by Enter.
    public void OpenLeaderboard()
    {
        if (State != GameState.Won && State != GameState.Lost)
        {
            return;
        }
        State = GameState.Scores;
        typedName = "";
        nameSaved = false;
        hud.ShowLeaderboard(gameWon, Stats.Score); // the score frozen at the end (late blasts do not count)
        hud.SetLeaderboardName(typedName);
    }

    // Called by TypingController every frame on the leaderboard page with what
    // was typed. Before the name is saved: letters build the name, Backspace
    // removes one, Enter saves it. After: Enter plays again.
    public void OnLeaderboardInput(string typed, bool backspace, bool enter)
    {
        if (State != GameState.Scores)
        {
            return;
        }
        if (nameSaved)
        {
            if (enter)
            {
                RestartGame();
            }
            return;
        }

        foreach (char character in typed)
        {
            if (!char.IsControl(character) && typedName.Length < Leaderboard.MaxNameLength
                && (typedName.Length > 0 || !char.IsWhiteSpace(character)))
            {
                typedName += character;
            }
        }
        if (backspace && typedName.Length > 0)
        {
            typedName = typedName.Substring(0, typedName.Length - 1);
        }
        hud.SetLeaderboardName(typedName);

        if (enter)
        {
            SubmitName();
        }
    }

    // Saves the name with the score and shows the player's place.
    public void SubmitName()
    {
        if (State != GameState.Scores || nameSaved)
        {
            return;
        }
        nameSaved = true;
        string playerName = Leaderboard.CleanName(typedName);
        int rank = Leaderboard.Add(playerName, Stats.Score);
        hud.ShowLeaderboardRank(rank, playerName, Stats.Score);
    }

    // The Quit button: closes the game (in the Unity Editor: stops Play mode).
    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
