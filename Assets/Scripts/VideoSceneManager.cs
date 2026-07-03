using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
using System.IO.Ports;
#endif

public class VideoSceneManager : MonoBehaviour
{
    [Header("Scene Settings")]
    [SerializeField] private string startSceneName = "Flappy Bird";

    [Header("Serial Port Settings")]
    [SerializeField] private string comPort = "COM3";
    [SerializeField] private int baudRate = 9600;

    [Header("Platform Settings")]
    [SerializeField] private bool enableSerialPortOnSupportedPlatforms = true;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    private SerialPort portNo;
#endif

    private bool portIsOpen = false;

    private void OnEnable()
    {
        if (!ShouldUseSerialPortOnThisRun())
        {
            Debug.Log("[VideoSceneManager] COM-порт отключён для текущей платформы или режима запуска.");
            return;
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        OpenSerialPort();
#endif
    }

    private void OnDisable()
    {
        CloseSerialPort();
    }

    private bool ShouldUseSerialPortOnThisRun()
    {
        if (!enableSerialPortOnSupportedPlatforms)
        {
            return false;
        }

#if UNITY_ANDROID
        return false;
#elif UNITY_EDITOR
        if (UnityEditor.EditorUserBuildSettings.activeBuildTarget == UnityEditor.BuildTarget.Android)
        {
            return false;
        }

        return true;
#elif UNITY_STANDALONE_WIN
        return true;
#else
        return false;
#endif
    }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    private void OpenSerialPort()
    {
        try
        {
            portNo = new SerialPort(comPort, baudRate)
            {
                ReadTimeout = 1000
            };

            portNo.Open();
            portIsOpen = true;

            Debug.Log($"[VideoSceneManager] Serial Port открыт: {comPort}");
        }
        catch (System.Exception ex)
        {
            portIsOpen = false;
            Debug.LogError($"[VideoSceneManager] Ошибка при открытии порта {comPort}: {ex.Message}");
        }
    }
#endif

    private void Update()
    {
        if (IsJumpPressed())
        {
            SceneManager.LoadScene(startSceneName);
            return;
        }

        if (Input.GetKeyDown(KeyCode.L))
        {
            LoadStartSceneWithLife();
            return;
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        ReadSerialPortIfAvailable();
#endif
    }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    private void ReadSerialPortIfAvailable()
    {
        if (!ShouldUseSerialPortOnThisRun())
        {
            return;
        }

        if (!portIsOpen || portNo == null || !portNo.IsOpen)
        {
            return;
        }

        try
        {
            if (portNo.BytesToRead <= 0)
            {
                return;
            }

            int portValue = portNo.ReadByte();
            Debug.Log($"[VideoSceneManager] Получен байт от монетоприёмника: {portValue}");

            if (portValue == 1)
            {
                LoadStartSceneWithLife();
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[VideoSceneManager] Ошибка при чтении порта: {ex.Message}");
        }
    }
#endif

    private bool IsJumpPressed()
    {
        return Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0);
    }

    private void LoadStartSceneWithLife()
    {
        PlayerPrefs.SetInt("ExtraLife", 1);
        PlayerPrefs.Save();

        try
        {
            Debug.Log("[VideoSceneManager] Переход на стартовую сцену с дополнительной жизнью.");
            SceneManager.LoadScene(startSceneName);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[VideoSceneManager] Ошибка при загрузке сцены '{startSceneName}': {ex.Message}");
        }
    }

    private void CloseSerialPort()
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        try
        {
            if (portNo != null && portNo.IsOpen)
            {
                portNo.Close();
                portNo.Dispose();
                Debug.Log("[VideoSceneManager] Serial Port закрыт.");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[VideoSceneManager] Ошибка при закрытии порта: {ex.Message}");
        }
#endif

        portIsOpen = false;
    }
}