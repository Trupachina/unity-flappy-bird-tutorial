using System.Collections;
using UnityEngine;

/// <summary>
/// Создаёт трубы и управляет прогрессивным усложнением игры.
///
/// Сохранено:
/// - базовый Flappy-спавн;
/// - этапы сложности каждые 20 очков;
/// - движущиеся трубы;
/// - Fair Spawn Director;
/// - ступенчатые серии поздней игры.
///
/// Новая логика режимов:
/// - дополнительные режимы больше НЕ включаются таймером посреди маршрута;
/// - Flight включается только через portal_in;
/// - возврат в Flappy происходит только через portal_out;
/// - перед portal_in создаётся пустой участок без труб;
/// - перед portal_out тоже создаётся пустой участок без труб;
/// - Arrow пока не используется.
/// </summary>
public class Spawner : MonoBehaviour
{
    [Header("Префабы труб")]
    [Tooltip("Обычный дневной префаб труб.")]
    public Pipes dayPipesPrefab;

    [Tooltip("Обычный ночной префаб труб.")]
    public Pipes nightPipesPrefab;

    [Header("Префабы порталов")]
    [Tooltip("Портал входа в Flight-режим. В проекте это portal_in.")]
    [SerializeField] private GameObject portalInPrefab;

    [Tooltip("Портал выхода обратно в Flappy-режим. В проекте это portal_out.")]
    [SerializeField] private GameObject portalOutPrefab;

    [Tooltip("Y-позиция появления порталов.")]
    [SerializeField] private float portalSpawnY = 0f;

    [Tooltip("Множитель скорости порталов относительно обычной скорости труб.")]
    [SerializeField] private float portalSpeedMultiplier = 1f;

    [Tooltip("Сколько секунд ждать срабатывания портала, прежде чем включить аварийный сценарий.")]
    [SerializeField] private float portalActivationTimeout = 8f;

    [Tooltip("Если портал выхода не сработал за timeout, принудительно вернуть игрока в Flappy, чтобы игра не зависла.")]
    [SerializeField] private bool forceFlappyOnExitPortalTimeout = true;

    [Header("Базовый спавн")]
    [Tooltip("Базовая пауза между появлением труб.")]
    public float spawnRate = 1f;

    [Tooltip("Минимальное базовое смещение трубы по высоте.")]
    public float minHeight = -1f;

    [Tooltip("Максимальное базовое смещение трубы по высоте.")]
    public float maxHeight = 2f;

    [Tooltip("Базовый зазор между верхней и нижней трубой.")]
    public float verticalGap = 3f;

    [Header("Смена дня и ночи")]
    [Tooltip("Старый интервал смены дня и ночи. Используется только если SkySwitcher не найден или отключено чтение состояния SkySwitcher.")]
    public float switchInterval = 60f;

    [Tooltip("Если SkySwitcher найден, новые трубы берут состояние день/ночь из него.")]
    public bool useSkySwitcherStateWhenAvailable = true;

    [Tooltip("Контроллер смены дня/ночи. Если не назначен, будет найден автоматически.")]
    public SkySwitcher skySwitcher;

    [Header("Прогрессивная сложность")]
    [Tooltip("Включает усложнение по количеству успешных пролётов труб.")]
    public bool useScoreBasedDifficulty = true;

    [Tooltip("Через сколько очков повышается этап сложности.")]
    public int pointsPerDifficultyStage = 20;

    [Tooltip("Минимально допустимый статический gap, ниже которого обычные трубы не сужаются.")]
    public float minimumVerticalGap = 2.35f;

    [Tooltip("Минимальный gap во время динамического сужения/расширения.")]
    public float minimumDynamicGap = 2.25f;

    [Tooltip("Минимально допустимая пауза между обычными трубами.")]
    public float minimumSpawnDelay = 0.80f;

    [Tooltip("Показывать в консоль смену этапов сложности.")]
    public bool logDifficultyStageChanges = true;

    [Header("Портальные Flight-секции")]
    [Tooltip("Главный переключатель портальных Flight-секций.")]
    [SerializeField] private bool enablePortalFlightSegments = true;

    [Tooltip("Писать в консоль подготовку, старт и завершение Flight-секций.")]
    [SerializeField] private bool logPortalSegments = true;

    [Tooltip("Минимальный score, после которого могут появляться Flight-секции.")]
    [SerializeField] private int minScoreBeforePortalSegments = 40;

    [Tooltip("Минимальный этап сложности, после которого могут появляться Flight-секции.")]
    [SerializeField] private int minDifficultyStageForPortalSegments = 2;

    [Tooltip("Вероятность запуска Flight-секции при очередной проверке.")]
    [Range(0f, 1f)]
    [SerializeField] private float portalSegmentChancePerCheck = 0.18f;

    [Tooltip("Минимальная пауза в секундах между Flight-секциями.")]
    [SerializeField] private float minSecondsBetweenPortalSegments = 24f;

    [Tooltip("Минимальное количество обычных труб между Flight-секциями.")]
    [SerializeField] private int minPipesBetweenPortalSegments = 8;

    [Tooltip("Пустой участок перед portal_in. Нужен, чтобы старые трубы ушли с экрана.")]
    [SerializeField] private float prePortalEmptySeconds = 3.0f;

    [Tooltip("Длительность активной Flight-секции после прохода portal_in.")]
    [SerializeField] private float flightSegmentDurationSeconds = 10f;

    [Tooltip("Пустой участок перед portal_out после Flight-труб.")]
    [SerializeField] private float preExitPortalEmptySeconds = 1.6f;

    [Tooltip("Пустой участок после portal_out перед возвратом к обычным трубам.")]
    [SerializeField] private float postExitPortalEmptySeconds = 0.8f;

