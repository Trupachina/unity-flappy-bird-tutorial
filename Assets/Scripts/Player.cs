using UnityEngine;

/// <summary>
/// Управляет игроком и режимами движения:
/// - Flappy: классический прыжок по нажатию;
/// - Flight: удержание кнопки поднимает игрока вверх, отпускание опускает вниз;
/// - Arrow: каждое нажатие меняет направление движения вверх/вниз.
///
/// Также отвечает за визуальную смену спрайта:
/// - обычная птица в Flappy;
/// - птица на корабле в Flight;
/// - отдельный спрайт/анимация для Arrow, если позже режим будет возвращён.
///
/// Важно:
/// при смерти и ResetPosition() режим НЕ сбрасывается автоматически.
/// Если игрок умер в Flight, он остаётся в Flight до тех пор, пока portal_out
/// не вызовет SetMoveMode(Flappy).
/// </summary>
public class Player : MonoBehaviour
{
    public enum PlayerMoveMode
    {
        Flappy,
        Flight,
        Arrow
    }

    [Header("Базовые настройки старой версии")]
    public Sprite[] sprites;
    public float strength = 5f;
    public float gravity = -9.81f;
    public float tilt = 5f;
    public AudioClip jumpSound;
    public AudioClip hitSound;
    public AudioClip scoreSound;

    [Header("Режим движения")]
    [SerializeField] private PlayerMoveMode currentMoveMode = PlayerMoveMode.Flappy;

    [Tooltip("Писать в консоль смену режима игрока.")]
    [SerializeField] private bool logModeChanges = true;

    [Header("Flight Mode")]
    [Tooltip("Ускорение вверх при удержании кнопки в режиме Flight.")]
    [SerializeField] private float flightLiftAcceleration = 18f;

    [Tooltip("Гравитация в режиме Flight, когда кнопка не удерживается.")]
    [SerializeField] private float flightGravity = -11f;

    [Tooltip("Максимальная скорость подъёма в режиме Flight.")]
    [SerializeField] private float flightMaxUpVelocity = 6f;

    [Tooltip("Максимальная скорость падения в режиме Flight.")]
    [SerializeField] private float flightMaxDownVelocity = -6.5f;

    [Header("Arrow Mode")]
    [Tooltip("Вертикальная скорость стрелки.")]
    [SerializeField] private float arrowVerticalSpeed = 3.4f;

    [Tooltip("Наклон игрока в режиме Arrow при движении вверх/вниз.")]
    [SerializeField] private float arrowTiltAngle = 35f;

    [Tooltip("След за игроком в режиме Arrow. Можно оставить пустым.")]
    [SerializeField] private TrailRenderer arrowTrail;

    [Header("Визуал режимов")]
    [Tooltip("Спрайты обычной птицы. Если пусто, используются базовые sprites.")]
    [SerializeField] private Sprite[] flappySprites;

    [Tooltip("Спрайты птицы на корабле. Можно указать один спрайт корабля, если анимация не нужна.")]
    [SerializeField] private Sprite[] flightSprites;

    [Tooltip("Спрайты для Arrow-режима, если позже вернёшь стрелку.")]
    [SerializeField] private Sprite[] arrowSprites;

    [Tooltip("Скорость анимации обычной птицы.")]
    [SerializeField] private float flappyAnimationInterval = 0.15f;

    [Tooltip("Скорость анимации корабля в Flight. Если один спрайт, значения не имеет.")]
    [SerializeField] private float flightAnimationInterval = 0.15f;

    [Tooltip("Скорость анимации Arrow-режима.")]
    [SerializeField] private float arrowAnimationInterval = 0.15f;

    [Header("Старый TrailRenderer для Flight")]
    [Tooltip("Старый TrailRenderer за кораблём. Лучше оставить пустым и использовать PlayerFlightExhaustTrail.")]
    [SerializeField] private TrailRenderer flightTrail;

    [Tooltip("Включать старый TrailRenderer в Flight. Лучше false, если используется PlayerFlightExhaustTrail.")]
    [SerializeField] private bool useFlightTrail = false;

    private SpriteRenderer spriteRenderer;
    private Vector3 direction;
    private int spriteIndex;
    private AudioSource audioSource;
    private bool isInvincible;

    private bool arrowMovingUp = true;
    private float animationTimer;

    private PlayerFlightExhaustTrail flightExhaustTrail;

