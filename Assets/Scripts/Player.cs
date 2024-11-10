using UnityEngine;

public class Player : MonoBehaviour
{
    public Sprite[] sprites;
    public float strength = 5f;
    public float gravity = -9.81f;
    public float tilt = 5f;
    public AudioClip jumpSound;
    public AudioClip hitSound;
    public AudioClip scoreSound;

    private SpriteRenderer spriteRenderer;
    private Vector3 direction;
    private int spriteIndex;
    private AudioSource audioSource;
    private bool isInvincible = false; // Флаг для неуязвимости игрока

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        audioSource = GetComponent<AudioSource>();

        // Если AudioSource не добавлен в инспекторе, добавляем его через код
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
    }

    private void Start()
    {
        InvokeRepeating(nameof(AnimateSprite), 0.15f, 0.15f);
    }

    private void OnEnable()
    {
        ResetPosition(); // Устанавливаем начальную позицию при активации игрока
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0))
        {
            direction = Vector3.up * strength;

            // Проигрываем звук прыжка, если аудиоклип назначен
            if (jumpSound != null)
            {
                audioSource.PlayOneShot(jumpSound);
            }
        }

        // Применение гравитации и обновление позиции
        direction.y += gravity * Time.deltaTime;
        transform.position += direction * Time.deltaTime;

        // Наклон игрока в зависимости от направления
        Vector3 rotation = transform.eulerAngles;
        rotation.z = direction.y * tilt;
        transform.eulerAngles = rotation;
    }

    private void AnimateSprite()
    {
        spriteIndex++;

        if (spriteIndex >= sprites.Length)
        {
            spriteIndex = 0;
        }

        if (spriteIndex < sprites.Length && spriteIndex >= 0)
        {
            spriteRenderer.sprite = sprites[spriteIndex];
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isInvincible) return; // Пропускаем столкновения, если включена неуязвимость

        if (other.gameObject.CompareTag("Obstacle"))
        {
            GameManager.Instance.GameOver();
            audioSource.PlayOneShot(hitSound);
        }
        else if (other.gameObject.CompareTag("Scoring"))
        {
            GameManager.Instance.IncreaseScore();
            audioSource.PlayOneShot(scoreSound);
        }
    }

    // Метод для сброса позиции и направления игрока
    public void ResetPosition()
    {
        Vector3 position = transform.position;
        position.y = 0f; // Начальная высота
        transform.position = position;
        direction = Vector3.zero; // Сброс направления
    }

    // Метод для включения/выключения бессмертия
    public void SetInvincibility(bool state)
    {
        isInvincible = state;
    }
}
