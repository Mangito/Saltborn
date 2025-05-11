using UnityEngine;
using WaveHarmonic.Crest;
using System.Reflection;

public class CrestWaveController : MonoBehaviour
{
    [Header("Target object with ShapeFFT & ShapeWaves (usually 'WaveManager')")]
    public GameObject waveManager;

    ShapeFFT _shapeFFT;
    ShapeWaves _shapeWaves;

    FieldInfo _turbulenceField;
    FieldInfo _alignmentField;
    FieldInfo _windSpeedField;
    FieldInfo _windHeadingAngleField;

    void Awake()
    {
        if (waveManager == null)
        {
            Debug.LogError("WaveManager GameObject is not assigned.");
            return;
        }

        _shapeFFT = waveManager.GetComponent<ShapeFFT>();
        _shapeWaves = waveManager.GetComponent<ShapeWaves>();

        if (_shapeFFT == null || _shapeWaves == null)
        {
            Debug.LogError("ShapeFFT or ShapeWaves component missing on WaveManager.");
            return;
        }

        _turbulenceField = typeof(ShapeFFT).GetField("_WindTurbulence", BindingFlags.NonPublic | BindingFlags.Instance);
        _alignmentField = typeof(ShapeFFT).GetField("_WindAlignment", BindingFlags.NonPublic | BindingFlags.Instance);
        _windSpeedField = typeof(ShapeWaves).GetField("_WindSpeed", BindingFlags.NonPublic | BindingFlags.Instance);
        _windHeadingAngleField = typeof(ShapeWaves).GetField("_WaveDirectionHeadingAngle", BindingFlags.NonPublic | BindingFlags.Instance);
    }

    public void SetWindTurbulence(float value)
    {
        if (_turbulenceField != null && _shapeFFT != null)
        {
            _turbulenceField.SetValue(_shapeFFT, Mathf.Clamp01(value));
        }
    }

    public void SetWindAlignment(float value)
    {
        if (_alignmentField != null && _shapeFFT != null)
        {
            _alignmentField.SetValue(_shapeFFT, Mathf.Clamp01(value));
        }
    }

    public void SetWindSpeed(float kph)
    {
        if (_windSpeedField != null && _shapeWaves != null)
        {
            _windSpeedField.SetValue(_shapeWaves, Mathf.Clamp(kph, 0f, 150f));
        }
    }

    public void SetWindDirectionAngle(float angle)
    {
        if (_windHeadingAngleField != null && _shapeWaves != null)
        {
            _windHeadingAngleField.SetValue(_shapeWaves, Mathf.Repeat(angle, 360f)); // Normalize to [0, 360)
        }
    }
}
