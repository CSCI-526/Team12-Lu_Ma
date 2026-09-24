// Level.cs
// ---------------------------------------------------------------------------
// Builds the whole greybox map out of plain boxes when the scene starts
// (Awake), and describes the fights in it as a list of Encounters that
// WaveSpawner plays in order. Lives on the "Level" object in the scene.
//
// THE MAP is the real USC School of Cinematic Arts block in Los Angeles, the
// whole block between W 34th St (north), Watt Way (east), W 35th St (south)
// and McClintock Ave (west), with every building on it:
//   SCI  Interactive Media Building (USC Games), along McClintock
//   SCB  Student Affairs + Animation, along 34th St
//   SCC  the sound stages, in the middle
//   SCA  the Spielberg (west) and Lucas (east) buildings around the
//        courtyard with the Douglas Fairbanks fountain
//   SCX  Production Equipment Center, SCE  Redstone stages, along 35th St
//   and the Cinematic Arts Park between SCI and SCC.
// Only the layout is copied (footprints, heights, openings); there is no
// detail, no text and no colour on purpose (greybox).
//
// POSITIONS are in metres, measured from the block's south-west kerb corner
// (McClintock x 35th St):  +x = along 34th St toward Watt Way,
//                          +z = along McClintock toward 34th St,  y = up.
// (The real street grid is turned 28 degrees from true north; the map is
// turned back so the streets line up with x and z.) Footprints come from
// OpenStreetMap and the LA County roof outlines, the SCA courtyard from the
// USC ground-floor plan, heights from storeys and the county's roof heights.
//
// THE FIGHTS (one method each, at the bottom):
//   1. W 34th St at McClintock: SCI's front door, the SCI|SCB palm walk
//   2. The Cinematic Arts Park, seen from the Muybridge plaza
//   3. The lane between the SCC stages and the Spielberg building
//   4. Watt Way at the Lucas building's east portico
//   5. Boss: the SCA courtyard, entered through the Main Gates
//
// HOW TO CHANGE IT: zombies walk in STRAIGHT lines from a spawn point's
// Position to its Exit, then straight at the player, so keep those lines
// free of buildings. Keep every spawn Exit, barrel and crate within about
// 33 degrees of the stop's Facing (doors within about 40). The screen edge
// is only about 45 degrees to each side, and the view turns up to 12
// degrees toward the word being typed; a zombie walking straight at the
// player never comes into view if it starts outside it.
// ---------------------------------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-200)] // build the map before any other script starts
public class Level : MonoBehaviour
{
    private const float RailGap = 0.75f;          // each rail is this far from the middle of the ride
    private const float PavementTop = 0.02f;      // sidewalks and plazas sit a little above the streets
    private const float LawnTop = 0.06f;          // lawns sit a little above the paving

    // The fights, in the order they are played. Filled in Awake.
    public List<Encounter> Encounters { get; private set; }

    // Where the player stands (on the ground) and faces before the first ride.
    public Vector3 StartPosition { get; private set; }
    public Vector3 StartFacing { get; private set; }

    private Transform area; // the part being built: new objects go under it

    private void Awake()
    {
        Encounters = new List<Encounter>();
        StartPosition = new Vector3(-6f, 0f, 68f); // on McClintock, beside SCI, looking up to 34th St
        StartFacing = Vector3.forward;

        // The map.
        BuildGround();
        BuildNeighbours();
        BuildSCI();
        BuildSCB();
        BuildSCC();
        BuildSCA();
        BuildCourtyard();
        BuildSCX();
        BuildSCE();
        BuildPark();

        // The fights, in order.
        AddWave1_34thStreet();
        AddWave2_Park();
        AddWave3_Lane();
        AddWave4_WattWay();
        AddBoss_Courtyard();

        foreach (Encounter encounter in Encounters)
        {
            BuildRails(encounter.Route);
        }
    }

    // =====================================================================
    // The map
    // =====================================================================

