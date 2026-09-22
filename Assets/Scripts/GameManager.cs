// GameManager.cs
// ---------------------------------------------------------------------------
// The "brain" of the game. It owns:
//   - the game state (Start -> Playing -> Won or Lost)
//   - score, combo and player health
//   - starting the game and restarting (reloading the scene)
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
    Won,      // all waves cleared, "You survived" panel is showing
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

    // Each kill is worth PointsPerKill x the current combo.
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

    // Called once for every zombie killed (typed or caught in a blast).
    public void AddKill()
    {
        Score += PointsPerKill * Combo;
        Combo += 1;
        hud.SetScore(Score);
        hud.SetCombo(Combo);
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
