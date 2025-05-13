using UnityEngine;
using WaveHarmonic.Crest;

public class DynamicWind : MonoBehaviour
{
    public enum WindPreset { Calm, Normal, Choppy, Storm }

    [Header("Preset Timing")]

    [Tooltip("Time between applying new wind presets (seconds).")]
    [SerializeField] float presetChangeInterval = 600f;

    [Tooltip("Smoothing speed for wind transitions.")]
    [SerializeField] float windLerpSpeed = 0.005f;

    [Header("Initial Wind State")]

    [Tooltip("Wind speed in km/h.")]
    [Range(0, 150)]
    [SerializeField] float speed = 30f;

    [Tooltip("Wave alignment.")]
    [Range(0f, 1f)]
    [SerializeField] float alignment = 1f;

    [Tooltip("Wave angle.")]
    [Range(-180f, 180f)]
    [SerializeField] float angle;

    [Tooltip("Wave turbulence.")]
    [Range(0f, 1f)]
    [SerializeField] float turbulence = 1f;

    [Header("Preset Probabilities")]

    [Tooltip("Probability (0-1) for each wind preset")]
    [SerializeField] private float calmProbability = 0.05f;
    [SerializeField] private float normalProbability = 0.50f;
    [SerializeField] private float choppyProbability = 0.80f;

    [Header("Crest Wave Control")]
    public CrestWaveController crestWaveController;

    float targetSpeed, targetAngle, targetAlignment, targetTurbulence;
    float presetTimer;

    void Start()
    {
        targetSpeed = speed;
        targetAngle = angle;
        targetAlignment = alignment;
        targetTurbulence = turbulence;

        ApplyWindPreset();
    }

    void Update()
    {
        if (crestWaveController == null) return;

        float dt = Time.deltaTime;
        presetTimer += dt;

        if (presetTimer >= presetChangeInterval)
        {
            ApplyWindPreset();
            presetTimer = 0f;
        }

        float t = Mathf.Clamp01(windLerpSpeed * dt);

        speed = Mathf.Lerp(speed, targetSpeed, t * 2f);
        angle = Mathf.Lerp(angle, targetAngle, t * 1.5f);
        alignment = Mathf.Lerp(alignment, targetAlignment, t);
        turbulence = Mathf.Lerp(turbulence, targetTurbulence, t);

        crestWaveController.SetWindSpeed(speed);
        crestWaveController.SetWindDirectionAngle(angle);
        crestWaveController.SetWindAlignment(alignment);
        crestWaveController.SetWindTurbulence(turbulence);
    }

    void ApplyWindPreset()
    {
        WindPreset preset = GetRandomPreset();

        switch (preset)
        {
            case WindPreset.Calm:
                targetSpeed = Random.Range(10f, 20f);
                targetTurbulence = Random.Range(0f, 0.4f);
                targetAlignment = Random.Range(0.8f, 1f);
                break;

            case WindPreset.Normal:
                targetSpeed = Random.Range(20f, 50f);
                targetTurbulence = Random.Range(0f, 0.4f);
                targetAlignment = Random.Range(0f, 0.7f);
                break;

            case WindPreset.Choppy:
                targetSpeed = Random.Range(50f, 90f);
                targetTurbulence = Random.Range(0.7f, 1f);
                targetAlignment = Random.Range(0f, 1f);
                break;

            case WindPreset.Storm:
                targetSpeed = Random.Range(90f, 150f);
                targetTurbulence = 1f;
                targetAlignment = Random.Range(0f, 1f);
                break;
        }

        targetAngle = Random.Range(-180f, 180f);
    }

    WindPreset GetRandomPreset()
    {
        float r = Random.value;

        if (r < calmProbability) return WindPreset.Calm;
        if (r < normalProbability) return WindPreset.Choppy;
        if (r < choppyProbability) return WindPreset.Normal;
        return WindPreset.Storm;
    }

    public float GetAngle() => angle;
}
