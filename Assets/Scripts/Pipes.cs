using UnityEngine;

public class Pipes : MonoBehaviour
{
    public Transform top;
    public Transform bottom;
    public float speed = 5f;
    public float gap = 3f;

    private float leftEdge;

    // Спрайты для дневного и ночного времени
    public Sprite daySprite;
    public Sprite nightSprite;

    private void Start()
    {
        leftEdge = Camera.main.ScreenToWorldPoint(Vector3.zero).x - 1f;

        // Устанавливаем начальные позиции с учетом зазора
        top.position += Vector3.up * gap / 2;
        bottom.position += Vector3.down * gap / 2;
    }

    private void Update()
    {
        // Двигаем трубы влево
        transform.position += speed * Time.deltaTime * Vector3.left;

        // Удаляем трубы, если они выходят за левый край экрана
        if (transform.position.x < leftEdge)
        {
            Destroy(gameObject);
        }
    }

    // Метод для изменения спрайта в зависимости от состояния дня и ночи
    public void SetSprite(bool isDay)
    {
        SpriteRenderer topRenderer = top.GetComponent<SpriteRenderer>();
        SpriteRenderer bottomRenderer = bottom.GetComponent<SpriteRenderer>();

        if (topRenderer != null && bottomRenderer != null)
        {
            // Устанавливаем дневной или ночной спрайт
            Sprite selectedSprite = isDay ? daySprite : nightSprite;
            topRenderer.sprite = selectedSprite;
            bottomRenderer.sprite = selectedSprite;
        }
    }
}
