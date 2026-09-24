// CameraDirector.cs
// ---------------------------------------------------------------------------
// The player's "head". Lives on the Main Camera, which is a child of the
// Player rig at eye height. RailMover turns the whole rig toward where it
// rides and toward each fight; this script adds, on top of that (in the
// camera's LOCAL rotation, position and field of view):
//
//   - FOCUS: like in The Typing of the Dead, the view turns a little toward
//     the enemy whose word is being typed (clamped, so it never swings too
//     far) and glides back to straight ahead when there is nothing to type.
//     With no word started, a zombie that comes close (idleFocusDistance)
//     pulls the view gently toward it, so the player notices the threat.
//   - SHAKE:     CameraDirector.Shake(0.5f);   explosions, hits ("trauma" 0..1)
//   - KICK:      CameraDirector.Kick(1f);      every shot tips the view up a bit
//   - a running head-bob between fights, and a slow breathing sway all the time.
//   - on the Start panel and the results screens the view drifts slowly
//     left and right, so the scene behind the panel feels alive.
//
// Any script can call the two static methods above. They do nothing if
// there is no CameraDirector in the scene.
//
// Everything happens in LateUpdate, after every Update has moved the rig and
// the enemies. DefaultExecutionOrder(-100) makes it run BEFORE the other
// LateUpdates, so the words on the HUD use this frame's view.
//
// Pause: everything moves with Time.deltaTime, which is 0 while the game is
// paused, so the view holds perfectly still behind the pause panel.
// ---------------------------------------------------------------------------
using UnityEngine;

[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(Camera))]
public class CameraDirector : MonoBehaviour
{
    [Header("Focus (turn toward the word being typed)")]
    [SerializeField] private float maxFocusYaw = 16f;         // degrees left/right, at most (before focusStrength); small, so zombies on the other side stay on screen
    [SerializeField] private float maxFocusPitchDown = 12f;   // degrees down, at most
    [SerializeField] private float maxFocusPitchUp = 22f;     // degrees up, at most (looking up at the tall boss)
    [SerializeField] private float focusStrength = 0.75f;     // 1 = put the target dead centre, 0 = never turn
    [SerializeField] private float focusSmoothTime = 0.22f;   // seconds; higher = slower, softer turns
    [SerializeField] private float focusLingerSeconds = 0.3f; // keep looking at a finished word this long (its last bullet is flying)

    [Header("Idle focus (no word started)")]
    [SerializeField] private float idleFocusDistance = 10f;   // metres: a zombie closer than this draws the eye
    [SerializeField] private float idleFocusStrength = 0.35f; // a gentle turn, not a full one

    [Header("Drift on the Start panel and results screens")]
    [SerializeField] private float idleDriftDegrees = 4f;     // how far the view wanders left and right
    [SerializeField] private float idleDriftSpeed = 0.05f;    // back-and-forth wanders per second

    [Header("Shake")]
    [SerializeField] private float maxShakeAngle = 5f;        // degrees of yaw and pitch at full trauma
    [SerializeField] private float maxShakeRoll = 3f;         // degrees of roll at full trauma
    [SerializeField] private float maxShakeOffset = 0.06f;    // metres of movement at full trauma
    [SerializeField] private float shakeFrequency = 22f;      // how fast it jitters
    [SerializeField] private float traumaDecay = 1.6f;        // trauma lost per second (at 1.6, a full shake of 1 is gone after about 0.6 s)

    [Header("Kick (every shot)")]
    [SerializeField] private float recoilReturnTime = 0.12f;  // seconds for the view to settle back after a kick
    [SerializeField] private float maxRecoil = 4f;            // degrees: fast typing never tips the view further

    [Header("Field of view")]
    [SerializeField] private float baseFieldOfView = 65f;     // degrees, top to bottom of the screen

    [Header("Head bob (while running between fights) and breathing")]
    [SerializeField] private float bobHeight = 0.06f;         // metres up and down
    [SerializeField] private float bobFrequency = 2.8f;       // bobs per second (one per step: a running pace)
    [SerializeField] private float bobRollDegrees = 1f;       // the head tilts left, then right, every two steps
    [SerializeField] private float bobFadeSeconds = 0.5f;     // time for the bob to fade in / out when starting / stopping
    [SerializeField] private float breathingDegrees = 0.25f;  // tiny sway that never stops
    [SerializeField] private float breathingFrequency = 0.25f; // breaths per second

    // The camera does not draw anything closer than this (metres).
    private const float NearClip = 0.1f;
    // Nothing further away than this is drawn (the whole USC block and the streets around it fit).
    private const float FarClip = 400f;

    private const float TwoPi = Mathf.PI * 2f;

    // The CameraDirector in the scene, used by the static methods (null if none).
    private static CameraDirector current;

    private Camera cam;
    private Vector3 basePosition;        // the eye position (local) set in the scene
    private TypingController typing;     // tells us which word is being typed
    private WaveSpawner spawner;         // tells us where the zombies are
    private RailMover rail;              // on the Player rig: tells us when we are riding

