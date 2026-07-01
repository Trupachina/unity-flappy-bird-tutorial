using UnityEngine;

/// <summary>
/// Ручной пиксельный выхлоп для Flight-режима.
/// 
/// Этот скрипт не использует TrailRenderer, потому что в Flappy-игре игрок почти
/// не двигается по X, и обычный TrailRenderer рисует вертикальный след.
/// 
/// Здесь частицы создаются вручную в точке хвоста и получают скорость строго влево.
/// Также исправлена сортировка: частицы по умолчанию рисуются выше фона,
/// чтобы след не пропадал за облаками/землёй/задником.
/// </summary>
[DisallowMultipleComponent]
public class PlayerFlightExhaustTrail : MonoBehaviour
{
    private enum ExhaustDirectionMode
    {
        WorldLeft,
        LocalBack
    }

    [Header("Основное")]
    [SerializeField] private bool emitOnlyInFlightMode = true;

    [Tooltip("Отключить старые TrailRenderer на объекте игрока, чтобы не было вертикального следа.")]
    [SerializeField] private bool disableExistingTrailRenderers = true;

    [Tooltip("Очищать частицы при выходе из Flight.")]
    [SerializeField] private bool clearWhenDisabled = true;

    [Header("Позиция хвоста")]
    [Tooltip("Локальное смещение хвоста ракеты. Для ракеты, смотрящей вправо, X отрицательный.")]
    [SerializeField] private Vector3 exhaustLocalOffset = new Vector3(-0.70f, -0.03f, 0f);

    [Header("Направление")]
    [Tooltip("WorldLeft — всегда влево по экрану. LocalBack — назад относительно поворота игрока.")]
    [SerializeField] private ExhaustDirectionMode directionMode = ExhaustDirectionMode.WorldLeft;

    [Tooltip("Основная скорость частиц назад. Чем больше — тем сильнее след уходит влево.")]
    [SerializeField] private float exhaustSpeed = 5.6f;

    [Tooltip("Случайный разброс скорости.")]
    [SerializeField] private float speedRandomness = 0.35f;

    [Tooltip("Вертикальный разброс. Для ровного следа держи маленьким.")]
    [SerializeField] private float verticalSpread = 0.04f;

    [Header("Длина и плотность")]
    [Tooltip("Время жизни частиц. Чем больше — тем длиннее след.")]
    [SerializeField] private float particleLifetime = 1.35f;

    [Tooltip("Сколько частиц создаётся в секунду.")]
    [SerializeField] private float particlesPerSecond = 130f;

    [Header("Размер")]
    [SerializeField] private float startSize = 0.13f;
    [SerializeField] private float endSizeMultiplier = 0.12f;
    [SerializeField] private float sizeRandomness = 0.02f;

    [Header("Цвет")]
    [SerializeField] private Color startColor = new Color(1.0f, 0.25f, 0.05f, 1.0f);
    [SerializeField] private Color middleColor = new Color(1.0f, 0.85f, 0.05f, 0.75f);
    [SerializeField] private Color endColor = new Color(0.2f, 0.75f, 1.0f, 0.0f);

    [Header("Сортировка")]
    [Tooltip("Если включено, частицы берут Sorting Layer игрока.")]
    [SerializeField] private bool usePlayerSortingLayer = true;

    [Tooltip("Order in Layer относительно SpriteRenderer игрока. Поставь 2 или 3, если след не видно.")]
    [SerializeField] private int sortingOrderOffset = 3;

    [Tooltip("Принудительный Order in Layer, если нет SpriteRenderer игрока.")]
    [SerializeField] private int fallbackSortingOrder = 20;

    [Header("Диагностика")]
    [SerializeField] private bool logDebug = true;

    private Player player;
    private SpriteRenderer playerRenderer;

    private ParticleSystem particleSystemInstance;
    private ParticleSystemRenderer particleRenderer;

    private Material runtimeMaterial;
    private Texture2D runtimeTexture;

    private float emissionAccumulator;
    private bool wasEmitting;

    private void Awake()
    {
        player = GetComponent<Player>();
        playerRenderer = GetComponent<SpriteRenderer>();

        if (disableExistingTrailRenderers)
        {
            DisableOldTrailRenderers();
        }

        CreateParticleSystem();
        ConfigureParticleSystem();
        ConfigureRenderer();
        ForceClear();
    }

