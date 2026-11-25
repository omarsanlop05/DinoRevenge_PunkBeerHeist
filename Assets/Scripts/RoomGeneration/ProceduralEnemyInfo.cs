using UnityEngine;

// Added by EnemyPlacer to every spawned enemy. The real behaviour scripts on the base prefabs
// can read the multiplier, the route and the projectile from here.
public class ProceduralEnemyInfo : MonoBehaviour
{
    public int roomIndex;
    public EnemyKind kind;
    [Tooltip("Score multiplier of the room when it was generated (0.75 - 1.25)")]
    public float multiplier = 1f;

    [Header("Visual variation chosen from the multiplier (1-3)")]
    public int helmet = 1;
    public int shoes = 1;
    public int aura = 1;

    [Header("Kind specific")]
    public Transform[] waypoints;        // flyers only
    public GameObject projectilePrefab;  // launchers only (prefab, not an instance)
}
