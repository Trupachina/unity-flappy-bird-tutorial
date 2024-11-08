using UnityEngine;
using UnityEngine.UI;

public class GameSettingsMenu : MonoBehaviour
{
    public GameObject settingsMenu;
    public Button muteButton;
    public Text muteButtonText;
    public Button livesPerTokenButton;
    public Text livesPerTokenButtonText;
    public Button exitButton;

    private int soundLevel = 3;
    private int menuSelection = 0;
    private int livesPerTokenSelection = 0;
    private bool isMenuOpen = false;

    private string[] soundLevelsText = { "Звук выключен", "Минимальная громкость", "Средняя громкость", "Максимальная громкость" };
    private int[] livesPerTokenOptions = { 1, 2, 3, 4, 5 };
    private GameManager gameManager;

    void Start()
    {
        gameManager = GameManager.Instance;

        soundLevel = PlayerPrefs.GetInt("soundLevel", 3);
        livesPerTokenSelection = PlayerPrefs.GetInt("livesPerTokenSelection", 0);

        ApplySoundSettings();
        gameManager.SetLivesPerToken(livesPerTokenOptions[livesPerTokenSelection]);

        settingsMenu.SetActive(false);

        muteButton.onClick.AddListener(ChangeSoundLevel);
        livesPerTokenButton.onClick.AddListener(ChangeLivesPerToken);
        exitButton.onClick.AddListener(ExitMenu);

        UpdateMuteButtonText();
        UpdateLivesPerTokenButtonText();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.M))
        {
            ToggleMenu();
        }

        if (isMenuOpen && Input.GetKeyDown(KeyCode.Return))
        {
            ExecuteMenuAction(menuSelection);
        }

        if (isMenuOpen && Input.GetKeyDown("p"))
        {
            menuSelection = (menuSelection + 1) % 3;
            HighlightSelection();
        }
    }

    void ToggleMenu()
    {
        isMenuOpen = !isMenuOpen;
        settingsMenu.SetActive(isMenuOpen);
        if (isMenuOpen)
        {
            HighlightSelection();
        }
    }

    void ExecuteMenuAction(int selection)
    {
        switch (selection)
        {
            case 0:
                ChangeSoundLevel();
                break;
            case 1:
                ChangeLivesPerToken();
                break;
            case 2:
                ExitMenu();
                break;
        }
    }

    void ChangeSoundLevel()
    {
        soundLevel = (soundLevel + 1) % 4;
        ApplySoundSettings();

        PlayerPrefs.SetInt("soundLevel", soundLevel);
        PlayerPrefs.Save();

        UpdateMuteButtonText();
    }

    void ApplySoundSettings()
    {
        float volume = 0f;
        switch (soundLevel)
        {
            case 0: volume = 0f; break;
            case 1: volume = 0.25f; break;
            case 2: volume = 0.5f; break;
            case 3: volume = 1f; break;
        }

        AudioListener.volume = volume;
    }

    void ChangeLivesPerToken()
    {
        livesPerTokenSelection = (livesPerTokenSelection + 1) % livesPerTokenOptions.Length;
        gameManager.SetLivesPerToken(livesPerTokenOptions[livesPerTokenSelection]);

        PlayerPrefs.SetInt("livesPerTokenSelection", livesPerTokenSelection);
        PlayerPrefs.Save();

        UpdateLivesPerTokenButtonText();
    }

    void UpdateMuteButtonText()
    {
        muteButtonText.text = soundLevelsText[soundLevel];
    }

    void UpdateLivesPerTokenButtonText()
    {
        livesPerTokenButtonText.text = "Жизни за жетон: " + livesPerTokenOptions[livesPerTokenSelection];
    }

    void ExitMenu()
    {
        isMenuOpen = false;
        settingsMenu.SetActive(false);
    }

    void HighlightSelection()
    {
        muteButton.GetComponent<Image>().color = Color.white;
        livesPerTokenButton.GetComponent<Image>().color = Color.white;
        exitButton.GetComponent<Image>().color = Color.white;

        switch (menuSelection)
        {
            case 0:
                muteButton.GetComponent<Image>().color = Color.yellow;
                break;
            case 1:
                livesPerTokenButton.GetComponent<Image>().color = Color.yellow;
                break;
            case 2:
                exitButton.GetComponent<Image>().color = Color.yellow;
                break;
        }
    }
}
