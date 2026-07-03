using System.Collections;
using UnityEngine;

/// <summary>
/// Спавнер стен из блоков.
/// Работает поверх оригинального GameManager.
/// Не требует отдельного BlockShooterGameManager.
/// </summary>
[DisallowMultipleComponent]
public class BlockWallSpawner : MonoBehaviour
{
    [Header("Префаб блока")]
    [SerializeField] private DamageableBlock blockPrefab;

    [Header("Запуск")]
    [Tooltip("Для работы со старым GameManager лучше включить. Пока игра на паузе, стены всё равно не будут появляться.")]
    [SerializeField] private bool spawnAutomatically = true;

    [SerializeField] private float initialDelay = 1.2f;

    [Header("Позиция стены")]
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private float wallCenterY = 0.4f;
    [SerializeField] private float randomCenterYOffset = 0.25f;
    [SerializeField] private float destroyX = -12f;

    [Header("Сетка блоков")]
    [SerializeField] private int rowCount = 5;
    [SerializeField] private float blockSpacingY = 1.85f;

    [Header("Сложность по очкам")]
    [Tooltip("Через сколько очков повышается этап сложности.")]
    [SerializeField] private int pointsPerDifficultyStage = 5;

    [Header("Движение стены")]
    [SerializeField] private float baseWallSpeed = 3.5f;
    [SerializeField] private float speedIncreasePerStage = 0.25f;
    [SerializeField] private float maximumWallSpeed = 6.0f;

    [Header("Частота стен")]
    [SerializeField] private float baseSpawnInterval = 2.4f;
    [SerializeField] private float spawnIntervalDecreasePerStage = 0.12f;
    [SerializeField] private float minimumSpawnInterval = 1.35f;

    [Header("HP блоков")]
    [SerializeField] private int minimumBlockHealth = 1;
    [SerializeField] private int stage0MaximumHealth = 3;
    [SerializeField] private int healthIncreasePerStage = 1;
    [SerializeField] private int absoluteMaximumHealth = 12;

    [Header("Гарантированный слабый путь")]
    [SerializeField] private bool useGuaranteedWeakLane = true;
    [SerializeField] private int weakLaneMinimumHealth = 1;
    [SerializeField] private int weakLaneMaximumHealth = 2;
    [SerializeField] private bool softenNeighbourLanes = true;
    [SerializeField] private int neighbourLaneMaximumHealth = 3;

    [Header("Условия спавна")]
    [SerializeField] private bool requirePlayerEnabled = true;
    [SerializeField] private bool stopWhenTimeScaleZero = true;

    [Header("Диагностика")]
    [SerializeField] private bool logDebug = false;

    private Coroutine spawnRoutine;
    private Player player;

    private void Awake()
    {
        player = FindObjectOfType<Player>();
    }

    private void OnEnable()
    {
        if (spawnAutomatically)
        {
            StartSpawning();
        }
    }

    private void OnDisable()
    {
        StopSpawning();
    }

    public void StartSpawning()
    {
        if (spawnRoutine != null)
        {
            StopCoroutine(spawnRoutine);
        }

        spawnRoutine = StartCoroutine(SpawnRoutine());
    }

    public void StopSpawning()
    {
        if (spawnRoutine != null)
        {
            StopCoroutine(spawnRoutine);
            spawnRoutine = null;
        }
    }

    private IEnumerator SpawnRoutine()
    {
        if (initialDelay > 0f)
        {
            yield return new WaitForSeconds(initialDelay);
        }

        while (true)
        {
            if (!CanSpawn())
            {
                yield return null;
                continue;
            }

            SpawnWall();

            float interval = GetCurrentSpawnInterval();

            if (logDebug)
            {
                Debug.Log($"BlockWallSpawner: следующая стена через {interval:F2} сек. Stage={GetDifficultyStage()}.");
            }

            yield return new WaitForSeconds(interval);
        }
    }

    private bool CanSpawn()
    {
        if (blockPrefab == null)
        {
            return false;
        }

        if (GameManager.Instance == null)
        {
            return false;
        }

        if (stopWhenTimeScaleZero && Time.timeScale <= 0.0001f)
        {
            return false;
        }

        if (requirePlayerEnabled)
        {
            if (player == null)
            {
                player = FindObjectOfType<Player>();
            }

            if (player == null || !player.enabled)
            {
                return false;
            }
        }

        return true;
    }