    // Focus: where the head points now, in degrees relative to the rig
    // (pitch > 0 = looking UP), and how fast it is turning (for SmoothDampAngle).
    private float focusYaw;
    private float focusPitch;
    private float focusYawVelocity;
    private float focusPitchVelocity;
    private Vector3 lingerPoint;         // the last target's position, looked at for a moment after it is gone
    private float lingerTimer;

    private float shakeTrauma;           // 0..1; the shake strength is trauma x trauma
    private float shakeTime;             // runs with game time, feeds the noise
    private float recoil;                // degrees the view is tipped up right now
    private float recoilVelocity;
    private float rideWeight;            // 0 = standing still, 1 = riding: scales the head-bob
    private float bobTime;
    private float breathTime;

    // Adds screen shake. trauma: 0..1 (0.2 = a bump, 0.6 = a close explosion, 1 = huge).
    public static void Shake(float trauma)
    {
        if (current == null)
        {
            return;
        }
        current.shakeTrauma = Mathf.Clamp01(current.shakeTrauma + Mathf.Max(0f, trauma));
    }

    // A shot's kick: tips the view up by this many degrees, then it settles back.
    public static void Kick(float degrees)
    {
        if (current == null)
        {
            return;
        }
        current.recoil = Mathf.Clamp(current.recoil + degrees, -current.maxRecoil, current.maxRecoil);
    }

    private void Awake()
    {
        current = this;

        cam = GetComponent<Camera>();
        cam.nearClipPlane = NearClip;
        cam.farClipPlane = FarClip;
        cam.fieldOfView = baseFieldOfView;

        basePosition = transform.localPosition; // eye height from the scene (0, 1.7, 0)
        rail = GetComponentInParent<RailMover>();
    }

    private void Start()
    {
        // Found once: both live in the scene from the start.
        typing = Object.FindFirstObjectByType<TypingController>();
        spawner = Object.FindFirstObjectByType<WaveSpawner>();
    }

    private void OnDestroy()
    {
        if (current == this)
        {
            current = null;
        }
    }

    private void LateUpdate()
    {
        float deltaTime = Time.deltaTime; // 0 while paused: the view holds still
        bool playing = GameManager.Instance != null && GameManager.Instance.State == GameState.Playing;

        // ---- 1. Focus: where should the head point? ----
        float wantedYaw;
        float wantedPitch;
        if (playing)
        {
            ChooseFocus(deltaTime, out wantedYaw, out wantedPitch);
        }
        else
        {
            // Start panel, pause, results: straight ahead, slowly looking around.
            // Real time (unscaled) so the drift never jumps, whatever the time scale.
            float wave = Time.unscaledTime * idleDriftSpeed * TwoPi;
            wantedYaw = Mathf.Sin(wave) * idleDriftDegrees;
            wantedPitch = Mathf.Sin(wave * 0.7f + 1.3f) * idleDriftDegrees * 0.3f;
            lingerTimer = 0f;
        }

        // Glide toward it, never snap. (SmoothDampAngle must not run with a frame
        // time of 0, it would divide by zero, so it is skipped while paused.)
        if (deltaTime > 0f)
        {
            focusYaw = Mathf.SmoothDampAngle(focusYaw, wantedYaw, ref focusYawVelocity, focusSmoothTime, Mathf.Infinity, deltaTime);
            focusPitch = Mathf.SmoothDampAngle(focusPitch, wantedPitch, ref focusPitchVelocity, focusSmoothTime, Mathf.Infinity, deltaTime);
        }

        // ---- 2. Shake and kick fade out ----
        shakeTrauma = Mathf.Max(0f, shakeTrauma - traumaDecay * deltaTime);
        shakeTime += deltaTime;
        recoil = SpringToZero(recoil, ref recoilVelocity, recoilReturnTime, deltaTime);

        // Shake strength grows with trauma squared: small bumps stay subtle,
        // big blasts really rattle. Perlin noise gives a smooth random jitter.
        float shake = shakeTrauma * shakeTrauma;
        float shakeYaw = 0f;
        float shakePitch = 0f;
        float shakeRoll = 0f;
        Vector3 shakeOffset = Vector3.zero;
        if (shake > 0f)
        {
            shakeYaw = Noise(1.7f) * maxShakeAngle * shake;
            shakePitch = Noise(23.3f) * maxShakeAngle * shake;
            shakeRoll = Noise(47.9f) * maxShakeRoll * shake;
            shakeOffset = new Vector3(Noise(71.3f), Noise(93.1f), 0f) * (maxShakeOffset * shake);
        }

        // ---- 3. Head bob while riding (fades in and out), breathing always ----
        bool riding = playing && rail != null && rail.IsRiding;
        float fadeSpeed = 1f / Mathf.Max(0.01f, bobFadeSeconds);
        rideWeight = Mathf.MoveTowards(rideWeight, riding ? 1f : 0f, fadeSpeed * deltaTime);
        if (rideWeight > 0f)
        {
            bobTime += deltaTime;
        }
        float bobHeightNow = Mathf.Sin(bobTime * bobFrequency * TwoPi) * bobHeight * rideWeight;
        float bobRoll = Mathf.Sin(bobTime * bobFrequency * Mathf.PI) * bobRollDegrees * rideWeight; // half as fast: left step, right step

        breathTime += deltaTime;
        float breathPitch = Mathf.Sin(breathTime * breathingFrequency * TwoPi) * breathingDegrees;
        float breathYaw = Mathf.Sin(breathTime * breathingFrequency * Mathf.PI + 1f) * breathingDegrees * 0.6f;

        // ---- 4. Put it all together ----
        // Unity's X rotation looks DOWN when positive, so the "up" angles are negated.
        float yaw = focusYaw + breathYaw + shakeYaw;
        float pitchUp = focusPitch + recoil + breathPitch + shakePitch;
        float roll = bobRoll + shakeRoll;
        transform.localRotation = Quaternion.Euler(-pitchUp, yaw, roll);
        transform.localPosition = basePosition + Vector3.up * bobHeightNow + transform.localRotation * shakeOffset;
        cam.fieldOfView = baseFieldOfView;
    }

