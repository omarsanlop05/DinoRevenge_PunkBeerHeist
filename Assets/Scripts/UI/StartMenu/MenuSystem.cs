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
#if UNITY_EDITOR
        // UnityEditor only exists inside the editor: without the #if the player build does not compile
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void Retry()
    {
        Debug.Log("Re-Starting game");
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
