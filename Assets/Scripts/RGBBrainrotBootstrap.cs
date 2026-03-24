using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
#if UNITY_EDITOR
using UnityEditor;
#endif

[DefaultExecutionOrder(-9000)]
public sealed class RGBBrainrotBootstrap : MonoBehaviour
{
    private sealed class SceneColorTarget
    {
        public Renderer Renderer;
        public bool HasBaseColor;
        public bool HasColor;
        public bool HasEmissiveColor;
        public bool HasEmissionColor;
        public float HueOffset;
        public float HueWaveSpeed;
        public float SaturationMultiplier;
        public float ValueMultiplier;
    }

    private static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorProperty = Shader.PropertyToID("_Color");
    private static readonly int EmissiveColorProperty = Shader.PropertyToID("_EmissiveColor");
    private static readonly int EmissionColorProperty = Shader.PropertyToID("_EmissionColor");

    [Header("Hierarchy")]
    [SerializeField] private string generatedRootName = "RGB Brainrot Generated";
    [SerializeField] private bool rebuildOnPlay = false;

    [Header("Psychedelic")]
    [SerializeField] private Vector3 worldSpinAxis = new(0.35f, 1f, 0.65f);
    [SerializeField] private float worldSpinSpeed = 34f;
    [SerializeField] private float hueCycleSpeed = 0.42f;
    [SerializeField] private float cameraRollStrength = 32f;
    [SerializeField] private float cameraShakeStrength = 0.85f;
    [SerializeField] private bool colorizeWholeScene = true;
    [SerializeField] [Range(0f, 1f)] private float globalSaturation = 0.98f;
    [SerializeField] [Range(0f, 1f)] private float globalValue = 1f;
    [SerializeField] private float globalEmissionBoost = 4f;
    [SerializeField] private float sceneRescanDelay = 2f;

    [Header("Chaos")]
    [SerializeField] private int spawnCount = 180;
    [SerializeField] private float spawnRadius = 55f;
    [SerializeField] private float verticalSpread = 22f;
    [SerializeField] private float baseSpeed = 1.15f;
    [SerializeField] private bool autoCreateCamera = true;

    [Header("Lighting")]
    [SerializeField] private int lightCount = 12;
    [SerializeField] private float lightRingRadius = 42f;
    [SerializeField] private float lightHeight = 12f;

    [Header("Audio")]
    [SerializeField] private AudioClip musicClip;
    [SerializeField] private bool autoFindHardstyleClip = true;
    [SerializeField] private string autoFindClipName = "hardstyle";
    [SerializeField] [Range(0f, 1f)] private float musicVolume = 0.85f;
    [SerializeField] private bool loopMusic = true;

    private readonly List<RGBBrainrotNode> nodes = new();
    private readonly List<Light> rgbLights = new();
    private readonly List<SceneColorTarget> sceneColorTargets = new();
    private readonly MaterialPropertyBlock sceneColorBlock = new();
    private Camera mainCamera;
    private AudioSource musicSource;
    private Transform cameraPivot;
    private Transform generatedRoot;
    private Vector3 generatedRootBaseScale = Vector3.one;
    private float nextSceneRescanTime;
    private float speedMultiplier = 1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Inject()
    {
        if (FindFirstObjectByType<RGBBrainrotBootstrap>() != null)
        {
            return;
        }

        GameObject host = new("RGB Brainrot Bootstrap");
        host.AddComponent<RGBBrainrotBootstrap>();
    }

    private void Awake()
    {
        CreateCameraIfNeeded();
        SetupAudio();
        SetupScene(rebuildOnPlay);
    }

    private void SetupAudio()
    {
        if (musicSource == null)
        {
            musicSource = GetComponent<AudioSource>();
            if (musicSource == null)
            {
                musicSource = gameObject.AddComponent<AudioSource>();
            }
        }

        if (musicClip == null && autoFindHardstyleClip)
        {
            musicClip = FindMusicClipByName();
        }

        musicSource.playOnAwake = false;
        musicSource.loop = loopMusic;
        musicSource.spatialBlend = 0f;
        musicSource.volume = Mathf.Clamp01(musicVolume);
        musicSource.clip = musicClip;

        if (musicClip != null && !musicSource.isPlaying)
        {
            musicSource.Play();
        }
    }

