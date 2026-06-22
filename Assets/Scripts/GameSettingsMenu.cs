using UnityEngine;
using UnityEngine.UI;
using System.Diagnostics;
using System;

public class GameSettingsMenu : MonoBehaviour
{
    public GameObject settingsMenu;
    public Button muteButton;
    public Text muteButtonText;
    public Button livesPerTokenButton;
    public Text livesPerTokenButtonText;
    public Button resetHighScoreButton;
    public Text resetHighScoreButtonText;
    public Button restartPCButton;
    public Text restartPCButtonText;
    public Button exitButton;
    public Text exitButtonText;

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
        resetHighScoreButton.onClick.AddListener(ResetHighScore);
        restartPCButton.onClick.AddListener(RestartPC);
        exitButton.onClick.AddListener(ExitMenu);

        UpdateMuteButtonText();
        UpdateLivesPerTokenButtonText();
        resetHighScoreButtonText.text = "Сбросить рекорд";
        restartPCButtonText.text = "Перезагрузить ПК";
        exitButtonText.text = "Выход";
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
            menuSelection = (menuSelection + 1) % 5;
            HighlightSelection();
        }
    }

    void ToggleMenu()
    {
        isMenuOpen = !isMenuOpen;
        settingsMenu.SetActive(isMenuOpen);
        if (isMenuOpen)
        {
            menuSelection = 0;
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
                ResetHighScore();
                break;
            case 3:
                RestartPC();
                break;
            case 4:
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
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.SetVolumeLevel(soundLevel);
        }
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
        Button[] buttons = {
            muteButton,
            livesPerTokenButton,
            resetHighScoreButton,
            restartPCButton,
            exitButton
        };

        foreach (var btn in buttons)
        {
            if (btn != null) btn.GetComponent<Image>().color = Color.white;
        }

        if (menuSelection >= 0 && menuSelection < buttons.Length && buttons[menuSelection] != null)
        {
            buttons[menuSelection].GetComponent<Image>().color = Color.yellow;
        }
    }

    void ResetHighScore()
    {
        PlayerPrefs.DeleteKey("HighScore");
        PlayerPrefs.Save();

        if (gameManager != null)
        {
            // Вызываем метод сброса в GameManager
            gameManager.ResetHighScore();
        }

        resetHighScoreButtonText.text = "Рекорд сброшен!";
        StartCoroutine(ResetButtonTextAfterDelay(1.5f, resetHighScoreButtonText, "Сбросить рекорд"));
    }

    void RestartPC()
    {
        restartPCButtonText.text = "Подтвердите еще раз";
        restartPCButton.onClick.RemoveAllListeners();
        restartPCButton.onClick.AddListener(ConfirmRestartPC);
    }

    void ConfirmRestartPC()
    {
        restartPCButtonText.text = "Перезагрузка...";
        restartPCButton.interactable = false;

        try
        {
            Process.Start("shutdown", "/r /t 0");
        }
        catch (Exception e)
        {
            restartPCButtonText.text = "Ошибка: " + e.Message;
            StartCoroutine(ResetButtonTextAfterDelay(2f, restartPCButtonText, "Перезагрузить ПК"));
            restartPCButton.interactable = true;
            restartPCButton.onClick.RemoveAllListeners();
            restartPCButton.onClick.AddListener(RestartPC);
        }
    }

    System.Collections.IEnumerator ResetButtonTextAfterDelay(float delay, Text targetText, string originalText)
    {
        yield return new WaitForSecondsRealtime(delay);
        if (targetText != null)
            targetText.text = originalText;

        if (targetText == restartPCButtonText)
        {
            restartPCButton.interactable = true;
            restartPCButton.onClick.RemoveAllListeners();
            restartPCButton.onClick.AddListener(RestartPC);
        }
    }
}