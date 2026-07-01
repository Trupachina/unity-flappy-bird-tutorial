using System.Collections;
using UnityEngine;

/// <summary>
/// Управляет одной парой труб:
/// - движение пары труб влево;
/// - настройка прохода между верхней и нижней трубой;
/// - дневной/ночной спрайт;
/// - плавное движение центра прохода вверх/вниз;
/// - резкое смещение прохода на поздних этапах;
/// - динамическое сужение/расширение gap.
///
/// В этой версии добавлена защита от нечестного движения прохода:
/// итоговый центр прохода clamp-ится в безопасной мировой зоне,
/// которую передаёт Spawner.
/// </summary>
public class Pipes : MonoBehaviour
{
    /// <summary>
    /// Режим движения центра прохода между трубами.
    /// Static — проход не двигается.
    /// SmoothSine — мягкое синусоидальное движение вверх/вниз.
    /// SmoothPingPong — движение вверх/вниз с более выраженной сменой направления.
    /// </summary>
    public enum PassageMotionMode
    {
        Static,
        SmoothSine,
        SmoothPingPong
    }

    /// <summary>
    /// Режим изменения размера прохода между трубами.
    /// Static — gap не меняется.
    /// SmoothPulse — gap плавно сужается и расширяется.
    /// </summary>
    public enum GapMotionMode
    {
        Static,
        SmoothPulse
    }

    [Header("Части труб")]
    [Tooltip("Верхняя труба.")]
    public Transform top;

    [Tooltip("Нижняя труба.")]
    public Transform bottom;

    [Header("Базовое движение")]
    [Tooltip("Скорость движения пары труб влево.")]
    public float speed = 5f;

    [Header("Геометрия")]
    [Tooltip("Базовый вертикальный зазор между верхней и нижней трубой.")]
    public float gap = 3f;

    [Header("Спрайты времени суток")]
    [Tooltip("Дневной спрайт трубы.")]
    public Sprite daySprite;

    [Tooltip("Ночной спрайт трубы.")]
    public Sprite nightSprite;

    [Header("Диагностика")]
    [Tooltip("Показывать предупреждения о некорректно настроенном префабе труб.")]
    public bool showSetupWarnings = true;

    private float leftEdge;

    private SpriteRenderer topRenderer;
    private SpriteRenderer bottomRenderer;
    private SkySwitcher skySwitcher;

    private Vector3 topInitialLocalPosition;
    private Vector3 bottomInitialLocalPosition;
    private bool initialPositionsCached;

    private PassageMotionMode passageMotionMode = PassageMotionMode.Static;
    private float centerMoveAmplitude;
    private float centerMoveFrequency = 1f;
    private float centerMovePhase;
    private float maxCenterOffset;

    private GapMotionMode gapMotionMode = GapMotionMode.Static;
    private float baseGap;
    private float minimumDynamicGap;
    private float maximumDynamicGap;
    private float gapPulseAmplitude;
    private float gapPulseFrequency = 1f;
    private float gapPulsePhase;

    private bool suddenShiftEnabled;
    private float suddenShiftAmplitude;
    private float suddenShiftMinDelay = 0.35f;
    private float suddenShiftMaxDelay = 0.9f;
    private float suddenShiftDuration = 0.1f;
    private float suddenShiftAllowedUntilX = 1.5f;
    private int suddenShiftsLeft;
    private float suddenShiftTimer;
    private float suddenShiftOffset;
    private Coroutine suddenShiftCoroutine;

    private float safeWorldMinCenterY = -10000f;
    private float safeWorldMaxCenterY = 10000f;

    private void Awake()
    {
        CacheInitialPositions();
        CacheRenderers();
        CalculateLeftEdge();

        baseGap = gap;
        minimumDynamicGap = gap;
        maximumDynamicGap = gap;
    }

    private void Start()
    {
        ApplyPipeLayout(0f, gap);
        ConnectToSkySwitcher();
        ApplyInitialDayNightVisual();
    }

    private void Update()
    {
        MovePipeLeft();
        UpdateSuddenShiftTimer();
        UpdateDynamicPipeLayout();
        DestroyIfOutsideScreen();
    }

    private void OnDestroy()
    {
        if (skySwitcher != null)
        {
            skySwitcher.OnDayNightChanged -= SetSprite;
        }

        if (suddenShiftCoroutine != null)
        {
            StopCoroutine(suddenShiftCoroutine);
            suddenShiftCoroutine = null;
        }
    }

