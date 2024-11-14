using UnityEngine;
using UnityEngine.Video;

public class VideoSceneController : MonoBehaviour
{
    private VideoPlayer videoPlayer;

    void Start()
    {
        videoPlayer = GetComponent<VideoPlayer>();
        if (SoundManager.Instance != null && videoPlayer != null)
        {
            SoundManager.Instance.SetVideoPlayer(videoPlayer);
        }
    }
}
