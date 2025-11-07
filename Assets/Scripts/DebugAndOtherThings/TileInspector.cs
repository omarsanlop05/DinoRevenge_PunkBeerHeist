using UnityEngine;
using UnityEngine.Tilemaps;

public class TileInspector : MonoBehaviour
{
    [Header("Tilemap a inspeccionar")]
    public Tilemap tilemap;

    [Header("Tile de resaltado")]
    public TileBase highlightTile;

    private Vector3Int lastHighlightedCell;

    void Update()
    {
        if (Input.GetMouseButtonDown(0)) // Clic izquierdo
        {
            Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Vector3Int cellPos = tilemap.WorldToCell(mouseWorldPos);

            TileBase tile = tilemap.GetTile(cellPos);

            if (tile != null)
            {
                Debug.Log($"Tile en {cellPos}: {tile.name}");
            }
            else
            {
                Debug.Log($"No hay tile en {cellPos}");
            }

            HighlightCell(cellPos);
        }
    }

    void HighlightCell(Vector3Int cellPos)
    {
        // Limpia el resaltado anterior
        if (tilemap.GetTile(lastHighlightedCell) == highlightTile)
        {
            tilemap.SetTile(lastHighlightedCell, null);
        }

        // Guarda la celda actual
        lastHighlightedCell = cellPos;

        // Coloca el tile de resaltado
        tilemap.SetTile(cellPos, highlightTile);
    }
}