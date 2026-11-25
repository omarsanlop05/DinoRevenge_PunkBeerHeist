using System.Collections.Generic;
using UnityEngine;

// One per generated room (EnemyPlacer adds it to room.root). It:
//  - keeps the room enemies asleep until the player walks in, then wakes them;
//  - times how long the player takes to kill them all and whether they got hurt meanwhile;
//  - reports it to PlayerScore (clear time and "no damage" bonuses).
public class RoomCombatTracker : MonoBehaviour
{
    [Header("Target clear time = baseTime + perEnemyTime * enemies")]
    [SerializeField] private float baseTime = 6f;
    [SerializeField] private float perEnemyTime = 5f;

    // The room the player is fighting in right now (the last one entered)
    public static RoomCombatTracker Current { get; private set; }

    public int RoomIndex { get; private set; }
    public int AliveCount => alive.Count;
    public float TargetTime { get; private set; }

    private readonly List<EnemyHealth> alive = new List<EnemyHealth>();
    private readonly List<EnemyVariantBase> sleepers = new List<EnemyVariantBase>();
    private RoomEntryTrigger entryTrigger;
    private int totalEnemies;
    private bool entered;
    private bool cleared;
    private bool tookDamage;
    private float enterTime;

    public void Init(RoomData room, float targetBase, float targetPerEnemy)
    {
        RoomIndex = room.index;
        baseTime = targetBase;
        perEnemyTime = targetPerEnemy;

        entryTrigger = room.entryTrigger;
        if (entryTrigger != null) entryTrigger.PlayerEntered += OnPlayerEntered;
    }

    // Called by EnemyPlacer for every enemy it spawns. The enemy is frozen until the player enters.
    public void Register(GameObject enemy)
    {
        totalEnemies++;

        var health = enemy.GetComponent<EnemyHealth>();
        if (health != null)
        {
            alive.Add(health);
            health.Died += OnEnemyDied;
        }

        foreach (var v in enemy.GetComponents<EnemyVariantBase>())
        {
            sleepers.Add(v);
            if (entered) v.Activate(); else v.Sleep();
        }
    }

    // The first room has no entry door the player crosses (they spawn inside it), so it starts awake
    public void StartNow() => OnPlayerEntered(RoomIndex);

    private void OnPlayerEntered(int index)
    {
        if (entered) return;
        entered = true;
        enterTime = Time.time;
        TargetTime = baseTime + perEnemyTime * totalEnemies;
        Current = this;

        foreach (var v in sleepers)
            if (v != null) v.Activate();

        if (PlayerScore.Instance != null) PlayerScore.Instance.OnDamaged += OnPlayerDamaged;

        Debug.Log($"[RoomCombat] Room {RoomIndex} entered: {alive.Count} enemies, target time {TargetTime:F1}s");
        if (alive.Count == 0) cleared = true;   // nothing to clear: no time bonus
    }

    private void OnPlayerDamaged(float damage)
    {
        if (Current == this && !cleared) tookDamage = true;
    }

    private void OnEnemyDied(EnemyHealth h)
    {
        h.Died -= OnEnemyDied;
        alive.Remove(h);
        if (!entered || cleared || alive.Count > 0) return;

        cleared = true;
        float elapsed = Time.time - enterTime;
        Debug.Log($"[RoomCombat] Room {RoomIndex} cleared in {elapsed:F1}s (target {TargetTime:F1}s, damage taken: {tookDamage})");
        PlayerScore.Instance?.RegisterRoomCleared(elapsed, TargetTime, tookDamage);
    }

    private void OnDestroy()
    {
        if (entryTrigger != null) entryTrigger.PlayerEntered -= OnPlayerEntered;
        if (PlayerScore.Instance != null) PlayerScore.Instance.OnDamaged -= OnPlayerDamaged;
        foreach (var h in alive) if (h != null) h.Died -= OnEnemyDied;
        if (Current == this) Current = null;
    }
}