    /// <summary>
    /// Базовая настройка трубы после создания из Spawner.
    /// Передаёт актуальный gap и ссылку на SkySwitcher.
    /// </summary>
    public void Configure(float verticalGap, SkySwitcher switcher)
    {
        gap = Mathf.Max(0.1f, verticalGap);
        baseGap = gap;

        minimumDynamicGap = gap;
        maximumDynamicGap = gap;

        skySwitcher = switcher;

        CacheInitialPositions();
        CacheRenderers();
        ApplyPipeLayout(0f, gap);

        SubscribeToSkySwitcher();
        ApplyInitialDayNightVisual();
    }

    /// <summary>
    /// Настройка прогрессивной сложности для конкретной пары труб.
    /// Spawner рассчитывает параметры этапа сложности и передаёт их сюда.
    ///
    /// newSafeWorldMinCenterY/newSafeWorldMaxCenterY защищают движущийся проход:
    /// даже если синусоида или пульсация пытаются увести центр слишком высоко/низко,
    /// итоговое положение прохода останется внутри безопасной игровой зоны.
    /// </summary>
    public void ConfigureDifficulty(
        float pipeSpeed,
        PassageMotionMode newPassageMotionMode,
        float newCenterMoveAmplitude,
        float newCenterMoveFrequency,
        float newCenterMovePhase,
        float newMaxCenterOffset,
        GapMotionMode newGapMotionMode,
        float newMinimumDynamicGap,
        float newMaximumDynamicGap,
        float newGapPulseAmplitude,
        float newGapPulseFrequency,
        float newGapPulsePhase,
        bool allowSuddenShift,
        float suddenMaxAmplitude,
        float suddenMinDelay,
        float suddenMaxDelay,
        int maxSuddenShifts,
        float suddenLerpDuration,
        float allowSuddenShiftUntilX,
        float newSafeWorldMinCenterY = -10000f,
        float newSafeWorldMaxCenterY = 10000f)
    {
        speed = Mathf.Max(0.1f, pipeSpeed);

        passageMotionMode = newPassageMotionMode;
        centerMoveAmplitude = Mathf.Max(0f, newCenterMoveAmplitude);
        centerMoveFrequency = Mathf.Max(0.01f, newCenterMoveFrequency);
        centerMovePhase = newCenterMovePhase;
        maxCenterOffset = Mathf.Max(0f, newMaxCenterOffset);

        baseGap = Mathf.Max(0.1f, gap);
        gapMotionMode = newGapMotionMode;
        minimumDynamicGap = Mathf.Max(0.1f, newMinimumDynamicGap);
        maximumDynamicGap = Mathf.Max(minimumDynamicGap, newMaximumDynamicGap);
        gapPulseAmplitude = Mathf.Max(0f, newGapPulseAmplitude);
        gapPulseFrequency = Mathf.Max(0.01f, newGapPulseFrequency);
        gapPulsePhase = newGapPulsePhase;

        suddenShiftEnabled = allowSuddenShift && maxSuddenShifts > 0 && suddenMaxAmplitude > 0f;
        suddenShiftAmplitude = Mathf.Max(0f, suddenMaxAmplitude);
        suddenShiftMinDelay = Mathf.Max(0.05f, suddenMinDelay);
        suddenShiftMaxDelay = Mathf.Max(suddenShiftMinDelay, suddenMaxDelay);
        suddenShiftsLeft = Mathf.Max(0, maxSuddenShifts);
        suddenShiftDuration = Mathf.Max(0.01f, suddenLerpDuration);
        suddenShiftAllowedUntilX = allowSuddenShiftUntilX;
        suddenShiftOffset = 0f;

        safeWorldMinCenterY = Mathf.Min(newSafeWorldMinCenterY, newSafeWorldMaxCenterY);
        safeWorldMaxCenterY = Mathf.Max(newSafeWorldMinCenterY, newSafeWorldMaxCenterY);

        ResetSuddenShiftTimer();
        ApplyPipeLayout(0f, baseGap);
    }