    // The ground, the streets and the paving of the block.
    private void BuildGround()
    {
        StartArea("Ground");
        // One big ground slab (the streets are this colour too).
        Slab(-140f, 250f, -95f, 145f, 0f, 0.3f, Palette.Floor);

        // The block's paving, kerb to kerb. The kerb steps in on 35th St in
        // front of SCX (a lay-by) and on Watt Way beside SCE.
        Slab(0f, 95.8f, 0f, 83.1f, PavementTop, 0.1f, Palette.Pavement);
        Slab(95.8f, 128.2f, 2.7f, 83.1f, PavementTop, 0.1f, Palette.Pavement);
        Slab(128.2f, 177.7f, 0f, 83.1f, PavementTop, 0.1f, Palette.Pavement);
        Slab(177.7f, 180.7f, 31.7f, 83.1f, PavementTop, 0.1f, Palette.Pavement);

        // Watt Way is a paved pedestrian mall, not a street.
        Slab(180.7f, 191.7f, 31.7f, 83.1f, PavementTop, 0.1f, Palette.Pavement);
        Slab(177.7f, 188.7f, -6.3f, 31.7f, PavementTop, 0.1f, Palette.Pavement);

        // The planting strip along the Watt Way side of the Lucas building
        // (with the paved entry plaza in front of the east portico left open).
        Slab(173.2f, 180.7f, 31.7f, 47.8f, LawnTop, 0.1f, Palette.Lawn);
        Slab(173.2f, 180.7f, 57.8f, 83.1f, LawnTop, 0.1f, Palette.Lawn);

        // The far sidewalks of 34th St, McClintock and 35th St.
        Slab(-15.8f, 200f, 95.8f, 98.8f, PavementTop, 0.1f, Palette.Pavement);
        Slab(-15.8f, -11.6f, -9.3f, 95.8f, PavementTop, 0.1f, Palette.Pavement);
        Slab(-11.6f, 177.7f, -9.3f, -6.3f, PavementTop, 0.1f, Palette.Pavement);
    }

    // The buildings across the four streets, as plain boxes (so the streets have two sides).
    private void BuildNeighbours()
    {
        StartArea("Neighbours");
        Neighbour(53.1f, 172.2f, 98.2f, 137.5f, 21.6f);    // Wilson Dental Library (across 34th St)
        Neighbour(191f, 239.3f, 4.9f, 25.2f, 12.4f);       // Music Complex (across Watt Way)
        Neighbour(207.1f, 236.4f, 38.5f, 78.8f, 11.6f);    // across Watt Way, north
        Neighbour(95.7f, 131f, -35.2f, -10.1f, 6.7f);      // across 35th St
        Neighbour(126.9f, 163.8f, -86.3f, -13.7f, 13f);    // Heritage Hall
        Neighbour(4.4f, 84.2f, -46.4f, -15f, 13.9f);       // McKay Center
        Neighbour(-123.6f, -29.9f, 39.6f, 87f, 21.5f);     // Lyon Center (across McClintock)
        Neighbour(-132.6f, -15.1f, -3.3f, 52.3f, 4.6f);    // Uytengsu Aquatics Center
    }

    // SCI: the Interactive Media Building (USC Games), 3 storeys, along McClintock.
    private void BuildSCI()
    {
        StartArea("SCI - Interactive Media Building");
        Building(17.8f, 23.2f, 8f, 10.7f, 4.5f);      // low terrace block at the south
        Building(23.2f, 30.4f, 7.5f, 15.2f, 17.5f);   // the south-east tower
        Building(2.9f, 23.2f, 10.7f, 15.2f, 13.5f);
        Building(2.8f, 29.8f, 15.2f, 28.2f, 13.5f);
        Building(4.1f, 26.6f, 28.2f, 47.3f, 13.5f);
        Building(26.6f, 29.8f, 28.2f, 47.3f, 0.5f, 4.5f);  // roof of the arcade facing the park
        Building(2.5f, 29.8f, 47.3f, 51f, 13.5f);
        Building(5.5f, 26.6f, 51f, 61.3f, 5f);        // ground floor between the two porches
        Building(2.5f, 29.8f, 51f, 61.3f, 8.5f, 5f);  // upper floors over the porches
        Building(2.5f, 29.8f, 61.3f, 62.5f, 13.5f);
        Building(3.7f, 28.6f, 62.5f, 76.2f, 13.5f);
        Building(6.8f, 24.9f, 76.2f, 79.6f, 13.5f);
        Building(12.3f, 15.3f, 79.6f, 81.1f, 1f, 3.5f);   // small porch roof over the front door on 34th St

        // The arcade along the park (6 piers).
        for (float z = 30f; z < 47f; z += 3.2f)
        {
            Pier(29.3f, z, 0.6f, 4.5f);
        }
        Pier(3f, 54.4f, 0.8f, 5f);      // the 3-arch porch on McClintock
        Pier(3f, 57.9f, 0.8f, 5f);
        Pier(29.3f, 54.4f, 0.8f, 5f);   // the porch facing the Muybridge plaza
        Pier(29.3f, 57.9f, 0.8f, 5f);
    }

