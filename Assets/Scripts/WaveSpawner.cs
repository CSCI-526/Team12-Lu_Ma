// WaveSpawner.cs
// ---------------------------------------------------------------------------
// Runs the level, Typing of the Dead style. For every Encounter of the Level
// (the 5 fights listed at the top of Level.cs), in order:
//   1. RIDE: the player rides the encounter's route, then stops and turns to
//      face the fight.
//   2. FIGHT: a "Wave N" banner, then the area's barrels and supply crates get
//      words (they can be typed now) and a wave of zombies comes out of the
//      area's spawn points (doors burst open), a few at a time. Or, in the
//      last area, the BOSS.
//   3. CLEARED: when every zombie is dead, the exit gate opens and the next
//      ride begins.
// After the last encounter the player wins.
//
// Wave n (encounter n) has zombiesBase + zombiesPerWave x n zombies (4 + 2n by
// default). Later waves are faster, spawn more often, and mix in special
// zombies: EXPLOSIVE ones from explosiveFirstWave, RUNNERS from
// runnerFirstWave, ARMORED ones from armoredFirstWave (see Zombie). From
// chainFirstWave on, WORD CHAIN pairs come on top of that (one more pair each
// wave): two zombies at once out of two different doors, "hunt" and "hunter", linked in
// purple (see AddChainPairs and SpawnChainPair). The first pair explains itself.
//
// It also keeps the lists of everything that can be typed (GetTypingTargets),
// which TypingController (targeting), Explosion (blasts) and the HUD use, and
// every frame it places the enemies' words on screen (LayoutLabels).
// ---------------------------------------------------------------------------
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class WaveSpawner : MonoBehaviour
{
    [Header("Waves")]
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

    [Header("Runners")]
    [SerializeField] private int runnerFirstWave = 3;
    [SerializeField] private int runnerStartCount = 2;
    [SerializeField] private int runnersPerWave = 1;

    [Header("Armored zombies")]
    [SerializeField] private int armoredFirstWave = 4;
    [SerializeField] private int armoredStartCount = 2;
    [SerializeField] private int armoredPerWave = 1;

    [Header("Word chains")]
    [SerializeField] private int chainFirstWave = 2;            // no WORD CHAIN pairs (hunt + hunter) before this wave
    [SerializeField] private int chainStartCount = 1;           // pairs in chainFirstWave
    [SerializeField] private int chainPerWave = 1;              // one more pair in every later wave

    [Header("Boss")]
    [SerializeField] private string bossName = "SUBJECT ZERO";

    [Header("References (wired in the scene)")]
    [SerializeField] private Zombie zombiePrefab;
    [SerializeField] private Transform player;
    [SerializeField] private HUD hud;
    [SerializeField] private Level level;

    private const float MinSpawnInterval = 0.8f; // the spawn interval never goes below this
    private const float PackStagger = 0.35f;     // seconds between the zombies of one pack
    private const float BannerSeconds = 2f;      // how long the "Wave N" banner stays up
    private const float GateSeconds = 1.5f;      // time for a gate to open before the ride goes on

    private readonly List<Zombie> aliveZombies = new List<Zombie>();
    private readonly List<ITypingTarget> typingTargets = new List<ITypingTarget>();
    private readonly List<SupplyCrate> activeCrates = new List<SupplyCrate>();
    private readonly List<Vector3> threatPositions = new List<Vector3>();

    private RailMover rail;          // on the Player object
    private Boss boss;               // the current boss, or null

    // Every zombie that is currently alive.
    public List<Zombie> AliveZombies
    {
        get { return aliveZombies; }
    }

    // The barrels of the area being fought (Explosion sets them off).
    public List<Barrel> ActiveBarrels { get; } = new List<Barrel>();

    // The boss being fought, or null.
    public Boss CurrentBoss
    {
        get { return boss; }
    }

    // Used by zombies, barrels... to create their word.
    public HUD Hud
    {
        get { return hud; }
    }

    // Everything the player can type right now: alive zombies, the active
    // barrels and supply crates, and the boss's parts, orbs and quiz answers.
    // The list is reused, so read it right away.
    public List<ITypingTarget> GetTypingTargets()
    {
        typingTargets.Clear();
        typingTargets.AddRange(aliveZombies);
        foreach (Barrel barrel in ActiveBarrels)
        {
            if (barrel != null && barrel.IsAlive)
            {
                typingTargets.Add(barrel);
            }
        }
        foreach (SupplyCrate crate in activeCrates)
        {
            if (crate != null && crate.IsAlive)
            {
                typingTargets.Add(crate);
            }
        }
        if (boss != null && boss.IsAlive)
        {
            boss.AddTypeableParts(typingTargets);
        }
        return typingTargets;
    }

    // UPPERCASE first letters of every word that can be typed right now. New
    // words avoid them, so the first key usually points at exactly one target
    // (a WORD CHAIN pair shares its first letter on purpose, see SpawnChainPair).
    public List<char> UsedFirstLetters()
    {
        List<char> used = new List<char>();
        foreach (ITypingTarget target in GetTypingTargets())
        {
            used.Add(char.ToUpperInvariant(target.Word[0]));
        }
        return used;
    }

    // Awake (not Start): after a Restart, GameManager.Start begins the waves at once,
    // and that may happen before this object's Start. Level builds its map in its
    // own Awake, which runs before this one (see Level's DefaultExecutionOrder).
    private void Awake()
    {
        rail = player.GetComponent<RailMover>();

        if (level == null || level.Encounters.Count == 0)
        {
            Debug.LogError("WaveSpawner: no Level with encounters is wired in the scene.");
            return;
        }

        hud.SetWave(1, level.Encounters.Count);
        rail.PlaceAt(level.StartPosition, level.StartFacing);
    }

    private void LateUpdate()
    {
        LayoutLabels();
        UpdateThreatArrows();
    }

    // Arrows at the screen edges toward zombies that are off screen.
    private void UpdateThreatArrows()
    {
        bool playing = GameManager.Instance.State == GameState.Playing;
        threatPositions.Clear();
        if (playing)
        {
            foreach (Zombie zombie in aliveZombies)
            {
                threatPositions.Add(zombie.HitPoint);
            }
        }
        hud.UpdateThreatArrows(threatPositions, Camera.main);
    }

    // ---- Enemy words on screen ----
    // Every enemy word is a HUD text (HUD.CreateWordLabel), drawn on top of the
    // 3D scene, so no body can hide it. Every frame each word is pinned to its
    // enemy's LabelAnchor:
    //   - it is hidden while its enemy is behind the camera;
    //   - if its enemy is off to a side (or above), the word is kept
    //     labelEdgeMargin inside that screen edge, so it can still be read and
    //     typed (KeepOnScreen). For a zombie that is off screen, this puts the
    //     word next to its red threat arrow (only zombies get arrows);
    //   - it shrinks with distance, like the enemy does (labelReferenceDistance);
    //   - words are placed one by one in priority order (the word being typed
    //     first, then the nearest enemy first). A word that would overlap one
    //     already placed is pushed up until it is clear, and slides there
    //     smoothly, but never past the top edge: in a big pile-up the extra
    //     words stop at the top edge and are drawn over each other.

    [Header("Enemy words")]
    [SerializeField] private float labelReferenceDistance = 12f; // at this distance a word is drawn at full size
    [SerializeField] private float labelMinScale = 0.55f;        // far words never get smaller than this
    [SerializeField] private float labelMaxScale = 1.2f;         // near words never get bigger than this
    [SerializeField] private float labelGap = 4f;                // empty space (HUD units) between stacked words
    [SerializeField] private float labelSlideSharpness = 14f;    // higher = pushed words slide into place faster
    [SerializeField] private float labelEdgeMargin = 110f;       // HUD units: words always stay this far inside the screen edges

    private readonly List<ITypingTarget> labelOrder = new List<ITypingTarget>();
    private readonly List<Rect> placedLabels = new List<Rect>();

    // How far each word is currently pushed up. A word not in here yet (just
    // spawned) jumps straight to its spot instead of sliding in from elsewhere.
    private Dictionary<TMP_Text, float> labelLifts = new Dictionary<TMP_Text, float>();
    private Dictionary<TMP_Text, float> nextLabelLifts = new Dictionary<TMP_Text, float>();


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
            anchor = KeepOnScreen(anchor, size, labelRect.pivot, layer.rect);
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
            // Never push a word off the top of the screen. In a big pile-up the
            // extra words all stop at the top edge and are drawn over each other.
            rect.y = Mathf.Min(rect.y, layer.rect.yMax - labelEdgeMargin - rect.height);
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

    // Moves a word that would stick out of the screen back inside it, keeping
    // labelEdgeMargin free at the edges. The word slides straight toward the
    // middle of the screen, so it ends up on the same side as its enemy (for
    // a zombie that is off screen: next to its red threat arrow). A word that
    // already fits does not move.
    //   anchor = where the word's pivot would be (HUD units, 0,0 = screen middle)
    //   size   = the word's size on screen, pivot = the word's pivot, screen = the word layer
    private Vector2 KeepOnScreen(Vector2 anchor, Vector2 size, Vector2 pivot, Rect screen)
    {
        // The middle of the word's box.
        Vector2 middle = anchor + Vector2.Scale(new Vector2(0.5f, 0.5f) - pivot, size);

        // How far from the screen middle the box's middle may be.
        float roomX = Mathf.Max(0f, screen.width * 0.5f - labelEdgeMargin - size.x * 0.5f);
        float roomY = Mathf.Max(0f, screen.height * 0.5f - labelEdgeMargin - size.y * 0.5f);

        // fit = 1 means it already fits; smaller = pull it that much closer to the middle.
        float fit = 1f;
        if (Mathf.Abs(middle.x) > roomX)
        {
            fit = Mathf.Min(fit, roomX / Mathf.Abs(middle.x));
        }
        if (Mathf.Abs(middle.y) > roomY)
        {
            fit = Mathf.Min(fit, roomY / Mathf.Abs(middle.y));
        }

        // Pull the box's middle that much closer to the screen middle...
        Vector2 newMiddle = middle * fit;
        // ...and move the word's pivot by the same amount, so the whole box moves together.
        return anchor + (newMiddle - middle);
    }

    // Called by GameManager when the game starts.
    public void BeginWaves()
    {
        if (level == null || level.Encounters.Count == 0)
        {
            return;
        }
        StartCoroutine(RunLevel());
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
    private IEnumerator RunLevel()
    {
        int count = level.Encounters.Count;

        for (int index = 0; index < count; index++)
        {
            Encounter encounter = level.Encounters[index];
            int wave = index + 1;
            hud.SetWave(wave, count);

            // 1. Ride to the fight.
            rail.RideAlong(encounter.Route, encounter.Facing);
            while (!rail.IsStopped)
            {
                yield return null;
            }

            // 2. Fight.
            hud.ShowBanner(encounter.IsBossFight ? "BOSS FIGHT" : "Wave " + wave);
            yield return new WaitForSeconds(BannerSeconds);
            hud.HideBanner();
            ActivateProps(encounter);
            StartCoroutine(ShowPropHints());

            if (encounter.IsBossFight)
            {
                yield return RunBossFight(encounter);
            }
            else
            {
                yield return RunZombieWave(wave, encounter);
            }

            // 3. Cleared: the next gate opens.
            DeactivateProps();
            if (encounter.ExitGate != null)
            {
                encounter.ExitGate.Open();
                yield return new WaitForSeconds(GateSeconds);
            }
        }

        GameManager.Instance.WinGame();
    }

    // ---- A zombie wave ----

    private IEnumerator RunZombieWave(int wave, Encounter encounter)
    {
        // 1. This wave's numbers. Wave 1 uses the base values.
        int zombiesThisWave = zombiesBase + zombiesPerWave * wave;
        float speed = zombieSpeed + zombieSpeedPerWave * (wave - 1);
        float interval = spawnInterval - spawnIntervalPerWave * (wave - 1);
        if (interval < MinSpawnInterval)
        {
            interval = MinSpawnInterval;
        }

        // The WORD CHAIN pairs come on top of the wave's zombies (2 more each).
        HashSet<int> pairStarts;
        ZombieKind[] plan = AddChainPairs(PlanKinds(zombiesThisWave, wave), wave, out pairStarts);
        zombiesThisWave = plan.Length;
        Tutorial.Once("type", "Type the word above a zombie to shoot it. Every letter is a bullet!", 6f);

        // 2. Zombies come out in packs of GroupSize, from the spawn points in turn
        //    (a shuffled order, so it is not the same door every time).
        List<SpawnPoint> order = new List<SpawnPoint>(encounter.SpawnPoints);
        Shuffle(order);
        int spawned = 0;
        int next = 0;
        while (spawned < zombiesThisWave)
        {
            SpawnPoint point = order.Count > 0 ? order[next % order.Count] : FallbackSpawnPoint();
            next += 1;

            int pack = Mathf.Min(Mathf.Max(1, encounter.GroupSize), zombiesThisWave - spawned);
            if (point.Door != null)
            {
                point.Door.BurstOpen();
            }

            int packLeft = pack;
            while (packLeft > 0 && spawned < zombiesThisWave)
            {
                // A WORD CHAIN pair takes two places of the plan and comes out together.
                if (pairStarts.Contains(spawned) && SpawnChainPair(point, order, speed))
                {
                    spawned += 2;
                    packLeft -= 2;
                }
                else
                {
                    SpawnZombie(point, plan[spawned], speed);
                    spawned += 1;
                    packLeft -= 1;
                }
                if (packLeft > 0)
                {
                    yield return new WaitForSeconds(PackStagger);
                }
            }
            yield return new WaitForSeconds(interval * pack);
        }

        // 3. Wait until every zombie of this wave is dead or removed.
        while (aliveZombies.Count > 0)
        {
            yield return null;
        }
    }

    // One entry per zombie of the wave (in spawn order): which kind it is.
    private ZombieKind[] PlanKinds(int zombieCount, int wave)
    {
        ZombieKind[] plan = new ZombieKind[zombieCount];
        int index = 0;
        index = Fill(plan, index, ZombieKind.Explosive, CountForWave(wave, explosiveFirstWave, explosiveStartCount, explosivePerWave));
        index = Fill(plan, index, ZombieKind.Runner, CountForWave(wave, runnerFirstWave, runnerStartCount, runnersPerWave));
        Fill(plan, index, ZombieKind.Armored, CountForWave(wave, armoredFirstWave, armoredStartCount, armoredPerWave));
        // The rest stay ZombieKind.Normal (the default value).

        // Fisher-Yates shuffle: the special ones come at random moments.
        for (int i = plan.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            ZombieKind swap = plan[i];
            plan[i] = plan[j];
            plan[j] = swap;
        }
        return plan;
    }

    // Writes kind into plan from index on, count times (never past the end). Returns the next free index.
    private static int Fill(ZombieKind[] plan, int index, ZombieKind kind, int count)
    {
        for (int i = 0; i < count && index < plan.Length; i++)
        {
            plan[index] = kind;
            index += 1;
        }
        return index;
    }

    // How many special zombies of one kind a wave has: none before firstWave,
    // then startCount, plus perWave for every later wave.
    private static int CountForWave(int wave, int firstWave, int startCount, int perWave)
    {
        if (wave < firstWave)
        {
            return 0;
        }
        return startCount + perWave * (wave - firstWave);
    }

    // ---- WORD CHAIN pairs ----

    // Adds this wave's WORD CHAIN pairs to the plan: none before chainFirstWave,
    // then chainStartCount, plus chainPerWave for every later wave. Each pair is
    // two extra NORMAL zombies, next to each other in the plan (they come out at
    // the same moment, from two doors), put in at a random moment of the wave. pairStarts gets the
    // place of the first zombie of each pair.
    private ZombieKind[] AddChainPairs(ZombieKind[] plan, int wave, out HashSet<int> pairStarts)
    {
        pairStarts = new HashSet<int>();
        int pairs = CountForWave(wave, chainFirstWave, chainStartCount, chainPerWave);
        if (pairs <= 0)
        {
            return plan;
        }

        // Each entry of 'units' is one zombie of the plan, or -1 for a whole pair.
        List<int> units = new List<int>();
        foreach (ZombieKind kind in plan)
        {
            units.Add((int)kind);
        }
        for (int p = 0; p < pairs; p++)
        {
            units.Insert(Random.Range(0, units.Count + 1), -1);
        }

        List<ZombieKind> result = new List<ZombieKind>();
        foreach (int unit in units)
        {
            if (unit < 0)
            {
                pairStarts.Add(result.Count);
                result.Add(ZombieKind.Normal);
                result.Add(ZombieKind.Normal);
            }
            else
            {
                result.Add((ZombieKind)unit);
            }
        }
        return result.ToArray();
    }

    // Two normal zombies at the same moment, but out of two DIFFERENT doors
    // (point, and another spawn point of the area), carrying a short word and a
    // longer word that starts with it (hunt / hunter). Both words are purple and
    // a purple line links them across the area (ChainLink): typing the long word
    // kills both. Returns false if no pair of words fits right now.
    private bool SpawnChainPair(SpawnPoint point, List<SpawnPoint> areaPoints, float speed)
    {
        string[] words = WordBank.PickChainPair(UsedFirstLetters());
        if (words == null)
        {
            return false;
        }

        // The other door: any spawn point of the area but this one (an area
        // with a single spawn point uses it for both).
        SpawnPoint otherPoint = point;
        List<SpawnPoint> others = new List<SpawnPoint>();
        foreach (SpawnPoint candidate in areaPoints)
        {
            if (candidate != point)
            {
                others.Add(candidate);
            }
        }
        if (others.Count > 0)
        {
            otherPoint = others[Random.Range(0, others.Count)];
            if (otherPoint.Door != null)
            {
                otherPoint.Door.BurstOpen();
            }
        }

        // Which word comes out of which door is random too.
        bool shortHere = Random.value < 0.5f;
        Zombie shortOne = SpawnZombie(shortHere ? point : otherPoint, ZombieKind.Normal, speed, words[0]);
        Zombie longOne = SpawnZombie(shortHere ? otherPoint : point, ZombieKind.Normal, speed, words[1]);
        shortOne.MarkChainLinked();
        longOne.MarkChainLinked();
        ChainLink.Create(shortOne, longOne);

        GameManager.Instance.RequestTip("word chain", "WORD CHAIN!",
            "Two zombies are linked in PURPLE: \"" + words[0].ToLowerInvariant() + "\" and \""
            + words[1].ToLowerInvariant() + "\".\n\n"
            + "The long word starts with the short one. Type the LONG word:\n"
            + "the short zombie dies on the way, then the long one.\n\n"
            + "One word, two kills - and a WORD CHAIN bonus!",
            Palette.WordChain);
        return true;
    }

    // Called by the Boss's STOMP: a zombie crawls out of the ground at 'at' and
    // walks at the player. A red one blown up next to the boss hurts the boss.
    public Zombie SpawnBossMinion(Vector3 at, ZombieKind kind)
    {
        Vector3 towardPlayer = player.position - at;
        towardPlayer.y = 0f;
        Vector3 exit = at + towardPlayer.normalized * 1.5f;
        SpawnPoint point = new SpawnPoint(at, exit, null, 0.3f);
        return SpawnZombie(point, kind, zombieSpeed + zombieSpeedPerWave * 2f);
    }

    // Spawns one zombie at the spawn point. forcedWord: its word (a WORD CHAIN
    // pair's); null = pick a word whose first letter is not on screen yet.
    private Zombie SpawnZombie(SpawnPoint point, ZombieKind kind, float speed, string forcedWord = null)
    {
        // A small random offset so a pack does not stand in one spot.
        Vector2 offset = Random.insideUnitCircle * point.Spread;
        Vector3 side = new Vector3(offset.x, 0f, offset.y);
        Vector3 position = point.Position + side;
        Vector3 exit = point.Exit + side * 0.5f;

        string word = forcedWord;
        if (word == null)
        {
            List<char> used = UsedFirstLetters();
            word = kind == ZombieKind.Armored ? WordBank.PickArmorWord(used) : WordBank.PickWord(used, kind);
        }

        Quaternion rotation = Quaternion.identity;
        Vector3 walk = exit - position;
        walk.y = 0f;
        if (walk.sqrMagnitude > 0.001f)
        {
            rotation = Quaternion.LookRotation(walk);
        }

        Zombie zombie = Instantiate(zombiePrefab, position, rotation);
        zombie.Setup(word, speed, player, this, kind, exit);
        aliveZombies.Add(zombie);

        // The first time each special kind shows up, say what it does.
        if (kind == ZombieKind.Explosive)
        {
            Tutorial.Once("explosive", "RED ZOMBIE: finish its word to blow up EVERYTHING in its ring!", 6f);
        }
        else if (kind == ZombieKind.Runner)
        {
            Tutorial.Once("runner", "RUNNER! Fast and yellow - shoot it before the slow ones.", 6f);
        }
        else if (kind == ZombieKind.Armored)
        {
            Tutorial.Once("armored", "ARMORED: the first word breaks its armor. Explosions kill it at once!", 6f);
        }
        return zombie;
    }

    // Only used if an encounter has no spawn points: somewhere ahead of the player.
    private SpawnPoint FallbackSpawnPoint()
    {
        Vector3 ahead = player.position + rail.Facing * 30f;
        return new SpawnPoint(ahead, ahead - rail.Facing * 2f, null, 5f);
    }

    private static void Shuffle(List<SpawnPoint> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            SpawnPoint swap = list[i];
            list[i] = list[j];
            list[j] = swap;
        }
    }

    // ---- Barrels and supply crates of the area ----

    // Gives every barrel and crate of the encounter a word: they can be typed now.
    private void ActivateProps(Encounter encounter)
    {
        foreach (Barrel barrel in encounter.Barrels)
        {
            if (barrel != null && !barrel.IsExploded)
            {
                barrel.Activate(WordBank.PickBarrelWord(UsedFirstLetters()), hud);
                ActiveBarrels.Add(barrel);
            }
        }
        foreach (SupplyCrate crate in encounter.Crates)
        {
            if (crate != null && !crate.IsOpened)
            {
                crate.Activate(WordBank.PickCrateWord(crate.Kind, UsedFirstLetters()), hud);
                activeCrates.Add(crate);
            }
        }
    }

    // The fight is over: barrels and crates left behind cannot be typed any more.
    private void DeactivateProps()
    {
        foreach (Barrel barrel in ActiveBarrels)
        {
            if (barrel != null && !barrel.IsExploded)
            {
                barrel.Deactivate();
            }
        }
        ActiveBarrels.Clear();

        foreach (SupplyCrate crate in activeCrates)
        {
            if (crate != null && !crate.IsOpened)
            {
                crate.Deactivate();
            }
        }
        activeCrates.Clear();
    }

    // Explains barrels and crates the first time they appear (a few seconds
    // into the fight, so it does not hide the first hint).
    private IEnumerator ShowPropHints()
    {
        yield return new WaitForSeconds(6f);
        foreach (Barrel barrel in ActiveBarrels)
        {
            if (barrel != null && barrel.IsAlive)
            {
                Tutorial.Once("barrel", "BARREL: type its orange word to blow up everything in its ring. Wait for zombies to walk in!", 7f);
                break;
            }
        }

        yield return new WaitForSeconds(7f);
        foreach (SupplyCrate crate in activeCrates)
        {
            if (crate != null && crate.IsAlive)
            {
                Tutorial.Once("crate", "SUPPLY CRATE: type its green word to grab what is inside.", 6f);
                break;
            }
        }
    }

    // ---- The boss ----

    private IEnumerator RunBossFight(Encounter encounter)
    {
        // 1. The boss appears where it fights.
        CameraDirector.Shake(0.4f);
        boss = Boss.Create(encounter.BossStand, player.position, this, hud);
        hud.ShowBossBar(bossName);

        // 2. Wait until it is dead.
        while (boss.IsAlive)
        {
            yield return null;
        }

        // Zombies it summoned fall down with it.
        foreach (Zombie minion in new List<Zombie>(aliveZombies))
        {
            minion.KillByBlast(boss.BodyCenter);
        }

        // 3. Let it sink into the ground.
        yield return new WaitForSeconds(1.5f);
        boss = null;
        hud.HideBossBar();
    }
}
