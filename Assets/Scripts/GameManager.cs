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
    [SerializeField] private Text livesText; // UI ��� ����������� ���������� ������
    [SerializeField] private GameObject playButton;
    [SerializeField] private GameObject gameOver;
    [SerializeField] private GameObject logo;
    [SerializeField] private GameObject company;
    public AudioClip dieSound; // ���� ��� ����� ������

    public int score { get; private set; } = 0;
    public int lives { get; private set; } = 3; // ��������� ���������� ������
    private bool isGameOver = false; // ���� ��� �������� ��������� game over
    private AudioSource audioSource; // ��������� AudioSource

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
        UpdateLivesText(); // ��������� ����������� ���������� ������

        // ������������� ���������� AudioSource
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
    }

    private void Update()
    {
        // ������ ���� �� ������� �������, ���� ������ play ������� � �� � ��������� game over
        if (playButton.activeSelf && Input.GetKeyDown(KeyCode.Space) && !isGameOver)
        {
            Play();
        }

        // ���������� ����� �� ������� ������� ����� game over
        if (isGameOver && Input.GetKeyDown(KeyCode.Space))
        {
            RestartScene();
        }

        // ���������� ������ �� ������� ������� P
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
        isGameOver = false; // ���������� ���� game over

        Pipes[] pipes = FindObjectsOfType<Pipes>();

        for (int i = 0; i < pipes.Length; i++)
        {
            Destroy(pipes[i].gameObject);
        }
    }

    public void GameOver()
    {
        // ���������� ������ ��� ��������� ����
        lives--;
        UpdateLivesText();

        if (lives <= 0)
        {
            // �������� ����� game over � ���������� ����
            gameOver.SetActive(true);
            playButton.SetActive(true); // ���������� playButton ��� ���������� �������
            Pause();
            isGameOver = true; // ������������� ���� game over

            // ����������� ���� ������, ���� dieSound �����
            if (dieSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(dieSound);
            }
        }
        else
        {
            // ���� ��� ���� �����, ������ ���������� ������� ������
            player.ResetPosition();
        }
    }

    public void IncreaseScore()
    {
        score++;
        scoreText.text = score.ToString();
    }

    // ����� ��� ���������� ������
    public void AddLife()
    {
        lives++;
        UpdateLivesText();
    }

    // ����� ��� ���������� UI, ������������� ���������� ������
    private void UpdateLivesText()
    {
        if (livesText != null)
        {
            livesText.text = "Lives: " + lives.ToString();
        }
    }

    // ����� ��� ����������� �����
    private void RestartScene()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
