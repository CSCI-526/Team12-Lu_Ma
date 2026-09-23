// Powers.cs
// ---------------------------------------------------------------------------
// The player's two special powers, used with the number keys:
//
//   [1] LURE BOMB - thrown about throwDistance metres ahead, where you look.
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
// Lives on the GameManager object. TypingController calls TryUseLure /
// TryUseFreeze; GameManager calls OnComboChanged every time the combo changes.
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
    [SerializeField] private float throwDistance = 9f;    // metres ahead of the player
    [SerializeField] private float minThrowDistance = 3f; // never closer than this (e.g. facing a wall)

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

    // Gives the player one more charge (never more than maxCharges).
    public void AddCharge(PowerKind kind)
    {
        if (kind == PowerKind.Lure)
        {
            LureCharges = Mathf.Min(maxCharges, LureCharges + 1);
            Tutorial.Once("lure", "You got a LURE BOMB! Press 1: zombies crowd around it, then it explodes.", 7f);
        }
        else
        {
            FreezeCharges = Mathf.Min(maxCharges, FreezeCharges + 1);
            Tutorial.Once("freeze", "You got a FREEZE! Press 2 to stop every zombie for a few seconds.", 7f);
        }

        RefreshHud();
        hud.PulsePower(kind);
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

    public void TryUseLure()
    {
        if (LureCharges <= 0)
        {
            hud.ShowHint("No lure bombs. Earn them with a combo of " + killsPerReward + " kills, or from crates.", 3f);
            return;
        }

        LureCharges -= 1;
        RefreshHud();
        hud.PulsePower(PowerKind.Lure);
        // Thrown from just below the camera.
        Vector3 hand = Camera.main.transform.TransformPoint(new Vector3(0.2f, -0.4f, 0.5f));
        LureBomb.Throw(hand, LandingPoint());
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

    // Where the lure bomb lands: throwDistance metres ahead along the ground,
    // in the direction the camera looks, but short of any wall in the way.
    private Vector3 LandingPoint()
    {
        Transform view = Camera.main.transform;
        Vector3 ahead = view.forward;
        ahead.y = 0f;
        if (ahead.sqrMagnitude < 0.001f)
        {
            ahead = player.forward; // looking straight down or up: use the body's direction
        }
        ahead.Normalize();

        float distance = throwDistance;
        Vector3 from = player.position + Vector3.up * 1f;
        RaycastHit hit;
        if (Physics.Raycast(from, ahead, out hit, throwDistance))
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
