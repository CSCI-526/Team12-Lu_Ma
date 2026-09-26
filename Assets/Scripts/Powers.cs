// Powers.cs
// ---------------------------------------------------------------------------
// The player's two special powers, used with the number keys:
//
//   [1] LURE BOMB - HOLD 1: a dashed arc (ThrowArc) shows where it will land,
//       throwDistance metres ahead, where you look, and a hint (bottom-right)
//       says to use the ARROW keys: Left / Right turn the throw, Up / Down
//       move the landing point farther / closer. RELEASE 1 to throw it there
//       (a quick tap throws straight ahead).
//       It blinks; every zombie near it walks to it and crowds around it
//       (they forget about you), then it explodes. See LureBomb.
//   [2] FREEZE    - every zombie, energy orb and the boss stop for
//       freezeSeconds. You keep typing. While frozen, IsFrozen is true.
//
// Charges (max maxCharges of each) come from supply crates in the level and
// from the COMBO: every killsPerReward kills in a row without a mistake earn a
// charge, alternating lure bomb, freeze, lure bomb, ... A wrong key or getting
// hurt resets the combo, so accurate typing is what earns powers.
//
// Lives on the GameManager object. TypingController calls BeginLureAim (1 down),
// ReleaseLureAim (1 up) and TryUseFreeze; GameManager calls OnComboChanged
// every time the combo changes.
// ---------------------------------------------------------------------------
using UnityEngine;

public enum PowerKind
{
    Lure,    // key 1: lure bomb
    Freeze   // key 2: freeze every zombie
}

public class Powers : MonoBehaviour
{
    [Header("Charges")]
    [SerializeField] private int maxCharges = 3;
    [SerializeField] private int killsPerReward = 5;      // a combo of this many kills earns a charge

    [Header("Lure bomb")]
    [SerializeField] private float throwDistance = 9f;    // metres ahead of the player (a quick tap)
    [SerializeField] private float minThrowDistance = 3f; // never closer than this (e.g. facing a wall)
    [SerializeField] private float maxThrowDistance = 22f; // the farthest the Up arrow can push it
    [SerializeField] private float aimTurnSpeed = 90f;    // degrees per second the Left / Right arrows turn the throw
    [SerializeField] private float maxAimTurn = 60f;      // degrees left or right of where the camera looks, at most
    [SerializeField] private float aimDistanceSpeed = 10f; // metres per second the Up / Down arrows move the landing point

    [Header("Freeze")]
    [SerializeField] private float freezeSeconds = 4f;

    [Header("References (wired in the scene)")]
    [SerializeField] private HUD hud;
    [SerializeField] private Transform player;

    // Static so that zombies, orbs and the boss can simply check Powers.IsFrozen.
    // Reset in Awake, because static values survive a scene reload.
    private static float freezeTimeLeft;

    // True while the freeze power is on: zombies, orbs and the boss stand still.
    public static bool IsFrozen
    {
        get { return freezeTimeLeft > 0f; }
    }

    public int LureCharges { get; private set; }
    public int FreezeCharges { get; private set; }

    // Lure bomb aiming (while the 1 key is held).
    private bool aiming;

    // True while the player holds 1 to aim a lure bomb (the camera widens its view).
    public bool IsAiming
    {
        get { return aiming; }
    }
    private ThrowArc arc;        // the dashed preview, built on first use
    private float aimTurn;       // degrees the throw is turned right (< 0 = left) by the arrow keys
    private float aimDistance;   // metres: starts at throwDistance, Up / Down change it
    private Vector2 arrowInput;  // this frame's arrow keys: x = Right - Left, y = Up - Down (set by TypingController)

    private void Awake()
    {
        freezeTimeLeft = 0f;
    }

    private void Start()
    {
        RefreshHud();
        OnComboChanged(1);
    }

    private void Update()
    {
        UpdateAim();

        if (freezeTimeLeft <= 0f || GameManager.Instance.State != GameState.Playing)
        {
            return;
        }

        freezeTimeLeft -= Time.deltaTime;
        if (freezeTimeLeft <= 0f)
        {
            freezeTimeLeft = 0f;
        }

    }

    // ---- Earning charges ----

    // Gives the player one more charge. Returns false (and shows nothing) if
    // that power is already full (maxCharges). On a gain, a "+1" rises next to
    // the power on the HUD, and the very first charge of each power pauses the
    // game with a tip box explaining it (GameManager.RequestPowerTip).
    public bool AddCharge(PowerKind kind)
    {
        if (kind == PowerKind.Lure)
        {
            if (LureCharges >= maxCharges)
            {
                return false;
            }
            LureCharges += 1;
        }
        else
        {
            if (FreezeCharges >= maxCharges)
            {
                return false;
            }
            FreezeCharges += 1;
        }

        RefreshHud();
        hud.PulsePower(kind);
        hud.ShowPowerGain(kind);
        GameManager.Instance.RequestPowerTip(kind, TipTitle(kind), TipText(kind));
        return true;
    }

    // ---- The first-time tip box (shown by GameManager, drawn by HUD) ----

    private static string TipTitle(PowerKind kind)
    {
        return kind == PowerKind.Lure ? "YOU GOT A LURE BOMB!" : "YOU GOT A FREEZE!";
    }

    private string TipText(PowerKind kind)
    {
        if (kind == PowerKind.Lure)
        {
            return "Zombies near it walk to it and crowd around it, then it explodes.\n\n"
                + "HOLD 1 to aim: a dashed arc shows where it will land.\n"
                + "ARROW KEYS steer it: LEFT / RIGHT = direction, UP / DOWN = distance.\n"
                + "RELEASE 1 to throw. (A quick tap throws it straight ahead.)";
        }
        return "Press 2 to freeze every zombie, energy orb and the boss for "
            + freezeSeconds.ToString("0.#") + " seconds.\n\n"
            + "Keep typing while they cannot move!";
    }

