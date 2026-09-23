// Level.cs
// ---------------------------------------------------------------------------
// Builds the whole greybox map out of plain boxes when the scene starts
// (Awake), and describes the fights in it as a list of Encounters that
// WaveSpawner plays in order. Lives on the "Level" object in the scene.
//
// The map is five areas, one after the other along the ride:
//   1. Main street  (0, 0, -8)  -> stop at (0, 0, 4), facing +Z
//   2. Alley        turn right  -> stop at (14, 0, 52), facing +X
//   3. Warehouse    turn left   -> stop at (60, 0, 62), facing +Z
//   4. Yard                     -> stop at (73, 0, 98), facing +Z
//   5. Lab (boss)               -> stop at (73, 0, 134), facing +Z
// Two thin rails run along the whole ride (like the first prototype).
//
// Every area is made of: a floor, walls (with door openings), a few plain
// blocks, the doors zombies burst out of, the barrels and supply crates, and a
// gate that opens when the area is cleared.
//
// HOW TO CHANGE IT: each area has its own method below (BuildMainStreet,
// BuildAlley, ...). Positions are in metres; y = 0 is the ground; the player
// rides at ground level. Zombies walk in STRAIGHT lines from their spawn
// point's Exit to the player's stop, so keep those lines free of walls.
// Keep every spawn Exit, barrel and crate within about 33 degrees of the
// stop's Facing (doors within about 40). The screen edge is only about 45
// degrees to each side, and the view turns up to 12 degrees toward the word
// being typed. A zombie walks straight at the player, so one that starts
// outside the view never comes into it.
// ---------------------------------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-200)] // build the map before any other script starts
public class Level : MonoBehaviour
{
    private const float WallThickness = 0.5f;
    private const float DoorGapWidth = 1.6f;    // metres: the opening a door sits in (as wide as a Door)
    private const float DoorGapHeight = 2.6f;   // the wall continues above the opening (as tall as a Door)
    private const float RailGap = 0.75f;        // each rail is this far from the middle of the ride

    // The fights, in the order they are played. Filled in Awake.
    public List<Encounter> Encounters { get; private set; }

    // Where the player stands (on the ground) and faces before the first ride.
    public Vector3 StartPosition { get; private set; }
    public Vector3 StartFacing { get; private set; }

    private Transform area; // the area being built: new objects go under it

    private void Awake()
    {
        Encounters = new List<Encounter>();
        StartPosition = new Vector3(0f, 0f, -8f);
        StartFacing = Vector3.forward;

        BuildMainStreet();
        BuildAlley();
        BuildWarehouse();
        BuildYard();
        BuildLab();

        foreach (Encounter encounter in Encounters)
        {
            BuildRails(encounter.Route);
        }
    }

    // ---- 1. Main street ----
    private void BuildMainStreet()
    {
        StartArea("Area 1 - Main Street");
        Floor(-10f, 10f, -14f, 58f);

        // Buildings on the left (x -18..-10) and on the right (x 10..18).
        Building(-18f, -10f, -14f, 10f, 8f);
        Building(-18f, -10f, 10f, 22f, 10f);
        Building(-18f, -10f, 22f, 30f, 7f);   // has a door at z 26
        Building(-18f, -10f, 30f, 44f, 11f);
        Building(-18f, -10f, 44f, 58f, 8f);
        Building(10f, 18f, -14f, 14f, 9f);
        Building(10f, 18f, 14f, 20f, 7f);     // has a door at z 17
        Building(10f, 18f, 20f, 33f, 10f);
        Building(10f, 18f, 39f, 49.5f, 8f);   // (the side street is between z 33 and 39)
        Building(10f, 18f, 54.5f, 58f, 9f);   // (the alley starts between z 49.5 and 54.5)
        Floor(10f, 20f, 33f, 39f);            // side street
        WallZ(20f, 33f, 39f, 6f);
        WallX(58.25f, -18f, 18f, 8f);         // the far end of the street

        // A few plain blocks to hide behind (not in the zombies' way).
        Block(new Vector3(-6f, 0f, 10f), new Vector3(2f, 1.4f, 4f));
        Block(new Vector3(-5f, 0f, 40f), new Vector3(2f, 1.4f, 4f));
        Block(new Vector3(6f, 0f, 45f), new Vector3(2f, 1.4f, 4f));

        Door leftDoor = DoorOnFace(new Vector3(-10f, 0f, 26f), Vector3.right);
        Door rightDoor = DoorOnFace(new Vector3(10f, 0f, 17f), Vector3.left);

        Encounter encounter = new Encounter();
        encounter.Route.Add(new Vector3(0f, 0f, -8f));
        encounter.Route.Add(new Vector3(0f, 0f, 4f));
        encounter.Facing = Vector3.forward;
        encounter.SpawnPoints.Add(new SpawnPoint(new Vector3(0f, 0f, 48f), new Vector3(0f, 0f, 44f), null, 4f));
        encounter.SpawnPoints.Add(new SpawnPoint(new Vector3(-10.5f, 0f, 26f), new Vector3(-7f, 0f, 25f), leftDoor, 0.4f));
        encounter.SpawnPoints.Add(new SpawnPoint(new Vector3(16f, 0f, 36f), new Vector3(8f, 0f, 35f), null, 1.5f));
        encounter.SpawnPoints.Add(new SpawnPoint(new Vector3(10.5f, 0f, 17f), new Vector3(7f, 0f, 17f), rightDoor, 0.4f));
        encounter.GroupSize = 1;
        encounter.Barrels.Add(Barrel.Create(new Vector3(3.5f, 0f, 34f), area));
        encounter.ExitGate = null; // the alley is open
        Encounters.Add(encounter);
    }

