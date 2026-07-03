using UnityEngine;
using UnityEngine.UI;
using System.Text;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
using System.IO.Ports;
#endif

public class CoinAcceptorTester : MonoBehaviour
{
    [Header("Serial Settings")]
    public string comPort = "COM3";
    public int baudRate = 9600;

    [Header("UI References")]
    public Text comStatusText;
    public Text rawDataText;
    public Text decodedDataText;
    public Text coinCounterText;
    public InputField multiplierInput;
    public Button connectButton;
    public Button disconnectButton;
    public Button testCoinButton;
    public Button setMultiplierButton;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    private SerialPort serialPort;
#endif

    private readonly StringBuilder rawDataLog = new StringBuilder();
    private int coinCount = 0;
    private int creditMultiplier = 1;
    private readonly byte[] packetBuffer = new byte[6];
    private int bufferIndex = 0;

    private void Start()
    {
        if (connectButton != null)
        {
            connectButton.onClick.AddListener(ConnectToPort);
        }

        if (disconnectButton != null)
        {
            disconnectButton.onClick.AddListener(DisconnectPort);
        }

        if (testCoinButton != null)
        {
            testCoinButton.onClick.AddListener(SimulateCoin);
        }

        if (setMultiplierButton != null)
        {
            setMultiplierButton.onClick.AddListener(SetCoinMultiplier);
        }

        UpdateUI();

#if UNITY_ANDROID
        SetStatus("Serial Port отключён на Android", Color.yellow);
        SetButtonsState(isConnected: false);

        if (connectButton != null)
        {
            connectButton.interactable = false;
        }

        if (disconnectButton != null)
        {
            disconnectButton.interactable = false;
        }

        if (testCoinButton != null)
        {
            testCoinButton.interactable = true;
        }
#elif UNITY_EDITOR
        if (UnityEditor.EditorUserBuildSettings.activeBuildTarget == UnityEditor.BuildTarget.Android)
        {
            SetStatus("Serial Port отключён: Editor работает под Android Build Target", Color.yellow);
            SetButtonsState(isConnected: false);

            if (connectButton != null)
            {
                connectButton.interactable = false;
            }

            if (disconnectButton != null)
            {
                disconnectButton.interactable = false;
            }

            if (testCoinButton != null)
            {
                testCoinButton.interactable = true;
            }

            return;
        }

        SetStatus("Отключено", Color.yellow);
        SetButtonsState(isConnected: false);
#else
        SetStatus("Отключено", Color.yellow);
        SetButtonsState(isConnected: false);
#endif
    }

    private void Update()
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        if (!ShouldUseSerialPortOnThisRun())
        {
            return;
        }

        if (serialPort != null && serialPort.IsOpen)
        {
            ReadSerialData();
        }
#endif
    }

    private bool ShouldUseSerialPortOnThisRun()
    {
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

    private void ConnectToPort()
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        if (!ShouldUseSerialPortOnThisRun())
        {
            SetStatus("Подключение невозможно: Serial Port отключён для текущей платформы", Color.red);
            return;
        }

        try
        {
            serialPort = new SerialPort(comPort, baudRate)
            {
                ReadTimeout = 50
            };

            serialPort.Open();

            SetStatus($"Подключено к {comPort}", Color.green);
            SetButtonsState(isConnected: true);
        }
        catch (System.Exception ex)
        {
            SetStatus($"Ошибка: {ex.Message}", Color.red);
        }
#else
        SetStatus("Подключение невозможно: Serial Port отключён для текущей платформы", Color.red);
#endif
    }

    private void DisconnectPort()
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        try
        {
            if (serialPort != null && serialPort.IsOpen)
            {
                serialPort.Close();
                serialPort.Dispose();
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[CoinAcceptorTester] Ошибка при отключении порта: {ex.Message}");
        }
#endif

        SetStatus("Отключено", Color.yellow);
        SetButtonsState(isConnected: false);
    }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    private void ReadSerialData()
    {
        try
        {
            while (serialPort.BytesToRead > 0)
            {
                byte data = (byte)serialPort.ReadByte();
                ProcessByte(data);

                rawDataLog.Append($"{data:X2} ");

                if (rawDataLog.Length > 200)
                {
                    rawDataLog.Remove(0, 50);
                }

                if (rawDataText != null)
                {
                    rawDataText.text = rawDataLog.ToString();
                }
            }
        }
        catch (System.TimeoutException)
        {
        }
        catch (System.Exception ex)
        {
            if (decodedDataText != null)
            {
                decodedDataText.text = $"Ошибка чтения: {ex.Message}";
            }
        }
    }
#endif

    private void ProcessByte(byte data)
    {
        if (bufferIndex == 0 && data != 0xFA)
        {
            return;
        }

        packetBuffer[bufferIndex] = data;
        bufferIndex++;

        if (bufferIndex < 6)
        {
            return;
        }

        bool isValidPacket =
            packetBuffer[0] == 0xFA &&
            packetBuffer[1] == 0xFA &&
            packetBuffer[2] == 0xFA &&
            packetBuffer[5] == 0xF0;

        if (isValidPacket)
        {
            int credit = (packetBuffer[3] << 8) | packetBuffer[4];

            if (credit > 0)
            {
                int coinsReceived = credit * creditMultiplier;
                coinCount += coinsReceived;

                if (decodedDataText != null)
                {
                    decodedDataText.text = $"Получено: {credit} кредит(ов) -> {coinsReceived} монет(ы)";
                }
            }
            else
            {
                if (decodedDataText != null)
                {
                    decodedDataText.text = "Тестовый пакет";
                }
            }

            UpdateUI();
        }
        else
        {
            if (decodedDataText != null)
            {
                decodedDataText.text = "Некорректный пакет";
            }
        }

        bufferIndex = 0;
    }

    private void SimulateCoin()
    {
        byte[] testCoin =
        {
            0xFA,
            0xFA,
            0xFA,
            0x00,
            0x01,
            0xF0
        };

        ProcessByteArray(testCoin);
    }

    private void ProcessByteArray(byte[] data)
    {
        if (data == null)
        {
            return;
        }

        foreach (byte b in data)
        {
            ProcessByte(b);
        }
    }

    private void SetCoinMultiplier()
    {
        if (multiplierInput == null)
        {
            return;
        }

        bool isValid =
            int.TryParse(multiplierInput.text, out int multiplier) &&
            multiplier >= 1 &&
            multiplier <= 200;

        if (isValid)
        {
            creditMultiplier = multiplier;

            if (decodedDataText != null)
            {
                decodedDataText.text = $"Множитель установлен: {multiplier}";
            }
        }
        else
        {
            if (decodedDataText != null)
            {
                decodedDataText.text = "Недопустимое значение. Разрешено: 1-200";
            }
        }
    }

    private void UpdateUI()
    {
        if (coinCounterText != null)
        {
            coinCounterText.text = $"Всего монет: {coinCount}";
        }
    }

    private void SetButtonsState(bool isConnected)
    {
        if (connectButton != null)
        {
            connectButton.interactable = !isConnected;
        }

        if (disconnectButton != null)
        {
            disconnectButton.interactable = isConnected;
        }

        if (testCoinButton != null)
        {
            testCoinButton.interactable = true;
        }
    }

    private void SetStatus(string message, Color color)
    {
        if (comStatusText == null)
        {
            return;
        }

        comStatusText.text = message;
        comStatusText.color = color;
    }

    private void OnApplicationQuit()
    {
        DisconnectPort();
    }

    private void OnDestroy()
    {
        DisconnectPort();
    }
}