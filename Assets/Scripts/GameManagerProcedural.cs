using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManagerProcedural : MonoBehaviour
{
    public static GameManagerProcedural instance;

    [Header("UI")]
    public GameObject deathScreen;
    public GameObject victoryScreen;

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

        // First rooms: no score yet, so neutral multiplier
        int count = Mathf.Min(initialRooms, totalRooms);
        for (int i = 0; i < count; i++)
            GenerateNextRoom(1f);
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

        RoomData data = roomGenerator.Generate(index, baseSeed + index, previous, multiplier);
        rooms.Add(data);
        data.entryTrigger.PlayerEntered += OnPlayerEnteredRoom;

        Debug.Log($"[GameManager] Room {index} generated (multiplier {multiplier:F2})");
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

    // TODO: call this from the hallway's CameraChanger once the hallway exists.
    // It should use the total score to build the boss room.
    public void OnHallwayEntered()
    {
        float multiplier = PlayerScore.Instance != null ? PlayerScore.Instance.CurrentMultiplier : 1f;
        Debug.Log($"[GameManager] Hallway entered. Boss multiplier: {multiplier:F2}");
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
}