    [Header("Flight Mode")]
    [Tooltip("Дополнительный gap для Flight-режима.")]
    [SerializeField] private float flightModeGapBonus = 0.12f;

    [Tooltip("В Flight-режиме разрешать движущиеся трубы по текущей сложности.")]
    [SerializeField] private bool allowMovingPipesInFlightMode = true;

    [Header("Fair Spawn Director")]
    [Tooltip("Если включено, Spawner ограничивает резкие перепады высоты между соседними трубами.")]
    public bool useFairSpawnDirector = true;

    [Tooltip("Насколько движущиеся трубы должны быть дальше от крайних верхних/нижних позиций.")]
    public float movingPipeHeightPadding = 0.35f;

    [Tooltip("Дополнительный gap для движущихся труб. Движущийся проход сложнее читать, поэтому ему нужен небольшой запас.")]
    public float movingPipeGapBonus = 0.12f;

    [Tooltip("Минимальная безопасная высота центра прохода в мировых координатах.")]
    public float safeWorldMinCenterY = -1.65f;

    [Tooltip("Максимальная безопасная высота центра прохода в мировых координатах.")]
    public float safeWorldMaxCenterY = 2.65f;

    [Tooltip("Сколько обычных труб должно пройти между ступенчатыми сериями.")]
    public int minimumPipesBetweenStaircases = 5;

    [Header("Ступенчатые серии поздней игры")]
    [Tooltip("Разрешить продолжительные серии близких труб в форме ступенек на позднем этапе.")]
    public bool enableStaircasePatterns = true;

    [Tooltip("Минимальное количество труб в ступенчатой серии.")]
    public int minStaircaseLength = 5;

    [Tooltip("Максимальное количество труб в ступенчатой серии.")]
    public int maxStaircaseLength = 7;

    [Tooltip("Вертикальный шаг между соседними трубами в ступенчатой серии.")]
    public float staircaseStepHeight = 0.45f;

    [Tooltip("Во сколько раз промежуток внутри ступенчатой серии короче обычного spawnRate текущего этапа.")]
    public float staircaseSpawnDelayMultiplier = 0.78f;

    [Tooltip("Минимальная пауза между трубами внутри ступенчатой серии.")]
    public float minimumStaircaseSpawnDelay = 0.58f;

    [Tooltip("После ступенчатой серии добавляется пауза, чтобы серия не слипалась со следующей трубой.")]
    public float staircaseRecoveryDelayMultiplier = 1.10f;

    private bool isDay = true;
    private float switchTimer;

    private Coroutine spawnLoopCoroutine;
    private int lastLoggedDifficultyStage = -1;

    private bool hasLastSpawnHeight;
    private float lastSpawnHeight;
    private int pipesSinceLastStaircase = 999;
    private int pipesSinceLastPortalSegment = 999;

    private Player player;
    private bool portalSegmentRunning;
    private float nextPortalSegmentAllowedTime;

    private bool waitingForPortalActivation;
    private bool expectedPortalActivated;
    private Player.PlayerMoveMode expectedPortalMode;

    private void Awake()
    {
        if (skySwitcher == null)
        {
            skySwitcher = FindObjectOfType<SkySwitcher>();
        }

        if (player == null)
        {
            player = FindObjectOfType<Player>();
        }
    }

    private void OnEnable()
    {
        ModeSwitchPortal.OnPortalActivated += HandlePortalActivated;

        if (spawnLoopCoroutine != null)
        {
            StopCoroutine(spawnLoopCoroutine);
        }

        spawnLoopCoroutine = StartCoroutine(SpawnLoop());
    }

    private void OnDisable()
    {
        ModeSwitchPortal.OnPortalActivated -= HandlePortalActivated;

        if (spawnLoopCoroutine != null)
        {
            StopCoroutine(spawnLoopCoroutine);
            spawnLoopCoroutine = null;
        }

        SetPlayerMode(Player.PlayerMoveMode.Flappy);

        portalSegmentRunning = false;
        waitingForPortalActivation = false;
        expectedPortalActivated = false;
    }

    private void OnValidate()
    {
        pointsPerDifficultyStage = Mathf.Max(20, pointsPerDifficultyStage);

        minimumVerticalGap = Mathf.Max(2.2f, minimumVerticalGap);
        minimumDynamicGap = Mathf.Max(2.1f, minimumDynamicGap);
        minimumSpawnDelay = Mathf.Max(0.72f, minimumSpawnDelay);

        portalSpeedMultiplier = Mathf.Clamp(portalSpeedMultiplier, 0.5f, 1.5f);
        portalActivationTimeout = Mathf.Max(2f, portalActivationTimeout);

        minScoreBeforePortalSegments = Mathf.Max(0, minScoreBeforePortalSegments);
        minDifficultyStageForPortalSegments = Mathf.Max(0, minDifficultyStageForPortalSegments);
        portalSegmentChancePerCheck = Mathf.Clamp01(portalSegmentChancePerCheck);
        minSecondsBetweenPortalSegments = Mathf.Max(1f, minSecondsBetweenPortalSegments);
        minPipesBetweenPortalSegments = Mathf.Max(0, minPipesBetweenPortalSegments);

        prePortalEmptySeconds = Mathf.Max(0f, prePortalEmptySeconds);
        flightSegmentDurationSeconds = Mathf.Max(2f, flightSegmentDurationSeconds);
        preExitPortalEmptySeconds = Mathf.Max(0f, preExitPortalEmptySeconds);
        postExitPortalEmptySeconds = Mathf.Max(0f, postExitPortalEmptySeconds);

        flightModeGapBonus = Mathf.Clamp(flightModeGapBonus, 0f, 0.6f);

        movingPipeHeightPadding = Mathf.Clamp(movingPipeHeightPadding, 0.15f, 0.75f);
        movingPipeGapBonus = Mathf.Clamp(movingPipeGapBonus, 0f, 0.35f);

        minimumPipesBetweenStaircases = Mathf.Max(0, minimumPipesBetweenStaircases);

        minStaircaseLength = Mathf.Clamp(minStaircaseLength, 5, 9);
        maxStaircaseLength = Mathf.Clamp(maxStaircaseLength, minStaircaseLength, 9);

        staircaseStepHeight = Mathf.Clamp(staircaseStepHeight, 0.25f, 0.60f);
        staircaseSpawnDelayMultiplier = Mathf.Clamp(staircaseSpawnDelayMultiplier, 0.65f, 1.15f);
        minimumStaircaseSpawnDelay = Mathf.Max(0.50f, minimumStaircaseSpawnDelay);
        staircaseRecoveryDelayMultiplier = Mathf.Max(1.00f, staircaseRecoveryDelayMultiplier);

        if (safeWorldMaxCenterY < safeWorldMinCenterY)
        {
            float temporary = safeWorldMinCenterY;
            safeWorldMinCenterY = safeWorldMaxCenterY;
            safeWorldMaxCenterY = temporary;
        }
    }

