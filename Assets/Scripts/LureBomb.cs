// LureBomb.cs
// ---------------------------------------------------------------------------
// The lure bomb (press 1): a plain dark sphere. It is thrown ahead of the
// player, lands, and blinks red for FuseSeconds (faster and faster). Every
// zombie within LureRadius forgets the player and crowds around it (each
// Zombie asks FindLure). Then it explodes (Explosion.Detonate with
// BlastRadius): everything that gathered around it dies. Its blast ring is
// drawn on the ground while it waits, so you can see what will die.
//
// How it is used:
//   LureBomb.Throw(from, pointOnTheGround);             // by the Powers script
//   LureBomb lure = LureBomb.FindLure(zombie.Position); // by each Zombie
//
// The fuse keeps ticking during the freeze power (frozen zombies just cannot
// walk to it), but everything stops while the game is paused.
// ---------------------------------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

public class LureBomb : MonoBehaviour
{
    public const float LureRadius = 16f;   // zombies closer than this walk to the bomb
    public const float BlastRadius = 4.5f;
    public const float FuseSeconds = 3.5f; // from landing to the explosion
    public const float BossDamage = 120f;  // if it goes off next to the boss

    private const float FlightSeconds = 0.6f;       // from the hand to the ground
    private const float ArcHeight = 2f;             // metres above the straight line at the top of the throw
    private const float BombSize = 0.35f;           // diameter in metres
    private const float FirstBlinkInterval = 0.5f;  // seconds between blinks right after landing...
    private const float LastBlinkInterval = 0.1f;   // ...and just before the explosion

    // Bombs that have landed and are waiting to explode.
    public static readonly List<LureBomb> Active = new List<LureBomb>();

    private Vector3 startPoint;
    private Vector3 landingPoint;
    private float flightTimer;
    private float fuseTimer;
    private float blinkTimer;
    private bool blinkOn;
    private Renderer ball;

    // Throws a bomb from "from" so that it lands on landingPoint (on the ground).
    public static LureBomb Throw(Vector3 from, Vector3 landingPoint)
    {
        GameObject bombObject = new GameObject("LureBomb");
        bombObject.transform.position = from;
        LureBomb bomb = bombObject.AddComponent<LureBomb>();
        bomb.startPoint = from;
        bomb.landingPoint = landingPoint;
        bomb.ball = Shapes.Block(PrimitiveType.Sphere, "Ball", bombObject.transform,
            new Vector3(0f, BombSize * 0.5f, 0f), Vector3.one * BombSize, Palette.Lit(Palette.Bomb)).GetComponent<Renderer>();
        return bomb;
    }

    // True while it is on the ground, waiting to explode.
    public bool IsLuring { get; private set; }

    public Vector3 Position
    {
        get { return transform.position; }
    }

    // The luring bomb closest to position, if it is within LureRadius; otherwise null.
    public static LureBomb FindLure(Vector3 position)
    {
        LureBomb closest = null;
        float closestDistance = LureRadius;
        foreach (LureBomb bomb in Active)
        {
            if (bomb == null || !bomb.IsLuring)
            {
                continue;
            }

            // Measured flat on the ground, like the rings.
            Vector3 offset = bomb.Position - position;
            offset.y = 0f;
            float distance = offset.magnitude;
            if (distance <= closestDistance)
            {
                closest = bomb;
                closestDistance = distance;
            }
        }
        return closest;
    }

    private void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.State != GameState.Playing)
        {
            return;
        }

        if (!IsLuring)
        {
            Fly();
        }
        else
        {
            Tick();
        }
    }

    // In the air: a straight line from start to landing, plus an arc on top.
    private void Fly()
    {
        flightTimer += Time.deltaTime;
        float t = Mathf.Clamp01(flightTimer / FlightSeconds);
        Vector3 position = Vector3.Lerp(startPoint, landingPoint, t);
        position.y += ArcHeight * 4f * t * (1f - t); // 0 at both ends, ArcHeight in the middle
        transform.position = position;

        if (t >= 1f)
        {
            Land();
        }
    }

    private void Land()
    {
        transform.position = landingPoint;
        IsLuring = true;
        Active.Add(this);
        BlastRing.Create(transform, BlastRadius, Palette.BlastOrange);
    }

    // On the ground: blink faster and faster, then explode.
    private void Tick()
    {
        fuseTimer += Time.deltaTime;
        if (fuseTimer >= FuseSeconds)
        {
            Active.Remove(this);
            IsLuring = false;
            Explosion.Detonate(transform.position, BlastRadius, null, BossDamage);
            Destroy(gameObject);
            return;
        }

        blinkTimer -= Time.deltaTime;
        if (blinkTimer <= 0f)
        {
            blinkOn = !blinkOn;
            ball.sharedMaterial = Palette.Lit(blinkOn ? Palette.BombBlink : Palette.Bomb);
            blinkTimer = Mathf.Lerp(FirstBlinkInterval, LastBlinkInterval, fuseTimer / FuseSeconds);
        }
    }

    // The list is static (it survives a scene reload), so a bomb must always leave it.
    private void OnDestroy()
    {
        Active.Remove(this);
    }
}
