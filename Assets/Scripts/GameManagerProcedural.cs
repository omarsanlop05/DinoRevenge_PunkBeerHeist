using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManagerProcedural : MonoBehaviour
{
    public static GameManagerProcedural instance;

    [Header("UI")]
    public GameObject deathScreen;
    public GameObject victoryScreen;

    [Header("World")]
    [Tooltip("Extra width (tiles) reserved to the right of the last room for the hallway and the boss room")]
    [SerializeField] private int worldExtraWidth = 120;
    [SerializeField] private bool placePlayerInFirstRoom = true;

    [Header("Room sequence")]
    [SerializeField] private RoomGenerator roomGenerator;
    [Tooltip("Rooms generated when the scene starts. Entering room i generates room i + (initialRooms - 1)")]
    [SerializeField] private int initialRooms = 2;
    [Tooltip("Total procedural rooms before the hallway / boss")]
    [SerializeField] private int totalRooms = 7;
    [Tooltip("Clears Tilemaps and generated rooms before starting (removes leftovers from the context menu tests)")]
    [SerializeField] private bool clearBeforeGenerating = true;
    [SerializeField] private bool useRandomSeed = true;
    [SerializeField] private int baseSeed = 1;

    // Rooms 1 and 2 are built when the scene starts, before the player has done anything,
    // so secret rooms can only go in rooms 3..totalRooms. That gives at most totalRooms - 2.
    private const int FirstSecretRoom = 3;

    [Header("Secret Rooms (rooms 3 to totalRooms; at most totalRooms - 2 per run)")]
    [Tooltip("Secret rooms every run gets at least. When the rooms left are just enough, the next ones are forced")]
    [SerializeField] private int minSecretRooms = 2;
    [Tooltip("Minimum hits received so far to be eligible, for rooms 3, 4, 5, 6 and 7 (in that order; the last " +
             "value is used for any later room). Rooms are generated one room ahead, so room 3 is decided when " +
             "entering room 2 (only room 1 was played)")]
    [SerializeField] private int[] secretMinHits = { 0, 0, 1, 1, 2 };
    [Tooltip("A room is also eligible when ANY of the three scores (speed, health, power) is at or below this, " +
             "even without enough hits")]
    [SerializeField] private float secretScoreThreshold = -25f;
    [Tooltip("Testing only: every room 3+ tries to get a secret room, ignoring hits and chance (the maximum still applies)")]
    [SerializeField] private bool forceSecretRooms = false;
    [Range(0f, 1f)] [SerializeField] private float secretChanceAtMinHits = 0.85f;
    [Range(0f, 1f)] [SerializeField] private float secretChancePerExtraHit = 0.1f;
    [Tooltip("Chance when the room is eligible only because of a low score")]
    [Range(0f, 1f)] [SerializeField] private float secretChanceLowScore = 0.9f;
    [Range(0f, 1f)] [SerializeField] private float secretMaxChance = 1f;
    [Tooltip("Chance multiplier applied once for every secret room already generated")]
    [Range(0f, 1f)] [SerializeField] private float secretChanceAfterEach = 0.95f;
    [Tooltip("Extra chance multiplier when the previous room already had a secret room")]
    [Range(0f, 1f)] [SerializeField] private float secretChanceIfPreviousHad = 0.85f;
    private int secretRoomsCount;

    private int MaxSecretRooms => Mathf.Max(0, totalRooms - (FirstSecretRoom - 1));

    [Header("Music")]
    [Tooltip("Level music. SoundManager survives scene reloads, so without this the boss music keeps playing after Retry. " +
             "Empty = uses the clip of the MusicStart object in the scene")]
    [SerializeField] private AudioClip levelMusic;

    [Header("Enemies")]
    [Tooltip("Places minEnemies-maxEnemies (1-4) enemies in every generated room. Leave empty to generate rooms without enemies")]
    [SerializeField] private EnemyPlacer enemyPlacer;

    [Header("Boss")]
    [SerializeField] private BossRoomPrefab bossRoomPrefab;
    private BossRoomPrefab bossRoom;

    [SerializeField] private HallwayPrefab hallwayPrefab;
    private HallwayPrefab hallway;

    private readonly List<RoomData> rooms = new List<RoomData>();

    private void Awake()
    {
        // Simple singleton
        if (instance == null)
            instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }

        deathScreen.SetActive(false);
        victoryScreen.SetActive(false);
    }

    private void Start()
    {
        StartLevelMusic();

        if (roomGenerator == null)
        {
            Debug.LogError("[GameManager] RoomGenerator reference is missing.");
            return;
        }

        if (useRandomSeed)
            baseSeed = Random.Range(0, int.MaxValue / 2);
        Debug.Log($"[GameManager] Run seed: {baseSeed}");

        if (clearBeforeGenerating)
        roomGenerator.ClearAll();

        roomGenerator.FillWorld(totalRooms, worldExtraWidth);   // 1) fill first

        int count = Mathf.Min(initialRooms, totalRooms);
        for (int i = 0; i < count; i++)                          // 2) then carve the rooms
            GenerateNextRoom(1f);

        // Build the tile colliders now, before the first physics step (enemies and player stand on them)
        roomGenerator.RefreshColliders();

        if (placePlayerInFirstRoom && rooms.Count > 0)
            PlacePlayer(rooms[0]);                               // 3) then place the player
    }

    private void OnDestroy()
    {
        foreach (var r in rooms)
        {
            if (r != null && r.entryTrigger != null)
                r.entryTrigger.PlayerEntered -= OnPlayerEnteredRoom;
        }

        if (instance == this) instance = null;
    }

    // ---------- Room sequence ----------

    private void GenerateNextRoom(float multiplier)
    {
        int index = rooms.Count;
        RoomData previous = index > 0 ? rooms[index - 1] : null;

        // Decidir si se hace un cuarto secreto (niveles 3, 4 o 5)
        
        int roomNumber = index + 1;   // 1-based, as in the design (rooms 3, 4, 5)
        bool makeSecret = ShouldMakeSecretRoom(roomNumber, previous);

        RoomData data = roomGenerator.Generate(index, baseSeed + index, previous, multiplier, makeSecret);

        // Snapshot of the three scores: the enemies of this room are built with them
        data.enemyDifficulty = PlayerScore.Instance != null ? PlayerScore.Instance.GetDifficulty() : EnemyDifficulty.Neutral;
        if (data.hasSecretRoom)
            secretRoomsCount++;   // only counts if the generator actually managed to build it
        rooms.Add(data);
        data.entryTrigger.PlayerEntered += OnPlayerEnteredRoom;

        // Enemies go in after the room (and its secret room) is carved, so they see the final tiles
        if (enemyPlacer != null)
            enemyPlacer.PopulateRoom(data, baseSeed + index * 7919 + 101);

        Debug.Log($"[GameManager] Room {index} generated (multiplier {multiplier:F2})");
        if (rooms.Count == totalRooms)
            SpawnHallway(data);
    }

    private bool ShouldMakeSecretRoom(int roomNumber, RoomData previous)
    {
        if (roomNumber < FirstSecretRoom || roomNumber > totalRooms) return false;
        if (secretRoomsCount >= MaxSecretRooms) return false;

        if (forceSecretRooms)
        {
            Debug.Log($"[GameManager] Secret room check for room {roomNumber}: forced (testing)");
            return true;
        }

        // Guarantee: if the rooms left (this one included) are just enough to reach the minimum, it has to be this one.
        // A room whose layout can't fit one doesn't count, so the next room is forced instead.
        int roomsLeft = totalRooms - roomNumber + 1;
        int missing = Mathf.Min(minSecretRooms, MaxSecretRooms) - secretRoomsCount;
        if (missing >= roomsLeft)
        {
            Debug.Log($"[GameManager] Secret room check for room {roomNumber}: forced ({secretRoomsCount} so far, " +
                      $"minimum {minSecretRooms}, {roomsLeft} rooms left)");
            return true;
        }

        if (PlayerScore.Instance == null) return false;

        int slot = roomNumber - FirstSecretRoom;   // room 3 -> 0, room 4 -> 1, ...
        int hits = PlayerScore.Instance.HitsReceived;
        int minHits = secretMinHits != null && secretMinHits.Length > 0
            ? secretMinHits[Mathf.Min(slot, secretMinHits.Length - 1)] : 0;

        float lowest = Mathf.Min(PlayerScore.Instance.GetScore(StatAxis.Speed),
                                 PlayerScore.Instance.GetScore(StatAxis.Health),
                                 PlayerScore.Instance.GetScore(StatAxis.Power));
        bool byHits = hits >= minHits;
        bool byScore = lowest <= secretScoreThreshold;
        if (!byHits && !byScore)
        {
            Debug.Log($"[GameManager] Secret room check for room {roomNumber}: hits {hits} < {minHits} and lowest score " +
                      $"{lowest:0.#} > {secretScoreThreshold:0.#}, not eligible");
            return false;
        }

        float chance = byHits ? secretChanceAtMinHits + (hits - minHits) * secretChancePerExtraHit : 0f;
        if (byScore) chance = Mathf.Max(chance, secretChanceLowScore);
        chance = Mathf.Min(chance, secretMaxChance);
        chance *= Mathf.Pow(secretChanceAfterEach, secretRoomsCount);
        if (previous != null && previous.hasSecretRoom)
            chance *= secretChanceIfPreviousHad;

        float roll = Random.value;
        bool ok = roll < chance;
        Debug.Log($"[GameManager] Secret room check for room {roomNumber}: hits {hits} (min {minHits}), lowest score " +
                  $"{lowest:0.#} (threshold {secretScoreThreshold:0.#}), chance {chance:P0}, roll {roll:F2} -> {ok}");
        return ok;
    }

    // Entering room i means the player completed room i - 1
    private void OnPlayerEnteredRoom(int roomIndex)
    {
        if (roomIndex < 1) return;
        StartCoroutine(ProcessRoomCompleted(roomIndex));
    }

    private IEnumerator ProcessRoomCompleted(int roomIndex)
    {
        // Wait one frame so CameraChanger has already called PlayerScore.RegisterRoomChange()
        // (both triggers live on the same object and their callback order is not guaranteed)
        yield return null;

        float multiplier = 1f;
        if (PlayerScore.Instance != null)
            multiplier = PlayerScore.Instance.CurrentMultiplier;
        else
            Debug.LogWarning("[GameManager] No PlayerScore in the scene, using multiplier 1.");

        Debug.Log($"[GameManager] Room {roomIndex - 1} completed. Multiplier: {multiplier:F2}");

        int target = roomIndex + initialRooms - 1;
        while (rooms.Count <= target && rooms.Count < totalRooms)
            GenerateNextRoom(multiplier);

        if (roomIndex == totalRooms - 1)
            Debug.Log("[GameManager] Last room reached. Hallway and boss room are not implemented yet.");
    }

    // It should use the total score to build the boss room.
    private void SpawnHallway(RoomData last)
{
    if (hallway != null || hallwayPrefab == null) return;

    if (hallwayPrefab.entryAnchor == null || hallwayPrefab.exitAnchor == null ||
        hallwayPrefab.hallwayCamera == null || hallwayPrefab.entryChanger == null ||
        hallwayPrefab.entryTrigger == null)
    {
        Debug.LogError("[GameManager] HallwayPrefab has unassigned fields. Fill them in the prefab.");
        return;
    }

    // The hallway attaches to the last room's exit door
    Vector3Int entryCell = new Vector3Int(last.originCell.x + last.width, last.originCell.y + last.exitRow, 0);
    Vector3 entryWorld = roomGenerator.CellToWorld(entryCell);

    hallway = Instantiate(hallwayPrefab, entryWorld, Quaternion.identity);

    // Shift the instance so its entry anchor lands exactly on entryCell
    Vector3 shift = entryWorld - hallway.entryAnchor.position;
    shift.z = 0f;
    hallway.transform.position += shift;

    roomGenerator.CarveInterior(hallway.gameObject, hallway.entryAnchor.position, hallway.exitAnchor.position);

    hallway.Initialize(last.cam);
    hallway.entryTrigger.PlayerEntered += _ => OnHallwayEntered();
}

    // Called when the player enters the hallway: builds the boss room with the total score
    public void OnHallwayEntered()
    {
        StartCoroutine(SpawnBossAfterScore());
    }

    private IEnumerator SpawnBossAfterScore()
    {
        // Wait one frame so CameraChanger has registered the room change before we read the score
        yield return null;

        float multiplier = PlayerScore.Instance != null ? PlayerScore.Instance.CurrentMultiplier : 1f;

        // The boss room attaches where the hallway ends
        Vector3Int bossEntry = roomGenerator.WorldToCell(hallway.exitAnchor.position);
        SpawnBossRoom(bossEntry, hallway.hallwayCamera, multiplier);
    }

    public void SpawnBossRoom(Vector3Int entryCell, CinemachineCamera previousCamera, float multiplier)
    {
        if (bossRoom != null || bossRoomPrefab == null) return;

if (bossRoomPrefab.entryAnchor == null || bossRoomPrefab.bossCamera == null || bossRoomPrefab.entryChanger == null)
{
    Debug.LogError("[GameManager] BossRoomPrefab has unassigned fields. Fill them in the prefab.");
    return;
}

        Vector3 entryWorld = roomGenerator.CellToWorld(entryCell);

        bossRoom = Instantiate(bossRoomPrefab, entryWorld, Quaternion.identity);

        // Shift the instance so its entry anchor lands exactly on entryCell
        Vector3 shift = entryWorld - bossRoom.entryAnchor.position;
        shift.z = 0f;
        bossRoom.transform.position += shift;

        roomGenerator.CarveInterior(bossRoom.gameObject, bossRoom.entryAnchor.position);

        bossRoom.Initialize(previousCamera, multiplier);
        Debug.Log($"[GameManager] Boss room spawned (multiplier {multiplier:F2})");
    }

    // ---------- Music ----------

    // Runs every time the scene loads (first play and Retry), replacing whatever was playing (boss music)
    private void StartLevelMusic()
    {
        AudioClip clip = levelMusic;
        if (clip == null)
        {
            MusicStart musicStart = FindFirstObjectByType<MusicStart>();
            if (musicStart != null) clip = musicStart.startMusic;
        }

        if (clip == null || SoundManager.instance == null) return;
        SoundManager.instance.gameMusicSource.loop = true;
        SoundManager.instance.playMusic(clip);
    }

    // ---------- Screens and scenes ----------

    public void ShowDeathScreen()
    {
        Time.timeScale = 0f; // Pause game
        deathScreen.SetActive(true);
    }

    public void ShowVictoryScreen()
    {
        Time.timeScale = 0f; // Pause game
        victoryScreen.SetActive(true);
    }

    public void RetryLevel()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void GoToMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu"); // Use your real scene name
    }

    public void NextLevel()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);
    }

    private void PlacePlayer(RoomData first)
    {
        GameObject player = GameObject.FindWithTag("Player");
        if (player == null) return;

        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
        if (rb != null) rb.linearVelocity = Vector2.zero;

        // The spawn spot is found for the real body size, and the body is put with its feet on the floor
        // (the pivot does not have to be the collider's center).
        Vector3 spawn;
        Collider2D body = player.GetComponent<Collider2D>();
        if (body != null)
        {
            Physics2D.SyncTransforms();
            Bounds b = body.bounds;
            Vector3 pivotFromFeet = player.transform.position - new Vector3(b.center.x, b.min.y, player.transform.position.z);
            spawn = roomGenerator.GetSpawnFeet(first, b.size) + pivotFromFeet;
        }
        else
        {
            spawn = roomGenerator.GetSpawnPoint(first);
        }
        SetPlayerPosition(player, rb, spawn);

        // Coming from another scene, the first frame is as long as the loading (seconds) and Unity runs many
        // physics steps in it. The player is kept out of the physics during those first frames and put back on
        // the spawn afterwards, so it can't fall through anything while the level settles.
        if (rb != null) StartCoroutine(HoldPlayerWhileLoading(player, rb, spawn));
    }

    private static void SetPlayerPosition(GameObject player, Rigidbody2D rb, Vector3 position)
    {
        player.transform.position = position;
        if (rb != null)
        {
            rb.position = position;
            rb.linearVelocity = Vector2.zero;
        }
        Physics2D.SyncTransforms();
    }

    private IEnumerator HoldPlayerWhileLoading(GameObject player, Rigidbody2D rb, Vector3 spawn)
    {
        bool wasSimulated = rb.simulated;
        rb.simulated = false;

        yield return null;   // the long loading frame
        yield return null;
        yield return new WaitForFixedUpdate();

        if (player == null || rb == null) yield break;
        roomGenerator.RefreshColliders();
        SetPlayerPosition(player, rb, spawn);
        rb.simulated = wasSimulated;

        // Safety net for the first moments: the spawn stands on the floor, so if the player ends up well below it
        // something let it fall through (the floor collider was not there yet). Rebuild and put it back.
        const float fallLimit = 3f;
        float until = Time.time + 1.5f;
        while (Time.time < until && player != null && rb != null)
        {
            yield return new WaitForFixedUpdate();
            if (player != null && player.transform.position.y < spawn.y - fallLimit)
            {
                Debug.LogWarning($"[GameManager] Player fell from the spawn {spawn} to {player.transform.position} " +
                                 "while the level was loading; colliders rebuilt and player put back.");
                roomGenerator.RefreshColliders();
                SetPlayerPosition(player, rb, spawn);
            }
        }
    }
}