    // ---- Focus ----

    // While playing: the angles (degrees, pitch > 0 = up) the head should turn to.
    private void ChooseFocus(float deltaTime, out float wantedYaw, out float wantedPitch)
    {
        wantedYaw = 0f;   // nothing to look at: straight ahead
        wantedPitch = 0f;

        // A word is being typed: look toward its enemy.
        ITypingTarget target = null;
        if (typing != null)
        {
            target = typing.CurrentTarget;
        }
        if (target != null && target.IsAlive)
        {
            lingerPoint = target.HitPoint;
            lingerTimer = focusLingerSeconds;
            AnglesToward(lingerPoint, focusStrength, out wantedYaw, out wantedPitch);
            return;
        }

        // The word was just finished (or dropped): keep looking there a moment,
        // so the view does not leave before the final bullet lands.
        if (lingerTimer > 0f)
        {
            lingerTimer -= deltaTime;
            AnglesToward(lingerPoint, focusStrength, out wantedYaw, out wantedPitch);
            return;
        }

        // No word started: a close zombie draws the eye a little.
        Zombie closest = ClosestZombie();
        if (closest != null)
        {
            AnglesToward(closest.HitPoint, idleFocusStrength, out wantedYaw, out wantedPitch);
        }
    }

    // The yaw and pitch (degrees, pitch > 0 = up) that turn the head toward
    // worldPoint, measured from the way the Player rig faces, clamped to the
    // limits and scaled by strength (0..1).
    private void AnglesToward(Vector3 worldPoint, float strength, out float yaw, out float pitch)
    {
        // The direction to the point, turned into the rig's own space
        // (x = right, y = up, z = the way the rig faces).
        Vector3 direction = Quaternion.Inverse(ParentRotation()) * (worldPoint - EyePosition());
        float flatDistance = new Vector2(direction.x, direction.z).magnitude;

        yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
        pitch = Mathf.Atan2(direction.y, flatDistance) * Mathf.Rad2Deg;

        yaw = Mathf.Clamp(yaw, -maxFocusYaw, maxFocusYaw) * strength;
        pitch = Mathf.Clamp(pitch, -maxFocusPitchDown, maxFocusPitchUp) * strength;
    }

    // The alive zombie closest to the eye within idleFocusDistance, or null.
    private Zombie ClosestZombie()
    {
        if (spawner == null)
        {
            return null;
        }

        Vector3 eye = EyePosition();
        Zombie closest = null;
        float closestDistance = idleFocusDistance;
        foreach (Zombie zombie in spawner.AliveZombies)
        {
            if (zombie == null || !zombie.IsAlive)
            {
                continue;
            }
            float distance = Vector3.Distance(eye, zombie.HitPoint);
            if (distance < closestDistance)
            {
                closest = zombie;
                closestDistance = distance;
            }
        }
        return closest;
    }

    // ---- Small helpers ----

    // The rotation of the Player rig (the camera's parent), or none.
    private Quaternion ParentRotation()
    {
        if (transform.parent != null)
        {
            return transform.parent.rotation;
        }
        return Quaternion.identity;
    }

    // Where the eye is in the world, without bob or shake.
    private Vector3 EyePosition()
    {
        if (transform.parent != null)
        {
            return transform.parent.TransformPoint(basePosition);
        }
        return basePosition;
    }

    // Smooth random number from -1 to 1. Each seed gives a different wobble.
    private float Noise(float seed)
    {
        return Mathf.PerlinNoise(shakeTime * shakeFrequency, seed) * 2f - 1f;
    }

    // Moves value back to 0 like a stiff spring that does not bounce
    // ("critically damped"): it is almost back after 'seconds'. velocity keeps
    // the speed between frames. Holds still while paused (deltaTime 0).
    private static float SpringToZero(float value, ref float velocity, float seconds, float deltaTime)
    {
        if (deltaTime <= 0f)
        {
            return value;
        }
        // SmoothDamp's smoothTime is about half the time it needs to settle.
        return Mathf.SmoothDamp(value, 0f, ref velocity, seconds * 0.5f, Mathf.Infinity, deltaTime);
    }
}