    private AudioClip FindMusicClipByName()
    {
        string needle = string.IsNullOrWhiteSpace(autoFindClipName) ? "hardstyle" : autoFindClipName;

        AudioClip[] loadedClips = Resources.FindObjectsOfTypeAll<AudioClip>();
        for (int i = 0; i < loadedClips.Length; i++)
        {
            AudioClip clip = loadedClips[i];
            if (clip != null && clip.name.Contains(needle, System.StringComparison.OrdinalIgnoreCase))
            {
                return clip;
            }
        }

#if UNITY_EDITOR
        string[] guids = AssetDatabase.FindAssets($"{needle} t:AudioClip");
        if (guids.Length > 0)
        {
            string clipPath = AssetDatabase.GUIDToAssetPath(guids[0]);
            AudioClip editorClip = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);
            if (editorClip != null)
            {
                return editorClip;
            }
        }
#endif

        return null;
    }

    private void CreateCameraIfNeeded()
    {
        mainCamera = Camera.main;
        if (mainCamera != null || !autoCreateCamera)
        {
            return;
        }

        GameObject camObj = new("Main Camera");
        camObj.tag = "MainCamera";
        mainCamera = camObj.AddComponent<Camera>();
        mainCamera.transform.position = new Vector3(0f, 12f, -45f);
        mainCamera.transform.rotation = Quaternion.Euler(8f, 0f, 0f);
    }

    private void SetupScene(bool forceRebuild)
    {
        if (mainCamera == null)
        {
            return;
        }

        mainCamera.clearFlags = CameraClearFlags.SolidColor;
        mainCamera.backgroundColor = Color.black;
        mainCamera.fieldOfView = 75f;

        bool foundExisting = TryCollectGeneratedContent();
        if (!foundExisting || forceRebuild)
        {
            BuildGeneratedContent();
        }

        CacheSceneColorTargets();
        nextSceneRescanTime = 0f;
    }

    private bool TryCollectGeneratedContent()
    {
        nodes.Clear();
        rgbLights.Clear();
        generatedRoot = transform.Find(generatedRootName);
        if (generatedRoot == null)
        {
            return false;
        }

        generatedRootBaseScale = generatedRoot.localScale == Vector3.zero ? Vector3.one : generatedRoot.localScale;

        RGBBrainrotNode[] existingNodes = generatedRoot.GetComponentsInChildren<RGBBrainrotNode>(true);
        for (int i = 0; i < existingNodes.Length; i++)
        {
            nodes.Add(existingNodes[i]);
        }

        Light[] lights = generatedRoot.GetComponentsInChildren<Light>(true);
        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i].type == LightType.Point)
            {
                rgbLights.Add(lights[i]);
            }
        }

        cameraPivot = generatedRoot.Find("RGB Camera Pivot");
        if (cameraPivot == null)
        {
            cameraPivot = new GameObject("RGB Camera Pivot").transform;
            cameraPivot.SetParent(generatedRoot, false);
        }

        return nodes.Count > 0 && rgbLights.Count > 0;
    }

    private void BuildGeneratedContent()
    {
        nodes.Clear();
        rgbLights.Clear();
        generatedRoot = GetOrCreateGeneratedRoot(clearExisting: true);
        generatedRoot.localScale = Vector3.one;
        generatedRootBaseScale = generatedRoot.localScale;

        cameraPivot = new GameObject("RGB Camera Pivot").transform;
        cameraPivot.SetParent(generatedRoot, false);
        cameraPivot.position = Vector3.zero;

        CreateChaosNodes();
        CreateLights();
    }

    private Transform GetOrCreateGeneratedRoot(bool clearExisting)
    {
        Transform root = transform.Find(generatedRootName);
        if (root == null)
        {
            GameObject rootObj = new(generatedRootName);
            root = rootObj.transform;
            root.SetParent(transform, false);
        }

        if (clearExisting)
        {
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                DestroySafe(root.GetChild(i).gameObject);
            }
        }

        return root;
    }

    private static void DestroySafe(Object target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }

    private void CreateChaosNodes()
    {
        Shader shader = FindBestLitShader();
        if (shader == null)
        {
            Debug.LogWarning("RGB Brainrot: no compatible shader found.");
            return;
        }

        Material sharedMaterial = new(shader);
        sharedMaterial.enableInstancing = true;
        if (sharedMaterial.HasProperty("_Smoothness"))
        {
            sharedMaterial.SetFloat("_Smoothness", 0.1f);
        }

        if (sharedMaterial.HasProperty("_Metallic"))
        {
            sharedMaterial.SetFloat("_Metallic", 0f);
        }

        sharedMaterial.EnableKeyword("_EMISSION");

        for (int i = 0; i < spawnCount; i++)
        {
            PrimitiveType primitiveType = (PrimitiveType)(i % 4);
            GameObject nodeObj = GameObject.CreatePrimitive(primitiveType);
            nodeObj.name = $"RGB Node {i:000}";

            Vector3 randomSphere = Random.onUnitSphere * Random.Range(8f, spawnRadius);
            randomSphere.y = Random.Range(-verticalSpread, verticalSpread);
            nodeObj.transform.position = randomSphere;
            nodeObj.transform.rotation = Random.rotation;
            nodeObj.transform.localScale = Vector3.one * Random.Range(0.7f, 3.8f);
            nodeObj.transform.SetParent(generatedRoot, true);

            Renderer renderer = nodeObj.GetComponent<Renderer>();
            renderer.sharedMaterial = sharedMaterial;

            if (nodeObj.TryGetComponent(out Collider collider))
            {
                collider.enabled = false;
            }

            RGBBrainrotNode node = nodeObj.AddComponent<RGBBrainrotNode>();
            node.Initialize(i * 0.073f);
            nodes.Add(node);
        }
    }

    private void CreateLights()
    {
        for (int i = 0; i < lightCount; i++)
        {
            float ratio = i / (float)lightCount;
            float angle = ratio * Mathf.PI * 2f;
            Vector3 position = new(
                Mathf.Cos(angle) * lightRingRadius,
                lightHeight + Mathf.Sin(angle * 3f) * 4f,
                Mathf.Sin(angle) * lightRingRadius
            );

            GameObject lightObj = new($"RGB Light {i:00}");
            lightObj.transform.position = position;
            lightObj.transform.SetParent(cameraPivot, false);

            Light lightComponent = lightObj.AddComponent<Light>();
            lightComponent.type = LightType.Point;
            lightComponent.range = 65f;
            lightComponent.intensity = 1800f;
            lightComponent.shadows = LightShadows.None;
            rgbLights.Add(lightComponent);
        }
    }

    private void CacheSceneColorTargets()
    {
        sceneColorTargets.Clear();

        Renderer[] renderers = FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            Material[] materials = renderer.sharedMaterials;
            if (materials == null || materials.Length == 0)
            {
                continue;
            }

            bool hasBaseColor = false;
            bool hasColor = false;
            bool hasEmissiveColor = false;
            bool hasEmissionColor = false;
            for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            {
                Material material = materials[materialIndex];
                if (material == null)
                {
                    continue;
                }

                hasBaseColor |= material.HasProperty(BaseColorProperty);
                hasColor |= material.HasProperty(ColorProperty);
                hasEmissiveColor |= material.HasProperty(EmissiveColorProperty);
                hasEmissionColor |= material.HasProperty(EmissionColorProperty);
            }

            if (!hasBaseColor && !hasColor && !hasEmissiveColor && !hasEmissionColor)
            {
                continue;
            }

            sceneColorTargets.Add(new SceneColorTarget
            {
                Renderer = renderer,
                HasBaseColor = hasBaseColor,
                HasColor = hasColor,
                HasEmissiveColor = hasEmissiveColor,
                HasEmissionColor = hasEmissionColor,
                HueOffset = Random.value,
                HueWaveSpeed = Random.Range(1.5f, 6.5f),
                SaturationMultiplier = Random.Range(0.75f, 1f),
                ValueMultiplier = Random.Range(0.8f, 1f)
            });
        }
    }

    private void ApplyGlobalSceneColorization(float time)
    {
        if (!colorizeWholeScene)
        {
            return;
        }

        float saturation = Mathf.Clamp01(globalSaturation);
        float value = Mathf.Clamp01(globalValue);
        for (int i = 0; i < sceneColorTargets.Count; i++)
        {
            SceneColorTarget target = sceneColorTargets[i];
            if (target.Renderer == null)
            {
                continue;
            }

            float hue = Mathf.Repeat(
                time * hueCycleSpeed * 1.4f + target.HueOffset + Mathf.Sin(time * target.HueWaveSpeed + target.HueOffset * 20f) * 0.24f,
                1f
            );
            Color mainColor = Color.HSVToRGB(
                hue,
                Mathf.Clamp01(saturation * target.SaturationMultiplier),
                Mathf.Clamp01(value * target.ValueMultiplier)
            );
            Color emissionColor = mainColor * (globalEmissionBoost + Mathf.Sin(time * 9f + target.HueOffset * 40f) * 1.4f);

            sceneColorBlock.Clear();
            if (target.HasBaseColor)
            {
                sceneColorBlock.SetColor(BaseColorProperty, mainColor);
            }

            if (target.HasColor)
            {
                sceneColorBlock.SetColor(ColorProperty, mainColor);
            }

            if (target.HasEmissiveColor)
            {
                sceneColorBlock.SetColor(EmissiveColorProperty, emissionColor);
            }

            if (target.HasEmissionColor)
            {
                sceneColorBlock.SetColor(EmissionColorProperty, emissionColor);
            }

            target.Renderer.SetPropertyBlock(sceneColorBlock);
        }

        RenderSettings.ambientLight = Color.HSVToRGB(Mathf.Repeat(time * hueCycleSpeed * 0.9f, 1f), 0.85f, 0.35f);
        if (RenderSettings.fog)
        {
            RenderSettings.fogColor = Color.HSVToRGB(Mathf.Repeat(time * hueCycleSpeed + 0.5f, 1f), 0.92f, 0.34f);
        }
    }

    private static Shader FindBestLitShader()
    {
        string[] shaderNames =
        {
            "HDRP/Lit",
            "Universal Render Pipeline/Lit",
            "Standard"
        };

        foreach (string shaderName in shaderNames)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader != null)
            {
                return shader;
            }
        }

        return Shader.Find("Sprites/Default");
    }

    private void Update()
    {
        if (mainCamera == null)
        {
            return;
        }

        if (cameraPivot == null)
        {
            return;
        }

        if (generatedRoot == null)
        {
            return;
        }

        if (IsTurboTogglePressed())
        {
            speedMultiplier = speedMultiplier > 1f ? 1f : 2.65f;
        }

        if (musicSource != null)
        {
            musicSource.loop = loopMusic;
            musicSource.volume = Mathf.Clamp01(musicVolume);
            if (musicClip != null && musicSource.clip != musicClip)
            {
                musicSource.clip = musicClip;
                musicSource.Play();
            }
            else if (musicClip != null && !musicSource.isPlaying)
            {
                musicSource.Play();
            }
        }

        float time = Time.time * baseSpeed * speedMultiplier;
        if (Time.time >= nextSceneRescanTime)
        {
            CacheSceneColorTargets();
            nextSceneRescanTime = Time.time + Mathf.Max(0.25f, sceneRescanDelay);
        }

        float shakeX = (Mathf.PerlinNoise(time * 7.2f, 0f) - 0.5f) * cameraShakeStrength;
        float shakeY = (Mathf.PerlinNoise(0f, time * 8.4f) - 0.5f) * cameraShakeStrength;
        float shakeZ = (Mathf.PerlinNoise(time * 5.9f, time * 6.7f) - 0.5f) * cameraShakeStrength;
        Vector3 cameraShake = new(shakeX, shakeY, shakeZ);

        if (worldSpinAxis.sqrMagnitude < 0.001f)
        {
            worldSpinAxis = Vector3.up;
        }

        generatedRoot.Rotate(
            worldSpinAxis.normalized,
            worldSpinSpeed * (1.2f + Mathf.Sin(time * 0.85f) * 0.55f) * speedMultiplier * Time.deltaTime,
            Space.World
        );
        float rootPulse = 1f + Mathf.Sin(time * 2.8f) * 0.08f + Mathf.Sin(time * 5.4f + 1.2f) * 0.03f;
        generatedRoot.localScale = generatedRootBaseScale * rootPulse;

        cameraPivot.localPosition = new Vector3(
            Mathf.Sin(time * 1.2f) * 6f,
            Mathf.Sin(time * 0.8f) * 2.5f,
            Mathf.Cos(time * 1.5f) * 6f
        );
        cameraPivot.localRotation = Quaternion.Euler(
            Mathf.Sin(time * 1.7f) * 20f,
            time * 70f,
            Mathf.Cos(time * 1.2f) * 20f
        );

        float cameraRadius = 52f + Mathf.Sin(time * 0.7f) * 6.5f;
        Vector3 cameraPosition = new(
            Mathf.Cos(time * 0.55f) * cameraRadius,
            11f + Mathf.Sin(time * 1.3f) * 5.5f,
            Mathf.Sin(time * 0.55f) * cameraRadius
        );

        mainCamera.transform.position = cameraPosition + cameraShake;
        mainCamera.transform.LookAt(cameraPivot.position + Vector3.up * Mathf.Sin(time * 2f) * 3f);
        mainCamera.transform.Rotate(Vector3.forward, Mathf.Sin(time * 7.5f) * cameraRollStrength);
        mainCamera.transform.Rotate(Vector3.right, Mathf.Sin(time * 10.5f) * 4f);
        mainCamera.fieldOfView = 72f + Mathf.Sin(time * 4.8f) * 20f;

        float backgroundHue = Mathf.Repeat(time * hueCycleSpeed + Mathf.Sin(time * 0.9f) * 0.15f, 1f);
        mainCamera.backgroundColor = Color.HSVToRGB(backgroundHue, 0.92f, 0.36f);

        for (int i = 0; i < nodes.Count; i++)
        {
            nodes[i].Tick(time, speedMultiplier);
        }

        for (int i = 0; i < rgbLights.Count; i++)
        {
            float hue = Mathf.Repeat(
                (i / (float)rgbLights.Count) + time * hueCycleSpeed * 1.6f + Mathf.Sin(time * 2f + i) * 0.08f,
                1f
            );
            Color color = Color.HSVToRGB(hue, 1f, 1f) * (3.5f + Mathf.Sin(time * 10f + i * 2f) * 1.2f);
            rgbLights[i].color = color;
            rgbLights[i].intensity = Mathf.Max(
                300f,
                1500f + Mathf.Sin(time * 8f + i * 1.3f) * 900f + Mathf.Cos(time * 13f + i) * 450f
            );
            rgbLights[i].range = Mathf.Max(10f, 45f + Mathf.Sin(time * 5f + i) * 18f);
        }

        ApplyGlobalSceneColorization(time);
    }

    private static bool IsTurboTogglePressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        return keyboard != null && keyboard.spaceKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.Space);