    // Called by GameManager whenever the combo changes (up by one per kill, or back to 1).
    // Combo 1 = no kills in a row yet, so the streak length is combo - 1.
    public void OnComboChanged(int combo)
    {
        int streak = combo - 1;

        // Just reached a multiple of killsPerReward: reward number n (1, 2, 3...).
        if (streak > 0 && streak % killsPerReward == 0)
        {
            AddCharge(RewardKind(streak / killsPerReward));
        }

        // Tell the HUD what the next reward is and how far away it is.
        int nextReward = streak / killsPerReward + 1;
        int killsToGo = killsPerReward - streak % killsPerReward;
        float progress = (float)(streak % killsPerReward) / killsPerReward;
        string rewardName = RewardKind(nextReward) == PowerKind.Lure ? "LURE BOMB" : "FREEZE";
        hud.SetComboReward(rewardName, killsToGo, progress);
    }

    // Rewards alternate: 1st = lure bomb, 2nd = freeze, 3rd = lure bomb, ...
    private static PowerKind RewardKind(int rewardNumber)
    {
        return rewardNumber % 2 == 1 ? PowerKind.Lure : PowerKind.Freeze;
    }

    // ---- Using charges (called by TypingController) ----

    // The 1 key went down: start aiming (the dashed arc shows at once).
    public void BeginLureAim()
    {
        if (LureCharges <= 0)
        {
            hud.ShowHint("No lure bombs. Earn them with a combo of " + killsPerReward + " kills, or from crates.", 3f);
            return;
        }

        aiming = true;
        aimTurn = 0f;                 // every aim starts straight ahead...
        aimDistance = throwDistance;  // ...at the usual distance
        if (arc == null)
        {
            arc = ThrowArc.Create();
        }
        hud.SetAimHint(true);
        UpdateAim();
    }

    // The 1 key came up: throw where the arc shows now (a quick tap, with no
    // arrow key pressed, throws straight ahead at throwDistance).
    public void ReleaseLureAim()
    {
        if (!aiming)
        {
            return;
        }

        Vector3 landing = LandingPoint(aimDistance);
        StopAiming();

        LureCharges -= 1;
        RefreshHud();
        hud.PulsePower(PowerKind.Lure);
        LureBomb.Throw(Hand(), landing);
    }

    // Stops aiming without throwing (e.g. the game was paused); no charge is used.
    private void StopAiming()
    {
        aiming = false;
        if (arc != null)
        {
            arc.Hide();
        }
        hud.SetAimHint(false);
    }

    // Every frame while aiming: steer with the arrow keys and redraw the arc.
    private void UpdateAim()
    {
        if (!aiming)
        {
            return;
        }
        if (GameManager.Instance.State != GameState.Playing)
        {
            StopAiming(); // paused or game over: cancel, press 1 again to aim
            return;
        }

        // Left / Right: turn the throw, within maxAimTurn of where the camera looks.
        aimTurn = Mathf.Clamp(aimTurn + arrowInput.x * aimTurnSpeed * Time.deltaTime, -maxAimTurn, maxAimTurn);

        // Up / Down: move the landing point farther / closer.
        aimDistance = Mathf.Clamp(aimDistance + arrowInput.y * aimDistanceSpeed * Time.deltaTime,
            minThrowDistance, maxThrowDistance);

        arc.Show(Hand(), LandingPoint(aimDistance));
    }

    // Called by TypingController every frame with the arrow keys held:
    // x = Right minus Left, y = Up minus Down (each -1, 0 or 1). Used only while aiming.
    public void SetAimInput(Vector2 arrows)
    {
        arrowInput = arrows;
    }

    // Where the bomb leaves the hand: just below and right of the camera.
    private static Vector3 Hand()
    {
        return Camera.main.transform.TransformPoint(new Vector3(0.2f, -0.4f, 0.5f));
    }

    public void TryUseFreeze()
    {
        if (FreezeCharges <= 0)
        {
            hud.ShowHint("No freeze left. Earn one with a combo of " + killsPerReward + " kills, or from crates.", 3f);
            return;
        }

        FreezeCharges -= 1;
        RefreshHud();
        hud.PulsePower(PowerKind.Freeze);
        freezeTimeLeft = freezeSeconds;
    }

    // Where the lure bomb lands: 'wanted' metres ahead along the ground,
    // in the direction the camera looks, but short of any wall in the way.
    private Vector3 LandingPoint(float wanted)
    {
        Transform view = Camera.main.transform;
        Vector3 ahead = view.forward;
        ahead.y = 0f;
        if (ahead.sqrMagnitude < 0.001f)
        {
            ahead = player.forward; // looking straight down or up: use the body's direction
        }
        ahead.Normalize();
        ahead = Quaternion.AngleAxis(aimTurn, Vector3.up) * ahead; // turned with Left / Right

        float distance = wanted;
        Vector3 from = player.position + Vector3.up * 1f;
        RaycastHit hit;
        if (Physics.Raycast(from, ahead, out hit, wanted))
        {
            distance = Mathf.Max(minThrowDistance, hit.distance - 0.8f);
        }

        Vector3 landing = player.position + ahead * distance;
        landing.y = 0f;
        return landing;
    }

    private void RefreshHud()
    {
        hud.SetPowers(LureCharges, FreezeCharges, maxCharges);
    }
}
