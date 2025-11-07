using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class TileController : MonoBehaviour
{
    [Header("Referencia al Tilemap")]
    public Tilemap tilemap;

    [Header("Posiciones a eliminar")]
    public List<Vector3Int> tilesToClear = new List<Vector3Int>();

    // Método para eliminar los tiles
    public void ClearTiles()
    {
        foreach (var pos in tilesToClear)
        {
            tilemap.SetTile(pos, null); // Elimina el tile en esa celda
        }
    }

    // Método opcional para reemplazar tiles
    public void ReplaceTiles(TileBase newTile)
    {
        foreach (var pos in tilesToClear)
        {
            tilemap.SetTile(pos, newTile); // Reemplaza el tile
        }
    }
}