#else
        return false;
#endif
    }

    private void OnGUI()
    {
        GUIStyle labelStyle = new(GUI.skin.label)
        {
            fontSize = 20,
            normal = { textColor = Color.white }
        };

        GUI.Label(
            new Rect(14f, 14f, 900f, 40f),
            $"RGB BRAINROT MODE  |  SPACE = TURBO {(speedMultiplier > 1f ? "ON" : "OFF")}",
            labelStyle
        );
    }

    [ContextMenu("Generate In Hierarchy (Editor)")]
    public void GenerateInHierarchyEditor()
    {
        if (Application.isPlaying)
        {
            return;
        }

        TryAutoAssignMusicClipInEditor();
        CreateCameraIfNeeded();
        SetupScene(forceRebuild: true);
    }

    [ContextMenu("Clear Generated Content (Editor)")]
    public void ClearGeneratedInHierarchyEditor()
    {
        if (Application.isPlaying)
        {
            return;
        }

        Transform root = transform.Find(generatedRootName);
        if (root != null)
        {
            DestroySafe(root.gameObject);
        }

        generatedRoot = null;
        cameraPivot = null;
        nodes.Clear();
        rgbLights.Clear();
    }

#if UNITY_EDITOR
    public void TryAutoAssignMusicClipInEditor()
    {
        if (!autoFindHardstyleClip)
        {
            return;
        }

        AudioClip found = FindMusicClipByName();
        if (found == null || musicClip == found)
        {
            return;
        }

        musicClip = found;
        EditorUtility.SetDirty(this);
    }
