// Boss.cs
// ---------------------------------------------------------------------------
// The boss of a boss wave (every bossEveryWaves-th wave, see WaveSpawner).
// It is built entirely in code (Boss.Create), so it needs no prefab: a big
// grey figure made of a torso plus 5 typeable parts -- head, 2 arms, 2 legs.
// The torso, arms and legs are capsules, the same shape as a zombie's body;
// the head is a sphere.
//
// Fight rules:
//   - Every part carries a hard word (WordBank.PickBossWord). Typing a part's
//     word BREAKS that part (it turns dark) and deals partDamage to the boss.
//   - Breaking ALL 5 parts deals a big extra hit (allPartsBonusDamage). If the
//     boss is still alive, the parts regrow with new words after regrowSeconds.
//     With the default numbers one full round (5 x 100 + 400 = 900) kills it.
//   - Every attackInterval seconds the boss fires an EnergyOrb at the player.
//     Its body slowly turns red during the last attackWarningSeconds, as a warning.
//     The orb carries a short, case-sensitive word with symbols (e.g. "Sp@rk"):
//     typing it shoots the orb down; if it arrives (after orbFlightSeconds) the
//     player takes attackDamage.
//   - Health 0 = boss dies. WaveSpawner waits for that, then the rail moves on.
// The red health bar at the top of the screen is drawn by HUD (ShowBossBar).
// The parts' words are HUD texts too, placed by WaveSpawner.LayoutLabels.
// ---------------------------------------------------------------------------
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class Boss : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private float maxHealth = 900f;
    [SerializeField] private float partDamage = 100f;           // damage for each part's word
    [SerializeField] private float allPartsBonusDamage = 400f;  // extra damage when all 5 parts are broken
    [SerializeField] private float regrowSeconds = 1.5f;        // pause before broken parts come back

    [Header("Attack")]
    [SerializeField] private float attackInterval = 8f;         // seconds between energy orbs (0 = never attacks)
    [SerializeField] private int attackDamage = 15;             // damage if an orb reaches the player
    [SerializeField] private float attackWarningSeconds = 3f;   // the body turns red over this many seconds before an orb
    [SerializeField] private float orbFlightSeconds = 5f;       // time the player has to type the orb's word

    [Header("Look")]
    [SerializeField] private Color bodyColor = new Color(0.55f, 0.58f, 0.62f);   // normal colour (grey)
    [SerializeField] private Color attackColor = new Color(0.9f, 0.08f, 0.08f);  // colour at the moment it hits
    [SerializeField] private Color brokenColor = new Color(0.12f, 0.12f, 0.12f); // a broken part
    [SerializeField] private float shakeSeconds = 0.35f;
    [SerializeField] private float shakeStrength = 0.25f;       // metres

    public bool IsAlive { get; private set; }
    public float Health { get; private set; }

    private readonly List<BossPart> parts = new List<BossPart>();
    private readonly List<EnergyOrb> orbs = new List<EnergyOrb>(); // orbs fired and still in the air
    private Renderer torso;
    private HUD hud;
    private Vector3 homePosition;  // where the boss stands (shaking moves it around this point)
    private float attackTimer;
    private float shakeTimer;
    private bool regrowing;

    // Builds a boss standing at position (on the ground, facing the player).
    // baseMaterial is copied for every block, then tinted.
    public static Boss Create(Vector3 position, Material baseMaterial, HUD hud)
    {
        GameObject root = new GameObject("Boss");
        root.transform.position = position;
        Boss boss = root.AddComponent<Boss>();
        boss.Build(baseMaterial, hud);
        return boss;
    }

    private void Build(Material baseMaterial, HUD hudRef)
    {
        hud = hudRef;
        homePosition = transform.position;
        Health = maxHealth;
        IsAlive = true;

        // Body layout, in metres, relative to the boss's feet. The player looks
        // along +Z, so -X is the player's left and -Z is the side facing the player.
        // A Unity capsule is 1 m wide and 2 m tall at scale 1, centred on its middle.
        torso = CreateBlock("Torso", PrimitiveType.Capsule, new Vector3(0f, 4f, 0f), new Vector3(2.4f, 1.5f, 1.4f), baseMaterial);

        // Each word's pivot says which side of its anchor point it sits on:
        // the head's word is centred above it, left-side words end at their
        // anchor (pivot on the right), right-side words start at it.
        Vector2 above = new Vector2(0.5f, 0f);
        Vector2 toTheLeft = new Vector2(1f, 0.5f);
        Vector2 toTheRight = new Vector2(0f, 0.5f);

        AddPart("Head", PrimitiveType.Sphere, new Vector3(0f, 6.3f, 0f), new Vector3(1.6f, 1.6f, 1.6f),
            new Vector3(0f, 7.4f, -0.8f), above, baseMaterial);
        AddPart("LeftArm", PrimitiveType.Capsule, new Vector3(-1.75f, 4f, 0f), new Vector3(0.8f, 1.4f, 0.8f),
            new Vector3(-2.35f, 4.2f, -0.8f), toTheLeft, baseMaterial);
        AddPart("RightArm", PrimitiveType.Capsule, new Vector3(1.75f, 4f, 0f), new Vector3(0.8f, 1.4f, 0.8f),
            new Vector3(2.35f, 4.2f, -0.8f), toTheRight, baseMaterial);
        AddPart("LeftLeg", PrimitiveType.Capsule, new Vector3(-0.6f, 1.3f, 0f), new Vector3(0.9f, 1.3f, 0.9f),
            new Vector3(-1.2f, 1f, -0.8f), toTheLeft, baseMaterial);
        AddPart("RightLeg", PrimitiveType.Capsule, new Vector3(0.6f, 1.3f, 0f), new Vector3(0.9f, 1.3f, 0.9f),
            new Vector3(1.2f, 1f, -0.8f), toTheRight, baseMaterial);

        GiveEveryPartANewWord();
        hud.SetBossHealth(Health, maxHealth);
    }

    private Renderer CreateBlock(string blockName, PrimitiveType shape, Vector3 localPosition, Vector3 localScale, Material baseMaterial)
    {
        GameObject block = GameObject.CreatePrimitive(shape);
        block.name = blockName;
        Destroy(block.GetComponent<Collider>()); // nothing in this game uses physics
        block.transform.SetParent(transform, false);
        block.transform.localPosition = localPosition;
        block.transform.localScale = localScale;

        Renderer blockRenderer = block.GetComponent<Renderer>();
        blockRenderer.material = new Material(baseMaterial);
        blockRenderer.material.color = bodyColor;
        return blockRenderer;
    }

    private void AddPart(string partName, PrimitiveType shape, Vector3 localPosition, Vector3 localScale,
        Vector3 labelLocalPosition, Vector2 labelPivot, Material baseMaterial)
    {
        Renderer block = CreateBlock(partName, shape, localPosition, localScale, baseMaterial);
        TMP_Text label = hud.CreateWordLabel(labelPivot);
        parts.Add(new BossPart(this, block, label, labelLocalPosition));
    }

    // Adds every part and energy orb that can still be typed to the list
    // (used by WaveSpawner.GetTypingTargets).
    public void AddTypeableParts(List<ITypingTarget> targets)
    {
        foreach (BossPart part in parts)
        {
            if (part.IsAlive)
            {
                targets.Add(part);
            }
        }

        orbs.RemoveAll(orb => !orb.IsAlive); // forget orbs that burst or hit the player
        foreach (EnergyOrb orb in orbs)
        {
            targets.Add(orb);
        }
    }

    private void Update()
    {
        if (!IsAlive || GameManager.Instance.State != GameState.Playing)
        {
            return;
        }

        UpdateAttack();
        UpdateShake();
    }

    // ---- Attack ----

    private void UpdateAttack()
    {
        if (attackInterval <= 0f)
        {
            return;
        }

        attackTimer += Time.deltaTime;

        // Warning: 0 until the last attackWarningSeconds, then rises to 1 at the hit.
        float warning = 0f;
        if (attackWarningSeconds > 0f)
        {
            warning = Mathf.Clamp01((attackTimer - (attackInterval - attackWarningSeconds)) / attackWarningSeconds);
        }
        Color color = Color.Lerp(bodyColor, attackColor, warning);
        torso.material.color = color;
        foreach (BossPart part in parts)
        {
            if (!part.IsBroken)
            {
                part.SetColor(color);
            }
        }

        if (attackTimer >= attackInterval)
        {
            attackTimer = 0f;
            LaunchOrb();
            Shake();
        }
    }

    // Fires an energy orb from the boss's chest at the player. Its word never
    // starts with the same letter as a standing part or another orb, so the
    // first key always picks exactly one target.
    private void LaunchOrb()
    {
        Vector3 chest = transform.TransformPoint(new Vector3(0f, 4.5f, -1.2f));
        string word = WordBank.PickOrbWord(UsedFirstLetters());
        orbs.Add(EnergyOrb.Launch(chest, word, orbFlightSeconds, attackDamage, hud));
    }

    // First letters (UPPERCASE) of every word the player could type right now:
    // standing parts and orbs in the air.
    private List<char> UsedFirstLetters()
    {
        List<char> used = new List<char>();
        foreach (BossPart part in parts)
        {
            if (!part.IsBroken)
            {
                used.Add(char.ToUpperInvariant(part.Word[0]));
            }
        }
        foreach (EnergyOrb orb in orbs)
        {
            if (orb.IsAlive)
            {
                used.Add(char.ToUpperInvariant(orb.Word[0]));
            }
        }
        return used;
    }

    // ---- Taking damage (called by BossPart) ----

    public void OnPartBroken(BossPart part)
    {
        part.SetColor(brokenColor);
        GameManager.Instance.AddKill();
        TakeHit(partDamage);
        if (!IsAlive)
        {
            return;
        }

        // Were all 5 parts broken? Big bonus hit, then (if still alive) regrow.
        foreach (BossPart other in parts)
        {
            if (!other.IsBroken)
            {
                return;
            }
        }
        TakeHit(allPartsBonusDamage);
        if (IsAlive && !regrowing)
        {
            StartCoroutine(RegrowParts());
        }
    }

    private void TakeHit(float damage)
    {
        Health = Mathf.Max(0f, Health - damage);
        hud.SetBossHealth(Health, maxHealth);
        Shake();

        if (Health <= 0f)
        {
            Die();
        }
    }

    private IEnumerator RegrowParts()
    {
        regrowing = true;
        yield return new WaitForSeconds(regrowSeconds);
        regrowing = false;

        if (IsAlive)
        {
            GiveEveryPartANewWord();
        }
    }

    // Every part gets a new hard word, each with a different first letter.
    private void GiveEveryPartANewWord()
    {
        // Orbs may be in the air: keep clear of their first letters too.
        List<char> usedFirstLetters = new List<char>();
        foreach (EnergyOrb orb in orbs)
        {
            if (orb.IsAlive)
            {
                usedFirstLetters.Add(char.ToUpperInvariant(orb.Word[0]));
            }
        }

        foreach (BossPart part in parts)
        {
            string word = WordBank.PickBossWord(usedFirstLetters);
            usedFirstLetters.Add(word[0]);
            part.Regrow(word, bodyColor);
        }
    }

    // ---- Dying ----

    private void Die()
    {
        IsAlive = false; // TypingController and WaveSpawner see this right away
        GameManager.Instance.AddKill();
        foreach (BossPart part in parts)
        {
            Destroy(part.Label.gameObject); // the words are on the HUD, not children of the boss
        }
        foreach (EnergyOrb orb in orbs)
        {
            orb.Dissipate(); // orbs still in the air fizzle out harmlessly
        }
        orbs.Clear();
        StartCoroutine(DeathEffect());
    }

    // Shrinks the boss into the ground, then removes it.
    private IEnumerator DeathEffect()
    {
        const float duration = 1f;
        transform.position = homePosition;
        for (float time = 0f; time < duration; time += Time.deltaTime)
        {
            float scale = 1f - time / duration;
            transform.localScale = new Vector3(scale, scale, scale);
            yield return null;
        }
        Destroy(gameObject);
    }

    // ---- Shake ----

    private void Shake()
    {
        shakeTimer = shakeSeconds;
    }

    private void UpdateShake()
    {
        if (shakeTimer <= 0f)
        {
            transform.position = homePosition;
            return;
        }

        shakeTimer -= Time.deltaTime;
        float strength = shakeStrength * Mathf.Clamp01(shakeTimer / shakeSeconds);
        Vector2 offset = Random.insideUnitCircle * strength;
        transform.position = homePosition + new Vector3(offset.x, 0f, offset.y);
    }
}

