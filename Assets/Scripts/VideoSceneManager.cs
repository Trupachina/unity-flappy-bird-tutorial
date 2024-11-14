using UnityEngine;
using UnityEngine.SceneManagement;
using System.IO.Ports;

public class VideoSceneManager : MonoBehaviour
{
    private const string StartSceneName = "Flappy Bird";
    public string comPort = "COM3";
    private SerialPort portNo;
    private bool portIsOpen = false;

    private void OnEnable()
    {
        portNo = new SerialPort(comPort, 19200);
        try
        {
            portNo.Open();
            portNo.ReadTimeout = 1000;
            portIsOpen = true;
            Debug.Log("Serial Port ������ ��� ���������������.");
        }
        catch (System.Exception ex)
        {
            Debug.LogError("������ ��� �������� ����� ��� ���������������: " + ex.Message);
        }
    }

    private void OnDisable()
    {
        if (portNo != null && portNo.IsOpen)
        {
            portNo.Close();
            Debug.Log("Serial Port ������ ��� ����� �����.");
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            SceneManager.LoadScene(StartSceneName);
        }

        if (Input.GetKeyDown(KeyCode.L))
        {
            LoadStartSceneWithLife();
        }

        if (portIsOpen)
        {
            try
            {
                if (portNo.BytesToRead > 0)
                {
                    int portValue = portNo.ReadByte();
                    Debug.Log("������� ���� �� ���������������: " + portValue);

                    if (portValue == 1)
                    {
                        LoadStartSceneWithLife();
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError("������ ��� ������ �����: " + ex.Message);
            }
        }
    }

    private void LoadStartSceneWithLife()
    {
        PlayerPrefs.SetInt("ExtraLife", 1);
        PlayerPrefs.Save();

        try
        {
            Debug.Log("������������ �� ��������� ����� � �������������� ������...");
            SceneManager.LoadScene(StartSceneName);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"������ ��� �������� ��������� ����� '{StartSceneName}': {ex.Message}");
        }
    }
}
