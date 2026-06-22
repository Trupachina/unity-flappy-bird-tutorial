using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using UnityEngine.Video;
using System;

[DefaultExecutionOrder(-1)]
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Основные ссылки")]
    [SerializeField] private Player player;
    [SerializeField] private Spawner spawner;
    [SerializeField] private Text scoreText;
    [SerializeField] private Text livesText;
    [SerializeField] private GameObject playButton;
    [SerializeField] private GameObject gameOver;
    [SerializeField] private Text gameOverScoreText;
    [SerializeField] private GameObject logo;
    [SerializeField] private GameObject company;
    [SerializeField] private GameObject continueText;
    [SerializeField] private Text timerText;

    [Header("Камера")]
    [Tooltip("Компонент плавного смещения камеры между стартовым экраном и игровым режимом.")]
    [SerializeField] private GameplayCameraShift gameplayCameraShift;

    [Header("Звуки")]
    public AudioClip dieSound;
    [SerializeField] private GameObject lifeLostTextPrefab;

    [Header("Музыкальные объекты")]
    [SerializeField] private GameObject StartMusic;
    [SerializeField] private GameObject PlayMusic;
    [SerializeField] private GameObject GameOverMusic;

    [Header("Музыка главного экрана")]
    [Tooltip("Если включено, StartMusic работает циклом: играет, затем пауза, затем снова играет.")]
    [SerializeField] private bool useIntermittentStartMusic = true;

    [Tooltip("Сколько секунд музыка главного экрана играет при каждом включении.")]
    [SerializeField] private float startMusicPlaySeconds = 20f;

    [Tooltip("Сколько секунд длится тишина между включениями музыки главного экрана.")]
    [SerializeField] private float startMusicSilentSeconds = 180f;

    [Header("Ручной перезапуск после Game Over")]
    [Tooltip("Через сколько секунд после Game Over разрешить ручной перезапуск прыжком.")]
    [SerializeField] private float gameOverManualRestartDelay = 10f;

    [Header("Демонстрационное видео главного экрана")]
    [SerializeField] private GameObject videoContainer;
    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField] private RawImage videoDisplay;

    [Tooltip("Задержка перед первым запуском демонстрационного видео на главном экране.")]
    [SerializeField] private float videoDelay = 30f;

    [Tooltip("Если включено, видео работает циклом: 20 секунд видео, затем 120 секунд без видео.")]
    [SerializeField] private bool useIntermittentDemoVideo = true;

    [Tooltip("Сколько секунд демонстрационное видео отображается на главном экране.")]
    [SerializeField] private float demoVideoPlaySeconds = 20f;

    [Tooltip("Сколько секунд длится пауза без видео между повторами.")]
    [SerializeField] private float demoVideoSilentSeconds = 120f;

    [Tooltip("Длительность плавного появления/исчезновения видео.")]
    [SerializeField] private float videoFadeDuration = 1.0f;

    [SerializeField] private AudioSource videoAudioSource;

    private bool isRestarting = false;

    private float[] originalVolumes;
    private AudioSource[] allAudioSources;

    private Coroutine videoCountdownCoroutine;
    private Coroutine videoFadeCoroutine;
    private Coroutine startMusicCoroutine;

    private bool isVideoPlaying = false;

    public int score { get; private set; } = 0;
    public int lives = 3;
    public int deathTimerSeconds = 30;

    private bool isGameOver = false;
    private bool isPausedAfterCollision = false;
    private bool isInvincible = false;

    private AudioSource audioSource;
    private Coroutine deathTimerCoroutine;

    private bool awaitingSpaceToResume = false;
    private bool canManualRestartDuringGameOver = false;

    private int currentHighScore;
    private const string HighScoreKey = "HighScore";

    [Header("Token Settings")]
    public int livesPerToken = 1;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (SerialPortManager.Instance != null)
        {
            SerialPortManager.Instance.OnCoinsReceived += HandleCoinsReceived;
        }
    }

    private void OnDestroy()
    {
        StopVideoCompletely();
        StopStartScreenMusicLoop(true);

        if (SerialPortManager.Instance != null)
        {
            SerialPortManager.Instance.OnCoinsReceived -= HandleCoinsReceived;
        }
    }

    private void HandleCoinsReceived(int coins)
    {
        for (int i = 0; i < coins; i++)
        {
            AddLife();
        }
    }

    public void Start()
    {
        Pause();

        if (gameplayCameraShift != null)
        {
            gameplayCameraShift.ResetToStartPosition();
        }

        UpdateLivesText();

        audioSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();

        StartStartScreenMusicLoop();

        if (scoreText != null)
        {
            scoreText.gameObject.SetActive(true);
        }

        currentHighScore = PlayerPrefs.GetInt(HighScoreKey, 0);
        DisplayHighScore();

        SetAnimatorsUpdateMode(AnimatorUpdateMode.UnscaledTime);

        InitializeVideoSystem();

        if (PlayerPrefs.HasKey("ExtraLife"))
        {
            AddLife();
            PlayerPrefs.DeleteKey("ExtraLife");
        }

        if (lives <= 0 && !isGameOver && playButton != null && playButton.activeSelf)
        {
            StartVideoCountdown();
        }
    }

    public void Pause()
    {
        Time.timeScale = 0f;

        if (player != null)
        {
            player.enabled = false;
        }
    }

    public void Play()
    {
        StopStartScreenMusicLoop(true);
        StopVideoCompletely();

        if (playButton != null) playButton.SetActive(false);
        if (gameOver != null) gameOver.SetActive(false);
        if (continueText != null) continueText.SetActive(false);
        if (logo != null) logo.SetActive(false);
        if (company != null) company.SetActive(false);

        if (GameOverMusic != null) GameOverMusic.SetActive(false);
        if (StartMusic != null) StartMusic.SetActive(false);
        if (PlayMusic != null) PlayMusic.SetActive(true);

        if (scoreText != null)
        {
            scoreText.gameObject.SetActive(true);
        }

        UpdateScoreDisplay();
        SetAnimatorsUpdateMode(AnimatorUpdateMode.Normal);

        if (gameplayCameraShift != null)
        {
            gameplayCameraShift.MoveToGameplayPosition();
        }

        Time.timeScale = 1f;

        if (player != null)
        {
            player.enabled = true;
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

            if (player != null)
            {
                player.ResetPosition();
            }

            Pause();
        }
        else
        {
            StopStartScreenMusicLoop(true);
            StopVideoCompletely();

            if (gameOver != null) gameOver.SetActive(true);
            if (continueText != null) continueText.SetActive(true);

            if (GameOverMusic != null) GameOverMusic.SetActive(true);
            if (PlayMusic != null) PlayMusic.SetActive(false);
            if (StartMusic != null) StartMusic.SetActive(false);

            if (scoreText != null)
            {
                scoreText.gameObject.SetActive(false);
            }

            Pause();

            isGameOver = true;
            canManualRestartDuringGameOver = false;

            SetAnimatorsUpdateMode(AnimatorUpdateMode.UnscaledTime);

            if (dieSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(dieSound);
            }

            UpdateHighScore();
            DisplayGameOverScore();

            deathTimerCoroutine = StartCoroutine(StartDeathTimer(deathTimerSeconds));
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
        if (timerText != null)
        {
            timerText.gameObject.SetActive(true);
        }

        float timeRemaining = seconds;
        float elapsedAfterGameOver = 0f;

        canManualRestartDuringGameOver = false;

        while (timeRemaining > 0)
        {
            if (timerText != null)
            {
                timerText.text = Mathf.CeilToInt(timeRemaining).ToString();
            }

            yield return new WaitForSecondsRealtime(1f);

            elapsedAfterGameOver += 1f;
            timeRemaining -= 1f;

            if (!canManualRestartDuringGameOver && elapsedAfterGameOver >= gameOverManualRestartDelay)
            {
                canManualRestartDuringGameOver = true;
                Debug.Log("GameOver: теперь можно нажать прыжок для ручного перезапуска.");
            }

            if (lives > 0)
            {
                PrepareForResume();
                yield break;
            }

            if (isRestarting)
            {
                yield break;
            }
        }

        StartCoroutine(SafeRestartScene());
    }

    private IEnumerator SafeRestartScene()
    {
        yield return null;
        yield return StartCoroutine(RestartSceneCoroutine());
    }

    private void RestartFromGameOverByJump()
    {
        if (!isGameOver || isRestarting)
        {
            return;
        }

        canManualRestartDuringGameOver = false;

        if (deathTimerCoroutine != null)
        {
            StopCoroutine(deathTimerCoroutine);
            deathTimerCoroutine = null;
        }

        if (timerText != null)
        {
            timerText.gameObject.SetActive(false);
        }

        StartCoroutine(SafeRestartScene());
    }

    private void PrepareForResume()
    {
        isGameOver = false;
        canManualRestartDuringGameOver = false;

        if (timerText != null)
        {
            timerText.gameObject.SetActive(false);
        }

        awaitingSpaceToResume = true;

        if (deathTimerCoroutine != null)
        {
            StopCoroutine(deathTimerCoroutine);
            deathTimerCoroutine = null;
        }

        if (gameOver != null) gameOver.SetActive(false);
        if (playButton != null) playButton.SetActive(false);
        if (continueText != null) continueText.SetActive(false);
        if (logo != null) logo.SetActive(false);
        if (company != null) company.SetActive(false);

        if (gameOverScoreText != null)
        {
            gameOverScoreText.gameObject.SetActive(false);
        }

        if (GameOverMusic != null) GameOverMusic.SetActive(false);
        if (StartMusic != null) StartMusic.SetActive(false);
        if (PlayMusic != null) PlayMusic.SetActive(true);

        if (scoreText != null)
        {
            scoreText.gameObject.SetActive(true);
        }

        UpdateScoreDisplay();

        if (player != null)
        {
            player.ResetPosition();
        }

        Pause();
    }

    public void ResetHighScore()
    {
        currentHighScore = 0;
        PlayerPrefs.DeleteKey(HighScoreKey);
        PlayerPrefs.Save();

        if (playButton != null && playButton.activeSelf)
        {
            DisplayHighScore();
        }
        else if (!isGameOver)
        {
            UpdateScoreDisplay();
        }
        else
        {
            DisplayGameOverScore();
        }
    }

    private void UpdateHighScore()
    {
        if (score > currentHighScore)
        {
            currentHighScore = score;
            PlayerPrefs.SetInt(HighScoreKey, currentHighScore);
            PlayerPrefs.Save();
        }
    }

    public void DisplayHighScore()
    {
        currentHighScore = PlayerPrefs.GetInt(HighScoreKey, 0);

        if (scoreText != null)
        {
            scoreText.text = $"Рекорд: {currentHighScore}";
        }
    }

    private void UpdateScoreDisplay()
    {
        if (scoreText != null)
        {
            scoreText.text = $"{score} ({currentHighScore})";
        }
    }

    private void DisplayGameOverScore()
    {
        if (gameOverScoreText == null)
        {
            return;
        }

        gameOverScoreText.text = $"Рекорд: {currentHighScore}\n\nВаш результат: {score}";
        gameOverScoreText.gameObject.SetActive(true);
    }

    public void IncreaseScore()
    {
        score++;

        if (score > currentHighScore)
        {
            currentHighScore = score;
            PlayerPrefs.SetInt(HighScoreKey, currentHighScore);
            PlayerPrefs.Save();
        }

        UpdateScoreDisplay();
    }

    public int GetDifficultyStage()
    {
        if (score < 20)
        {
            return 0;
        }

        if (score < 40)
        {
            return 1;
        }

        if (score < 60)
        {
            return 2;
        }

        if (score < 80)
        {
            return 3;
        }

        return 4;
    }

    public void AddLife()
    {
        lives += livesPerToken;
        UpdateLivesText();

        Debug.Log($"Добавлено жизней: {livesPerToken}. Текущее количество: {lives}");

        // При добавлении жизни демонстрационное видео должно полностью остановиться,
        // потому что игрок уже может начать игру или продолжить после Game Over.
        StopVideoCompletely();

        if (isGameOver && deathTimerCoroutine != null)
        {
            PrepareForResume();
        }
    }

    private void UpdateLivesText()
    {
        if (livesText != null)
        {
            livesText.text = "Жизни: " + lives.ToString();
        }
    }

    private IEnumerator RestartSceneCoroutine()
    {
        if (isRestarting)
        {
            yield break;
        }

        isRestarting = true;

        Debug.Log("Начало перезагрузки сцены");

        StopStartScreenMusicLoop(true);
        StopVideoCompletely();
        ReleaseVideoResources();

        score = 0;
        lives = 3;
        isGameOver = false;
        isPausedAfterCollision = false;
        isInvincible = false;
        awaitingSpaceToResume = false;
        canManualRestartDuringGameOver = false;

        GC.Collect();
        GC.WaitForPendingFinalizers();
        Resources.UnloadUnusedAssets();

        SceneManager.LoadScene(SceneManager.GetActiveScene().name);

        isRestarting = false;
    }

    private void ReleaseVideoResources()
    {
        if (videoPlayer == null)
        {
            return;
        }

        if (videoPlayer.isPlaying)
        {
            videoPlayer.Stop();
        }

        if (videoPlayer.targetTexture != null)
        {
            videoPlayer.targetTexture.Release();
            RenderTexture.Destroy(videoPlayer.targetTexture);
            videoPlayer.targetTexture = null;
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

        if (player != null)
        {
            player.SetInvincibility(true);
        }

        yield return new WaitForSeconds(duration);

        isInvincible = false;

        if (player != null)
        {
            player.SetInvincibility(false);
        }
    }

    private void ShowLifeLostText()
    {
        if (lifeLostTextPrefab == null)
        {
            return;
        }

        GameObject canvas = GameObject.Find("Canvas");

        if (canvas == null)
        {
            return;
        }

        GameObject lifeLostText = Instantiate(
            lifeLostTextPrefab,
            canvas.transform,
            false
        );

        Text textComponent = lifeLostText.GetComponent<Text>();

        if (textComponent != null)
        {
            StartCoroutine(AnimateLifeLostText(textComponent));
        }
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
            text.color = new Color(
                originalColor.r,
                originalColor.g,
                originalColor.b,
                1 - t
            );

            yield return null;
        }

        Destroy(text.gameObject);
    }

    // ===================== ДЕМОНСТРАЦИОННОЕ ВИДЕО =====================

    private void InitializeVideoSystem()
    {
        if (videoPlayer == null || videoDisplay == null)
        {
            return;
        }

        if (videoContainer != null)
        {
            videoContainer.SetActive(false);
        }

        RenderTexture renderTexture = new RenderTexture(
            Mathf.Max(1, (int)videoDisplay.rectTransform.rect.width),
            Mathf.Max(1, (int)videoDisplay.rectTransform.rect.height),
            0
        );

        videoPlayer.targetTexture = renderTexture;
        videoDisplay.texture = renderTexture;
        videoDisplay.color = new Color(1f, 1f, 1f, 0f);

        videoAudioSource = videoPlayer.GetComponent<AudioSource>();

        if (videoAudioSource == null)
        {
            Debug.LogWarning("У видео нет компонента AudioSource!");
        }
    }

    private void StartVideoCountdown()
    {
        if (!IsDemoVideoAllowed())
        {
            return;
        }

        if (videoCountdownCoroutine != null)
        {
            StopCoroutine(videoCountdownCoroutine);
            videoCountdownCoroutine = null;
        }

        Debug.Log("Начинаем отсчёт до показа демонстрационного видео.");
        videoCountdownCoroutine = StartCoroutine(VideoCountdownRoutine());
    }

    private IEnumerator VideoCountdownRoutine()
    {
        float firstDelay = Mathf.Max(0f, videoDelay);

        if (firstDelay > 0f)
        {
            Debug.Log($"Первый запуск видео через {firstDelay:F0} сек.");

            float countdown = firstDelay;
            int lastFullSecond = Mathf.CeilToInt(countdown);

            while (countdown > 0f)
            {
                if (!IsDemoVideoAllowed())
                {
                    Debug.Log($"Отмена отсчёта видео. Оставшееся время: {countdown:F1} сек.");
                    videoCountdownCoroutine = null;
                    yield break;
                }

                int currentFullSecond = Mathf.CeilToInt(countdown);

                if (currentFullSecond != lastFullSecond)
                {
                    lastFullSecond = currentFullSecond;
                    Debug.Log($"До показа видео: {currentFullSecond} сек.");
                }

                countdown -= Time.unscaledDeltaTime;
                yield return null;
            }
        }

        if (!useIntermittentDemoVideo)
        {
            if (IsDemoVideoAllowed())
            {
                PlayVideo();
            }

            videoCountdownCoroutine = null;
            yield break;
        }

        while (IsDemoVideoAllowed())
        {
            PlayVideo();

            float playDuration = Mathf.Max(0f, demoVideoPlaySeconds);
            float playTimer = 0f;

            while (playTimer < playDuration)
            {
                if (!IsDemoVideoAllowed())
                {
                    StopVideoForCycle();
                    videoCountdownCoroutine = null;
                    yield break;
                }

                if (!isVideoPlaying)
                {
                    break;
                }

                playTimer += Time.unscaledDeltaTime;
                yield return null;
            }

            if (isVideoPlaying)
            {
                StopVideoForCycle();
            }

            float silentDuration = Mathf.Max(0f, demoVideoSilentSeconds);
            float silentTimer = 0f;

            Debug.Log($"Пауза без видео: {silentDuration:F0} сек.");

            while (silentTimer < silentDuration)
            {
                if (!IsDemoVideoAllowed())
                {
                    videoCountdownCoroutine = null;
                    yield break;
                }

                silentTimer += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        videoCountdownCoroutine = null;
    }

    private bool IsDemoVideoAllowed()
    {
        return videoPlayer != null &&
               videoContainer != null &&
               playButton != null &&
               playButton.activeSelf &&
               lives <= 0 &&
               !isGameOver &&
               !isRestarting;
    }

    private void PlayVideo()
    {
        if (videoPlayer == null || videoContainer == null)
        {
            return;
        }

        MuteAllSoundsExceptVideo();

        videoContainer.SetActive(true);

        if (videoDisplay != null)
        {
            videoDisplay.color = new Color(1f, 1f, 1f, 0f);
        }

        videoPlayer.Stop();
        videoPlayer.time = 0f;
        videoPlayer.isLooping = true;
        videoPlayer.Play();

        isVideoPlaying = true;

        float volume = SoundManager.Instance != null
            ? SoundManager.Instance.GetCurrentVolume()
            : 1f;

        videoPlayer.SetDirectAudioVolume(0, volume);

        if (videoFadeCoroutine != null)
        {
            StopCoroutine(videoFadeCoroutine);
            videoFadeCoroutine = null;
        }

        videoFadeCoroutine = StartCoroutine(FadeVideo(0f, 1f));
    }

    private void StopVideo()
    {
        StopVideoInternal(true);
    }

    private void StopVideoForCycle()
    {
        StopVideoInternal(false);
    }

    private void StopVideoCompletely()
    {
        StopVideoInternal(true);
    }

    private void StopVideoInternal(bool stopVideoRoutine)
    {
        if (stopVideoRoutine && videoCountdownCoroutine != null)
        {
            StopCoroutine(videoCountdownCoroutine);
            videoCountdownCoroutine = null;
        }

        if (!isVideoPlaying)
        {
            return;
        }

        UnmuteAllSounds();

        if (videoPlayer != null && videoPlayer.isPlaying)
        {
            videoPlayer.Stop();
        }

        if (videoFadeCoroutine != null)
        {
            StopCoroutine(videoFadeCoroutine);
            videoFadeCoroutine = null;
        }

        if (videoContainer != null)
        {
            if (videoDisplay != null && videoContainer.activeSelf)
            {
                videoFadeCoroutine = StartCoroutine(
                    FadeVideo(videoDisplay.color.a, 0f, () =>
                    {
                        if (videoContainer != null)
                        {
                            videoContainer.SetActive(false);
                        }
                    })
                );
            }
            else
            {
                videoContainer.SetActive(false);
            }
        }

        isVideoPlaying = false;
    }

    private void MuteAllSoundsExceptVideo()
    {
        StopStartScreenMusicLoop(true);

        allAudioSources = FindObjectsOfType<AudioSource>();
        originalVolumes = new float[allAudioSources.Length];

        for (int i = 0; i < allAudioSources.Length; i++)
        {
            AudioSource source = allAudioSources[i];

            if (source == null)
            {
                continue;
            }

            originalVolumes[i] = source.volume;

            if (source == videoAudioSource)
            {
                continue;
            }

            source.volume = 0f;
        }

        if (videoAudioSource != null)
        {
            float volume = SoundManager.Instance != null
                ? SoundManager.Instance.GetCurrentVolume()
                : 1f;

            videoAudioSource.volume = volume;
        }

        if (StartMusic != null) StartMusic.SetActive(false);
        if (PlayMusic != null) PlayMusic.SetActive(false);
        if (GameOverMusic != null) GameOverMusic.SetActive(false);
    }

    private void UnmuteAllSounds()
    {
        if (allAudioSources != null && originalVolumes != null)
        {
            int count = Mathf.Min(allAudioSources.Length, originalVolumes.Length);

            for (int i = 0; i < count; i++)
            {
                AudioSource source = allAudioSources[i];

                if (source == null || source == videoAudioSource)
                {
                    continue;
                }

                source.volume = originalVolumes[i];
            }
        }

        if (isGameOver)
        {
            if (GameOverMusic != null) GameOverMusic.SetActive(true);
        }
        else if (playButton != null && playButton.activeSelf)
        {
            StartStartScreenMusicLoop();
        }
        else
        {
            if (PlayMusic != null) PlayMusic.SetActive(true);
        }
    }

    private IEnumerator FadeVideo(float startAlpha, float targetAlpha, Action onComplete = null)
    {
        if (videoDisplay == null)
        {
            onComplete?.Invoke();
            yield break;
        }

        float elapsed = 0f;
        Color color = videoDisplay.color;

        while (elapsed < videoFadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            color.a = Mathf.Lerp(startAlpha, targetAlpha, elapsed / videoFadeDuration);
            videoDisplay.color = color;

            yield return null;
        }

        color.a = targetAlpha;
        videoDisplay.color = color;

        onComplete?.Invoke();
    }

    private void Update()
    {
        if (isVideoPlaying && IsJumpPressed())
        {
            StopVideoForCycle();
            return;
        }

        if (isGameOver && canManualRestartDuringGameOver && IsJumpPressed())
        {
            RestartFromGameOverByJump();
            return;
        }

        if (playButton != null &&
            playButton.activeSelf &&
            Input.GetKeyDown(KeyCode.Space) &&
            !isGameOver &&
            lives > 0)
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

        if (Input.GetKeyDown(KeyCode.F9))
        {
            Debug.Log("Эмуляция монеты по F9");
            AddLife();
        }
    }

    private bool IsJumpPressed()
    {
        return Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0);
    }

    // ===================== МУЗЫКА ГЛАВНОГО ЭКРАНА =====================

    private void StartStartScreenMusicLoop()
    {
        StopStartScreenMusicLoop(true);

        if (StartMusic == null)
        {
            return;
        }

        if (!IsStartScreenMusicAllowed())
        {
            return;
        }

        if (!useIntermittentStartMusic)
        {
            SetStartMusicActive(true, true);
            return;
        }

        startMusicCoroutine = StartCoroutine(StartScreenMusicLoopCoroutine());
    }

    private IEnumerator StartScreenMusicLoopCoroutine()
    {
        while (IsStartScreenMusicAllowed())
        {
            SetStartMusicActive(true, true);

            float playSeconds = Mathf.Max(0f, startMusicPlaySeconds);

            if (playSeconds > 0f)
            {
                yield return new WaitForSecondsRealtime(playSeconds);
            }
            else
            {
                yield return null;
            }

            SetStartMusicActive(false, false);

            float silentSeconds = Mathf.Max(0f, startMusicSilentSeconds);

            if (silentSeconds > 0f)
            {
                yield return new WaitForSecondsRealtime(silentSeconds);
            }
            else
            {
                yield return null;
            }
        }

        startMusicCoroutine = null;
    }

    private void StopStartScreenMusicLoop(bool stopMusicObject)
    {
        if (startMusicCoroutine != null)
        {
            StopCoroutine(startMusicCoroutine);
            startMusicCoroutine = null;
        }

        if (stopMusicObject)
        {
            SetStartMusicActive(false, false);
        }
    }

    private bool IsStartScreenMusicAllowed()
    {
        return playButton != null &&
               playButton.activeSelf &&
               !isGameOver &&
               !isRestarting &&
               !isVideoPlaying;
    }

    private void SetStartMusicActive(bool active, bool restartAudio)
    {
        if (StartMusic == null)
        {
            return;
        }

        if (active)
        {
            if (!StartMusic.activeSelf)
            {
                StartMusic.SetActive(true);
            }

            if (restartAudio)
            {
                AudioSource[] sources = StartMusic.GetComponentsInChildren<AudioSource>(true);

                for (int i = 0; i < sources.Length; i++)
                {
                    AudioSource source = sources[i];

                    if (source == null || source.clip == null)
                    {
                        continue;
                    }

                    source.Stop();
                    source.time = 0f;
                    source.Play();
                }
            }
        }
        else
        {
            AudioSource[] sources = StartMusic.GetComponentsInChildren<AudioSource>(true);

            for (int i = 0; i < sources.Length; i++)
            {
                AudioSource source = sources[i];

                if (source != null)
                {
                    source.Stop();
                }
            }

            if (StartMusic.activeSelf)
            {
                StartMusic.SetActive(false);
            }
        }
    }
}