using UnityEngine;
using UnityEngine.UI;

#if UNITY_ANDROID || UNITY_EDITOR

[DisallowMultipleComponent]
public sealed class StartUIButtonCreditLauncher : MonoBehaviour
{
    [Header("UI Button")]
    [Tooltip("Кнопка START. Если поле пустое, скрипт попробует найти Button на этом объекте или в дочерних объектах.")]
    [SerializeField] private Button targetButton;

    [Header("Логика запуска")]
    [Tooltip("Если включено, при нажатии кнопки будет добавляться жизнь перед запуском игры.")]
    [SerializeField] private bool addLifeBeforeStart = true;

    [Tooltip("Если включено, жизнь добавится только когда жизней 0 или меньше. Это защищает от лишнего накручивания жизней.")]
    [SerializeField] private bool addLifeOnlyIfNoLives = true;

    [Tooltip("Если включено, после добавления жизни игра сразу запустится.")]
    [SerializeField] private bool startGameImmediately = true;

    [Header("Отладка")]
    [SerializeField] private bool debugLogs = true;

    private void Awake()
    {
        ResolveButtonReference();
    }

    private void OnEnable()
    {
        ResolveButtonReference();

        if (targetButton == null)
        {
            Debug.LogError("[StartUIButtonCreditLauncher] Button не найден.");
            return;
        }

        targetButton.onClick.RemoveListener(HandleStartButtonClicked);
        targetButton.onClick.AddListener(HandleStartButtonClicked);
        targetButton.interactable = true;
    }

    private void OnDisable()
    {
        if (targetButton != null)
        {
            targetButton.onClick.RemoveListener(HandleStartButtonClicked);
        }
    }

    private void ResolveButtonReference()
    {
        if (targetButton != null)
        {
            return;
        }

        targetButton = GetComponent<Button>();

        if (targetButton != null)
        {
            return;
        }

        targetButton = GetComponentInChildren<Button>(true);
    }

    private void HandleStartButtonClicked()
    {
        GameManager gameManager = GameManager.Instance;

        if (gameManager == null)
        {
            Debug.LogError("[StartUIButtonCreditLauncher] GameManager.Instance не найден. Кнопка START не может запустить игру.");
            return;
        }

        if (debugLogs)
        {
            Debug.Log($"[StartUIButtonCreditLauncher] Нажата кнопка START. Жизни до нажатия: {gameManager.lives}");
        }

        if (addLifeBeforeStart)
        {
            bool shouldAddLife = !addLifeOnlyIfNoLives || gameManager.lives <= 0;

            if (shouldAddLife)
            {
                gameManager.AddLife();

                if (debugLogs)
                {
                    Debug.Log($"[StartUIButtonCreditLauncher] Жизнь добавлена. Жизни после добавления: {gameManager.lives}");
                }
            }
        }

        if (startGameImmediately && gameManager.lives > 0)
        {
            if (debugLogs)
            {
                Debug.Log("[StartUIButtonCreditLauncher] Запуск игры через кнопку START.");
            }

            gameManager.Play();
        }
        else if (debugLogs)
        {
            Debug.LogWarning("[StartUIButtonCreditLauncher] Игра не запущена, потому что жизней всё ещё 0.");
        }
    }
}

#else

[DisallowMultipleComponent]
public sealed class StartUIButtonCreditLauncher : MonoBehaviour
{
    [Header("UI Button")]
    [SerializeField] private Button targetButton;

    private void Awake()
    {
        ResolveButtonReference();
        DisableButtonForArcadeBuild();
    }

    private void OnEnable()
    {
        ResolveButtonReference();
        DisableButtonForArcadeBuild();
    }

    private void ResolveButtonReference()
    {
        if (targetButton != null)
        {
            return;
        }

        targetButton = GetComponent<Button>();

        if (targetButton != null)
        {
            return;
        }

        targetButton = GetComponentInChildren<Button>(true);
    }

    private void DisableButtonForArcadeBuild()
    {
        if (targetButton != null)
        {
            targetButton.interactable = false;
        }

        Debug.Log("[StartUIButtonCreditLauncher] Кнопка START отключена в этой сборке. Бесплатный запуск разрешён только на Android и в Editor.");
    }
}

#endif