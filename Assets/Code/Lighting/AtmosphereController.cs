using UnityEngine;
using UnityEngine.Rendering;

namespace Assets.Code.Lighting
{
  [ExecuteAlways, DisallowMultipleComponent]
  public sealed class AtmosphereController : MonoBehaviour
  {
    [SerializeField] private Light sun;
    [Tooltip("Optional shared preset. Leave empty to use the settings below.")]
    [SerializeField] private AtmosphereProfile profile;
    [SerializeField] private AtmosphereSettings settings = new();
    [SerializeField, Range(0f, 24f)] private float timeOfDay = 17.5f;
    [SerializeField] private bool advanceTime;
    [SerializeField, Min(1f)] private float dayLengthSeconds = 600f;

    public static AtmosphereController Active { get; private set; }
    public AtmosphereSettings Settings => profile != null ? profile.Settings : settings;
    /// <summary>Hours in [0,24). A simulation clock can drive this without enabling Advance Time.</summary>
    public float TimeOfDay
    {
      get => timeOfDay;
      set { timeOfDay = Mathf.Repeat(value, 24f); if (isActiveAndEnabled) Apply(); }
    }
    public Light Sun => sun;
    public Color CurrentFogColor { get; private set; }
    public Color CurrentSunColor { get; private set; }

    private Material sky;
    private bool ownsSun;
    private Material previousSky;
    private Light previousSun;
    private bool previousFog;
    private AmbientMode previousAmbientMode;
    private Color previousAmbientSky, previousAmbientEquator, previousAmbientGround;
    private Quaternion previousSunRotation;
    private Color previousSunColor;
    private float previousSunIntensity;
    private LightShadows previousShadows;

    private void OnEnable()
    {
      if (Active != null && Active != this)
      {
        Debug.LogWarning("Only one OpenSH2 atmosphere may control scene lighting at a time.", this);
        enabled = false;
        return;
      }
      Active = this;
      previousSky = RenderSettings.skybox;
      previousSun = RenderSettings.sun;
      previousFog = RenderSettings.fog;
      previousAmbientMode = RenderSettings.ambientMode;
      previousAmbientSky = RenderSettings.ambientSkyColor;
      previousAmbientEquator = RenderSettings.ambientEquatorColor;
      previousAmbientGround = RenderSettings.ambientGroundColor;
      if (sun == null) sun = RenderSettings.sun;
      if (sun == null)
        foreach (Light candidate in FindObjectsByType<Light>())
          if (candidate.type == LightType.Directional && candidate.isActiveAndEnabled) { sun = candidate; break; }
      if (sun == null)
      {
        var root = new GameObject("Atmosphere Sun") { hideFlags = HideFlags.HideAndDontSave };
        sun = root.AddComponent<Light>();
        sun.type = LightType.Directional;
        ownsSun = true;
      }
      previousSunRotation = sun.transform.rotation;
      previousSunColor = sun.color;
      previousSunIntensity = sun.intensity;
      previousShadows = sun.shadows;
      Shader shader = Resources.Load<Shader>("OpenSH2Sky");
      if (shader != null) sky = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
      else Debug.LogError("OpenSH2 sky shader is missing from Lighting/Resources.", this);
      Apply();
    }

    private void Update()
    {
      if (Active != this) return;
      if (Application.isPlaying && advanceTime)
        timeOfDay = Mathf.Repeat(timeOfDay + (Game.GetInstance()?.Simulation.DeltaTime ?? Time.deltaTime) * 24f / Mathf.Max(1f, dayLengthSeconds), 24f);
      Apply();
    }

    public void Apply()
    {
      if (Active != this || sun == null || Settings == null) return;
      AtmosphereSettings s = Settings;
      float t = Mathf.Repeat(timeOfDay, 24f) / 24f;
      float elevation = Mathf.Sin((t - 0.25f) * Mathf.PI * 2f);
      float daylight = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.12f, 0.35f, elevation));
      float sunset = (1f - Mathf.SmoothStep(0f, 0.4f, Mathf.Abs(elevation))) * daylight;
      sun.transform.rotation = Quaternion.Euler(t * 360f - 90f, s.SunAzimuth, 0f);
      sun.color = s.SunColor.Evaluate(Mathf.Clamp01(elevation));
      sun.intensity = Mathf.Max(0f, s.SunStrength.Evaluate(t) * s.SunIntensity);
      sun.shadows = LightShadows.Soft;
      CurrentSunColor = sun.color.linear * sun.intensity;
      CurrentFogColor = Color.Lerp(s.FogColor.Evaluate(daylight), s.SunsetColor, sunset * 0.45f).linear;
      Shader.SetGlobalColor("_OpenSH2FogColor", CurrentFogColor);
      Shader.SetGlobalVector("_OpenSH2FogParams", new Vector4(s.FogEnabled ? Mathf.Max(0f, s.FogDensity) : 0f,
        s.FogBaseHeight, Mathf.Max(0.001f, s.FogHeightFalloff), Mathf.Max(0f, s.FogStartDistance)));
      Shader.SetGlobalVector("_OpenSH2FogLimits", new Vector4(Mathf.Max(1f, s.MaxDistance), Mathf.Clamp01(s.MaxFogOpacity), 0f, 0f));
      RenderSettings.sun = sun;
      // The depth-based pass handles fog for every opaque shader consistently.
      RenderSettings.fog = false;
      RenderSettings.ambientMode = AmbientMode.Trilight;
      Color ambient = s.AmbientColor.Evaluate(daylight) * s.AmbientIntensity;
      RenderSettings.ambientSkyColor = ambient;
      RenderSettings.ambientEquatorColor = Color.Lerp(ambient, s.SunsetColor * s.AmbientIntensity, sunset * 0.25f);
      RenderSettings.ambientGroundColor = ambient * 0.35f;
      if (sky == null) return;
      sky.SetColor("_Zenith", s.ZenithColor.Evaluate(daylight).linear);
      sky.SetColor("_Horizon", s.HorizonColor.Evaluate(daylight).linear);
      sky.SetColor("_Sunset", s.SunsetColor.linear);
      sky.SetColor("_SunColor", CurrentSunColor);
      sky.SetVector("_SunDirection", -sun.transform.forward);
      sky.SetFloat("_SunsetStrength", sunset);
      sky.SetFloat("_SunSize", Mathf.Max(0.1f, s.SunDiscSize) * Mathf.Deg2Rad);
      RenderSettings.skybox = sky;
    }

    private void OnDisable()
    {
      if (Active != this) return;
      Active = null;
      Shader.SetGlobalVector("_OpenSH2FogParams", Vector4.zero);
      RenderSettings.skybox = previousSky;
      RenderSettings.sun = previousSun;
      RenderSettings.fog = previousFog;
      RenderSettings.ambientMode = previousAmbientMode;
      RenderSettings.ambientSkyColor = previousAmbientSky;
      RenderSettings.ambientEquatorColor = previousAmbientEquator;
      RenderSettings.ambientGroundColor = previousAmbientGround;
      if (sun != null)
      {
        sun.transform.rotation = previousSunRotation;
        sun.color = previousSunColor;
        sun.intensity = previousSunIntensity;
        sun.shadows = previousShadows;
        if (ownsSun) { Release(sun.gameObject); sun = null; ownsSun = false; }
      }
      if (sky != null) { Release(sky); sky = null; }
    }

    private static void Release(Object obj)
    {
      if (Application.isPlaying) Destroy(obj);
      else DestroyImmediate(obj);
    }
  }
}
