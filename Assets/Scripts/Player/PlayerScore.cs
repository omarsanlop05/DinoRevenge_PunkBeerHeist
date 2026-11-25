using System;
using UnityEngine;

// The three enemy attributes the player's performance controls
public enum StatAxis
{
    Speed = 0,   // player pace: time to clear rooms, quick kills       -> enemy shoes
    Health = 1,  // offensive efficiency: how much / how fast they kill -> enemy helmet
    Power = 2    // defense and survival: damage taken, heals            -> enemy aura
}

// Points one action gives to each of the three scores
[Serializable]
public struct ScoreDelta
{
    public float speed;
    public float health;
    public float power;

    public ScoreDelta(float speed, float health, float power)
    {
        this.speed = speed;
        this.health = health;
        this.power = power;
    }

    public static ScoreDelta operator *(ScoreDelta d, float k) => new ScoreDelta(d.speed * k, d.health * k, d.power * k);

    public override string ToString() => $"vel {speed:+0.##;-0.##;0}, vida {health:+0.##;-0.##;0}, poder {power:+0.##;-0.##;0}";
}

// Snapshot of the three scores (0..1 each) taken when a room is generated
[Serializable]
public struct EnemyDifficulty
{
    public float speed01;
    public float health01;
    public float power01;

    public float Average01 => (speed01 + health01 + power01) / 3f;

    public float Get(StatAxis axis) => axis == StatAxis.Speed ? speed01 : axis == StatAxis.Health ? health01 : power01;

    public static EnemyDifficulty Neutral => new EnemyDifficulty { speed01 = 0.5f, health01 = 0.5f, power01 = 0.5f };

    public override string ToString() => $"vel {speed01:F2}, vida {health01:F2}, poder {power01:F2}";
}

public class PlayerScore : MonoBehaviour
{
    public static PlayerScore Instance { get; private set; }

    [Header("General")]
    [SerializeField] private ScoreDelta death = new ScoreDelta(-50, -50, -50);
    [SerializeField] private ScoreDelta roomChange = new ScoreDelta(5, 5, 5);
    [SerializeField] private ScoreDelta bossDefeated = new ScoreDelta(15, 25, 15);

    [Header("Combat")]
    [SerializeField] private ScoreDelta enemyDefeated = new ScoreDelta(0, 5, 0);
    [Tooltip("Replaces the normal kill when the enemy dies less than 'fastKillSeconds' after it was first seen")]
    [SerializeField] private ScoreDelta enemyDefeatedFast = new ScoreDelta(2, 3, 0);
    [SerializeField] private float fastKillSeconds = 3f;
    [Tooltip("Applied once per point of damage received")]
    [SerializeField] private ScoreDelta damagePerPoint = new ScoreDelta(0, 0, -1);
    [SerializeField] private ScoreDelta heal = new ScoreDelta(0, -3, -10);
    [Tooltip("Only this many kills per room give points (stops farming a room)")]
    [SerializeField] private int maxKillPointsPerRoom = 4;

    [Header("Room clear time (fraction of the target time)")]
    [SerializeField] private ScoreDelta clearVeryFast = new ScoreDelta(15, 5, 0);   // <= 50%
    [SerializeField] private ScoreDelta clearFast = new ScoreDelta(5, 0, 0);        // 51% - 100%
    [SerializeField] private ScoreDelta clearSlow = new ScoreDelta(0, 0, 0);        // 100% - 150%
    [SerializeField] private ScoreDelta clearVerySlow = new ScoreDelta(-10, -3, 0); // > 150%

    [Header("Rooms")]
    [SerializeField] private ScoreDelta roomNoDamage = new ScoreDelta(0, 5, 15);
    [SerializeField] private ScoreDelta challengeCompleted = new ScoreDelta(10, 10, 10);
    [SerializeField] private ScoreDelta challengeFailed = new ScoreDelta(-10, -10, -10);
    [SerializeField] private ScoreDelta secretRoomFound = new ScoreDelta(5, 5, 5);

    [Header("Score range (same for the three scores)")]
    [SerializeField] private float minScore = -100f;
    [SerializeField] private float maxScore = 100f;

    [Header("Multiplier range per attribute (min score -> x, max score -> y)")]
    [SerializeField] private Vector2 speedMultiplierRange = new Vector2(0.9f, 1.15f);
    [SerializeField] private Vector2 healthMultiplierRange = new Vector2(0.75f, 1.25f);
    [SerializeField] private Vector2 powerMultiplierRange = new Vector2(0.8f, 1.2f);

    [Header("Live values (read only)")]
    [SerializeField] private float speedScore;
    [SerializeField] private float healthScore;
    [SerializeField] private float powerScore;

    [field: SerializeField] public int HitsReceived { get; private set; } = 0;

    // Average of the three multipliers. Kept so older code (boss room, logs) keeps working.
    public float CurrentMultiplier { get; private set; } = 1f;

    // Old single score, now the average of the three
    public float Score => (speedScore + healthScore + powerScore) / 3f;

