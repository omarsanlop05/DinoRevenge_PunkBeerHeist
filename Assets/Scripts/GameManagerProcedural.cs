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
    [SerializeField] private int totalRooms = 5;
    [Tooltip("Clears Tilemaps and generated rooms before starting (removes leftovers from the context menu tests)")]
    [SerializeField] private bool clearBeforeGenerating = true;
    [SerializeField] private bool useRandomSeed = true;
    [SerializeField] private int baseSeed = 1;

    [Header("Secret Rooms")]
    [SerializeField] private int maxSecretRooms = 2;
    [Tooltip("Minimum hits received so far to be eligible, for rooms 3, 4 and 5 (in that order)")]
    [SerializeField] private int[] secretMinHits = { 3, 5, 6 };
    [Range(0f, 1f)] [SerializeField] private float secretChanceAtMinHits = 0.5f;
    [Range(0f, 1f)] [SerializeField] private float secretChancePerExtraHit = 0.15f;
    [Range(0f, 1f)] [SerializeField] private float secretMaxChance = 0.9f;
    [Tooltip("Chance multiplier applied once for every secret room already generated")]
    [Range(0f, 1f)] [SerializeField] private float secretChanceAfterEach = 0.7f;
    [Tooltip("Extra chance multiplier when the previous room already had a secret room")]
    [Range(0f, 1f)] [SerializeField] private float secretChanceIfPreviousHad = 0.35f;
    private int secretRoomsCount;

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
        if (data.hasSecretRoom)
            secretRoomsCount++;   // only counts if the generator actually managed to build it
        rooms.Add(data);
        data.entryTrigger.PlayerEntered += OnPlayerEnteredRoom;

        Debug.Log($"[GameManager] Room {index} generated (multiplier {multiplier:F2})");
        if (rooms.Count == totalRooms)
            SpawnHallway(data);
    }

    private bool ShouldMakeSecretRoom(int roomNumber, RoomData previous)
    {
        if (secretRoomsCount >= maxSecretRooms) return false;

        int slot = roomNumber - 3;   // room 3 -> 0, room 4 -> 1, room 5 -> 2
        if (slot < 0 || slot >= secretMinHits.Length) return false;
        if (PlayerScore.Instance == null) return false;

        int hits = PlayerScore.Instance.HitsReceived;
        int minHits = secretMinHits[slot];
        if (hits < minHits)
        {
            Debug.Log($"[GameManager] Secret room check for room {roomNumber}: hits {hits} < {minHits}, not eligible");
            return false;
        }

        float chance = secretChanceAtMinHits + (hits - minHits) * secretChancePerExtraHit;
        chance = Mathf.Min(chance, secretMaxChance);
        chance *= Mathf.Pow(secretChanceAfterEach, secretRoomsCount);
        if (previous != null && previous.hasSecretRoom)
            chance *= secretChanceIfPreviousHad;

        float roll = Random.value;
        bool ok = roll < chance;
        Debug.Log($"[GameManager] Secret room check for room {roomNumber}: hits {hits} (min {minHits}), chance {chance:P0}, roll {roll:F2} -> {ok}");
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

        player.transform.position = roomGenerator.GetSpawnPoint(first);
        Physics2D.SyncTransforms();
    }
}