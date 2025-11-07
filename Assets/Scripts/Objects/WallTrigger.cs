using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class WallTrigger : MonoBehaviour
{
    [Header("Referencia al Tilemap")]
    public Tilemap tilemap;

    [Header("Posiciones a eliminar")]
    public List<Vector3Int> tilesToClear = new List<Vector3Int>
    {
        new Vector3Int(-99, -35, 0), new Vector3Int(-98, -35, 0), new Vector3Int(-97, -35, 0), new Vector3Int(-96, -35, 0),
        new Vector3Int(-99, -36, 0), new Vector3Int(-98, -36, 0), new Vector3Int(-97, -36, 0), new Vector3Int(-96, -36, 0), new Vector3Int(-95, -36, 0),
        new Vector3Int(-99, -37, 0), new Vector3Int(-98, -37, 0), new Vector3Int(-97, -37, 0), new Vector3Int(-96, -37, 0), new Vector3Int(-95, -37, 0),
        new Vector3Int(-99, -38, 0), new Vector3Int(-98, -38, 0), new Vector3Int(-97, -38, 0), new Vector3Int(-96, -38, 0), new Vector3Int(-95, -38, 0),
        new Vector3Int(-99, -39, 0), new Vector3Int(-98, -39, 0), new Vector3Int(-97, -39, 0), new Vector3Int(-96, -39, 0), new Vector3Int(-95, -39, 0),
        new Vector3Int(-98, -40, 0), new Vector3Int(-97, -40, 0), new Vector3Int(-96, -40, 0), new Vector3Int(-95, -40, 0),
        new Vector3Int(-99, -41, 0), new Vector3Int(-98, -41, 0), new Vector3Int(-97, -41, 0), new Vector3Int(-96, -41, 0), new Vector3Int(-95, -41, 0),
        new Vector3Int(-99, -42, 0), new Vector3Int(-98, -42, 0), new Vector3Int(-97, -42, 0), new Vector3Int(-96, -42, 0),
        new Vector3Int(-99, -43, 0), new Vector3Int(-98, -43, 0), new Vector3Int(-97, -43, 0), new Vector3Int(-96, -43, 0),
        new Vector3Int(-98, -44, 0), new Vector3Int(-97, -44, 0), new Vector3Int(-96, -44, 0),
        new Vector3Int(-99, -45, 0), new Vector3Int(-98, -45, 0), new Vector3Int(-97, -45, 0), new Vector3Int(-96, -45, 0),
        new Vector3Int(-98, -46, 0), new Vector3Int(-97, -46, 0), new Vector3Int(-96, -46, 0),
        new Vector3Int(-98, -47, 0)
    };

    private bool alreadyCleared = false;

    // Llama este método cuando la pared se rompa
    public void BreakWall()
    {
        if (alreadyCleared) return;

        alreadyCleared = true;

        foreach (var pos in tilesToClear)
        {
            tilemap.SetTile(pos, null);
        }

        Debug.Log("Tiles eliminados por ruptura de pared.");
    }
}