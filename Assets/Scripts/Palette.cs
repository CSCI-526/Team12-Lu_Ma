// Palette.cs
// ---------------------------------------------------------------------------
// Every colour in the game, and every material, comes from here. The game is
// a GREYBOX prototype on purpose: no textures, no images, only flat colours on
// Unity's primitive shapes (cubes, spheres, capsules, cylinders).
//
//   Palette.Lit(color)   - a matte material lit by the scene's light (almost everything).
//   Palette.Unlit(color) - a material that ignores light and always shows its
//                          exact colour (only the flat blast rings and bullets).
//
// A material is created once per colour and then shared, which is faster to
// draw than one material per object. Never change the colour of a shared
// material: every object using it would change. To recolour one object, give
// it another shared material, e.g. renderer.sharedMaterial = Palette.Lit(red).
// ---------------------------------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

public static class Palette
{
    // ---- The level: neutral greys and blues ----
    public static readonly Color Floor = new Color(0.20f, 0.26f, 0.38f);   // the ground and the streets
    public static readonly Color Pavement = new Color(0.29f, 0.33f, 0.43f); // sidewalks, plazas and walks (a bit lighter)
    public static readonly Color Lawn = new Color(0.16f, 0.21f, 0.29f);     // lawns and planting beds (a bit darker)
    public static readonly Color Rail = new Color(0.62f, 0.68f, 0.78f);
    public static readonly Color Wall = new Color(0.42f, 0.45f, 0.52f);
    public static readonly Color Building = new Color(0.30f, 0.40f, 0.58f);
    public static readonly Color Block = new Color(0.36f, 0.38f, 0.44f);   // the buildings across the streets, statues, pedestals
    public static readonly Color Door = new Color(0.22f, 0.25f, 0.32f);
    public static readonly Color Doorway = new Color(0.06f, 0.07f, 0.09f); // the dark hole behind an open door
    public static readonly Color Gate = new Color(0.55f, 0.58f, 0.64f);

    // ---- Zombies (colour coded) ----
    public static readonly Color ZombieGrey = new Color(0.80f, 0.82f, 0.85f);
    public static readonly Color RunnerYellow = new Color(0.90f, 0.85f, 0.30f);
    public static readonly Color ArmorSteel = new Color(0.35f, 0.42f, 0.55f);
    public static readonly Color FrozenIce = new Color(0.60f, 0.88f, 1.00f);

    // ---- Props ----
    public static readonly Color BarrelRed = new Color(0.75f, 0.12f, 0.08f);
    public static readonly Color HealthCrate = new Color(0.90f, 0.90f, 0.90f);
    public static readonly Color LureCrate = new Color(1.00f, 0.50f, 0.10f);
    public static readonly Color FreezeCrate = new Color(0.30f, 0.70f, 1.00f);
    public static readonly Color Bomb = new Color(0.10f, 0.10f, 0.10f);
    public static readonly Color BombBlink = new Color(0.90f, 0.10f, 0.10f);
    public static readonly Color QuizAnswer = new Color(0.40f, 0.90f, 1.00f);
    public static readonly Color BlastOrange = new Color(1.00f, 0.55f, 0.10f);

    // ---- Word colours (HUD text) ----
    public static readonly Color WordBarrel = new Color(1.00f, 0.60f, 0.20f);
    public static readonly Color WordCrate = new Color(0.45f, 1.00f, 0.50f);
    public static readonly Color WordQuiz = new Color(0.40f, 0.90f, 1.00f);
    public static readonly Color WordArmor = new Color(0.75f, 0.80f, 0.90f);

    private static readonly Dictionary<Color, Material> litMaterials = new Dictionary<Color, Material>();
    private static readonly Dictionary<Color, Material> unlitMaterials = new Dictionary<Color, Material>();

    // A matte material lit by the scene's light.
    public static Material Lit(Color color)
    {
        Material material;
        // "!= null" also catches a material Unity has destroyed in the meantime.
        if (litMaterials.TryGetValue(color, out material) && material != null)
        {
            return material;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }
        material = new Material(shader);
        material.name = "Lit " + color;
        material.color = color;
        if (material.HasProperty("_Smoothness"))
        {
            material.SetFloat("_Smoothness", 0f); // matte: no shiny highlights
        }
        litMaterials[color] = material;
        return material;
    }

    // A material that ignores lighting: it always shows exactly its colour.
    public static Material Unlit(Color color)
    {
        Material material;
        if (unlitMaterials.TryGetValue(color, out material) && material != null)
        {
            return material;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }
        material = new Material(shader);
        material.name = "Unlit " + color;
        material.color = color;
        unlitMaterials[color] = material;
        return material;
    }
}