    private void Update()
    {
        UpdateFallbackDayNightTimer();
    }

    /// <summary>
    /// Основной цикл создания препятствий.
    /// Если портальные режимы отключены, работает как обычный Spawner.
    /// </summary>
    private IEnumerator SpawnLoop()
    {
        while (true)
        {
            DifficultySettings settings = GetCurrentDifficultySettings();

            if (ShouldStartPortalFlightSegment(settings))
            {
                yield return StartCoroutine(RunPortalFlightSegment());
                continue;
            }

            yield return new WaitForSeconds(settings.spawnDelay);

            settings = GetCurrentDifficultySettings();
            LogDifficultyStageIfNeeded(settings.stage);

            if (ShouldSpawnStaircase(settings))
            {
                yield return StartCoroutine(SpawnStaircasePattern(settings));

                float recoveryDelay = Mathf.Max(
                    minimumStaircaseSpawnDelay,
                    settings.spawnDelay * staircaseRecoveryDelayMultiplier
                );

                yield return new WaitForSeconds(recoveryDelay);
            }
            else
            {
                bool isMovingPipe = IsMovingPipe(settings);
                float height = SelectFairHeight(settings, settings.minHeight, settings.maxHeight, isMovingPipe);
                SpawnSinglePipe(settings, height, false);
            }
        }
    }

    /// <summary>
    /// Полная портальная секция:
    /// пустой участок -> portal_in -> Flight-трубы -> пустой участок -> portal_out -> обычная игра.
    /// </summary>
    private IEnumerator RunPortalFlightSegment()
    {
        portalSegmentRunning = true;

        LogPortalMessage(
            $"[PORTAL] Подготовка Flight-секции. " +
            $"Пустой участок перед portal_in: {prePortalEmptySeconds:F1} сек."
        );

        if (prePortalEmptySeconds > 0f)
        {
            yield return new WaitForSeconds(prePortalEmptySeconds);
        }

        DifficultySettings settingsAtPortalIn = GetCurrentDifficultySettings();
        float portalSpeed = GetPortalSpeed(settingsAtPortalIn);

        SpawnPortal(portalInPrefab, Player.PlayerMoveMode.Flight, portalSpeed);

        bool flightPortalActivated = false;
        yield return StartCoroutine(WaitForPortalActivation(Player.PlayerMoveMode.Flight, portalActivationTimeout, result =>
        {
            flightPortalActivated = result;
        }));

        if (!flightPortalActivated)
        {
            LogPortalMessage("[PORTAL] portal_in не был активирован. Flight-секция отменена.");
            FinishPortalSegment();
            yield break;
        }

        LogPortalMessage($"[PORTAL] Flight-секция началась. Длительность: {flightSegmentDurationSeconds:F1} сек.");

        float elapsed = 0f;

        while (elapsed < flightSegmentDurationSeconds)
        {
            DifficultySettings flightSettings = GetCurrentDifficultySettings();
            float delay = Mathf.Max(minimumSpawnDelay, flightSettings.spawnDelay);

            yield return new WaitForSeconds(delay);
            elapsed += delay;

            SpawnFlightModePipe(flightSettings);
        }

        LogPortalMessage(
            $"[PORTAL] Подготовка выхода из Flight. " +
            $"Пустой участок перед portal_out: {preExitPortalEmptySeconds:F1} сек."
        );

        if (preExitPortalEmptySeconds > 0f)
        {
            yield return new WaitForSeconds(preExitPortalEmptySeconds);
        }

        DifficultySettings settingsAtPortalOut = GetCurrentDifficultySettings();
        float exitPortalSpeed = GetPortalSpeed(settingsAtPortalOut);

        SpawnPortal(portalOutPrefab, Player.PlayerMoveMode.Flappy, exitPortalSpeed);

        bool flappyPortalActivated = false;
        yield return StartCoroutine(WaitForPortalActivation(Player.PlayerMoveMode.Flappy, portalActivationTimeout, result =>
        {
            flappyPortalActivated = result;
        }));

        if (!flappyPortalActivated)
        {
            Debug.LogWarning(
                "[PORTAL] portal_out не был активирован за отведённое время. " +
                "Это может означать, что портал стоит слишком высоко/низко или collider слишком узкий.",
                this
            );

            if (forceFlappyOnExitPortalTimeout)
            {
                SetPlayerMode(Player.PlayerMoveMode.Flappy);

                LogPortalMessage("[PORTAL] Игрок принудительно возвращён в Flappy из-за timeout portal_out.");
            }
        }

        if (postExitPortalEmptySeconds > 0f)
        {
            yield return new WaitForSeconds(postExitPortalEmptySeconds);
        }

        FinishPortalSegment();
    }

