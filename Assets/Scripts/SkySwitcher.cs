using UnityEngine;

public class SkySwitcher : MonoBehaviour
{
    public Material daySkyMaterial;
    public Material nightSkyMaterial;
    public float animationSpeed = 1f;
    public float transitionDuration = 5f; // Длительность перехода между материалами
    public float switchInterval = 60f; // Интервал смены в секундах (1 минута)

    private MeshRenderer meshRenderer;
    private bool isDay = true; // Флаг текущего времени суток
    private float transitionTimer = 0f;

    private void Awake()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        meshRenderer.material = daySkyMaterial; // Устанавливаем дневной материал изначально
    }

    private void Update()
    {
        // Параллакс-анимация
        meshRenderer.material.mainTextureOffset += new Vector2(animationSpeed * Time.deltaTime, 0);

        // Обновляем таймер
        transitionTimer += Time.deltaTime;

        // Проверяем, если прошло необходимое время для смены
        if (transitionTimer >= switchInterval)
        {
            transitionTimer = 0f; // Сбрасываем таймер
            StartCoroutine(SwitchSky()); // Запускаем плавную смену
        }
    }

    private System.Collections.IEnumerator SwitchSky()
    {
        Material startMaterial = isDay ? daySkyMaterial : nightSkyMaterial;
        Material endMaterial = isDay ? nightSkyMaterial : daySkyMaterial;

        float elapsedTime = 0f;
        isDay = !isDay; // Переключаем флаг

        // Плавно изменяем материал с использованием линейной интерполяции (Lerp)
        while (elapsedTime < transitionDuration)
        {
            elapsedTime += Time.deltaTime;
            float blendFactor = Mathf.Clamp01(elapsedTime / transitionDuration); // Нормализованный таймер (от 0 до 1)

            // Линейно интерполируем между двумя текстурами
            meshRenderer.material.Lerp(startMaterial, endMaterial, blendFactor);

            yield return null;
        }

        // Устанавливаем конечный материал
        meshRenderer.material = endMaterial;
    }
}
