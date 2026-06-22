using UnityEngine;
using System;
using System.IO;
using System.Runtime.InteropServices;

[DefaultExecutionOrder(-1000)]
public class AntiPiracySystem : MonoBehaviour
{
    private string deviceKey;
    private string savedKey;
    private string installKeyPath;
    private string userKeyPath;

    void Start()
    {
        // Получаем уникальный идентификатор устройства
        deviceKey = SystemInfo.deviceUniqueIdentifier;

        // Пути хранения файла
        // В билде: Application.dataPath — это папка *_Data рядом с .exe
        // В Editor: это папка Assets проекта
        installKeyPath = Path.Combine(Application.dataPath, "device_key.txt");
        userKeyPath = Path.Combine(Application.persistentDataPath, "device_key.txt");

        bool keyLoaded = false;

        // 1) Пробуем работать с папкой установки (installKeyPath)
        try
        {
            if (File.Exists(installKeyPath))
            {
                savedKey = File.ReadAllText(installKeyPath).Trim();
                keyLoaded = true;
                Debug.Log("[AntiPiracy] Найден ключ в папке установки: " + installKeyPath);
            }
            else
            {
                File.WriteAllText(installKeyPath, deviceKey);
                savedKey = deviceKey;
                keyLoaded = true;
                Debug.Log("[AntiPiracy] Создан ключ в папке установки: " + installKeyPath);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[AntiPiracy] Нет доступа к папке установки: " + e.Message);
        }

        // 2) Если не получилось с installKeyPath — пробуем persistentDataPath
        if (!keyLoaded)
        {
            try
            {
                if (File.Exists(userKeyPath))
                {
                    savedKey = File.ReadAllText(userKeyPath).Trim();
                    Debug.Log("[AntiPiracy] Найден ключ в папке пользователя: " + userKeyPath);
                }
                else
                {
                    File.WriteAllText(userKeyPath, deviceKey);
                    savedKey = deviceKey;
                    Debug.Log("[AntiPiracy] Создан ключ в папке пользователя: " + userKeyPath);
                }

                keyLoaded = true;
            }
            catch (Exception e)
            {
                Debug.LogError("[AntiPiracy] Не удалось создать/прочитать ключ вообще: " + e.Message);
                // Чтобы не заблокировать игру из-за проблем с файловой системой,
                // считаем текущее устройство "правильным".
                savedKey = deviceKey;
                keyLoaded = true;
            }
        }

        // 3) Проверяем ключ
        if (savedKey != deviceKey)
        {
            Debug.LogError("Пиратская версия! Ключ не совпадает.");
            ShowPiracyWarning();
        }
        else
        {
            Debug.Log("Игра запущена на авторизованном устройстве.");
        }
    }

    // Метод для вывода стандартного окна Windows
    void ShowPiracyWarning()
    {
#if UNITY_STANDALONE_WIN
        // В Editor под Windows тоже сработает
        NativeWinAlert.Error(
            "Warning! You are using a pirated version of the game!\nThe game will be closed.",
            "Pirated version detected"
        );
#else
        Debug.LogError("Pirated version detected. Quitting.");
#endif

        Application.Quit(); // Завершаем работу приложения
    }
}

// Класс для Windows MessageBox
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
            // 0x00000010L = MB_ICONERROR
            MessageBox(GetActiveWindow(), text, caption, (uint)(0x00000010L));
        }
        catch (Exception ex)
        {
            Debug.LogError("Ошибка при вызове MessageBox: " + ex.Message);
        }
    }
}
