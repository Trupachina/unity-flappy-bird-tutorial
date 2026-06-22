using System.Collections;
using UnityEngine;

/// <summary>
/// Создаёт трубы и управляет прогрессивным усложнением игры.
/// 
/// Сложность повышается по score.
/// В твоём проекте score увеличивается при пролёте зоны Scoring,
/// поэтому score фактически равен количеству успешных пролётов труб.
/// 
/// GameManager менять не нужно:
/// Spawner читает GameManager.Instance.score напрямую.
/// </summary>
public class Spawner : MonoBehaviour
{
    [Header("Префабы труб")]
    [Tooltip("Префаб дневных труб из рабочей версии.")]
    public Pipes dayPipesPrefab;

    [Tooltip("Префаб ночных труб из рабочей версии.")]
    public Pipes nightPipesPrefab;

    [Header("Базовый спавн")]
    [Tooltip("Базовая пауза между появлением труб. На первых 20 пролётах используется почти как есть.")]
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

    [Tooltip("Через сколько очков повышается этап сложности. Рекомендуемое значение — 20.")]
    public int pointsPerDifficultyStage = 20;

    [Tooltip("Минимально допустимый статический gap, ниже которого обычные трубы не сужаются.")]
    public float minimumVerticalGap = 1.8f;

    [Tooltip("Минимальный gap во время динамического сужения/расширения.")]
    public float minimumDynamicGap = 1.65f;

    [Tooltip("Минимально допустимая пауза между обычными трубами.")]
    public float minimumSpawnDelay = 0.55f;

    [Tooltip("Показывать в консоли смену этапов сложности.")]
    public bool logDifficultyStageChanges = true;

    [Header("Ступенчатые серии поздней игры")]
    [Tooltip("Разрешить короткие серии близких труб в форме ступенек на позднем этапе.")]
    public bool enableStaircasePatterns = true;

    [Tooltip("Минимальное количество труб в ступенчатой серии.")]
    public int minStaircaseLength = 3;

    [Tooltip("Максимальное количество труб в ступенчатой серии.")]
    public int maxStaircaseLength = 4;

    [Tooltip("Вертикальный шаг между соседними трубами в ступенчатой серии.")]
    public float staircaseStepHeight = 0.65f;

    [Tooltip("Во сколько раз промежуток внутри ступенчатой серии короче обычного spawnRate текущего этапа.")]
    public float staircaseSpawnDelayMultiplier = 0.62f;

    [Tooltip("Минимальная пауза между трубами внутри ступенчатой серии.")]
    public float minimumStaircaseSpawnDelay = 0.35f;

    [Tooltip("После ступенчатой серии добавляется небольшая пауза, чтобы серия не слипалась со следующей трубой.")]
    public float staircaseRecoveryDelayMultiplier = 0.75f;

    private bool isDay = true;
    private float switchTimer;

    private Coroutine spawnLoopCoroutine;
    private int lastLoggedDifficultyStage = -1;

    private void Awake()
    {
        if (skySwitcher == null)
        {
            skySwitcher = FindObjectOfType<SkySwitcher>();
        }
    }

    private void OnEnable()
    {
        if (spawnLoopCoroutine != null)
        {
            StopCoroutine(spawnLoopCoroutine);
        }

        spawnLoopCoroutine = StartCoroutine(SpawnLoop());
    }

    private void OnDisable()
    {
        if (spawnLoopCoroutine != null)
        {
            StopCoroutine(spawnLoopCoroutine);
            spawnLoopCoroutine = null;
        }
    }

    private void Update()
    {
        UpdateFallbackDayNightTimer();
    }

