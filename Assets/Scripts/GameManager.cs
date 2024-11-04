using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

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
    public int lives { get; private set; } = 3; // Начальное количество жизней
    private bool isGameOver = false; // Флаг для проверки состояния game over
    private AudioSource audioSource; // Компонент AudioSource

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }
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
    }

    private void Update()
    {
        // Запуск игры по нажатию пробела, если кнопка play активна и не в состоянии game over
        if (playButton.activeSelf && Input.GetKeyDown(KeyCode.Space) && !isGameOver)
        {
            Play();
        }

        // Перезапуск сцены по нажатию пробела после game over
        if (isGameOver && Input.GetKeyDown(KeyCode.Space))
        {
            RestartScene();
        }

        // Увеличение жизней по нажатию клавиши P
        if (Input.GetKeyDown(KeyCode.P))
        {
            AddLife();
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
    public void AddLife()
    {
        lives++;
        UpdateLivesText();
    }

    // Метод для обновления UI, отображающего количество жизней
    private void UpdateLivesText()
    {
        if (livesText != null)
        {
            livesText.text = "Lives: " + lives.ToString();
        }
    }

    // Метод для перезапуска сцены
    private void RestartScene()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
