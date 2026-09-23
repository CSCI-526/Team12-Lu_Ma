// Door.cs
// ---------------------------------------------------------------------------
// A door that zombies burst through: a plain box on a hinge, with a dark box
// behind it (the doorway). Level.cs builds each door with:
//
//   Door.Create(parent, position, outward);
//
//   position = bottom centre of the doorway, on the ground, in the wall face.
//   outward  = the direction the door opens toward (usually toward the player).
//
// WaveSpawner calls BurstOpen() when the first zombie comes out of it: the
// door swings open fast and the camera shakes a little.
// ---------------------------------------------------------------------------
using System.Collections;
using UnityEngine;

public class Door : MonoBehaviour
{
    private const float Width = 1.6f;          // metres (Level makes its wall openings this size too)
    private const float Height = 2.6f;
    private const float Thickness = 0.12f;
    private const float OpenAngle = 100f;      // degrees
    private const float OpenSeconds = 0.18f;
    private const float Shake = 0.15f;         // camera shake when right next to it (0..1)
    private const float ShakeRange = 35f;      // no shake beyond this distance (metres)

    public bool IsOpen { get; private set; }

    private Transform hinge;                   // the panel turns around this point

    // Builds a closed door. See the comment at the top of the file.
    public static Door Create(Transform parent, Vector3 position, Vector3 outward)
    {
        GameObject root = new GameObject("Door");
        root.transform.SetParent(parent, false);
        root.transform.position = position;

        // The door's own space: x = across the doorway, z = outward.
        Vector3 flat = new Vector3(outward.x, 0f, outward.z);
        root.transform.rotation = Quaternion.LookRotation(flat.sqrMagnitude > 0.001f ? flat.normalized : Vector3.forward);

        Door door = root.AddComponent<Door>();
        door.Build();
        return door;
    }

    private void Build()
    {
        // The dark doorway, just behind the panel.
        Shapes.Block(PrimitiveType.Cube, "Doorway", transform, new Vector3(0f, Height * 0.5f, -Thickness),
            new Vector3(Width, Height, 0.1f), Palette.Lit(Palette.Doorway));

        // The panel hangs on a hinge at its left edge.
        hinge = new GameObject("Hinge").transform;
        hinge.SetParent(transform, false);
        hinge.localPosition = new Vector3(-Width * 0.5f, 0f, 0f);
        Shapes.Block(PrimitiveType.Cube, "Panel", hinge, new Vector3(Width * 0.5f, Height * 0.5f, 0f),
            new Vector3(Width, Height, Thickness), Palette.Lit(Palette.Door));
    }

    // Slams the door open. Does nothing if it is already open.
    public void BurstOpen()
    {
        if (IsOpen)
        {
            return;
        }
        IsOpen = true;

        Camera view = Camera.main;
        if (view != null)
        {
            float distance = Vector3.Distance(view.transform.position, transform.position);
            float strength = Shake * Mathf.Clamp01(1f - distance / ShakeRange);
            if (strength > 0.01f)
            {
                CameraDirector.Shake(strength);
            }
        }
        StartCoroutine(Swing());
    }

    private IEnumerator Swing()
    {
        for (float time = 0f; time < OpenSeconds; time += Time.deltaTime)
        {
            float angle = Mathf.Lerp(0f, OpenAngle, time / OpenSeconds);
            hinge.localRotation = Quaternion.Euler(0f, -angle, 0f); // swings outward
            yield return null;
        }
        hinge.localRotation = Quaternion.Euler(0f, -OpenAngle, 0f);
    }
}