    private void FinishPortalSegment()
    {
        portalSegmentRunning = false;
        nextPortalSegmentAllowedTime = Time.time + minSecondsBetweenPortalSegments;
        pipesSinceLastPortalSegment = 0;

        LogPortalMessage(
            $"[PORTAL] Flight-секция завершена. Следующая секция возможна не раньше чем через {minSecondsBetweenPortalSegments:F1} сек."
        );
    }

    private void SpawnPortal(GameObject portalPrefab, Player.PlayerMoveMode targetMode, float speed)
    {
        if (portalPrefab == null)
        {
            Debug.LogError(
                $"[PORTAL] Не назначен prefab портала для режима {targetMode}. " +
                "Проверь Portal In Prefab / Portal Out Prefab в Spawner.",
                this
            );

            return;
        }

        Vector3 spawnPosition = transform.position;
        spawnPosition.y = portalSpawnY;

        GameObject portalObject = Instantiate(portalPrefab, spawnPosition, portalPrefab.transform.rotation);

        ModeSwitchPortal portal = portalObject.GetComponent<ModeSwitchPortal>();

        if (portal == null)
        {
            portal = portalObject.AddComponent<ModeSwitchPortal>();

            Debug.LogWarning(
                $"[PORTAL] На prefab '{portalPrefab.name}' не было ModeSwitchPortal. Компонент добавлен автоматически.",
                portalObject
            );
        }

        portal.Configure(targetMode, speed, logPortalSegments);

        LogPortalMessage(
            $"[PORTAL] Создан портал '{portalPrefab.name}' -> {targetMode}. " +
            $"Position: {spawnPosition}, Speed: {speed:F2}."
        );
    }

    private IEnumerator WaitForPortalActivation(
        Player.PlayerMoveMode targetMode,
        float timeout,
        System.Action<bool> onComplete)
    {
        waitingForPortalActivation = true;
        expectedPortalActivated = false;
        expectedPortalMode = targetMode;

        float elapsed = 0f;

        while (elapsed < timeout && !expectedPortalActivated)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        bool result = expectedPortalActivated;

        waitingForPortalActivation = false;
        expectedPortalActivated = false;

        onComplete?.Invoke(result);
    }

    private void HandlePortalActivated(Player.PlayerMoveMode activatedMode, ModeSwitchPortal portal)
    {
        if (!waitingForPortalActivation)
        {
            return;
        }

        if (activatedMode != expectedPortalMode)
        {
            return;
        }

        expectedPortalActivated = true;

        LogPortalMessage($"[PORTAL] Подтверждено прохождение портала. Режим: {activatedMode}.");
    }

    private float GetPortalSpeed(DifficultySettings settings)
    {
        Pipes basePrefab = GetSelectedPrefab(GetCurrentDayNightVisualState());

        float baseSpeed = basePrefab != null ? basePrefab.speed : 5f;
        float speed = baseSpeed * settings.speedMultiplier * portalSpeedMultiplier;

        return Mathf.Clamp(speed, 3.5f, 6.5f);
    }

    /// <summary>
    /// Flight использует обычные трубы, но с чуть большим gap.
    /// Основная логика сложности сохраняется.
    /// </summary>
    private void SpawnFlightModePipe(DifficultySettings settings)
    {
        DifficultySettings modeSettings = settings;

        modeSettings.verticalGap += flightModeGapBonus;
        modeSettings.enableSuddenShift = false;
        modeSettings.suddenShiftChance = 0f;
        modeSettings.maxSuddenShifts = 0;
        modeSettings.suddenShiftAmplitude = 0f;
        modeSettings.allowStaircasePattern = false;
        modeSettings.staircaseChance = 0f;

        if (!allowMovingPipesInFlightMode)
        {
            modeSettings.passageMotionMode = Pipes.PassageMotionMode.Static;
            modeSettings.centerMoveAmplitude = 0f;
            modeSettings.centerMoveFrequency = 1f;
            modeSettings.maxCenterOffset = 0f;

            modeSettings.gapMotionMode = Pipes.GapMotionMode.Static;
            modeSettings.minimumDynamicGap = modeSettings.verticalGap;
            modeSettings.maximumDynamicGap = modeSettings.verticalGap;
            modeSettings.gapPulseAmplitude = 0f;
            modeSettings.gapPulseFrequency = 1f;
        }

        ApplySafetyLimits(ref modeSettings);

        bool isMovingPipe = IsMovingPipe(modeSettings);
        float height = SelectFairHeight(modeSettings, modeSettings.minHeight, modeSettings.maxHeight, isMovingPipe);

        SpawnSinglePipe(modeSettings, height, false);
    }

    private bool ShouldStartPortalFlightSegment(DifficultySettings settings)
    {
        if (!enablePortalFlightSegments || portalSegmentRunning)
        {
            return false;
        }

        if (portalInPrefab == null || portalOutPrefab == null)
        {
            return false;
        }

        if (Time.time < nextPortalSegmentAllowedTime)
        {
            return false;
        }

        if (pipesSinceLastPortalSegment < minPipesBetweenPortalSegments)
        {
            return false;
        }

        int currentScore = GameManager.Instance != null ? GameManager.Instance.score : 0;

        if (currentScore < minScoreBeforePortalSegments)
        {
            return false;
        }

        if (settings.stage < minDifficultyStageForPortalSegments)
        {
            return false;
        }

        return Random.value <= portalSegmentChancePerCheck;
    }

