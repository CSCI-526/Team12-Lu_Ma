// RocketFx.cs
// ---------------------------------------------------------------------------
// The looks of the RPG rocket (a combo weapon, see Powers and Bullet), in the
// game's greybox style: only flat-coloured primitive shapes, no textures.
//
//   RocketFx.Launch(start, direction);  // muzzle flash + sparks + camera kick
//   RocketFx.SmokePuff(position);        // one grey puff left behind in flight
//   RocketFx.Blast(center, radius);      // flash, fireball, shockwave ring, debris
//
// Every piece is a FxPiece: it grows, shrinks, flies and falls on its own for a
// short life, then removes itself. Pieces freeze while the game is paused
// (they use game time).
// ---------------------------------------------------------------------------
using UnityEngine;

public static class RocketFx
{
    private static readonly Color FlashYellow = new Color(1f, 0.9f, 0.45f);
    private static readonly Color FireOrange = new Color(1f, 0.45f, 0.1f);
    private static readonly Color SmokeGrey = new Color(0.55f, 0.55f, 0.58f);
    private static readonly Color DebrisDark = new Color(0.25f, 0.22f, 0.2f);

    // The rocket leaves the launcher: a flash that pops and shrinks, sparks
    // shooting forward, and a hard kick of the view.
    public static void Launch(Vector3 start, Vector3 direction)
    {
        direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
        Piece(PrimitiveType.Sphere, start, FlashYellow, 0.15f, 0.9f, 0.18f, Vector3.zero, 0f);

        for (int i = 0; i < 6; i++)
        {
            Vector3 spread = Random.insideUnitSphere * 0.6f;
            Vector3 velocity = (direction + spread).normalized * Random.Range(5f, 9f);
            Piece(PrimitiveType.Cube, start, FireOrange, 0.08f, 0f, 0.25f, velocity, 0f);
        }

        CameraDirector.Kick(3f);
        CameraDirector.Shake(0.25f);
    }

    // A grey puff where the rocket just was: it swells a little, then shrinks away.
    public static void SmokePuff(Vector3 position)
    {
        Vector3 drift = Random.insideUnitSphere * 0.3f + Vector3.up * 0.4f;
        Piece(PrimitiveType.Sphere, position, SmokeGrey, 0.2f, 0f, 0.6f, drift, 0f, 0.45f);
    }

    // The explosion: a white flash, an orange fireball the size of the blast,
    // a fast shockwave ring on the ground, debris flung out, a strong shake.
    // (The damage and the usual Explosion look come from Explosion.Detonate.)
    public static void Blast(Vector3 center, float radius)
    {
        Piece(PrimitiveType.Sphere, center, Color.white, radius * 0.4f, radius * 0.9f, 0.12f, Vector3.zero, 0f);
        Piece(PrimitiveType.Sphere, center, FireOrange, radius * 0.5f, 0f, 0.45f, Vector3.zero, 0f, radius * 1.6f);

        for (int i = 0; i < 12; i++)
        {
            Vector3 outward = Random.insideUnitSphere;
            outward.y = Mathf.Abs(outward.y) + 0.4f;
            Vector3 velocity = outward.normalized * Random.Range(6f, 12f);
            Piece(PrimitiveType.Cube, center, i % 3 == 0 ? FireOrange : DebrisDark,
                Random.Range(0.12f, 0.25f), 0f, Random.Range(0.6f, 1f), velocity, 18f);
        }

        Transform ringHolder = new GameObject("RocketShockwave").transform;
        Vector3 ground = center;
        ground.y = 0f;
        ringHolder.position = ground;
        BlastRing ring = BlastRing.Create(ringHolder, 0.5f, FlashYellow);
        FxPiece ringPiece = ringHolder.gameObject.AddComponent<FxPiece>();
        ringPiece.StartRing(ring, radius * 2.2f, 0.35f);

        CameraDirector.Shake(0.7f);
    }

    // One animated shape: size goes startSize -> (peakSize, if > 0, at the
    // middle of its life) -> endSize, it flies at 'velocity' and falls with 'gravity'.
    private static void Piece(PrimitiveType shape, Vector3 position, Color color, float startSize, float endSize,
        float lifetime, Vector3 velocity, float gravity, float peakSize = 0f)
    {
        GameObject piece = Shapes.Block(shape, "RocketFx", null, position, Vector3.one * startSize, Palette.Unlit(color));
        piece.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        if (shape == PrimitiveType.Cube)
        {
            piece.transform.rotation = Random.rotation;
        }
        FxPiece fx = piece.AddComponent<FxPiece>();
        fx.Begin(startSize, peakSize, endSize, lifetime, velocity, gravity);
    }
}

// A short-lived effect piece (see RocketFx): animates its size and position,
// then destroys itself. It can also grow a BlastRing (a shockwave).
public class FxPiece : MonoBehaviour
{
    private float startSize;
    private float peakSize;   // 0 = no peak: straight from startSize to endSize
    private float endSize;
    private float lifetime;
    private Vector3 velocity;
    private float gravity;
    private float age;

    private BlastRing ring;   // a shockwave ring instead of a shape
    private float ringRadius;

    public void Begin(float fromSize, float middleSize, float toSize, float seconds, Vector3 startVelocity, float fall)
    {
        startSize = fromSize;
        peakSize = middleSize;
        endSize = toSize;
        lifetime = Mathf.Max(0.01f, seconds);
        velocity = startVelocity;
        gravity = fall;
    }

    public void StartRing(BlastRing blastRing, float radius, float seconds)
    {
        ring = blastRing;
        ringRadius = radius;
        lifetime = Mathf.Max(0.01f, seconds);
    }

    private void Update()
    {
        age += Time.deltaTime;
        float t = Mathf.Clamp01(age / lifetime);

        if (ring != null)
        {
            ring.SetRadius(Mathf.Lerp(0.5f, ringRadius, 1f - (1f - t) * (1f - t)));
        }
        else
        {
            float size;
            if (peakSize > 0f)
            {
                size = t < 0.5f ? Mathf.Lerp(startSize, peakSize, t * 2f) : Mathf.Lerp(peakSize, endSize, (t - 0.5f) * 2f);
            }
            else
            {
                size = Mathf.Lerp(startSize, endSize, t);
            }
            transform.localScale = Vector3.one * size;

            velocity += Vector3.down * gravity * Time.deltaTime;
            transform.position += velocity * Time.deltaTime;
        }

        if (t >= 1f)
        {
            Destroy(gameObject);
        }
    }
}
