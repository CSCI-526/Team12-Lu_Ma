// HUD.cs
// ---------------------------------------------------------------------------
// Everything drawn on the screen-space Canvas:
//   - health bar (top-left)
//   - "Wave 2 / 5", Score and Combo (top-right). Under them: what the combo
//     earns next ("Next: FREEZE in 3 kills" + a thin bar)
//   - the powers (bottom-left): "[1] LURE BOMB" and "[2] FREEZE" with their charges
//   - the current target's word (bottom-centre) and a hint box just above it
//   - red arrows at the screen edges pointing at zombies you cannot see
//   - floating texts ("+30 HEALTH") that rise from a point in the world
//   - the "Wave N" banner, the multi-kill popup and the red wrong-key flash
//   - the red boss health bar (top-centre) and the boss quiz box under it
//   - the enemies' words (CreateWordLabel). They are drawn on this Canvas, on
//     top of the 3D scene, so an enemy's body can never hide another's word.
//     WaveSpawner.LayoutLabels moves them to their enemies every frame.
//   - the Start, "You survived" and "You died" panels (with the run's stats
//     and a grade at the end), the pause panel and the 3-2-1 resume countdown
//
// The HUD never decides anything. Other scripts call these methods when a value
// changes (for example GameManager calls SetScore after a kill).
// The objects saved in the scene are wired in the scene (Prototype.unity);
// everything else is built here in code the first time it is needed (see the
// Build... methods).
//
// No image files: an Image component WITHOUT a sprite draws a plain rectangle
// in its colour, and every text is TextMeshPro with its default font.
//
// DRAWING ORDER. uGUI draws a Canvas's children in order: a later child is
// drawn on top of an earlier one. From back to front:
//   enemy words < readouts (health, score, powers, quiz...) < markers (threat
//   arrows, floating texts) < hint < red flash < Start/Won/Lost panels
//   < pause panel. EnsureLayers creates one empty full-screen "layer" object
//   per group so the pieces built later still land at the right depth.
// ---------------------------------------------------------------------------
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HUD : MonoBehaviour
{
    [Header("Readouts")]
    [SerializeField] private Slider healthBar;
    [SerializeField] private TMP_Text waveText;
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text comboText;
    [SerializeField] private TMP_Text targetWordText;

    [Header("Wave banner")]
    [SerializeField] private TMP_Text bannerText;

    [Header("Wrong-key flash")]
    [SerializeField] private Image flashImage;           // full-screen red image, normally invisible
    [SerializeField] private float flashDuration = 0.15f; // seconds
    [SerializeField] private float flashMaxAlpha = 0.35f; // how strong the red is at the start of a flash

    [Header("Panels")]
    [SerializeField] private GameObject startPanel;
    [SerializeField] private GameObject wonPanel;
    [SerializeField] private GameObject lostPanel;
    [SerializeField] private TMP_Text wonScoreText;
    [SerializeField] private TMP_Text lostScoreText;

    [Header("Boss health bar (built in code, see BuildBossBar)")]
    [SerializeField] private Vector2 bossBarSize = new Vector2(700f, 26f);
    [SerializeField] private float bossBarTopMargin = 70f; // pixels from the top of the screen
    [SerializeField] private Color bossBarColor = new Color(0.85f, 0.1f, 0.1f);

    [Header("Enemy words (built in code, see CreateWordLabel)")]
    [SerializeField] private float wordFontSize = 40f;   // size at scale 1 (see WaveSpawner.LayoutLabels)
    [SerializeField] private float wordOutlineWidth = 0.25f;

    [Header("Powers (bottom-left, built in code)")]
    [SerializeField] private float powersMargin = 40f;        // canvas units from the left and bottom edges
    [SerializeField] private float powerPulseSeconds = 0.4f;  // PulsePower: how long a row grows and flashes
    [SerializeField] private float powerPulseScale = 1.25f;   // PulsePower: row size at the top of the pulse
    [SerializeField] private float powerEmptyAlpha = 0.4f;    // a power with no charges is dimmed to this

    [Header("Threat arrows (built in code)")]
    [SerializeField] private Color threatArrowColor = new Color(1f, 0.18f, 0.12f);  // red
    [SerializeField] private float threatArrowInset = 70f;    // canvas units between an arrow and the screen edge
    [SerializeField] private float threatNearDistance = 8f;   // metres: closer threats get a big, fully opaque arrow
    [SerializeField] private float threatFarDistance = 30f;   // metres: farther threats get a small, faint arrow

    [Header("Feedback (built in code)")]
    [SerializeField] private float floatingTextSeconds = 1.1f;  // lifetime of a floating text
    [SerializeField] private float floatingTextRise = 70f;      // canvas units it rises during its life

    // Anchor and pivot points used by the code-built pieces (0,0 = bottom-left, 1,1 = top-right).
    private static readonly Vector2 Centre = new Vector2(0.5f, 0.5f);
    private static readonly Vector2 LeftMiddle = new Vector2(0f, 0.5f);
    private static readonly Vector2 BottomLeft = new Vector2(0f, 0f);
    private static readonly Vector2 BottomCentre = new Vector2(0.5f, 0f);
    private static readonly Vector2 TopCentre = new Vector2(0.5f, 1f);
    private static readonly Vector2 TopRight = new Vector2(1f, 1f);

    private static readonly Color Yellow = new Color(0.95f, 0.75f, 0.1f); // hint strip, reward bar

    private float flashTimer; // counts down from flashDuration to 0 while a flash is fading

    private RectTransform wordLayer;  // full-screen parent of every enemy word
    private Material wordMaterial;    // the font with a black outline, shared by every outlined text

    private GameObject pausePanel;    // null until the first pause
    private TMP_Text countdownText;   // null until the first resume

    private GameObject bossBar;       // null until the first boss fight
    private RectTransform bossBarFill;
    private TMP_Text bossBarTitle;

    private void Awake()
    {
        // The red flash is invisible until the first wrong key. Switching it off
        // saves drawing an invisible full-screen rectangle every frame.
        if (flashImage != null)
        {
            SetFlashAlpha(0f);
        }
    }

    private void Update()
    {
        UpdateMultiKill();
        UpdateRedFlash();
        UpdatePowerPulses();
        UpdateComboRewardBar();
        UpdateHint();
        UpdateQuizPop();
        UpdateGradeReveal();
    }

    private void LateUpdate()
    {
        // After every Update, so the camera has already moved this frame and the
        // texts sit exactly on their world points.
        UpdateFloatingTexts();
    }

    // ---- Readouts ----

    public void SetHealth(int health, int maxHealth)
    {
        healthBar.maxValue = maxHealth;
        healthBar.value = health;
    }

    public void SetWave(int wave, int waveCount)
    {
        waveText.text = "Wave " + wave + " / " + waveCount;
    }

    public void SetScore(int score)
    {
        scoreText.text = "Score " + score;
    }

    public void SetCombo(int combo)
    {
        comboText.text = "Combo x" + combo;
    }

    // richText is the word with TextMeshPro colour tags (see Zombie.ColoredWord),
    // or "" when there is no target.
    public void SetTargetWord(string richText)
    {
        targetWordText.text = richText;
    }

    // ---- Banner ----

    public void ShowBanner(string message)
    {
        bannerText.text = message;
        bannerText.gameObject.SetActive(true);
    }

    public void HideBanner()
    {
        bannerText.gameObject.SetActive(false);
    }

    // ---- Wrong-key flash ----

    public void FlashRed()
    {
        if (flashDuration <= 0f)
        {
            return; // a duration of 0 switches the flash off (it could never fade out)
        }

        flashTimer = flashDuration;
        SetFlashAlpha(flashMaxAlpha);
    }

    private void UpdateRedFlash()
    {
        if (flashTimer <= 0f)
        {
            return;
        }

        // Fade the red from flashMaxAlpha down to 0.
        flashTimer -= Time.unscaledDeltaTime; // unscaled: keeps fading while the game is paused
        float alpha = flashMaxAlpha * Mathf.Clamp01(flashTimer / flashDuration);
        SetFlashAlpha(alpha);
    }

    private void SetFlashAlpha(float alpha)
    {
        Color color = flashImage.color;
        color.a = alpha;
        flashImage.color = color;
        flashImage.enabled = alpha > 0f; // nothing to draw when it is fully transparent
    }

    // ---- Multi-kill popup ----
    // "TRIPLE KILL!" with the points under it, above the middle of the screen.
    // It pops in big, settles to normal size, stays a moment, then fades out.

    private const float MultiKillPopSeconds = 0.15f;  // shrinking from big to normal size
    private const float MultiKillHoldSeconds = 0.9f;  // fully visible
    private const float MultiKillFadeSeconds = 0.4f;  // fading out
    private const float MultiKillPopScale = 1.6f;

    private TMP_Text multiKillText; // null until the first multi-kill
    private float multiKillAge = float.MaxValue;

    public void ShowMultiKill(int kills, int points)
    {
        if (multiKillText == null)
        {
            multiKillText = CreateText(transform, "MultiKill", "", 96f, new Vector2(0f, 230f));
            multiKillText.color = new Color(1f, 0.6f, 0.15f);
            multiKillText.rectTransform.sizeDelta = new Vector2(1400f, 220f);
            multiKillText.fontStyle = FontStyles.Bold;
        }

        string title;
        if (kills == 2)
        {
            title = "DOUBLE KILL!";
        }
        else if (kills == 3)
        {
            title = "TRIPLE KILL!";
        }
        else if (kills == 4)
        {
            title = "QUAD KILL!";
        }
        else
        {
            title = "MASSACRE!";
        }

        multiKillText.text = title + "\n<size=55%>x" + kills + " SCORE  +" + points + "</size>";
        multiKillText.transform.SetAsLastSibling();
        multiKillText.gameObject.SetActive(true);
        multiKillAge = 0f;
        UpdateMultiKill();
    }

    private void UpdateMultiKill()
    {
        if (multiKillText == null || !multiKillText.gameObject.activeSelf)
        {
            return;
        }

        float total = MultiKillPopSeconds + MultiKillHoldSeconds + MultiKillFadeSeconds;
        if (multiKillAge >= total)
        {
            multiKillText.gameObject.SetActive(false);
            return;
        }

        // Scale: big -> normal during the pop.
        float pop = Mathf.Clamp01(multiKillAge / MultiKillPopSeconds);
        float scale = Mathf.Lerp(MultiKillPopScale, 1f, pop);
        multiKillText.transform.localScale = new Vector3(scale, scale, 1f);

        // Alpha: 1, then down to 0 during the fade.
        float fadeStart = MultiKillPopSeconds + MultiKillHoldSeconds;
        multiKillText.alpha = 1f - Mathf.Clamp01((multiKillAge - fadeStart) / MultiKillFadeSeconds);

        multiKillAge += Time.deltaTime; // game time: the popup freezes while paused
    }

    // ---- Panels ----

    public void ShowStartPanel()
    {
        HideAllPanels();
        startPanel.SetActive(true);
    }

    public void ShowWonPanel(int finalScore)
    {
        HideAllPanels();
        SetTargetWord(""); // typing is over, so clear the word at the bottom
        ClearResults(wonScoreText, wonGrade);
        wonScoreText.text = "Final score: " + finalScore;
        wonPanel.SetActive(true);
    }

    public void ShowLostPanel(int finalScore)
    {
        HideAllPanels();
        SetTargetWord("");
        ClearResults(lostScoreText, lostGrade);
        lostScoreText.text = "Final score: " + finalScore;
        lostPanel.SetActive(true);
    }

    public void HideAllPanels()
    {
        startPanel.SetActive(false);
        wonPanel.SetActive(false);
        lostPanel.SetActive(false);
        HideBanner();
        HideBossBar();
        HidePausePanel();
        HideCountdown();
        HideQuiz();
        if (multiKillText != null)
        {
            multiKillText.gameObject.SetActive(false);
        }
    }

    // ---- Boss health bar (top-centre) ----

    public void ShowBossBar(string title)
    {
        if (bossBar == null)
        {
            BuildBossBar();
        }
        bossBarTitle.text = title;
        SetBossHealth(1f, 1f);
        bossBar.SetActive(true);
    }

    public void SetBossHealth(float health, float maxHealth)
    {
        if (bossBar == null)
        {
            return;
        }
        // The fill is anchored to the left edge; its right anchor is the health fraction.
        float fraction = maxHealth > 0f ? Mathf.Clamp01(health / maxHealth) : 0f;
        bossBarFill.anchorMax = new Vector2(fraction, 1f);
    }

    public void HideBossBar()
    {
        if (bossBar != null)
        {
            bossBar.SetActive(false);
        }
    }

    // Creates the bar under this Canvas the first time it is needed:
    //   BossBar (dark background) > Fill (red) + Title (text above the bar).
    // An Image without a sprite simply draws a solid rectangle in its colour.
    private void BuildBossBar()
    {
        bossBar = new GameObject("BossBar", typeof(RectTransform), typeof(Image));
        RectTransform barRect = bossBar.GetComponent<RectTransform>();
        barRect.SetParent(transform, false);
        barRect.SetAsFirstSibling(); // drawn under the panels and the red flash
        barRect.anchorMin = new Vector2(0.5f, 1f);
        barRect.anchorMax = new Vector2(0.5f, 1f);
        barRect.pivot = new Vector2(0.5f, 1f);
        barRect.anchoredPosition = new Vector2(0f, -bossBarTopMargin);
        barRect.sizeDelta = bossBarSize;
        bossBar.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);

        GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        bossBarFill = fill.GetComponent<RectTransform>();
        bossBarFill.SetParent(barRect, false);
        bossBarFill.anchorMin = Vector2.zero;
        bossBarFill.anchorMax = Vector2.one;
        bossBarFill.offsetMin = new Vector2(3f, 3f);   // small dark border around the red
        bossBarFill.offsetMax = new Vector2(-3f, -3f);
        fill.GetComponent<Image>().color = bossBarColor;

        GameObject title = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform titleRect = title.GetComponent<RectTransform>();
        titleRect.SetParent(barRect, false);
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 0f);
        titleRect.anchoredPosition = new Vector2(0f, 4f);
        titleRect.sizeDelta = new Vector2(0f, 40f);
        bossBarTitle = title.GetComponent<TextMeshProUGUI>();
        bossBarTitle.fontSize = 32;
        bossBarTitle.alignment = TextAlignmentOptions.Center;
        bossBarTitle.color = Color.white;
    }

    // ---- Enemy words ----

    // Creates a word for an enemy (a zombie or a boss part). pivot says which point
    // of the word sits on the enemy's label anchor: (0.5, 0) = centred above it,
    // (1, 0.5) = to its left, (0, 0.5) = to its right.
    // The word starts hidden; WaveSpawner.LayoutLabels shows it once it has
    // placed it (on its enemy, or just inside the screen edge when the enemy is
    // off to a side), so a word never flashes up anywhere else first.
    // The owner destroys the word's GameObject when the enemy goes away.
    public TMP_Text CreateWordLabel(Vector2 pivot)
    {
        if (wordLayer == null)
        {
            GameObject layer = new GameObject("EnemyWords", typeof(RectTransform));
            wordLayer = layer.GetComponent<RectTransform>();
            wordLayer.SetParent(transform, false);
            wordLayer.SetAsFirstSibling(); // under every other part of the HUD
            wordLayer.anchorMin = Vector2.zero;
            wordLayer.anchorMax = Vector2.one;
            wordLayer.offsetMin = Vector2.zero;
            wordLayer.offsetMax = Vector2.zero;
        }

        GameObject word = new GameObject("Word", typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform rect = word.GetComponent<RectTransform>();
        rect.SetParent(wordLayer, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = pivot;

        TextMeshProUGUI label = word.GetComponent<TextMeshProUGUI>();
        label.fontSize = wordFontSize;
        label.color = Color.white;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;
        label.verticalAlignment = VerticalAlignmentOptions.Middle;
        if (pivot.x < 0.25f)
        {
            label.horizontalAlignment = HorizontalAlignmentOptions.Left;
        }
        else if (pivot.x > 0.75f)
        {
            label.horizontalAlignment = HorizontalAlignmentOptions.Right;
        }
        else
        {
            label.horizontalAlignment = HorizontalAlignmentOptions.Center;
        }

        // A black outline keeps the white word readable over any background.
        label.fontSharedMaterial = OutlineMaterial(label);

        label.enabled = false;
        return label;
    }

    // The RectTransform the words live in (WaveSpawner converts screen points into it).
    public RectTransform WordLayer
    {
        get { return wordLayer; }
    }

    // The font with a black outline. One material shared by every outlined text
    // (a material per text would be wasteful): enemy words, floating texts,
    // and the power names.
    private Material OutlineMaterial(TMP_Text sample)
    {
        if (wordMaterial == null)
        {
            wordMaterial = new Material(sample.fontSharedMaterial);
            wordMaterial.EnableKeyword(ShaderUtilities.Keyword_Outline);
            wordMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, wordOutlineWidth);
            wordMaterial.SetColor(ShaderUtilities.ID_OutlineColor, Color.black);
        }
        return wordMaterial;
    }

    // ---- Pause ----

    public void ShowPausePanel()
    {
        if (pausePanel == null)
        {
            BuildPausePanel();
        }
        pausePanel.transform.SetAsLastSibling(); // on top of everything
        pausePanel.SetActive(true);
    }

    public void HidePausePanel()
    {
        if (pausePanel != null)
        {
            pausePanel.SetActive(false);
        }
    }

    // Big number in the middle of the screen while the game is about to resume.
    public void ShowCountdown(string text)
    {
        if (countdownText == null)
        {
            countdownText = CreateText(transform, "Countdown", "", 220f, Vector2.zero);
            countdownText.color = new Color(1f, 0.82f, 0.12f);
        }
        countdownText.transform.SetAsLastSibling();
        countdownText.text = text;
        countdownText.gameObject.SetActive(true);
    }

    public void HideCountdown()
    {
        if (countdownText != null)
        {
            countdownText.gameObject.SetActive(false);
        }
    }

    // PausePanel (dark full-screen) > "PAUSED", hint text, Resume and Restart buttons.
    private void BuildPausePanel()
    {
        pausePanel = new GameObject("PausePanel", typeof(RectTransform), typeof(Image));
        RectTransform panelRect = pausePanel.GetComponent<RectTransform>();
        panelRect.SetParent(transform, false);
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        pausePanel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.7f);

        CreateText(panelRect, "Title", "PAUSED", 110f, new Vector2(0f, 180f));
        CreateText(panelRect, "Hint", "Press Esc to resume", 36f, new Vector2(0f, 80f));
        CreateButton(panelRect, "ResumeButton", "Resume", new Vector2(0f, -40f), OnResumeClicked);
        CreateButton(panelRect, "RestartButton", "Restart", new Vector2(0f, -150f), OnRestartClicked);
    }

    private void OnResumeClicked()
    {
        GameManager.Instance.ResumeGame();
    }

    private void OnRestartClicked()
    {
        GameManager.Instance.RestartGame();
    }

    private TMP_Text CreateText(Transform parent, string objectName, string text, float fontSize, Vector2 position)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(1200f, fontSize * 1.4f);

        TextMeshProUGUI label = textObject.GetComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.raycastTarget = false;
        return label;
    }

    // A grey button with a text on it. Returns the text, so it can be changed later.
    private TMP_Text CreateButton(Transform parent, string objectName, string text, Vector2 position,
        UnityEngine.Events.UnityAction onClick)
    {
        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(320f, 80f);
        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.25f, 0.25f, 0.28f);

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image; // darkens when hovered / pressed
        button.onClick.AddListener(onClick);

        // The keyboard is for typing only: the arrow keys must not move a
        // "selection" from button to button (same as the scene's buttons).
        Navigation navigation = button.navigation;
        navigation.mode = Navigation.Mode.None;
        button.navigation = navigation;

        TMP_Text label = CreateText(rect, "Text", text, 40f, Vector2.zero);
        label.rectTransform.sizeDelta = rect.sizeDelta;
        return label;
    }

    // =====================================================================
    // The newer HUD pieces. Everything below is built in code the first time
    // it is needed, like the boss bar and pause panel.
    // =====================================================================

    // ---- Drawing layers ----
    // Empty full-screen objects that keep the pieces below in the drawing order
    // described at the top of this file. They are created together, all just
    // under the red wrong-key flash (so also under the Start/Won/Lost panels).

    private RectTransform readoutLayer;   // powers, combo reward, quiz
    private RectTransform markerLayer;    // threat arrows, floating texts
    private RectTransform hintLayer;      // hint box

    private void EnsureLayers()
    {
        if (readoutLayer != null)
        {
            return; // already created
        }

        // Each new layer is inserted just under the red flash, so each one ends
        // up on top of the layer created before it.
        readoutLayer = CreateLayer("Readouts");
        markerLayer = CreateLayer("Markers");
        hintLayer = CreateLayer("Hint");
    }

    private RectTransform CreateLayer(string layerName)
    {
        RectTransform layer = CreateRect(layerName, transform);
        StretchToParent(layer);
        layer.SetSiblingIndex(LayerInsertIndex());
        return layer;
    }

    // Where a new layer goes among the Canvas's children: at the red flash's
    // place, which pushes the flash (and everything after it) one step up.
    private int LayerInsertIndex()
    {
        if (flashImage != null && flashImage.transform.parent == transform)
        {
            return flashImage.transform.GetSiblingIndex();
        }
        if (startPanel != null && startPanel.transform.parent == transform)
        {
            return startPanel.transform.GetSiblingIndex();
        }
        return transform.childCount - 1;
    }

    // ---- Powers (bottom-left) ----
    // Two rows, one per power. Row 0 = lure bomb (on top), row 1 = freeze: the
    // same numbers as PowerKind, so (int)kind gives the row. Each row:
    //   [1] LURE BOMB  [#][#][ ]
    // a white key cap with the key to press, the power's name, then one small
    // square ("pip") per possible charge, filled in the power's colour.

    private const int PowerCount = 2;
    private const float PowerRowHeight = 44f;
    private const float PowerRowGap = 10f;
    private const float PowerKeySize = 40f;
    private const float PowerNameX = 54f;       // the name starts this far right of the row's left end
    private const float PowerNameWidth = 190f;
    private const float PowerPipsX = 250f;      // the pips start this far right of the row's left end
    private const float PowerPipSize = 24f;
    private const float PowerPipGap = 6f;
    private const float PowerPipPadding = 4f;   // dark border around the pips
    private static readonly Color EmptyPipColor = new Color(0.3f, 0.31f, 0.35f);

    private RectTransform powersPanel;          // null until the first SetPowers / PulsePower
    private readonly RectTransform[] powerRows = new RectTransform[PowerCount];
    private readonly CanvasGroup[] powerRowGroups = new CanvasGroup[PowerCount]; // dims a whole row at once
    private readonly Image[] powerKeyCaps = new Image[PowerCount];
    private readonly TMP_Text[] powerNames = new TMP_Text[PowerCount];
    private readonly RectTransform[] powerPipStrips = new RectTransform[PowerCount]; // dark strip behind the pips
    private readonly List<Image> lurePips = new List<Image>();
    private readonly List<Image> freezePips = new List<Image>();
    private readonly int[] powerCharges = new int[PowerCount];
    private readonly float[] powerPulseTimers = new float[PowerCount]; // counts down while a row pulses

    public void SetPowers(int lureCharges, int freezeCharges, int maxCharges)
    {
        if (powersPanel == null)
        {
            BuildPowers();
        }

        powerCharges[(int)PowerKind.Lure] = lureCharges;
        powerCharges[(int)PowerKind.Freeze] = freezeCharges;
        for (int row = 0; row < PowerCount; row++)
        {
            ShowPips(row, maxCharges);
            if (powerPulseTimers[row] <= 0f)
            {
                ApplyPowerRowLook(row, 0f, 0f); // a pulsing row is updated by UpdatePowerPulses
            }
        }
    }

    // Makes one power slot flash and grow for a moment (a charge was just earned or used).
    public void PulsePower(PowerKind kind)
    {
        if (powersPanel == null)
        {
            BuildPowers();
        }

        int row = (int)kind;
        powerPulseTimers[row] = Mathf.Max(0.01f, powerPulseSeconds);
        powerRows[row].SetAsLastSibling(); // while it is big, it is drawn over the other row
    }

    private void UpdatePowerPulses()
    {
        if (powersPanel == null)
        {
            return;
        }

        for (int row = 0; row < PowerCount; row++)
        {
            if (powerPulseTimers[row] <= 0f)
            {
                continue;
            }

            powerPulseTimers[row] -= Time.unscaledDeltaTime; // unscaled: also pulses in the pause
            if (powerPulseTimers[row] <= 0f)
            {
                ApplyPowerRowLook(row, 0f, 0f); // back to normal
                continue;
            }

            // progress goes 0 -> 1. The size goes up and back down (half a sine
            // wave); the colour starts at full strength and fades back to white.
            float progress = 1f - powerPulseTimers[row] / Mathf.Max(0.01f, powerPulseSeconds);
            ApplyPowerRowLook(row, Mathf.Sin(progress * Mathf.PI), 1f - progress);
        }
    }

    // grow: 0 = normal size .. 1 = powerPulseScale.
    // flash: 0 = normal colours .. 1 = key cap and name in the power's colour.
    private void ApplyPowerRowLook(int row, float grow, float flash)
    {
        float scale = Mathf.Lerp(1f, powerPulseScale, grow);
        powerRows[row].localScale = new Vector3(scale, scale, 1f);

        Color color = PowerColor(row);
        powerKeyCaps[row].color = Color.Lerp(Color.white, color, flash);
        powerNames[row].color = Color.Lerp(Color.white, color, flash);

        // No charges: the whole row is dimmed (but fully visible while it flashes).
        float restAlpha = powerCharges[row] > 0 ? 1f : powerEmptyAlpha;
        powerRowGroups[row].alpha = Mathf.Lerp(restAlpha, 1f, flash);
    }

    private static Color PowerColor(int row)
    {
        return row == (int)PowerKind.Lure ? Palette.LureCrate : Palette.FreezeCrate;
    }

    // Shows maxCharges pips in the row (creating more when needed), filled for
    // each charge the power has.
    private void ShowPips(int row, int maxCharges)
    {
        List<Image> pips = row == (int)PowerKind.Lure ? lurePips : freezePips;
        int shown = Mathf.Max(0, maxCharges);

        while (pips.Count < shown)
        {
            Image pip = CreateImage("Pip", powerPipStrips[row], EmptyPipColor);
            float x = PowerPipPadding + pips.Count * (PowerPipSize + PowerPipGap);
            PlaceRect(pip.rectTransform, LeftMiddle, LeftMiddle, new Vector2(x, 0f), new Vector2(PowerPipSize, PowerPipSize));
            pips.Add(pip);
        }

        for (int i = 0; i < pips.Count; i++)
        {
            bool visible = i < shown;
            if (pips[i].gameObject.activeSelf != visible)
            {
                pips[i].gameObject.SetActive(visible);
            }
            pips[i].color = i < powerCharges[row] ? PowerColor(row) : EmptyPipColor;
        }

        // The dark strip hugs the visible pips.
        float stripWidth = 0f;
        if (shown > 0)
        {
            stripWidth = PowerPipPadding * 2f + shown * PowerPipSize + (shown - 1) * PowerPipGap;
        }
        powerPipStrips[row].sizeDelta = new Vector2(stripWidth, PowerPipSize + PowerPipPadding * 2f);
    }

    // Powers (bottom-left corner) > one row per power > KeyCap (+ Key text), Name, Pips.
    private void BuildPowers()
    {
        EnsureLayers();
        float panelHeight = PowerCount * PowerRowHeight + (PowerCount - 1) * PowerRowGap;
        powersPanel = CreateRect("Powers", readoutLayer);
        PlaceRect(powersPanel, BottomLeft, BottomLeft, new Vector2(powersMargin, powersMargin),
            new Vector2(PowerPipsX + 120f, panelHeight));

        for (int row = 0; row < PowerCount; row++)
        {
            bool isLure = row == (int)PowerKind.Lure;
            string key = isLure ? "1" : "2";
            string powerName = isLure ? "LURE BOMB" : "FREEZE";

            // Rows are stacked from the bottom: the last row sits at the bottom.
            // The pivot is the row's left-middle, so a pulsing row grows to the right.
            float y = (PowerCount - 1 - row) * (PowerRowHeight + PowerRowGap) + PowerRowHeight * 0.5f;
            RectTransform rowRect = CreateRect(powerName, powersPanel);
            PlaceRect(rowRect, BottomLeft, LeftMiddle, new Vector2(0f, y), new Vector2(PowerPipsX + 120f, PowerRowHeight));
            powerRows[row] = rowRect;
            powerRowGroups[row] = rowRect.gameObject.AddComponent<CanvasGroup>();

            Image keyCap = CreateImage("KeyCap", rowRect, Color.white);
            PlaceRect(keyCap.rectTransform, LeftMiddle, LeftMiddle, Vector2.zero, new Vector2(PowerKeySize, PowerKeySize));
            powerKeyCaps[row] = keyCap;
            TMP_Text keyText = CreateText(keyCap.rectTransform, "Key", key, 30f, Vector2.zero);
            StretchToParent(keyText.rectTransform);
            keyText.color = Color.black;
            keyText.fontStyle = FontStyles.Bold;

            TMP_Text nameText = CreateText(rowRect, "Name", powerName, 30f, Vector2.zero);
            PlaceRect(nameText.rectTransform, LeftMiddle, LeftMiddle, new Vector2(PowerNameX, 0f),
                new Vector2(PowerNameWidth, PowerRowHeight));
            nameText.alignment = TextAlignmentOptions.Left;
            nameText.fontStyle = FontStyles.Bold;
            nameText.textWrappingMode = TextWrappingModes.NoWrap;
            nameText.fontSharedMaterial = OutlineMaterial(nameText);
            powerNames[row] = nameText;

            Image strip = CreateImage("Pips", rowRect, new Color(0f, 0f, 0f, 0.55f));
            PlaceRect(strip.rectTransform, LeftMiddle, LeftMiddle, new Vector2(PowerPipsX, 0f), Vector2.zero);
            powerPipStrips[row] = strip.rectTransform;

            ApplyPowerRowLook(row, 0f, 0f);
        }
    }

    // ---- Combo reward (top-right, under the Combo text) ----

    private const float RightMargin = 40f;        // the same margin as the Wave / Score / Combo texts
    private const float ComboRewardTop = 194f;    // the Combo text ends 190 units below the top of the screen
    private const float ComboBarTop = 234f;
    private const float ComboBarSpeed = 2.5f;     // the bar fills / empties at up to 2.5 whole bars per second
    private static readonly Vector2 ComboBarSize = new Vector2(260f, 8f);
    private static readonly Color SmallTextColor = new Color(0.8f, 0.82f, 0.86f); // light grey

    private TMP_Text comboRewardText;       // null until the first SetComboReward
    private RectTransform comboRewardBar;   // dark background of the bar
    private RectTransform comboRewardFill;
    private Image comboRewardFillImage;
    private float comboRewardTarget;        // the progress to show (0..1)
    private float comboRewardShown;         // the progress drawn now; slides toward comboRewardTarget
    private string lastComboReward;         // what the text shows now, so it is only rebuilt when it changes
    private int lastComboKillsToGo = -1;

    // Under the combo text: what the combo earns next and how close it is,
    // e.g. SetComboReward("FREEZE", 3, 0.4f) -> "Next: FREEZE in 3 kills" with a 40% bar.
    // An empty nextReward hides the line and the bar.
    public void SetComboReward(string nextReward, int killsToGo, float progress)
    {
        if (string.IsNullOrEmpty(nextReward))
        {
            if (comboRewardText != null)
            {
                comboRewardText.gameObject.SetActive(false);
                comboRewardBar.gameObject.SetActive(false);
            }
            return;
        }

        if (comboRewardText == null)
        {
            BuildComboReward();
        }
        if (!comboRewardText.gameObject.activeSelf)
        {
            comboRewardText.gameObject.SetActive(true);
            comboRewardBar.gameObject.SetActive(true);
        }
        comboRewardTarget = Mathf.Clamp01(progress);

        // Only build a new string when the text really changes.
        if (nextReward != lastComboReward || killsToGo != lastComboKillsToGo)
        {
            lastComboReward = nextReward;
            lastComboKillsToGo = killsToGo;

            string when;
            if (killsToGo <= 0)
            {
                when = " now!";
            }
            else if (killsToGo == 1)
            {
                when = " in 1 kill";
            }
            else
            {
                when = " in " + killsToGo + " kills";
            }

            Color color = RewardColor(nextReward);
            string colorTag = "<color=#" + ColorUtility.ToHtmlStringRGB(color) + ">";
            comboRewardText.text = "Next: " + colorTag + nextReward + "</color>" + when;
            comboRewardFillImage.color = color;
        }
    }

    // The colour of a reward: cyan for the freeze, orange for the lure bomb.
    private static Color RewardColor(string reward)
    {
        if (reward.IndexOf("FREEZE", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return Palette.FreezeCrate;
        }
        if (reward.IndexOf("LURE", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return Palette.LureCrate;
        }
        return Yellow;
    }

    private void UpdateComboRewardBar()
    {
        if (comboRewardFill == null || comboRewardShown == comboRewardTarget)
        {
            return;
        }

        // Slide toward the real progress instead of jumping there.
        comboRewardShown = Mathf.MoveTowards(comboRewardShown, comboRewardTarget, ComboBarSpeed * Time.unscaledDeltaTime);
        comboRewardFill.anchorMax = new Vector2(comboRewardShown, 1f);
    }

    // ComboReward (text) and ComboRewardBar (dark) > Fill, right-aligned under the Combo text.
    private void BuildComboReward()
    {
        EnsureLayers();
        comboRewardText = CreateText(readoutLayer, "ComboReward", "", 28f, Vector2.zero);
        PlaceRect(comboRewardText.rectTransform, TopRight, TopRight, new Vector2(-RightMargin, -ComboRewardTop),
            new Vector2(700f, 36f));
        comboRewardText.alignment = TextAlignmentOptions.Right;
        comboRewardText.textWrappingMode = TextWrappingModes.NoWrap;
        comboRewardText.color = SmallTextColor;

        Image bar = CreateImage("ComboRewardBar", readoutLayer, new Color(0f, 0f, 0f, 0.6f));
        comboRewardBar = bar.rectTransform;
        PlaceRect(comboRewardBar, TopRight, TopRight, new Vector2(-RightMargin, -ComboBarTop), ComboBarSize);

        // The fill is anchored to the bar's left edge; its right anchor is the progress.
        comboRewardFillImage = CreateImage("Fill", comboRewardBar, Yellow);
        comboRewardFill = comboRewardFillImage.rectTransform;
        comboRewardFill.anchorMin = Vector2.zero;
        comboRewardFill.anchorMax = new Vector2(0f, 1f);
        comboRewardFill.offsetMin = Vector2.zero;
        comboRewardFill.offsetMax = Vector2.zero;
        comboRewardShown = 0f;
    }

    // ---- Hint box (bottom-centre, just above the target word box) ----

    private const float HintBottom = 174f;        // the target word box ends 160 units above the bottom edge
    private const float HintMaxTextWidth = 1100f; // longer hints wrap onto more lines
    private const float HintStripWidth = 8f;      // the yellow strip on the left
    private const float HintPaddingLeft = 30f;    // text starts after the strip
    private const float HintPaddingRight = 22f;
    private const float HintPaddingY = 12f;
    private const float HintFadeInSeconds = 0.2f;
    private const float HintFadeOutSeconds = 0.4f;
    private const float HintRise = 14f;           // it rises this many units while fading in

    private RectTransform hintBox;   // null until the first hint
    private CanvasGroup hintGroup;   // fades the whole box
    private TMP_Text hintText;
    private float hintTimeLeft;      // game seconds before the hint starts fading out
    private float hintAlpha;         // 0 = invisible .. 1 = fully visible

    // A hint box above the typed word at the bottom of the screen, for seconds
    // (game time). Newer hints replace older ones. "" fades the current hint out.
    public void ShowHint(string text, float seconds)
    {
        if (string.IsNullOrEmpty(text))
        {
            hintTimeLeft = 0f;
            return;
        }

        if (hintBox == null)
        {
            BuildHint();
        }
        if (!hintBox.gameObject.activeSelf)
        {
            hintAlpha = 0f; // it was hidden: fade in from nothing
            hintBox.gameObject.SetActive(true);
        }

        // Fit the box around the text: as wide as the text (up to the limit), as tall as its lines.
        Vector2 textSize = hintText.GetPreferredValues(text, HintMaxTextWidth, 10000f);
        float textWidth = Mathf.Min(textSize.x, HintMaxTextWidth) + 2f; // +2: a little room so it never wraps early
        hintBox.sizeDelta = new Vector2(textWidth + HintPaddingLeft + HintPaddingRight, textSize.y + HintPaddingY * 2f);
        hintText.text = text;

        hintTimeLeft = Mathf.Max(0.1f, seconds);
        ApplyHint();
    }

    private void UpdateHint()
    {
        if (hintBox == null || !hintBox.gameObject.activeSelf)
        {
            return;
        }

        float deltaTime = Time.deltaTime; // game time: the hint waits while the game is paused
        if (hintTimeLeft > 0f)
        {
            hintTimeLeft -= deltaTime;
            hintAlpha = Mathf.MoveTowards(hintAlpha, 1f, deltaTime / HintFadeInSeconds);
        }
        else
        {
            hintAlpha = Mathf.MoveTowards(hintAlpha, 0f, deltaTime / HintFadeOutSeconds);
            if (hintAlpha <= 0f)
            {
                hintBox.gameObject.SetActive(false);
                return;
            }
        }
        ApplyHint();
    }

    private void ApplyHint()
    {
        hintGroup.alpha = hintAlpha;
        hintBox.anchoredPosition = new Vector2(0f, HintBottom - HintRise * (1f - hintAlpha));
    }

    // Hint (dark box, bottom-centre) > Strip (yellow, left edge) + Text.
    private void BuildHint()
    {
        EnsureLayers();
        Image box = CreateImage("HintBox", hintLayer, new Color(0.03f, 0.04f, 0.08f, 0.82f));
        hintBox = box.rectTransform;
        PlaceRect(hintBox, BottomCentre, BottomCentre, new Vector2(0f, HintBottom), new Vector2(600f, 60f));
        hintGroup = box.gameObject.AddComponent<CanvasGroup>();

        // The strip is as tall as the box (anchored to its left edge, top to bottom).
        Image strip = CreateImage("Strip", hintBox, Yellow);
        strip.rectTransform.anchorMin = new Vector2(0f, 0f);
        strip.rectTransform.anchorMax = new Vector2(0f, 1f);
        strip.rectTransform.pivot = LeftMiddle;
        strip.rectTransform.anchoredPosition = Vector2.zero;
        strip.rectTransform.sizeDelta = new Vector2(HintStripWidth, 0f);

        // The text fills the box minus the padding.
        hintText = CreateText(hintBox, "Text", "", 34f, Vector2.zero);
        RectTransform textRect = hintText.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(HintPaddingLeft, HintPaddingY);
        textRect.offsetMax = new Vector2(-HintPaddingRight, -HintPaddingY);
        hintText.alignment = TextAlignmentOptions.Left;
        hintText.textWrappingMode = TextWrappingModes.Normal;

        hintAlpha = 0f;
    }

    // ---- Threat arrows (screen edges) ----

    private const int MaxThreatArrows = 8;
    private const float ThreatArrowArmLength = 34f;
    private const float ThreatArrowThickness = 10f;
    private const float ThreatArrowFarAlpha = 0.3f;   // alpha of an arrow for a far threat
    private const float ThreatArrowFarScale = 0.75f;  // size of an arrow for a far threat
    private const float ThreatArrowNearScale = 1.25f; // size of an arrow for a near threat
    private const float ThreatArrowThrob = 0.12f;     // near threats: the arrow throbs by +-12%
    private const float ThreatArrowThrobSpeed = 12f;

    private readonly RectTransform[] threatArrows = new RectTransform[MaxThreatArrows]; // built on first use
    private readonly CanvasGroup[] threatArrowGroups = new CanvasGroup[MaxThreatArrows];

    // Arrows at the screen edges pointing at threats that are off screen
    // (world positions). Call every frame; an empty list hides them all.
    public void UpdateThreatArrows(List<Vector3> threats, Camera cam)
    {
        int used = 0; // arrows placed this frame

        if (threats != null && threats.Count > 0 && cam != null)
        {
            if (threatArrows[0] == null)
            {
                BuildThreatArrows();
            }

            // The arrows sit on a rectangle threatArrowInset inside the screen edges.
            Rect area = markerLayer.rect;
            float halfWidth = area.width * 0.5f - threatArrowInset;
            float halfHeight = area.height * 0.5f - threatArrowInset;
            Transform view = cam.transform;

            for (int i = 0; i < threats.Count && used < MaxThreatArrows; i++)
            {
                Vector3 threat = threats[i];

                // On screen (in front of the camera and inside the view): no arrow needed.
                Vector3 viewportPoint = cam.WorldToViewportPoint(threat);
                bool onScreen = viewportPoint.z > 0f
                    && viewportPoint.x >= 0f && viewportPoint.x <= 1f
                    && viewportPoint.y >= 0f && viewportPoint.y <= 1f;
                if (onScreen)
                {
                    continue;
                }

                PlaceThreatArrow(used, threat, view, halfWidth, halfHeight);
                used++;
            }
        }

        // Hide the arrows not used this frame.
        for (int i = used; i < MaxThreatArrows; i++)
        {
            if (threatArrows[i] != null && threatArrows[i].gameObject.activeSelf)
            {
                threatArrows[i].gameObject.SetActive(false);
            }
        }
    }

    private void PlaceThreatArrow(int index, Vector3 threat, Transform view, float halfWidth, float halfHeight)
    {
        // Which way to point, seen from the middle of the screen: the threat's
        // position relative to the camera (x = right, y = up).
        //   - In front of the camera this is exactly the direction in which the
        //     point would appear on screen.
        //   - BEHIND the camera the projected point comes out mirrored through the
        //     middle of the screen; x and y relative to the camera are that
        //     projection flipped back, so "behind you on the left" points left.
        Vector3 local = view.InverseTransformPoint(threat);
        Vector2 direction = new Vector2(local.x, local.y);
        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = Vector2.down; // exactly behind the camera: point down ("behind you")
        }

        // Stretch the direction until it touches the inset rectangle.
        float stretchX = direction.x != 0f ? halfWidth / Mathf.Abs(direction.x) : float.PositiveInfinity;
        float stretchY = direction.y != 0f ? halfHeight / Mathf.Abs(direction.y) : float.PositiveInfinity;
        Vector2 edgePoint = direction * Mathf.Min(stretchX, stretchY);
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        // closeness: 1 = nearer than threatNearDistance .. 0 = farther than threatFarDistance.
        float distance = Vector3.Distance(view.position, threat);
        float closeness = 1f - Mathf.Clamp01((distance - threatNearDistance) / Mathf.Max(0.01f, threatFarDistance - threatNearDistance));
        float scale = Mathf.Lerp(ThreatArrowFarScale, ThreatArrowNearScale, closeness);
        if (closeness >= 1f)
        {
            scale *= 1f + ThreatArrowThrob * Mathf.Sin(Time.unscaledTime * ThreatArrowThrobSpeed); // very close: warning throb
        }

        RectTransform arrow = threatArrows[index];
        arrow.anchoredPosition = edgePoint;
        arrow.localRotation = Quaternion.Euler(0f, 0f, angle);
        arrow.localScale = new Vector3(scale, scale, 1f);
        threatArrowGroups[index].alpha = Mathf.Lerp(ThreatArrowFarAlpha, 1f, closeness);
        if (!arrow.gameObject.activeSelf)
        {
            arrow.gameObject.SetActive(true);
        }
    }

    // 8 arrows, each a ">" chevron: two bars that meet at the arrow's tip. The
    // tip is the arrow's position and it points right (+x) before it is turned.
    private void BuildThreatArrows()
    {
        EnsureLayers();
        for (int i = 0; i < MaxThreatArrows; i++)
        {
            RectTransform arrow = CreateRect("ThreatArrow", markerLayer);
            arrow.sizeDelta = Vector2.zero;
            threatArrowGroups[i] = arrow.gameObject.AddComponent<CanvasGroup>();
            AddArrowBar(arrow, 45f);   // lower bar of the ">"
            AddArrowBar(arrow, -45f);  // upper bar of the ">"
            arrow.gameObject.SetActive(false);
            threatArrows[i] = arrow;
        }
    }

    // One bar of a chevron. Its pivot is its right end (the tip), so turning it
    // swings the bar around the tip: +45 degrees = down-left, -45 = up-left.
    private void AddArrowBar(RectTransform arrow, float degrees)
    {
        Image bar = CreateImage("Bar", arrow, threatArrowColor);
        PlaceRect(bar.rectTransform, Centre, new Vector2(1f, 0.5f), Vector2.zero, new Vector2(ThreatArrowArmLength, ThreatArrowThickness));
        bar.rectTransform.localRotation = Quaternion.Euler(0f, 0f, degrees);
    }

    // ---- Floating texts ----

    private const int FloatingTextCount = 10;       // at most this many at once (the oldest is reused)
    private const float FloatingTextPopSeconds = 0.12f;
    private const float FloatingTextPopScale = 1.35f;
    private const float FloatingTextFadeFrom = 0.55f; // starts fading after 55% of its life
    private const float FloatingStackSeconds = 0.35f; // texts started this close together...
    private const float FloatingStackRange = 2f;      // ...this close (metres) are stacked...
    private const float FloatingStackGap = 52f;       // ...this many canvas units apart

    private readonly TMP_Text[] floatingTexts = new TMP_Text[FloatingTextCount]; // built on first use
    private readonly Vector3[] floatingPoints = new Vector3[FloatingTextCount];   // the world point each follows
    private readonly float[] floatingAges = new float[FloatingTextCount];
    private readonly float[] floatingLifts = new float[FloatingTextCount];        // extra height when stacked

    // Text that appears at a world position (e.g. "+30" or "+1 LURE BOMB"),
    // rises and fades out.
    public void ShowFloatingText(Vector3 worldPosition, string text, Color color)
    {
        if (floatingTexts[0] == null)
        {
            BuildFloatingTexts();
        }

        int slot = FreeFloatingText();

        // Texts that start together at the same place (for example several kills
        // at once) are stacked instead of drawn on top of each
        // other: the new one starts just above the highest recent one.
        float lift = 0f;
        for (int i = 0; i < FloatingTextCount; i++)
        {
            if (i == slot || !floatingTexts[i].gameObject.activeSelf)
            {
                continue;
            }
            bool recent = floatingAges[i] < FloatingStackSeconds;
            bool near = (floatingPoints[i] - worldPosition).sqrMagnitude < FloatingStackRange * FloatingStackRange;
            if (recent && near)
            {
                float top = floatingLifts[i] + FloatingRise(floatingAges[i]) + FloatingStackGap;
                lift = Mathf.Max(lift, top);
            }
        }

        TMP_Text label = floatingTexts[slot];
        label.text = text;
        label.color = color;
        floatingPoints[slot] = worldPosition;
        floatingAges[slot] = 0f;
        floatingLifts[slot] = lift;
        label.transform.SetAsLastSibling(); // the newest text is drawn on top
        label.gameObject.SetActive(true);
        PlaceFloatingText(slot, Camera.main);
    }

    // A hidden text, or else the oldest one.
    private int FreeFloatingText()
    {
        int oldest = 0;
        for (int i = 0; i < FloatingTextCount; i++)
        {
            if (!floatingTexts[i].gameObject.activeSelf)
            {
                return i;
            }
            if (floatingAges[i] > floatingAges[oldest])
            {
                oldest = i;
            }
        }
        return oldest;
    }

    // How far a floating text has risen at this age (steady speed, so stacked
    // texts keep the same gap between them).
    private float FloatingRise(float age)
    {
        return floatingTextRise * Mathf.Clamp01(age / Mathf.Max(0.01f, floatingTextSeconds));
    }

    private void UpdateFloatingTexts()
    {
        if (floatingTexts[0] == null)
        {
            return;
        }

        Camera cam = Camera.main;
        for (int i = 0; i < FloatingTextCount; i++)
        {
            if (!floatingTexts[i].gameObject.activeSelf)
            {
                continue;
            }

            floatingAges[i] += Time.deltaTime; // game time: frozen while paused
            if (floatingAges[i] >= floatingTextSeconds)
            {
                floatingTexts[i].gameObject.SetActive(false);
                continue;
            }
            PlaceFloatingText(i, cam);
        }
    }

    // Pins a floating text to its world point on screen, risen and faded by its age.
    private void PlaceFloatingText(int slot, Camera cam)
    {
        TMP_Text label = floatingTexts[slot];
        if (cam == null)
        {
            label.enabled = false;
            return;
        }

        Vector3 screenPoint = cam.WorldToScreenPoint(floatingPoints[slot]);
        if (screenPoint.z <= 0f)
        {
            label.enabled = false; // behind the camera (it keeps ageing)
            return;
        }

        Vector2 position;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(markerLayer, screenPoint, null, out position);

        float age = floatingAges[slot];
        position.y += floatingLifts[slot] + FloatingRise(age);
        label.rectTransform.anchoredPosition = position;

        // Pops in a bit big, then settles.
        float scale = Mathf.Lerp(FloatingTextPopScale, 1f, Mathf.Clamp01(age / FloatingTextPopSeconds));
        label.rectTransform.localScale = new Vector3(scale, scale, 1f);

        // Fully visible, then fades out over the rest of its life.
        float life = Mathf.Clamp01(age / Mathf.Max(0.01f, floatingTextSeconds));
        label.alpha = life < FloatingTextFadeFrom ? 1f : 1f - (life - FloatingTextFadeFrom) / (1f - FloatingTextFadeFrom);
        label.enabled = true;
    }

    private void BuildFloatingTexts()
    {
        EnsureLayers();
        for (int i = 0; i < FloatingTextCount; i++)
        {
            TMP_Text label = CreateText(markerLayer, "FloatingText", "", 44f, Vector2.zero);
            label.rectTransform.sizeDelta = new Vector2(900f, 70f);
            label.fontStyle = FontStyles.Bold;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.fontSharedMaterial = OutlineMaterial(label);
            label.gameObject.SetActive(false);
            floatingTexts[i] = label;
        }
    }

    // ---- Boss quiz (top-centre, under the boss health bar) ----

    private const float QuizTop = 120f;              // canvas units below the top edge (the boss bar is above)
    private const float QuizMaxTextWidth = 1300f;    // longer questions wrap onto more lines
    private const float QuizMinBoxWidth = 760f;
    private const float QuizSidePadding = 50f;
    private const float QuizCaptionTop = 14f;
    private const float QuizQuestionTop = 58f;       // below the caption
    private const float QuizTimerBottom = 18f;
    private const float QuizExtraHeight = 102f;      // box height = question height + this (caption, timer, padding)
    private const float QuizTimerWarning = 0.25f;    // below this much time left the bar turns red
    private const float QuizPopSeconds = 0.18f;
    private const float QuizPopScale = 1.12f;
    private static readonly Vector2 QuizTimerSize = new Vector2(600f, 10f);
    private static readonly Color QuizTimerLowColor = new Color(1f, 0.3f, 0.2f);

    private RectTransform quizBox;        // null until the first quiz
    private TMP_Text quizQuestion;
    private RectTransform quizTimerFill;
    private Image quizTimerFillImage;
    private float quizPopAge = float.MaxValue;

    // Question box at the top-centre, under the boss health bar.
    public void ShowQuiz(string question)
    {
        if (quizBox == null)
        {
            BuildQuiz();
        }
        quizBox.gameObject.SetActive(true);

        // Fit the box to the question: as wide as the text (between the limits),
        // as tall as its lines.
        Vector2 textSize = quizQuestion.GetPreferredValues(question, QuizMaxTextWidth, 10000f);
        float textWidth = Mathf.Min(textSize.x, QuizMaxTextWidth) + 2f; // +2: a little room so it never wraps early
        quizQuestion.rectTransform.sizeDelta = new Vector2(textWidth, textSize.y);
        float boxWidth = Mathf.Max(QuizMinBoxWidth, textWidth + QuizSidePadding * 2f);
        quizBox.sizeDelta = new Vector2(boxWidth, textSize.y + QuizExtraHeight);
        quizQuestion.text = question;

        SetQuizTimer(1f);
        quizPopAge = 0f;
        UpdateQuizPop();
    }

    // Time left to answer (1 = all of it, 0 = none), as a shrinking bar.
    public void SetQuizTimer(float fraction)
    {
        if (quizBox == null)
        {
            return;
        }

        // The bar shrinks toward its middle.
        fraction = Mathf.Clamp01(fraction);
        quizTimerFill.anchorMin = new Vector2(0.5f - fraction * 0.5f, 0f);
        quizTimerFill.anchorMax = new Vector2(0.5f + fraction * 0.5f, 1f);
        quizTimerFillImage.color = fraction < QuizTimerWarning ? QuizTimerLowColor : Palette.WordQuiz;
    }

    public void HideQuiz()
    {
        if (quizBox != null)
        {
            quizBox.gameObject.SetActive(false);
        }
    }

    // A new question pops in slightly big, then settles.
    private void UpdateQuizPop()
    {
        if (quizBox == null || quizPopAge > QuizPopSeconds)
        {
            return;
        }
        quizPopAge += Time.unscaledDeltaTime;
        float settle = Mathf.Clamp01(quizPopAge / QuizPopSeconds);
        settle = 1f - (1f - settle) * (1f - settle);
        float scale = Mathf.Lerp(QuizPopScale, 1f, settle);
        quizBox.localScale = new Vector3(scale, scale, 1f);
    }

    // QuizBox (dark, top-centre) > Accent (cyan line on top), Caption, Question, Timer > Fill.
    private void BuildQuiz()
    {
        EnsureLayers();
        Image box = CreateImage("QuizBox", readoutLayer, new Color(0.03f, 0.05f, 0.1f, 0.88f));
        quizBox = box.rectTransform;
        PlaceRect(quizBox, TopCentre, TopCentre, new Vector2(0f, -QuizTop), new Vector2(QuizMinBoxWidth, 200f));

        Image accent = CreateImage("Accent", quizBox, Palette.WordQuiz);
        accent.rectTransform.anchorMin = new Vector2(0f, 1f);
        accent.rectTransform.anchorMax = new Vector2(1f, 1f);
        accent.rectTransform.pivot = TopCentre;
        accent.rectTransform.anchoredPosition = Vector2.zero;
        accent.rectTransform.sizeDelta = new Vector2(0f, 4f);

        TMP_Text caption = CreateText(quizBox, "Caption", "<b>BOSS QUIZ</b>   <color=#9AA3AE><size=80%>type the right answer</size></color>", 30f, Vector2.zero);
        PlaceRect(caption.rectTransform, TopCentre, TopCentre, new Vector2(0f, -QuizCaptionTop), new Vector2(QuizMaxTextWidth, 40f));
        caption.color = Palette.WordQuiz;
        caption.textWrappingMode = TextWrappingModes.NoWrap;

        quizQuestion = CreateText(quizBox, "Question", "", 48f, Vector2.zero);
        PlaceRect(quizQuestion.rectTransform, TopCentre, TopCentre, new Vector2(0f, -QuizQuestionTop), new Vector2(QuizMaxTextWidth, 60f));
        quizQuestion.textWrappingMode = TextWrappingModes.Normal;

        Image timer = CreateImage("Timer", quizBox, new Color(0.15f, 0.17f, 0.21f));
        PlaceRect(timer.rectTransform, BottomCentre, BottomCentre, new Vector2(0f, QuizTimerBottom), QuizTimerSize);

        quizTimerFillImage = CreateImage("Fill", timer.rectTransform, Palette.WordQuiz);
        quizTimerFill = quizTimerFillImage.rectTransform;
        quizTimerFill.anchorMin = Vector2.zero;
        quizTimerFill.anchorMax = Vector2.one;
        quizTimerFill.offsetMin = Vector2.zero;
        quizTimerFill.offsetMax = Vector2.zero;
    }

    // ---- End of game: stats and grade ----

    private const float StatsRightMargin = 480f;   // the stats text leaves this much room on its right for the grade
    private const float GradeX = 470f;             // the grade letter's centre, right of the panel's centre
    private const float GradeY = 10f;
    private const float GradeRevealDelay = 0.35f;  // the grade appears a moment after the panel...
    private const float GradeRevealSeconds = 0.25f; // ...slamming down like a stamp
    private const float GradeRevealScale = 2.4f;
    private const string StatLabel = "<color=#9AA3AE>";  // grey labels, white numbers

    private TMP_Text wonGrade;          // the big grade letter of each panel, created on its first results
    private TMP_Text lostGrade;
    private TMP_Text revealingGrade;    // the grade being stamped in, or null
    private CanvasGroup revealingGradeGroup;
    private float gradeRevealAge;

    // Shows the "You survived" (won) or "You died" panel with the run's stats
    // and grade, using the existing panels and their Restart buttons.
    public void ShowResults(bool won, RunStats stats)
    {
        if (stats == null)
        {
            stats = new RunStats(); // nothing was counted: show zeros rather than nothing
        }

        // The game is over: hide the enemies' words so they do not show through the panel.
        if (wordLayer != null)
        {
            wordLayer.gameObject.SetActive(false);
        }

        TMP_Text message;
        TMP_Text grade;
        if (won)
        {
            ShowWonPanel(stats.Score);
            message = wonScoreText;
            if (wonGrade == null)
            {
                wonGrade = CreateGrade(wonPanel.transform);
            }
            grade = wonGrade;
        }
        else
        {
            ShowLostPanel(stats.Score);
            message = lostScoreText;
            if (lostGrade == null)
            {
                lostGrade = CreateGrade(lostPanel.transform);
            }
            grade = lostGrade;
        }

        // The stats replace "Final score: ...". The right margin moves them to the
        // left half of the text box; the grade letter stands on the right.
        message.text = StatsText(stats);
        message.margin = new Vector4(0f, 0f, StatsRightMargin, 0f);

        string letter = stats.Grade;
        grade.text = letter;
        grade.color = GradeColor(letter);
        grade.gameObject.SetActive(true);

        revealingGrade = grade;
        revealingGradeGroup = grade.GetComponent<CanvasGroup>();
        gradeRevealAge = 0f;
        ApplyGradeReveal();
    }

    // Four lines, e.g.
    //   Score 12750
    //   Accuracy 97.5%    Speed 62 WPM
    //   Kills 50    Best combo x51    Biggest blast x7
    //   Time 4:12
    private static string StatsText(RunStats stats)
    {
        int seconds = Mathf.FloorToInt(stats.Seconds);
        string time = (seconds / 60) + ":" + (seconds % 60).ToString("00");
        // InvariantCulture: always a dot in "97.5", whatever the computer's language.
        string accuracy = stats.Accuracy.ToString("0.0", CultureInfo.InvariantCulture);

        return "<size=125%>" + StatLabel + "Score</color>  <b>" + stats.Score + "</b></size>\n"
            + StatLabel + "Accuracy</color>  <b>" + accuracy + "%</b>     "
            + StatLabel + "Speed</color>  <b>" + Mathf.RoundToInt(stats.WordsPerMinute) + " WPM</b>\n"
            + StatLabel + "Kills</color>  <b>" + stats.Kills + "</b>     "
            + StatLabel + "Best combo</color>  <b>x" + stats.BestCombo + "</b>     "
            + StatLabel + "Biggest blast</color>  <b>x" + stats.BiggestMultiKill + "</b>\n"
            + StatLabel + "Time</color>  <b>" + time + "</b>";
    }

    // S gold, A green, B cyan, C orange, D red.
    private static Color GradeColor(string grade)
    {
        switch (grade)
        {
            case "S":
                return new Color(1f, 0.82f, 0.2f);
            case "A":
                return new Color(0.35f, 0.9f, 0.4f);
            case "B":
                return new Color(0.3f, 0.82f, 1f);
            case "C":
                return new Color(1f, 0.58f, 0.18f);
            default:
                return new Color(1f, 0.25f, 0.2f);
        }
    }

    // Grade (huge letter) > Caption ("GRADE", above the letter), on the right of a panel.
    private TMP_Text CreateGrade(Transform panel)
    {
        TMP_Text grade = CreateText(panel, "Grade", "", 230f, new Vector2(GradeX, GradeY));
        grade.rectTransform.sizeDelta = new Vector2(320f, 280f);
        grade.fontStyle = FontStyles.Bold;
        grade.textWrappingMode = TextWrappingModes.NoWrap;
        grade.gameObject.AddComponent<CanvasGroup>(); // fades the letter and its caption together

        TMP_Text caption = CreateText(grade.rectTransform, "Caption", "GRADE", 32f, new Vector2(0f, 150f));
        caption.rectTransform.sizeDelta = new Vector2(320f, 44f);
        caption.characterSpacing = 12f;
        caption.color = new Color(0.75f, 0.78f, 0.84f);
        return grade;
    }

    // Puts a panel's message text back to how the scene made it (ShowWonPanel /
    // ShowLostPanel without stats) and hides the grade.
    private void ClearResults(TMP_Text message, TMP_Text grade)
    {
        message.margin = Vector4.zero;
        if (grade != null)
        {
            grade.gameObject.SetActive(false);
            if (revealingGrade == grade)
            {
                revealingGrade = null;
            }
        }
    }

    private void UpdateGradeReveal()
    {
        if (revealingGrade == null)
        {
            return;
        }
        gradeRevealAge += Time.unscaledDeltaTime; // real time, in case the game time is stopped
        ApplyGradeReveal();
    }

    private void ApplyGradeReveal()
    {
        // stamp: 0 before the delay, then 0 -> 1. Squared, so it speeds up and "lands".
        float stamp = Mathf.Clamp01((gradeRevealAge - GradeRevealDelay) / GradeRevealSeconds);
        float scale = Mathf.Lerp(GradeRevealScale, 1f, stamp * stamp);
        revealingGrade.rectTransform.localScale = new Vector3(scale, scale, 1f);
        revealingGradeGroup.alpha = stamp;
        if (stamp >= 1f)
        {
            revealingGrade = null; // done
        }
    }

    // ---- Small UI helpers ----

    // An empty UI object (only a RectTransform) under parent.
    private static RectTransform CreateRect(string objectName, Transform parent)
    {
        GameObject uiObject = new GameObject(objectName, typeof(RectTransform));
        RectTransform rect = uiObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    // A flat-coloured rectangle (an Image with no sprite draws a plain rectangle).
    private static Image CreateImage(string objectName, Transform parent, Color color)
    {
        GameObject imageObject = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        imageObject.GetComponent<RectTransform>().SetParent(parent, false);
        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false; // never catches mouse clicks (the buttons must stay clickable)
        return image;
    }

    // Pins a UI element to one point of its parent.
    //   anchor: which point of the PARENT it sticks to   (0,0 = bottom-left ... 1,1 = top-right)
    //   pivot:  which point of the ELEMENT sits there
    //   position: offset from that point in canvas units;  size: width and height
    private static void PlaceRect(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    // Makes a UI element fill its parent completely.
    private static void StretchToParent(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
