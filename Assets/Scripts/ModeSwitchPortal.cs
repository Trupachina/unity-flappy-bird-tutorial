using System;
using UnityEngine;

/// <summary>
/// Движущийся портал смены режима игрока.
///
/// Использование:
/// - portal_in включает Flight;
/// - portal_out возвращает Flappy.
///
/// Портал:
/// - двигается влево как трубы;
/// - срабатывает только при пересечении с Player;
/// - вызывает Player.SetMoveMode(targetMode);
/// - уведомляет Spawner через событие OnPortalActivated;
/// - уничтожается после срабатывания или после ухода за левую границу экрана.
///
/// Важно:
/// Portal не должен иметь Tag = Obstacle или Tag = Scoring.
/// BoxCollider2D должен быть Is Trigger = true.
/// </summary>
[DisallowMultipleComponent]
public class ModeSwitchPortal : MonoBehaviour
{
    public static event Action<Player.PlayerMoveMode, ModeSwitchPortal> OnPortalActivated;

    [Header("Режим")]
    [Tooltip("Режим, который включится после прохождения портала.")]
    [SerializeField] private Player.PlayerMoveMode targetMode = Player.PlayerMoveMode.Flight;

    [Header("Движение")]
    [Tooltip("Скорость движения портала влево.")]
    [SerializeField] private float speed = 5f;

    [Tooltip("Запас за левой границей экрана перед уничтожением.")]
    [SerializeField] private float destroyPadding = 1.5f;

    [Header("Поведение")]
    [Tooltip("Уничтожать портал после срабатывания.")]
    [SerializeField] private bool destroyAfterActivation = true;

    [Tooltip("Писать в консоль настройку и срабатывание портала.")]
    [SerializeField] private bool logDebug = true;

    private float leftEdge = -15f;
    private bool activated;

    private void Awake()
    {
        EnsureTriggerCollider();
        CalculateLeftEdge();
    }

    private void Update()
    {
        MoveLeft();
        DestroyIfOutsideScreen();
    }

    /// <summary>
    /// Настраивает портал после создания из Spawner.
    /// </summary>
    public void Configure(Player.PlayerMoveMode newTargetMode, float movementSpeed, bool debugLogs)
    {
        targetMode = newTargetMode;
        speed = Mathf.Max(0.1f, movementSpeed);
        logDebug = debugLogs;

        EnsureTriggerCollider();
        CalculateLeftEdge();

        if (logDebug)
        {
            Debug.Log(
                $"ModeSwitchPortal configured: targetMode = {targetMode}, speed = {speed:F2}.",
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

    private void EnsureTriggerCollider()
    {
        Collider2D collider2D = GetComponent<Collider2D>();

        if (collider2D == null)
        {
            collider2D = gameObject.AddComponent<BoxCollider2D>();
        }

        collider2D.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (activated)
        {
            return;
        }

        Player player = other.GetComponent<Player>();

        if (player == null)
        {
            player = other.GetComponentInParent<Player>();
        }

        if (player == null)
        {
            return;
        }

        activated = true;

        player.SetMoveMode(targetMode);
        OnPortalActivated?.Invoke(targetMode, this);

        if (logDebug)
        {
            Debug.Log($"ModeSwitchPortal: игрок прошёл портал. Новый режим: {targetMode}.", this);
        }

        if (destroyAfterActivation)
        {
            Destroy(gameObject);
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