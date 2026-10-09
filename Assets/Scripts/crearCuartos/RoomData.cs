using UnityEngine;
using Unity.Cinemachine;

// Superficie donde se puede caminar. Coordenadas locales del cuarto (0,0 = esquina inferior izquierda).
// y = fila del tile de la superficie; el jugador se para en la fila y + 1.
public struct Surface
{
    public int xMin, xMax, y;
}

public class RoomData
{
    public int index;
    public Vector3Int originCell;
    public int width;
    public int entryRow;   // fila local donde se para el jugador al cruzar la puerta izquierda
    public int exitRow;    // fila local donde se para el jugador al cruzar la puerta derecha
    public Transform root;
    public CinemachineCamera cam;
    public RoomEntryTrigger entryTrigger;

    // Celda mundo donde se pega el siguiente cuarto (esquina inferior izquierda = origen + (width, exitRow - nextEntryRow)).
    public Vector3Int NextOrigin(int nextEntryRow)
    {
        return new Vector3Int(originCell.x + width, originCell.y + exitRow - nextEntryRow, 0);
    }
}