    public PlayerMoveMode CurrentMoveMode => currentMoveMode;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        audioSource = GetComponent<AudioSource>();
        flightExhaustTrail = GetComponent<PlayerFlightExhaustTrail>();

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        if (arrowTrail == null)
        {
            arrowTrail = GetComponent<TrailRenderer>();
        }

        ApplyModeVisuals(forceFirstFrame: true);
        UpdateTrailsState();
    }

    private void OnEnable()
    {
        ResetPosition();
    }

    private void OnDisable()
    {
        ClearFlightEffects();
    }

    private void Update()
    {
        UpdateAnimation();

        switch (currentMoveMode)
        {
            case PlayerMoveMode.Flight:
                UpdateFlightMode();
                break;

            case PlayerMoveMode.Arrow:
                UpdateArrowMode();
                break;

            case PlayerMoveMode.Flappy:
            default:
                UpdateFlappyMode();
                break;
        }
    }

    /// <summary>
    /// Переключает режим движения и визуал игрока.
    /// Вызывается порталами ModeSwitchPortal.
    /// </summary>
    public void SetMoveMode(PlayerMoveMode newMode)
    {
        if (currentMoveMode == newMode)
        {
            return;
        }

        PlayerMoveMode previousMode = currentMoveMode;
        currentMoveMode = newMode;

        // Очищаем выхлоп только при выходе из Flight.
        // При входе в Flight ничего не чистим агрессивно, чтобы не убить новый эффект.
        if (previousMode == PlayerMoveMode.Flight && newMode != PlayerMoveMode.Flight)
        {
            ClearFlightEffects();
        }

        direction = Vector3.zero;
        animationTimer = 0f;
        spriteIndex = 0;

        if (newMode == PlayerMoveMode.Arrow)
        {
            arrowMovingUp = true;
        }

        ApplyModeVisuals(forceFirstFrame: true);
        UpdateTrailsState();

        if (logModeChanges)
        {
            Debug.Log($"Player: режим движения изменён на {currentMoveMode}.", this);
        }
    }

    /// <summary>
    /// Классический Flappy:
    /// одиночное нажатие даёт импульс вверх.
    /// </summary>
    private void UpdateFlappyMode()
    {
        if (IsJumpPressed())
        {
            direction = Vector3.up * strength;
            PlayJumpSound();
        }

        direction.y += gravity * Time.deltaTime;
        transform.position += direction * Time.deltaTime;

        ApplyTilt(direction.y * tilt);
    }

    /// <summary>
    /// Flight:
    /// удержание кнопки поднимает игрока, отпускание опускает вниз.
    /// </summary>
    private void UpdateFlightMode()
    {
        if (IsJumpHeld())
        {
            direction.y += flightLiftAcceleration * Time.deltaTime;
        }
        else
        {
            direction.y += flightGravity * Time.deltaTime;
        }

        direction.y = Mathf.Clamp(direction.y, flightMaxDownVelocity, flightMaxUpVelocity);

        transform.position += direction * Time.deltaTime;

        ApplyTilt(direction.y * tilt);
    }

    /// <summary>
    /// Arrow пока можно не использовать, но логика оставлена,
    /// чтобы не ломать структуру.
    /// </summary>
    private void UpdateArrowMode()
    {
        if (IsJumpPressed())
        {
            arrowMovingUp = !arrowMovingUp;
            PlayJumpSound();
        }

        float verticalDirection = arrowMovingUp ? 1f : -1f;
        direction = Vector3.up * (verticalDirection * arrowVerticalSpeed);

        transform.position += direction * Time.deltaTime;

        float targetAngle = arrowMovingUp ? arrowTiltAngle : -arrowTiltAngle;
        ApplyTilt(targetAngle);
    }

    private void UpdateAnimation()
    {
        Sprite[] activeSprites = GetActiveSprites();

        if (activeSprites == null || activeSprites.Length == 0 || spriteRenderer == null)
        {
            return;
        }

        if (activeSprites.Length == 1)
        {
            if (spriteRenderer.sprite != activeSprites[0])
            {
                spriteRenderer.sprite = activeSprites[0];
            }

            return;
        }

        float interval = GetCurrentAnimationInterval();

        animationTimer += Time.deltaTime;

        if (animationTimer < interval)
        {
            return;
        }

        animationTimer = 0f;
        spriteIndex++;

        if (spriteIndex >= activeSprites.Length)
        {
            spriteIndex = 0;
        }

        spriteRenderer.sprite = activeSprites[spriteIndex];
    }

    private Sprite[] GetActiveSprites()
    {
        switch (currentMoveMode)
        {
            case PlayerMoveMode.Flight:
                return HasSprites(flightSprites) ? flightSprites : GetFallbackFlappySprites();

            case PlayerMoveMode.Arrow:
                return HasSprites(arrowSprites) ? arrowSprites : GetFallbackFlappySprites();

            case PlayerMoveMode.Flappy:
            default:
                return GetFallbackFlappySprites();
        }
    }

    private Sprite[] GetFallbackFlappySprites()
    {
        return HasSprites(flappySprites) ? flappySprites : sprites;
    }

    private float GetCurrentAnimationInterval()
    {
        switch (currentMoveMode)
        {
            case PlayerMoveMode.Flight:
                return Mathf.Max(0.03f, flightAnimationInterval);

            case PlayerMoveMode.Arrow:
                return Mathf.Max(0.03f, arrowAnimationInterval);

            case PlayerMoveMode.Flappy:
            default:
                return Mathf.Max(0.03f, flappyAnimationInterval);
        }
    }

    private bool HasSprites(Sprite[] spriteArray)
    {
        return spriteArray != null && spriteArray.Length > 0;
    }

    private void ApplyModeVisuals(bool forceFirstFrame)
    {
        Sprite[] activeSprites = GetActiveSprites();

        if (activeSprites == null || activeSprites.Length == 0 || spriteRenderer == null)
        {
            return;
        }

        if (forceFirstFrame)
        {
            spriteIndex = 0;
            spriteRenderer.sprite = activeSprites[0];
        }
    }

    private void UpdateTrailsState()
    {
        if (arrowTrail != null)
        {
            bool shouldEnableArrowTrail = currentMoveMode == PlayerMoveMode.Arrow;
            arrowTrail.enabled = shouldEnableArrowTrail;
            arrowTrail.emitting = shouldEnableArrowTrail;

            if (!shouldEnableArrowTrail)
            {
                arrowTrail.Clear();
            }
        }

        if (flightTrail != null)
        {
            bool shouldEnableFlightTrail = useFlightTrail && currentMoveMode == PlayerMoveMode.Flight;
            flightTrail.enabled = shouldEnableFlightTrail;
            flightTrail.emitting = shouldEnableFlightTrail;

            if (!shouldEnableFlightTrail)
            {
                flightTrail.Clear();
            }
        }
    }

    private void ApplyTilt(float zAngle)
    {
        Vector3 rotation = transform.eulerAngles;
        rotation.z = zAngle;
        transform.eulerAngles = rotation;
    }

    private bool IsJumpPressed()
    {
        return Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0);
    }

    private bool IsJumpHeld()
    {
        return Input.GetKey(KeyCode.Space) || Input.GetMouseButton(0);
    }

    private void PlayJumpSound()
    {
        if (jumpSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(jumpSound);
        }
    }

    /// <summary>
    /// Очищает только визуальные эффекты Flight.
    /// Не меняет currentMoveMode и не трогает спрайт игрока.
    /// </summary>
    private void ClearFlightEffects()
    {
        if (flightExhaustTrail == null)
        {
            flightExhaustTrail = GetComponent<PlayerFlightExhaustTrail>();
        }

        if (flightExhaustTrail != null)
        {
            flightExhaustTrail.ForceClear();
        }

        if (flightTrail != null)
        {
            flightTrail.emitting = false;
            flightTrail.Clear();
        }
    }

    /// <summary>
    /// Сохраняем старую логику:
    /// сбрасываем только Y, X не трогаем.
    /// Режим движения здесь не меняем.
    /// </summary>
    public void ResetPosition()
    {
        ClearFlightEffects();

        Vector3 position = transform.position;
        position.y = 0f;
        transform.position = position;

        direction = Vector3.zero;
        arrowMovingUp = true;
        animationTimer = 0f;
        spriteIndex = 0;

        ApplyTilt(0f);
        ApplyModeVisuals(forceFirstFrame: true);
        UpdateTrailsState();
    }

    public void SetInvincibility(bool state)
    {
        isInvincible = state;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isInvincible)
        {
            return;
        }

        if (other.gameObject.CompareTag("Obstacle"))
        {
            ClearFlightEffects();

            if (GameManager.Instance != null)
            {
                GameManager.Instance.GameOver();
            }

            if (hitSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(hitSound);
            }
        }
        else if (other.gameObject.CompareTag("Scoring"))
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.IncreaseScore();
            }

            if (scoreSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(scoreSound);
            }
        }
    }
}