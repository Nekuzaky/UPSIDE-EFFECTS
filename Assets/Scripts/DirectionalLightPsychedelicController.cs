using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

[DefaultExecutionOrder(-8500)]
public sealed class DirectionalLightPsychedelicController : MonoBehaviour
{
    [Header("Auto Setup")]
    [SerializeField] private bool autoFindDirectionalLights = true;
    [SerializeField] private bool createFallbackLightsIfMissing = true;
    [SerializeField] private bool ensureTriDirectionalRig = true;

    [Header("RGB Cycle")]
    [SerializeField] private float hueCycleSpeed = 0.28f;
    [SerializeField] private float hueOffsetPerLight = 0.27f;
    [SerializeField] [Range(0f, 1f)] private float saturation = 1f;
    [SerializeField] [Range(0f, 1f)] private float value = 1f;
    [SerializeField] [Range(0f, 2f)] private float intensityPulseStrength = 0.55f;
    [SerializeField] private float intensityPulseSpeed = 2.8f;

    [Header("Motion")]
    [SerializeField] private bool rotateDirectionalLights = true;
    [SerializeField] private Vector3 lightRotationSpeedEuler = new(5f, 12f, 0f);

    [Header("Background")]
    [SerializeField] private bool driveAmbientAndFog = true;
    [SerializeField] [Range(0f, 2f)] private float ambientIntensity = 1.25f;
    [SerializeField] [Range(0f, 1f)] private float ambientValue = 0.55f;
    [SerializeField] [Range(0f, 1f)] private float fogValue = 0.45f;
    [SerializeField] [Range(0f, 1f)] private float fogSaturation = 1f;
    [SerializeField] private bool forceHdrpSolidColorBackground = true;
    [SerializeField] [Range(0f, 1f)] private float backgroundSaturation = 1f;
    [SerializeField] [Range(0f, 1f)] private float backgroundValue = 0.8f;
    [SerializeField] private float backgroundHueSpeedMultiplier = 1.35f;

    [Header("Targets")]
    [SerializeField] private List<Light> directionalLights = new();

    private readonly List<float> baseIntensities = new();
    private float nextRefreshTime;
    private Camera cachedMainCamera;
    private HDAdditionalCameraData cachedHdCameraData;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureInstance()
    {
        if (FindFirstObjectByType<DirectionalLightPsychedelicController>() != null)
        {
            return;
        }

        GameObject host = GameObject.Find("Lighting");
        if (host == null)
        {
            host = new GameObject("Lighting");
        }

        host.AddComponent<DirectionalLightPsychedelicController>();
    }

    private void Awake()
    {
        CacheMainCamera();
        RefreshDirectionalLights();
    }

    private void OnEnable()
    {
        CacheMainCamera();
        RefreshDirectionalLights();
    }

    private void LateUpdate()
    {
        if ((directionalLights.Count == 0 || Time.time >= nextRefreshTime) && autoFindDirectionalLights)
        {
            RefreshDirectionalLights();
            nextRefreshTime = Time.time + 2f;
        }

        if (directionalLights.Count == 0)
        {
            ApplyBackground(Time.time);
            return;
        }

        float time = Time.time;
        float safeSaturation = Mathf.Clamp01(saturation);
        float safeValue = Mathf.Clamp01(value);

        for (int i = 0; i < directionalLights.Count; i++)
        {
            Light lightComponent = directionalLights[i];
            if (lightComponent == null)
            {
                continue;
            }

            float hue = Mathf.Repeat(
                time * hueCycleSpeed
                + i * hueOffsetPerLight
                + Mathf.Sin(time * 0.8f + i * 1.31f) * 0.08f,
                1f
            );

            Color rgb = Color.HSVToRGB(hue, safeSaturation, safeValue);
            lightComponent.color = rgb;
            lightComponent.useColorTemperature = false;

            float baseIntensity = baseIntensities.Count > i ? baseIntensities[i] : Mathf.Max(1f, lightComponent.intensity);
            float pulse = 1f + Mathf.Sin(time * intensityPulseSpeed + i * 1.17f) * intensityPulseStrength;
            lightComponent.intensity = Mathf.Max(0f, baseIntensity * pulse);

            if (rotateDirectionalLights && lightRotationSpeedEuler.sqrMagnitude > 0.0001f)
            {
                lightComponent.transform.Rotate(lightRotationSpeedEuler * Time.deltaTime, Space.Self);
            }
        }

        ApplyBackground(time);
    }

