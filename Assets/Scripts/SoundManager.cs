using UnityEngine;
using UnityEngine.Video;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    private VideoPlayer videoPlayer;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject); // ��������� SoundManager ����� �������

        int savedSoundLevel = PlayerPrefs.GetInt("soundLevel", 3);
        SetVolumeLevel(savedSoundLevel);
    }

    public void SetVolumeLevel(int soundLevel)
    {
        float volume = 0f;
        switch (soundLevel)
        {
            case 0: volume = 0f; break;
            case 1: volume = 0.25f; break;
            case 2: volume = 0.5f; break;
            case 3: volume = 1f; break;
        }

        AudioListener.volume = volume;

        // ���� ���� VideoPlayer, ��������� ��������� ��� ����
        if (videoPlayer != null)
        {
            videoPlayer.SetDirectAudioVolume(0, volume);
        }
    }

    // ����� ��� ��������� VideoPlayer
    public void SetVideoPlayer(VideoPlayer vp)
    {
        videoPlayer = vp;
        int savedSoundLevel = PlayerPrefs.GetInt("soundLevel", 3);
        SetVolumeLevel(savedSoundLevel);
    }
}
