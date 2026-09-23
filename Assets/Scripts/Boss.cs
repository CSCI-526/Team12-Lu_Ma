// Boss.cs
// ---------------------------------------------------------------------------
// The boss of the last area (see Level and WaveSpawner). It is built entirely
// in code (Boss.Create), so it needs no prefab: a big grey figure made of a
// torso plus 5 typeable parts -- head, 2 arms, 2 legs. The torso, arms and legs
// are capsules, the same shape as a zombie's body; the head is a sphere.
//
// Fight rules:
//   - Every part carries a hard word (WordBank.PickBossWord). Typing a part's
//     word BREAKS that part (it turns dark) and deals partDamage to the boss.
//   - QUIZ: when ALL 5 parts are broken the boss is stunned and asks a question
//     (QuizBank) with 3 answers floating in front of it (QuizAnswer). Type the
//     RIGHT answer: critical hit (quizCorrectDamage). A WRONG answer heals it
//     (quizWrongHeal) and it fires an orb at once. Too slow: nothing happens.
//     Then the parts regrow with new words after regrowSeconds.
//     With the default numbers one perfect round (5 x 100 + 400 = 900) kills it.
//   - Every attackInterval seconds the boss fires an EnergyOrb at the player.
//     Its body slowly turns red during the last attackWarningSeconds, as a warning.
//     The orb carries a short, case-sensitive word with symbols (e.g. "Sp@rk"):
//     typing it shoots the orb down; if it arrives (after orbFlightSeconds) the
//     player takes attackDamage. Below half health it gets ANGRY: orbs come faster.
//   - Explosions next to it hurt it (barrels, lure bombs: TakeBlastDamage).
//   - The freeze power stops its attacks (and the quiz timer).
//   - Health 0 = boss dies (it shrinks into the ground). WaveSpawner waits for that.
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
    [SerializeField] private float regrowSeconds = 1.5f;        // pause before broken parts come back

    [Header("Quiz")]
    [SerializeField] private float quizSeconds = 10f;           // time to type an answer
    [SerializeField] private float quizCorrectDamage = 400f;    // critical hit for the right answer
    [SerializeField] private float quizWrongHeal = 150f;        // the boss heals this much on a wrong answer

    [Header("Attack")]
    [SerializeField] private float attackInterval = 8f;         // seconds between energy orbs (0 = never attacks)
    [SerializeField] private float angryAttackInterval = 5.5f;  // below half health
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
    private readonly List<EnergyOrb> orbs = new List<EnergyOrb>();       // orbs fired and still in the air
    private readonly List<QuizAnswer> answers = new List<QuizAnswer>();  // the quiz answers on screen
    private Renderer torso;
    private HUD hud;
    private WaveSpawner spawner;
    private Vector3 homePosition;  // where the boss stands (shaking moves it around this point)
    private float attackTimer;
    private float shakeTimer;
    private bool regrowing;
    private bool stunned;          // the quiz is on
    private bool angry;

    // The quiz answer the player chose (set by OnQuizAnswer while the quiz is open).
    private bool quizOpen;
    private QuizAnswer chosenAnswer;

    // Builds the boss standing at stand (on the ground), facing the player at playerPosition.
    public static Boss Create(Vector3 stand, Vector3 playerPosition, WaveSpawner owner, HUD hud)
    {
        GameObject root = new GameObject("Boss");
        root.transform.position = stand;

        // The boss's local -Z must face the player (the parts and their words
        // are laid out that way): its forward (+Z) points AWAY from the player.
        Vector3 away = stand - playerPosition;
        away.y = 0f;
        if (away.sqrMagnitude > 0.001f)
        {
            root.transform.rotation = Quaternion.LookRotation(away);
        }

        Boss boss = root.AddComponent<Boss>();
        boss.spawner = owner;
        boss.Build(hud);
        return boss;
    }

    private void Build(HUD hudRef)
    {
        hud = hudRef;
        homePosition = transform.position;
        Health = maxHealth;
        IsAlive = true;

        // Body layout, in metres, relative to the boss's feet. -X is the player's
        // left and -Z is the side facing the player.
        // A Unity capsule is 1 m wide and 2 m tall at scale 1, centred on its middle.
        torso = CreateBlock("Torso", PrimitiveType.Capsule, new Vector3(0f, 4f, 0f), new Vector3(2.4f, 1.5f, 1.4f));

        // Each word's pivot says which side of its anchor point it sits on:
        // the head's word is centred above it, left-side words end at their
        // anchor (pivot on the right), right-side words start at it.
        Vector2 above = new Vector2(0.5f, 0f);
        Vector2 toTheLeft = new Vector2(1f, 0.5f);
        Vector2 toTheRight = new Vector2(0f, 0.5f);

        AddPart("Head", PrimitiveType.Sphere, new Vector3(0f, 6.3f, 0f), new Vector3(1.6f, 1.6f, 1.6f),
            new Vector3(0f, 7.4f, -0.8f), above);
        AddPart("LeftArm", PrimitiveType.Capsule, new Vector3(-1.75f, 4f, 0f), new Vector3(0.8f, 1.4f, 0.8f),
            new Vector3(-2.35f, 4.2f, -0.8f), toTheLeft);
        AddPart("RightArm", PrimitiveType.Capsule, new Vector3(1.75f, 4f, 0f), new Vector3(0.8f, 1.4f, 0.8f),
            new Vector3(2.35f, 4.2f, -0.8f), toTheRight);
        AddPart("LeftLeg", PrimitiveType.Capsule, new Vector3(-0.6f, 1.3f, 0f), new Vector3(0.9f, 1.3f, 0.9f),
            new Vector3(-1.2f, 1f, -0.8f), toTheLeft);
        AddPart("RightLeg", PrimitiveType.Capsule, new Vector3(0.6f, 1.3f, 0f), new Vector3(0.9f, 1.3f, 0.9f),
            new Vector3(1.2f, 1f, -0.8f), toTheRight);

        GiveEveryPartANewWord();
        hud.SetBossHealth(Health, maxHealth);
    }

    // Every block gets its OWN copy of a lit material, because its colour
    // changes on its own (red warning, broken part, frozen).
    private Renderer CreateBlock(string blockName, PrimitiveType shape, Vector3 localPosition, Vector3 localScale)
    {
        GameObject block = Shapes.Block(shape, blockName, transform, localPosition, localScale, Palette.Lit(bodyColor));
        Renderer blockRenderer = block.GetComponent<Renderer>();
        blockRenderer.material = new Material(Palette.Lit(bodyColor));
        return blockRenderer;
    }

    private Renderer AddPart(string partName, PrimitiveType shape, Vector3 localPosition, Vector3 localScale,
        Vector3 labelLocalPosition, Vector2 labelPivot)
    {
        Renderer block = CreateBlock(partName, shape, localPosition, localScale);
        TMP_Text label = hud.CreateWordLabel(labelPivot);
        parts.Add(new BossPart(this, block, label, labelLocalPosition));
        return block;
    }

    // Adds every part, energy orb and quiz answer that can still be typed to
    // the list (used by WaveSpawner.GetTypingTargets).
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

        foreach (QuizAnswer answer in answers)
        {
            if (answer.IsAlive)
            {
                targets.Add(answer);
            }
        }
    }

    // The middle of the boss's body (explosions measure from here).
    public Vector3 BodyCenter
    {
        get { return transform.position + Vector3.up * 3f; }
    }

    private void Update()
    {
        if (!IsAlive || GameManager.Instance.State != GameState.Playing)
        {
            return;
        }

        if (!stunned && !Powers.IsFrozen)
        {
            UpdateAttack();
        }
        UpdateColors();
        UpdateShake();
    }

    // ---- Attack ----

    private void UpdateAttack()
    {
        float interval = angry ? angryAttackInterval : attackInterval;
        if (interval <= 0f)
        {
            return;
        }

        attackTimer += Time.deltaTime;
        if (attackTimer >= interval)
        {
            attackTimer = 0f;
            LaunchOrb();
            Shake();
        }
    }

    // The body colour: grey, turning red before an attack (the warning),
    // icy blue while frozen. Broken parts stay dark.
    private void UpdateColors()
    {
        Color color = bodyColor;
        if (Powers.IsFrozen)
        {
            color = Palette.FrozenIce;
        }
        else if (!stunned && attackWarningSeconds > 0f)
        {
            float interval = angry ? angryAttackInterval : attackInterval;
            float warning = Mathf.Clamp01((attackTimer - (interval - attackWarningSeconds)) / attackWarningSeconds);
            color = Color.Lerp(bodyColor, attackColor, warning);
        }

        torso.material.color = color;
        foreach (BossPart part in parts)
        {
            if (!part.IsBroken)
            {
                part.SetColor(color);
            }
        }
    }

    // Fires an energy orb from the boss's chest at the player. Its word never
    // starts with the same letter as anything else on screen, so the first key
    // always picks exactly one target.
    private void LaunchOrb()
    {
        Vector3 chest = transform.TransformPoint(new Vector3(0f, 4.5f, -1.2f));
        string word = WordBank.PickOrbWord(spawner.UsedFirstLetters());
        orbs.Add(EnergyOrb.Launch(chest, word, orbFlightSeconds, attackDamage, hud));
    }

    // ---- Taking damage ----

    // Called by BossPart when its word is typed.
    public void OnPartBroken(BossPart part)
    {
        part.SetColor(brokenColor);
        GameManager.Instance.AddKill();
        TakeHit(partDamage);
        if (!IsAlive)
        {
            return;
        }

        // Were all 5 parts broken? Then the quiz.
        foreach (BossPart other in parts)
        {
            if (!other.IsBroken)
            {
                return;
            }
        }
        if (!stunned && !regrowing)
        {
            StartCoroutine(QuizRound());
        }
    }

    // An explosion (barrel, lure bomb) went off next to the boss.
    public void TakeBlastDamage(float damage)
    {
        if (IsAlive)
        {
            hud.ShowFloatingText(BodyCenter + Vector3.up * 2f, "-" + Mathf.RoundToInt(damage), Palette.WordBarrel);
            TakeHit(damage);
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
            return;
        }

        // Below half health: angry, attacks come faster.
        if (!angry && Health <= maxHealth * 0.5f)
        {
            angry = true;
            hud.ShowFloatingText(BodyCenter + Vector3.up * 3f, "IT'S ANGRY!", new Color(1f, 0.3f, 0.2f));
        }
    }

    private void Heal(float amount)
    {
        Health = Mathf.Min(maxHealth, Health + amount);
        hud.SetBossHealth(Health, maxHealth);
    }

    // ---- The quiz ----

    // Called by a QuizAnswer when the player's final shot hits it.
    public void OnQuizAnswer(QuizAnswer answer)
    {
        if (quizOpen && chosenAnswer == null)
        {
            chosenAnswer = answer;
        }
    }

    private IEnumerator QuizRound()
    {
        stunned = true;
        Shake();

        // 1. The question and its 3 answers, floating in an arc in front of the boss.
        QuizQuestion question = QuizBank.Pick(spawner.UsedFirstLetters());
        hud.ShowQuiz(question.Question);
        Tutorial.Once("quiz", "BOSS QUIZ! Type the RIGHT answer for a critical hit. A wrong one heals it!", 6f);

        Vector3[] spots =
        {
            new Vector3(-3.2f, 3.2f, -2.5f),
            new Vector3(0f, 4.3f, -3f),
            new Vector3(3.2f, 3.2f, -2.5f)
        };
        answers.Clear();
        for (int i = 0; i < question.Answers.Length && i < spots.Length; i++)
        {
            answers.Add(QuizAnswer.Create(transform.TransformPoint(spots[i]), question.Answers[i], this, hud));
        }

        // 2. Wait for an answer (the timer stops while frozen or while a
        //    killing shot is still flying to an answer).
        chosenAnswer = null;
        quizOpen = true;
        float elapsed = 0f;
        while (chosenAnswer == null && (elapsed < quizSeconds || AnswerShotInFlight()))
        {
            if (GameManager.Instance.State == GameState.Playing && !Powers.IsFrozen)
            {
                elapsed += Time.deltaTime;
            }
            hud.SetQuizTimer(1f - Mathf.Clamp01(elapsed / quizSeconds));
            yield return null;
        }
        quizOpen = false;

        // 3. The verdict.
        Vector3 textPosition = BodyCenter + Vector3.up * 3.5f;
        if (chosenAnswer == null)
        {
            hud.ShowFloatingText(textPosition, "TOO SLOW!", Color.white);
        }
        else if (chosenAnswer.Word == question.CorrectAnswer)
        {
            hud.ShowFloatingText(textPosition, "CRITICAL HIT!", Color.yellow);
            CameraDirector.Shake(0.6f);
            GameManager.Instance.AddKill();
            TakeHit(quizCorrectDamage);
        }
        else
        {
            hud.ShowFloatingText(textPosition, "WRONG! IT HEALS", new Color(1f, 0.3f, 0.2f));
            GameManager.Instance.ResetCombo();
            Heal(quizWrongHeal);
            LaunchOrb();
        }

        foreach (QuizAnswer answer in answers)
        {
            answer.Remove();
        }
        answers.Clear();
        hud.HideQuiz();
        stunned = false;

        // 4. The parts come back with new words.
        if (IsAlive)
        {
            regrowing = true;
            yield return new WaitForSeconds(regrowSeconds);
            regrowing = false;
            if (IsAlive)
            {
                GiveEveryPartANewWord();
            }
        }
    }

    // True if the player finished typing an answer and the bullet has not hit yet.
    private bool AnswerShotInFlight()
    {
        foreach (QuizAnswer answer in answers)
        {
            if (answer.IsAlive && answer.IsWordComplete)
            {
                return true;
            }
        }
        return false;
    }

    // Every part gets a new hard word, each with a different first letter.
    private void GiveEveryPartANewWord()
    {
        // Keep clear of everything else on screen (orbs, barrels...).
        List<char> usedFirstLetters = spawner.UsedFirstLetters();
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
        foreach (QuizAnswer answer in answers)
        {
            answer.Remove();
        }
        answers.Clear();
        hud.HideQuiz();
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
        Word = "?";      // a placeholder until the first Regrow
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
        if (IsBroken)
        {
            return;
        }
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
