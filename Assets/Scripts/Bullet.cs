// Bullet.cs
// ---------------------------------------------------------------------------
// Every correct letter the player types fires a Bullet (Bullet.Fire) from a
// point just below the camera at the target. The bullet homes in on the target, so it always
// hits even though zombies keep walking.
//   - A normal shot (any letter but the last) makes a zombie stagger (Flinch).
//   - The FINAL shot (the last letter) is bigger, and ON IMPACT it calls
//     target.CompleteWord(): only then does the zombie die / explode, the
//     barrel blow up, the boss part break...
//
// If the target is already gone when the bullet gets there (caught in another
// blast, or it reached the player), the bullet simply disappears.
//
// It is built entirely in code (a small flat-coloured sphere with a short trail), so
// it needs no prefab.
// ---------------------------------------------------------------------------
using UnityEngine;

public class Bullet : MonoBehaviour
{
    private const float Speed = 70f;          // metres per second
    private const float Size = 0.08f;         // sphere diameter in metres (normal shot)
    private const float FinalSize = 0.16f;    // the killing shot is bigger
    private const float TrailSeconds = 0.06f; // how long the tracer trail is
    private const float MaxLifetime = 3f;     // safety net: never fly forever

    // Where shots start, relative to the camera (right, up, forward), in metres.
    private static readonly Vector3 StartOffset = new Vector3(0f, -0.4f, 0.6f);

    private static readonly Color BulletColor = new Color(1f, 0.85f, 0.3f);

    private ITypingTarget target;
    private bool finalShot;
    private Vector3 flyDirection;
    private float age;

    // Fires a bullet from just below the camera at the target. finalShot = the last letter of the word.
    public static void Fire(ITypingTarget target, bool finalShot)
    {
        Vector3 aim = target.HitPoint;
        Transform cameraTransform = Camera.main.transform;
        Vector3 start = cameraTransform.TransformPoint(StartOffset);
        CameraDirector.Kick(finalShot ? 1f : 0.3f); // a small kick of the view
        float size = finalShot ? FinalSize : Size;

        // Shapes.Block with no parent: a sphere without a collider (it hits by distance).
        GameObject bulletObject = Shapes.Block(PrimitiveType.Sphere, "Bullet", null, start,
            Vector3.one * size, Palette.Unlit(BulletColor));

        TrailRenderer trail = bulletObject.AddComponent<TrailRenderer>();
        trail.time = TrailSeconds;
        trail.startWidth = size;
        trail.endWidth = 0f;
        trail.sharedMaterial = Palette.Unlit(BulletColor);
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        Bullet bullet = bulletObject.AddComponent<Bullet>();
        bullet.target = target;
        bullet.finalShot = finalShot;
        bullet.flyDirection = (aim - start).normalized;
    }

    private void Update()
    {
        // The target is gone (another blast got it first, or it reached the player).
        if (!target.IsAlive)
        {
            Destroy(gameObject);
            return;
        }

        age += Time.deltaTime;
        if (age > MaxLifetime)
        {
            Destroy(gameObject);
            return;
        }

        // Fly straight at where the target is NOW.
        Vector3 aim = target.HitPoint;
        float step = Speed * Time.deltaTime;
        if (Vector3.Distance(transform.position, aim) <= step)
        {
            transform.position = aim;
            Hit();
            return;
        }
        flyDirection = (aim - transform.position).normalized;
        transform.position = Vector3.MoveTowards(transform.position, aim, step);
    }

    private void Hit()
    {
        // Only count the hit while the game is on (not after the player died).
        if (GameManager.Instance.State == GameState.Playing)
        {
            if (finalShot)
            {
                target.CompleteWord();
            }
            else
            {
                // A zombie staggers; other targets just take the hit.
                Zombie zombie = target as Zombie;
                if (zombie != null)
                {
                    zombie.Flinch(flyDirection);
                }
            }
        }
        Destroy(gameObject);
    }
}
