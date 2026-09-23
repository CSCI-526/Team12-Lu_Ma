// Gate.cs
// ---------------------------------------------------------------------------
// A gate between two areas: a plain box that blocks the way until the area is
// cleared. Then WaveSpawner calls Open() and it sinks into the floor, and the
// player rides over it. Level.cs builds each gate with:
//
//   Gate.Create(parent, position, rideDirection, width, height);
//
//   position = bottom centre of the opening (on the ground).
// ---------------------------------------------------------------------------
using System.Collections;
using UnityEngine;

public class Gate : MonoBehaviour
{
    private const float Thickness = 0.3f;     // metres
    private const float OpenSeconds = 1.5f;   // time to sink all the way down

    public bool IsOpen { get; private set; }

    private Transform panel;
    private float height;

    // Builds a closed gate. See the comment at the top of the file.
    public static Gate Create(Transform parent, Vector3 position, Vector3 rideDirection, float width, float height)
    {
        GameObject root = new GameObject("Gate");
        root.transform.SetParent(parent, false);
        root.transform.position = position;

        // The gate's own space: x = across the opening, z = the ride direction.
        Vector3 flat = new Vector3(rideDirection.x, 0f, rideDirection.z);
        root.transform.rotation = Quaternion.LookRotation(flat.sqrMagnitude > 0.001f ? flat.normalized : Vector3.forward);

        Gate gate = root.AddComponent<Gate>();
        gate.height = height;
        // Keeps its collider while closed, so thrown bodies bounce off it.
        gate.panel = Shapes.Block(PrimitiveType.Cube, "Panel", root.transform, new Vector3(0f, height * 0.5f, 0f),
            new Vector3(width, height, Thickness), Palette.Lit(Palette.Gate), true).transform;
        return gate;
    }

    // Sinks the gate into the floor. Does nothing if it is already open.
    public void Open()
    {
        if (IsOpen)
        {
            return;
        }
        IsOpen = true;
        panel.GetComponent<Collider>().enabled = false;
        StartCoroutine(Sink());
    }

    private IEnumerator Sink()
    {
        Vector3 closed = panel.localPosition;
        Vector3 open = closed + Vector3.down * (height + 0.1f); // fully below the floor
        for (float time = 0f; time < OpenSeconds; time += Time.deltaTime)
        {
            panel.localPosition = Vector3.Lerp(closed, open, time / OpenSeconds);
            yield return null;
        }
        panel.gameObject.SetActive(false);
    }
}
