using UnityEngine;
using System;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
using System.IO;
using System.IO.Ports;
#endif

public class SerialPortManager : MonoBehaviour
{
    public static SerialPortManager Instance { get; private set; }

    [Header("Serial Port Settings")]
    [Tooltip("Используется только на Windows/Windows Editor. На Android COM-порт отключается автоматически.")]
    public string comPort = "COM7";

    [Tooltip("Файл конфигурации в StreamingAssets. Пример строки внутри файла: COM_PORT=COM7")]
    public string configFileName = "com_port_config.txt";

    [Header("Platform Settings")]
    [Tooltip("Разрешает работу COM-порта на поддерживаемых платформах. На Android всё равно будет принудительно отключено.")]
    [SerializeField] private bool enableSerialPortOnSupportedPlatforms = true;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    private SerialPort port;
#endif

    private bool portIsOpen = false;
    private bool portInitialized = false;

    private readonly byte[] packetBuffer = new byte[6];
    private int bufferIndex = 0;
    private float lastPacketTime = 0f;
    private const float PACKET_TIMEOUT = 1.5f;

    public event Action<int> OnCoinsReceived;

    public bool IsSerialActive => portIsOpen && portInitialized;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        if (!ShouldUseSerialPortOnThisRun())
        {
            Debug.Log("[SerialPortManager] COM-порт отключён для текущей платформы или текущего режима запуска.");
            return;
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        LoadComPortFromFile();
        InitializeSerialPort();
#endif
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
    private void LoadComPortFromFile()
    {
        string filePath = Path.Combine(Application.streamingAssetsPath, configFileName);

        if (!File.Exists(filePath))
        {
            Debug.Log($"[SerialPortManager] Файл конфигурации не найден: {filePath}. Используется порт по умолчанию: {comPort}");
            return;
        }

        try
        {
            string[] lines = File.ReadAllLines(filePath);

            foreach (string line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                string trimmedLine = line.Trim();

                if (trimmedLine.StartsWith("#"))
                {
                    continue;
                }

                if (!trimmedLine.Contains("="))
                {
                    continue;
                }

                string[] parts = trimmedLine.Split('=');

                if (parts.Length != 2)
                {
                    continue;
                }

                string key = parts[0].Trim();
                string value = parts[1].Trim();

                if (!key.Equals("COM_PORT", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (string.IsNullOrEmpty(value))
                {
                    continue;
                }

                comPort = value;
                Debug.Log($"[SerialPortManager] COM-порт изменён из файла конфигурации: {comPort}");
                break;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SerialPortManager] Ошибка при чтении файла конфигурации: {ex.Message}");
        }
    }

    private void InitializeSerialPort()
    {
        if (portInitialized)
        {
            return;
        }

        try
        {
            port = new SerialPort(comPort, 9600)
            {
                ReadTimeout = 50,
                DtrEnable = true,
                RtsEnable = true
            };

            port.Open();

            portIsOpen = true;
            portInitialized = true;

            Debug.Log($"[SerialPortManager] Serial Port {comPort} успешно открыт.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SerialPortManager] Ошибка при открытии порта {comPort}: {ex.Message}");

            portIsOpen = false;
            portInitialized = false;
        }
    }
#endif

    private void Update()
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        if (!ShouldUseSerialPortOnThisRun())
        {
            return;
        }

        if (!portIsOpen || port == null || !port.IsOpen)
        {
            return;
        }

        try
        {
            while (port.BytesToRead > 0 && bufferIndex < 6)
            {
                byte data = (byte)port.ReadByte();
                lastPacketTime = Time.time;

                if (bufferIndex == 0 && data != 0xFA)
                {
                    continue;
                }

                packetBuffer[bufferIndex] = data;
                bufferIndex++;

                if (bufferIndex == 6)
                {
                    ProcessReceivedPacket();
                    bufferIndex = 0;
                }
            }

            if (bufferIndex > 0 && Time.time - lastPacketTime > PACKET_TIMEOUT)
            {
                bufferIndex = 0;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SerialPortManager] Ошибка при чтении порта: {ex.Message}");
            bufferIndex = 0;
        }
#endif
    }

    private void ProcessReceivedPacket()
    {
        bool isValidPacket =
            packetBuffer[0] == 0xFA &&
            packetBuffer[1] == 0xFA &&
            packetBuffer[2] == 0xFA &&
            packetBuffer[5] == 0xF0;

        if (!isValidPacket)
        {
            return;
        }

        int credit = (packetBuffer[3] << 8) | packetBuffer[4];

        if (credit <= 0)
        {
            return;
        }

        Debug.Log($"[SerialPortManager] Получен кредит: {credit}");
        OnCoinsReceived?.Invoke(credit);
    }

    private void OnApplicationQuit()
    {
        CloseSerialPort();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            CloseSerialPort();
            Instance = null;
        }
    }

    private void CloseSerialPort()
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        try
        {
            if (port != null && port.IsOpen)
            {
                port.Close();
                port.Dispose();
                Debug.Log("[SerialPortManager] Serial Port закрыт.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[SerialPortManager] Ошибка при закрытии порта: {ex.Message}");
        }
#endif

        portIsOpen = false;
        portInitialized = false;
    }

    public void ReinitializePort(string newPort)
    {
        comPort = newPort;

        if (!ShouldUseSerialPortOnThisRun())
        {
            Debug.Log("[SerialPortManager] ReinitializePort пропущен: COM-порт отключён для текущей платформы.");
            return;
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        CloseSerialPort();
        InitializeSerialPort();
#endif
    }

    public void SimulateCoinForDebug(int coins = 1)
    {
        if (coins <= 0)
        {
            return;
        }

        Debug.Log($"[SerialPortManager] Debug-эмуляция монеты: {coins}");
        OnCoinsReceived?.Invoke(coins);
    }
}