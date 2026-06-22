using UnityEngine;
using UnityEngine.SceneManagement;

public class Preloader : MonoBehaviour
{
    void Start()
    {
        // Создаем и инициализируем менеджер последовательного порта
        GameObject serialPortObject = new GameObject("SerialPortManager");
        serialPortObject.AddComponent<SerialPortManager>();
        DontDestroyOnLoad(serialPortObject);

        // Загружаем основную сцену
        SceneManager.LoadScene("Flappy Bird");
    }
}