#endif
}

public sealed class RGBBrainrotNode : MonoBehaviour
{
    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorProperty = Shader.PropertyToID("_Color");
    private static readonly int EmissiveColor = Shader.PropertyToID("_EmissiveColor");
    private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

    private readonly MaterialPropertyBlock propertyBlock = new();

    private bool initialized;
    private Renderer cachedRenderer;
    private Vector3 baseLocalPosition;
    private Vector3 baseScale;
    private Vector3 spinAxis;
    private Vector3 orbitAxis;
    private float seed;
    private float phase;
    private float wobbleAmplitude;
    private float spinSpeed;
    private float orbitSpeed;
    private float colorSpeed;
    private float emissiveMultiplier;

    public void Initialize(float initialSeed)
    {
        seed = initialSeed;
        SetupDerivedData();
    }

    private void Awake()
    {
        EnsureInitialized();
    }

    private void OnEnable()
    {
        EnsureInitialized();
    }

    private void EnsureInitialized()
    {
        if (initialized)
        {
            return;
        }

        if (seed <= 0f)
        {
            seed = Random.value * 10f + transform.GetSiblingIndex() * 0.017f;
        }

        SetupDerivedData();
    }

    private void SetupDerivedData()
    {
        baseLocalPosition = transform.localPosition;
        baseScale = transform.localScale;
        spinAxis = Random.onUnitSphere.normalized;
        orbitAxis = Vector3.Lerp(Random.onUnitSphere.normalized, Vector3.up, 0.35f).normalized;
        phase = seed * 17.123f;
        wobbleAmplitude = Mathf.Lerp(0.9f, 2.2f, Mathf.Repeat(seed * 1.13f, 1f));
        spinSpeed = Mathf.Lerp(120f, 360f, Mathf.Repeat(seed * 2.1f, 1f));
        orbitSpeed = Mathf.Lerp(1.2f, 4.5f, Mathf.Repeat(seed * 3.7f, 1f));
        colorSpeed = Mathf.Lerp(0.35f, 0.95f, Mathf.Repeat(seed * 4.9f, 1f));
        emissiveMultiplier = Mathf.Lerp(2.8f, 7.5f, Mathf.Repeat(seed * 5.5f, 1f));
        cachedRenderer = GetComponent<Renderer>();
        initialized = true;
    }

