// Zombie.cs
// ---------------------------------------------------------------------------
// One zombie: a grey capsule that walks at the player and carries a word.
//
// There are two kinds of zombie:
//   - NORMAL zombie (grey): typing its word kills only this zombie. No ring.
//   - EXPLOSIVE zombie (bigger red body + ring on the ground, always a long
//     word, see WordBank): blastRadius = word length x radiusPerLetter. When
//     the player finishes typing its word, Explode() kills this zombie AND
//     every other alive zombie standing inside that radius. The ring uses
//     exactly the same radius, so what you see is what explodes.
// WaveSpawner decides which kind each zombie is (see ExplosiveCountForWave).
//
// THE WORD is not part of the 3D scene: it is a HUD text (HUD.CreateWordLabel)
// pinned above the zombie's head by WaveSpawner.LayoutLabels, so other zombies'
// bodies can never hide it. (The prefab's old world-space "Label" child is
// switched off.)
//
// Zombies keep a little distance from each other while they walk
// (separationDistance), so they do not melt into one blob.
//
// This script lives on the root of the Zombie prefab (Assets/Prefabs/Zombie).
// The root sits at ground level (y = 0); the capsule and ring are children.
// Keep the root's scale at (1, 1, 1). To resize the zombie, scale the "Body" child
// instead: scaling the root would also stretch the ring, and then the ring would
// no longer match the kill radius.
// ---------------------------------------------------------------------------
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class Zombie : MonoBehaviour, ITypingTarget
{
    [Header("Tuning")]
    [SerializeField] private float attackDistance = 1.5f;  // how close it must get to hit the player
    [SerializeField] private int damagePerHit = 10;
    [SerializeField] private float radiusPerLetter = 0.75f; // blast radius in metres per letter of the word (explosive zombies only)
    [SerializeField] private Color explosiveBodyColor = new Color(0.9f, 0.25f, 0.2f); // body tint of an explosive zombie
    [SerializeField] private float explosiveBodyScale = 1.5f; // an explosive zombie's body is this many times bigger

    [Header("Spacing")]
    [SerializeField] private float separationDistance = 1.2f; // wanted gap (metres) between two zombies' bodies
    [SerializeField] private float separationStrength = 1.5f; // how hard they step away from each other

    [Header("References (wired by the scene builder)")]
    [SerializeField] private TMP_Text label;  // the prefab's old world-space word; switched off (see header)
    [SerializeField] private BlastRing ring;  // ring on the ground (hidden on normal zombies)

    // Words this long (or longer) get an orange ring instead of a light blue one.
    private const int LongWordLength = 8;

    // A zombie this many metres BEHIND the player is removed (see the safety net in Update).
    private const float BehindPlayerDistance = 2f;

    // The word sits this many metres above the top of the head.
    private const float LabelAboveHead = 0.3f;

    // TextMeshPro rich-text colour for the letters typed so far (yellow).
    public const string TypedColorTag = "<color=#FFD21E>";

    public string Word { get; private set; }
    public int TypedCount { get; private set; }       // how many letters have been typed so far
    public float BlastRadius { get; private set; }    // 0 for a normal zombie
    public bool IsExplosive { get; private set; }
    public bool IsAlive { get; private set; }
    public string ColoredWord { get; private set; }   // the word as rich text: typed part yellow, rest white
    public float BodyRadius { get; private set; }     // metres, used for spacing
    public TMP_Text Label { get; private set; }       // the word on the HUD

    private float speed;
    private Transform player;
    private WaveSpawner spawner;
    private float headHeight; // metres from the feet to the top of the head

    // Called by WaveSpawner right after it creates this zombie.
    public void Setup(string word, float moveSpeed, Transform playerTransform, WaveSpawner owner, bool explosive)
    {
        Word = word;
        speed = moveSpeed;
        player = playerTransform;
        spawner = owner;

        IsAlive = true;
        TypedCount = 0;
        IsExplosive = explosive;

        // Found by name, because the old Label child has a MeshRenderer too.
        Transform body = transform.Find("Body");

        if (IsExplosive)
        {
            BlastRadius = word.Length * radiusPerLetter;
            ring.Show(BlastRadius, word.Length >= LongWordLength);

            // Make the body bigger and red so the player can spot the explosive zombie.
            // Scale the Body child, NOT the root (scaling the root would stretch the ring).
            // The body's centre is at half its height, so scaling its position as well
            // keeps its feet on the ground.
            body.localScale *= explosiveBodyScale;
            body.localPosition *= explosiveBodyScale;

            // .material makes a copy for this zombie only, so the grey zombies stay grey.
            body.GetComponent<Renderer>().material.color = explosiveBodyColor;
        }
        else
        {
            BlastRadius = 0f;
            ring.gameObject.SetActive(false);
        }

        // The body is a capsule centred at half its height, 1 metre wide at scale 1.
        headHeight = body.localPosition.y * 2f;
        BodyRadius = 0.5f * body.localScale.x;

        // The word lives on the HUD now; the prefab's world-space label is not used.
        label.gameObject.SetActive(false);
        Label = spawner.Hud.CreateWordLabel(new Vector2(0.5f, 0f)); // centred, just above the head

        RefreshLabel();
    }

    private void Update()
    {
        if (!IsAlive || GameManager.Instance.State != GameState.Playing)
        {
            return;
        }

        // Walk toward where the player is right now, stepping away from zombies
        // that are too close.
        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;
        Vector3 direction = toPlayer.normalized + SeparationPush() * separationStrength;
        direction.y = 0f;
        direction = Vector3.ClampMagnitude(direction, 1f);
        transform.position += direction * speed * Time.deltaTime;

        // Close enough to bite: hurt the player and disappear.
        if (Vector3.Distance(transform.position, player.position) <= attackDistance)
        {
            GameManager.Instance.TakeDamage(damagePerHit);
            Remove();
            return;
        }

        // Safety net for tuning: if railSpeed is set much higher than the zombie speed,
        // the player can ride PAST a zombie. It is then behind the camera, can never be
        // seen again and may never catch up, so the wave would never end. Remove it
        // (no damage, no score). This never happens with the default values.
        if (transform.position.z < player.position.z - BehindPlayerDistance)
        {
            Remove();
        }
    }

    // Sum of "step away" directions from every zombie that is too close. Each
    // push is stronger the closer the other zombie is (0 at the wanted distance).
    private Vector3 SeparationPush()
    {
        Vector3 push = Vector3.zero;
        foreach (Zombie other in spawner.AliveZombies)
        {
            if (other == this)
            {
                continue;
            }

            Vector3 away = transform.position - other.transform.position;
            away.y = 0f;
            float distance = away.magnitude;
            float wanted = BodyRadius + other.BodyRadius + separationDistance;
            if (distance >= wanted)
            {
                continue;
            }

            if (distance < 0.001f)
            {
                away = Random.insideUnitSphere; // exactly on top of each other: pick any direction
                away.y = 0f;
                distance = 0.001f;
            }
            push += away.normalized * (1f - distance / wanted);
        }
        return push;
    }

    // ---- Word on screen (used by WaveSpawner.LayoutLabels) ----

    public Vector3 LabelAnchor
    {
        get { return transform.position + Vector3.up * (headHeight + LabelAboveHead); }
    }

    // True while this zombie is the player's current target (at least one letter typed).
    public bool IsTargeted
    {
        get { return TypedCount > 0; }
    }

    // ---- Typing progress (used by TypingController) ----

    // The letter the player has to type next.
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

    // Called when the player drops this zombie as their target.
    public void ResetProgress()
    {
        TypedCount = 0;
        RefreshLabel();
    }

    // ---- Dying ----

    public Vector3 Position
    {
        get { return transform.position; }
    }

    // Bullets aim at the chest.
    public Vector3 HitPoint
    {
        get { return transform.position + Vector3.up * headHeight * 0.6f; }
    }

    // Called by the Bullet when it hits (this zombie's word has been fully typed).
    public void CompleteWord()
    {
        Explode();
    }

    // Kills this zombie, and (explosive zombies only) everything inside the blast radius.
    public void Explode()
    {
        // A normal zombie has no blast: only it dies.
        if (!IsExplosive)
        {
            Die();
            return;
        }

        // Let the ring go first, so it survives this zombie and can play its effect.
        ring.PlayBlastEffect();

        // Copy the list, because Die() removes zombies from the spawner's list while we loop.
        List<Zombie> zombies = new List<Zombie>(spawner.AliveZombies);
        foreach (Zombie other in zombies)
        {
            // The ring is flat on the ground, so measure the distance on the ground too.
            Vector3 offset = other.transform.position - transform.position;
            offset.y = 0f;

            // This zombie is in the list as well (distance 0), so it dies with the rest.
            if (offset.magnitude <= BlastRadius)
            {
                other.Die();
            }
        }
    }

    // Killed by the player (typed, or caught in a blast): counts for score.
    public void Die()
    {
        GameManager.Instance.AddKill();
        Remove();
    }

    // Takes this zombie out of the game. No score here: also used when it reaches the player.
    private void Remove()
    {
        // Destroy() only really happens at the end of the frame, so other scripts
        // check IsAlive to know right away that this zombie is gone.
        IsAlive = false;
        spawner.RemoveZombie(this);
        Destroy(Label.gameObject); // the word is on the HUD, not a child of this zombie
        Destroy(gameObject);
    }

    // Rebuilds the rich text: typed letters in yellow, remaining letters in white.
    private void RefreshLabel()
    {
        string typedPart = Word.Substring(0, TypedCount);
        string remainingPart = Word.Substring(TypedCount);
        ColoredWord = TypedColorTag + typedPart + "</color>" + remainingPart;
        Label.text = ColoredWord;
    }
}