    // SCB: Student Affairs (the lower west part) and Animation, along 34th St.
    private void BuildSCB()
    {
        StartArea("SCB");
        Building(38.7f, 52.2f, 58.3f, 79.3f, 9.5f);   // west pavilion (2 storeys), stepped at its west end
        Building(35.8f, 38.7f, 61.6f, 76.1f, 9.5f);
        Building(34f, 35.8f, 65.4f, 72.5f, 9.5f);
        Building(52.2f, 63.9f, 58.3f, 80f, 13.5f);    // central hall
        Building(52.4f, 63.9f, 54.7f, 58.3f, 13.5f);
        Building(63.9f, 88.3f, 58.3f, 79f, 13.5f);    // east part
        Building(63.9f, 85.2f, 55f, 58.3f, 13.5f);
    }

    // SCC: the sound stages, one big windowless box in the middle of the block.
    private void BuildSCC()
    {
        StartArea("SCC - Sound Stages");
        Building(63.3f, 86.7f, 4.9f, 49.7f, 12f);
        Building(86.7f, 88.5f, 4.9f, 21f, 12f);
        Building(86.7f, 88.4f, 24.7f, 33.3f, 12f);
    }

    // SCA: the Steven Spielberg Building (west wing) and the George Lucas
    // Building (east wing), 4 storeys, around the courtyard, joined at the
    // south by a range with a covered passage and at the north by the Main Gates.
    private void BuildSCA()
    {
        StartArea("SCA - Spielberg and Lucas Buildings");
        // South range, west part, and the square tower (the tallest thing on the block).
        Building(96.48f, 128.97f, 31.77f, 35.93f, 19f);
        Building(94.88f, 117.97f, 35.93f, 45.05f, 19f);
        Building(117.97f, 124.12f, 35.93f, 38.5f, 19f);
        Building(117.97f, 124.12f, 38.5f, 45.05f, 26.5f);  // tower
        Building(124.12f, 128.97f, 35.93f, 45.05f, 14.5f);

        // Spielberg (west) wing. The Harold Lloyd lobby runs through it at ground level.
        Building(94.88f, 120.99f, 45.05f, 48.02f, 19f);
        Building(96.48f, 120.99f, 48.02f, 60.67f, 5.5f);         // Harold Lloyd lobby (one tall storey)
        Building(96.48f, 117.85f, 48.02f, 60.67f, 13.5f, 5.5f);  // floors above it
        Building(94.88f, 120.99f, 60.67f, 72.75f, 19f);
        Building(96.48f, 115f, 72.75f, 77.43f, 19f);
        Building(115f, 120.7f, 72.75f, 73.55f, 19f);
        Building(93.2f, 96.48f, 48.02f, 60.67f, 1f, 5f);         // roof of the west portico

        // The covered passage south of the courtyard: the building bridges over it.
        Building(128.97f, 137.8f, 35.93f, 45.05f, 9f, 5.5f);
        Building(128.97f, 137.8f, 31.77f, 35.93f, 13.5f, 5.5f);

        // South range, east part.
        Building(137.8f, 170.01f, 31.77f, 35.93f, 19f);
        Building(137.8f, 145.67f, 35.93f, 45.05f, 14.5f);

        // Lucas (east) wing. The Mary Pickford lobby runs through it at ground level.
        Building(145.67f, 171.72f, 35.93f, 48.02f, 19f);
        Building(145.67f, 170.01f, 48.02f, 60.67f, 5.5f);        // Mary Pickford lobby
        Building(148.63f, 170.01f, 48.02f, 60.67f, 13.5f, 5.5f); // floors above it
        Building(145.67f, 171.72f, 60.67f, 72.98f, 19f);
        Building(151.77f, 170.01f, 72.98f, 77.43f, 19f);
        Building(145.67f, 151.77f, 72.98f, 73.55f, 19f);
        Building(170.01f, 173.2f, 48.02f, 60.67f, 1f, 5f);       // roof of the east portico (faces Watt Way)

        // The porticos in front of the two lobbies (6 piers each).
        float[] porticoZ = { 48.02f, 49.78f, 52.8f, 56.05f, 59.07f, 60.67f };
        foreach (float z in porticoZ)
        {
            Pier(93.51f, z, 0.6f, 5f);
            Pier(172.86f, z, 0.6f, 5f);
        }
    }

