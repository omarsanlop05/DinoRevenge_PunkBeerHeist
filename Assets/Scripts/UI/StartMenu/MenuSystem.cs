using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuSystem : MonoBehaviour
{
    public void Jugar()
    {
        Debug.Log("Starting game");
        Time.timeScale = 1f;
        SceneManager.LoadScene("FirstGameplayScene");
    }

    public void NextLevel()
    {
        Debug.Log("Starting game");
        Time.timeScale = 1f;
        SceneManager.LoadScene("ProceduralScene");
    }

    public void Arcade()
    {
        Debug.Log("Starting arcade");
        Time.timeScale = 1f;
        SceneManager.LoadScene("ProceduralScene");
    }

    public void Salir()
    {
        Debug.Log("Exiting");
        UnityEditor.EditorApplication.isPlaying = false;
        //Application.Quit();
    }

    public void Retry()
    {
        Debug.Log("Re-Starting game");
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
