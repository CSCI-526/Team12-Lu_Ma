// BlastRing.cs
// ---------------------------------------------------------------------------
// Draws a zombie's blast radius as a circle on the ground with a LineRenderer.
// The circle's radius is EXACTLY the zombie's kill radius (Zombie.BlastRadius).
//   - orange ring     = long word (8+ letters), big blast
//   - light blue ring = shorter word
// The ring is a child of the zombie, so it moves with it.
//
// When the zombie explodes, PlayBlastEffect() detaches the ring, scales it up
// to about 1.5x over 0.3 seconds, then destroys it.
//
// The two materials use the URP "Unlit" shader so they render correctly in this
// project's render pipeline (a wrong shader would show up as bright magenta).
// ---------------------------------------------------------------------------
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class BlastRing : MonoBehaviour
{
    [Header("Ring shape")]
    [SerializeField] private int pointCount = 48;        // points around the circle
    [SerializeField] private float lineWidth = 0.08f;
    [SerializeField] private float heightAboveGround = 0.03f; // just above the floor so it does not flicker

    [Header("Blast effect")]
    [SerializeField] private float blastScale = 1.5f;    // how much the ring grows
    [SerializeField] private float blastDuration = 0.3f; // seconds

    [Header("References (wired by the scene builder)")]
    [SerializeField] private Material longWordMaterial;  // orange
    [SerializeField] private Material shortWordMaterial; // light blue

    private bool isBlasting;
    private float blastTimer;

    // Draws the circle. Called once by Zombie.Setup().
    public void Show(float radius, bool isLongWord)
    {
        LineRenderer line = GetComponent<LineRenderer>();
        line.useWorldSpace = false; // points are relative to this object, so the ring follows the zombie
        line.loop = true;           // connect the last point back to the first
        line.startWidth = lineWidth;
        line.endWidth = lineWidth;
        line.positionCount = pointCount;

        if (isLongWord)
        {
            line.sharedMaterial = longWordMaterial;
        }
        else
        {
            line.sharedMaterial = shortWordMaterial;
        }

        // Lie flat, just above the ground, centred under the zombie.
        transform.localPosition = new Vector3(0f, heightAboveGround, 0f);

        // Place the points evenly around a circle in the X/Z (ground) plane.
        for (int i = 0; i < pointCount; i++)
        {
            float angle = i * 2f * Mathf.PI / pointCount;
            float x = Mathf.Cos(angle) * radius;
            float z = Mathf.Sin(angle) * radius;
            line.SetPosition(i, new Vector3(x, 0f, z));
        }
    }

    // Called by Zombie.Explode(). The zombie is about to be destroyed, so the ring
    // lets go of it first (otherwise the ring would be destroyed along with it).
    public void PlayBlastEffect()
    {
        transform.SetParent(null);
        isBlasting = true;
        blastTimer = 0f;
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
