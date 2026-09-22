// WaveSpawner.cs
// ---------------------------------------------------------------------------
// Runs the waves and creates the zombies.
//   - Wave n spawns zombiesBase + zombiesPerWave x n zombies (4 + 2n by default),
//     one every spawnInterval seconds.
//   - Every wave after the first is a little faster: zombies gain
//     zombieSpeedPerWave, and the spawn interval shrinks by spawnIntervalPerWave
//     (but never below 0.8 seconds).
//   - A "Wave N" banner shows for 3 seconds before each wave.
//   - A wave ends when all of its zombies are dead or removed.
//   - Clearing the last wave wins the game.
//
// It also keeps the list of alive zombies, which TypingController (targeting)
// and Zombie (blast) both read.
// ---------------------------------------------------------------------------
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WaveSpawner : MonoBehaviour
{
    [Header("Waves")]
    [SerializeField] private int waveCount = 5;
    [SerializeField] private int zombiesBase = 4;               // wave n spawns zombiesBase + zombiesPerWave x n
    [SerializeField] private int zombiesPerWave = 2;
    [SerializeField] private float spawnInterval = 2.0f;        // seconds between spawns in wave 1
    [SerializeField] private float spawnIntervalPerWave = 0.2f; // seconds SUBTRACTED for each later wave

    [Header("Zombies")]
    [SerializeField] private float zombieSpeed = 1.6f;          // metres per second in wave 1
    [SerializeField] private float zombieSpeedPerWave = 0.15f;  // speed ADDED for each later wave

    [Header("Spawn area (in front of the player)")]
    [SerializeField] private float spawnDistanceMin = 35f;
    [SerializeField] private float spawnDistanceMax = 45f;
    [SerializeField] private float spawnLaneHalfWidth = 7f;     // zombies spawn at X from -this to +this

    [Header("References (wired by the scene builder)")]
    [SerializeField] private Zombie zombiePrefab;
    [SerializeField] private Transform player;
    [SerializeField] private HUD hud;

    private const float MinSpawnInterval = 0.8f; // the spawn interval never goes below this
    private const float BannerSeconds = 3f;      // how long the "Wave N" banner stays up

    private readonly List<Zombie> aliveZombies = new List<Zombie>();

    // Every zombie that is currently alive.
    public List<Zombie> AliveZombies
    {
        get { return aliveZombies; }
    }

    private void Start()
    {
        hud.SetWave(1, waveCount);
    }

    // Called by GameManager when the game starts.
    public void BeginWaves()
    {
        StartCoroutine(RunWaves());
    }

    // Called by GameManager when the player dies.
    public void StopWaves()
    {
        StopAllCoroutines();
    }

    // Called by a zombie when it dies or reaches the player.
    public void RemoveZombie(Zombie zombie)
    {
        aliveZombies.Remove(zombie);
    }

    // A coroutine: it runs over many frames. Every "yield return" pauses it here
    // and Unity continues it later (after the wait is over, or on the next frame).
    private IEnumerator RunWaves()
    {
        for (int wave = 1; wave <= waveCount; wave++)
        {
            // 1. Announce the wave.
            hud.SetWave(wave, waveCount);
            hud.ShowBanner("Wave " + wave);
            yield return new WaitForSeconds(BannerSeconds);
            hud.HideBanner();

            // 2. Work out this wave's numbers. Wave 1 uses the base values.
            int zombiesThisWave = zombiesBase + zombiesPerWave * wave;
            float speed = zombieSpeed + zombieSpeedPerWave * (wave - 1);
            float interval = spawnInterval - spawnIntervalPerWave * (wave - 1);
            if (interval < MinSpawnInterval)
            {
                interval = MinSpawnInterval;
            }

            // 3. Spawn the zombies one at a time.
            for (int i = 0; i < zombiesThisWave; i++)
            {
                SpawnZombie(speed);
                yield return new WaitForSeconds(interval);
            }

            // 4. Wait until every zombie of this wave is dead or removed.
            while (aliveZombies.Count > 0)
            {
                yield return null; // wait one frame, then check again
            }
        }

        GameManager.Instance.WinGame();
    }

    private void SpawnZombie(float speed)
    {
        // Somewhere ahead of the player, at a random spot across the corridor.
        float x = Random.Range(-spawnLaneHalfWidth, spawnLaneHalfWidth);
        float z = player.position.z + Random.Range(spawnDistanceMin, spawnDistanceMax);
        Vector3 position = new Vector3(x, 0f, z);

        // Collect the first letters already in use, so WordBank can avoid them.
        List<char> usedFirstLetters = new List<char>();
        foreach (Zombie alive in aliveZombies)
        {
            usedFirstLetters.Add(alive.Word[0]);
        }
        string word = WordBank.PickWord(usedFirstLetters);

        Zombie zombie = Instantiate(zombiePrefab, position, Quaternion.identity);
        zombie.Setup(word, speed, player, this);
        aliveZombies.Add(zombie);
    }
}
