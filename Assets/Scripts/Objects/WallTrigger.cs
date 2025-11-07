using UnityEngine;

public class WallTrigger : MonoBehaviour
{
    [Header("Referencia al controlador de tiles")]
    public TileController tileController;

    private bool wallBroken = false;

    // Simulación de evento externo (puedes reemplazar esto con colisión, botón, etc.)
    void Update()
    {
        if (!wallBroken && Input.GetKeyDown(KeyCode.Space)) // Simula romper la pared con barra espaciadora
        {
            wallBroken = true;
            OnWallBroken();
        }
    }

    // Evento que se dispara al romper la pared
    void OnWallBroken()
    {
        if (tileController != null)
        {
            tileController.ClearTiles(); // Elimina los tiles definidos
        }
    }
}