    // ---- 2. Alley (runs along +X) ----
    private void BuildAlley()
    {
        StartArea("Area 2 - Alley");
        Floor(10f, 52f, 49f, 55f);             // (reaches under both walls)
        WallX(55f, 18f, 52f, 8f, 30f);    // north wall, door at x 30
        WallX(49f, 18f, 52f, 8f, 38f);    // south wall, door at x 38
        Door northDoor = DoorInWallX(30f, 55f, -1f);
        Door southDoor = DoorInWallX(38f, 49f, 1f);
        Gate gate = Gate.Create(area, new Vector3(50f, 0f, 52f), Vector3.right, 5.5f, 4.5f);

        // Behind the gate: a small yard leading to the warehouse door.
        Floor(52f, 64f, 48.5f, 56f);
        WallX(48.25f, 52f, 64f, 5f);
        WallZ(64.25f, 48.5f, 56f, 5f);

        Encounter encounter = new Encounter();
        encounter.Route.Add(new Vector3(0f, 0f, 4f));
        encounter.Route.Add(new Vector3(0f, 0f, 52f));
        encounter.Route.Add(new Vector3(14f, 0f, 52f));
        encounter.Facing = Vector3.right;
        encounter.SpawnPoints.Add(new SpawnPoint(new Vector3(47f, 0f, 52f), new Vector3(44f, 0f, 52f), null, 1.2f));
        encounter.SpawnPoints.Add(new SpawnPoint(new Vector3(30f, 0f, 55.6f), new Vector3(30f, 0f, 53.3f), northDoor, 0.4f));
        encounter.SpawnPoints.Add(new SpawnPoint(new Vector3(38f, 0f, 48.4f), new Vector3(38f, 0f, 50.7f), southDoor, 0.4f));
        encounter.GroupSize = 2;
        encounter.Barrels.Add(Barrel.Create(new Vector3(24f, 0f, 50.2f), area));
        encounter.Barrels.Add(Barrel.Create(new Vector3(34f, 0f, 53.6f), area));
        encounter.Crates.Add(SupplyCrate.Create(new Vector3(20f, 0f, 53.9f), SupplyKind.Freeze, area));
        encounter.ExitGate = gate;
        Encounters.Add(encounter);
    }