    private void SpawnWall()
    {
        int stage = GetDifficultyStage();

        Vector3 rootPosition = spawnPoint != null ? spawnPoint.position : transform.position;
        rootPosition.y = 0f;

        GameObject wallObject = new GameObject($"BlockWall_Stage_{stage}");
        wallObject.transform.position = rootPosition;
        wallObject.transform.rotation = Quaternion.identity;
        wallObject.transform.localScale = Vector3.one;

        BlockWallMover mover = wallObject.AddComponent<BlockWallMover>();
        mover.Initialize(
            newSpeed: GetCurrentWallSpeed(stage),
            newDestroyX: destroyX,
            newStopWhenTimeScaleZero: stopWhenTimeScaleZero
        );

        int safeRowCount = Mathf.Max(1, rowCount);
        int weakLaneIndex = useGuaranteedWeakLane ? Random.Range(0, safeRowCount) : -1;

        float centerY = wallCenterY + Random.Range(-randomCenterYOffset, randomCenterYOffset);
        float firstLaneOffset = -(safeRowCount - 1) * 0.5f * blockSpacingY;

        for (int rowIndex = 0; rowIndex < safeRowCount; rowIndex++)
        {
            float y = centerY + firstLaneOffset + rowIndex * blockSpacingY;
            Vector3 blockPosition = new Vector3(rootPosition.x, y, rootPosition.z);

            int health = GenerateHealthForLane(rowIndex, weakLaneIndex, stage);

            DamageableBlock block = Instantiate(
                blockPrefab,
                blockPosition,
                Quaternion.identity,
                wallObject.transform
            );

            block.Initialize(health);
            block.name = $"DamageableBlock_HP_{health}_Row_{rowIndex}";
        }
    }

    private int GenerateHealthForLane(int rowIndex, int weakLaneIndex, int stage)
    {
        int currentMaximumHealth = GetCurrentMaximumHealth(stage);

        if (useGuaranteedWeakLane && rowIndex == weakLaneIndex)
        {
            int min = Mathf.Clamp(weakLaneMinimumHealth, 1, currentMaximumHealth);
            int max = Mathf.Clamp(weakLaneMaximumHealth, min, currentMaximumHealth);

            return Random.Range(min, max + 1);
        }

        if (useGuaranteedWeakLane &&
            softenNeighbourLanes &&
            Mathf.Abs(rowIndex - weakLaneIndex) == 1)
        {
            int maxNeighbourHealth = Mathf.Min(
                currentMaximumHealth,
                neighbourLaneMaximumHealth + Mathf.Max(0, stage / 2)
            );

            maxNeighbourHealth = Mathf.Max(minimumBlockHealth, maxNeighbourHealth);

            return Random.Range(minimumBlockHealth, maxNeighbourHealth + 1);
        }

        return Random.Range(minimumBlockHealth, currentMaximumHealth + 1);
    }

    private int GetCurrentMaximumHealth(int stage)
    {
        int maximumHealth = stage0MaximumHealth + stage * healthIncreasePerStage;
        return Mathf.Clamp(maximumHealth, minimumBlockHealth, absoluteMaximumHealth);
    }

    private float GetCurrentWallSpeed(int stage)
    {
        float speed = baseWallSpeed + stage * speedIncreasePerStage;
        return Mathf.Clamp(speed, 0.1f, maximumWallSpeed);
    }

    private float GetCurrentSpawnInterval()
    {
        int stage = GetDifficultyStage();
        float interval = baseSpawnInterval - stage * spawnIntervalDecreasePerStage;
        return Mathf.Max(minimumSpawnInterval, interval);
    }

    private int GetDifficultyStage()
    {
        if (GameManager.Instance == null)
        {
            return 0;
        }

        int safeStep = Mathf.Max(1, pointsPerDifficultyStage);
        return Mathf.Max(0, GameManager.Instance.score / safeStep);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        initialDelay = Mathf.Max(0f, initialDelay);

        rowCount = Mathf.Clamp(rowCount, 1, 12);
        blockSpacingY = Mathf.Max(0.1f, blockSpacingY);
        randomCenterYOffset = Mathf.Max(0f, randomCenterYOffset);

        pointsPerDifficultyStage = Mathf.Max(1, pointsPerDifficultyStage);

        baseWallSpeed = Mathf.Max(0.1f, baseWallSpeed);
        speedIncreasePerStage = Mathf.Max(0f, speedIncreasePerStage);
        maximumWallSpeed = Mathf.Max(baseWallSpeed, maximumWallSpeed);

        baseSpawnInterval = Mathf.Max(0.1f, baseSpawnInterval);
        spawnIntervalDecreasePerStage = Mathf.Max(0f, spawnIntervalDecreasePerStage);
        minimumSpawnInterval = Mathf.Max(0.1f, minimumSpawnInterval);

        minimumBlockHealth = Mathf.Max(1, minimumBlockHealth);
        stage0MaximumHealth = Mathf.Max(minimumBlockHealth, stage0MaximumHealth);
        healthIncreasePerStage = Mathf.Max(0, healthIncreasePerStage);
        absoluteMaximumHealth = Mathf.Max(stage0MaximumHealth, absoluteMaximumHealth);

        weakLaneMinimumHealth = Mathf.Max(1, weakLaneMinimumHealth);
        weakLaneMaximumHealth = Mathf.Max(weakLaneMinimumHealth, weakLaneMaximumHealth);
        neighbourLaneMaximumHealth = Mathf.Max(minimumBlockHealth, neighbourLaneMaximumHealth);
    }
#endif
}