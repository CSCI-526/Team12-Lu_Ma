// TypingController.cs
// ---------------------------------------------------------------------------
// The ONLY input in the game: reads the keyboard and turns letters into kills.
//
// Rules:
//   - No target yet: the typed letter picks the NEAREST alive zombie whose word
//     starts with that letter, and counts as that word's first letter.
//     If no zombie matches, it is a wrong key.
//   - Target exists: the next expected letter advances the word. Any other
//     letter is a wrong key.
//   - A wrong key never resets progress. It flashes the screen red and resets
//     the combo.
//   - Backspace or Escape drops the current target so the player can retarget.
//   - Finishing the word makes the zombie explode (see Zombie.Explode).
//   - Enter starts the game from the Start panel, and restarts it from the
//     "You survived" / "You died" panels.
//
// INPUT BACKEND: this project uses Unity's Input System package
// (Project Settings > Player > Active Input Handling), so typed characters come
// from Keyboard.current.onTextInput. If that setting is ever switched to the old
// Input Manager, the #else branches below use Input.inputString instead.
// ---------------------------------------------------------------------------
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class TypingController : MonoBehaviour
{
    [Header("References (wired by the scene builder)")]
    [SerializeField] private WaveSpawner spawner;
    [SerializeField] private Transform player;
    [SerializeField] private HUD hud;

    // The zombie whose word is currently being typed (null = no target).
    private Zombie target;

#if ENABLE_INPUT_SYSTEM
    private Keyboard keyboard;        // the keyboard we are listening to
    private string pendingText = "";  // characters typed since the last Update

    // The Input System calls this once for every character the player types.
    // We only store the character here; Update() handles it.
    private void OnTextInput(char character)
    {
        pendingText += character;
    }

    private void OnDisable()
    {
        if (keyboard != null)
        {
            keyboard.onTextInput -= OnTextInput;
            keyboard = null;
        }
    }
#endif

    private void Update()
    {
        // ---- 1. Read the keyboard ----
        string typedText;
        bool enterPressed;
        bool dropPressed; // Backspace or Escape

#if ENABLE_INPUT_SYSTEM
        // Keyboard.current can be missing on the very first frames, or change if a
        // keyboard is plugged in, so make sure we listen to the current one.
        if (keyboard != Keyboard.current)
        {
            if (keyboard != null)
            {
                keyboard.onTextInput -= OnTextInput;
            }
            keyboard = Keyboard.current;
            if (keyboard != null)
            {
                keyboard.onTextInput += OnTextInput;
            }
        }

        typedText = pendingText;
        pendingText = "";
        enterPressed = keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame);
        dropPressed = keyboard != null && (keyboard.backspaceKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame);
#else
        typedText = Input.inputString;
        enterPressed = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
        dropPressed = Input.GetKeyDown(KeyCode.Backspace) || Input.GetKeyDown(KeyCode.Escape);
#endif

        // ---- 2. Outside of play, only Enter does something ----
        if (GameManager.Instance.State != GameState.Playing)
        {
            if (enterPressed)
            {
                GameManager.Instance.StartOrRestart();
            }
            return;
        }

        // ---- 3. Forget a target that is gone (it reached the player) ----
        if (target != null && !target.IsAlive)
        {
            target = null;
        }

        // ---- 4. Backspace / Escape: drop the target ----
        if (dropPressed && target != null)
        {
            target.ResetProgress();
            target = null;
        }

        // ---- 5. Handle every letter typed this frame ----
        foreach (char character in typedText)
        {
            char letter = char.ToUpperInvariant(character);
            if (letter < 'A' || letter > 'Z')
            {
                continue; // not a letter (space, digit, Enter, ...): ignore it
            }
            HandleLetter(letter);
        }

        // ---- 6. Show the current target's word at the bottom of the screen ----
        if (target != null)
        {
            hud.SetTargetWord(target.ColoredWord);
        }
        else
        {
            hud.SetTargetWord("");
        }
    }

    private void HandleLetter(char letter)
    {
        // No target yet: this letter has to select one.
        if (target == null)
        {
            target = FindNearestZombieStartingWith(letter);
            if (target == null)
            {
                WrongKey();
                return;
            }
            // Carry on below: the selecting letter also counts as the word's first letter.
        }

        if (letter == target.NextLetter)
        {
            target.AdvanceProgress();
            if (target.IsWordComplete)
            {
                target.Explode();
                target = null;
            }
        }
        else
        {
            WrongKey();
        }
    }

    private void WrongKey()
    {
        // Progress is NOT reset; the player only loses the combo.
        GameManager.Instance.ResetCombo();
        hud.FlashRed();
    }

    // Returns the alive zombie closest to the player whose word starts with
    // the given letter, or null if there is none.
    private Zombie FindNearestZombieStartingWith(char letter)
    {
        Zombie nearest = null;
        float nearestDistance = float.MaxValue;

        foreach (Zombie zombie in spawner.AliveZombies)
        {
            if (zombie.Word[0] != letter)
            {
                continue;
            }

            float distance = Vector3.Distance(player.position, zombie.transform.position);
            if (distance < nearestDistance)
            {
                nearest = zombie;
                nearestDistance = distance;
            }
        }

        return nearest;
    }
}