    // The Academy of Motion Picture Arts & Sciences Courtyard, between the two
    // SCA wings: arcades in front of both lobbies, the Main Gates on 34th St,
    // the covered passage to the south, and the round fountain with the
    // statue of Douglas Fairbanks in the middle.
    private void BuildCourtyard()
    {
        StartArea("SCA Courtyard");
        // Arcades in front of the two lobbies (4 piers each, with a roof).
        Building(120.99f, 124.4f, 49.4f, 59.5f, 0.5f, 5f);
        Building(142f, 145.67f, 49.4f, 59.5f, 0.5f, 5f);
        float[] arcadeZ = { 49.73f, 52.92f, 56f, 59.13f };
        foreach (float z in arcadeZ)
        {
            Pier(124.01f, z, 0.6f, 5f);
            Pier(142.36f, z, 0.6f, 5f);
        }

        // The Main Gates: two rows of 4 piers (three open arches) under a
        // one-storey roof, with low walls closing the rest of the courtyard's
        // north side.
        Building(120.99f, 145.67f, 69.55f, 73.28f, 1f, 4.5f);
        Building(127.3f, 138.9f, 69.4f, 73.4f, 2f, 5.5f);
        float[] gateX = { 128.4f, 131.53f, 134.67f, 137.8f };
        foreach (float x in gateX)
        {
            Pier(x, 72.98f, 0.9f, 4.5f);
            Pier(x, 69.85f, 0.9f, 4.5f);
        }
        LowBox(120.99f, 127.95f, 72.83f, 73.13f, 1.2f, Palette.Wall);
        LowBox(138.25f, 145.67f, 72.83f, 73.13f, 1.2f, Palette.Wall);

        // The covered passage to the south (two rows of 4 piers).
        float[] passageZ = { 32.11f, 35.93f, 39.69f, 43.51f };
        foreach (float z in passageZ)
        {
            Pier(131.7f, z, 0.8f, 5.5f);
            Pier(134.73f, z, 0.8f, 5.5f);
        }

        // Raised planters: one in the north-east, two beside the passage.
        LowBox(138.3f, 142.2f, 60.2f, 69.6f, 0.6f, Palette.Lawn);
        LowBox(124.47f, 128.57f, 45.39f, 48.42f, 0.5f, Palette.Lawn);
        LowBox(138.09f, 142.25f, 45.39f, 48.42f, 0.5f, Palette.Lawn);

        // Lawn panels on both sides of the forecourt outside the gates, on 34th St.
        LowBox(117.1f, 124.3f, 73.9f, 80.6f, 0.5f, Palette.Lawn);
        LowBox(124.3f, 128.4f, 73.9f, 78.3f, 0.5f, Palette.Lawn);
        LowBox(142.1f, 149.6f, 73.9f, 80.6f, 0.5f, Palette.Lawn);
        LowBox(138.3f, 142.1f, 73.9f, 78.3f, 0.5f, Palette.Lawn);

        // The fountain: a round basin (dark water on top), a pedestal and the statue (a plain block).
        Disc(133.3f, 54.4f, 3.1f, 0f, 0.5f, Palette.Wall);
        Disc(133.3f, 54.4f, 2.2f, 0.5f, 0.02f, Palette.Floor);
        Box("Pedestal", new Vector3(133.3f, 0.5f, 54.4f), new Vector3(1f, 1.8f, 0.6f), Palette.Block);
        Box("Statue", new Vector3(133.3f, 2.3f, 54.4f), new Vector3(0.6f, 2.1f, 0.4f), Palette.Block);
    }

    // SCX: the Production Equipment Center, along 35th St, with a walled service yard.
    private void BuildSCX()
    {
        StartArea("SCX - Production Equipment Center");
        Building(101.3f, 131.2f, 5f, 23.2f, 10f);
        Building(101.3f, 130f, 23.2f, 26.2f, 10f);
        Building(94.2f, 101.3f, 17.9f, 26.2f, 10f);
        LowBox(94.2f, 94.5f, 5f, 17.9f, 3f, Palette.Wall);   // service yard walls
        LowBox(94.5f, 101.3f, 5f, 5.3f, 3f, Palette.Wall);
    }

    // SCE: the Redstone production building (two stages), along 35th St.
    // Between SCX and SCE a narrow walk runs from 35th St, through a gateway
    // arch, straight to the SCA courtyard passage.
    private void BuildSCE()
    {
        StartArea("SCE - Redstone Stages");
        Building(136.4f, 172.4f, 4.8f, 26.2f, 12.5f);
        Building(135.1f, 136.4f, 4.8f, 23f, 12.5f);

        // The gateway on 35th St: two posts and a lintel.
        LowBox(130.2f, 131.7f, 3.8f, 4.8f, 6.5f, Palette.Wall);
        LowBox(134.6f, 136.1f, 3.8f, 4.8f, 6.5f, Palette.Wall);
        Box("Lintel", new Vector3(133.15f, 5f, 4.3f), new Vector3(2.9f, 1.5f, 1f), Palette.Wall);
    }

