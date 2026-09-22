// HUD.cs
// ---------------------------------------------------------------------------
// Everything drawn on the screen-space Canvas:
//   - health bar (top-left)
//   - "Wave 2 / 5", Score and Combo (top-right)
//   - the current target's word (bottom-centre)
//   - the "Wave N" banner and the red wrong-key flash
//   - the Start, "You survived" and "You died" panels
//   - the red boss health bar (top-centre), built in code on the first boss fight
//   - the enemies' words (CreateWordLabel). They are drawn on this Canvas, on
//     top of the 3D scene, so an enemy's body can never hide another's word.
//     WaveSpawner.LayoutLabels moves them to their enemies every frame.
//   - the pause panel and the 3-2-1 resume countdown, built in code
//
// The HUD never decides anything. Other scripts call these methods when a value
// changes (for example GameManager calls SetScore after a kill).
// All references are wired by Tools > Blast Radius > Build Prototype Scene.
// ---------------------------------------------------------------------------
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

    private float flashTimer; // counts down from flashDuration to 0 while a flash is fading

    private RectTransform wordLayer;  // full-screen parent of every enemy word
    private Material wordMaterial;    // shared by every word: the font with a black outline

    private GameObject pausePanel;    // null until the first pause
    private TMP_Text countdownText;   // null until the first resume

    private GameObject bossBar;       // null until the first boss fight
    private RectTransform bossBarFill;
    private TMP_Text bossBarTitle;

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

    private void Update()
    {
        UpdateMultiKill();

        if (flashTimer <= 0f)
        {
            return;
        }

        // Fade the red from flashMaxAlpha down to 0.
        flashTimer -= Time.unscaledDeltaTime; // unscaled: keeps fading while the game is paused
        float alpha = flashMaxAlpha * Mathf.Clamp01(flashTimer / flashDuration);
        SetFlashAlpha(alpha);
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

    private void SetFlashAlpha(float alpha)
    {
        Color color = flashImage.color;
        color.a = alpha;
        flashImage.color = color;
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
        wonScoreText.text = "Final score: " + finalScore;
        wonPanel.SetActive(true);
    }

    public void ShowLostPanel(int finalScore)
    {
        HideAllPanels();
        SetTargetWord("");
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
    // The word starts hidden; WaveSpawner.LayoutLabels shows it once it is placed
    // on its enemy, so a word never appears anywhere else first.
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
        // One shared material for all words (a material per word would be wasteful).
        if (wordMaterial == null)
        {
            wordMaterial = new Material(label.fontSharedMaterial);
            wordMaterial.EnableKeyword(ShaderUtilities.Keyword_Outline);
            wordMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, wordOutlineWidth);
            wordMaterial.SetColor(ShaderUtilities.ID_OutlineColor, Color.black);
        }
        label.fontSharedMaterial = wordMaterial;

        label.enabled = false;
        return label;
    }

    // The RectTransform the words live in (WaveSpawner converts screen points into it).
    public RectTransform WordLayer
    {
        get { return wordLayer; }
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
        CreateButton(panelRect, "Resume", new Vector2(0f, -40f), () => GameManager.Instance.ResumeGame());
        CreateButton(panelRect, "Restart", new Vector2(0f, -150f), () => GameManager.Instance.RestartGame());
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

    private void CreateButton(Transform parent, string text, Vector2 position, UnityEngine.Events.UnityAction onClick)
    {
        GameObject buttonObject = new GameObject(text + "Button", typeof(RectTransform), typeof(Image), typeof(Button));
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(320f, 80f);
        buttonObject.GetComponent<Image>().color = new Color(0.25f, 0.25f, 0.28f);
        buttonObject.GetComponent<Button>().onClick.AddListener(onClick);

        TMP_Text label = CreateText(rect, "Text", text, 40f, Vector2.zero);
        label.rectTransform.sizeDelta = rect.sizeDelta;
    }
}
