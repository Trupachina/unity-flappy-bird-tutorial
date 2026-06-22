using UnityEngine;
using UnityEngine.UI;
using System.IO.Ports;
using System.Text;
using System.Collections;

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

    private SerialPort serialPort;
    private StringBuilder rawDataLog = new StringBuilder();
    private int coinCount = 0;
    private int creditMultiplier = 1;
    private bool isReceiving = false;
    private byte[] packetBuffer = new byte[6];
    private int bufferIndex = 0;

    private void Start()
    {
        // Настройка кнопок
        connectButton.onClick.AddListener(ConnectToPort);
        disconnectButton.onClick.AddListener(DisconnectPort);
        testCoinButton.onClick.AddListener(SimulateCoin);
        setMultiplierButton.onClick.AddListener(SetCoinMultiplier);

        // Начальное состояние
        UpdateUI();
        disconnectButton.interactable = false;
        testCoinButton.interactable = false;
    }

    private void Update()
    {
        if (serialPort != null && serialPort.IsOpen)
        {
            ReadSerialData();
        }
    }

    private void ConnectToPort()
    {
        try
        {
            serialPort = new SerialPort(comPort, baudRate);
            serialPort.ReadTimeout = 50;
            serialPort.Open();

            comStatusText.text = $"Подключено к {comPort}";
            comStatusText.color = Color.green;

            connectButton.interactable = false;
            disconnectButton.interactable = true;
            testCoinButton.interactable = true;
        }
        catch (System.Exception ex)
        {
            comStatusText.text = $"Ошибка: {ex.Message}";
            comStatusText.color = Color.red;
        }
    }

    private void DisconnectPort()
    {
        if (serialPort != null && serialPort.IsOpen)
        {
            serialPort.Close();
            serialPort.Dispose();
        }

        comStatusText.text = "Отключено";
        comStatusText.color = Color.yellow;

        connectButton.interactable = true;
        disconnectButton.interactable = false;
        testCoinButton.interactable = false;
    }

    private void ReadSerialData()
    {
        try
        {
            while (serialPort.BytesToRead > 0)
            {
                byte data = (byte)serialPort.ReadByte();
                ProcessByte(data);

                // Обновляем лог сырых данных
                rawDataLog.Append($"{data:X2} ");
                if (rawDataLog.Length > 200) rawDataLog.Remove(0, 50);
                rawDataText.text = rawDataLog.ToString();
            }
        }
        catch (System.TimeoutException) { }
        catch (System.Exception ex)
        {
            decodedDataText.text = $"Ошибка чтения: {ex.Message}";
        }
    }

    private void ProcessByte(byte data)
    {
        // Поиск начала пакета
        if (bufferIndex == 0 && data != 0xFA) return;

        packetBuffer[bufferIndex++] = data;

        // Проверка полного пакета
        if (bufferIndex == 6)
        {
            if (packetBuffer[0] == 0xFA &&
                packetBuffer[1] == 0xFA &&
                packetBuffer[2] == 0xFA &&
                packetBuffer[5] == 0xF0)
            {
                int credit = (packetBuffer[3] << 8) | packetBuffer[4];

                if (credit > 0)
                {
                    // Учет множителя
                    int coinsReceived = credit * creditMultiplier;
                    coinCount += coinsReceived;

                    decodedDataText.text = $"Получено: {credit} кредит(ов) -> {coinsReceived} монет(ы)";
                }
                else
                {
                    decodedDataText.text = "Тестовый пакет";
                }

                UpdateUI();
            }
            else
            {
                decodedDataText.text = "Некорректный пакет";
            }

            bufferIndex = 0;
        }
    }

    private void SimulateCoin()
    {
        // Эмуляция монеты (1 кредит)
        byte[] testCoin = { 0xFA, 0xFA, 0xFA, 0x00, 0x01, 0xF0 };
        ProcessByteArray(testCoin);
    }

    private void ProcessByteArray(byte[] data)
    {
        foreach (byte b in data)
        {
            ProcessByte(b);
        }
    }

    private void SetCoinMultiplier()
    {
        if (int.TryParse(multiplierInput.text, out int multiplier) &&
            multiplier >= 1 && multiplier <= 200)
        {
            creditMultiplier = multiplier;
            decodedDataText.text = $"Множитель установлен: {multiplier}";
        }
        else
        {
            decodedDataText.text = "Недопустимое значение (1-200)";
        }
    }

    private void UpdateUI()
    {
        coinCounterText.text = $"Всего монет: {coinCount}";
    }

    private void OnApplicationQuit()
    {
        DisconnectPort();
    }
}