    // The Cinematic Arts Park (the lawn between SCI and SCC, its seat wall and
    // the planting beds along 35th St), the Muybridge statue in the plaza at its
    // north end, and the long pool north of SCC.
    private void BuildPark()
    {
        StartArea("Park");
        // The lawn (its west edge steps in at the south) and the planting beds along 35th St.
        Slab(35f, 56f, 12f, 18f, LawnTop, 0.1f, Palette.Lawn);
        Slab(32.5f, 56f, 18f, 49.3f, LawnTop, 0.1f, Palette.Lawn);
        Slab(35.4f, 56.2f, 0.5f, 9.5f, LawnTop, 0.1f, Palette.Lawn);

        // The low curved seat wall across the south end of the lawn (the map's "semicircle").
        Vector3[] seatWall =
        {
            new Vector3(35.7f, 0f, 9.2f), new Vector3(38f, 0f, 10.7f), new Vector3(40.5f, 0f, 11.8f),
            new Vector3(43.1f, 0f, 12.5f), new Vector3(45.8f, 0f, 12.7f), new Vector3(48.5f, 0f, 12.6f),
            new Vector3(51.2f, 0f, 12f), new Vector3(53.7f, 0f, 11f), new Vector3(56.1f, 0f, 9.7f),
        };
        for (int i = 0; i + 1 < seatWall.Length; i++)
        {
            WallPiece(seatWall[i], seatWall[i + 1], 0.45f, 0.5f);
        }


        // The Eadweard Muybridge statue in the plaza (the map's small circle).
        Disc(33.4f, 55.6f, 1.8f, 0f, 0.6f, Palette.Wall);
        Box("Pedestal", new Vector3(33.4f, 0.6f, 55.6f), new Vector3(1f, 1.4f, 1f), Palette.Block);
        Box("Statue", new Vector3(33.4f, 2f, 55.6f), new Vector3(0.6f, 2f, 0.4f), Palette.Block);

        // The long pool north of SCC (the map's rounded rectangle).
        LowBox(67.4f, 82.2f, 50.6f, 53f, 0.5f, Palette.Wall);
    }

    // =====================================================================
    // The fights
    // =====================================================================

    // 1. W 34th St at McClintock, looking east along 34th St: SCI's front door
    //    on the right, the palm walk between SCI and SCB, SCB's frontage, and
    //    the SCA far away at the end of the street.
    private void AddWave1_34thStreet()
    {
        StartArea("Fight 1 - W 34th St");
        Door sciFrontDoor = DoorOnFace(new Vector3(13.8f, 0f, 79.6f), Vector3.forward);
        Door scbWestDoor = DoorOnFace(new Vector3(45.5f, 0f, 79.3f), Vector3.forward);
        // The real double gate across the palm walk between SCI and SCB.
        Gate gate = Gate.Create(area, new Vector3(32.8f, 0f, 62.3f), Vector3.back, 6.2f, 3f);

        Encounter encounter = new Encounter();
        encounter.Route.Add(new Vector3(-6f, 0f, 68f));
        encounter.Route.Add(new Vector3(-6f, 0f, 84f));
        encounter.Route.Add(new Vector3(-2.5f, 0f, 88.5f));
        encounter.Route.Add(new Vector3(0.5f, 0f, 88.5f));
        encounter.Facing = new Vector3(0.985f, 0f, -0.174f);
        encounter.SpawnPoints.Add(new SpawnPoint(new Vector3(13.8f, 0f, 79.1f), new Vector3(14.8f, 0f, 84f), sciFrontDoor, 0.4f));
        encounter.SpawnPoints.Add(new SpawnPoint(new Vector3(31.9f, 0f, 75f), new Vector3(31.6f, 0f, 84.8f), null, 1f));    // down the palm walk
        encounter.SpawnPoints.Add(new SpawnPoint(new Vector3(29.4f, 0f, 100.4f), new Vector3(30.8f, 0f, 94f), null, 1f));   // the driveway across 34th St
        encounter.SpawnPoints.Add(new SpawnPoint(new Vector3(45.5f, 0f, 78.8f), new Vector3(45.5f, 0f, 83.6f), scbWestDoor, 0.4f));
        encounter.GroupSize = 1; // 6 zombies one by one: every spawn point is used
        encounter.Barrels.Add(Barrel.Create(new Vector3(20.5f, 0f, 84.3f), area));
        encounter.Barrels.Add(Barrel.Create(new Vector3(16.5f, 0f, 93.3f), area));
        encounter.Barrels.Add(Barrel.Create(new Vector3(8.5f, 0f, 84.3f), area));
        encounter.Crates.Add(SupplyCrate.Create(new Vector3(26f, 0f, 90.8f), SupplyKind.Lure, area));
        encounter.ExitGate = gate;
        Encounters.Add(encounter);
    }

