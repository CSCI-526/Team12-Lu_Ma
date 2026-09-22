// Zombie.cs
// ---------------------------------------------------------------------------
// One zombie: a grey capsule that walks straight at the player, carries a word
// above its head, and has a blast ring on the ground under it.
//
// THE TWIST: blastRadius = word length x radiusPerLetter. When the player
// finishes typing this zombie's word, Explode() kills this zombie AND every
// other alive zombie standing inside that radius. The ring drawn on the ground
// uses exactly the same radius, so what you see is what explodes.
//
// This script lives on the root of the Zombie prefab (Assets/Prefabs/Zombie).
// The root sits at ground level (y = 0); the capsule, label and ring are children.
// Keep the root's scale at (1, 1, 1). To resize the zombie, scale the "Body" child
// instead: scaling the root would also stretch the ring, and then the ring would
// no longer match the kill radius.
// ---------------------------------------------------------------------------
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class Zombie : MonoBehaviour
{
    [Header("Tuning")]
    [SerializeField] private float attackDistance = 1.5f;  // how close it must get to hit the player
    [SerializeField] private int damagePerHit = 10;
    [SerializeField] private float radiusPerLetter = 0.6f; // blast radius in metres per letter of the word

    [Header("References (wired by the scene builder)")]
    [SerializeField] private TMP_Text label;  // world-space text above the head
    [SerializeField] private BlastRing ring;  // ring on the ground

    // Words this long (or longer) get an orange ring instead of a light blue one.
    private const int LongWordLength = 8;

    // A zombie this many metres BEHIND the player is removed (see the safety net in Update).
    private const float BehindPlayerDistance = 2f;

    // TextMeshPro rich-text colour for the letters typed so far (yellow).
    private const string TypedColorTag = "<color=#FFD21E>";

    public string Word { get; private set; }
    public int TypedCount { get; private set; }       // how many letters have been typed so far
    public float BlastRadius { get; private set; }
    public bool IsAlive { get; private set; }
    public string ColoredWord { get; private set; }   // the word as rich text: typed part yellow, rest white

    private float speed;
    private Transform player;
    private WaveSpawner spawner;
    private Transform cameraTransform;

    // Called by WaveSpawner right after it creates this zombie.
    public void Setup(string word, float moveSpeed, Transform playerTransform, WaveSpawner owner)
    {
        Word = word;
        speed = moveSpeed;
        player = playerTransform;
        spawner = owner;
        cameraTransform = Camera.main.transform;

        IsAlive = true;
        TypedCount = 0;
        BlastRadius = word.Length * radiusPerLetter;

        ring.Show(BlastRadius, word.Length >= LongWordLength);
        RefreshLabel();
    }

    private void Update()
    {
        if (!IsAlive || GameManager.Instance.State != GameState.Playing)
        {
            return;
        }

        // Walk straight toward where the player is right now.
        transform.position = Vector3.MoveTowards(transform.position, player.position, speed * Time.deltaTime);

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

    private void LateUpdate()
    {
        // Not set up yet (for example a Zombie prefab dragged into the scene by hand): do nothing.
        if (!IsAlive)
        {
            return;
        }

        // Billboard: copy the camera's rotation so the word always faces the
        // camera and is never mirrored.
        label.transform.rotation = cameraTransform.rotation;
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

    // Called by TypingController when this zombie's word has been fully typed.
    public void Explode()
    {
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
        Destroy(gameObject);
    }

    // Rebuilds the rich text: typed letters in yellow, remaining letters in white.
    private void RefreshLabel()
    {
        string typedPart = Word.Substring(0, TypedCount);
        string remainingPart = Word.Substring(TypedCount);
        ColoredWord = TypedColorTag + typedPart + "</color>" + remainingPart;
        label.text = ColoredWord;
    }
}
