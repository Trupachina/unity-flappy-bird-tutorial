using UnityEngine;

/// <summary>
/// Автоматическая стрельба игрока.
/// Работает с оригинальным GameManager.
/// Скорость стрельбы растёт по score / pointsPerDifficultyStage.
/// </summary>
[DisallowMultipleComponent]
public class PlayerWeaponController : MonoBehaviour
{
    [Header("Префаб пули")]
    [SerializeField] private Bullet bulletPrefab;

    [Header("Точка выстрела")]
    [SerializeField] private Transform muzzlePoint;
    [SerializeField] private bool createMuzzlePointIfMissing = true;
    [SerializeField] private Vector3 fallbackMuzzleLocalOffset = new Vector3(0.55f, 0f, 0f);

    [Header("Базовая стрельба")]
    [SerializeField] private float baseFireInterval = 0.22f;
    [SerializeField] private float bulletSpeed = 10f;
    [SerializeField] private int bulletDamage = 1;

    [Header("Рост скорости стрельбы")]
    [SerializeField] private bool useStageBasedFireRate = true;
    [SerializeField] private int pointsPerDifficultyStage = 5;
    [SerializeField] private float fireIntervalDecreasePerStage = 0.015f;
    [SerializeField] private float minimumFireInterval = 0.11f;

    [Header("Мультивыстрел на будущее")]
    [SerializeField] private int bulletsPerShot = 1;
    [SerializeField] private float multiShotVerticalSpacing = 0.22f;
    [SerializeField] private float spreadAngle = 0f;

    [Header("Условия стрельбы")]
    [SerializeField] private bool requirePlayerEnabled = true;
    [SerializeField] private bool stopWhenTimeScaleZero = true;

    [Header("Диагностика")]
    [SerializeField] private bool logStageChanges = false;
    [SerializeField] private bool logShots = false;

    private Player player;
    private float fireTimer;
    private int cachedStage = -1;
    private float cachedFireInterval;

    private void Awake()
    {
        player = GetComponent<Player>();

        if (muzzlePoint == null && createMuzzlePointIfMissing)
        {
            CreateFallbackMuzzlePoint();
        }

        RefreshFireRate();
    }

    private void OnEnable()
    {
        RefreshFireRate();
        fireTimer = cachedFireInterval;
    }

    private void Update()
    {
        RefreshFireRate();

        if (!CanShoot())
        {
            return;
        }

        fireTimer += Time.deltaTime;

        if (fireTimer < cachedFireInterval)
        {
            return;
        }

        fireTimer -= cachedFireInterval;

        if (fireTimer > cachedFireInterval)
        {
            fireTimer = 0f;
        }

        Shoot();
    }

    private void RefreshFireRate()
    {
        int stage = GetCurrentDifficultyStage();
        float newInterval = CalculateFireInterval(stage);

        if (stage != cachedStage && logStageChanges)
        {
            Debug.Log($"PlayerWeaponController: Stage={stage}, FireInterval={newInterval:F3}.", this);
        }

        cachedStage = stage;
        cachedFireInterval = newInterval;
    }

    private float CalculateFireInterval(int stage)
    {
        if (!useStageBasedFireRate)
        {
            return Mathf.Max(0.03f, baseFireInterval);
        }

        float interval = baseFireInterval - Mathf.Max(0, stage) * fireIntervalDecreasePerStage;
        return Mathf.Max(minimumFireInterval, interval);
    }

    private int GetCurrentDifficultyStage()
    {
        if (GameManager.Instance == null)
        {
            return 0;
        }

        int safeStep = Mathf.Max(1, pointsPerDifficultyStage);
        return Mathf.Max(0, GameManager.Instance.score / safeStep);
    }

    private bool CanShoot()
    {
        if (bulletPrefab == null || muzzlePoint == null)
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

        if (requirePlayerEnabled && player != null && !player.enabled)
        {
            return false;
        }

        return gameObject.activeInHierarchy;
    }

    private void Shoot()
    {
        int safeBulletCount = Mathf.Max(1, bulletsPerShot);

        if (safeBulletCount == 1)
        {
            SpawnBullet(muzzlePoint.position, Vector2.right);
            return;
        }

        float centerIndex = (safeBulletCount - 1) * 0.5f;

        for (int i = 0; i < safeBulletCount; i++)
        {
            float offsetIndex = i - centerIndex;

            Vector3 spawnPosition = muzzlePoint.position +
                                    Vector3.up * (offsetIndex * multiShotVerticalSpacing);

            Vector2 direction = GetSpreadDirection(offsetIndex, safeBulletCount);

            SpawnBullet(spawnPosition, direction);
        }
    }

    private Vector2 GetSpreadDirection(float offsetIndex, int bulletCount)
    {
        if (Mathf.Abs(spreadAngle) <= 0.001f || bulletCount <= 1)
        {
            return Vector2.right;
        }

        float centerIndex = (bulletCount - 1) * 0.5f;
        float normalizedOffset = centerIndex <= 0.001f ? 0f : offsetIndex / centerIndex;
        float angle = normalizedOffset * spreadAngle;

        Quaternion rotation = Quaternion.Euler(0f, 0f, angle);
        return (rotation * Vector3.right).normalized;
    }

    private void SpawnBullet(Vector3 position, Vector2 direction)
    {
        Bullet bullet = Instantiate(bulletPrefab, position, Quaternion.identity);

        bullet.Initialize(
            newDamage: bulletDamage,
            newSpeed: bulletSpeed,
            newDirection: direction
        );

        if (logShots)
        {
            Debug.Log($"PlayerWeaponController: выстрел. Stage={cachedStage}, Damage={bulletDamage}, Speed={bulletSpeed}.", this);
        }
    }

    private void CreateFallbackMuzzlePoint()
    {
        GameObject muzzleObject = new GameObject("MuzzlePoint");

        muzzleObject.transform.SetParent(transform);
        muzzleObject.transform.localPosition = fallbackMuzzleLocalOffset;
        muzzleObject.transform.localRotation = Quaternion.identity;
        muzzleObject.transform.localScale = Vector3.one;

        muzzlePoint = muzzleObject.transform;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        baseFireInterval = Mathf.Max(0.03f, baseFireInterval);
        pointsPerDifficultyStage = Mathf.Max(1, pointsPerDifficultyStage);
        fireIntervalDecreasePerStage = Mathf.Max(0f, fireIntervalDecreasePerStage);
        minimumFireInterval = Mathf.Clamp(minimumFireInterval, 0.03f, baseFireInterval);

        bulletSpeed = Mathf.Max(0.1f, bulletSpeed);
        bulletDamage = Mathf.Max(1, bulletDamage);

        bulletsPerShot = Mathf.Clamp(bulletsPerShot, 1, 5);
        multiShotVerticalSpacing = Mathf.Max(0f, multiShotVerticalSpacing);
        spreadAngle = Mathf.Clamp(spreadAngle, 0f, 45f);
    }
#endif
}