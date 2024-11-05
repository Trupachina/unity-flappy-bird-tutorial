using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.IO.Ports;

[DefaultExecutionOrder(-1)]
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [SerializeField] private Player player;
    [SerializeField] private Spawner spawner;
    [SerializeField] private Text scoreText;
    [SerializeField] private Text livesText; // UI для отображения количества жизней
    [SerializeField] private GameObject playButton;
    [SerializeField] private GameObject gameOver;
    [SerializeField] private GameObject logo;
    [SerializeField] private GameObject company;
    public AudioClip dieSound; // Поле для звука смерти

    public int score { get; private set; } = 0;
    public int lives = 3; // Начальное количество жизней
    private bool isGameOver = false; // Флаг для проверки состояния game over
    private AudioSource audioSource; // Компонент AudioSource

    [Header("Serial Port Settings")]
    public string comPort = "COM3"; // Укажите здесь COM-порт
    private SerialPort portNo; // Порт для подключения SerialPort
    private bool portIsOpen = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        //DontDestroyOnLoad(gameObject); // Оставить GameManager при переходе между сценами, если требуется
    }

    private void Start()
    {
        Pause();
        UpdateLivesText(); // Обновляем отображение количества жизней

        // Инициализация компонента AudioSource
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        // Установка всех анимаций на UnscaledTime
        Animator[] animators = FindObjectsOfType<Animator>();
        foreach (Animator animator in animators)
        {
            animator.updateMode = AnimatorUpdateMode.UnscaledTime;
        }

        // Инициализация порта
        portNo = new SerialPort(comPort, 19200); // Задайте нужный COM-порт
        try
        {
            portNo.Open();
            portNo.ReadTimeout = 1000;
            portIsOpen = true;
            Debug.Log("Serial Port открыт.");
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Ошибка при открытии порта: " + ex.Message);
        }
    }

    private void Update()
    {
        // Запуск игры по нажатию пробела, если кнопка play активна и не в состоянии game over
        if (playButton.activeSelf && Input.GetKeyDown(KeyCode.Space) && !isGameOver && lives > 0)
        {
            Play();
        }

        // Перезапуск сцены по нажатию пробела после game over
        if (isGameOver && Input.GetKeyDown(KeyCode.Space))
        {
            RestartScene();
        }

        // Проверка порта на получение данных
        if (portIsOpen)
        {
            try
            {
                if (portNo.BytesToRead > 0)
                {
                    int portValue = portNo.ReadByte();
                    Debug.Log("Получен байт: " + portValue);

                    // Если получен сигнал для добавления жизни
                    if (portValue == 1)
                    {
                        AddLife();
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError("Ошибка при чтении порта: " + ex.Message);
            }
        }
    }

    public void Pause()
    {
        Time.timeScale = 0f;
        player.enabled = false;
    }

    public void Play()
    {
        score = 0;
        scoreText.text = score.ToString();

        playButton.SetActive(false);
        gameOver.SetActive(false);
        logo.SetActive(false);
        company.SetActive(false);

        Time.timeScale = 1f;
        player.enabled = true;
        isGameOver = false; // Сбрасываем флаг game over

        Pipes[] pipes = FindObjectsOfType<Pipes>();
        for (int i = 0; i < pipes.Length; i++)
        {
            Destroy(pipes[i].gameObject);
        }
    }

    public void GameOver()
    {
        // Уменьшение жизней при окончании игры
        lives--;
        UpdateLivesText();

        if (lives <= 0)
        {
            // Показать экран game over и установить флаг
            gameOver.SetActive(true);
            playButton.SetActive(true); // Показываем playButton для повторного запуска
            Pause();
            isGameOver = true; // Устанавливаем флаг game over

            // Проигрываем звук смерти, если dieSound задан
            if (dieSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(dieSound);
            }

            if (portNo != null && portNo.IsOpen)
            {
                portNo.Close();
                Debug.Log("Serial Port закрыт.");
            }
        }
        else
        {
            // Если еще есть жизни, просто сбрасываем позицию игрока
            player.ResetPosition();
        }
    }

    public void IncreaseScore()
    {
        score++;
        scoreText.text = score.ToString();
    }

    // Метод для добавления жизней
    private void AddLife()
    {
        lives++;
        UpdateLivesText();
        Debug.Log("Добавлена жизнь. Текущее количество жизней: " + lives);
    }

    // Метод для обновления UI, отображающего количество жизней
    private void UpdateLivesText()
    {
        if (livesText != null)
        {
            livesText.text = "Жизни: " + lives.ToString();
        }
    }

    // Метод для перезапуска сцены
    private void RestartScene()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private void OnApplicationQuit()
    {
        if (portNo != null && portNo.IsOpen)
        {
            portNo.Close();
            Debug.Log("Serial Port закрыт.");
        }
    }
}
