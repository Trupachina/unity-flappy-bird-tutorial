using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using UnityEngine.UI;

public class ScenePreloader : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private int sceneBuildIndex = -1;
    [SerializeField] private float minPreloadTime = 2f;
    [SerializeField] private bool showDebug = true;

    [Header("UI References")]
    [SerializeField] private Text progressText;
    [SerializeField] private Image progressBar;

    private AsyncOperation _preloadOperation;
    private bool _isPreloading;
    private float _startTime;

    public bool IsPreloading => _isPreloading;
    public bool IsReadyToActivate => _preloadOperation?.progress >= 0.9f;
    public float Progress => _preloadOperation?.progress ?? 0f;
    public float ElapsedTime => Time.realtimeSinceStartup - _startTime;

    private void Awake()
    {
        if (sceneBuildIndex < 0)
        {
            sceneBuildIndex = SceneManager.GetActiveScene().buildIndex;
        }
    }

    public void StartPreloading()
    {
        if (_isPreloading) return;

        _startTime = Time.realtimeSinceStartup;
        StartCoroutine(PreloadRoutine());
    }

    private IEnumerator PreloadRoutine()
    {
        _isPreloading = true;

        // Начинаем загрузку сцены в фоне
        _preloadOperation = SceneManager.LoadSceneAsync(sceneBuildIndex);
        _preloadOperation.allowSceneActivation = false;
        _preloadOperation.priority = (int)ThreadPriority.High;

        if (showDebug) Debug.Log($"[Preloader] Начата предзагрузка сцены {GetSceneName()}");

        // Ждем минимальное время для имитации загрузки
        float minWaitEndTime = Time.realtimeSinceStartup + minPreloadTime;

        // Обновляем UI пока не загружено
        while (!IsReadyToActivate || Time.realtimeSinceStartup < minWaitEndTime)
        {
            UpdateProgressUI();
            yield return null;
        }

        if (showDebug) Debug.Log($"[Preloader] Сцена готова к активации. Время: {ElapsedTime:F2}с");
        UpdateProgressUI();
    }

    private void UpdateProgressUI()
    {
        if (progressText != null)
        {
            float progress = Mathf.Clamp01(Progress / 0.9f);
            progressText.text = $"{(int)(progress * 100)}%";
        }

        if (progressBar != null)
        {
            progressBar.fillAmount = Progress / 0.9f;
        }
    }

    public void ActivatePreloadedScene()
    {
        if (!IsReadyToActivate)
        {
            Debug.LogWarning("[Preloader] Попытка активации неготовой сцены!");
            return;
        }

        if (showDebug) Debug.Log($"[Preloader] Активация сцены {GetSceneName()}");

        // Выполняем финальную подготовку
        System.GC.Collect();
        Resources.UnloadUnusedAssets();

        // Активируем сцену
        _preloadOperation.allowSceneActivation = true;
    }

    public void CancelPreload()
    {
        if (!_isPreloading) return;

        StopAllCoroutines();

        if (_preloadOperation != null)
        {
            _preloadOperation.allowSceneActivation = false;
            _preloadOperation = null;
        }

        _isPreloading = false;

        if (showDebug) Debug.Log("[Preloader] Предзагрузка отменена");
    }

    private string GetSceneName()
    {
        return SceneUtility.GetScenePathByBuildIndex(sceneBuildIndex);
    }
}