    // ---- 3. Warehouse (x 50..80, z 56..90) ----
    private void BuildWarehouse()
    {
        StartArea("Area 3 - Warehouse");
        Floor(50f, 80f, 56f, 90f);
        WallX(56f, 50f, 57f, 7f);             // south wall, left of the big opening
        WallX(56f, 63f, 80f, 7f);             // south wall, right of it (the ride comes in at x 57..63)
        WallZ(50f, 56f, 90f, 7f, 82f);        // west wall, door at z 82
        WallZ(80f, 56f, 90f, 7f, 88f);        // east wall, door at z 88
        WallX(90f, 50f, 70f, 7f, 60.5f, 66f); // north wall, doors at x 60.5 and 66...
        WallX(90f, 76f, 80f, 7f);             // ...and the exit gate between x 70 and 76
        Door eastDoor = DoorInWallZ(80f, 88f, -1f);
        Door westDoor = DoorInWallZ(50f, 82f, 1f);
        Door northDoorA = DoorInWallX(60.5f, 90f, -1f);
        Door northDoorB = DoorInWallX(66f, 90f, -1f);
        Gate gate = Gate.Create(area, new Vector3(73f, 0f, 90f), Vector3.forward, 6f, 4.5f);

        // Shelves along the side walls.
        Block(new Vector3(52f, 0f, 67f), new Vector3(2f, 3f, 14f));
        Block(new Vector3(78f, 0f, 65f), new Vector3(2f, 3f, 14f));

        Encounter encounter = new Encounter();
        encounter.Route.Add(new Vector3(14f, 0f, 52f));
        encounter.Route.Add(new Vector3(56f, 0f, 52f));
        encounter.Route.Add(new Vector3(60f, 0f, 52f));
        encounter.Route.Add(new Vector3(60f, 0f, 62f));
        encounter.Facing = Vector3.forward;
        encounter.SpawnPoints.Add(new SpawnPoint(new Vector3(60.5f, 0f, 91f), new Vector3(60.5f, 0f, 87f), northDoorA, 0.4f));
        encounter.SpawnPoints.Add(new SpawnPoint(new Vector3(66f, 0f, 91f), new Vector3(66f, 0f, 87f), northDoorB, 0.4f));
        encounter.SpawnPoints.Add(new SpawnPoint(new Vector3(81f, 0f, 88f), new Vector3(76f, 0f, 88f), eastDoor, 0.4f));
        encounter.SpawnPoints.Add(new SpawnPoint(new Vector3(49f, 0f, 82f), new Vector3(53f, 0f, 82f), westDoor, 0.4f));
        encounter.GroupSize = 3;
        encounter.Barrels.Add(Barrel.Create(new Vector3(59f, 0f, 77f), area));
        encounter.Barrels.Add(Barrel.Create(new Vector3(62.5f, 0f, 82f), area));
        encounter.Barrels.Add(Barrel.Create(new Vector3(65.5f, 0f, 74f), area));
        encounter.Crates.Add(SupplyCrate.Create(new Vector3(55.5f, 0f, 70f), SupplyKind.Lure, area));
        encounter.ExitGate = gate;
        Encounters.Add(encounter);
    }

    // ---- 4. Yard (x 56..90, z 90..128) ----
    private void BuildYard()
    {
        StartArea("Area 4 - Yard");
        Floor(56f, 90f, 90f, 128f);
        WallZ(56f, 90f, 128f, 3f, 121f);      // west wall, door at z 121
        WallZ(90f, 90f, 128f, 3f, 121f);      // east wall, door at z 121
        WallX(90f, 80f, 90f, 3f);             // south wall (the warehouse closes the rest)
        WallX(128f, 56f, 70f, 8f);            // north wall = the lab's front wall...
        WallX(128f, 76f, 90f, 8f);            // ...with the lab gate between x 70 and 76
        Gate gate = Gate.Create(area, new Vector3(73f, 0f, 128f), Vector3.forward, 6f, 4.5f);

        // Blocks to hide behind, a container and a shed that zombies come out of.
        Block(new Vector3(60f, 0f, 103f), new Vector3(2.4f, 2.6f, 8f));
        Block(new Vector3(86f, 0f, 103f), new Vector3(2.4f, 2.6f, 8f));
        Block(new Vector3(84f, 0f, 123.25f), new Vector3(8f, 3.1f, 2.5f));  // container (taller than a red zombie)
        Block(new Vector3(63f, 0f, 124.5f), new Vector3(6f, 3f, 5f));       // shed
        Door containerDoor = DoorOnFace(new Vector3(84f, 0f, 122f), Vector3.back);
        Door shedDoor = DoorOnFace(new Vector3(63f, 0f, 122f), Vector3.back);
        Door westGateDoor = DoorInWallZ(56f, 121f, 1f);
        Door eastGateDoor = DoorInWallZ(90f, 121f, -1f);

        Encounter encounter = new Encounter();
        encounter.Route.Add(new Vector3(60f, 0f, 62f));
        encounter.Route.Add(new Vector3(60f, 0f, 74f));
        encounter.Route.Add(new Vector3(73f, 0f, 84f));
        encounter.Route.Add(new Vector3(73f, 0f, 98f));
        encounter.Facing = Vector3.forward;
        encounter.SpawnPoints.Add(new SpawnPoint(new Vector3(63f, 0f, 124.5f), new Vector3(63f, 0f, 120.5f), shedDoor, 0.4f));
        encounter.SpawnPoints.Add(new SpawnPoint(new Vector3(84f, 0f, 123f), new Vector3(84f, 0f, 120f), containerDoor, 0.4f));
        encounter.SpawnPoints.Add(new SpawnPoint(new Vector3(54.5f, 0f, 121f), new Vector3(59f, 0f, 121f), westGateDoor, 0.4f));
        encounter.SpawnPoints.Add(new SpawnPoint(new Vector3(91.5f, 0f, 121f), new Vector3(87f, 0f, 121f), eastGateDoor, 0.4f));
        encounter.GroupSize = 3;
        encounter.Barrels.Add(Barrel.Create(new Vector3(70f, 0f, 112f), area));
        encounter.Barrels.Add(Barrel.Create(new Vector3(79f, 0f, 114f), area));
        encounter.Barrels.Add(Barrel.Create(new Vector3(63.5f, 0f, 116f), area));
        encounter.Crates.Add(SupplyCrate.Create(new Vector3(71f, 0f, 108f), SupplyKind.Health, area));
        encounter.Crates.Add(SupplyCrate.Create(new Vector3(75f, 0f, 108f), SupplyKind.Freeze, area));
        encounter.ExitGate = gate;
        Encounters.Add(encounter);
    }

