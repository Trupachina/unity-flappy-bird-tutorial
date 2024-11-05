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
    [SerializeField] private Text livesText; // UI ��� ����������� ���������� ������
    [SerializeField] private GameObject playButton;
    [SerializeField] private GameObject gameOver;
    [SerializeField] private GameObject logo;
    [SerializeField] private GameObject company;
    public AudioClip dieSound; // ���� ��� ����� ������

    public int score { get; private set; } = 0;
    public int lives = 3; // ��������� ���������� ������
    private bool isGameOver = false; // ���� ��� �������� ��������� game over
    private AudioSource audioSource; // ��������� AudioSource

    [Header("Serial Port Settings")]
    public string comPort = "COM3"; // ������� ����� COM-����
    private SerialPort portNo; // ���� ��� ����������� SerialPort
    private bool portIsOpen = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        //DontDestroyOnLoad(gameObject); // �������� GameManager ��� �������� ����� �������, ���� ���������
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

        // ��������� ���� �������� �� UnscaledTime
        Animator[] animators = FindObjectsOfType<Animator>();
        foreach (Animator animator in animators)
        {
            animator.updateMode = AnimatorUpdateMode.UnscaledTime;
        }

        // ������������� �����
        portNo = new SerialPort(comPort, 19200); // ������� ������ COM-����
        try
        {
            portNo.Open();
            portNo.ReadTimeout = 1000;
            portIsOpen = true;
            Debug.Log("Serial Port ������.");
        }
        catch (System.Exception ex)
        {
            Debug.LogError("������ ��� �������� �����: " + ex.Message);
        }
    }

    private void Update()
    {
        // ������ ���� �� ������� �������, ���� ������ play ������� � �� � ��������� game over
        if (playButton.activeSelf && Input.GetKeyDown(KeyCode.Space) && !isGameOver && lives > 0)
        {
            Play();
        }

        // ���������� ����� �� ������� ������� ����� game over
        if (isGameOver && Input.GetKeyDown(KeyCode.Space))
        {
            RestartScene();
        }

        // �������� ����� �� ��������� ������
        if (portIsOpen)
        {
            try
            {
                if (portNo.BytesToRead > 0)
                {
                    int portValue = portNo.ReadByte();
                    Debug.Log("������� ����: " + portValue);

                    // ���� ������� ������ ��� ���������� �����
                    if (portValue == 1)
                    {
                        AddLife();
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError("������ ��� ������ �����: " + ex.Message);
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

            if (portNo != null && portNo.IsOpen)
            {
                portNo.Close();
                Debug.Log("Serial Port ������.");
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
    private void AddLife()
    {
        lives++;
        UpdateLivesText();
        Debug.Log("��������� �����. ������� ���������� ������: " + lives);
    }

    // ����� ��� ���������� UI, ������������� ���������� ������
    private void UpdateLivesText()
    {
        if (livesText != null)
        {
            livesText.text = "�����: " + lives.ToString();
        }
    }

    // ����� ��� ����������� �����
    private void RestartScene()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private void OnApplicationQuit()
    {
        if (portNo != null && portNo.IsOpen)
        {
            portNo.Close();
            Debug.Log("Serial Port ������.");
        }
    }
}
