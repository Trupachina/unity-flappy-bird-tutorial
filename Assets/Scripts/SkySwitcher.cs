using UnityEngine;
using System.Collections;

public class SkySwitcher : MonoBehaviour
{
    public Material daySkyMaterial;
    public Material nightSkyMaterial;
    public float transitionDuration = 5f;
    public float switchInterval = 60f;

    private MeshRenderer meshRenderer;
    public bool IsDay { get; private set; } = true;
    private float transitionTimer = 0f;

    // Событие для оповещения о смене времени суток
    public event System.Action<bool> OnDayNightChanged;

    private void Awake()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        meshRenderer.material = new Material(daySkyMaterial); // Используем копию материала
    }

    private void Update()
    {
        transitionTimer += Time.deltaTime;
        if (transitionTimer >= switchInterval)
        {
            transitionTimer = 0f;
            StartCoroutine(SwitchSky());
        }
    }

    private IEnumerator SwitchSky()
    {
        Material startMaterial = IsDay ? daySkyMaterial : nightSkyMaterial;
        Material endMaterial = IsDay ? nightSkyMaterial : daySkyMaterial;

        float elapsedTime = 0f;
        IsDay = !IsDay; // Переключаем флаг

        // Уведомляем подписчиков
        OnDayNightChanged?.Invoke(IsDay);

        while (elapsedTime < transitionDuration)
        {
            elapsedTime += Time.deltaTime;
            float blendFactor = Mathf.Clamp01(elapsedTime / transitionDuration);
            meshRenderer.material.Lerp(startMaterial, endMaterial, blendFactor);
            yield return null;
        }

        meshRenderer.material = new Material(endMaterial);
    }
}