    public float FastKillSeconds => fastKillSeconds;

    // Fired every time the player changes room, with the freshly computed average multiplier
    public event Action<float> OnMultiplierUpdated;
    // Fired every time the player takes damage (RoomCombatTracker uses it for the "no damage" bonus)
    public event Action<float> OnDamaged;

    private int killsScoredThisRoom;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        CurrentMultiplier = ComputeAverageMultiplier();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ---- Reading the scores ----

    public float GetScore(StatAxis axis) =>
        axis == StatAxis.Speed ? speedScore : axis == StatAxis.Health ? healthScore : powerScore;

    // 0 = minScore or less, 1 = maxScore or more
    public float GetScore01(StatAxis axis) => Mathf.InverseLerp(minScore, maxScore, GetScore(axis));

    public Vector2 GetMultiplierRange(StatAxis axis) =>
        axis == StatAxis.Speed ? speedMultiplierRange : axis == StatAxis.Health ? healthMultiplierRange : powerMultiplierRange;

    public float GetMultiplier(StatAxis axis)
    {
        Vector2 r = GetMultiplierRange(axis);
        return Mathf.Lerp(r.x, r.y, GetScore01(axis));
    }

    public EnemyDifficulty GetDifficulty() => new EnemyDifficulty
    {
        speed01 = GetScore01(StatAxis.Speed),
        health01 = GetScore01(StatAxis.Health),
        power01 = GetScore01(StatAxis.Power)
    };

    // ---- Public API: one method per player action ----

    public void RegisterHeal(bool inRestRoom = false)
    {
        if (inRestRoom) { Debug.Log("[Score] Heal in rest room: no change"); return; }
        Add(heal, "Heal");
    }

    public void RegisterDamage(float damage)
    {
        HitsReceived++;
        Add(damagePerPoint * damage, $"Damage received ({damage})");
        OnDamaged?.Invoke(damage);
    }

    public void RegisterEnemyDefeated(bool isBoss = false) => RegisterEnemyDefeated(isBoss, false);

    public void RegisterEnemyDefeated(bool isBoss, bool fast)
    {
        if (isBoss) { Add(bossDefeated, "Boss defeated"); return; }

        if (killsScoredThisRoom >= maxKillPointsPerRoom)
        {
            Debug.Log($"[Score] Enemy defeated: no points (limit of {maxKillPointsPerRoom} per room reached)");
            return;
        }
        killsScoredThisRoom++;
        Add(fast ? enemyDefeatedFast : enemyDefeated, fast ? "Enemy defeated fast" : "Enemy defeated");
    }

    public void RegisterDeath() => Add(death, "Death");

    // Call this when the player enters a new room.
    // Awards the room points, then recalculates the multiplier.
    public void RegisterRoomChange()
    {
        Add(roomChange, "Room change");
        killsScoredThisRoom = 0;
        CurrentMultiplier = ComputeAverageMultiplier();
        Debug.Log($"[Score] Room changed. Scores: {DescribeScores()} -> average multiplier {CurrentMultiplier:F2}");
        OnMultiplierUpdated?.Invoke(CurrentMultiplier);
    }

    // Called by RoomCombatTracker when the last enemy of a room dies
    public void RegisterRoomCleared(float elapsed, float target, bool tookDamage)
    {
        if (target > 0f)
        {
            float ratio = elapsed / target;
            ScoreDelta d = ratio <= 0.5f ? clearVeryFast : ratio <= 1f ? clearFast : ratio <= 1.5f ? clearSlow : clearVerySlow;
            Add(d, $"Room cleared in {elapsed:F1}s ({ratio:P0} of {target:F1}s)");
        }

        if (!tookDamage) Add(roomNoDamage, "Room cleared without damage");
    }

    public void RegisterSecretRoomFound() => Add(secretRoomFound, "Secret room found");

    public void RegisterChallengeRoom(bool completed) =>
        Add(completed ? challengeCompleted : challengeFailed, completed ? "Challenge completed" : "Challenge failed");

    // Rest / shop rooms and skipping optional rooms give nothing on purpose; kept so the calls exist
    public void RegisterNeutralRoomAction(string what) => Debug.Log($"[Score] {what}: no change");

    // ---- Internals ----

    private void Add(ScoreDelta d, string reason)
    {
        speedScore = Mathf.Clamp(speedScore + d.speed, minScore, maxScore);
        healthScore = Mathf.Clamp(healthScore + d.health, minScore, maxScore);
        powerScore = Mathf.Clamp(powerScore + d.power, minScore, maxScore);
        Debug.Log($"[Score] {reason}: {d} -> {DescribeScores()}");
    }

    private string DescribeScores() => $"vel {speedScore:0.#}, vida {healthScore:0.#}, poder {powerScore:0.#}";

    private float ComputeAverageMultiplier() =>
        (GetMultiplier(StatAxis.Speed) + GetMultiplier(StatAxis.Health) + GetMultiplier(StatAxis.Power)) / 3f;
}
