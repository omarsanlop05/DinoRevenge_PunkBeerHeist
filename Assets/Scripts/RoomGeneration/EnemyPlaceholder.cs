using UnityEngine;

public enum EnemyKind
{
    Contact = 0,    // "contacto": walks on the main path and hurts on touch
    Launcher = 1,   // "proyectil": stands on air platforms or off the main path and throws
    Flyer = 2       // "volador": floats in empty air and follows waypoints
}

// Pass 1 output: a spot chosen by EnemyPlacer plus the facts about what is around it.
// Pass 2 (or any other spawner) reads kind and waypoints to know which enemy goes here.
public class EnemyPlaceholder : MonoBehaviour
{
    public int roomIndex;
    public Vector3Int cell;         // world cell the enemy occupies
    public Vector2Int localCell;    // same cell, relative to the room origin

    [Tooltip("Enemy kind for this spot, from the rules in Classify")]
    public EnemyKind kind;

    [Header("What is around the spot")]
    public bool inAir;              // nothing solid under it
    public SurfaceKind surfaceKind; // only valid when inAir is false
    public bool onMainPath;         // only valid when inAir is false

    [Header("Flyers only")]
    public Transform[] waypoints;

    [Header("Filled by pass 2 (null in placeholders-only mode)")]
    public GameObject spawnedEnemy;

    // Placement rules (single source of truth for both passes):
    //  - in the air with nothing under it            -> Flyer
    //  - on an air platform / ledge, or off the path -> Launcher
    //  - on the ground of the main path              -> Contact
    public static EnemyKind Classify(bool inAir, SurfaceKind surfaceKind, bool onMainPath)
    {
        if (inAir) return EnemyKind.Flyer;

        bool elevated = surfaceKind == SurfaceKind.Platform || surfaceKind == SurfaceKind.Ledge;
        if (elevated || !onMainPath) return EnemyKind.Launcher;

        return EnemyKind.Contact;
    }

    // Type names used by CatalogoEnemigos / FabricaEnemigos
    public static string CatalogName(EnemyKind kind)
    {
        switch (kind)
        {
            case EnemyKind.Contact: return "contacto";
            case EnemyKind.Launcher: return "proyectil";
            default: return "volador";
        }
    }

    private static Color GizmoColor(EnemyKind kind)
    {
        switch (kind)
        {
            case EnemyKind.Contact: return new Color(1f, 0.35f, 0.2f);
            case EnemyKind.Launcher: return new Color(1f, 0.85f, 0.1f);
            default: return new Color(0.3f, 0.8f, 1f);
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = GizmoColor(kind);
        Gizmos.DrawWireCube(transform.position, new Vector3(0.8f, 0.8f, 0f));

        if (waypoints == null || waypoints.Length == 0) return;

        for (int i = 0; i < waypoints.Length; i++)
        {
            if (waypoints[i] == null) continue;
            Gizmos.DrawWireSphere(waypoints[i].position, 0.25f);

            Transform next = waypoints[(i + 1) % waypoints.Length];
            if (next != null) Gizmos.DrawLine(waypoints[i].position, next.position);
        }
    }
}