    // 2. The Cinematic Arts Park: run down the palm walk, through its gate,
    //    and stop in the Muybridge plaza looking south over the lawn.
    private void AddWave2_Park()
    {
        StartArea("Fight 2 - Park");
        Door sciTowerDoor = DoorOnFace(new Vector3(30.4f, 0f, 11.3f), Vector3.right);
        Door sccStageDoor = DoorOnFace(new Vector3(63.3f, 0f, 16f), Vector3.left);
        // The real double gate across the service lane beside SCC, near 35th St.
        Gate gate = Gate.Create(area, new Vector3(59.75f, 0f, 8.7f), Vector3.back, 7.3f, 3f);

        Encounter encounter = new Encounter();
        encounter.Route.Add(new Vector3(0.5f, 0f, 88.5f));
        encounter.Route.Add(new Vector3(31.9f, 0f, 88.5f));
        encounter.Route.Add(new Vector3(31.9f, 0f, 64.5f));
        encounter.Route.Add(new Vector3(34.3f, 0f, 59.4f));
        encounter.Route.Add(new Vector3(42f, 0f, 50.5f));
        encounter.Facing = new Vector3(-0.087f, 0f, -0.996f);
        encounter.SpawnPoints.Add(new SpawnPoint(new Vector3(28f, 0f, 34.8f), new Vector3(34.6f, 0f, 34.2f), null, 0.4f));   // out of SCI's park arcade
        encounter.SpawnPoints.Add(new SpawnPoint(new Vector3(29.9f, 0f, 11.3f), new Vector3(33.3f, 0f, 11f), sciTowerDoor, 0.4f));
        encounter.SpawnPoints.Add(new SpawnPoint(new Vector3(32.9f, 0f, 7.6f), new Vector3(33.6f, 0f, 16.8f), null, 0.8f));  // the walk from 35th St
        encounter.SpawnPoints.Add(new SpawnPoint(new Vector3(63.8f, 0f, 16f), new Vector3(59.6f, 0f, 15f), sccStageDoor, 0.5f));
        encounter.GroupSize = 2; // 8 zombies in pairs: every spawn point is used
        encounter.Barrels.Add(Barrel.Create(new Vector3(38.5f, 0f, 30f), area));
        encounter.Barrels.Add(Barrel.Create(new Vector3(52f, 0f, 26f), area));
        encounter.Barrels.Add(Barrel.Create(new Vector3(37.5f, 0f, 19.5f), area));
        encounter.Crates.Add(SupplyCrate.Create(new Vector3(44f, 0f, 33.5f), SupplyKind.Health, area));
        encounter.Crates.Add(SupplyCrate.Create(new Vector3(35.5f, 0f, 40.5f), SupplyKind.Freeze, area));
        encounter.ExitGate = gate;
        Encounters.Add(encounter);
    }

    // 3. Out through the service lane, along 35th St, and up the lane between
    //    the SCC stages (left) and SCX / the Spielberg building (right).
    private void AddWave3_Lane()
    {
        StartArea("Fight 3 - SCC lane");
        Door sccEastDoor = DoorOnFace(new Vector3(88.4f, 0f, 29f), Vector3.right);
        Door galleryDoor = DoorOnFace(new Vector3(94.88f, 0f, 44.97f), Vector3.left);
        // Blocks the lane between SCA and SCX/SCE (the way to Watt Way).
        Gate gate = Gate.Create(area, new Vector3(95.2f, 0f, 31.05f), Vector3.right, 10.1f, 3f);

        Encounter encounter = new Encounter();
        encounter.Route.Add(new Vector3(42f, 0f, 50.5f));
        encounter.Route.Add(new Vector3(52f, 0f, 50.6f));
        encounter.Route.Add(new Vector3(59.75f, 0f, 44f));
        encounter.Route.Add(new Vector3(59.75f, 0f, -2.4f));
        encounter.Route.Add(new Vector3(91.2f, 0f, -2.4f));
        encounter.Route.Add(new Vector3(91.2f, 0f, 18f));
        encounter.Facing = Vector3.forward;
        encounter.SpawnPoints.Add(new SpawnPoint(new Vector3(87.9f, 0f, 29f), new Vector3(90.3f, 0f, 29.4f), sccEastDoor, 0.4f));
        encounter.SpawnPoints.Add(new SpawnPoint(new Vector3(95.38f, 0f, 44.97f), new Vector3(92.6f, 0f, 45.1f), galleryDoor, 0.4f));
        encounter.SpawnPoints.Add(new SpawnPoint(new Vector3(95.9f, 0f, 51.3f), new Vector3(92.3f, 0f, 51.3f), null, 0.3f));  // out of the west portico
        encounter.SpawnPoints.Add(new SpawnPoint(new Vector3(84.5f, 0f, 52.3f), new Vector3(90.3f, 0f, 52.6f), null, 1f));    // round SCC's corner from the plaza
        encounter.GroupSize = 3;
        encounter.Barrels.Add(Barrel.Create(new Vector3(93.6f, 0f, 34f), area));
        encounter.Barrels.Add(Barrel.Create(new Vector3(89.3f, 0f, 40.5f), area));
        encounter.Barrels.Add(Barrel.Create(new Vector3(93.3f, 0f, 24.5f), area));
        encounter.Crates.Add(SupplyCrate.Create(new Vector3(93.9f, 0f, 42.3f), SupplyKind.Lure, area));
        encounter.Crates.Add(SupplyCrate.Create(new Vector3(89.3f, 0f, 25f), SupplyKind.Health, area));
        encounter.ExitGate = gate;
        Encounters.Add(encounter);
    }

