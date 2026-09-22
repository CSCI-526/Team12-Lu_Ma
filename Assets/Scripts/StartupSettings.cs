// StartupSettings.cs
// ---------------------------------------------------------------------------
// One-time settings applied when the scene loads. They matter for the Web build:
//   - runInBackground: keep the game running when the browser tab loses focus.
//   - captureAllKeyboardInput: let Unity receive every key press on the page,
//     not only when the canvas element itself has focus.
// ---------------------------------------------------------------------------
using UnityEngine;

public class StartupSettings : MonoBehaviour
{
    private void Awake()
    {
        Application.runInBackground = true;

        // WebGLInput only exists inside a real Web build (it is not part of the
        // Editor), so this line must be compiled out everywhere else.
#if UNITY_WEBGL && !UNITY_EDITOR
        WebGLInput.captureAllKeyboardInput = true;
#endif
    }
}
