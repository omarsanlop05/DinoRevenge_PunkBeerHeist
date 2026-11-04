using System;
using UnityEngine;

public class PlayerScore : MonoBehaviour
{
    public static PlayerScore Instance { get; private set; }

    [Header("Points per action")]
    [SerializeField] private float healPoints = -10f;
    [SerializeField] private float roomChangePoints = 20f;
    [SerializeField] private float enemyDefeatedPoints = 10f;
    [SerializeField] private float bossDefeatedPoints = 30f;
    [Tooltip("Points lost per point of damage received")]
    [SerializeField] private float damagePointsPerHit = -1f;
    [SerializeField] private float deathPoints = -100f;

    [Header("Multiplier mapping")]
    [Tooltip("Score at or below this value gives the minimum multiplier")]
    [SerializeField] private float minScore = -100f;
    [Tooltip("Score at or above this value gives the maximum multiplier")]
    [SerializeField] private float maxScore = 100f;
    [SerializeField] private float minMultiplier = 0.75f;
    [SerializeField] private float maxMultiplier = 1.25f;

    public float Score { get; private set; }
    [field: SerializeField] public int HitsReceived { get; private set; } = 0;
    public float CurrentMultiplier { get; private set; } = 1f;

    // Fired every time the player changes room, with the freshly computed multiplier
    public event Action<float> OnMultiplierUpdated;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        CurrentMultiplier = ComputeMultiplier();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ---- Public API: one method per player action ----

    public void RegisterHeal() => AddScore(healPoints, "Heal");

    public void RegisterDamage(float damage)
    {
        HitsReceived++;
        AddScore(damagePointsPerHit * damage, "Damage received");
    }

    public void RegisterEnemyDefeated(bool isBoss = false) =>
        AddScore(isBoss ? bossDefeatedPoints : enemyDefeatedPoints, "Enemy defeated");

    public void RegisterDeath() => AddScore(deathPoints, "Death");

    // Call this when the player enters a new room.
    // Awards the room points, then recalculates the multiplier.
    public void RegisterRoomChange()
    {
        AddScore(roomChangePoints, "Room change");
        CurrentMultiplier = ComputeMultiplier();
        Debug.Log($"[Score] Room changed. Score: {Score} -> Multiplier: {CurrentMultiplier:F2}");
        OnMultiplierUpdated?.Invoke(CurrentMultiplier);
    }

    // ---- Internals ----

    private void AddScore(float points, string reason)
    {
        Score += points;
        Debug.Log($"[Score] {reason}: {points:+0.##;-0.##} (total {Score})");
    }

    private float ComputeMultiplier()
    {
        float t = Mathf.InverseLerp(minScore, maxScore, Score); // clamped to 0..1
        return Mathf.Lerp(minMultiplier, maxMultiplier, t);
    }

    
}