    // ---- 5. Lab: the boss (x 57..89, z 128..166) ----
    private void BuildLab()
    {
        StartArea("Area 5 - Lab");
        Floor(57f, 89f, 128f, 166f);
        WallZ(57f, 128f, 166f, 8f);
        WallZ(89f, 128f, 166f, 8f);
        WallX(166f, 57f, 89f, 8f);
        Block(new Vector3(62f, 0f, 140f), new Vector3(1.2f, 8f, 1.2f));   // pillars
        Block(new Vector3(84f, 0f, 140f), new Vector3(1.2f, 8f, 1.2f));
        Block(new Vector3(62f, 0f, 156f), new Vector3(1.2f, 8f, 1.2f));
        Block(new Vector3(84f, 0f, 156f), new Vector3(1.2f, 8f, 1.2f));

        Encounter encounter = new Encounter();
        encounter.Route.Add(new Vector3(73f, 0f, 98f));
        encounter.Route.Add(new Vector3(73f, 0f, 134f));
        encounter.Facing = Vector3.forward;
        encounter.IsBossFight = true;
        encounter.BossStand = new Vector3(73f, 0f, 150f);
        // Barrels next to the boss: blowing them up hurts it.
        encounter.Barrels.Add(Barrel.Create(new Vector3(69f, 0f, 148f), area));
        encounter.Barrels.Add(Barrel.Create(new Vector3(77f, 0f, 149f), area));
        encounter.Crates.Add(SupplyCrate.Create(new Vector3(77f, 0f, 142f), SupplyKind.Health, area));
        Encounters.Add(encounter);
    }

    // =====================================================================
    // Building helpers. Every box keeps its collider (dead zombies land on them).
    // =====================================================================

    // Starts a new area: everything built next goes under an object with this name.
    private void StartArea(string areaName)
    {
        area = new GameObject(areaName).transform;
        area.SetParent(transform, false);
    }

    // A box of any colour. bottomCentre = the middle of its bottom face.
    private GameObject Box(string boxName, Vector3 bottomCentre, Vector3 size, Color color)
    {
        Vector3 centre = bottomCentre + Vector3.up * size.y * 0.5f;
        return Shapes.Block(PrimitiveType.Cube, boxName, area, centre, size, Palette.Lit(color), true);
    }

    // A floor whose top is at y = 0, from xMin to xMax and zMin to zMax.
    private void Floor(float xMin, float xMax, float zMin, float zMax)
    {
        Box("Floor", new Vector3((xMin + xMax) * 0.5f, -0.2f, (zMin + zMax) * 0.5f),
            new Vector3(xMax - xMin, 0.2f, zMax - zMin), Palette.Floor);
    }

    // A building: a plain box from xMin..xMax, zMin..zMax, height tall.
    private void Building(float xMin, float xMax, float zMin, float zMax, float height)
    {
        Box("Building", new Vector3((xMin + xMax) * 0.5f, 0f, (zMin + zMax) * 0.5f),
            new Vector3(xMax - xMin, height, zMax - zMin), Palette.Building);
    }

