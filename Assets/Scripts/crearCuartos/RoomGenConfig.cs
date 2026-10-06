using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(menuName = "Procedural/Room Gen Config")]
public class RoomGenConfig : ScriptableObject
{
    [Header("Tamaño del cuarto (en tiles)")]
    public int width = 37;
    public int height = 21;

    [Header("Cascarón del cuarto (en tiles)")]
    public int wallThickness = 2;
    public int floorThickness = 2;
    public int ceilingThickness = 2;
    [Tooltip("Alto de la apertura de la puerta")]
    public int doorHeight = 4;

    [Header("Puertas a distinta altura")]
    [Tooltip("Fila (contando desde abajo) donde se para el jugador al cruzar una puerta. " +
             "Debe ser igual a floorThickness para una puerta a nivel del suelo. Cada puerta elige una al azar.")]
    public int[] doorRows = { 2, 8, 13 };
    [Tooltip("Largo del saliente sólido pegado a cada puerta alta")]
    public int ledgeLength = 4;

    [Header("Tiles")]
    public TileBase solidTile;
    public TileBase platformTile;

    [Header("Movimiento del jugador (en tiles) - AJUSTAR")]
    public int playerHeight = 2;
    [Tooltip("Mínimo recomendado: playerHeight + 2, si no las plataformas sobre el suelo no son alcanzables")]
    public int maxJumpHeight = 4;
    public int maxJumpDistance = 4;

    [Header("Plataformas extra (además del camino obligatorio)")]
    public int minPlatforms = 2;
    public int maxPlatforms = 5;
    public Vector2Int platformLength = new Vector2Int(3, 6);
    public int placementAttempts = 60;

    [Header("Enemigos")]
    public GameObject[] enemyPrefabs;
    public Vector2Int enemyCount = new Vector2Int(2, 4);
    [Tooltip("Tiles libres de enemigos cerca de la entrada")]
    public int safeZoneFromEntry = 8;
}