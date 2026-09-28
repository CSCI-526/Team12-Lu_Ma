// Door.cs
// ---------------------------------------------------------------------------
// A door that zombies burst through: a plain panel on a hinge, with a dark
// box behind it (the doorway). It is a prefab (Assets/Prefabs/Door): place
// it on a building's face with its blue arrow (forward) pointing out of the
// building, the way it swings open. Put it under a SpawnPoint and drag it
// into that spawn point's Door field.
//
// WaveSpawner calls BurstOpen() when the first zombie comes out of it: the
// door swings open fast and the camera shakes a little.
// ---------------------------------------------------------------------------
using System.Collections;
using UnityEngine;

public class Door : MonoBehaviour
{
    private const float OpenAngle = 100f;      // degrees
    private const float OpenSeconds = 0.18f;
    private const float Shake = 0.15f;         // camera shake when right next to it (0..1)
    private const float ShakeRange = 35f;      // no shake beyond this distance (metres)

    [SerializeField] private Transform hinge;  // the panel turns around this point (its left edge)

    public bool IsOpen { get; private set; }

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