    // A block (obstacle, shelf, pillar...). bottomCentre = the middle of its bottom face.
    private void Block(Vector3 bottomCentre, Vector3 size)
    {
        Box("Block", bottomCentre, size, Palette.Block);
    }

    // A wall along the X axis at z, from xMin to xMax, height tall. doorGaps:
    // the x of each door opening (DoorGapWidth wide, with wall above it).
    private void WallX(float z, float xMin, float xMax, float height, params float[] doorGaps)
    {
        float from = xMin;
        foreach (float gap in doorGaps)
        {
            float gapStart = gap - DoorGapWidth * 0.5f;
            float gapEnd = gap + DoorGapWidth * 0.5f;
            WallPieceX(z, from, gapStart, 0f, height);
            WallPieceX(z, gapStart, gapEnd, DoorGapHeight, height); // above the opening
            from = gapEnd;
        }
        WallPieceX(z, from, xMax, 0f, height);
    }

    // The same, for a wall along the Z axis at x.
    private void WallZ(float x, float zMin, float zMax, float height, params float[] doorGaps)
    {
        float from = zMin;
        foreach (float gap in doorGaps)
        {
            float gapStart = gap - DoorGapWidth * 0.5f;
            float gapEnd = gap + DoorGapWidth * 0.5f;
            WallPieceZ(x, from, gapStart, 0f, height);
            WallPieceZ(x, gapStart, gapEnd, DoorGapHeight, height);
            from = gapEnd;
        }
        WallPieceZ(x, from, zMax, 0f, height);
    }

    private void WallPieceX(float z, float xMin, float xMax, float bottom, float top)
    {
        if (xMax - xMin < 0.01f || top - bottom < 0.01f)
        {
            return;
        }
        Box("Wall", new Vector3((xMin + xMax) * 0.5f, bottom, z),
            new Vector3(xMax - xMin, top - bottom, WallThickness), Palette.Wall);
    }

    private void WallPieceZ(float x, float zMin, float zMax, float bottom, float top)
    {
        if (zMax - zMin < 0.01f || top - bottom < 0.01f)
        {
            return;
        }
        Box("Wall", new Vector3(x, bottom, (zMin + zMax) * 0.5f),
            new Vector3(WallThickness, top - bottom, zMax - zMin), Palette.Wall);
    }

    // A door in the opening of a WallX at (x, z). outwardZ: +1 opens toward +Z, -1 toward -Z.
    private Door DoorInWallX(float x, float z, float outwardZ)
    {
        Vector3 outward = new Vector3(0f, 0f, outwardZ);
        return DoorOnFace(new Vector3(x, 0f, z) + outward * WallThickness * 0.5f, outward);
    }

    // A door in the opening of a WallZ at (x, z). outwardX: +1 opens toward +X, -1 toward -X.
    private Door DoorInWallZ(float x, float z, float outwardX)
    {
        Vector3 outward = new Vector3(outwardX, 0f, 0f);
        return DoorOnFace(new Vector3(x, 0f, z) + outward * WallThickness * 0.5f, outward);
    }

    // A door on a flat face (a building side, a wall face) at facePoint, opening toward outward.
    private Door DoorOnFace(Vector3 facePoint, Vector3 outward)
    {
        return Door.Create(area, facePoint + outward * 0.18f, outward);
    }

    // Two thin rails along a route (the ride), like the first prototype's corridor.
    private void BuildRails(List<Vector3> route)
    {
        Transform rails = new GameObject("Rails").transform;
        rails.SetParent(transform, false);
        Material railMaterial = Palette.Lit(Palette.Rail);

        for (int i = 0; i + 1 < route.Count; i++)
        {
            Vector3 from = route[i];
            Vector3 to = route[i + 1];
            Vector3 along = to - from;
            along.y = 0f;
            if (along.sqrMagnitude < 0.01f)
            {
                continue;
            }
            Vector3 side = new Vector3(along.z, 0f, -along.x).normalized * RailGap;
            Vector3 middle = (from + to) * 0.5f + Vector3.up * 0.06f;
            Vector3 size = new Vector3(0.12f, 0.12f, along.magnitude + 0.12f);
            Vector3 angles = Quaternion.LookRotation(along).eulerAngles;
            Shapes.Block(PrimitiveType.Cube, "Rail", rails, middle - side, size, railMaterial, false, angles);
            Shapes.Block(PrimitiveType.Cube, "Rail", rails, middle + side, size, railMaterial, false, angles);
        }
    }
}
