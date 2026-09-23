// Encounter.cs
// ---------------------------------------------------------------------------
// One fight in the level: a place where the player stops and zombies come at
// them (Typing of the Dead style). Level.cs creates one Encounter per area
// (street, alley, warehouse, yard, lab) and WaveSpawner plays them in order:
//
//   1. ride along Route (the last point is where the player stops),
//   2. turn to face Facing,
//   3. fight: zombies come out of the SpawnPoints (or the boss appears),
//      the area's Barrels and Crates can be typed,
//   4. when every zombie is dead, open ExitGate and ride on to the next one.
//
// This is plain data (no MonoBehaviour): Level fills in the fields.
// ---------------------------------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

public class Encounter
{
    // Ride through these points (world positions on the ground). The LAST one is
    // where the player stops to fight.
    public List<Vector3> Route = new List<Vector3>();

    // The direction the player faces while fighting here (flat: y = 0).
    public Vector3 Facing = Vector3.forward;

    // Where this area's zombies come from.
    public List<SpawnPoint> SpawnPoints = new List<SpawnPoint>();

    // How many zombies come out of one spawn point together (a small pack).
    public int GroupSize = 1;

    // Explosive barrels and supply crates of this area. They can only be typed
    // while this encounter is being fought.
    public List<Barrel> Barrels = new List<Barrel>();
    public List<SupplyCrate> Crates = new List<SupplyCrate>();

    // Opened when the area is cleared; the next Route passes through it (null = none).
    public Gate ExitGate;

    // ---- Boss fight (only when IsBossFight is true) ----
    public bool IsBossFight;
    public Vector3 BossStand;   // where the boss stands and fights, in front of the player
}
