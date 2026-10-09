using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

public class ProceduralRoomGenerator : MonoBehaviour
{
    [Header("Métricas del Cuarto")]
    public int width = 38;
    public int height = 21;

    [Header("Configuración del Agente Excavador")]
    public Vector2Int startPos = new Vector2Int(2, 3);
    public Vector2Int exitPos = new Vector2Int(35, 18);
    public int minJumpHeight = 2;
    public int maxJumpHeight = 4;
    public int maxJumpDistance = 3;

    [Header("Configuración del Autómata Celular")]
    [Range(0, 100)] public int randomFillPercent = 48;
    public int smoothIterations = 3;

    [Header("Referencias de Tilemap")]
    public Tilemap groundTilemap;
    public TileBase groundTile;

    // 0 = Aire, 1 = Sólido/Pared, 2 = Zona reservada para paso (Aire forzado)
    private int[,] map;

    [ContextMenu("Generar Cuarto")]
    public void GenerateRoom()
    {
        map = new int[width, height];

        // Paso 1: Llenar el mapa con ruido inicial
        InitializeRandomNoise();

        // Paso 2: El Agente cava el camino garantizado (Entrada -> Salida)
        CarvePathWithAgent();

        // Paso 3: Aplicar Autómata Celular para suavizar y dar textura orgánica
        for (int i = 0; i < smoothIterations; i++)
        {
            SmoothMap();
        }

        // Paso 4: Limpiar espacios de aire forzados por el agente y renderizar
        FinalizeAndRender();
    }

    void InitializeRandomNoise()
    {
        string seed = Time.time.ToString() + Random.Range(0, 1000);
        System.Random pseudoRandom = new System.Random(seed.GetHashCode());

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                // Bordes externos siempre son pared sólida
                if (x == 0 || x == width - 1 || y == 0 || y == height - 1)
                {
                    map[x, y] = 1;
                }
                else
                {
                    map[x, y] = (pseudoRandom.Next(0, 100) < randomFillPercent) ? 1 : 0;
                }
            }
        }
    }

    void CarvePathWithAgent()
    {
        Vector2Int current = startPos;

        // Limpiar área inmediata alrededor de la entrada
        ClearArea(current.x, current.y, 2);

        while (current.x < exitPos.x)
        {
            // El agente avanza hacia la derecha con saltos o desplazamientos
            int stepX = Random.Range(2, maxJumpDistance + 1);
            int stepY = Random.Range(-minJumpHeight, maxJumpHeight + 1);

            int nextX = Mathf.Clamp(current.x + stepX, 1, exitPos.x);
            int nextY = Mathf.Clamp(current.y + stepY, 2, height - 3);

            // Escavar el espacio aéreo del salto
            CarveSegment(current, new Vector2Int(nextX, nextY));

            current = new Vector2Int(nextX, nextY);

            // Crear una plataforma sólida justo debajo del punto donde llega el salto
            if (current.y - 1 > 0)
            {
                map[current.x, current.y - 1] = 1; // Plataforma segura
                if (current.x + 1 < width - 1) map[current.x + 1, current.y - 1] = 1;
            }
        }

        // Conectar con la salida final
        CarveSegment(current, exitPos);
        ClearArea(exitPos.x, exitPos.y, 2);
    }

    void CarveSegment(Vector2Int from, Vector2Int to)
    {
        // Traza una línea despejando espacio para el jugador (altura de 3 tiles)
        int steps = Mathf.Max(Mathf.Abs(to.x - from.x), Mathf.Abs(to.y - from.y));
        for (int i = 0; i <= steps; i++)
        {
            float t = steps == 0 ? 0 : (float)i / steps;
            int x = Mathf.RoundToInt(Mathf.Lerp(from.x, to.x, t));
            int y = Mathf.RoundToInt(Mathf.Lerp(from.y, to.y, t));

            // Marcamos como 2 (Zona despejada que el autómata celular NO debe tapar)
            ClearArea(x, y, 1);
        }
    }

    void ClearArea(int centerX, int centerY, int radius)
    {
        for (int x = centerX - radius; x <= centerX + radius; x++)
        {
            for (int y = centerY; y <= centerY + radius + 1; y++)
            {
                if (x > 0 && x < width - 1 && y > 0 && y < height - 1)
                {
                    map[x, y] = 2; // Espacio libre protegido
                }
            }
        }
    }

    void SmoothMap()
    {
        int[,] newMap = new int[width, height];

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                // Si es un espacio despejado por el agente o el borde, no se altera
                if (map[x, y] == 2)
                {
                    newMap[x, y] = 2;
                    continue;
                }

                if (x == 0 || x == width - 1 || y == 0 || y == height - 1)
                {
                    newMap[x, y] = 1;
                    continue;
                }

                int neighborWalls = GetSurroundingWallCount(x, y);

                // Reglas de formación de caverna/plataformas
                if (neighborWalls > 4)
                    newMap[x, y] = 1;
                else if (neighborWalls < 4)
                    newMap[x, y] = 0;
                else
                    newMap[x, y] = map[x, y];
            }
        }
        map = newMap;
    }

    int GetSurroundingWallCount(int gridX, int gridY)
    {
        int wallCount = 0;
        for (int neighborX = gridX - 1; neighborX <= gridX + 1; neighborX++)
        {
            for (int neighborY = gridY - 1; neighborY <= gridY + 1; neighborY++)
            {
                if (neighborX >= 0 && neighborX < width && neighborY >= 0 && neighborY < height)
                {
                    if (neighborX != gridX || neighborY != gridY)
                    {
                        // Contamos como pared las sólidas (1) o bordes fuera de límite
                        if (map[neighborX, neighborY] == 1) wallCount++;
                    }
                }
                else
                {
                    wallCount++;
                }
            }
        }
        return wallCount;
    }

    void FinalizeAndRender()
    {
        if (groundTilemap != null) groundTilemap.ClearAllTiles();

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                // Si es 1 dibuja terreno sólido
                if (map[x, y] == 1)
                {
                    if (groundTilemap != null && groundTile != null)
                    {
                        groundTilemap.SetTile(new Vector3Int(x, y, 0), groundTile);
                    }
                }
            }
        }
    }
}