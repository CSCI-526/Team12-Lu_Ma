// ChainLink.cs
// ---------------------------------------------------------------------------
// The purple line between the two zombies of a WORD CHAIN pair (e.g. "hunt"
// and "hunter", spawned together by WaveSpawner.SpawnChainPair). It shows the
// player that the two words belong together: typing the long one kills both.
//
//   ChainLink.Create(shortZombie, longZombie);
//
// The line runs from one zombie's chest to the other's every frame and
// disappears as soon as either of them is gone.
// ---------------------------------------------------------------------------
using UnityEngine;

public class ChainLink : MonoBehaviour
{
    private const float Width = 0.06f; // metres

    private Zombie first;
    private Zombie second;
    private LineRenderer line;

    public static ChainLink Create(Zombie first, Zombie second)
    {
        GameObject linkObject = new GameObject("ChainLink");
        ChainLink link = linkObject.AddComponent<ChainLink>();
        link.first = first;
        link.second = second;

        link.line = linkObject.AddComponent<LineRenderer>();
        link.line.useWorldSpace = true;
        link.line.positionCount = 2;
        link.line.startWidth = Width;
        link.line.endWidth = Width;
        link.line.numCapVertices = 2;
        link.line.sharedMaterial = Palette.Unlit(Palette.WordChain);
        link.line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        link.line.receiveShadows = false;
        link.LateUpdate(); // in place from the very first frame
        return link;
    }

    private void LateUpdate()
    {
        // "== null" also catches a zombie Unity has already destroyed.
        if (first == null || second == null || !first.IsAlive || !second.IsAlive)
        {
            Destroy(gameObject);
            return;
        }

        line.SetPosition(0, first.HitPoint);
        line.SetPosition(1, second.HitPoint);
    }
}
