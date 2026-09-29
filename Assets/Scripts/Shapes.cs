// Shapes.cs
// ---------------------------------------------------------------------------
// One helper for building things out of Unity's primitive shapes while the
// game runs. The boss, its quiz answers, the bullets, the lure bomb and the
// effects are made with it (the zombies, the map, the props and the route
// arrows are prefabs or scene objects instead):
//
//   Shapes.Block(PrimitiveType.Cube, "Box", parent, position, size, Palette.Lit(Palette.Bomb));
//
// makes a cube called "Box" under parent, at position (local to the parent),
// scaled to size, with that material. Every primitive comes with a collider;
// it is removed, because things built here never need one: gameplay never uses
// physics for hits (they are measured with distances). The only colliders that
// matter are the map's and the props' in the scene: dead zombies tumble on
// them, and a lure bomb throw stops short of them (Powers.LandingPoint).
// ---------------------------------------------------------------------------
using UnityEngine;

public static class Shapes
{
    public static GameObject Block(PrimitiveType shape, string name, Transform parent,
        Vector3 localPosition, Vector3 localScale, Material material,
        Vector3 localEulerAngles = default(Vector3))
    {
        GameObject block = GameObject.CreatePrimitive(shape);
        block.name = name;
        // DestroyImmediate: the collider must be gone now, not at the end of the frame.
        Object.DestroyImmediate(block.GetComponent<Collider>());
        block.transform.SetParent(parent, false);
        block.transform.localPosition = localPosition;
        block.transform.localEulerAngles = localEulerAngles;
        block.transform.localScale = localScale;
        block.GetComponent<Renderer>().sharedMaterial = material;
        return block;
    }
}
