// Level.cs
// ---------------------------------------------------------------------------
// The level: where the player starts and the fights, in the order they are
// played. Lives on the "Level" object in the scene; WaveSpawner reads it.
//
// THE MAP is the real USC School of Cinematic Arts block in Los Angeles, the
// whole block between W 34th St (north), Watt Way (east), W 35th St (south)
// and McClintock Ave (west). It is built from plain boxes placed in the scene
// under "Map" (one group per building: SCI, SCB, SCC, SCA and its courtyard,
// SCX, SCE, the park, the ground and the buildings across the streets).
// Only the layout is copied (footprints, heights, openings); there is no
// detail, no text and no colour on purpose (greybox). Footprints came from
// OpenStreetMap and the LA County roof outlines, the SCA courtyard from the
// USC ground-floor plan. One metre in the game is one metre on the block.
// The map's objects are marked Static; their colours are the materials in
// Assets/Materials/Map.
//
// THE FIGHTS are under "Level > Fights" (see Encounter.cs), in this order:
//   1. W 34th St at McClintock: SCI's front door, the SCI|SCB palm walk
//   2. The Cinematic Arts Park, seen from the Muybridge plaza
//   3. The lane between the SCC stages and the Spielberg building
//   4. Watt Way at the Lucas building's east portico
//   5. Boss: the SCA courtyard, entered through the Main Gates
//
// HOW TO CHANGE IT: move things in the Scene view. Zombies walk in straight
// lines from a spawn point to its Exit, then straight at the player, so keep
// those lines (red in the Scene view) free of buildings. Keep every spawn
// Exit, barrel and crate within about 33 degrees of the direction the player
// faces at the stop (doors within about 40): the screen edge is only about
// 45 degrees to each side, and the view turns up to 12 degrees toward the
// word being typed.
//
// The only thing built by code (when the game starts) is the route guide: faint
// arrows on the ground along the route points (move a point and they follow).
// In the Scene view the run is drawn as a cyan line.
// ---------------------------------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-200)] // ready before WaveSpawner starts (it reads Encounters in its Awake)
public class Level : MonoBehaviour
{
    [SerializeField] private Transform start;        // where the player stands before the first run; blue arrow = facing
    [SerializeField] private Encounter[] encounters; // the fights, in the order they are played

    // The fights, in the order they are played. Filled in Awake.
    public List<Encounter> Encounters { get; private set; }

    // Where the player stands (on the ground) and faces before the first run.
    public Vector3 StartPosition
    {
        get { return start.position; }
    }

    public Vector3 StartFacing
    {
        get
        {
            Vector3 forward = start.forward;
            forward.y = 0f;
            return forward.normalized;
        }
    }

    private void Awake()
    {
        Encounters = new List<Encounter>();
        foreach (Encounter encounter in encounters)
        {
            if (encounter != null)
            {
                Encounters.Add(encounter);
            }
        }

        // Faint arrows on the ground along the whole run: from the start through
        // every fight's route to its stop.
        Transform guide = new GameObject("Route guide").transform;
        guide.SetParent(transform, true); // true: stays at the world's origin, so the arrows sit on the route points
        Vector3 from = StartPosition;
        foreach (Encounter encounter in Encounters)
        {
            List<Vector3> points = encounter.Route;
            points.Insert(0, from);
            PaintArrows(points, guide);
            from = points[points.Count - 1];
        }
    }

    // ---- Route guide ----
    // Small flat chevrons (">") painted on the paving every GuideSpacing metres,
    // pointing the way the player runs: they show the route like the old rails
    // did, but lie flat, in a soft sand colour, and cast no shadow.

    private const float GuideSpacing = 3.2f;     // metres between two arrows
    private const float GuideEndGap = 2f;        // no arrow this close to a stop (or the start)
    private const float GuideArmLength = 0.55f;
    private const float GuideArmWidth = 0.09f;
    private const float GuideArmAngle = 38f;     // degrees each arm is turned from the running direction
    private const float GuideHeight = 0.025f;    // just above the paving

    private static void PaintArrows(List<Vector3> route, Transform parent)
    {
        Material material = Palette.Lit(Palette.RouteGuide);

        // Walk the route, dropping an arrow every GuideSpacing metres (the
        // spacing carries on around corners, so the arrows stay evenly spaced).
        float total = 0f;
        for (int i = 0; i + 1 < route.Count; i++)
        {
            total += Flat(route[i + 1] - route[i]).magnitude;
        }

        float travelled = 0f;
        float nextArrow = GuideEndGap;
        for (int i = 0; i + 1 < route.Count; i++)
        {
            Vector3 along = Flat(route[i + 1] - route[i]);
            float length = along.magnitude;
            if (length < 0.1f)
            {
                continue;
            }
            Vector3 direction = along / length;
            while (nextArrow <= travelled + length && nextArrow <= total - GuideEndGap)
            {
                Vector3 at = route[i] + direction * (nextArrow - travelled);
                PaintArrow(at, direction, material, parent);
                nextArrow += GuideSpacing;
            }
            travelled += length;
        }
    }

    // One ">" pointing along direction: two thin flat bars meeting at the tip.
    private static void PaintArrow(Vector3 tip, Vector3 direction, Material material, Transform parent)
    {
        float yaw = Quaternion.LookRotation(direction).eulerAngles.y;
        for (int side = -1; side <= 1; side += 2)
        {
            float armYaw = yaw + 180f + side * GuideArmAngle; // the arm reaches back from the tip
            Vector3 back = Quaternion.Euler(0f, armYaw, 0f) * Vector3.forward;
            Vector3 middle = tip + back * (GuideArmLength * 0.5f) + Vector3.up * GuideHeight;
            GameObject arm = Shapes.Block(PrimitiveType.Cube, "Route arrow", parent, middle,
                new Vector3(GuideArmWidth, 0.01f, GuideArmLength), material, new Vector3(0f, armYaw, 0f));
            arm.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }

    private static Vector3 Flat(Vector3 vector)
    {
        vector.y = 0f;
        return vector;
    }

    // Only drawn in the Editor's Scene view, never in the game: the whole run
    // in cyan, from the start (with an arrow where the player first faces)
    // through every fight's route points to its stop.
    private void OnDrawGizmos()
    {
        if (start == null || encounters == null)
        {
            return;
        }
        Vector3 up = Vector3.up * 0.2f;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(start.position + up, start.position + up + StartFacing * 4f);

        Vector3 from = start.position;
        foreach (Encounter encounter in encounters)
        {
            if (encounter == null)
            {
                continue;
            }
            foreach (Vector3 point in encounter.Route)
            {
                Gizmos.DrawLine(from + up, point + up);
                from = point;
            }
        }
    }
}
