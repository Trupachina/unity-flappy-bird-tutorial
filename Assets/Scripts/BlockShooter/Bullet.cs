using UnityEngine;

/// <summary>
/// Пуля игрока для режима Block Shooter.
///
/// Важная логика:
/// - пуля летит вправо;
/// - наносит урон только DamageableBlock;
/// - уничтожается при попадании;
/// - уничтожается по lifetime;
/// - уничтожается по максимальной дистанции;
/// - уничтожается на правой границе камеры, чтобы не пробивать стены за экраном.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public class Bullet : MonoBehaviour
{
    [Header("Движение")]
    [SerializeField] private float speed = 10f;

    [Tooltip("Направление полёта. Для текущей версии — вправо.")]
    [SerializeField] private Vector2 direction = Vector2.right;

    [Header("Урон")]
    [SerializeField] private int damage = 1;

    [Header("Время жизни")]
    [Tooltip("Страховочная длительность жизни пули.")]
    [SerializeField] private float lifeTime = 2f;

    [Header("Ограничение по дистанции")]
    [Tooltip("Если включено, пуля уничтожается после прохождения заданной дистанции.")]
    [SerializeField] private bool useMaxTravelDistance = true;

    [Tooltip("Максимальная дистанция полёта от точки выстрела.")]
    [SerializeField] private float maxTravelDistance = 5.5f;

    [Header("Ограничение по камере")]
    [Tooltip("Если включено, пуля уничтожается у правой границы камеры.")]
    [SerializeField] private bool useCameraRightLimit = true;

    [Tooltip("Запас относительно правого края камеры. 0 — ровно край. Отрицательное значение уничтожит пулю чуть раньше.")]
    [SerializeField] private float cameraRightViewportMargin = 0.0f;

    [Tooltip("Если блок находится правее разрешённой боевой зоны, пуля не нанесёт ему урон.")]
    [SerializeField] private bool preventDamageOutsideCamera = true;

    [Header("Диагностика")]
    [SerializeField] private bool logDebug = false;

    private Rigidbody2D rb;
    private Collider2D bulletCollider;
    private Camera cachedCamera;

    private Vector2 spawnPosition;
    private float lifeTimer;
    private bool hasHit;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        bulletCollider = GetComponent<Collider2D>();

        cachedCamera = Camera.main;

        ConfigurePhysics();
    }

    private void OnEnable()
    {
        spawnPosition = transform.position;
        lifeTimer = 0f;
        hasHit = false;

        if (direction.sqrMagnitude <= 0.001f)
        {
            direction = Vector2.right;
        }

        direction.Normalize();

        if (cachedCamera == null)
        {
            cachedCamera = Camera.main;
        }
    }

    private void ConfigurePhysics()
    {
        if (bulletCollider != null)
        {
            bulletCollider.isTrigger = true;
        }

        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.simulated = true;
            rb.useFullKinematicContacts = false;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Discrete;
            rb.interpolation = RigidbodyInterpolation2D.None;
        }
    }

    private void Update()
    {
        lifeTimer += Time.deltaTime;

        if (lifeTimer >= lifeTime)
        {
            DestroyBullet();
            return;
        }

        if (ShouldDestroyByDistance(transform.position))
        {
            DestroyBullet();
            return;
        }

        if (ShouldDestroyByCameraLimit(transform.position))
        {
            DestroyBullet();
        }
    }

    private void FixedUpdate()
    {
        if (hasHit || rb == null)
        {
            return;
        }

        Vector2 currentPosition = rb.position;
        Vector2 nextPosition = currentPosition + direction * (speed * Time.fixedDeltaTime);

        // Важно: не даём пуле даже физически перелететь за правую границу камеры,
        // иначе OnTriggerEnter2D может успеть сработать по блоку за экраном.
        if (ShouldDestroyByDistance(nextPosition) || ShouldDestroyByCameraLimit(nextPosition))
        {
            DestroyBullet();
            return;
        }

        rb.MovePosition(nextPosition);
    }

    /// <summary>
    /// Настройка пули при создании из PlayerWeaponController.
    /// </summary>
    public void Initialize(int newDamage, float newSpeed, Vector2 newDirection)
    {
        damage = Mathf.Max(1, newDamage);
        speed = Mathf.Max(0.1f, newSpeed);

        direction = newDirection.sqrMagnitude > 0.001f
            ? newDirection.normalized
            : Vector2.right;

        spawnPosition = transform.position;
        lifeTimer = 0f;
        hasHit = false;

        if (cachedCamera == null)
        {
            cachedCamera = Camera.main;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hasHit)
        {
            return;
        }

        DamageableBlock block = other.GetComponent<DamageableBlock>();

        if (block == null)
        {
            block = other.GetComponentInParent<DamageableBlock>();
        }

        if (block == null || !block.IsAlive)
        {
            return;
        }

        // Дополнительная защита:
        // если блок ещё находится за правым краем камеры, пуля его не повреждает.
        if (preventDamageOutsideCamera && IsPointOutsideCameraCombatZone(block.transform.position))
        {
            DestroyBullet();

            if (logDebug)
            {
                Debug.Log("Bullet: блок за пределами камеры, урон не нанесён.", this);
            }

            return;
        }

        hasHit = true;

        block.TakeDamage(damage);

        if (logDebug)
        {
            Debug.Log($"Bullet: попадание по блоку. Damage = {damage}.", this);
        }

        DestroyBullet();
    }

    private bool ShouldDestroyByDistance(Vector2 position)
    {
        if (!useMaxTravelDistance)
        {
            return false;
        }

        float traveledDistance = Vector2.Distance(spawnPosition, position);

        return traveledDistance >= maxTravelDistance;
    }

    private bool ShouldDestroyByCameraLimit(Vector2 position)
    {
        if (!useCameraRightLimit)
        {
            return false;
        }

        float rightLimitX = GetCameraRightLimitX();

        return position.x >= rightLimitX;
    }

    private bool IsPointOutsideCameraCombatZone(Vector3 worldPosition)
    {
        if (!useCameraRightLimit)
        {
            return false;
        }

        float rightLimitX = GetCameraRightLimitX();

        return worldPosition.x > rightLimitX;
    }

    private float GetCameraRightLimitX()
    {
        if (cachedCamera == null)
        {
            cachedCamera = Camera.main;
        }

        if (cachedCamera == null)
        {
            // Если камера не найдена, fallback идёт только через maxTravelDistance.
            return spawnPosition.x + maxTravelDistance;
        }

        Vector3 viewportPoint = new Vector3(
            1f + cameraRightViewportMargin,
            0.5f,
            Mathf.Abs(cachedCamera.transform.position.z)
        );

        Vector3 worldPoint = cachedCamera.ViewportToWorldPoint(viewportPoint);

        return worldPoint.x;
    }

    private void DestroyBullet()
    {
        if (gameObject != null)
        {
            Destroy(gameObject);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        speed = Mathf.Max(0.1f, speed);
        damage = Mathf.Max(1, damage);
        lifeTime = Mathf.Max(0.05f, lifeTime);

        maxTravelDistance = Mathf.Max(0.1f, maxTravelDistance);
        cameraRightViewportMargin = Mathf.Clamp(cameraRightViewportMargin, -0.25f, 0.25f);

        if (direction.sqrMagnitude <= 0.001f)
        {
            direction = Vector2.right;
        }

        direction.Normalize();
    }
#endif
}