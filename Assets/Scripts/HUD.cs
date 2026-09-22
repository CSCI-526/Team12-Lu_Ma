// HUD.cs
// ---------------------------------------------------------------------------
// Everything drawn on the screen-space Canvas:
//   - health bar (top-left)
//   - "Wave 2 / 5", Score and Combo (top-right)
//   - the current target's word (bottom-centre)
//   - the "Wave N" banner and the red wrong-key flash
//   - the Start, "You survived" and "You died" panels
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

    private float flashTimer; // counts down from flashDuration to 0 while a flash is fading

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
        if (flashTimer <= 0f)
        {
            return;
        }

        // Fade the red from flashMaxAlpha down to 0.
        flashTimer -= Time.deltaTime;
        float alpha = flashMaxAlpha * Mathf.Clamp01(flashTimer / flashDuration);
        SetFlashAlpha(alpha);
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
    }
}
