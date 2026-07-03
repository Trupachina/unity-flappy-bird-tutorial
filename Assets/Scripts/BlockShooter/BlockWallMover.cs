using UnityEngine;

/// <summary>
/// Двигает стену блоков влево и удаляет её,
/// когда она ушла за экран или внутри не осталось блоков.
/// </summary>
[DisallowMultipleComponent]
public class BlockWallMover : MonoBehaviour
{
    [Header("Движение")]
    [SerializeField] private float speed = 3.5f;

    [Header("Удаление")]
    [SerializeField] private float destroyX = -12f;
    [SerializeField] private bool destroyWhenEmpty = true;

    [Header("Пауза")]
    [SerializeField] private bool stopWhenTimeScaleZero = true;

    private bool initialized;

    public void Initialize(float newSpeed, float newDestroyX, bool newStopWhenTimeScaleZero)
    {
        speed = Mathf.Max(0.1f, newSpeed);
        destroyX = newDestroyX;
        stopWhenTimeScaleZero = newStopWhenTimeScaleZero;
        initialized = true;
    }

    private void Update()
    {
        if (stopWhenTimeScaleZero && Time.timeScale <= 0.0001f)
        {
            return;
        }

        transform.position += Vector3.left * (speed * Time.deltaTime);

        if (transform.position.x <= destroyX)
        {
            Destroy(gameObject);
            return;
        }

        if (destroyWhenEmpty && initialized && transform.childCount == 0)
        {
            Destroy(gameObject);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        speed = Mathf.Max(0.1f, speed);
    }
#endif
}