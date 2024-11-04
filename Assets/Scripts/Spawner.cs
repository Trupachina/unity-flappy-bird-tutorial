using UnityEngine;

public class Spawner : MonoBehaviour
{
    public Pipes dayPipesPrefab;  // Префаб дневных труб
    public Pipes nightPipesPrefab; // Префаб ночных труб
    public float spawnRate = 1f;
    public float minHeight = -1f;
    public float maxHeight = 2f;
    public float verticalGap = 3f;
    public float switchInterval = 60f; // Интервал смены дня и ночи

    private bool isDay = true; // Текущий режим (день или ночь)
    private float switchTimer = 0f;

    private void OnEnable()
    {
        InvokeRepeating(nameof(Spawn), spawnRate, spawnRate);
    }

    private void OnDisable()
    {
        CancelInvoke(nameof(Spawn));
    }

    private void Update()
    {
        // Обновляем таймер смены дня и ночи
        switchTimer += Time.deltaTime;

        // Проверяем, если прошло необходимое время для смены
        if (switchTimer >= switchInterval)
        {
            switchTimer = 0f; // Сбрасываем таймер
            isDay = !isDay;   // Переключаем режим дня и ночи
        }
    }

    private void Spawn()
    {
        // Выбираем префаб в зависимости от текущего состояния (день или ночь)
        Pipes selectedPrefab = isDay ? dayPipesPrefab : nightPipesPrefab;

        // Создаем экземпляр выбранного префаба
        Pipes pipes = Instantiate(selectedPrefab, transform.position, Quaternion.identity);

        // Рандомно изменяем позицию труб по вертикали
        pipes.transform.position += Vector3.up * Random.Range(minHeight, maxHeight);

        // Устанавливаем зазор между трубами
        pipes.gap = verticalGap;

        // Устанавливаем спрайт для текущего времени суток
        pipes.SetSprite(isDay);
    }
}
