using System.Collections.Generic;
using UnityEngine;
using Unity.Cinemachine;

// What a walkable surface is, so the enemy placer can apply its rules
public enum SurfaceKind
{
    Floor,      // ground segment painted from the bottom of the room
    Pillar,     // solid column that rises from the floor
    Platform,   // floating platform of the main jump chain
    Ledge       // solid ledge stuck to a high door (entry or exit pad)
}

// Superficie donde se puede caminar. Coordenadas locales del cuarto (0,0 = esquina inferior izquierda).
// y = fila del tile de la superficie; el jugador se para en la fila y + 1.

public struct Surface
{
    public int xMin, xMax, y;
    public SurfaceKind kind;
    public bool onMainPath;   // the player has to cross it to reach the exit door
}

public class RoomData
{
    public float difficulty = 1f;                                        // average multiplier (kept for older code)
    public EnemyDifficulty enemyDifficulty = EnemyDifficulty.Neutral;   // the three player scores (0..1) when the room was built
    public int index;
    public Vector3Int originCell;
    public int width;
    public int entryRow;   // fila local donde se para el jugador al cruzar la puerta izquierda
    public int exitRow;    // fila local donde se para el jugador al cruzar la puerta derecha
    public Transform root;
    public CinemachineCamera cam;
    public RoomEntryTrigger entryTrigger;
    public bool hasSecretRoom;
    public RectInt secretEntrance;   // local cells around the secret door (only valid when hasSecretRoom)
    public List<Surface> surfaces = new List<Surface>();   // every walkable surface, tagged

    // Celda mundo donde se pega el siguiente cuarto (esquina inferior izquierda = origen + (width, exitRow - nextEntryRow)).
    public Vector3Int NextOrigin(int nextEntryRow)
    {
        return new Vector3Int(originCell.x + width, originCell.y + exitRow - nextEntryRow, 0);
    }
}