    // 4. Along the lane south of SCA to Watt Way, then turn to face the Lucas
    //    building's east portico: zombies burst out of the Mary Pickford lobby.
    private void AddWave4_WattWay()
    {
        StartArea("Fight 4 - Watt Way");
        Door pickfordSouth = DoorOnFace(new Vector3(170.01f, 0f, 51.47f), Vector3.right);
        Door pickfordMiddle = DoorOnFace(new Vector3(170.01f, 0f, 54.6f), Vector3.right);
        Door pickfordNorth = DoorOnFace(new Vector3(170.01f, 0f, 57.74f), Vector3.right);
        // Blocks Watt Way at 34th St (where the bollards are).
        Gate gate = Gate.Create(area, new Vector3(180.1f, 0f, 78f), Vector3.forward, 23.2f, 3f);

        Encounter encounter = new Encounter();
        encounter.Route.Add(new Vector3(91.2f, 0f, 18f));
        encounter.Route.Add(new Vector3(91.2f, 0f, 29f));
        encounter.Route.Add(new Vector3(177f, 0f, 29f));
        encounter.Route.Add(new Vector3(187.5f, 0f, 49.5f));
        encounter.Facing = new Vector3(-0.793f, 0f, 0.609f);
        encounter.SpawnPoints.Add(new SpawnPoint(new Vector3(169.51f, 0f, 51.47f), new Vector3(174.6f, 0f, 51.3f), pickfordSouth, 0.4f));
        encounter.SpawnPoints.Add(new SpawnPoint(new Vector3(169.51f, 0f, 54.6f), new Vector3(174.6f, 0f, 54.43f), pickfordMiddle, 0.4f));
        encounter.SpawnPoints.Add(new SpawnPoint(new Vector3(169.51f, 0f, 57.74f), new Vector3(174.6f, 0f, 57.56f), pickfordNorth, 0.4f));
        encounter.SpawnPoints.Add(new SpawnPoint(new Vector3(170.6f, 0f, 73.6f), new Vector3(177.5f, 0f, 74f), null, 0.2f)); // the Ray Stark Theatre exit
        encounter.GroupSize = 3;
        encounter.Barrels.Add(Barrel.Create(new Vector3(182f, 0f, 55f), area));
        encounter.Barrels.Add(Barrel.Create(new Vector3(175.5f, 0f, 52.5f), area));
        encounter.Barrels.Add(Barrel.Create(new Vector3(177f, 0f, 58.5f), area));
        encounter.Crates.Add(SupplyCrate.Create(new Vector3(180f, 0f, 59f), SupplyKind.Freeze, area));
        encounter.Crates.Add(SupplyCrate.Create(new Vector3(181.75f, 0f, 58.5f), SupplyKind.Health, area));
        encounter.ExitGate = gate;
        Encounters.Add(encounter);
    }

    // 5. Boss: up Watt Way, west along 34th St, and in through the middle arch
    //    of the Main Gates. The boss stands in the courtyard beside the fountain.
    private void AddBoss_Courtyard()
    {
        StartArea("Fight 5 - Courtyard (boss)");
        Encounter encounter = new Encounter();
        encounter.Route.Add(new Vector3(187.5f, 0f, 49.5f));
        encounter.Route.Add(new Vector3(182.2f, 0f, 62f));
        encounter.Route.Add(new Vector3(182.2f, 0f, 85.5f));
        encounter.Route.Add(new Vector3(178.5f, 0f, 89f));
        encounter.Route.Add(new Vector3(136.5f, 0f, 89f));
        encounter.Route.Add(new Vector3(133.1f, 0f, 85.5f));
        encounter.Route.Add(new Vector3(133.1f, 0f, 65.5f));
        encounter.Facing = new Vector3(-0.332f, 0f, -0.943f);
        encounter.IsBossFight = true;
        encounter.BossStand = new Vector3(128f, 0f, 51f);
        // Barrels next to the boss: blowing them up hurts it.
        encounter.Barrels.Add(Barrel.Create(new Vector3(131.6f, 0f, 49.6f), area));
        encounter.Barrels.Add(Barrel.Create(new Vector3(125.4f, 0f, 55f), area));
        encounter.Crates.Add(SupplyCrate.Create(new Vector3(131.2f, 0f, 60.5f), SupplyKind.Health, area));
        Encounters.Add(encounter);
    }