    private void Update()
    {
        bool shouldEmit = ShouldEmit();

        if (!shouldEmit)
        {
            emissionAccumulator = 0f;
        }

        if (shouldEmit && !particleSystemInstance.isPlaying)
        {
            particleSystemInstance.Play(true);
        }

        if (!shouldEmit && particleSystemInstance.isPlaying)
        {
            particleSystemInstance.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        if (!shouldEmit && wasEmitting && clearWhenDisabled)
        {
            ForceClear();
        }

        if (shouldEmit)
        {
            EmitManualParticles();
        }

        if (logDebug && shouldEmit != wasEmitting)
        {
            Debug.Log($"PlayerFlightExhaustTrail: выхлоп {(shouldEmit ? "включён" : "выключен")}.", this);
        }

        wasEmitting = shouldEmit;
    }

    private void OnDisable()
    {
        ForceClear();
    }

    private void OnDestroy()
    {
        if (particleSystemInstance != null)
        {
            Destroy(particleSystemInstance.gameObject);
        }

        if (runtimeMaterial != null)
        {
            Destroy(runtimeMaterial);
        }

        if (runtimeTexture != null)
        {
            Destroy(runtimeTexture);
        }
    }

    /// <summary>
    /// Внешний вызов для смерти, сброса позиции, GameOver и выхода из режима.
    /// </summary>
    public void ForceClear()
    {
        emissionAccumulator = 0f;
        wasEmitting = false;

        if (particleSystemInstance != null)
        {
            particleSystemInstance.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particleSystemInstance.Clear(true);
        }

        if (logDebug)
        {
            Debug.Log("PlayerFlightExhaustTrail: след принудительно очищен.", this);
        }
    }

    private void DisableOldTrailRenderers()
    {
        TrailRenderer[] trails = GetComponentsInChildren<TrailRenderer>(true);

        for (int i = 0; i < trails.Length; i++)
        {
            if (trails[i] == null)
            {
                continue;
            }

            trails[i].emitting = false;
            trails[i].enabled = false;
            trails[i].Clear();
        }
    }

    private void CreateParticleSystem()
    {
        GameObject particlesObject = new GameObject("RuntimeFlightExhaustParticles");

        // Не делаем дочерним игроку: частицы должны оставаться в мире позади ракеты.
        particlesObject.transform.SetParent(null);
        particlesObject.transform.position = transform.position;
        particlesObject.transform.rotation = Quaternion.identity;
        particlesObject.transform.localScale = Vector3.one;

        particleSystemInstance = particlesObject.AddComponent<ParticleSystem>();
        particleRenderer = particlesObject.GetComponent<ParticleSystemRenderer>();
    }

    private void ConfigureParticleSystem()
    {
        ParticleSystem.MainModule main = particleSystemInstance.main;

        main.playOnAwake = false;
        main.loop = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startSpeed = 0f;
        main.startLifetime = particleLifetime;
        main.startSize = startSize;
        main.startColor = startColor;
        main.gravityModifier = 0f;
        main.maxParticles = 1500;

        ParticleSystem.EmissionModule emission = particleSystemInstance.emission;
        emission.enabled = false;
        emission.rateOverTime = 0f;

        ParticleSystem.ShapeModule shape = particleSystemInstance.shape;
        shape.enabled = false;

        ParticleSystem.VelocityOverLifetimeModule velocity = particleSystemInstance.velocityOverLifetime;
        velocity.enabled = false;

        ParticleSystem.ColorOverLifetimeModule color = particleSystemInstance.colorOverLifetime;
        color.enabled = true;
        color.color = CreateColorGradient();

        ParticleSystem.SizeOverLifetimeModule size = particleSystemInstance.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(
            1f,
            new AnimationCurve(
                new Keyframe(0f, 1f),
                new Keyframe(0.55f, 0.75f),
                new Keyframe(1f, Mathf.Clamp01(endSizeMultiplier))
            )
        );
    }

    private void ConfigureRenderer()
    {
        if (particleRenderer == null)
        {
            return;
        }

        particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        particleRenderer.alignment = ParticleSystemRenderSpace.View;
        particleRenderer.sortMode = ParticleSystemSortMode.None;

        runtimeTexture = CreatePixelTexture();
        runtimeMaterial = CreatePixelMaterial(runtimeTexture);

        particleRenderer.material = runtimeMaterial;

        if (usePlayerSortingLayer && playerRenderer != null)
        {
            particleRenderer.sortingLayerID = playerRenderer.sortingLayerID;
            particleRenderer.sortingOrder = playerRenderer.sortingOrder + sortingOrderOffset;
        }
        else
        {
            particleRenderer.sortingOrder = fallbackSortingOrder;
        }

        if (logDebug)
        {
            Debug.Log(
                $"PlayerFlightExhaustTrail: SortingLayerID={particleRenderer.sortingLayerID}, SortingOrder={particleRenderer.sortingOrder}.",
                this
            );
        }
    }

    private Texture2D CreatePixelTexture()
    {
        Texture2D texture = new Texture2D(6, 6, TextureFormat.RGBA32, false);
        texture.name = "Runtime_Pixel_Exhaust_Texture";
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;

        Color[] pixels = new Color[36];

        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = Color.white;
        }

        texture.SetPixels(pixels);
        texture.Apply();

        return texture;
    }

