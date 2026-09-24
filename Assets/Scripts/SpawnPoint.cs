// SpawnPoint.cs
// ---------------------------------------------------------------------------
// A place zombies come out of: a doorway, a side street, the far end of a
// walk or an arcade... A zombie appears at Position (out of sight), walks
// to Exit (just outside the doorway), then heads straight for the player.
// Make sure the straight line from Exit to the player's stop is free of
// walls, because zombies walk straight (they do not path-find).
// ---------------------------------------------------------------------------
using UnityEngine;

public class SpawnPoint
{
    public Vector3 Position;   // where zombies appear (on the ground)
    public Vector3 Exit;       // they walk here first, then straight at the player
    public Door Door;          // bursts open when the first zombie comes out (null = no door)
    public float Spread;       // random sideways offset (metres) so a pack does not stack up

    public SpawnPoint(Vector3 position, Vector3 exit, Door door = null, float spread = 1f)
    {
        Position = position;
        Exit = exit;
        Door = door;
        Spread = spread;
    }
}
