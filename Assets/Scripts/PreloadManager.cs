using UnityEngine;
using UnityEngine.SceneManagement;

public class Preloader : MonoBehaviour
{
    [Header("Scene Settings")]
    [SerializeField] private string mainSceneName = "Flappy Bird";

    [Header("Serial Port")]
    [Tooltip("Разрешает создавать SerialPortManager в Windows Editor, если активная платформа не Android.")]
    [SerializeField] private bool createSerialPortManagerInEditorWindows = true;

    private void Start()
    {
        CreateSerialPortManagerIfNeeded();
        SceneManager.LoadScene(mainSceneName);
    }

    private void CreateSerialPortManagerIfNeeded()
    {
#if UNITY_ANDROID
        Debug.Log("[Preloader] Android: SerialPortManager не создаётся.");
        return;
#elif UNITY_EDITOR
        if (UnityEditor.EditorUserBuildSettings.activeBuildTarget == UnityEditor.BuildTarget.Android)
        {
            Debug.Log("[Preloader] Editor с Android Build Target: SerialPortManager не создаётся.");
            return;
        }

        if (!createSerialPortManagerInEditorWindows)
        {
            Debug.Log("[Preloader] Создание SerialPortManager в Editor отключено настройкой.");
            return;
        }

        CreateSerialPortManagerObject();
#elif UNITY_STANDALONE_WIN
        CreateSerialPortManagerObject();
#else
        Debug.Log("[Preloader] Текущая платформа не использует SerialPortManager.");
#endif
    }

    private void CreateSerialPortManagerObject()
    {
        if (SerialPortManager.Instance != null)
        {
            Debug.Log("[Preloader] SerialPortManager уже существует.");
            return;
        }

        GameObject serialPortObject = new GameObject("SerialPortManager");
        serialPortObject.AddComponent<SerialPortManager>();
        DontDestroyOnLoad(serialPortObject);

        Debug.Log("[Preloader] SerialPortManager создан.");
    }
}