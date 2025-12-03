using UnityEngine;

public class SoundManager : MonoBehaviour
{
    public static SoundManager instance;

    [Header("Audio Sources")]
    public AudioSource loopSource;     // Para sonidos en loop (caminar)
    public AudioSource sfxSource;      // Para efectos de un solo uso (salto, ataque)
    public AudioSource gameMusicSource;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // Si no se asignaron en el Inspector, los crea automáticamente
        if (loopSource == null)
            loopSource = gameObject.AddComponent<AudioSource>();
        if (sfxSource == null)
            sfxSource = gameObject.AddComponent<AudioSource>();
        if (gameMusicSource == null)
            gameMusicSource = gameObject.AddComponent<AudioSource>();
    }

    public void playSound(AudioClip clip)
    {
        if (clip == null) return;
        loopSource.clip = clip;
        if (!loopSource.isPlaying)
            loopSource.Play();
    }

    public void stopSound(AudioClip clip)
    {
        if (loopSource.clip == clip)
            loopSource.Stop();
    }

    public void playOnce(AudioClip clip)
    {
        if (clip == null) return;
        sfxSource.PlayOneShot(clip);
    }

    public void playMusic(AudioClip clip)
    {
        if (clip == null) return;
        gameMusicSource.Stop();

        gameMusicSource.clip = clip;
        if (!gameMusicSource.isPlaying)
            gameMusicSource.Play();
    }
}
