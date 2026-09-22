// RailMover.cs
// ---------------------------------------------------------------------------
// Moves the Player rig forward along +Z at a constant speed, like a cart on a
// rail. There is no mouse look and no WASD: the player's hands stay on the
// keyboard for typing. Lives on the "Player" object (the Main Camera is a
// child of it, so the camera rides along).
// ---------------------------------------------------------------------------
using UnityEngine;

public class RailMover : MonoBehaviour
{
    [Header("Tuning")]
    // Metres per second. Keep this at or below the zombie speed (WaveSpawner.zombieSpeed).
    // If the rail is much faster than the zombies, the player rides past zombies near
    // the walls before they can reach the middle, and those zombies are simply removed.
    [SerializeField] private float railSpeed = 1.5f;

    private void Update()
    {
        // Only ride the rail while the game is being played.
        if (GameManager.Instance.State != GameState.Playing)
        {
            return;
        }

        transform.position += Vector3.forward * railSpeed * Time.deltaTime;
    }
}