    /// <summary>
    /// Старый метод из рабочей версии.
    /// Мгновенно ставит дневной или ночной спрайт труб.
    /// </summary>
    public void SetSprite(bool isDay)
    {
        CacheRenderers();

        Sprite selectedSprite = isDay ? daySprite : nightSprite;

        if (selectedSprite == null)
        {
            selectedSprite = ResolveFallbackSprite();
        }

        if (selectedSprite == null)
        {
            return;
        }

        if (topRenderer != null)
        {
            topRenderer.sprite = selectedSprite;
        }

        if (bottomRenderer != null)
        {
            bottomRenderer.sprite = selectedSprite;
        }
    }

    private void MovePipeLeft()
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

    /// <summary>
    /// Каждый кадр пересчитывает:
    /// - смещение центра прохода;
    /// - текущий gap;
    /// - итоговое положение верхней и нижней трубы.
    /// </summary>
    private void UpdateDynamicPipeLayout()
    {
        float centerOffset = CalculateCenterOffset();
        float currentGap = CalculateCurrentGap();

        ApplyPipeLayout(centerOffset, currentGap);
    }

    /// <summary>
    /// Считает смещение центра прохода вверх/вниз.
    /// Это движение не меняет gap, а двигает проход целиком.
    /// </summary>
    private float CalculateCenterOffset()
    {
        float motionOffset = 0f;

        if (passageMotionMode == PassageMotionMode.SmoothSine && centerMoveAmplitude > 0f)
        {
            motionOffset = Mathf.Sin(Time.time * centerMoveFrequency + centerMovePhase) * centerMoveAmplitude;
        }
        else if (passageMotionMode == PassageMotionMode.SmoothPingPong && centerMoveAmplitude > 0f)
        {
            float pingPong = Mathf.PingPong(Time.time * centerMoveFrequency + centerMovePhase, 1f);
            float smoothed = Mathf.SmoothStep(0f, 1f, pingPong);
            motionOffset = Mathf.Lerp(-centerMoveAmplitude, centerMoveAmplitude, smoothed);
        }

        float totalOffset = motionOffset + suddenShiftOffset;

        if (maxCenterOffset > 0f)
        {
            totalOffset = Mathf.Clamp(totalOffset, -maxCenterOffset, maxCenterOffset);
        }

        // Главное исправление для движущихся труб:
        // итоговый центр прохода не должен уходить слишком высоко или слишком низко.
        float minLocalOffset = safeWorldMinCenterY - transform.position.y;
        float maxLocalOffset = safeWorldMaxCenterY - transform.position.y;
        totalOffset = Mathf.Clamp(totalOffset, minLocalOffset, maxLocalOffset);

        return totalOffset;
    }

    /// <summary>
    /// Считает текущий gap.
    /// На поздних этапах gap может плавно сужаться и расширяться.
    /// </summary>
    private float CalculateCurrentGap()
    {
        float currentGap = baseGap > 0f ? baseGap : gap;

        if (gapMotionMode == GapMotionMode.SmoothPulse && gapPulseAmplitude > 0f)
        {
            float pulse = Mathf.Sin(Time.time * gapPulseFrequency + gapPulsePhase);
            currentGap += pulse * gapPulseAmplitude;
        }

        if (minimumDynamicGap > 0f && maximumDynamicGap > 0f)
        {
            currentGap = Mathf.Clamp(currentGap, minimumDynamicGap, maximumDynamicGap);
        }

        return Mathf.Max(0.1f, currentGap);
    }

    /// <summary>
    /// Применяет итоговое положение верхней и нижней трубы.
    ///
    /// centerOffset двигает весь проход вверх/вниз.
    /// currentGap разводит или сближает верхнюю и нижнюю трубу.
    /// </summary>
    private void ApplyPipeLayout(float centerOffset, float currentGap)
    {
        CacheInitialPositions();

        if (top != null)
        {
            top.localPosition = topInitialLocalPosition + Vector3.up * (currentGap * 0.5f + centerOffset);
        }

        if (bottom != null)
        {
            bottom.localPosition = bottomInitialLocalPosition + Vector3.down * (currentGap * 0.5f) + Vector3.up * centerOffset;
        }
    }