    [ContextMenu("Refresh Directional Lights")]
    public void RefreshDirectionalLights()
    {
        directionalLights.RemoveAll(lightComponent => lightComponent == null);

        if (autoFindDirectionalLights)
        {
            directionalLights.Clear();
            Light[] lights = FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i].type == LightType.Directional)
                {
                    directionalLights.Add(lights[i]);
                }
            }
        }

        if (directionalLights.Count == 0 && createFallbackLightsIfMissing)
        {
            CreateFallbackLights();
        }
        else if (ensureTriDirectionalRig && directionalLights.Count > 0 && directionalLights.Count < 3)
        {
            CreateSupplementalLights(3 - directionalLights.Count);
        }

        baseIntensities.Clear();
        for (int i = 0; i < directionalLights.Count; i++)
        {
            Light lightComponent = directionalLights[i];
            float baseIntensity = lightComponent != null && lightComponent.intensity > 0.001f
                ? lightComponent.intensity
                : 35000f;
            baseIntensities.Add(baseIntensity);
        }
    }

    private void CreateFallbackLights()
    {
        CreateSupplementalLights(3);
    }

    private void CreateSupplementalLights(int count)
    {
        count = Mathf.Max(0, count);
        for (int i = 0; i < count; i++)
        {
            int index = directionalLights.Count + 1;
            GameObject lightObject = new($"RGB Directional {index}");
            lightObject.transform.SetParent(transform, false);
            lightObject.transform.rotation = Quaternion.Euler(20f + i * 12f, index * 120f, 0f);

            Light lightComponent = lightObject.AddComponent<Light>();
            lightComponent.type = LightType.Directional;
            lightComponent.intensity = 45000f;
            lightComponent.shadows = LightShadows.None;
            lightComponent.useColorTemperature = false;
            directionalLights.Add(lightComponent);
        }
    }

    private void ApplyBackground(float time)
    {
        if (driveAmbientAndFog)
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientIntensity = ambientIntensity;

            Color sky = Color.HSVToRGB(Mathf.Repeat(time * hueCycleSpeed * 0.72f, 1f), 0.95f, Mathf.Clamp01(ambientValue));
            Color equator = Color.HSVToRGB(Mathf.Repeat(time * hueCycleSpeed * 0.72f + 0.33f, 1f), 0.95f, Mathf.Clamp01(ambientValue * 0.8f));
            Color ground = Color.HSVToRGB(Mathf.Repeat(time * hueCycleSpeed * 0.72f + 0.66f, 1f), 0.95f, Mathf.Clamp01(ambientValue * 0.65f));

            RenderSettings.ambientSkyColor = sky;
            RenderSettings.ambientEquatorColor = equator;
            RenderSettings.ambientGroundColor = ground;

            if (RenderSettings.fog)
            {
                Color fog = Color.HSVToRGB(
                    Mathf.Repeat(time * hueCycleSpeed * 0.92f + 0.18f, 1f),
                    Mathf.Clamp01(fogSaturation),
                    Mathf.Clamp01(fogValue)
                );
                RenderSettings.fogColor = fog;
            }
        }

        if (!forceHdrpSolidColorBackground)
        {
            return;
        }

        CacheMainCamera();
        if (cachedMainCamera == null)
        {
            return;
        }

        Color background = Color.HSVToRGB(
            Mathf.Repeat(time * hueCycleSpeed * backgroundHueSpeedMultiplier + 0.12f, 1f),
            Mathf.Clamp01(backgroundSaturation),
            Mathf.Clamp01(backgroundValue)
        );

        cachedMainCamera.clearFlags = CameraClearFlags.SolidColor;
        cachedMainCamera.backgroundColor = background;

        if (cachedHdCameraData != null)
        {
            cachedHdCameraData.clearColorMode = HDAdditionalCameraData.ClearColorMode.Color;
            cachedHdCameraData.backgroundColorHDR = background;
        }
    }

    private void CacheMainCamera()
    {
        if (cachedMainCamera == null)
        {
            cachedMainCamera = Camera.main;
            if (cachedMainCamera == null)
            {
                cachedMainCamera = FindFirstObjectByType<Camera>();
            }
        }

        if (cachedMainCamera != null && cachedHdCameraData == null)
        {
            cachedMainCamera.TryGetComponent(out cachedHdCameraData);
        }
    }
}
