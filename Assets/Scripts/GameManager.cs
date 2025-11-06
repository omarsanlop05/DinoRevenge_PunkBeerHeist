using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;

    [Header("UI")]
    public GameObject deathScreen;
    public GameObject victoryScreen;

    private void Awake()
    {
        // Patrón Singleton simple
        if (instance == null)
            instance = this;
        else
            Destroy(gameObject);

        deathScreen.SetActive(false);
        victoryScreen.SetActive(false);
    }

    public void ShowDeathScreen()
    {
        Time.timeScale = 0f; // Pausar juego
        deathScreen.SetActive(true);
    }

    public void ShowVictoryScreen()
    {
        Time.timeScale = 0f; // Pausar juego
        victoryScreen.SetActive(true);
    }

    public void RetryLevel()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void GoToMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu"); // Ajusta al nombre real de tu escena
    }

    public void NextLevel()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);
    }
}
