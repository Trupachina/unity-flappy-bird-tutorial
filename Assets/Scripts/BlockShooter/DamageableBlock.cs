using TMPro;
using UnityEngine;

/// <summary>
/// Разрушаемый блок для Block Shooter режима.
///
/// Важно:
/// - блок НЕ должен иметь Tag = Obstacle;
/// - столкновение с игроком обрабатывается здесь;
/// - GameOver вызывается через оригинальный GameManager.Instance;
/// - очки начисляются через оригинальный GameManager.IncreaseScore().
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public class DamageableBlock : MonoBehaviour
{
    private enum ScoreAwardMode
    {
        FullBlockValueOnDestroy,
        PerDamageHit
    }

    [Header("Здоровье")]
    [SerializeField] private int startHealth = 1;
    [SerializeField] private int minimumHealth = 1;
    [SerializeField] private int maximumHealth = 99;

    [Header("Очки")]
    [SerializeField] private bool awardScore = true;

    [Tooltip("FullBlockValueOnDestroy — очки только при полном уничтожении. PerDamageHit — очки за каждый нанесённый урон.")]
    [SerializeField] private ScoreAwardMode scoreAwardMode = ScoreAwardMode.FullBlockValueOnDestroy;

    [Tooltip("Если включено, стоимость блока равна его стартовому HP.")]
    [SerializeField] private bool scoreEqualsStartHealth = true;

    [Tooltip("Фиксированная стоимость блока, если scoreEqualsStartHealth выключен.")]
    [SerializeField] private int fixedScoreValue = 1;

    [Header("Столкновение с игроком")]
    [SerializeField] private bool killPlayerOnTouch = true;

    [Tooltip("Принудительно ставить блоку Untagged, чтобы Player.cs не вызвал GameOver второй раз через Tag Obstacle.")]
    [SerializeField] private bool forceUntaggedForPlayerSafety = true;

    [Tooltip("Защита от списания нескольких жизней, если игрок влетел сразу в несколько блоков.")]
    [SerializeField] private float globalPlayerHitCooldownSeconds = 0.25f;

    [Tooltip("Очищать пули и стены после столкновения игрока с блоком.")]
    [SerializeField] private bool clearRuntimeObjectsOnPlayerHit = true;

    [Header("Текст HP")]
    [SerializeField] private TextMeshPro hpText;
    [SerializeField] private Color textColor = Color.black;
    [SerializeField] private int textSortingOrderOffset = 1;

    [Header("Цвета блока")]
    [SerializeField] private Color health1Color = new Color(0.20f, 0.75f, 1.00f, 1f);
    [SerializeField] private Color health2To3Color = new Color(0.85f, 0.10f, 0.85f, 1f);
    [SerializeField] private Color health4To5Color = new Color(1.00f, 0.45f, 0.15f, 1f);
    [SerializeField] private Color health6PlusColor = new Color(1.00f, 0.10f, 0.10f, 1f);

    [Header("Диагностика")]
    [SerializeField] private bool logDebug = false;

    private static float lastGlobalPlayerHitTime = -999f;

    private SpriteRenderer spriteRenderer;
    private BoxCollider2D boxCollider;
    private Rigidbody2D rb;

    private int currentHealth;
    private int startHealthValue;
    private int scoreValue;

    private bool destroyed;
    private bool playerHitRegistered;
    private bool fullDestroyScoreAwarded;

    public int CurrentHealth => currentHealth;
    public int ScoreValue => scoreValue;
    public bool IsAlive => !destroyed && currentHealth > 0;

    private void Awake()
    {
        CacheComponents();
        ConfigureTag();
        ConfigurePhysics();
        Initialize(startHealth);
    }

    private void CacheComponents()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        boxCollider = GetComponent<BoxCollider2D>();
        rb = GetComponent<Rigidbody2D>();

        if (hpText == null)
        {
            hpText = GetComponentInChildren<TextMeshPro>(true);
        }
    }

    private void ConfigureTag()
    {
        if (!forceUntaggedForPlayerSafety)
        {
            return;
        }

        if (gameObject.CompareTag("Obstacle"))
        {
            gameObject.tag = "Untagged";
        }
    }

    private void ConfigurePhysics()
    {
        if (boxCollider != null)
        {
            boxCollider.isTrigger = true;
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

    public void Initialize(int health)
    {
        destroyed = false;
        playerHitRegistered = false;
        fullDestroyScoreAwarded = false;

        currentHealth = Mathf.Clamp(health, minimumHealth, maximumHealth);
        startHealthValue = currentHealth;

        scoreValue = scoreEqualsStartHealth
            ? startHealthValue
            : Mathf.Max(1, fixedScoreValue);

        UpdateVisuals();

        if (logDebug)
        {
            Debug.Log($"DamageableBlock: HP={currentHealth}, ScoreValue={scoreValue}, Mode={scoreAwardMode}.", this);
        }
    }

    public void TakeDamage(int damage)
    {
        if (destroyed)
        {
            return;
        }

        int safeDamage = Mathf.Max(1, damage);
        int actualDamageApplied = Mathf.Min(safeDamage, currentHealth);

        if (actualDamageApplied <= 0)
        {
            return;
        }

        currentHealth -= actualDamageApplied;

        if (awardScore && scoreAwardMode == ScoreAwardMode.PerDamageHit)
        {
            AddScore(actualDamageApplied);
        }

        if (currentHealth <= 0)
        {
            DestroyBlock();
            return;
        }

        UpdateVisuals();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!killPlayerOnTouch || destroyed || playerHitRegistered)
        {
            return;
        }

        Player touchedPlayer = other.GetComponent<Player>();

        if (touchedPlayer == null)
        {
            touchedPlayer = other.GetComponentInParent<Player>();
        }

        if (touchedPlayer == null)
        {
            return;
        }

        float timeSinceLastGlobalHit = Time.unscaledTime - lastGlobalPlayerHitTime;

        if (timeSinceLastGlobalHit < globalPlayerHitCooldownSeconds)
        {
            return;
        }

        lastGlobalPlayerHitTime = Time.unscaledTime;
        playerHitRegistered = true;

        if (clearRuntimeObjectsOnPlayerHit)
        {
            ClearRuntimeObjects();
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.GameOver();
        }

        if (logDebug)
        {
            Debug.Log("DamageableBlock: игрок столкнулся с блоком. Вызван GameManager.GameOver().", this);
        }
    }

    private void DestroyBlock()
    {
        if (destroyed)
        {
            return;
        }

        destroyed = true;

        if (awardScore && scoreAwardMode == ScoreAwardMode.FullBlockValueOnDestroy)
        {
            AwardFullDestroyScore();
        }

        Destroy(gameObject);
    }

    private void AwardFullDestroyScore()
    {
        if (fullDestroyScoreAwarded)
        {
            return;
        }

        fullDestroyScoreAwarded = true;
        AddScore(scoreValue);
    }

    private void AddScore(int amount)
    {
        if (amount <= 0 || GameManager.Instance == null)
        {
            return;
        }

        for (int i = 0; i < amount; i++)
        {
            GameManager.Instance.IncreaseScore();
        }
    }

    private void ClearRuntimeObjects()
    {
        Bullet[] bullets = FindObjectsOfType<Bullet>();

        for (int i = 0; i < bullets.Length; i++)
        {
            if (bullets[i] != null)
            {
                Destroy(bullets[i].gameObject);
            }
        }

        BlockWallMover[] walls = FindObjectsOfType<BlockWallMover>();

        for (int i = 0; i < walls.Length; i++)
        {
            if (walls[i] != null)
            {
                Destroy(walls[i].gameObject);
            }
        }
    }

    private void UpdateVisuals()
    {
        UpdateColor();
        UpdateText();
        UpdateTextSorting();
    }

    private void UpdateColor()
    {
        if (spriteRenderer == null)
        {
            return;
        }

        if (currentHealth <= 1)
        {
            spriteRenderer.color = health1Color;
        }
        else if (currentHealth <= 3)
        {
            spriteRenderer.color = health2To3Color;
        }
        else if (currentHealth <= 5)
        {
            spriteRenderer.color = health4To5Color;
        }
        else
        {
            spriteRenderer.color = health6PlusColor;
        }
    }

    private void UpdateText()
    {
        if (hpText == null)
        {
            return;
        }

        hpText.text = Mathf.Max(0, currentHealth).ToString();
        hpText.color = textColor;
        hpText.alignment = TextAlignmentOptions.Center;
    }

    private void UpdateTextSorting()
    {
        if (spriteRenderer == null || hpText == null)
        {
            return;
        }

        Renderer textRenderer = hpText.GetComponent<Renderer>();

        if (textRenderer == null)
        {
            return;
        }

        textRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
        textRenderer.sortingOrder = spriteRenderer.sortingOrder + textSortingOrderOffset;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        minimumHealth = Mathf.Max(1, minimumHealth);
        maximumHealth = Mathf.Max(minimumHealth, maximumHealth);
        startHealth = Mathf.Clamp(startHealth, minimumHealth, maximumHealth);

        fixedScoreValue = Mathf.Max(1, fixedScoreValue);
        textSortingOrderOffset = Mathf.Max(0, textSortingOrderOffset);
        globalPlayerHitCooldownSeconds = Mathf.Max(0.01f, globalPlayerHitCooldownSeconds);
    }
#endif
}