    /// <summary>
    /// Управляет таймером резкого смещения прохода.
    /// Смещение разрешено только пока труба ещё достаточно далеко от игрока.
    /// В текущем балансе Spawner выключает suddenShift для движущихся труб.
    /// </summary>
    private void UpdateSuddenShiftTimer()
    {
        if (!suddenShiftEnabled || suddenShiftsLeft <= 0)
        {
            return;
        }

        if (transform.position.x <= suddenShiftAllowedUntilX)
        {
            return;
        }

        if (suddenShiftCoroutine != null)
        {
            return;
        }

        suddenShiftTimer -= Time.deltaTime;

        if (suddenShiftTimer <= 0f)
        {
            suddenShiftsLeft--;

            float targetOffset = Random.Range(-suddenShiftAmplitude, suddenShiftAmplitude);
            suddenShiftCoroutine = StartCoroutine(SmoothSuddenShift(targetOffset));
        }
    }

    /// <summary>
    /// Быстро, но не в один кадр, смещает проход.
    /// Это выглядит как резкая смена положения, но без технического телепорта.
    /// </summary>
    private IEnumerator SmoothSuddenShift(float targetOffset)
    {
        float startOffset = suddenShiftOffset;
        float elapsedTime = 0f;

        while (elapsedTime < suddenShiftDuration)
        {
            elapsedTime += Time.deltaTime;

            float t = Mathf.Clamp01(elapsedTime / suddenShiftDuration);
            float smoothedT = Mathf.SmoothStep(0f, 1f, t);

            suddenShiftOffset = Mathf.Lerp(startOffset, targetOffset, smoothedT);

            yield return null;
        }

        suddenShiftOffset = targetOffset;
        suddenShiftCoroutine = null;

        ResetSuddenShiftTimer();
    }

    private void ResetSuddenShiftTimer()
    {
        suddenShiftTimer = Random.Range(suddenShiftMinDelay, suddenShiftMaxDelay);
    }

    private void CacheInitialPositions()
    {
        if (initialPositionsCached)
        {
            return;
        }

        if (top != null)
        {
            topInitialLocalPosition = top.localPosition;
        }
        else if (showSetupWarnings)
        {
            Debug.LogWarning("Pipes: не назначена ссылка на верхнюю трубу top.", this);
        }

        if (bottom != null)
        {
            bottomInitialLocalPosition = bottom.localPosition;
        }
        else if (showSetupWarnings)
        {
            Debug.LogWarning("Pipes: не назначена ссылка на нижнюю трубу bottom.", this);
        }

        initialPositionsCached = true;
    }

    private void CacheRenderers()
    {
        if (topRenderer == null && top != null)
        {
            topRenderer = top.GetComponent<SpriteRenderer>();
        }

        if (bottomRenderer == null && bottom != null)
        {
            bottomRenderer = bottom.GetComponent<SpriteRenderer>();
        }

        if (showSetupWarnings && top != null && topRenderer == null)
        {
            Debug.LogWarning("Pipes: на объекте top нет SpriteRenderer.", this);
        }

        if (showSetupWarnings && bottom != null && bottomRenderer == null)
        {
            Debug.LogWarning("Pipes: на объекте bottom нет SpriteRenderer.", this);
        }
    }

    private void CalculateLeftEdge()
    {
        Camera mainCamera = Camera.main;

        if (mainCamera != null)
        {
            leftEdge = mainCamera.ScreenToWorldPoint(Vector3.zero).x - 1f;
        }
        else
        {
            leftEdge = -15f;

            if (showSetupWarnings)
            {
                Debug.LogWarning("Pipes: Camera.main не найдена. Используется запасная левая граница -15.", this);
            }
        }
    }

    private void ConnectToSkySwitcher()
    {
        if (skySwitcher == null)
        {
            skySwitcher = FindObjectOfType<SkySwitcher>();
        }

        SubscribeToSkySwitcher();
    }

    private void SubscribeToSkySwitcher()
    {
        if (skySwitcher == null)
        {
            return;
        }

        skySwitcher.OnDayNightChanged -= SetSprite;
        skySwitcher.OnDayNightChanged += SetSprite;
    }

    private void ApplyInitialDayNightVisual()
    {
        if (skySwitcher != null)
        {
            SetSprite(skySwitcher.IsDay);
        }
        else
        {
            SetSprite(true);
        }
    }

    private Sprite ResolveFallbackSprite()
    {
        if (topRenderer != null && topRenderer.sprite != null)
        {
            return topRenderer.sprite;
        }

        if (bottomRenderer != null && bottomRenderer.sprite != null)
        {
            return bottomRenderer.sprite;
        }

        return null;
    }
}