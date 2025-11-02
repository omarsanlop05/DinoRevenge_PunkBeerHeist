using UnityEngine;
using UnityEngine.UI;

public class CervezaUI : MonoBehaviour
{
    public GameObject cervezaSlotPrefab;
    public Transform barraContenedor;
    public Sprite cervezaApagada;
    public Sprite cervezaEncendida;

    private PlayerHealth playerHealth;
    private Image[] slots;
    private int lastMaxCervezas = -1;

    void Start()
    {
        playerHealth = FindFirstObjectByType<PlayerHealth>();
        ActualizarBarraSiNecesario();
    }

    void Update()
    {
        if (playerHealth == null) return;

        // Detectar cambio en el máximo
        if (playerHealth.maxCervezas != lastMaxCervezas)
        {
            ActualizarBarraSiNecesario();
        }

        // Actualizar sprites según cantidad actual
        for (int i = 0; i < slots.Length; i++)
        {
            slots[i].sprite = (i < playerHealth.cervezas) ? cervezaEncendida : cervezaApagada;
        }
    }

    void ActualizarBarraSiNecesario()
    {
        // Limpiar barra anterior
        foreach (Transform child in barraContenedor)
        {
            Destroy(child.gameObject);
        }

        // Crear nueva barra
        slots = new Image[playerHealth.maxCervezas];
        lastMaxCervezas = playerHealth.maxCervezas;

        for (int i = 0; i < playerHealth.maxCervezas; i++)
        {
            GameObject slot = Instantiate(cervezaSlotPrefab, barraContenedor);
            Image img = slot.GetComponent<Image>();
            img.sprite = cervezaApagada;
            slots[i] = img;
        }
    }
}