    // =====================================================================
    // Building helpers. Boxes keep their colliders (dead zombies land on them).
    // =====================================================================

    // Starts a new part of the map: everything built next goes under an object with this name.
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

    // A flat slab from xMin..xMax, zMin..zMax whose top is at y = top.
    private void Slab(float xMin, float xMax, float zMin, float zMax, float top, float thickness, Color color)
    {
        Box("Slab", new Vector3((xMin + xMax) * 0.5f, top - thickness, (zMin + zMax) * 0.5f),
            new Vector3(xMax - xMin, thickness, zMax - zMin), color);
    }

    // A building block: a plain box from xMin..xMax, zMin..zMax, height tall,
    // starting at y = bottom (0 = on the ground; higher for upper floors and
    // roofs that span over an open ground floor, an arcade or a passage).
    private void Building(float xMin, float xMax, float zMin, float zMax, float height, float bottom = 0f)
    {
        Box("Building", new Vector3((xMin + xMax) * 0.5f, bottom, (zMin + zMax) * 0.5f),
            new Vector3(xMax - xMin, height, zMax - zMin), Palette.Building);
    }

    // A building across the street (plain grey instead of the block's blue, so the block stands out).
    private void Neighbour(float xMin, float xMax, float zMin, float zMax, float height)
    {
        Box("Neighbour", new Vector3((xMin + xMax) * 0.5f, 0f, (zMin + zMax) * 0.5f),
            new Vector3(xMax - xMin, height, zMax - zMin), Palette.Block);
    }

    // A square pier (of an arcade, a portico or a gate) centred on (x, z).
    private void Pier(float x, float z, float size, float height)
    {
        Box("Pier", new Vector3(x, 0f, z), new Vector3(size, height, size), Palette.Wall);
    }

    // A box standing on the ground (a planter, a wall, a gateway post, a pool...).
    private void LowBox(float xMin, float xMax, float zMin, float zMax, float height, Color color)
    {
        Box("Low box", new Vector3((xMin + xMax) * 0.5f, 0f, (zMin + zMax) * 0.5f),
            new Vector3(xMax - xMin, height, zMax - zMin), color);
    }

    // A round, flat cylinder (the fountain, a statue base). No collider: a
    // cylinder's collider is a capsule, which would be the wrong shape.
    private void Disc(float x, float z, float radius, float bottom, float height, Color color)
    {
        // Unity's cylinder is 2 units tall at scale 1, so its y scale is half the height.
        Shapes.Block(PrimitiveType.Cylinder, "Disc", area, new Vector3(x, bottom + height * 0.5f, z),
            new Vector3(radius * 2f, height * 0.5f, radius * 2f), Palette.Lit(color));
    }

    // A straight piece of low wall from one point to another (for curved walls).
    private void WallPiece(Vector3 from, Vector3 to, float height, float thickness)
    {
        Vector3 along = to - from;
        Vector3 middle = (from + to) * 0.5f + Vector3.up * height * 0.5f;
        Vector3 size = new Vector3(thickness, height, along.magnitude + thickness); // overlaps a little at the joints
        Vector3 angles = Quaternion.LookRotation(along).eulerAngles;
        Shapes.Block(PrimitiveType.Cube, "Wall", area, middle, size, Palette.Lit(Palette.Wall), true, angles);
    }

    // A door on a flat face (a building side) at facePoint, opening toward outward.
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
            Vector3 middle = (from + to) * 0.5f + Vector3.up * 0.08f;
            Vector3 size = new Vector3(0.12f, 0.12f, along.magnitude + 0.12f);
            Vector3 angles = Quaternion.LookRotation(along).eulerAngles;
            Shapes.Block(PrimitiveType.Cube, "Rail", rails, middle - side, size, railMaterial, false, angles);
            Shapes.Block(PrimitiveType.Cube, "Rail", rails, middle + side, size, railMaterial, false, angles);
        }
    }
}
