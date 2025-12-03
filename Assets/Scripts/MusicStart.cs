using UnityEngine;

public class MusicStart : MonoBehaviour
{
    public AudioClip startMusic;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        SoundManager.instance.gameMusicSource.loop = true;
        SoundManager.instance.playMusic(startMusic);
    }
}
