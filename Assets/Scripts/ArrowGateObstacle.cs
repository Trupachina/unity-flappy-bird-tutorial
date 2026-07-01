using UnityEngine;

/// <summary>
/// Контроллер готового короткого наклонного препятствия для Arrow-режима.
///
/// Этот скрипт НЕ создаёт gap.
/// Этот скрипт НЕ разводит трубы.
/// Этот скрипт НЕ использует Pipes.cs.
///
/// Gap, верхняя труба, нижняя труба и ScoringTrigger уже должны быть собраны внутри prefab-а:
/// - ArrowGate_Up;
/// - ArrowGate_Down;
/// - ArrowGate_night_Up;
/// - ArrowGate_night_Down.
///
/// Задача скрипта:
/// - двигать готовый prefab влево;
/// - уничтожать его за экраном;
/// - отключать лишний Pipes.cs, если он случайно остался на prefab-е.
/// </summary>
[DisallowMultipleComponent]
public class ArrowGateObstacle : MonoBehaviour
{
    [Header("Movement")]
    [Tooltip("Скорость движения готового Arrow-препятствия влево.")]
    [SerializeField] private float speed = 5f;

    [Tooltip("Дополнительный запас за левой границей экрана перед уничтожением объекта.")]
    [SerializeField] private float destroyPadding = 1.5f;

    [Header("Safety")]
    [Tooltip("Отключать Pipes.cs, если он случайно остался на Arrow-prefab-е.")]
    [SerializeField] private bool disablePipesComponent = true;

    [Tooltip("Отключать старый ArrowCorridorObstacle, если он случайно остался на prefab-е.")]
    [SerializeField] private bool disableOldArrowCorridorObstacle = true;

    [Header("Debug")]
    [Tooltip("Писать диагностику в консоль.")]
    [SerializeField] private bool logDebug = false;

    private float leftEdge = -15f;
    private bool configured;

    private void Awake()
    {
        DisableConflictingComponents();
        CalculateLeftEdge();
    }

    private void Start()
    {
        if (!configured && logDebug)
        {
            Debug.LogWarning(
                $"ArrowGateObstacle '{name}' создан без Configure(). Используется скорость из Inspector: {speed:F2}.",
                this
            );
        }
    }

    private void Update()
    {
        MoveLeft();
        DestroyIfOutsideScreen();
    }

    /// <summary>
    /// Настраивает скорость готового Arrow-препятствия.
    /// Вызывается Spawner-ом сразу после Instantiate().
    /// </summary>
    public void Configure(float movementSpeed, bool debugLogs)
    {
        speed = Mathf.Max(0.1f, movementSpeed);
        logDebug = debugLogs;
        configured = true;

        DisableConflictingComponents();
        CalculateLeftEdge();

        if (logDebug)
        {
            Debug.Log(
                $"ArrowGateObstacle configured: {name}, speed = {speed:F2}, rotation = {transform.eulerAngles}.",
                this
            );
        }
    }

    private void MoveLeft()
    {
        transform.position += speed * Time.deltaTime * Vector3.left;
    }

    private void DestroyIfOutsideScreen()
    {
        if (transform.position.x < leftEdge)
        {
            Destroy(gameObject);
        }
    }

    private void CalculateLeftEdge()
    {
        if (Camera.main != null)
        {
            leftEdge = Camera.main.ScreenToWorldPoint(Vector3.zero).x - destroyPadding;
            return;
        }

        leftEdge = -15f - destroyPadding;
    }

    /// <summary>
    /// Страховка от старых компонентов.
    /// В Arrow-режиме готовые ворота должен двигать только ArrowGateObstacle.
    /// </summary>
    private void DisableConflictingComponents()
    {
        if (disablePipesComponent)
        {
            Pipes pipes = GetComponent<Pipes>();

            if (pipes != null)
            {
                pipes.enabled = false;

                if (logDebug)
                {
                    Debug.LogWarning(
                        $"ArrowGateObstacle: на '{name}' найден Pipes.cs. Он отключён, чтобы prefab не двигался дважды.",
                        this
                    );
                }
            }
        }

        if (disableOldArrowCorridorObstacle)
        {
            MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();

            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];

                if (behaviour == null || behaviour == this)
                {
                    continue;
                }

                if (behaviour.GetType().Name == "ArrowCorridorObstacle")
                {
                    behaviour.enabled = false;

                    if (logDebug)
                    {
                        Debug.LogWarning(
                            $"ArrowGateObstacle: на '{name}' найден старый ArrowCorridorObstacle. Он отключён.",
                            this
                        );
                    }
                }
            }
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        speed = Mathf.Max(0.1f, speed);
        destroyPadding = Mathf.Max(0f, destroyPadding);
    }
#endif
}