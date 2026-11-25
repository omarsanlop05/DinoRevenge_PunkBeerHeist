using UnityEngine;

// Added by EnemyPlacer to every spawned enemy. The variant behaviours (Assets/Scripts/Enemy/Variants)
// read their stats, route and projectile from here.
public class ProceduralEnemyInfo : MonoBehaviour
{
    public int roomIndex;
    public EnemyKind kind;
    [Tooltip("Average score multiplier of the room when it was generated")]
    public float multiplier = 1f;

    [Header("Visual variation chosen from the player scores (1-3)")]
    [Tooltip("Helmet = Health score")]
    public int helmet = 1;
    [Tooltip("Shoes = Speed score")]
    public int shoes = 1;
    [Tooltip("Aura = Power score")]
    public int aura = 1;

    [Header("Stat multipliers (from the gear levels)")]
    public float speedMultiplier = 1f;
    public float healthMultiplier = 1f;
    public float powerMultiplier = 1f;

    [Header("Kind specific")]
    public Transform[] waypoints;        // flyers only
    public GameObject projectilePrefab;  // launchers only (prefab, not an instance)
}
