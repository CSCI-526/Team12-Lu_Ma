// RailMover.cs
// ---------------------------------------------------------------------------
// Moves the Player rig through the level like a camera on rails (Typing of the
// Dead style). There is no mouse look and no WASD: the player's hands stay on
// the keyboard. Lives on the "Player" object; the Main Camera is its child, so
// the camera rides along (CameraDirector adds the head movement on top).
//
//   RideAlong(route, facing) - ride through the route's points (speeding up and
//                              slowing down smoothly, turning at corners), stop
//                              at the last point and turn to face "facing".
//   IsStopped                - true once it has arrived AND turned: time to fight.
//
// WaveSpawner decides where to ride (from the Level's encounters).
// ---------------------------------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

public class RailMover : MonoBehaviour
{
    [Header("Tuning")]
    [SerializeField] private float railSpeed = 9f;           // metres per second between fights (a run)
    [SerializeField] private float accelerationSeconds = 0.8f; // time to reach full speed (and to stop)
    [SerializeField] private float turnSpeed = 160f;         // degrees per second the body turns
    [SerializeField] private float cornerLookAhead = 5f;     // start turning this many metres before a corner

    private readonly List<Vector3> route = new List<Vector3>();
    private int nextPoint;          // index in route of the point we are heading to
    private Vector3 finalFacing = Vector3.forward;
    private float currentSpeed;
    private bool riding;

    // True while the player is moving between two fights.
    public bool IsRiding
    {
        get { return riding; }
    }

    // True once the last ride is over and the body faces the fight.
    public bool IsStopped
    {
        get { return !riding && Vector3.Angle(transform.forward, finalFacing) < 1f; }
    }

    // The flat direction the player's body faces.
    public Vector3 Facing
    {
        get { return transform.forward; }
    }

    // Puts the player somewhere right away (the start of the level).
    public void PlaceAt(Vector3 position, Vector3 facing)
    {
        transform.position = position;
        finalFacing = Flat(facing);
        transform.rotation = Quaternion.LookRotation(finalFacing);
        riding = false;
        currentSpeed = 0f;
    }

    // Starts riding through points; at the end, turns to face facingAtEnd.
    public void RideAlong(List<Vector3> points, Vector3 facingAtEnd)
    {
        route.Clear();
        route.AddRange(points);
        finalFacing = Flat(facingAtEnd);
        nextPoint = 0;

        // Skip a first point we are already standing on.
        while (nextPoint < route.Count && FlatDistance(transform.position, route[nextPoint]) < 0.05f)
        {
            nextPoint += 1;
        }
        riding = nextPoint < route.Count;
    }

    private void Update()
    {
        // Only ride while the game is being played.
        if (GameManager.Instance.State != GameState.Playing)
        {
            return;
        }

        if (riding)
        {
            Ride();
        }

        // Turn the body: toward the road ahead while riding, toward the fight when stopped.
        Vector3 wanted = riding ? RideDirection() : finalFacing;
        if (wanted.sqrMagnitude > 0.001f)
        {
            Quaternion target = Quaternion.LookRotation(wanted);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * Time.deltaTime);
        }
    }

    private void Ride()
    {
        // Speed up at the start and slow down so that we stop exactly at the end:
        // the speed from which we can still brake in the distance left is
        // sqrt(2 x deceleration x distance).
        float acceleration = railSpeed / Mathf.Max(0.05f, accelerationSeconds);
        float brakingSpeed = Mathf.Sqrt(2f * acceleration * DistanceLeft());
        float wantedSpeed = Mathf.Min(railSpeed, brakingSpeed);
        currentSpeed = Mathf.MoveTowards(currentSpeed, wantedSpeed, acceleration * Time.deltaTime);
        currentSpeed = Mathf.Max(currentSpeed, 0.25f); // never crawl forever right before the end

        // Move along the route, possibly passing several points in one frame.
        float step = currentSpeed * Time.deltaTime;
        while (step > 0f && nextPoint < route.Count)
        {
            Vector3 target = route[nextPoint];
            target.y = transform.position.y;
            float distance = Vector3.Distance(transform.position, target);
            if (distance <= step)
            {
                transform.position = target;
                step -= distance;
                nextPoint += 1;
            }
            else
            {
                transform.position = Vector3.MoveTowards(transform.position, target, step);
                step = 0f;
            }
        }

        if (nextPoint >= route.Count)
        {
            riding = false;
            currentSpeed = 0f;
        }
    }

    // Distance still to ride, along the route.
    private float DistanceLeft()
    {
        float total = 0f;
        Vector3 from = transform.position;
        for (int i = nextPoint; i < route.Count; i++)
        {
            total += FlatDistance(from, route[i]);
            from = route[i];
        }
        return total;
    }

    // Where to look while riding: toward the next point, and, close to a corner,
    // already toward the point after it, so the view turns smoothly.
    private Vector3 RideDirection()
    {
        if (nextPoint >= route.Count)
        {
            return finalFacing;
        }

        Vector3 toNext = Flat(route[nextPoint] - transform.position);
        float distance = FlatDistance(transform.position, route[nextPoint]);
        if (nextPoint + 1 < route.Count && distance < cornerLookAhead)
        {
            Vector3 after = Flat(route[nextPoint + 1] - route[nextPoint]);
            float blend = 1f - distance / cornerLookAhead;
            return Vector3.Slerp(toNext, after, blend);
        }
        if (nextPoint + 1 >= route.Count && distance < cornerLookAhead)
        {
            // Arriving: start turning toward the fight.
            float blend = 1f - distance / cornerLookAhead;
            return Vector3.Slerp(toNext, finalFacing, blend);
        }
        return toNext;
    }

    // The direction on the ground (y = 0), normalised.
    private static Vector3 Flat(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
        {
            return Vector3.forward;
        }
        return direction.normalized;
    }

    private static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }
}
