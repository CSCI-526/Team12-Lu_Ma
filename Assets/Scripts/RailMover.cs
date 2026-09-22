// RailMover.cs
// ---------------------------------------------------------------------------
// Moves the Player rig forward along +Z at a constant speed, like a cart on a
// rail. There is no mouse look and no WASD: the player's hands stay on the
// keyboard for typing. Lives on the "Player" object (the Main Camera is a
// child of it, so the camera rides along).
//
// For a boss fight WaveSpawner calls Brake(): the cart slows down smoothly and
// stops. Release() speeds it back up to railSpeed afterwards.
// ---------------------------------------------------------------------------
using UnityEngine;

public class RailMover : MonoBehaviour
{
    [Header("Tuning")]
    // Metres per second. Keep this at or below the zombie speed (WaveSpawner.zombieSpeed).
    // If the rail is much faster than the zombies, the player rides past zombies near
    // the walls before they can reach the middle, and those zombies are simply removed.
    [SerializeField] private float railSpeed = 1.5f;
    [SerializeField] private float brakeSeconds = 1.5f; // time to slow from railSpeed to a stop (and to speed back up)

    private float currentSpeed;
    private bool braking;

    // True once the cart has fully stopped after Brake().
    public bool IsStopped
    {
        get { return braking && currentSpeed <= 0f; }
    }

    private void Start()
    {
        currentSpeed = railSpeed;
    }

    // Slow down smoothly and stop (boss fight).
    public void Brake()
    {
        braking = true;
    }

    // Speed back up to railSpeed (boss defeated).
    public void Release()
    {
        braking = false;
    }

    private void Update()
    {
        // Only ride the rail while the game is being played.
        if (GameManager.Instance.State != GameState.Playing)
        {
            return;
        }

        float targetSpeed = braking ? 0f : railSpeed;
        float acceleration = brakeSeconds > 0f ? railSpeed / brakeSeconds : float.MaxValue;
        currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, acceleration * Time.deltaTime);

        transform.position += Vector3.forward * currentSpeed * Time.deltaTime;
    }
}
