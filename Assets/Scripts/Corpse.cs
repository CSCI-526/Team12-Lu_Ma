// Corpse.cs
// ---------------------------------------------------------------------------
// Makes a dead zombie's capsule fly. Launch turns an object into a physics
// object that tumbles on the floor, then sinks into the ground and disappears:
//
//   Corpse.Launch(body.gameObject, Vector3.up * 5f + away * 3f, Random.insideUnitSphere * 6f, 2f);
//
// What Launch does, step by step:
//   1. The piece is detached from its parent, keeping its place in the world
//      (so the body can fly while the zombie object is removed).
//   2. Any collider it already has is removed, and ONE box collider is added
//      around all its shapes, so it bounces on the level's floors and walls
//      (they keep their BoxColliders, see Shapes).
//   3. A Rigidbody is added and given the starting speed and spin.
//   4. After lifeSeconds it stops being physical and sinks into the ground
//      while shrinking, then it is destroyed. Pieces that fall out of the
//      world (below y = -5) are destroyed at once.
//
// Only the dead pieces use physics; gameplay never does (hits are measured
// with distances), so corpses can never block a zombie or the player. Pieces
// are put on the "Ignore Raycast" layer, so they cannot block a raycast either
// (e.g. the lure bomb's throw, see Powers).
// ---------------------------------------------------------------------------
using UnityEngine;

public class Corpse : MonoBehaviour
{
    private const float Mass = 1f;               // kilograms (all pieces weigh the same: they only need to tumble)
    private const float LinearDamping = 0.05f;   // air drag on its movement
    private const float AngularDamping = 0.3f;   // air drag on its spin
    private const float MaxSpin = 25f;           // radians per second (Unity's default limit is 7)
    private const float SinkDepth = 1.5f;        // metres it sinks into the ground at the end
    private const float SinkSeconds = 0.7f;      // how long the sinking takes
    private const float SinkEndScale = 0.3f;     // it also shrinks to this fraction of its size while sinking
    private const float FellOutHeight = -5f;     // below this it fell out of the level: remove it
    private const float MinColliderSize = 0.05f; // metres: no paper-thin colliders (they would fall through floors)
    private const int IgnoreRaycastLayer = 2;     // Unity's built-in "Ignore Raycast" layer

    private Rigidbody body;
    private BoxCollider box;
    private float lifeLeft;       // seconds of tumbling left
    private bool isSinking;
    private float sinkTimer;
    private Vector3 sinkStart;    // where it was when it started sinking
    private Vector3 startScale;   // its size when it started sinking

    // piece: any GameObject (with child shapes or not). It is detached from its
    // parent (keeping its place in the world), given a Rigidbody and a collider
    // around its shapes, then:
    //   velocity - initial speed in m/s (e.g. away from an explosion, plus up)
    //   spin     - initial spin in radians/s (tumbling)
    //   lifeSeconds - how long it lies around before sinking away
    public static void Launch(GameObject piece, Vector3 velocity, Vector3 spin, float lifeSeconds = 2.5f)
    {
        if (piece == null)
        {
            return;
        }

        // Already flying (launched twice): just give it the new push.
        Corpse existing = piece.GetComponent<Corpse>();
        if (existing != null)
        {
            if (existing.body != null && !existing.isSinking)
            {
                existing.body.linearVelocity = velocity;
                existing.body.angularVelocity = spin;
            }
            return;
        }

        // 1. Detach, keeping its place in the world ("true").
        piece.transform.SetParent(null, true);

        // 2. One collider around all its shapes. Colliders it had before are
        // removed first, otherwise they would all
        // become part of the flying object. DestroyImmediate: they must be gone
        // before the Rigidbody is added below.
        Collider[] oldColliders = piece.GetComponentsInChildren<Collider>();
        for (int i = 0; i < oldColliders.Length; i++)
        {
            DestroyImmediate(oldColliders[i]);
        }

        Corpse corpse = piece.AddComponent<Corpse>();
        corpse.box = piece.AddComponent<BoxCollider>();
        corpse.FitColliderToShapes();
        // Only this root object has a collider now: on the "Ignore Raycast"
        // layer it still bounces on floors and walls, but never blocks a ray.
        piece.layer = IgnoreRaycastLayer;

        // 3. Physics.
        Rigidbody newBody = piece.GetComponent<Rigidbody>();
        if (newBody == null)
        {
            newBody = piece.AddComponent<Rigidbody>();
        }
        newBody.mass = Mass;
        newBody.linearDamping = LinearDamping;
        newBody.angularDamping = AngularDamping;
        newBody.maxAngularVelocity = MaxSpin;
        newBody.interpolation = RigidbodyInterpolation.Interpolate; // smooth movement between physics steps
        // Speculative collision detection stops fast small pieces from passing
        // through thin floors, and it is cheap.
        newBody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        newBody.isKinematic = false;
        newBody.useGravity = true;
        newBody.linearVelocity = velocity;
        newBody.angularVelocity = spin;

        corpse.body = newBody;
        corpse.lifeLeft = Mathf.Max(0f, lifeSeconds);
    }