    private void LogPortalMessage(string message)
    {
        if (logPortalSegments)
        {
            Debug.Log(message, this);
        }
    }

    private void SetPlayerMode(Player.PlayerMoveMode mode)
    {
        if (player == null)
        {
            player = FindObjectOfType<Player>();
        }

        if (player != null)
        {
            player.SetMoveMode(mode);
        }
    }

    private void UpdateFallbackDayNightTimer()
    {
        if (useSkySwitcherStateWhenAvailable && skySwitcher != null)
        {
            return;
        }

        switchTimer += Time.deltaTime;

        if (switchTimer >= switchInterval)
        {
            switchTimer = 0f;
            isDay = !isDay;
        }
    }

    private Pipes SpawnSinglePipe(DifficultySettings settings, float verticalOffset, bool isPartOfStaircase)
    {
        bool visualIsDay = GetCurrentDayNightVisualState();
        Pipes selectedPrefab = GetSelectedPrefab(visualIsDay);

        if (selectedPrefab == null)
        {
            Debug.LogError("Spawner: не назначен dayPipesPrefab или nightPipesPrefab.", this);
            return null;
        }

        bool isMovingPipe = IsMovingPipe(settings);
        float safeHeight = ClampHeightToFairRange(settings, verticalOffset, isMovingPipe);
        float effectiveGap = settings.verticalGap;

        if (isMovingPipe)
        {
            effectiveGap += settings.movingPipeGapBonus;
        }

        Vector3 spawnPosition = transform.position + Vector3.up * safeHeight;
        Pipes pipes = Instantiate(selectedPrefab, spawnPosition, Quaternion.identity);

        pipes.Configure(effectiveGap, skySwitcher);
        pipes.SetSprite(visualIsDay);

        float effectiveMinimumDynamicGap = settings.minimumDynamicGap;
        float effectiveMaximumDynamicGap = settings.maximumDynamicGap;

        if (isMovingPipe)
        {
            effectiveMinimumDynamicGap = Mathf.Max(effectiveMinimumDynamicGap, effectiveGap - 0.14f);
            effectiveMaximumDynamicGap = Mathf.Max(effectiveMaximumDynamicGap, effectiveGap + 0.14f);
        }

        bool allowSuddenShiftForThisPipe =
            !isMovingPipe &&
            !isPartOfStaircase &&
            settings.enableSuddenShift &&
            Random.value <= settings.suddenShiftChance;

        pipes.ConfigureDifficulty(
            pipeSpeed: GetPipeSpeed(selectedPrefab, settings),

            newPassageMotionMode: settings.passageMotionMode,
            newCenterMoveAmplitude: settings.centerMoveAmplitude,
            newCenterMoveFrequency: settings.centerMoveFrequency,
            newCenterMovePhase: Random.Range(0f, Mathf.PI * 2f),
            newMaxCenterOffset: settings.maxCenterOffset,

            newGapMotionMode: settings.gapMotionMode,
            newMinimumDynamicGap: effectiveMinimumDynamicGap,
            newMaximumDynamicGap: effectiveMaximumDynamicGap,
            newGapPulseAmplitude: settings.gapPulseAmplitude,
            newGapPulseFrequency: settings.gapPulseFrequency,
            newGapPulsePhase: Random.Range(0f, Mathf.PI * 2f),

            allowSuddenShift: allowSuddenShiftForThisPipe,
            suddenMaxAmplitude: settings.suddenShiftAmplitude,
            suddenMinDelay: settings.suddenShiftMinDelay,
            suddenMaxDelay: settings.suddenShiftMaxDelay,
            maxSuddenShifts: settings.maxSuddenShifts,
            suddenLerpDuration: settings.suddenShiftDuration,
            allowSuddenShiftUntilX: settings.suddenShiftAllowedUntilX,

            newSafeWorldMinCenterY: safeWorldMinCenterY,
            newSafeWorldMaxCenterY: safeWorldMaxCenterY
        );

        RegisterSpawnHeight(safeHeight);

        return pipes;
    }

    private IEnumerator SpawnStaircasePattern(DifficultySettings settings)
    {
        int minLength = Mathf.Clamp(minStaircaseLength, 5, 9);
        int maxLength = Mathf.Clamp(maxStaircaseLength, minLength, 9);
        int length = Random.Range(minLength, maxLength + 1);

        float stepHeight = Mathf.Clamp(staircaseStepHeight, 0.25f, 0.60f);

        bool directionIsUp = Random.value < 0.5f;
        float directionMultiplier = directionIsUp ? 1f : -1f;

        float[] heightOffsets = new float[length];

        for (int i = 0; i < length; i++)
        {
            int ascendingLevel = i;
            int descendingLevel = length - 1 - i;
            int level = Mathf.Min(ascendingLevel, descendingLevel);

            heightOffsets[i] = directionMultiplier * stepHeight * level;
        }

        float minOffset = heightOffsets[0];
        float maxOffset = heightOffsets[0];

        for (int i = 1; i < heightOffsets.Length; i++)
        {
            minOffset = Mathf.Min(minOffset, heightOffsets[i]);
            maxOffset = Mathf.Max(maxOffset, heightOffsets[i]);
        }

        float minBaseHeight = settings.minHeight - minOffset;
        float maxBaseHeight = settings.maxHeight - maxOffset;

        float baseHeight;

        if (minBaseHeight <= maxBaseHeight)
        {
            baseHeight = SelectFairHeight(settings, minBaseHeight, maxBaseHeight, false);
        }
        else
        {
            float rangeCenter = (settings.minHeight + settings.maxHeight) * 0.5f;
            float offsetCenter = (minOffset + maxOffset) * 0.5f;
            baseHeight = rangeCenter - offsetCenter;
        }

        float internalDelay = Mathf.Max(
            minimumStaircaseSpawnDelay,
            settings.spawnDelay * staircaseSpawnDelayMultiplier
        );

        DifficultySettings patternSettings = settings;

        patternSettings.passageMotionMode = Pipes.PassageMotionMode.Static;
        patternSettings.centerMoveAmplitude = 0f;
        patternSettings.centerMoveFrequency = 1f;
        patternSettings.maxCenterOffset = 0f;

        patternSettings.gapMotionMode = Pipes.GapMotionMode.Static;
        patternSettings.minimumDynamicGap = patternSettings.verticalGap;
        patternSettings.maximumDynamicGap = patternSettings.verticalGap;
        patternSettings.gapPulseAmplitude = 0f;
        patternSettings.gapPulseFrequency = 1f;

        patternSettings.enableSuddenShift = false;
        patternSettings.suddenShiftChance = 0f;
        patternSettings.maxSuddenShifts = 0;
        patternSettings.suddenShiftAmplitude = 0f;

        for (int i = 0; i < length; i++)
        {
            float height = baseHeight + heightOffsets[i];
            height = Mathf.Clamp(height, settings.minHeight, settings.maxHeight);

            SpawnSinglePipe(patternSettings, height, true);

            if (i < length - 1)
            {
                yield return new WaitForSeconds(internalDelay);
            }
        }

        pipesSinceLastStaircase = 0;

        Debug.Log(
            $"Spawner: создана ступенчатая серия. Тип: {(directionIsUp ? "горка вверх" : "яма вниз")}, длина: {length}, шаг: {stepHeight:F2}.",
            this
        );
    }

