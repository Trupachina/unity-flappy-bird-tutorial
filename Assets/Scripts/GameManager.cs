using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.IO.Ports;
using System.Collections;

[DefaultExecutionOrder(-1)]
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [SerializeField] private Player player;
    [SerializeField] private Spawner spawner;
    [SerializeField] private Text scoreText;
    [SerializeField] private Text livesText;
    [SerializeField] private Text highScoreText; // Новое текстовое поле для отображения рекорда
    [SerializeField] private GameObject playButton;
    [SerializeField] private GameObject gameOver;
    [SerializeField] private Text gameOverScoreText;
    [SerializeField] private GameObject logo;
    [SerializeField] private GameObject company;
    [SerializeField] private GameObject continueText;
    [SerializeField] private Text timerText;
    public AudioClip dieSound;
    [SerializeField] private GameObject lifeLostTextPrefab;
    [SerializeField] private GameObject StartMusic;
    [SerializeField] private GameObject PlayMusic;
    [SerializeField] private GameObject GameOverMusic;

    public int score { get; private set; } = 0;
    public int lives = 3;
    public int deathTimerSeconds = 5;
    private bool isGameOver = false;
    private bool isPausedAfterCollision = false;
    private bool isInvincible = false;
    private AudioSource audioSource;
    private Coroutine deathTimerCoroutine;
    private Coroutine videoCountdownCoroutine;
    private bool awaitingSpaceToResume = false;

    private const string HighScoreKey = "HighScore";
    private const string VideoSceneName = "Visualization";
    private const string StartSceneName = "Flappy Bird";

    [Header("Token Settings")]
    public int livesPerToken = 1;

    [Header("Serial Port Settings")]
    public string comPort = "COM3";
    private SerialPort portNo;
    private bool portIsOpen = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnEnable()
    {
        if (portNo == null)
        {
            portNo = new SerialPort(comPort, 19200);
        }

        try
        {
            if (!portNo.IsOpen)
            {
                portNo.Open();
                portNo.ReadTimeout = 1000;
                portIsOpen = true;
                Debug.Log("Serial Port открыт.");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Ошибка при открытии порта: " + ex.Message);
        }
    }

    private void OnDisable()
    {
        if (portNo != null && portNo.IsOpen)
        {
            portNo.Close();
            Debug.Log("Serial Port закрыт при смене сцены.");
        }
    }

    public void Start()
    {
        Pause();
        UpdateLivesText();

        audioSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();

        StartMusic.SetActive(true);
        highScoreText.gameObject.SetActive(true);

        SetAnimatorsUpdateMode(AnimatorUpdateMode.UnscaledTime);

        if (Application.CanStreamedLevelBeLoaded(VideoSceneName))
        {
            Debug.Log($"Сцена '{VideoSceneName}' найдена. Запуск через 15 секунд...");
            videoCountdownCoroutine = StartCoroutine(StartCountdownToVideoScene(30));
        }
        else
        {
            Debug.LogError($"Сцена '{VideoSceneName}' не найдена! Проверьте имя сцены.");
        }

        DisplayHighScore();

        if (PlayerPrefs.HasKey("ExtraLife"))
        {
            AddLife();
            PlayerPrefs.DeleteKey("ExtraLife");
        }
    }

    private IEnumerator StartCountdownToVideoScene(int countdownTime)
    {
        while (countdownTime > 0)
        {
            if (lives > 0)
            {
                Debug.Log("Жизни больше 0, отмена запуска видео-сцены.");
                yield break;
            }

            Debug.Log($"До запуска сцены с видео осталось: {countdownTime} секунд...");
            yield return new WaitForSecondsRealtime(1f);
            countdownTime--;
        }

        LoadVideoScene();
    }

    private void Update()
    {
        if (playButton.activeSelf && Input.GetKeyDown(KeyCode.Space) && !isGameOver && lives > 0)
        {
            Play();
        }

        if (awaitingSpaceToResume && Input.GetKeyDown(KeyCode.Space))
        {
            awaitingSpaceToResume = false;
            Play();
        }

        if (isPausedAfterCollision && Input.GetKeyDown(KeyCode.Space))
        {
            isPausedAfterCollision = false;
            StartCoroutine(GrantInvincibility(0.5f));
            Play();
        }

        if (Input.GetKeyDown(KeyCode.L))
        {
            AddLife();
        }

        if (portIsOpen)
        {
            try
            {
                if (portNo.BytesToRead > 0)
                {
                    int portValue = portNo.ReadByte();
                    Debug.Log("Получен байт: " + portValue);

                    if (portValue == 1)
                    {
                        AddLife();

                        if (videoCountdownCoroutine != null)
                        {
                            StopCoroutine(videoCountdownCoroutine);
                            videoCountdownCoroutine = null;
                            Debug.Log("Корутина запуска видео-сцены остановлена, так как у игрока есть жизни.");
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError("Ошибка при чтении порта: " + ex.Message);
            }
        }
    }

    private void LoadVideoScene()
    {
        try
        {
            Debug.Log("Попытка загрузить сцену с видео...");
            SceneManager.LoadScene(VideoSceneName);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Ошибка при загрузке сцены '{VideoSceneName}': {ex.Message}");
        }
    }

    public void Pause()
    {
        Time.timeScale = 0f;
        player.enabled = false;
    }

    public void Play()
    {
        playButton.SetActive(false);
        gameOver.SetActive(false);
        continueText.SetActive(false);
        logo.SetActive(false);
        company.SetActive(false);
        StartMusic.SetActive(false);
        PlayMusic.SetActive(true);
        highScoreText.gameObject.SetActive(true);

        SetAnimatorsUpdateMode(AnimatorUpdateMode.Normal);

        Time.timeScale = 1f;
        player.enabled = true;

        if (videoCountdownCoroutine != null)
        {
            StopCoroutine(videoCountdownCoroutine);
            videoCountdownCoroutine = null;
        }
    }

    public void GameOver()
    {
        lives--;
        UpdateLivesText();

        if (lives > 0)
        {
            ShowLifeLostText();
            isPausedAfterCollision = true;
            player.ResetPosition();
            Pause();
        }
        else
        {
            gameOver.SetActive(true);
            continueText.SetActive(true);
            GameOverMusic.SetActive(true);
            PlayMusic.SetActive(false);
            highScoreText.gameObject.SetActive(false);

            Pause();
            isGameOver = true;

            SetAnimatorsUpdateMode(AnimatorUpdateMode.UnscaledTime);

            if (dieSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(dieSound);
            }

            if (portNo != null && portNo.IsOpen)
            {
                portNo.Close();
                Debug.Log("Serial Port закрыт.");
            }

            UpdateHighScore();
            DisplayGameOverScore();

            deathTimerCoroutine = StartCoroutine(StartDeathTimer(deathTimerSeconds));

            if (videoCountdownCoroutine != null)
            {
                StopCoroutine(videoCountdownCoroutine);
                videoCountdownCoroutine = null;
            }
        }
    }

    private void SetAnimatorsUpdateMode(AnimatorUpdateMode mode)
    {
        Animator[] animators = FindObjectsOfType<Animator>();
        foreach (Animator animator in animators)
        {
            animator.updateMode = mode;
        }
    }

    private IEnumerator StartDeathTimer(int seconds)
    {
        timerText.gameObject.SetActive(true);
        float timeRemaining = seconds;

        while (timeRemaining > 0)
        {
            timerText.text = Mathf.CeilToInt(timeRemaining).ToString();
            yield return new WaitForSecondsRealtime(1f);
            timeRemaining -= 1f;

            if (lives > 0)
            {
                PrepareForResume();
                yield break;
            }
        }

        RestartScene();
    }

    private void PrepareForResume()
    {
        isGameOver = false;
        timerText.gameObject.SetActive(false);
        awaitingSpaceToResume = true;
        if (deathTimerCoroutine != null)
        {
            StopCoroutine(deathTimerCoroutine);
            deathTimerCoroutine = null;
        }

        gameOver.SetActive(false);
        playButton.SetActive(false);
        continueText.SetActive(false);
        logo.SetActive(false);
        company.SetActive(false);
        gameOverScoreText.gameObject.SetActive(false);
        GameOverMusic.SetActive(false);
        PlayMusic.SetActive(true);

        player.ResetPosition();
        Pause();
    }

    private void UpdateHighScore()
    {
        int highScore = PlayerPrefs.GetInt(HighScoreKey, 0);
        if (score > highScore)
        {
            PlayerPrefs.SetInt(HighScoreKey, score);
            PlayerPrefs.Save();
        }
    }

    private void DisplayHighScore()
    {
        int highScore = PlayerPrefs.GetInt(HighScoreKey, 0);
        highScoreText.text = $"Рекорд: {highScore}";
    }

    private void DisplayGameOverScore()
    {
        int highScore = PlayerPrefs.GetInt(HighScoreKey, 0);
        gameOverScoreText.text = $"Рекорд: {highScore}\n\nВаш результат: {score}";
        gameOverScoreText.gameObject.SetActive(true);
    }

    public void IncreaseScore()
    {
        score++;
        scoreText.text = score.ToString();
    }

    public void AddLife()
    {
        lives += livesPerToken;
        UpdateLivesText();
        Debug.Log("Добавлено жизней: " + livesPerToken + ". Текущее количество жизней: " + lives);

        if (isGameOver && deathTimerCoroutine != null)
        {
            PrepareForResume();
        }

        if (videoCountdownCoroutine != null)
        {
            StopCoroutine(videoCountdownCoroutine);
            videoCountdownCoroutine = null;
            Debug.Log("Корутина запуска видео-сцены остановлена, так как у игрока есть жизни.");
        }
    }

    private void UpdateLivesText()
    {
        if (livesText != null)
        {
            livesText.text = "Жизни: " + lives.ToString();
        }
    }

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

    public void SetLivesPerToken(int amount)
    {
        livesPerToken = amount;
        Debug.Log("Установлено количество жизней за жетон: " + livesPerToken);
    }

    private IEnumerator GrantInvincibility(float duration)
    {
        isInvincible = true;
        player.SetInvincibility(true);

        yield return new WaitForSeconds(duration);

        isInvincible = false;
        player.SetInvincibility(false);
    }

    private void ShowLifeLostText()
    {
        GameObject lifeLostText = Instantiate(lifeLostTextPrefab, GameObject.Find("Canvas").transform, false);
        StartCoroutine(AnimateLifeLostText(lifeLostText.GetComponent<Text>()));
    }

    private IEnumerator AnimateLifeLostText(Text text)
    {
        Vector3 startPos = text.transform.position;
        Vector3 endPos = startPos + new Vector3(0, 50, 0);
        Color originalColor = text.color;

        float duration = 1f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;

            text.transform.position = Vector3.Lerp(startPos, endPos, t);
            text.color = new Color(originalColor.r, originalColor.g, originalColor.b, 1 - t);

            yield return null;
        }

        Destroy(text.gameObject);
    }
}