// One typeable part of the boss (head, arm or leg). Not a MonoBehaviour: the
// Boss creates and owns these, and TypingController sees them as ITypingTarget.
public class BossPart : ITypingTarget
{
    private readonly Boss boss;
    private readonly Renderer block;
    private readonly Vector3 labelLocalPosition; // where the word sits, relative to the boss's feet

    public TMP_Text Label { get; private set; }
    public string Word { get; private set; }
    public string ColoredWord { get; private set; }
    public int TypedCount { get; private set; }
    public bool IsBroken { get; private set; }

    public BossPart(Boss owner, Renderer blockRenderer, TMP_Text label, Vector3 labelPosition)
    {
        boss = owner;
        block = blockRenderer;
        Label = label;
        labelLocalPosition = labelPosition;
        IsBroken = true; // until Regrow gives it a word
    }

    public bool IsAlive
    {
        get { return boss.IsAlive && !IsBroken; }
    }

    public bool IsTargeted
    {
        get { return TypedCount > 0; }
    }

    public bool IsCaseSensitive
    {
        get { return false; }
    }

    public Vector3 Position
    {
        get { return block.transform.position; }
    }

    public Vector3 HitPoint
    {
        get { return block.transform.position; }
    }

    public Vector3 LabelAnchor
    {
        get { return boss.transform.TransformPoint(labelLocalPosition); }
    }

    public char NextLetter
    {
        get { return Word[TypedCount]; }
    }

    public bool IsWordComplete
    {
        get { return TypedCount >= Word.Length; }
    }

    public void AdvanceProgress()
    {
        TypedCount += 1;
        RefreshLabel();
    }

    public void ResetProgress()
    {
        TypedCount = 0;
        RefreshLabel();
    }

    public void CompleteWord()
    {
        IsBroken = true;
        TypedCount = 0;
        Label.enabled = false; // WaveSpawner.LayoutLabels shows it again after Regrow
        boss.OnPartBroken(this);
    }

    // Brings the part back (or sets it up the first time) with a new word.
    public void Regrow(string word, Color color)
    {
        Word = word;
        TypedCount = 0;
        IsBroken = false;
        SetColor(color);
        RefreshLabel();
    }

    public void SetColor(Color color)
    {
        block.material.color = color;
    }

    // Same look as a zombie's word: stored UPPERCASE, shown in lowercase.
    private void RefreshLabel()
    {
        string shown = Word.ToLowerInvariant();
        ColoredWord = Zombie.TypedColorTag + shown.Substring(0, TypedCount) + "</color>" + shown.Substring(TypedCount);
        Label.text = ColoredWord;
    }
}
