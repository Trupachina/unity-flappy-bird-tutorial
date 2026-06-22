using System.Collections;
using UnityEngine;

/// <summary>
/// Управляет плавным смещением камеры между стартовым экраном и игровым режимом.
///
/// Логика:
/// - при открытии сцены камера принудительно стоит в стартовой позиции;
/// - после фактического запуска игры камера плавно смещается вправо;
/// - скрипт не зависит от покупки жизни, монетоприёмника и UI;
/// - запуск движения должен вызываться из GameManager.Play().
/// </summary>
[DisallowMultipleComponent]
public class GameplayCameraShift : MonoBehaviour
{
    [Header("Camera Positions")]
    [Tooltip("Позиция камеры на стартовом экране. Обычно X = 0.")]
    [SerializeField] private Vector3 startPosition = new Vector3(0f, 0f, -10f);

    [Tooltip("Позиция камеры во время игры. По задаче заказчика X примерно = 2.")]
    [SerializeField] private Vector3 gameplayPosition = new Vector3(2f, 0f, -10f);

    [Header("Movement Settings")]
    [Tooltip("Длительность плавного смещения камеры в секундах.")]
    [SerializeField] private float moveDuration = 0.75f;

    [Tooltip("Использовать сглаживание SmoothStep вместо линейного движения.")]
    [SerializeField] private bool useSmoothStep = true;

    [Tooltip("Использовать unscaled time. Это безопаснее для стартового экрана, где Time.timeScale может быть 0.")]
    [SerializeField] private bool useUnscaledTime = true;

    private Coroutine moveCoroutine;

    private void Awake()
    {
        ResetToStartPosition();
    }

    private void OnEnable()
    {
        ResetToStartPosition();
    }

    /// <summary>
    /// Мгновенно возвращает камеру в стартовую позицию.
    /// Вызывается при открытии сцены.
    /// </summary>
    public void ResetToStartPosition()
    {
        StopMoveCoroutineIfNeeded();
        transform.position = startPosition;
    }

    /// <summary>
    /// Мгновенно переводит камеру в игровую позицию.
    /// Может пригодиться для тестов, но в основной логике лучше использовать MoveToGameplayPosition().
    /// </summary>
    public void SetGameplayPositionInstant()
    {
        StopMoveCoroutineIfNeeded();
        transform.position = gameplayPosition;
    }

    /// <summary>
    /// Плавно переводит камеру в игровую позицию.
    /// Основной метод для вызова из GameManager.Play().
    /// </summary>
    public void MoveToGameplayPosition()
    {
        MoveToPosition(gameplayPosition);
    }

    /// <summary>
    /// Плавно возвращает камеру на стартовую позицию.
    /// Сейчас это не обязательно использовать, потому что при рестарте сцена перезагружается.
    /// </summary>
    public void MoveToStartPosition()
    {
        MoveToPosition(startPosition);
    }

    private void MoveToPosition(Vector3 targetPosition)
    {
        StopMoveCoroutineIfNeeded();

        if (moveDuration <= 0f)
        {
            transform.position = targetPosition;
            return;
        }

        moveCoroutine = StartCoroutine(MoveRoutine(targetPosition));
    }

    private IEnumerator MoveRoutine(Vector3 targetPosition)
    {
        Vector3 fromPosition = transform.position;
        float elapsed = 0f;

        while (elapsed < moveDuration)
        {
            elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / moveDuration);

            if (useSmoothStep)
            {
                t = t * t * (3f - 2f * t);
            }

            transform.position = Vector3.Lerp(fromPosition, targetPosition, t);

            yield return null;
        }

        transform.position = targetPosition;
        moveCoroutine = null;
    }

    private void StopMoveCoroutineIfNeeded()
    {
        if (moveCoroutine == null)
        {
            return;
        }

        StopCoroutine(moveCoroutine);
        moveCoroutine = null;
    }
}