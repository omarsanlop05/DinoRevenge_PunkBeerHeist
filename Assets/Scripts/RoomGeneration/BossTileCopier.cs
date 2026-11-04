using UnityEngine;
using UnityEngine.Tilemaps;

// Temporary helper: copies every tile inside 'area' from the shared tilemap into the prefab's own tilemap.
public class BossTileCopier : MonoBehaviour
{
    public Tilemap source;
    public Tilemap target;
    [Tooltip("Active collider covering the WHOLE boss room, walls included")]
    public Collider2D area;
    public bool removeFromSource = false;

    [ContextMenu("Copy Tiles")]
    private void CopyTiles()
    {
        if (source == null || target == null || area == null)
        {
            Debug.LogError("[BossTileCopier] Assign source, target and area.");
            return;
        }

        Bounds b = area.bounds;
        Vector3Int min = source.WorldToCell(b.min);
        Vector3Int max = source.WorldToCell(b.max);
        int count = 0;

        for (int x = min.x; x <= max.x; x++)
        {
            for (int y = min.y; y <= max.y; y++)
            {
                var cell = new Vector3Int(x, y, 0);
                TileBase tile = source.GetTile(cell);
                if (tile == null) continue;

                Vector3Int dst = target.WorldToCell(source.GetCellCenterWorld(cell));
                target.SetTile(dst, tile);
                target.SetTransformMatrix(dst, source.GetTransformMatrix(cell));
                target.SetColor(dst, source.GetColor(cell));

                if (removeFromSource)
                    source.SetTile(cell, null);

                count++;
            }
        }

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(target);
        UnityEditor.EditorUtility.SetDirty(source);
#endif
        Debug.Log($"[BossTileCopier] Copied {count} tiles.");
    }
}