    public void Tick(float globalTime, float speedMultiplier)
    {
        EnsureInitialized();

        float localTime = globalTime + phase;
        float pulse = 1f + Mathf.Sin(localTime * 6.2f) * 0.25f + Mathf.Sin(localTime * 11.3f) * 0.07f;
        float orbitRadius = 0.55f + wobbleAmplitude * 0.5f;
        Vector3 wobble = new(
            Mathf.Sin(localTime * 1.7f + phase) * wobbleAmplitude,
            Mathf.Sin(localTime * 2.9f + phase * 0.5f) * wobbleAmplitude * 0.55f,
            Mathf.Cos(localTime * 2.1f + phase) * wobbleAmplitude
        );
        Quaternion orbitRotation = Quaternion.AngleAxis(localTime * orbitSpeed * 55f, orbitAxis);
        Vector3 orbitOffset = orbitRotation * (Vector3.right * orbitRadius);

        transform.localPosition = baseLocalPosition + wobble + orbitOffset;
        transform.Rotate(spinAxis, Time.deltaTime * spinSpeed * speedMultiplier, Space.Self);
        transform.localScale = baseScale * pulse;

        if (cachedRenderer == null)
        {
            return;
        }

        float hue = Mathf.Repeat(localTime * colorSpeed + seed + Mathf.Sin(localTime * 3.5f) * 0.1f, 1f);
        float saturation = Mathf.Clamp01(0.75f + Mathf.Sin(localTime * 4.1f + seed) * 0.25f);
        float value = Mathf.Clamp01(0.85f + Mathf.Sin(localTime * 5.6f + seed * 2f) * 0.15f);
        Color mainColor = Color.HSVToRGB(hue, saturation, value);
        float emissionPulse = emissiveMultiplier + Mathf.Sin(localTime * 12f + seed) * 2.2f;
        Color emission = mainColor * Mathf.Max(1.2f, emissionPulse);

        propertyBlock.Clear();
        propertyBlock.SetColor(BaseColor, mainColor);
        propertyBlock.SetColor(ColorProperty, mainColor);
        propertyBlock.SetColor(EmissiveColor, emission);
        propertyBlock.SetColor(EmissionColor, emission);
        cachedRenderer.SetPropertyBlock(propertyBlock);
    }
}
