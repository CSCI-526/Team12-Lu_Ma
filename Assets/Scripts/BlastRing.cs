// BlastRing.cs
// ---------------------------------------------------------------------------
// Draws a blast radius as a circle on the ground with a LineRenderer.
// The circle's radius is EXACTLY the kill radius, so what you see is what explodes.
//
// RED ZOMBIES (the Zombie prefab has a ring child, wired in the prefab): an
// orange ring. The ring is a child of the zombie, so it moves with it.
//
// When the zombie explodes, PlayBlastEffect() detaches the ring, scales it up
// to about 1.5x over 0.3 seconds, then destroys it.
//
// BARRELS, LURE BOMBS and EXPLOSIONS build their ring in code instead:
//   BlastRing ring = BlastRing.Create(transform, 4f, new Color(1f, 0.55f, 0.1f));
// makes a ring of radius 4 m in that colour, lying on the ground under
// "transform" (the ring sits at its parent's position, so the parent must be
// on the ground). SetRadius redraws it with a new radius.
//
// The materials use the URP "Unlit" shader so they render correctly in this
// project's render pipeline (a wrong shader would show up as bright magenta).
// ---------------------------------------------------------------------------
using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(LineRenderer))]
public class BlastRing : MonoBehaviour
{
    [Header("Ring shape")]
    [SerializeField] private int pointCount = 48;        // points around the circle
    [SerializeField] private float lineWidth = 0.08f;
    [SerializeField] private float heightAboveGround = 0.1f;  // above the paving and the lawns, so it never hides in them or flickers

    [Header("Blast effect")]
    [SerializeField] private float blastScale = 1.5f;    // how much the ring grows
    [SerializeField] private float blastDuration = 0.3f; // seconds

    [Header("References (wired in the Zombie prefab)")]
    [SerializeField] private Material longWordMaterial;  // orange
    [SerializeField] private Material shortWordMaterial; // light blue (not used now: red zombies always have long words)

    private bool isBlasting;
    private float blastTimer;
    private LineRenderer line; // found the first time it is needed

    // Builds a ring in code (for barrels, lure bombs and explosions; zombies use
    // the ring in their prefab). The ring lies flat on the ground at the parent's
    // position and moves with it. color: any colour, it gets a shared Unlit
    // material from Palette (a flat colour: keep every value between 0 and 1).
    public static BlastRing Create(Transform parent, float radius, Color color)
    {
        GameObject ringObject = new GameObject("BlastRing");
        ringObject.transform.SetParent(parent, false);

        LineRenderer newLine = ringObject.AddComponent<LineRenderer>();
        newLine.shadowCastingMode = ShadowCastingMode.Off; // a flat line on the floor casts no shadow
        newLine.receiveShadows = false;

        BlastRing ring = ringObject.AddComponent<BlastRing>();
        Material material = Palette.Unlit(color);
        ring.longWordMaterial = material;
        ring.shortWordMaterial = material;
        ring.Show(radius, true);
        return ring;
    }

    // Draws the circle. Called once by Zombie.Setup() (and by Create).
    public void Show(float radius, bool isLongWord)
    {
        LineRenderer ringLine = GetLine();
        ringLine.useWorldSpace = false; // points are relative to this object, so the ring follows the zombie
        ringLine.loop = true;           // connect the last point back to the first
        ringLine.startWidth = lineWidth;
        ringLine.endWidth = lineWidth;
        ringLine.positionCount = pointCount;

        if (isLongWord)
        {
            ringLine.sharedMaterial = longWordMaterial;
        }
        else
        {
            ringLine.sharedMaterial = shortWordMaterial;
        }

        // Lie flat, just above the ground, centred under the zombie.
        transform.localPosition = new Vector3(0f, heightAboveGround, 0f);

        SetRadius(radius);
    }

    // Redraws the circle with a new radius (in metres), e.g. a shock ring that grows.
    public void SetRadius(float radius)
    {
        LineRenderer ringLine = GetLine();

        // Place the points evenly around a circle in the X/Z (ground) plane.
        for (int i = 0; i < pointCount; i++)
        {
            float angle = i * 2f * Mathf.PI / pointCount;
            float x = Mathf.Cos(angle) * radius;
            float z = Mathf.Sin(angle) * radius;
            ringLine.SetPosition(i, new Vector3(x, 0f, z));
        }
    }

    // Called when its zombie, barrel or explosion blows up. The owner is about to
    // be destroyed, so the ring lets go of it first (otherwise the ring would be destroyed along with it).
    public void PlayBlastEffect()
    {
        transform.SetParent(null);
        isBlasting = true;
        blastTimer = 0f;
    }

    private LineRenderer GetLine()
    {
        if (line == null)
        {
            line = GetComponent<LineRenderer>();
        }
        return line;
    }

    private void Update()
    {
        if (!isBlasting)
        {
            return;
        }

        blastTimer += Time.deltaTime;
        float progress = Mathf.Clamp01(blastTimer / blastDuration); // goes from 0 to 1
        float scale = Mathf.Lerp(1f, blastScale, progress);
        transform.localScale = new Vector3(scale, scale, scale);

        if (progress >= 1f)
        {
            Destroy(gameObject);
        }
    }
}
