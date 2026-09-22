// WaveSpawner.cs
// ---------------------------------------------------------------------------
// Runs the waves and creates the zombies.
//   - Wave n spawns zombiesBase + zombiesPerWave x n zombies (4 + 2n by default),
//     one every spawnInterval seconds.
//   - Every wave after the first is a little faster: zombies gain
//     zombieSpeedPerWave, and the spawn interval shrinks by spawnIntervalPerWave
//     (but never below 0.8 seconds).
//   - EXPLOSIVE zombies (bigger, red, long word, blast ring) start in wave
//     explosiveFirstWave (2) with explosiveStartCount (2) of them, and each later
//     wave has explosivePerWave (1) more. The rest are normal.
//   - A "Wave N" banner shows for 3 seconds before each wave.
//   - A wave ends when all of its zombies are dead or removed.
//   - Every bossEveryWaves-th wave (5, 10, ...) is a BOSS FIGHT instead: a
//     "BOSS FIGHT" banner shows while the rail brakes to a stop, then a Boss
//     appears in the middle of the track ahead, with a red health bar. No
//     zombies spawn. When the boss dies the rail starts moving again.
//   - Clearing the last wave wins the game.
//
// It also keeps the list of alive zombies, which TypingController (targeting)
// and Zombie (blast) both read.
// ---------------------------------------------------------------------------
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class WaveSpawner : MonoBehaviour
{
    [Header("Waves")]
    [SerializeField] private int waveCount = 5;
    [SerializeField] private int zombiesBase = 4;               // wave n spawns zombiesBase + zombiesPerWave x n
    [SerializeField] private int zombiesPerWave = 2;
    [SerializeField] private float spawnInterval = 2.0f;        // seconds between spawns in wave 1
    [SerializeField] private float spawnIntervalPerWave = 0.2f; // seconds SUBTRACTED for each later wave

    [Header("Zombies")]
    [SerializeField] private float zombieSpeed = 1.5f;          // metres per second in wave 1
    [SerializeField] private float zombieSpeedPerWave = 0.12f;  // speed ADDED for each later wave

    [Header("Explosive zombies")]
    [SerializeField] private int explosiveFirstWave = 2;        // no explosive zombies before this wave
    [SerializeField] private int explosiveStartCount = 2;       // explosive zombies in explosiveFirstWave
    [SerializeField] private int explosivePerWave = 1;          // one more explosive zombie in every later wave

    [Header("Spawn area (in front of the player)")]
    [SerializeField] private float spawnDistanceMin = 35f;
    [SerializeField] private float spawnDistanceMax = 45f;
    [SerializeField] private float spawnLaneHalfWidth = 7f;     // zombies spawn at X from -this to +this

    [Header("Boss")]
    [SerializeField] private int bossEveryWaves = 5;            // every Nth wave is a boss fight (0 = no bosses)
    [SerializeField] private float bossDistance = 16f;          // metres ahead of the stopped player

    [Header("References (wired by the scene builder)")]
    [SerializeField] private Zombie zombiePrefab;
    [SerializeField] private Transform player;
    [SerializeField] private HUD hud;

    private const float MinSpawnInterval = 0.8f; // the spawn interval never goes below this
    private const float BannerSeconds = 3f;      // how long the "Wave N" banner stays up

    private readonly List<Zombie> aliveZombies = new List<Zombie>();
    private readonly List<ITypingTarget> typingTargets = new List<ITypingTarget>();

    private RailMover rail;   // on the Player object; braked during boss fights
    private Boss boss;        // the current boss, or null

    // Every zombie that is currently alive.
    public List<Zombie> AliveZombies
    {
        get { return aliveZombies; }
    }

    // Everything the player can type right now: alive zombies plus the boss's
    // unbroken parts. The list is reused, so read it right away.
    public List<ITypingTarget> GetTypingTargets()
    {
        typingTargets.Clear();
        typingTargets.AddRange(aliveZombies);
        if (boss != null && boss.IsAlive)
        {
            boss.AddTypeableParts(typingTargets);
        }
        return typingTargets;
    }

    private void Start()
    {
        hud.SetWave(1, waveCount);
        rail = player.GetComponent<RailMover>();
    }

    private void LateUpdate()
    {
        LayoutLabels();
    }

    // ---- Enemy words on screen ----
    // Every enemy word is a HUD text (HUD.CreateWordLabel), drawn on top of the
    // 3D scene, so no body can hide it. Every frame each word is pinned to its
    // enemy's LabelAnchor:
    //   - it is shown only while its enemy is in front of the camera, so the
    //     word appears and disappears together with the enemy;
    //   - it shrinks with distance, like the enemy does (labelReferenceDistance);
    //   - words are placed one by one in priority order (the word being typed
    //     first, then the nearest enemy first). A word that would overlap one
    //     already placed is pushed up until it is clear, and slides there smoothly.

    [Header("Enemy words")]
    [SerializeField] private float labelReferenceDistance = 12f; // at this distance a word is drawn at full size
    [SerializeField] private float labelMinScale = 0.55f;        // far words never get smaller than this
    [SerializeField] private float labelMaxScale = 1.2f;         // near words never get bigger than this
    [SerializeField] private float labelGap = 4f;                // empty space (HUD units) between stacked words
    [SerializeField] private float labelSlideSharpness = 14f;    // higher = pushed words slide into place faster

    private readonly List<ITypingTarget> labelOrder = new List<ITypingTarget>();
    private readonly List<Rect> placedLabels = new List<Rect>();

    // How far each word is currently pushed up. A word not in here yet (just
    // spawned) jumps straight to its spot instead of sliding in from elsewhere.
    private Dictionary<TMP_Text, float> labelLifts = new Dictionary<TMP_Text, float>();
    private Dictionary<TMP_Text, float> nextLabelLifts = new Dictionary<TMP_Text, float>();

    // Used by Zombie to create its word.
    public HUD Hud
    {
        get { return hud; }
    }

    private void LayoutLabels()
    {
        RectTransform layer = hud.WordLayer;
        if (layer == null)
        {
            return; // no word has been created yet
        }

        Camera cam = Camera.main;
        Vector3 cameraPosition = cam.transform.position;

        labelOrder.Clear();
        labelOrder.AddRange(GetTypingTargets());
        labelOrder.Sort((a, b) =>
        {
            if (a.IsTargeted != b.IsTargeted)
            {
                return a.IsTargeted ? -1 : 1;
            }
            float distanceA = (a.LabelAnchor - cameraPosition).sqrMagnitude;
            float distanceB = (b.LabelAnchor - cameraPosition).sqrMagnitude;
            return distanceA.CompareTo(distanceB);
        });

        placedLabels.Clear();
        nextLabelLifts.Clear();
        float slide = 1f - Mathf.Exp(-labelSlideSharpness * Time.unscaledDeltaTime);

        foreach (ITypingTarget target in labelOrder)
        {
            TMP_Text label = target.Label;
            RectTransform labelRect = label.rectTransform;

            // Behind the camera: hide the word.
            Vector3 screenPoint = cam.WorldToScreenPoint(target.LabelAnchor);
            if (screenPoint.z <= 0f)
            {
                label.enabled = false;
                continue;
            }

            // Screen pixels -> position inside the word layer (Canvas units).
            Vector2 anchor;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(layer, screenPoint, null, out anchor);

            // Size: fit the box to the text, then scale with distance.
            float scale = Mathf.Clamp(labelReferenceDistance / screenPoint.z, labelMinScale, labelMaxScale);
            Vector2 textSize = new Vector2(label.preferredWidth, label.preferredHeight);
            labelRect.sizeDelta = textSize;
            labelRect.localScale = new Vector3(scale, scale, 1f);

            // The word's box on screen with no push, then pushed up past every
            // word already placed. It only ever moves up, so this always finishes.
            Vector2 size = textSize * scale;
            Rect baseRect = new Rect(anchor - Vector2.Scale(size, labelRect.pivot), size);
            Rect rect = baseRect;
            bool moved = true;
            while (moved)
            {
                moved = false;
                foreach (Rect placed in placedLabels)
                {
                    if (rect.Overlaps(placed))
                    {
                        rect.y = placed.yMax + labelGap;
                        moved = true;
                    }
                }
            }
            placedLabels.Add(rect);

            // Slide toward the new push (new words jump straight there).
            float targetLift = rect.y - baseRect.y;
            float lift;
            if (labelLifts.TryGetValue(label, out lift))
            {
                lift = Mathf.Lerp(lift, targetLift, slide);
            }
            else
            {
                lift = targetLift;
            }
            nextLabelLifts[label] = lift;

            labelRect.anchoredPosition = anchor + Vector2.up * lift;
            label.enabled = true;

            // The word being typed is drawn on top of all the others.
            if (target.IsTargeted && labelRect.GetSiblingIndex() != layer.childCount - 1)
            {
                labelRect.SetAsLastSibling();
            }
        }

        // Keep only the words that still exist.
        Dictionary<TMP_Text, float> swap = labelLifts;
        labelLifts = nextLabelLifts;
        nextLabelLifts = swap;
    }

    // Called by GameManager when the game starts.
    public void BeginWaves()
    {
        StartCoroutine(RunWaves());
    }

    // Called by GameManager when the player dies.
    public void StopWaves()
    {
        StopAllCoroutines();
    }

    // Called by a zombie when it dies or reaches the player.
    public void RemoveZombie(Zombie zombie)
    {
        aliveZombies.Remove(zombie);
    }

    // A coroutine: it runs over many frames. Every "yield return" pauses it here
    // and Unity continues it later (after the wait is over, or on the next frame).
    private IEnumerator RunWaves()
    {
        for (int wave = 1; wave <= waveCount; wave++)
        {
            hud.SetWave(wave, waveCount);

            // Boss wave: the whole wave is the boss fight.
            if (bossEveryWaves > 0 && wave % bossEveryWaves == 0)
            {
                yield return RunBossFight();
                continue;
            }

            // 1. Work out this wave's numbers. Wave 1 uses the base values.
            int zombiesThisWave = zombiesBase + zombiesPerWave * wave;
            float speed = zombieSpeed + zombieSpeedPerWave * (wave - 1);
            float interval = spawnInterval - spawnIntervalPerWave * (wave - 1);
            if (interval < MinSpawnInterval)
            {
                interval = MinSpawnInterval;
            }

            // 2. Announce the wave.
            hud.ShowBanner("Wave " + wave);
            yield return new WaitForSeconds(BannerSeconds);
            hud.HideBanner();

            // 3. Spawn the zombies one at a time. The explosive ones are mixed in
            //    at random positions in the spawn order.
            bool[] explosivePlan = PlanExplosives(zombiesThisWave, ExplosiveCountForWave(wave));
            for (int i = 0; i < zombiesThisWave; i++)
            {
                SpawnZombie(speed, explosivePlan[i]);
                yield return new WaitForSeconds(interval);
            }

            // 4. Wait until every zombie of this wave is dead or removed.
            while (aliveZombies.Count > 0)
            {
                yield return null; // wait one frame, then check again
            }
        }

        GameManager.Instance.WinGame();
    }

    // How many explosive zombies this wave has: none before explosiveFirstWave,
    // then explosiveStartCount, plus explosivePerWave for every later wave.
    private int ExplosiveCountForWave(int wave)
    {
        if (wave < explosiveFirstWave)
        {
            return 0;
        }
        return explosiveStartCount + explosivePerWave * (wave - explosiveFirstWave);
    }

    // Returns one entry per zombie of the wave: true = that spawn is explosive.
    // explosiveCount entries are true (never more than the whole wave), shuffled.
    private bool[] PlanExplosives(int zombieCount, int explosiveCount)
    {
        bool[] plan = new bool[zombieCount];
        for (int i = 0; i < zombieCount && i < explosiveCount; i++)
        {
            plan[i] = true;
        }

        // Fisher-Yates shuffle: every order is equally likely.
        for (int i = zombieCount - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            bool swap = plan[i];
            plan[i] = plan[j];
            plan[j] = swap;
        }
        return plan;
    }

    private IEnumerator RunBossFight()
    {
        // 1. Warn the player and brake the rail to a stop.
        rail.Brake();
        hud.ShowBanner("BOSS FIGHT");
        yield return new WaitForSeconds(BannerSeconds);
        while (!rail.IsStopped)
        {
            yield return null;
        }
        hud.HideBanner();

        // 2. The boss appears in the middle of the track, ahead of the player.
        //    It reuses the zombie body's material (tinted red) so it renders
        //    correctly in this project's render pipeline.
        Vector3 position = new Vector3(0f, 0f, player.position.z + bossDistance);
        Material bodyMaterial = zombiePrefab.transform.Find("Body").GetComponent<Renderer>().sharedMaterial;
        hud.ShowBossBar("BOSS");
        boss = Boss.Create(position, bodyMaterial, hud);

        // 3. Wait until it is dead, then ride on.
        while (boss.IsAlive)
        {
            yield return null;
        }
        boss = null;
        hud.HideBossBar();
        rail.Release();
    }

    private void SpawnZombie(float speed, bool explosive)
    {
        // Somewhere ahead of the player, at a random spot across the corridor.
        float x = Random.Range(-spawnLaneHalfWidth, spawnLaneHalfWidth);
        float z = player.position.z + Random.Range(spawnDistanceMin, spawnDistanceMax);
        Vector3 position = new Vector3(x, 0f, z);

        // Collect the first letters already in use, so WordBank can avoid them.
        List<char> usedFirstLetters = new List<char>();
        foreach (Zombie alive in aliveZombies)
        {
            usedFirstLetters.Add(alive.Word[0]);
        }
        string word = WordBank.PickWord(usedFirstLetters, explosive);

        Zombie zombie = Instantiate(zombiePrefab, position, Quaternion.identity);
        zombie.Setup(word, speed, player, this, explosive);
        aliveZombies.Add(zombie);
    }
}