    /// <summary>
    /// Основной цикл создания труб.
    /// Coroutine используется вместо InvokeRepeating, потому что задержка между трубами
    /// теперь меняется в зависимости от этапа сложности.
    /// </summary>
    private IEnumerator SpawnLoop()
    {
        while (true)
        {
            DifficultySettings settings = GetCurrentDifficultySettings();

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
                SpawnSinglePipe(settings, SafeRandomRange(settings.minHeight, settings.maxHeight));
            }
        }
    }

    /// <summary>
    /// Старый таймер дня/ночи оставлен как запасной режим.
    /// Если есть SkySwitcher, визуал труб берётся из него.
    /// </summary>
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

    /// <summary>
    /// Создаёт одну трубу с параметрами текущего этапа сложности.
    /// </summary>
    private Pipes SpawnSinglePipe(DifficultySettings settings, float verticalOffset)
    {
        bool visualIsDay = GetCurrentDayNightVisualState();
        Pipes selectedPrefab = GetSelectedPrefab(visualIsDay);

        if (selectedPrefab == null)
        {
            Debug.LogError("Spawner: не назначен dayPipesPrefab или nightPipesPrefab.", this);
            return null;
        }

        Vector3 spawnPosition = transform.position + Vector3.up * verticalOffset;
        Pipes pipes = Instantiate(selectedPrefab, spawnPosition, Quaternion.identity);

        pipes.Configure(settings.verticalGap, skySwitcher);
        pipes.SetSprite(visualIsDay);

        bool allowSuddenShiftForThisPipe =
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
            newMinimumDynamicGap: settings.minimumDynamicGap,
            newMaximumDynamicGap: settings.maximumDynamicGap,
            newGapPulseAmplitude: settings.gapPulseAmplitude,
            newGapPulseFrequency: settings.gapPulseFrequency,
            newGapPulsePhase: Random.Range(0f, Mathf.PI * 2f),

            allowSuddenShift: allowSuddenShiftForThisPipe,
            suddenMaxAmplitude: settings.suddenShiftAmplitude,
            suddenMinDelay: settings.suddenShiftMinDelay,
            suddenMaxDelay: settings.suddenShiftMaxDelay,
            maxSuddenShifts: settings.maxSuddenShifts,
            suddenLerpDuration: settings.suddenShiftDuration,
            allowSuddenShiftUntilX: settings.suddenShiftAllowedUntilX
        );

        return pipes;
    }

    /// <summary>
    /// Создаёт серию труб в форме ступенек.
    ///
    /// Варианты серии:
    /// 0 — ступеньки вверх;
    /// 1 — ступеньки вниз;
    /// 2 — смешанный паттерн вверх+вниз или вниз+вверх.
    ///
    /// Важно:
    /// ступенчатая серия специально делается без вертикального движения,
    /// без изменения gap и без резких сдвигов.
    /// Сложность создаётся только формой серии.
    /// </summary>
    private IEnumerator SpawnStaircasePattern(DifficultySettings settings)
    {
        int minLength = Mathf.Max(3, minStaircaseLength);
        int maxLength = Mathf.Max(minLength, maxStaircaseLength);
        int length = Random.Range(minLength, maxLength + 1);

        float stepHeight = Mathf.Max(0.1f, staircaseStepHeight);

        // 0 — вверх, 1 — вниз, 2 — вверх+вниз / вниз+вверх.
        int patternType = Random.Range(0, 3);

        float[] heightOffsets = new float[length];

        if (patternType == 0)
        {
            // Ступеньки вверх:
            // 0, +step, +2step, +3step...
            for (int i = 0; i < length; i++)
            {
                heightOffsets[i] = stepHeight * i;
            }
        }
        else if (patternType == 1)
        {
            // Ступеньки вниз:
            // 0, -step, -2step, -3step...
            for (int i = 0; i < length; i++)
            {
                heightOffsets[i] = -stepHeight * i;
            }
        }
        else
        {
            // Смешанный паттерн:
            // например: 0, +step, +2step, +step, 0
            // или:      0, -step, -2step, -step, 0
            bool firstDirectionIsUp = Random.value < 0.5f;
            int peakIndex = Mathf.Max(1, length / 2);

            for (int i = 0; i < length; i++)
            {
                int level;

                if (i <= peakIndex)
                {
                    level = i;
                }
                else
                {
                    level = peakIndex - (i - peakIndex);
                }

                level = Mathf.Max(0, level);

                float signedStep = firstDirectionIsUp ? stepHeight : -stepHeight;
                heightOffsets[i] = signedStep * level;
            }
        }

        float minOffset = heightOffsets[0];
        float maxOffset = heightOffsets[0];

        for (int i = 1; i < heightOffsets.Length; i++)
        {
            minOffset = Mathf.Min(minOffset, heightOffsets[i]);
            maxOffset = Mathf.Max(maxOffset, heightOffsets[i]);
        }

        // Подбираем базовую высоту так, чтобы вся серия по возможности помещалась
        // в допустимый диапазон minHeight/maxHeight текущей сложности.
        float minBaseHeight = settings.minHeight - minOffset;
        float maxBaseHeight = settings.maxHeight - maxOffset;

        float baseHeight;

        if (minBaseHeight <= maxBaseHeight)
        {
            baseHeight = SafeRandomRange(minBaseHeight, maxBaseHeight);
        }
        else
        {
            // Запасной вариант для слишком большого паттерна:
            // центрируем серию и затем дополнительно clamp-им каждую трубу.
            float rangeCenter = (settings.minHeight + settings.maxHeight) * 0.5f;
            float offsetCenter = (minOffset + maxOffset) * 0.5f;
            baseHeight = rangeCenter - offsetCenter;
        }

        float internalDelay = Mathf.Max(
            minimumStaircaseSpawnDelay,
            settings.spawnDelay * staircaseSpawnDelayMultiplier
        );

        DifficultySettings patternSettings = settings;

        // Ступенчатая серия должна быть статичной.
        patternSettings.passageMotionMode = Pipes.PassageMotionMode.Static;
        patternSettings.centerMoveAmplitude = 0f;
        patternSettings.centerMoveFrequency = 1f;
        patternSettings.maxCenterOffset = 0f;

        // Gap в ступенчатой серии не должен пульсировать.
        patternSettings.gapMotionMode = Pipes.GapMotionMode.Static;
        patternSettings.minimumDynamicGap = patternSettings.verticalGap;
        patternSettings.maximumDynamicGap = patternSettings.verticalGap;
        patternSettings.gapPulseAmplitude = 0f;
        patternSettings.gapPulseFrequency = 1f;

        // Резкие сдвиги в ступенчатой серии отключены.
        patternSettings.enableSuddenShift = false;
        patternSettings.suddenShiftChance = 0f;
        patternSettings.maxSuddenShifts = 0;
        patternSettings.suddenShiftAmplitude = 0f;

        for (int i = 0; i < length; i++)
        {
            float height = baseHeight + heightOffsets[i];
            height = Mathf.Clamp(height, settings.minHeight, settings.maxHeight);

            SpawnSinglePipe(patternSettings, height);

            if (i < length - 1)
            {
                yield return new WaitForSeconds(internalDelay);
            }
        }
    }

    /// <summary>
    /// Возвращает настройки текущей сложности по score.
    /// Этап меняется каждые pointsPerDifficultyStage очков.
    /// </summary>
    private DifficultySettings GetCurrentDifficultySettings()
    {
        int stage = useScoreBasedDifficulty ? GetDifficultyStageFromScore() : 0;
        DifficultySettings settings = CreateBaseDifficultySettings(stage);

        switch (stage)
        {
            case 0:
                // 0–19 очков: базовая рабочая версия.
                break;

            case 1:
                // 20–39 очков: немного выше скорость, плотнее трубы, уже проход, шире разброс высоты.
                settings.spawnDelay = spawnRate * 0.92f;
                settings.verticalGap = verticalGap * 0.92f;
                settings.minHeight = minHeight - 0.15f;
                settings.maxHeight = maxHeight + 0.15f;
                settings.speedMultiplier = 1.08f;
                break;

            case 2:
                // 40–59 очков: проход начинает плавно двигаться вверх-вниз.
                settings.spawnDelay = spawnRate * 0.86f;
                settings.verticalGap = verticalGap * 0.86f;
                settings.minHeight = minHeight - 0.35f;
                settings.maxHeight = maxHeight + 0.35f;
                settings.speedMultiplier = 1.14f;

                settings.passageMotionMode = Pipes.PassageMotionMode.SmoothSine;
                settings.centerMoveAmplitude = 0.35f;
                settings.centerMoveFrequency = 1.15f;
                settings.maxCenterOffset = 0.55f;
                break;

            case 3:
                // 60–79 очков: проход двигается иначе, gap начинает сужаться/расширяться,
                // часть труб резко меняет положение прохода.
                settings.spawnDelay = spawnRate * 0.80f;
                settings.verticalGap = verticalGap * 0.80f;
                settings.minHeight = minHeight - 0.50f;
                settings.maxHeight = maxHeight + 0.50f;
                settings.speedMultiplier = 1.20f;

                settings.passageMotionMode = Pipes.PassageMotionMode.SmoothPingPong;
                settings.centerMoveAmplitude = 0.45f;
                settings.centerMoveFrequency = 0.95f;
                settings.maxCenterOffset = 0.75f;

                settings.gapMotionMode = Pipes.GapMotionMode.SmoothPulse;
                settings.minimumDynamicGap = Mathf.Max(minimumDynamicGap, settings.verticalGap - 0.22f);
                settings.maximumDynamicGap = settings.verticalGap + 0.15f;
                settings.gapPulseAmplitude = 0.20f;
                settings.gapPulseFrequency = 1.20f;

                settings.enableSuddenShift = true;
                settings.suddenShiftChance = 0.38f;
                settings.maxSuddenShifts = 1;
                settings.suddenShiftAmplitude = 0.42f;
                settings.suddenShiftMinDelay = 0.35f;
                settings.suddenShiftMaxDelay = 0.90f;
                settings.suddenShiftDuration = 0.10f;
                settings.suddenShiftAllowedUntilX = 1.75f;
                break;

            default:
                // 80+ очков: поздняя игра.
                // Обычные трубы могут двигаться, менять gap и иногда резко смещать проход.
                // Иногда появляются статичные ступенчатые серии.
                settings.spawnDelay = spawnRate * 0.74f;
                settings.verticalGap = verticalGap * 0.76f;
                settings.minHeight = minHeight - 0.65f;
                settings.maxHeight = maxHeight + 0.65f;
                settings.speedMultiplier = 1.25f;

                settings.passageMotionMode = Pipes.PassageMotionMode.SmoothSine;
                settings.centerMoveAmplitude = 0.55f;
                settings.centerMoveFrequency = 1.45f;
                settings.maxCenterOffset = 0.90f;

                settings.gapMotionMode = Pipes.GapMotionMode.SmoothPulse;
                settings.minimumDynamicGap = Mathf.Max(minimumDynamicGap, settings.verticalGap - 0.30f);
                settings.maximumDynamicGap = settings.verticalGap + 0.20f;
                settings.gapPulseAmplitude = 0.26f;
                settings.gapPulseFrequency = 1.35f;

                settings.enableSuddenShift = true;
                settings.suddenShiftChance = 0.48f;
                settings.maxSuddenShifts = 1;
                settings.suddenShiftAmplitude = 0.52f;
                settings.suddenShiftMinDelay = 0.30f;
                settings.suddenShiftMaxDelay = 0.80f;
                settings.suddenShiftDuration = 0.09f;
                settings.suddenShiftAllowedUntilX = 1.90f;

                settings.allowStaircasePattern = enableStaircasePatterns;
                settings.staircaseChance = 0.26f;
                break;
        }

        settings.spawnDelay = Mathf.Max(minimumSpawnDelay, settings.spawnDelay);
        settings.verticalGap = Mathf.Max(minimumVerticalGap, settings.verticalGap);

        if (settings.gapMotionMode == Pipes.GapMotionMode.Static)
        {
            settings.minimumDynamicGap = settings.verticalGap;
            settings.maximumDynamicGap = settings.verticalGap;
            settings.gapPulseAmplitude = 0f;
        }
        else
        {
            settings.minimumDynamicGap = Mathf.Max(0.1f, settings.minimumDynamicGap);
            settings.maximumDynamicGap = Mathf.Max(settings.minimumDynamicGap, settings.maximumDynamicGap);
        }

        if (settings.maxHeight < settings.minHeight)
        {
            float temporary = settings.minHeight;
            settings.minHeight = settings.maxHeight;
            settings.maxHeight = temporary;
        }

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

        settings.gapMotionMode = Pipes.GapMotionMode.Static;
        settings.minimumDynamicGap = verticalGap;
        settings.maximumDynamicGap = verticalGap;
        settings.gapPulseAmplitude = 0f;
        settings.gapPulseFrequency = 1f;

        settings.enableSuddenShift = false;
        settings.suddenShiftChance = 0f;
        settings.maxSuddenShifts = 0;
        settings.suddenShiftAmplitude = 0f;
        settings.suddenShiftMinDelay = 0.35f;
        settings.suddenShiftMaxDelay = 0.90f;
        settings.suddenShiftDuration = 0.10f;
        settings.suddenShiftAllowedUntilX = 1.75f;

        settings.allowStaircasePattern = false;
        settings.staircaseChance = 0f;

        return settings;
    }

    /// <summary>
    /// Вычисляет этап сложности из GameManager.Instance.score.
    /// GameManager менять не нужно.
    /// </summary>
    private int GetDifficultyStageFromScore()
    {
        int safeStep = Mathf.Max(1, pointsPerDifficultyStage);
        int currentScore = 0;

        if (GameManager.Instance != null)
        {
            currentScore = GameManager.Instance.score;
        }

        return Mathf.Max(0, currentScore / safeStep);
    }

    private bool ShouldSpawnStaircase(DifficultySettings settings)
    {
        if (!settings.allowStaircasePattern)
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

    private float GetSafeStaircaseStartHeight(DifficultySettings settings, int direction, float totalHeight)
    {
        if (direction > 0)
        {
            float maxStart = settings.maxHeight - totalHeight;

            if (maxStart >= settings.minHeight)
            {
                return SafeRandomRange(settings.minHeight, maxStart);
            }
        }
        else
        {
            float minStart = settings.minHeight + totalHeight;

            if (minStart <= settings.maxHeight)
            {
                return SafeRandomRange(minStart, settings.maxHeight);
            }
        }

        return SafeRandomRange(settings.minHeight, settings.maxHeight);
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
        Debug.Log($"Spawner: активен этап сложности {stage}. Score: {currentScore}", this);
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