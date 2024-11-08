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
    [SerializeField] private Text livesText;
    [SerializeField] private GameObject playButton;
    [SerializeField] private GameObject gameOver;
    [SerializeField] private GameObject logo;
    [SerializeField] private GameObject company;
    public AudioClip dieSound;

    public int score { get; private set; } = 0;
    public int lives = 3;
    private bool isGameOver = false;
    private bool isPausedAfterCollision = false; // ���� ��� ����� ����� ������������
    private AudioSource audioSource;

    [Header("Token Settings")]
    public int livesPerToken = 1; // ���������� ������, ����������� �� ���� �����

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

    private void Start()
    {
        Pause();
        UpdateLivesText();

        audioSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();

        Animator[] animators = FindObjectsOfType<Animator>();
        foreach (Animator animator in animators)
        {
            animator.updateMode = AnimatorUpdateMode.UnscaledTime;
        }

        portNo = new SerialPort(comPort, 19200);
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
        if (playButton.activeSelf && Input.GetKeyDown(KeyCode.Space) && !isGameOver && lives > 0)
        {
            Play();
        }

        if (isGameOver && Input.GetKeyDown(KeyCode.Space))
        {
            RestartScene();
        }

        // �������� ������� Space ��� ����������� ���� ����� ����� ��� ������������
        if (isPausedAfterCollision && Input.GetKeyDown(KeyCode.Space))
        {
            isPausedAfterCollision = false;
            Play();
        }

        if (portIsOpen)
        {
            try
            {
                if (portNo.BytesToRead > 0)
                {
                    int portValue = portNo.ReadByte();
                    Debug.Log("������� ����: " + portValue);

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
        //score = 0;
        //scoreText.text = score.ToString();

        playButton.SetActive(false);
        gameOver.SetActive(false);
        logo.SetActive(false);
        company.SetActive(false);

        Time.timeScale = 1f;
        player.enabled = true;
    }

    public void GameOver()
    {
        lives--;
        UpdateLivesText();

        if (lives <= 0)
        {
            gameOver.SetActive(true);
            playButton.SetActive(true);
            Pause();
            isGameOver = true;

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
            isPausedAfterCollision = true;
            player.ResetPosition();
            Pause();  // ������ ����� ����� ������ �������, ���� ����� ��� ��������
        }
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
        Debug.Log("��������� ������: " + livesPerToken + ". ������� ���������� ������: " + lives);
    }

    private void UpdateLivesText()
    {
        if (livesText != null)
        {
            livesText.text = "�����: " + lives.ToString();
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
            Debug.Log("Serial Port ������.");
        }
    }

    public void SetLivesPerToken(int amount)
    {
        livesPerToken = amount;
        Debug.Log("����������� ���������� ������ �� �����: " + livesPerToken);
    }
}