    private Material CreatePixelMaterial(Texture2D texture)
    {
        Shader shader = Shader.Find("Sprites/Default");

        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        }

        if (shader == null)
        {
            shader = Shader.Find("Particles/Standard Unlit");
        }

        if (shader == null)
        {
            shader = Shader.Find("Unlit/Transparent");
        }

        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        Material material = new Material(shader);
        material.name = "Runtime_Pixel_Flight_Exhaust_Material";
        material.mainTexture = texture;

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", Color.white);
        }

        return material;
    }

    private Gradient CreateColorGradient()
    {
        Gradient gradient = new Gradient();

        GradientColorKey[] colorKeys =
        {
            new GradientColorKey(startColor, 0f),
            new GradientColorKey(middleColor, 0.45f),
            new GradientColorKey(endColor, 1f)
        };

        GradientAlphaKey[] alphaKeys =
        {
            new GradientAlphaKey(startColor.a, 0f),
            new GradientAlphaKey(middleColor.a, 0.45f),
            new GradientAlphaKey(0f, 1f)
        };

        gradient.SetKeys(colorKeys, alphaKeys);
        return gradient;
    }

    private bool ShouldEmit()
    {
        if (player == null)
        {
            return false;
        }

        if (!gameObject.activeInHierarchy)
        {
            return false;
        }

        if (!player.enabled)
        {
            return false;
        }

        if (Time.timeScale <= 0.0001f)
        {
            return false;
        }

        if (!emitOnlyInFlightMode)
        {
            return true;
        }

        return player.CurrentMoveMode == Player.PlayerMoveMode.Flight;
    }

    private void EmitManualParticles()
    {
        if (particleSystemInstance == null)
        {
            return;
        }

        emissionAccumulator += particlesPerSecond * Time.deltaTime;

        int particlesToEmit = Mathf.FloorToInt(emissionAccumulator);

        if (particlesToEmit <= 0)
        {
            return;
        }

        emissionAccumulator -= particlesToEmit;

        Vector3 tailWorldPosition = transform.TransformPoint(exhaustLocalOffset);
        Vector3 direction = GetExhaustDirection();

        for (int i = 0; i < particlesToEmit; i++)
        {
            EmitOneParticle(tailWorldPosition, direction);
        }
    }

    private void EmitOneParticle(Vector3 tailWorldPosition, Vector3 direction)
    {
        Vector3 randomOffset = new Vector3(
            Random.Range(-0.025f, 0.025f),
            Random.Range(-0.025f, 0.025f),
            0f
        );

        float speed = exhaustSpeed + Random.Range(-speedRandomness, speedRandomness);
        speed = Mathf.Max(0.1f, speed);

        Vector3 perpendicular = new Vector3(-direction.y, direction.x, 0f);
        Vector3 spreadVelocity = perpendicular * Random.Range(-verticalSpread, verticalSpread);

        ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams();

        emitParams.position = tailWorldPosition + randomOffset;
        emitParams.velocity = direction * speed + spreadVelocity;
        emitParams.startLifetime = particleLifetime * Random.Range(0.85f, 1.15f);
        emitParams.startSize = Mathf.Max(0.01f, startSize + Random.Range(-sizeRandomness, sizeRandomness));
        emitParams.startColor = startColor;
        emitParams.rotation = Random.Range(0f, 360f);

        particleSystemInstance.Emit(emitParams, 1);
    }

    private Vector3 GetExhaustDirection()
    {
        if (directionMode == ExhaustDirectionMode.LocalBack)
        {
            Vector3 localBack = -transform.right;
            localBack.z = 0f;

            if (localBack.sqrMagnitude > 0.001f)
            {
                return localBack.normalized;
            }
        }

        return Vector3.left;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        exhaustSpeed = Mathf.Max(0.1f, exhaustSpeed);
        speedRandomness = Mathf.Max(0f, speedRandomness);
        verticalSpread = Mathf.Max(0f, verticalSpread);

        particleLifetime = Mathf.Max(0.05f, particleLifetime);
        particlesPerSecond = Mathf.Max(0f, particlesPerSecond);

        startSize = Mathf.Max(0.01f, startSize);
        endSizeMultiplier = Mathf.Clamp01(endSizeMultiplier);
        sizeRandomness = Mathf.Max(0f, sizeRandomness);
    }
#endif
}