// Bullet.cs
// ---------------------------------------------------------------------------
// When the player finishes typing a word, TypingController fires a Bullet
// (Bullet.Fire) from the player's "gun" (a point just below and to the right of
// the camera) at that target. The bullet homes in on the target, so it always
// hits even though the zombie keeps walking, and ON IMPACT calls
// target.CompleteWord(): only then does the zombie die / explode, or the boss
// part break.
//
// If the target is already gone when the bullet gets there (caught in another
// zombie's blast, or it reached the player), the bullet simply disappears.
//
// It is built entirely in code (a small glowing sphere with a short trail), so
// it needs no prefab.
// ---------------------------------------------------------------------------
using UnityEngine;

public class Bullet : MonoBehaviour
{
    private const float Speed = 60f;          // metres per second
    private const float Size = 0.15f;         // sphere diameter in metres
    private const float TrailSeconds = 0.08f; // how long the tracer trail is
    private const float MaxLifetime = 3f;     // safety net: never fly forever

    // Where the gun sits, relative to the camera (right, up, forward), in metres.
    private static readonly Vector3 MuzzleOffset = new Vector3(0.35f, -0.35f, 0.6f);

    private static readonly Color BulletColor = new Color(1f, 0.85f, 0.3f);
    private static Material sharedMaterial; // one material for every bullet

    private ITypingTarget target;
    private float age;

    // Fires a bullet from the gun at the target.
    public static void Fire(ITypingTarget target)
    {
        Transform cameraTransform = Camera.main.transform;
        Vector3 muzzle = cameraTransform.TransformPoint(MuzzleOffset);

        GameObject bulletObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        bulletObject.name = "Bullet";
        Destroy(bulletObject.GetComponent<Collider>()); // it hits by distance, not physics
        bulletObject.transform.position = muzzle;
        bulletObject.transform.localScale = Vector3.one * Size;
        bulletObject.GetComponent<Renderer>().sharedMaterial = GetMaterial();

        TrailRenderer trail = bulletObject.AddComponent<TrailRenderer>();
        trail.time = TrailSeconds;
        trail.startWidth = Size;
        trail.endWidth = 0f;
        trail.sharedMaterial = GetMaterial();

        Bullet bullet = bulletObject.AddComponent<Bullet>();
        bullet.target = target;
    }

    // An unlit (always bright) yellow material, created once. URP's Unlit shader
    // is used because this project renders with URP; it is already in the build
    // because the blast ring materials use it.
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
            sharedMaterial.color = BulletColor;
        }
        return sharedMaterial;
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
        transform.position = Vector3.MoveTowards(transform.position, aim, step);
    }

    private void Hit()
    {
        // Only count the hit while the game is on (not after the player died).
        if (GameManager.Instance.State == GameState.Playing)
        {
            target.CompleteWord();
        }
        Destroy(gameObject);
    }
}