    private DifficultySettings GetCurrentDifficultySettings()
    {
        int stage = useScoreBasedDifficulty ? GetDifficultyStageFromScore() : 0;
        DifficultySettings settings = CreateBaseDifficultySettings(stage);

        switch (stage)
        {
            case 0:
                break;

            case 1:
                settings.spawnDelay = spawnRate * 0.94f;
                settings.verticalGap = verticalGap * 0.94f;
                settings.minHeight = minHeight - 0.12f;
                settings.maxHeight = maxHeight + 0.12f;
                settings.speedMultiplier = 1.07f;
                break;

            case 2:
                settings.spawnDelay = spawnRate * 0.90f;
                settings.verticalGap = verticalGap * 0.90f;
                settings.minHeight = minHeight - 0.22f;
                settings.maxHeight = maxHeight + 0.22f;
                settings.speedMultiplier = 1.12f;

                settings.passageMotionMode = Pipes.PassageMotionMode.SmoothSine;
                settings.centerMoveAmplitude = 0.38f;
                settings.centerMoveFrequency = 1.05f;
                settings.maxCenterOffset = 0.56f;
                settings.movingPipeGapBonus = movingPipeGapBonus;
                break;

            case 3:
                settings.spawnDelay = spawnRate * 0.86f;
                settings.verticalGap = verticalGap * 0.87f;
                settings.minHeight = minHeight - 0.30f;
                settings.maxHeight = maxHeight + 0.30f;
                settings.speedMultiplier = 1.17f;

                settings.passageMotionMode = Pipes.PassageMotionMode.SmoothPingPong;
                settings.centerMoveAmplitude = 0.48f;
                settings.centerMoveFrequency = 1.05f;
                settings.maxCenterOffset = 0.68f;
                settings.movingPipeGapBonus = movingPipeGapBonus + 0.03f;

                settings.gapMotionMode = Pipes.GapMotionMode.SmoothPulse;
                settings.minimumDynamicGap = settings.verticalGap - 0.12f;
                settings.maximumDynamicGap = settings.verticalGap + 0.16f;
                settings.gapPulseAmplitude = 0.12f;
                settings.gapPulseFrequency = 1.05f;
                break;

            default:
                settings.spawnDelay = spawnRate * 0.82f;
                settings.verticalGap = verticalGap * 0.84f;
                settings.minHeight = minHeight - 0.36f;
                settings.maxHeight = maxHeight + 0.36f;
                settings.speedMultiplier = 1.22f;

                settings.passageMotionMode = Pipes.PassageMotionMode.SmoothSine;
                settings.centerMoveAmplitude = 0.58f;
                settings.centerMoveFrequency = 1.20f;
                settings.maxCenterOffset = 0.78f;
                settings.movingPipeGapBonus = movingPipeGapBonus + 0.06f;

                settings.gapMotionMode = Pipes.GapMotionMode.SmoothPulse;
                settings.minimumDynamicGap = settings.verticalGap - 0.14f;
                settings.maximumDynamicGap = settings.verticalGap + 0.18f;
                settings.gapPulseAmplitude = 0.14f;
                settings.gapPulseFrequency = 1.12f;

                settings.enableSuddenShift = false;
                settings.suddenShiftChance = 0f;
                settings.maxSuddenShifts = 0;
                settings.suddenShiftAmplitude = 0f;

                settings.allowStaircasePattern = enableStaircasePatterns;
                settings.staircaseChance = 0.16f;
                break;
        }

        ApplySafetyLimits(ref settings);
        return settings;
    }

