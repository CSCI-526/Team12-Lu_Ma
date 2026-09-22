// EnergyOrb.cs
// ---------------------------------------------------------------------------
// The boss's attack. When its red warning glow is full, the Boss fires an
// EnergyOrb (EnergyOrb.Launch) at the player. The orb flies for flightSeconds
// and carries its own word, like an enemy:
//   - type the word -> a Bullet hits the orb -> it bursts, the attack is
//     cancelled (and it counts as a kill for score and combo);
//   - too slow -> the orb reaches the player and deals its damage.
//
// Orb words are short but tricky: case-sensitive, with digits and symbols
// (WordBank.PickOrbWord). IsCaseSensitive = true tells TypingController to
// compare every character exactly.
//
// Built entirely in code (a glowing pulsing sphere), so it needs no prefab.
// ---------------------------------------------------------------------------
using System.Collections;
using TMPro;
using UnityEngine;

public class EnergyOrb : MonoBehaviour, ITypingTarget
{
    private const float Size = 0.9f;             // diameter in metres
    private const float PulseSpeed = 10f;        // how fast it throbs
    private const float PulseAmount = 0.12f;     // how much it throbs (fraction of its size)
    private const float HitPlayerDistance = 1.2f;
    private const float LabelAbove = 0.7f;       // the word sits this far above the orb's centre

    private static readonly Color OrbColor = new Color(1f, 0.15f, 0.55f);    // hot pink
    private static Material sharedMaterial;

    public string Word { get; private set; }
    public string ColoredWord { get; private set; }
    public int TypedCount { get; private set; }
    public bool IsAlive { get; private set; }
    public TMP_Text Label { get; private set; }

    private float speed;  // metres per second
    private int damage;
    private HUD hud;

    // Fires an orb from position at the player. It arrives after flightSeconds
    // unless the player types its word first.
    public static EnergyOrb Launch(Vector3 position, string word, float flightSeconds, int damage, HUD hud)
    {
        GameObject orbObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        orbObject.name = "EnergyOrb";
        Destroy(orbObject.GetComponent<Collider>()); // hits by distance, not physics
        orbObject.transform.position = position;
        orbObject.transform.localScale = Vector3.one * Size;
        orbObject.GetComponent<Renderer>().sharedMaterial = GetMaterial();

        EnergyOrb orb = orbObject.AddComponent<EnergyOrb>();
        orb.Word = word;
        orb.damage = damage;
        orb.hud = hud;
        orb.IsAlive = true;
        orb.speed = Vector3.Distance(position, AimPoint()) / Mathf.Max(0.1f, flightSeconds);

        orb.Label = hud.CreateWordLabel(new Vector2(0.5f, 0f)); // centred above the orb
        orb.RefreshLabel();
        return orb;
    }

    // An unlit (always bright) material, created once. See Bullet.GetMaterial.
    private static Material GetMaterial()
    {
        if (sharedMaterial == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }
            sharedMaterial = new Material(shader);
            sharedMaterial.color = OrbColor;
        }
        return sharedMaterial;
    }

    // The orb flies at the player's face, a little below the camera.
    private static Vector3 AimPoint()
    {
        return Camera.main.transform.position + Vector3.down * 0.4f;
    }

    private void Update()
    {
        if (!IsAlive || GameManager.Instance.State != GameState.Playing)
        {
            return;
        }

        // Throb, so it reads as "dangerous energy".
        float pulse = 1f + Mathf.Sin(Time.time * PulseSpeed) * PulseAmount;
        transform.localScale = Vector3.one * Size * pulse;

        Vector3 aim = AimPoint();
        transform.position = Vector3.MoveTowards(transform.position, aim, speed * Time.deltaTime);

        // Too late: it hits the player.
        if (Vector3.Distance(transform.position, aim) <= HitPlayerDistance)
        {
            GameManager.Instance.TakeDamage(damage);
            hud.FlashRed();
            Vanish();
            Destroy(gameObject);
        }
    }

    // ---- ITypingTarget ----

    public bool IsTargeted
    {
        get { return TypedCount > 0; }
    }

    public bool IsCaseSensitive
    {
        get { return true; }
    }

    public Vector3 Position
    {
        get { return transform.position; }
    }

    public Vector3 HitPoint
    {
        get { return transform.position; }
    }

    public Vector3 LabelAnchor
    {
        get { return transform.position + Vector3.up * LabelAbove; }
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

    // The player's bullet hit it: the attack is cancelled.
    public void CompleteWord()
    {
        GameManager.Instance.AddKill();
        Vanish();
        StartCoroutine(Burst());
    }

    // Called by the Boss when it dies: orbs still in the air fizzle out.
    public void Dissipate()
    {
        if (!IsAlive)
        {
            return;
        }
        Vanish();
        StartCoroutine(Burst());
    }

    // Stops it counting as a target and removes its word from the HUD.
    private void Vanish()
    {
        IsAlive = false;
        Destroy(Label.gameObject);
    }

    // A quick "pop": swells up and shrinks away, then removes the orb.
    private IEnumerator Burst()
    {
        const float duration = 0.2f;
        for (float time = 0f; time < duration; time += Time.deltaTime)
        {
            float progress = time / duration;
            float scale = Size * Mathf.Lerp(1.6f, 0f, progress * progress);
            transform.localScale = Vector3.one * scale;
            yield return null;
        }
        Destroy(gameObject);
    }

    private void RefreshLabel()
    {
        ColoredWord = Zombie.TypedColorTag + Word.Substring(0, TypedCount) + "</color>" + Word.Substring(TypedCount);
        Label.text = ColoredWord;
    }
}