    // Sizes the box collider so it encloses every shape (mesh) of the piece.
    // Each shape's mesh box has 8 corners; they are moved into the piece's own
    // space, and the box around all of those corners becomes the collider.
    private void FitColliderToShapes()
    {
        MeshFilter[] shapes = GetComponentsInChildren<MeshFilter>();
        bool foundAny = false;
        Vector3 min = Vector3.zero;
        Vector3 max = Vector3.zero;

        for (int i = 0; i < shapes.Length; i++)
        {
            Mesh mesh = shapes[i].sharedMesh;
            if (mesh == null)
            {
                continue;
            }

            Bounds meshBounds = mesh.bounds; // in the shape's own space
            Transform shapeTransform = shapes[i].transform;
            for (int corner = 0; corner < 8; corner++)
            {
                // Bits 0, 1 and 2 of "corner" pick the min or max side on X, Y and Z.
                Vector3 local = new Vector3(
                    (corner & 1) == 0 ? meshBounds.min.x : meshBounds.max.x,
                    (corner & 2) == 0 ? meshBounds.min.y : meshBounds.max.y,
                    (corner & 4) == 0 ? meshBounds.min.z : meshBounds.max.z);
                Vector3 world = shapeTransform.TransformPoint(local);
                Vector3 inPiece = transform.InverseTransformPoint(world);

                if (!foundAny)
                {
                    min = inPiece;
                    max = inPiece;
                    foundAny = true;
                }
                else
                {
                    min = Vector3.Min(min, inPiece);
                    max = Vector3.Max(max, inPiece);
                }
            }
        }

        if (!foundAny)
        {
            // No shapes at all: a small box so it still lands on the floor.
            box.center = Vector3.zero;
            box.size = Vector3.one * 0.2f;
            return;
        }

        // The collider's size is in the piece's own space, so a scaled piece
        // (e.g. a cube scaled to 0.2 m) must not end up with a paper-thin box:
        // the minimum is converted from metres into that space.
        Vector3 lossy = transform.lossyScale;
        Vector3 size = max - min;
        size.x = Mathf.Max(size.x, MinColliderSize / Mathf.Max(0.0001f, Mathf.Abs(lossy.x)));
        size.y = Mathf.Max(size.y, MinColliderSize / Mathf.Max(0.0001f, Mathf.Abs(lossy.y)));
        size.z = Mathf.Max(size.z, MinColliderSize / Mathf.Max(0.0001f, Mathf.Abs(lossy.z)));

        box.center = (min + max) * 0.5f;
        box.size = size;
    }

    private void Update()
    {
        // Fell through a gap or off the map: nothing to see any more.
        if (transform.position.y < FellOutHeight)
        {
            Destroy(gameObject);
            return;
        }

        // Time.deltaTime is 0 while the game is paused, so corpses wait too.
        if (!isSinking)
        {
            lifeLeft -= Time.deltaTime;
            if (lifeLeft <= 0f)
            {
                StartSinking();
            }
            return;
        }

        sinkTimer += Time.deltaTime;
        float progress = Mathf.Clamp01(sinkTimer / SinkSeconds); // 0 to 1
        transform.position = sinkStart + Vector3.down * (SinkDepth * progress);
        transform.localScale = startScale * Mathf.Lerp(1f, SinkEndScale, progress);

        if (progress >= 1f)
        {
            Destroy(gameObject);
        }
    }

    // Stops the physics so the piece can slide down into the floor.
    private void StartSinking()
    {
        isSinking = true;
        sinkTimer = 0f;

        // Without a collider nothing holds it up, and nothing bumps into it.
        if (box != null)
        {
            Destroy(box);
            box = null;
        }

        // Kinematic = physics no longer moves it; this script does. No
        // interpolation, or physics would keep pulling it back to its old spot.
        if (body != null)
        {
            body.interpolation = RigidbodyInterpolation.None;
            body.isKinematic = true;
        }

        sinkStart = transform.position;
        startScale = transform.localScale;
    }
}