    private DifficultySettings CreateBaseDifficultySettings(int stage)
    {
        DifficultySettings settings = new DifficultySettings();

        settings.stage = stage;

        settings.spawnDelay = spawnRate;
        settings.verticalGap = verticalGap;
        settings.minHeight = minHeight;
        settings.maxHeight = maxHeight;
        settings.speedMultiplier = 1f;

        settings.passageMotionMode = Pipes.PassageMotionMode.Static;
        settings.centerMoveAmplitude = 0f;
        settings.centerMoveFrequency = 1f;
        settings.maxCenterOffset = 0f;
        settings.movingPipeGapBonus = 0f;

        settings.gapMotionMode = Pipes.GapMotionMode.Static;
        settings.minimumDynamicGap = verticalGap;
        settings.maximumDynamicGap = verticalGap;
        settings.gapPulseAmplitude = 0f;
        settings.gapPulseFrequency = 1f;

        settings.enableSuddenShift = false;
        settings.suddenShiftChance = 0f;
        settings.maxSuddenShifts = 0;
        settings.suddenShiftAmplitude = 0f;
        settings.suddenShiftMinDelay = 0.45f;
        settings.suddenShiftMaxDelay = 1.1f;
        settings.suddenShiftDuration = 0.18f;
        settings.suddenShiftAllowedUntilX = 3.5f;

        settings.allowStaircasePattern = false;
        settings.staircaseChance = 0f;

        return settings;
    }

    private void ApplySafetyLimits(ref DifficultySettings settings)
    {
        settings.spawnDelay = Mathf.Max(GetRecommendedMinimumSpawnDelay(settings.stage), settings.spawnDelay, minimumSpawnDelay);
        settings.verticalGap = Mathf.Max(GetRecommendedMinimumGap(settings.stage), settings.verticalGap, minimumVerticalGap);

        if (settings.maxHeight < settings.minHeight)
        {
            float temporary = settings.minHeight;
            settings.minHeight = settings.maxHeight;
            settings.maxHeight = temporary;
        }

        settings.minHeight = Mathf.Max(settings.minHeight, safeWorldMinCenterY + 0.08f);
        settings.maxHeight = Mathf.Min(settings.maxHeight, safeWorldMaxCenterY - 0.08f);

        if (settings.maxHeight < settings.minHeight)
        {
            float center = (safeWorldMinCenterY + safeWorldMaxCenterY) * 0.5f;
            settings.minHeight = center - 0.5f;
            settings.maxHeight = center + 0.5f;
        }

        if (settings.gapMotionMode == Pipes.GapMotionMode.Static)
        {
            settings.minimumDynamicGap = settings.verticalGap;
            settings.maximumDynamicGap = settings.verticalGap;
            settings.gapPulseAmplitude = 0f;
        }
        else
        {
            settings.minimumDynamicGap = Mathf.Max(
                minimumDynamicGap,
                settings.verticalGap - 0.18f,
                settings.minimumDynamicGap
            );

            settings.maximumDynamicGap = Mathf.Max(
                settings.minimumDynamicGap,
                settings.maximumDynamicGap
            );

            settings.gapPulseAmplitude = Mathf.Min(settings.gapPulseAmplitude, 0.16f);
        }

        if (IsMovingPipe(settings))
        {
            settings.centerMoveAmplitude = Mathf.Min(settings.centerMoveAmplitude, 0.62f);
            settings.maxCenterOffset = Mathf.Min(settings.maxCenterOffset, 0.82f);
            settings.centerMoveFrequency = Mathf.Min(settings.centerMoveFrequency, 1.25f);

            settings.enableSuddenShift = false;
            settings.suddenShiftChance = 0f;
            settings.maxSuddenShifts = 0;
            settings.suddenShiftAmplitude = 0f;
        }
    }

    private float GetRecommendedMinimumSpawnDelay(int stage)
    {
        if (stage <= 0) return 0.95f;
        if (stage == 1) return 0.90f;
        if (stage == 2) return 0.86f;
        if (stage == 3) return 0.82f;

        return 0.80f;
    }

    private float GetRecommendedMinimumGap(int stage)
    {
        if (stage <= 0) return 2.80f;
        if (stage == 1) return 2.70f;
        if (stage == 2) return 2.58f;
        if (stage == 3) return 2.45f;

        return 2.36f;
    }

    private float SelectFairHeight(DifficultySettings settings, float requestedMin, float requestedMax, bool isMovingPipe)
    {
        float min = Mathf.Min(requestedMin, requestedMax);
        float max = Mathf.Max(requestedMin, requestedMax);

        if (!useFairSpawnDirector)
        {
            return SafeRandomRange(min, max);
        }

        if (isMovingPipe)
        {
            float paddedMin = min + movingPipeHeightPadding;
            float paddedMax = max - movingPipeHeightPadding;

            if (paddedMin <= paddedMax)
            {
                min = paddedMin;
                max = paddedMax;
            }
        }

        if (hasLastSpawnHeight)
        {
            float maxDelta = GetMaxHeightDelta(settings.stage, isMovingPipe);
            min = Mathf.Max(min, lastSpawnHeight - maxDelta);
            max = Mathf.Min(max, lastSpawnHeight + maxDelta);
        }

        if (max < min)
        {
            float fallback = hasLastSpawnHeight
                ? Mathf.Clamp(lastSpawnHeight, requestedMin, requestedMax)
                : (requestedMin + requestedMax) * 0.5f;

            return fallback;
        }

        float candidate = SafeRandomRange(min, max);

        if (hasLastSpawnHeight)
        {
            candidate = PreventOppositeExtremeJump(candidate, settings, isMovingPipe);
        }

        return Mathf.Clamp(candidate, requestedMin, requestedMax);
    }

    private float ClampHeightToFairRange(DifficultySettings settings, float height, bool isMovingPipe)
    {
        float min = settings.minHeight;
        float max = settings.maxHeight;

        if (isMovingPipe)
        {
            float paddedMin = min + movingPipeHeightPadding;
            float paddedMax = max - movingPipeHeightPadding;

            if (paddedMin <= paddedMax)
            {
                min = paddedMin;
                max = paddedMax;
            }
        }

        if (hasLastSpawnHeight && useFairSpawnDirector)
        {
            float maxDelta = GetMaxHeightDelta(settings.stage, isMovingPipe);
            min = Mathf.Max(min, lastSpawnHeight - maxDelta);
            max = Mathf.Min(max, lastSpawnHeight + maxDelta);
        }

        if (max < min)
        {
            return Mathf.Clamp(height, settings.minHeight, settings.maxHeight);
        }

        return Mathf.Clamp(height, min, max);
    }

