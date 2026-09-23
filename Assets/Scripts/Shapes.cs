// Shapes.cs
// ---------------------------------------------------------------------------
// One helper for building things out of Unity's primitive shapes. The level,
// the zombies, the boss and the props are all made with it:
//
//   Shapes.Block(PrimitiveType.Cube, "Crate", parent, position, size, Palette.Lit(Palette.Block));
//
// makes a cube called "Crate" under parent, at position (local to the parent),
// scaled to size, with that material. Every primitive comes with a collider;
// it is removed unless keepCollider is true. Keep colliders on floors, walls
// and big props (dead zombies tumble on them, and a lure bomb throw stops
// short of them); remove them on everything else, because gameplay never
// uses physics (hits are measured with distances).
// ---------------------------------------------------------------------------
using UnityEngine;

public static class Shapes
{
    public static GameObject Block(PrimitiveType shape, string name, Transform parent,
        Vector3 localPosition, Vector3 localScale, Material material,
        bool keepCollider = false, Vector3 localEulerAngles = default(Vector3))
    {
        GameObject block = GameObject.CreatePrimitive(shape);
        block.name = name;
        if (!keepCollider)
        {
            // DestroyImmediate: the collider must be gone now, not at the end of the frame.
            Object.DestroyImmediate(block.GetComponent<Collider>());
        }
        block.transform.SetParent(parent, false);
        block.transform.localPosition = localPosition;
        block.transform.localEulerAngles = localEulerAngles;
        block.transform.localScale = localScale;
        block.GetComponent<Renderer>().sharedMaterial = material;
        return block;
    }
}
