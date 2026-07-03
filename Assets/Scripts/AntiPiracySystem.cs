using UnityEngine;
using System;
using System.IO;

#if UNITY_STANDALONE_WIN
using System.Runtime.InteropServices;
#endif

[DefaultExecutionOrder(-1000)]
public class AntiPiracySystem : MonoBehaviour
{
    [Header("Anti Piracy Settings")]
    [Tooltip("Если выключено, проверка привязки к устройству не выполняется.")]
    [SerializeField] private bool enableAntiPiracyCheck = true;

    [Tooltip("Если включено, игра закроется при несовпадении ключа устройства.")]
    [SerializeField] private bool quitApplicationOnPiracyDetected = true;

    private string deviceKey;
    private string savedKey;
    private string installKeyPath;
    private string userKeyPath;

    private void Start()
    {
        if (!enableAntiPiracyCheck)
        {
            Debug.Log("[AntiPiracy] Проверка отключена в настройках компонента.");
            return;
        }

        deviceKey = SystemInfo.deviceUniqueIdentifier;

        if (string.IsNullOrEmpty(deviceKey))
        {
            Debug.LogWarning("[AntiPiracy] SystemInfo.deviceUniqueIdentifier пустой. Проверка пропущена, чтобы не заблокировать игру.");
            return;
        }

        installKeyPath = Path.Combine(Application.dataPath, "device_key.txt");
        userKeyPath = Path.Combine(Application.persistentDataPath, "device_key.txt");

        bool keyLoaded = TryLoadOrCreateKeyInInstallFolder();

        if (!keyLoaded)
        {
            keyLoaded = TryLoadOrCreateKeyInPersistentFolder();
        }

        if (!keyLoaded)
        {
            Debug.LogWarning("[AntiPiracy] Не удалось загрузить или создать ключ. Текущее устройство считается разрешённым, чтобы не заблокировать игру из-за файловой системы.");
            savedKey = deviceKey;
            keyLoaded = true;
        }

        ValidateKey();
    }

    private bool TryLoadOrCreateKeyInInstallFolder()
    {
        try
        {
            if (File.Exists(installKeyPath))
            {
                savedKey = File.ReadAllText(installKeyPath).Trim();
                Debug.Log("[AntiPiracy] Найден ключ в папке установки: " + installKeyPath);
                return true;
            }

            File.WriteAllText(installKeyPath, deviceKey);
            savedKey = deviceKey;

            Debug.Log("[AntiPiracy] Создан ключ в папке установки: " + installKeyPath);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning("[AntiPiracy] Нет доступа к папке установки. Будет использован persistentDataPath. Причина: " + e.Message);
            return false;
        }
    }

    private bool TryLoadOrCreateKeyInPersistentFolder()
    {
        try
        {
            if (File.Exists(userKeyPath))
            {
                savedKey = File.ReadAllText(userKeyPath).Trim();
                Debug.Log("[AntiPiracy] Найден ключ в пользовательской папке: " + userKeyPath);
                return true;
            }

            File.WriteAllText(userKeyPath, deviceKey);
            savedKey = deviceKey;

            Debug.Log("[AntiPiracy] Создан ключ в пользовательской папке: " + userKeyPath);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError("[AntiPiracy] Не удалось создать или прочитать ключ в persistentDataPath: " + e.Message);
            return false;
        }
    }

    private void ValidateKey()
    {
        if (savedKey == deviceKey)
        {
            Debug.Log("[AntiPiracy] Игра запущена на авторизованном устройстве.");
            return;
        }

        Debug.LogError("[AntiPiracy] Обнаружено несовпадение ключа устройства.");
        ShowPiracyWarning();
    }

    private void ShowPiracyWarning()
    {
#if UNITY_STANDALONE_WIN
        NativeWinAlert.Error(
            "Warning! You are using a pirated version of the game!\nThe game will be closed.",
            "Pirated version detected"
        );
#else
        Debug.LogError("[AntiPiracy] Pirated version detected.");
#endif

        if (quitApplicationOnPiracyDetected)
        {
            Application.Quit();
        }
    }
}

#if UNITY_STANDALONE_WIN
public static class NativeWinAlert
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern int MessageBox(IntPtr hwnd, string lpText, string lpCaption, uint uType);

    [DllImport("user32.dll")]
    private static extern IntPtr GetActiveWindow();

    public static void Error(string text, string caption)
    {
        try
        {
            MessageBox(GetActiveWindow(), text, caption, 0x00000010);
        }
        catch (Exception ex)
        {
            Debug.LogError("[NativeWinAlert] Ошибка при вызове Windows MessageBox: " + ex.Message);
        }
    }
}
#endif