    private float PreventOppositeExtremeJump(float candidate, DifficultySettings settings, bool isMovingPipe)
    {
        float range = Mathf.Max(0.01f, settings.maxHeight - settings.minHeight);
        float lowerExtreme = settings.minHeight + range * 0.24f;
        float upperExtreme = settings.maxHeight - range * 0.24f;
        float center = (settings.minHeight + settings.maxHeight) * 0.5f;
        float centerBand = isMovingPipe ? range * 0.14f : range * 0.20f;

        bool lastWasLow = lastSpawnHeight <= lowerExtreme;
        bool lastWasHigh = lastSpawnHeight >= upperExtreme;

        if (lastWasLow && candidate >= upperExtreme)
        {
            return SafeRandomRange(center - centerBand, center + centerBand);
        }

        if (lastWasHigh && candidate <= lowerExtreme)
        {
            return SafeRandomRange(center - centerBand, center + centerBand);
        }

        return candidate;
    }

    private float GetMaxHeightDelta(int stage, bool isMovingPipe)
    {
        float maxDelta;

        if (stage <= 0)
        {
            maxDelta = 1.05f;
        }
        else if (stage == 1)
        {
            maxDelta = 1.14f;
        }
        else if (stage == 2)
        {
            maxDelta = 1.24f;
        }
        else if (stage == 3)
        {
            maxDelta = 1.34f;
        }
        else
        {
            maxDelta = 1.42f;
        }

        if (isMovingPipe)
        {
            maxDelta -= 0.16f;
        }

        return Mathf.Max(0.85f, maxDelta);
    }

    private void RegisterSpawnHeight(float height)
    {
        lastSpawnHeight = height;
        hasLastSpawnHeight = true;

        pipesSinceLastStaircase++;
        pipesSinceLastPortalSegment++;
    }

    private bool IsMovingPipe(DifficultySettings settings)
    {
        return settings.passageMotionMode != Pipes.PassageMotionMode.Static;
    }

    private int GetDifficultyStageFromScore()
    {
        int safeStep = Mathf.Max(20, pointsPerDifficultyStage);
        int currentScore = 0;

        if (GameManager.Instance != null)
        {
            currentScore = GameManager.Instance.score;
        }

        return Mathf.Max(0, currentScore / safeStep);
    }

    private bool ShouldSpawnStaircase(DifficultySettings settings)
    {
        if (portalSegmentRunning)
        {
            return false;
        }

        if (!settings.allowStaircasePattern)
        {
            return false;
        }

        if (pipesSinceLastStaircase < minimumPipesBetweenStaircases)
        {
            return false;
        }

        return Random.value <= settings.staircaseChance;
    }

    private float GetPipeSpeed(Pipes prefab, DifficultySettings settings)
    {
        float baseSpeed = prefab != null ? prefab.speed : 5f;
        return Mathf.Max(0.1f, baseSpeed * settings.speedMultiplier);
    }

    private bool GetCurrentDayNightVisualState()
    {
        if (useSkySwitcherStateWhenAvailable && skySwitcher != null)
        {
            return skySwitcher.IsDay;
        }

        return isDay;
    }

    private Pipes GetSelectedPrefab(bool visualIsDay)
    {
        Pipes preferredPrefab = visualIsDay ? dayPipesPrefab : nightPipesPrefab;

        if (preferredPrefab != null)
        {
            return preferredPrefab;
        }

        return visualIsDay ? nightPipesPrefab : dayPipesPrefab;
    }

    private float SafeRandomRange(float min, float max)
    {
        if (max < min)
        {
            float temporary = min;
            min = max;
            max = temporary;
        }

        if (Mathf.Approximately(min, max))
        {
            return min;
        }

        return Random.Range(min, max);
    }

    private void LogDifficultyStageIfNeeded(int stage)
    {
        if (!logDifficultyStageChanges)
        {
            return;
        }

        if (stage == lastLoggedDifficultyStage)
        {
            return;
        }

        lastLoggedDifficultyStage = stage;

        int currentScore = GameManager.Instance != null ? GameManager.Instance.score : 0;
        int safeStep = Mathf.Max(20, pointsPerDifficultyStage);
        int fromScore = stage * safeStep;
        int toScore = fromScore + safeStep - 1;

        Debug.Log($"Spawner: активен этап сложности {stage}. Score: {currentScore}. Диапазон этапа: {fromScore}–{toScore}.", this);
    }

    private struct DifficultySettings
    {
        public int stage;

        public float spawnDelay;
        public float verticalGap;
        public float minHeight;
        public float maxHeight;
        public float speedMultiplier;

        public Pipes.PassageMotionMode passageMotionMode;
        public float centerMoveAmplitude;
        public float centerMoveFrequency;
        public float maxCenterOffset;
        public float movingPipeGapBonus;

        public Pipes.GapMotionMode gapMotionMode;
        public float minimumDynamicGap;
        public float maximumDynamicGap;
        public float gapPulseAmplitude;
        public float gapPulseFrequency;

        public bool enableSuddenShift;
        public float suddenShiftChance;
        public int maxSuddenShifts;
        public float suddenShiftAmplitude;
        public float suddenShiftMinDelay;
        public float suddenShiftMaxDelay;
        public float suddenShiftDuration;
        public float suddenShiftAllowedUntilX;

        public bool allowStaircasePattern;
        public float staircaseChance;
    }
}