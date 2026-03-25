#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using Mindrift.Checkpoints;
using Mindrift.Core;
using Mindrift.Effects;
using Mindrift.Player;
using Mindrift.UI;
using Mindrift.World;

namespace Mindrift.Editor
{
    public static class MindriftPrototypeBuilder
    {
        private const string RootFolder = "Assets/_MINDRIFT";
        private const string MaterialsFolder = RootFolder + "/Materials";
        private const string ScenesFolder = RootFolder + "/Scenes";
        private const string SettingsFolder = RootFolder + "/Settings";
        private const string ShadersFolder = RootFolder + "/Shaders";
        private const string GamesScenePath = "Assets/Scenes/Games.unity";
        private const string GlobalVolumeProfilePath = SettingsFolder + "/UE_GlobalPsychedelicVolumeProfile.asset";
        private const string SkyFogFallbackProfilePath = SettingsFolder + "/UE_SkyAndFogProfile.asset";
        private const string CustomPassMaterialPath = MaterialsFolder + "/M_PsychedelicCustomPass.mat";
        private const string ExistingSkyFogProfilePath = "Assets/Settings/SkyandFogSettingsProfile.asset";
        private const string CustomPassShaderPath = ShadersFolder + "/PsychedelicFullScreen.shader";

        private sealed class MaterialSet
        {
            public Material BaseNeutral;
            public Material NeonToxic;
            public Material DeepBlack;
            public Material GlowPink;
            public Material GlowCyan;
        }

        private sealed class BuildContext
        {
            public GameManager GameManager;
            public HeightProgressionManager HeightProgressionManager;
            public RunSessionManager RunSessionManager;
            public CheckpointManager CheckpointManager;
            public AudioManager AudioManager;
            public AudioIntensityDriver AudioIntensityDriver;
            public FirstPersonMotor FirstPersonMotor;
            public FirstPersonLook FirstPersonLook;
            public MindriftPlayerInputRouter InputRouter;
#if ENABLE_INPUT_SYSTEM
            public PlayerInput PlayerInput;
#endif
            public PlayerFallRespawn PlayerFallRespawn;
            public GoalZone GoalZone;
            public CameraSideEffects CameraSideEffects;
            public PsychedelicVolumeController PsychedelicVolumeController;
            public PsychedelicCustomPassController PsychedelicCustomPassController;
            public SideEffectEventDirector SideEffectEventDirector;
            public SideEffectUI SideEffectUI;
            public CheckpointUI CheckpointUI;
            public HUDAltitude HUDAltitude;
            public RunHUD RunHUD;
            public Transform PlayerRoot;
            public Transform CameraRoot;
            public Camera MainCamera;
            public readonly List<Checkpoint> Checkpoints = new List<Checkpoint>();
            public readonly List<FakePlatform> FakePlatforms = new List<FakePlatform>();
            public readonly List<Vector3> RoutePoints = new List<Vector3>();
        }

        [MenuItem("Tools/MINDRIFT/Build Or Refresh In Games Scene")]
        public static void BuildPrototypeScene()
        {
            EnsureFoldersExist();

            MaterialSet materials = CreateOrUpdateMaterials();
            Material customPassMaterial = CreateOrUpdateCustomPassMaterial();
            VolumeProfile globalVolumeProfile = CreateOrUpdateGlobalVolumeProfile();
            VolumeProfile skyAndFogProfile = LoadOrCreateSkyAndFogProfile();

            Scene scene;
            if (System.IO.File.Exists(GamesScenePath))
            {
                scene = EditorSceneManager.OpenScene(GamesScenePath, OpenSceneMode.Single);
            }
            else
            {
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }

            GameObject previousRoot = GameObject.Find("MINDRIFT_Prototype");
            if (previousRoot != null)
            {
                UnityEngine.Object.DestroyImmediate(previousRoot);
            }

            BuildContext context = new BuildContext();

            GameObject rootObject = new GameObject("MINDRIFT_Prototype");
            Transform root = rootObject.transform;

            Transform systemsRoot = CreateChild("Systems", root, false);
            Transform playerRigRoot = CreateChild("PlayerRig", root, false);
            Transform worldRoot = CreateChild("World", root, false);
            Transform effectsRoot = CreateChild("Effects", root, false);
            Transform uiRoot = CreateChild("UI", root, false);
            Transform checkpointsRoot = CreateChild("Checkpoints", root, false);
            Transform lightingRoot = CreateChild("Lighting", root, false);
            CreateChild("Debug", root, false);

            CreateSystems(context, systemsRoot);
            CreatePlayerRig(context, playerRigRoot);
            CreateWorldAndCheckpoints(context, worldRoot, checkpointsRoot, materials);
            CreateEffects(context, effectsRoot, globalVolumeProfile, customPassMaterial);
            CreateUI(context, uiRoot);
            CreateLighting(lightingRoot, skyAndFogProfile, materials);

            WireReferences(context);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, GamesScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeGameObject = rootObject;
            Debug.Log($"[MINDRIFT] Prototype scene generated in {GamesScenePath}");
        }

        private static void EnsureFoldersExist()
        {
            string[] folders =
            {
                RootFolder,
                RootFolder + "/Art",
                RootFolder + "/Audio",
                MaterialsFolder,
                RootFolder + "/Prefabs",
                ScenesFolder,
                RootFolder + "/Scripts",
                RootFolder + "/Scripts/Core",
                RootFolder + "/Scripts/Player",
                RootFolder + "/Scripts/World",
                RootFolder + "/Scripts/Effects",
                RootFolder + "/Scripts/UI",
                RootFolder + "/Scripts/Checkpoints",
                RootFolder + "/Settings",
                ShadersFolder
            };

            for (int i = 0; i < folders.Length; i++)
            {
                CreateFolderRecursively(folders[i]);
            }
        }

        private static void CreateFolderRecursively(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            string parent = System.IO.Path.GetDirectoryName(folderPath)?.Replace("\\", "/");
            string folderName = System.IO.Path.GetFileName(folderPath);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                CreateFolderRecursively(parent);
            }

            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(folderPath))
            {
                AssetDatabase.CreateFolder(parent, folderName);
            }
        }

        private static MaterialSet CreateOrUpdateMaterials()
        {
            Shader litShader = Shader.Find("HDRP/Lit");
            if (litShader == null)
            {
                throw new InvalidOperationException("HDRP/Lit shader not found. The project must be in HDRP for this builder.");
            }

            MaterialSet set = new MaterialSet
            {
                BaseNeutral = GetOrCreateLitMaterial(MaterialsFolder + "/M_BaseNeutral.mat", litShader, new Color(0.36f, 0.38f, 0.43f), Color.black),
                NeonToxic = GetOrCreateLitMaterial(MaterialsFolder + "/M_NeonToxic.mat", litShader, new Color(0.24f, 0.95f, 0.29f), new Color(0.28f, 2.3f, 0.55f)),
                DeepBlack = GetOrCreateLitMaterial(MaterialsFolder + "/M_DeepBlack.mat", litShader, new Color(0.02f, 0.02f, 0.03f), new Color(0.01f, 0.01f, 0.02f)),
                GlowPink = GetOrCreateLitMaterial(MaterialsFolder + "/M_GlowPink.mat", litShader, new Color(0.92f, 0.24f, 0.72f), new Color(2.8f, 0.35f, 2.1f)),
                GlowCyan = GetOrCreateLitMaterial(MaterialsFolder + "/M_GlowCyan.mat", litShader, new Color(0.16f, 0.87f, 0.95f), new Color(0.35f, 2.9f, 3.4f))
            };

            return set;
        }

        private static Material GetOrCreateLitMaterial(string path, Shader shader, Color baseColor, Color emissiveColor)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            if (material.shader != shader)
            {
                material.shader = shader;
            }

            SetMaterialColor(material, "_BaseColor", baseColor);
            SetMaterialFloat(material, "_Smoothness", 0.32f);
            SetMaterialFloat(material, "_Metallic", 0f);
            SetMaterialColor(material, "_EmissiveColor", emissiveColor);
            SetMaterialFloat(material, "_UseEmissiveIntensity", 0f);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;

            EditorUtility.SetDirty(material);
            return material;
        }

        private static void SetMaterialColor(Material material, string propertyName, Color value)
        {
            if (material.HasProperty(propertyName))
            {
                material.SetColor(propertyName, value);
            }
        }

        private static void SetMaterialFloat(Material material, string propertyName, float value)
        {
            if (material.HasProperty(propertyName))
            {
                material.SetFloat(propertyName, value);
            }
        }

        private static Material CreateOrUpdateCustomPassMaterial()
        {
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(CustomPassShaderPath);
            if (shader == null)
            {
                shader = Shader.Find("Hidden/MINDRIFT/PsychedelicFullScreen");
            }

            if (shader == null)
            {
                throw new InvalidOperationException("Psychedelic custom pass shader not found.");
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(CustomPassMaterialPath);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, CustomPassMaterialPath);
            }

            material.shader = shader;
            SetMaterialFloat(material, "_UE_Intensity", 0f);
            SetMaterialFloat(material, "_UE_WarpStrength", 0.01f);
            SetMaterialFloat(material, "_UE_RGBSplit", 0.002f);
            SetMaterialFloat(material, "_UE_PulseSpeed", 1.2f);
            SetMaterialFloat(material, "_UE_ScanStrength", 0.1f);
            SetMaterialFloat(material, "_UE_TimeScale", 1f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static VolumeProfile CreateOrUpdateGlobalVolumeProfile()
        {
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(GlobalVolumeProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, GlobalVolumeProfilePath);
            }

            ChromaticAberration chromatic = GetOrCreateVolumeOverride<ChromaticAberration>(profile);
            chromatic.intensity.overrideState = true;
            chromatic.intensity.value = 0.08f;

            LensDistortion lens = GetOrCreateVolumeOverride<LensDistortion>(profile);
            lens.intensity.overrideState = true;
            lens.intensity.value = -0.08f;
            lens.xMultiplier.overrideState = true;
            lens.xMultiplier.value = 1f;
            lens.yMultiplier.overrideState = true;
            lens.yMultiplier.value = 1f;
            lens.scale.overrideState = true;
            lens.scale.value = 1f;

            Bloom bloom = GetOrCreateVolumeOverride<Bloom>(profile);
            bloom.intensity.overrideState = true;
            bloom.intensity.value = 0.08f;
            bloom.threshold.overrideState = true;
            bloom.threshold.value = 1f;
            bloom.scatter.overrideState = true;
            bloom.scatter.value = 0.62f;

            ColorAdjustments color = GetOrCreateVolumeOverride<ColorAdjustments>(profile);
            color.saturation.overrideState = true;
            color.saturation.value = 6f;
            color.contrast.overrideState = true;
            color.contrast.value = 0f;
            color.hueShift.overrideState = true;
            color.hueShift.value = 0f;
            color.postExposure.overrideState = true;
            color.postExposure.value = 0f;

            Vignette vignette = GetOrCreateVolumeOverride<Vignette>(profile);
            vignette.intensity.overrideState = true;
            vignette.intensity.value = 0.05f;
            vignette.smoothness.overrideState = true;
            vignette.smoothness.value = 0.2f;
            vignette.rounded.overrideState = true;
            vignette.rounded.value = true;

            FilmGrain grain = GetOrCreateVolumeOverride<FilmGrain>(profile);
            grain.type.overrideState = true;
            grain.type.value = FilmGrainLookup.Medium3;
            grain.intensity.overrideState = true;
            grain.intensity.value = 0.03f;
            grain.response.overrideState = true;
            grain.response.value = 0.78f;

            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static VolumeProfile LoadOrCreateSkyAndFogProfile()
        {
            VolumeProfile existing = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ExistingSkyFogProfilePath);
            if (existing != null)
            {
                return existing;
            }

            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(SkyFogFallbackProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, SkyFogFallbackProfilePath);
            }

            GetOrCreateVolumeOverride<Fog>(profile);
            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static T GetOrCreateVolumeOverride<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet(out T component))
            {
                component.active = true;
                return component;
            }

            T created = profile.Add<T>(true);
            created.active = true;
            return created;
        }

        private static void CreateSystems(BuildContext context, Transform systemsRoot)
        {
            context.GameManager = CreateChild("GameManager", systemsRoot, false).gameObject.AddComponent<GameManager>();
            context.HeightProgressionManager = CreateChild("HeightProgressionManager", systemsRoot, false).gameObject.AddComponent<HeightProgressionManager>();
            context.CheckpointManager = CreateChild("CheckpointManager", systemsRoot, false).gameObject.AddComponent<CheckpointManager>();
            context.RunSessionManager = CreateChild("RunSessionManager", systemsRoot, false).gameObject.AddComponent<RunSessionManager>();

            Transform audioManagerTransform = CreateChild("AudioManager", systemsRoot, false);
            AudioSource source = audioManagerTransform.gameObject.AddComponent<AudioSource>();
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = 0.35f;

            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/hardstyle.ogg");
            if (clip != null)
            {
                source.clip = clip;
            }

            AudioLowPassFilter lowPass = audioManagerTransform.gameObject.AddComponent<AudioLowPassFilter>();
            lowPass.enabled = true;
            lowPass.cutoffFrequency = 22000f;

            context.AudioManager = audioManagerTransform.gameObject.AddComponent<AudioManager>();
            context.AudioIntensityDriver = audioManagerTransform.gameObject.AddComponent<AudioIntensityDriver>();

            if (context.AudioManager != null)
            {
                SerializedObject audioManagerSO = new SerializedObject(context.AudioManager);
                SetObjectProperty(audioManagerSO, "masterLoopSource", source);
                SetObjectProperty(audioManagerSO, "audioIntensityDriver", context.AudioIntensityDriver);
                SetObjectProperty(audioManagerSO, "defaultLoopClip", clip);
                SetBoolProperty(audioManagerSO, "playOnStart", true);
                audioManagerSO.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void CreatePlayerRig(BuildContext context, Transform playerRigRoot)
        {
            Transform playerRoot = CreateChild("PlayerRoot", playerRigRoot, false);
            playerRoot.position = new Vector3(0f, 2.5f, -2f);
            if (TagExists("Player"))
            {
                playerRoot.gameObject.tag = "Player";
            }

            CharacterController characterController = playerRoot.gameObject.AddComponent<CharacterController>();
            characterController.height = 1.82f;
            characterController.radius = 0.33f;
            characterController.center = new Vector3(0f, 0.91f, 0f);
            characterController.stepOffset = 0.3f;
            characterController.slopeLimit = 45f;

            context.FirstPersonMotor = playerRoot.gameObject.AddComponent<FirstPersonMotor>();
            context.FirstPersonLook = playerRoot.gameObject.AddComponent<FirstPersonLook>();
            context.InputRouter = playerRoot.gameObject.AddComponent<MindriftPlayerInputRouter>();
#if ENABLE_INPUT_SYSTEM
            context.PlayerInput = playerRoot.gameObject.AddComponent<PlayerInput>();
#endif
            context.PlayerFallRespawn = playerRoot.gameObject.AddComponent<PlayerFallRespawn>();

#if ENABLE_INPUT_SYSTEM
            if (context.PlayerInput != null)
            {
                InputActionAsset actionAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
                context.PlayerInput.actions = actionAsset;
                context.PlayerInput.defaultActionMap = "Player";
                context.PlayerInput.neverAutoSwitchControlSchemes = false;
                context.PlayerInput.notificationBehavior = PlayerNotifications.InvokeCSharpEvents;

                SerializedObject inputRouterSO = new SerializedObject(context.InputRouter);
                SetObjectProperty(inputRouterSO, "playerInput", context.PlayerInput);
                SetObjectProperty(inputRouterSO, "fallbackActions", actionAsset);
                inputRouterSO.ApplyModifiedPropertiesWithoutUndo();
            }
#endif

            Transform cameraRoot = CreateChild("CameraRoot", playerRoot, false);
            cameraRoot.localPosition = new Vector3(0f, 1.62f, 0f);
            cameraRoot.localRotation = Quaternion.identity;

            Transform cameraTransform = CreateChild("Main Camera", cameraRoot, false);
            cameraTransform.localPosition = Vector3.zero;
            cameraTransform.localRotation = Quaternion.identity;
            cameraTransform.gameObject.tag = "MainCamera";

            Camera camera = cameraTransform.gameObject.AddComponent<Camera>();
            camera.fieldOfView = 86f;
            camera.nearClipPlane = 0.03f;
            camera.farClipPlane = 1200f;
            camera.allowHDR = true;

            if (cameraTransform.GetComponent<AudioListener>() == null)
            {
                cameraTransform.gameObject.AddComponent<AudioListener>();
            }

            if (cameraTransform.GetComponent<HDAdditionalCameraData>() == null)
            {
                cameraTransform.gameObject.AddComponent<HDAdditionalCameraData>();
            }

            context.CameraSideEffects = cameraTransform.gameObject.AddComponent<CameraSideEffects>();
            context.PlayerRoot = playerRoot;
            context.CameraRoot = cameraRoot;
            context.MainCamera = camera;
        }

        private static void CreateWorldAndCheckpoints(BuildContext context, Transform worldRoot, Transform checkpointsRoot, MaterialSet materials)
        {
            Transform startZone = CreateChild("StartZone", worldRoot, false);
            Transform verticalCourse = CreateChild("VerticalCourse", worldRoot, false);
            Transform midZone = CreateChild("MidZone", worldRoot, false);
            Transform highZone = CreateChild("HighZone", worldRoot, false);
            Transform endTeaseZone = CreateChild("EndTeaseZone", worldRoot, false);

            CreatePrimitive("StartPlatform", PrimitiveType.Cube, startZone, new Vector3(0f, 0f, 0f), new Vector3(14f, 2f, 14f), Quaternion.identity, materials.BaseNeutral, false);
            CreatePrimitive("StartAccent", PrimitiveType.Cube, startZone, new Vector3(0f, 1.25f, 0f), new Vector3(6f, 0.5f, 6f), Quaternion.identity, materials.NeonToxic, false);
            CreatePrimitive("KillPlane", PrimitiveType.Cube, worldRoot, new Vector3(0f, -30f, 60f), new Vector3(260f, 2f, 320f), Quaternion.identity, materials.DeepBlack, false);

            System.Random seeded = new System.Random(901);
            float y = 4.4f;
            const int platformCount = 54;
            List<GameObject> routePlatforms = new List<GameObject>(platformCount);

            for (int i = 0; i < platformCount; i++)
            {
                float t = i / (platformCount - 1f);
                float radius = Mathf.Lerp(4.8f, 8.8f, t);
                float angleRadians = Mathf.Deg2Rad * (i * 28f + Mathf.Sin(i * 0.23f) * 18f);
                float x = Mathf.Cos(angleRadians) * radius;
                float z = i * 1.75f + Mathf.Sin(angleRadians) * radius * 0.62f;
                if (i > 0)
                {
                    y += Mathf.Lerp(1.68f, 2.24f, t);
                }

                Vector3 point = new Vector3(x, y, z);
                context.RoutePoints.Add(point);

                float width = y < 32f ? 5.5f : y < 78f ? 4.4f : 3.1f;
                float depth = width;
                if (y > 82f && i % 6 == 0)
                {
                    width = 1.8f;
                    depth = 6f;
                }

                float tiltX = y > 70f ? Range(seeded, -7f, 7f) : Range(seeded, -2f, 2f);
                float tiltZ = y > 70f ? Range(seeded, -7f, 7f) : Range(seeded, -2f, 2f);
                Quaternion rotation = Quaternion.Euler(tiltX, (i * 17f) % 360f, tiltZ);

                Transform zoneParent = y < 22f
                    ? verticalCourse
                    : y < 68f
                        ? midZone
                        : y < 118f
                            ? highZone
                            : endTeaseZone;

                Material mat = i % 9 == 0
                    ? materials.NeonToxic
                    : y > 90f && i % 5 == 0 ? materials.DeepBlack : materials.BaseNeutral;

                GameObject platform = CreatePrimitive(
                    $"Route_{i:00}",
                    PrimitiveType.Cube,
                    zoneParent,
                    point,
                    new Vector3(width, 0.85f, depth),
                    rotation,
                    mat,
                    false);

                routePlatforms.Add(platform);

                if (i % 5 == 0)
                {
                    Vector3 supportScale = new Vector3(0.7f, Mathf.Max(5f, point.y * 0.5f), 0.7f);
                    Vector3 supportPos = new Vector3(point.x, point.y * 0.5f - 0.5f, point.z);
                    CreatePrimitive($"Support_{i:00}", PrimitiveType.Cylinder, zoneParent, supportPos, supportScale, Quaternion.identity, materials.DeepBlack, false);
                }
            }

            int[] movingIndices = { 9, 21, 34, 45 };
            for (int i = 0; i < movingIndices.Length; i++)
            {
                int index = movingIndices[i];
                if (index < 0 || index >= routePlatforms.Count)
                {
                    continue;
                }

                MovingPlatform moving = routePlatforms[index].AddComponent<MovingPlatform>();
                ConfigureMovingPlatform(moving, i % 2 == 0 ? Vector3.right : Vector3.forward, 1.6f + i * 0.55f, 2.8f - i * 0.35f, i * 0.32f);
            }

            int[] fakeIndices = { 12, 19, 27, 36, 43, 50 };
            for (int i = 0; i < fakeIndices.Length; i++)
            {
                int routeIndex = fakeIndices[i];
                if (routeIndex < 1 || routeIndex >= context.RoutePoints.Count - 1)
                {
                    continue;
                }

                Vector3 point = context.RoutePoints[routeIndex];
                Vector3 routeDirection = (context.RoutePoints[routeIndex + 1] - context.RoutePoints[routeIndex - 1]).normalized;
                Vector3 side = Vector3.Cross(Vector3.up, routeDirection).normalized;
                if (side.sqrMagnitude < 0.001f)
                {
                    side = Vector3.right;
                }

                float lateral = Range(seeded, 3.5f, 5.8f);
                float lift = Range(seeded, 0.2f, 1f);
                Vector3 fakePosition = point + side * lateral + Vector3.up * lift;
                Material fakeMaterial = i % 2 == 0 ? materials.GlowPink : materials.GlowCyan;

                GameObject fakeObject = CreatePrimitive(
                    $"FakeRoute_{routeIndex:00}",
                    PrimitiveType.Cube,
                    point.y < 68f ? midZone : highZone,
                    fakePosition,
                    new Vector3(3.2f, 0.75f, 3.2f),
                    Quaternion.Euler(Range(seeded, -8f, 8f), Range(seeded, 0f, 360f), Range(seeded, -8f, 8f)),
                    fakeMaterial,
                    false);

                FakePlatform fakePlatform = fakeObject.AddComponent<FakePlatform>();
                ConfigureFakePlatform(fakePlatform, 0.72f + i * 0.02f, 0.4f + i * 0.05f);
                context.FakePlatforms.Add(fakePlatform);

                if (i % 2 == 0)
                {
                    PlatformFlicker flicker = fakeObject.AddComponent<PlatformFlicker>();
                    SerializedObject flickerSO = new SerializedObject(flicker);
                    SetFloatProperty(flickerSO, "baseFrequency", 8f);
                    SetFloatProperty(flickerSO, "maxFrequency", 18f);
                    flickerSO.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            for (int i = 28; i < 46; i += 3)
            {
                if (i + 1 >= context.RoutePoints.Count)
                {
                    continue;
                }

                Vector3 a = context.RoutePoints[i];
                Vector3 b = context.RoutePoints[i + 1];
                Vector3 delta = b - a;
                float length = Mathf.Max(3f, delta.magnitude + 0.5f);
                Vector3 beamPos = (a + b) * 0.5f + Vector3.up * 0.2f;
                Quaternion beamRot = Quaternion.LookRotation(delta.normalized, Vector3.up);

                CreatePrimitive($"NarrowBeam_{i:00}", PrimitiveType.Cube, highZone, beamPos, new Vector3(1.25f, 0.42f, length), beamRot, materials.DeepBlack, false);
            }

            CreateTeaserZone(endTeaseZone, materials, context.RoutePoints[^1]);

            for (int i = 0; i < 4; i++)
            {
                float ringHeight = 18f + i * 28f;
                CreateRingFrame(highZone, $"Ring_{i:00}", new Vector3(0f, ringHeight, 42f + i * 12f), 14f + i * 1.8f, materials.GlowCyan);
            }

            Vector3 cp0Pos = new Vector3(0f, 2.2f, 0f);
            Vector3 cp1Pos = context.RoutePoints[Mathf.Min(11, context.RoutePoints.Count - 1)] + Vector3.up * 1.4f;
            Vector3 cp2Pos = context.RoutePoints[Mathf.Min(25, context.RoutePoints.Count - 1)] + Vector3.up * 1.4f;
            Vector3 cp3Pos = context.RoutePoints[Mathf.Min(40, context.RoutePoints.Count - 1)] + Vector3.up * 1.4f;

            context.Checkpoints.Add(CreateCheckpoint("CP_00_Start", "Start", 0, cp0Pos, checkpointsRoot, materials.GlowCyan));
            context.Checkpoints.Add(CreateCheckpoint("CP_01_Low", "Low", 1, cp1Pos, checkpointsRoot, materials.GlowCyan));
            context.Checkpoints.Add(CreateCheckpoint("CP_02_Mid", "Mid", 2, cp2Pos, checkpointsRoot, materials.GlowPink));
            context.Checkpoints.Add(CreateCheckpoint("CP_03_High", "High", 3, cp3Pos, checkpointsRoot, materials.GlowPink));

            Vector3 goalPosition = context.RoutePoints[^1] + new Vector3(0f, 10f, 12f);
            GameObject goalObject = new GameObject("GoalZone");
            goalObject.transform.SetParent(endTeaseZone, false);
            goalObject.transform.position = goalPosition;
            BoxCollider goalCollider = goalObject.AddComponent<BoxCollider>();
            goalCollider.isTrigger = true;
            goalCollider.size = new Vector3(9f, 6f, 9f);
            context.GoalZone = goalObject.AddComponent<GoalZone>();
        }

        private static void CreateEffects(BuildContext context, Transform effectsRoot, VolumeProfile globalVolumeProfile, Material customPassMaterial)
        {
            Transform globalVolumeObject = CreateChild("Global Volume", effectsRoot, false);
            Volume globalVolume = globalVolumeObject.gameObject.AddComponent<Volume>();
            globalVolume.isGlobal = true;
            globalVolume.priority = 200f;
            globalVolume.sharedProfile = globalVolumeProfile;

            context.PsychedelicVolumeController = globalVolumeObject.gameObject.AddComponent<PsychedelicVolumeController>();

            Transform customPassObject = CreateChild("Psychedelic Custom Pass Volume", effectsRoot, false);
            CustomPassVolume customPassVolume = customPassObject.gameObject.AddComponent<CustomPassVolume>();
            customPassVolume.isGlobal = true;
            customPassVolume.injectionPoint = CustomPassInjectionPoint.BeforePostProcess;
            customPassVolume.priority = 150f;

            context.PsychedelicCustomPassController = customPassObject.gameObject.AddComponent<PsychedelicCustomPassController>();

            SerializedObject customPassControllerSO = new SerializedObject(context.PsychedelicCustomPassController);
            SetObjectProperty(customPassControllerSO, "customPassVolume", customPassVolume);
            SetObjectProperty(customPassControllerSO, "fullscreenEffectMaterial", customPassMaterial);
            customPassControllerSO.ApplyModifiedPropertiesWithoutUndo();
            context.PsychedelicCustomPassController.EnsurePass();
        }

        private static void CreateUI(BuildContext context, Transform uiRoot)
        {
            Transform canvasTransform = CreateChild("GameplayCanvas", uiRoot, true);
            Canvas canvas = canvasTransform.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = false;

            CanvasScaler scaler = canvasTransform.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasTransform.gameObject.AddComponent<GraphicRaycaster>();

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
            {
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            Text altitudeText = CreateUIText(
                "AltitudeText",
                canvasTransform,
                font,
                26,
                TextAnchor.UpperLeft,
                new Color(0.63f, 0.98f, 1f, 0.95f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(26f, -22f),
                new Vector2(520f, 56f),
                "ALTITUDE: 000.0 m");

            Text sideEffectText = CreateUIText(
                "SideEffectsText",
                canvasTransform,
                font,
                24,
                TextAnchor.UpperRight,
                new Color(1f, 0.52f, 0.85f, 0.96f),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-28f, -22f),
                new Vector2(560f, 56f),
                "SIDE EFFECTS: Stable");

            Text checkpointText = CreateUIText(
                "CheckpointText",
                canvasTransform,
                font,
                28,
                TextAnchor.UpperCenter,
                new Color(0.95f, 0.99f, 1f, 0.96f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -70f),
                new Vector2(760f, 72f),
                string.Empty);

            CanvasGroup checkpointCanvasGroup = checkpointText.gameObject.AddComponent<CanvasGroup>();
            checkpointCanvasGroup.alpha = 0f;

            GameObject warningFlashObject = CreateChild("WarningFlash", canvasTransform, true).gameObject;
            RectTransform warningFlashRect = warningFlashObject.GetComponent<RectTransform>();
            warningFlashRect.anchorMin = Vector2.zero;
            warningFlashRect.anchorMax = Vector2.one;
            warningFlashRect.offsetMin = Vector2.zero;
            warningFlashRect.offsetMax = Vector2.zero;
            Image warningFlashImage = warningFlashObject.AddComponent<Image>();
            warningFlashImage.color = new Color(1f, 0.12f, 0.2f, 0f);
            warningFlashImage.raycastTarget = false;

            Text warningText = CreateUIText(
                "WarningText",
                warningFlashObject.transform,
                font,
                38,
                TextAnchor.MiddleCenter,
                new Color(1f, 0.35f, 0.35f, 1f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, 0f),
                new Vector2(1200f, 120f),
                string.Empty);

            Text neuralLoadText = CreateUIText(
                "NeuralLoadText",
                canvasTransform,
                font,
                18,
                TextAnchor.LowerRight,
                new Color(0.65f, 0.94f, 1f, 0.82f),
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(-28f, 24f),
                new Vector2(420f, 42f),
                "NEURAL LOAD: 000%");

            Text runTimerText = CreateUIText(
                "RunTimerText",
                canvasTransform,
                font,
                24,
                TextAnchor.UpperCenter,
                new Color(0.98f, 0.95f, 0.75f, 0.95f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -26f),
                new Vector2(460f, 56f),
                "RUN: 00:00.000");

            Text runSummaryText = CreateUIText(
                "RunSummaryText",
                canvasTransform,
                font,
                28,
                TextAnchor.MiddleCenter,
                new Color(0.95f, 0.98f, 1f, 1f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, 0f),
                new Vector2(920f, 320f),
                string.Empty);
            CanvasGroup runSummaryCanvasGroup = runSummaryText.gameObject.AddComponent<CanvasGroup>();
            runSummaryCanvasGroup.alpha = 0f;

            context.HUDAltitude = altitudeText.gameObject.AddComponent<HUDAltitude>();
            context.CheckpointUI = checkpointText.gameObject.AddComponent<CheckpointUI>();
            context.SideEffectUI = canvasTransform.gameObject.AddComponent<SideEffectUI>();
            context.RunHUD = canvasTransform.gameObject.AddComponent<RunHUD>();

            SerializedObject hudSO = new SerializedObject(context.HUDAltitude);
            SetObjectProperty(hudSO, "altitudeTextComponent", altitudeText);
            hudSO.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject checkpointUiSO = new SerializedObject(context.CheckpointUI);
            SetObjectProperty(checkpointUiSO, "checkpointTextComponent", checkpointText);
            SetObjectProperty(checkpointUiSO, "checkpointCanvasGroup", checkpointCanvasGroup);
            checkpointUiSO.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject sideEffectUiSO = new SerializedObject(context.SideEffectUI);
            SetObjectProperty(sideEffectUiSO, "sideEffectsTextComponent", sideEffectText);
            SetObjectProperty(sideEffectUiSO, "warningTextComponent", warningText);
            SetObjectProperty(sideEffectUiSO, "statusTextComponent", neuralLoadText);
            SetObjectProperty(sideEffectUiSO, "warningFlashImage", warningFlashImage);
            sideEffectUiSO.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject runHudSO = new SerializedObject(context.RunHUD);
            SetObjectProperty(runHudSO, "timerTextComponent", runTimerText);
            SetObjectProperty(runHudSO, "summaryTextComponent", runSummaryText);
            SetObjectProperty(runHudSO, "summaryCanvasGroup", runSummaryCanvasGroup);
            runHudSO.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateLighting(Transform lightingRoot, VolumeProfile skyAndFogProfile, MaterialSet materials)
        {
            Transform directional = CreateChild("Directional Light", lightingRoot, false);
            directional.rotation = Quaternion.Euler(44f, -32f, 0f);

            Light light = directional.gameObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 105000f;
            light.color = new Color(0.9f, 0.88f, 1f);
            light.shadows = LightShadows.Soft;

            if (directional.GetComponent<HDAdditionalLightData>() == null)
            {
                directional.gameObject.AddComponent<HDAdditionalLightData>();
            }

            Transform skyFog = CreateChild("Sky and Fog Volume", lightingRoot, false);
            Volume skyVolume = skyFog.gameObject.AddComponent<Volume>();
            skyVolume.isGlobal = true;
            skyVolume.priority = 10f;
            skyVolume.sharedProfile = skyAndFogProfile;

            CreatePointLight("LowZone_Cyan", lightingRoot, new Vector3(-8f, 16f, 14f), new Color(0.25f, 0.95f, 1f), 1600f, 36f);
            CreatePointLight("MidZone_Pink", lightingRoot, new Vector3(8f, 55f, 52f), new Color(1f, 0.35f, 0.68f), 1900f, 42f);
            CreatePointLight("HighZone_Toxic", lightingRoot, new Vector3(-6f, 98f, 88f), new Color(0.22f, 1f, 0.35f), 2100f, 48f);

            CreatePrimitive("BackWall", PrimitiveType.Cube, lightingRoot, new Vector3(0f, 78f, 126f), new Vector3(160f, 180f, 2f), Quaternion.Euler(0f, 0f, 0f), materials.DeepBlack, true);
        }

        private static void WireReferences(BuildContext context)
        {
            if (context.FirstPersonLook != null)
            {
                SerializedObject lookSO = new SerializedObject(context.FirstPersonLook);
                SetObjectProperty(lookSO, "yawTransform", context.PlayerRoot);
                SetObjectProperty(lookSO, "pitchTransform", context.CameraRoot);
                SetObjectProperty(lookSO, "inputRouter", context.InputRouter);
                SetFloatProperty(lookSO, "mouseSensitivityX", 2.2f);
                SetFloatProperty(lookSO, "mouseSensitivityY", 2.1f);
                SetFloatProperty(lookSO, "gamepadLookSpeedX", 190f);
                SetFloatProperty(lookSO, "gamepadLookSpeedY", 150f);
                lookSO.ApplyModifiedPropertiesWithoutUndo();
            }

            if (context.FirstPersonMotor != null)
            {
                SerializedObject motorSO = new SerializedObject(context.FirstPersonMotor);
                SetObjectProperty(motorSO, "movementReference", context.PlayerRoot);
                SetObjectProperty(motorSO, "inputRouter", context.InputRouter);
                SetFloatProperty(motorSO, "moveSpeed", 8.2f);
                SetFloatProperty(motorSO, "acceleration", 32f);
                SetFloatProperty(motorSO, "airControl", 0.45f);
                SetFloatProperty(motorSO, "jumpVelocity", 11f);
                SetFloatProperty(motorSO, "gravity", -22f);
                SetFloatProperty(motorSO, "groundedSnapVelocity", -4f);
                motorSO.ApplyModifiedPropertiesWithoutUndo();
            }

            if (context.PlayerFallRespawn != null)
            {
                SerializedObject respawnSO = new SerializedObject(context.PlayerFallRespawn);
                SetObjectProperty(respawnSO, "firstPersonMotor", context.FirstPersonMotor);
                SetObjectProperty(respawnSO, "checkpointManager", context.CheckpointManager);
                SetObjectProperty(respawnSO, "sideEffectUI", context.SideEffectUI);
                SetObjectProperty(respawnSO, "cameraSideEffects", context.CameraSideEffects);
                SetFloatProperty(respawnSO, "killHeight", -22f);
                respawnSO.ApplyModifiedPropertiesWithoutUndo();
            }

            if (context.CheckpointManager != null)
            {
                SerializedObject checkpointManagerSO = new SerializedObject(context.CheckpointManager);
                SetBoolProperty(checkpointManagerSO, "autoCollectCheckpointsFromChildren", false);
                SetObjectProperty(checkpointManagerSO, "playerFallRespawn", context.PlayerFallRespawn);
                SetObjectProperty(checkpointManagerSO, "checkpointUI", context.CheckpointUI);
                SetIntProperty(checkpointManagerSO, "defaultCheckpointIndex", 0);
                SetObjectListProperty(checkpointManagerSO, "checkpoints", context.Checkpoints);
                checkpointManagerSO.ApplyModifiedPropertiesWithoutUndo();
            }

            if (context.RunSessionManager != null)
            {
                SerializedObject runSO = new SerializedObject(context.RunSessionManager);
                SetObjectProperty(runSO, "playerFallRespawn", context.PlayerFallRespawn);
                SetObjectProperty(runSO, "checkpointManager", context.CheckpointManager);
                SetObjectProperty(runSO, "goalZone", context.GoalZone);
                SetBoolProperty(runSO, "autoStartOnPlay", true);
                SetBoolProperty(runSO, "allowRestartWithKey", true);
                runSO.ApplyModifiedPropertiesWithoutUndo();
            }

            if (context.HUDAltitude != null)
            {
                SerializedObject altitudeSO = new SerializedObject(context.HUDAltitude);
                SetObjectProperty(altitudeSO, "playerTransform", context.PlayerRoot);
                altitudeSO.ApplyModifiedPropertiesWithoutUndo();
            }

            if (context.PsychedelicVolumeController != null)
            {
                SerializedObject volumeSO = new SerializedObject(context.PsychedelicVolumeController);
                SetObjectProperty(volumeSO, "globalVolume", context.PsychedelicVolumeController.GetComponent<Volume>());
                volumeSO.ApplyModifiedPropertiesWithoutUndo();
                context.PsychedelicVolumeController.CacheOverrides();
            }

            if (context.PsychedelicCustomPassController != null)
            {
                context.PsychedelicCustomPassController.EnsurePass();
            }

            context.SideEffectEventDirector = context.PsychedelicVolumeController != null
                ? context.PsychedelicVolumeController.gameObject.AddComponent<SideEffectEventDirector>()
                : null;

            if (context.SideEffectEventDirector != null)
            {
                SerializedObject eventSO = new SerializedObject(context.SideEffectEventDirector);
                SetObjectProperty(eventSO, "sideEffectUI", context.SideEffectUI);
                SetObjectProperty(eventSO, "cameraSideEffects", context.CameraSideEffects);
                SetObjectProperty(eventSO, "volumeController", context.PsychedelicVolumeController);
                SetObjectProperty(eventSO, "customPassController", context.PsychedelicCustomPassController);
                SetObjectProperty(eventSO, "audioIntensityDriver", context.AudioIntensityDriver);
                SetObjectListProperty(eventSO, "deceptivePlatforms", context.FakePlatforms);
                SetFloatProperty(eventSO, "minimumEventProgression", 0.14f);
                eventSO.ApplyModifiedPropertiesWithoutUndo();
            }

            if (context.AudioManager != null && context.AudioIntensityDriver != null)
            {
                SerializedObject audioManagerSO = new SerializedObject(context.AudioManager);
                SetObjectProperty(audioManagerSO, "masterLoopSource", context.AudioManager.GetComponent<AudioSource>());
                SetObjectProperty(audioManagerSO, "audioIntensityDriver", context.AudioIntensityDriver);
                audioManagerSO.ApplyModifiedPropertiesWithoutUndo();
            }

            if (context.AudioIntensityDriver != null)
            {
                SerializedObject audioDriverSO = new SerializedObject(context.AudioIntensityDriver);
                SetObjectProperty(audioDriverSO, "masterLoopSource", context.AudioIntensityDriver.GetComponent<AudioSource>());
                SetObjectProperty(audioDriverSO, "lowPassFilter", context.AudioIntensityDriver.GetComponent<AudioLowPassFilter>());
                SetBoolProperty(audioDriverSO, "driveLowPass", false);
                audioDriverSO.ApplyModifiedPropertiesWithoutUndo();
            }

            if (context.HeightProgressionManager != null)
            {
                SerializedObject heightSO = new SerializedObject(context.HeightProgressionManager);
                SetObjectProperty(heightSO, "playerTransform", context.PlayerRoot);
                SetObjectProperty(heightSO, "volumeController", context.PsychedelicVolumeController);
                SetObjectProperty(heightSO, "customPassController", context.PsychedelicCustomPassController);
                SetObjectProperty(heightSO, "cameraSideEffects", context.CameraSideEffects);
                SetObjectProperty(heightSO, "audioIntensityDriver", context.AudioIntensityDriver);
                SetObjectProperty(heightSO, "sideEffectEventDirector", context.SideEffectEventDirector);
                SetObjectProperty(heightSO, "sideEffectUI", context.SideEffectUI);
                SetObjectListProperty(heightSO, "fakePlatforms", context.FakePlatforms);
                SetFloatProperty(heightSO, "minHeight", 0f);
                SetFloatProperty(heightSO, "maxHeight", 140f);
                heightSO.ApplyModifiedPropertiesWithoutUndo();
            }

            if (context.GameManager != null)
            {
                SerializedObject gameManagerSO = new SerializedObject(context.GameManager);
                SetObjectProperty(gameManagerSO, "heightProgressionManager", context.HeightProgressionManager);
                SetObjectProperty(gameManagerSO, "checkpointManager", context.CheckpointManager);
                SetObjectProperty(gameManagerSO, "playerFallRespawn", context.PlayerFallRespawn);
                gameManagerSO.ApplyModifiedPropertiesWithoutUndo();
            }

            if (context.RunHUD != null && context.RunSessionManager != null)
            {
                SerializedObject runHudSO = new SerializedObject(context.RunHUD);
                SetObjectProperty(runHudSO, "runSessionManager", context.RunSessionManager);
                runHudSO.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static Transform CreateChild(string name, Transform parent, bool uiRect)
        {
            GameObject child = uiRect
                ? new GameObject(name, typeof(RectTransform))
                : new GameObject(name);
            child.transform.SetParent(parent, false);
            return child.transform;
        }

        private static GameObject CreatePrimitive(
            string name,
            PrimitiveType primitiveType,
            Transform parent,
            Vector3 position,
            Vector3 localScale,
            Quaternion rotation,
            Material material,
            bool removeCollider)
        {
            GameObject primitive = GameObject.CreatePrimitive(primitiveType);
            primitive.name = name;
            primitive.transform.SetParent(parent, true);
            primitive.transform.position = position;
            primitive.transform.rotation = rotation;
            primitive.transform.localScale = localScale;

            Renderer renderer = primitive.GetComponent<Renderer>();
            if (renderer != null && material != null)
            {
                renderer.sharedMaterial = material;
            }

            if (removeCollider)
            {
                Collider collider = primitive.GetComponent<Collider>();
                if (collider != null)
                {
                    UnityEngine.Object.DestroyImmediate(collider);
                }
            }

            return primitive;
        }

        private static Text CreateUIText(
            string name,
            Transform parent,
            Font font,
            int fontSize,
            TextAnchor anchor,
            Color color,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 sizeDelta,
            string text)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);

            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;

            Text uiText = go.GetComponent<Text>();
            uiText.font = font;
            uiText.fontSize = fontSize;
            uiText.alignment = anchor;
            uiText.color = color;
            uiText.text = text;
            uiText.horizontalOverflow = HorizontalWrapMode.Overflow;
            uiText.verticalOverflow = VerticalWrapMode.Overflow;
            uiText.raycastTarget = false;

            return uiText;
        }

        private static Checkpoint CreateCheckpoint(string name, string label, int index, Vector3 position, Transform checkpointsRoot, Material markerMaterial)
        {
            GameObject checkpointObject = new GameObject(name);
            checkpointObject.transform.SetParent(checkpointsRoot, false);
            checkpointObject.transform.position = position;

            BoxCollider collider = checkpointObject.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.size = new Vector3(6f, 4f, 6f);
            collider.center = new Vector3(0f, 1f, 0f);

            GameObject anchor = new GameObject("RespawnAnchor");
            anchor.transform.SetParent(checkpointObject.transform, false);
            anchor.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            anchor.transform.localRotation = Quaternion.identity;

            GameObject marker = CreatePrimitive("Marker", PrimitiveType.Cylinder, checkpointObject.transform, position + Vector3.up * 0.1f, new Vector3(0.8f, 0.1f, 0.8f), Quaternion.identity, markerMaterial, false);
            Collider markerCollider = marker.GetComponent<Collider>();
            if (markerCollider != null)
            {
                UnityEngine.Object.DestroyImmediate(markerCollider);
            }

            Checkpoint checkpoint = checkpointObject.AddComponent<Checkpoint>();
            SerializedObject checkpointSO = new SerializedObject(checkpoint);
            SetIntProperty(checkpointSO, "checkpointIndex", index);
            SetStringProperty(checkpointSO, "checkpointLabel", label);
            SetObjectProperty(checkpointSO, "respawnAnchor", anchor.transform);
            SetBoolProperty(checkpointSO, "oneShotActivation", false);
            checkpointSO.ApplyModifiedPropertiesWithoutUndo();

            return checkpoint;
        }

        private static void CreateRingFrame(Transform parent, string name, Vector3 center, float radius, Material material)
        {
            Transform ringRoot = new GameObject(name).transform;
            ringRoot.SetParent(parent, false);
            ringRoot.position = center;

            for (int i = 0; i < 8; i++)
            {
                float angle = i / 8f * Mathf.PI * 2f;
                Vector3 position = center + new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * 0.9f, Mathf.Sin(angle) * radius);
                Quaternion rotation = Quaternion.LookRotation((center - position).normalized, Vector3.up);
                CreatePrimitive($"{name}_Segment_{i:00}", PrimitiveType.Cube, ringRoot, position, new Vector3(0.8f, 0.8f, 4.4f), rotation, material, false);
            }
        }

        private static void CreateTeaserZone(Transform endTeaseZone, MaterialSet materials, Vector3 routeTopPoint)
        {
            Vector3 teaserBase = routeTopPoint + new Vector3(0f, 9f, 12f);
            CreatePrimitive("Tease_Platform", PrimitiveType.Cube, endTeaseZone, teaserBase, new Vector3(8f, 1.2f, 8f), Quaternion.Euler(0f, 42f, 0f), materials.GlowPink, false);
            CreatePrimitive("Tease_Gate_Left", PrimitiveType.Cube, endTeaseZone, teaserBase + new Vector3(-4f, 6f, 0f), new Vector3(1.5f, 12f, 1.5f), Quaternion.Euler(0f, 18f, 5f), materials.DeepBlack, false);
            CreatePrimitive("Tease_Gate_Right", PrimitiveType.Cube, endTeaseZone, teaserBase + new Vector3(4f, 6f, 0f), new Vector3(1.5f, 12f, 1.5f), Quaternion.Euler(0f, -18f, -5f), materials.DeepBlack, false);
            CreatePrimitive("Tease_Gate_Top", PrimitiveType.Cube, endTeaseZone, teaserBase + new Vector3(0f, 12f, 0f), new Vector3(10f, 1.2f, 2f), Quaternion.identity, materials.GlowCyan, false);
        }

        private static void CreatePointLight(string name, Transform parent, Vector3 position, Color color, float intensity, float range)
        {
            GameObject lightObject = new GameObject(name);
            lightObject.transform.SetParent(parent, false);
            lightObject.transform.position = position;

            Light point = lightObject.AddComponent<Light>();
            point.type = LightType.Point;
            point.color = color;
            point.intensity = intensity;
            point.range = range;
            point.shadows = LightShadows.None;

            if (lightObject.GetComponent<HDAdditionalLightData>() == null)
            {
                lightObject.AddComponent<HDAdditionalLightData>();
            }
        }

        private static void ConfigureMovingPlatform(MovingPlatform platform, Vector3 axis, float amplitude, float duration, float phaseOffset)
        {
            SerializedObject so = new SerializedObject(platform);
            SetEnumProperty(so, "motionMode", (int)MovingPlatform.MotionMode.AxisAmplitude);
            SetFloatProperty(so, "cycleDuration", duration);
            SetFloatProperty(so, "phaseOffset", phaseOffset);
            SetVector3Property(so, "localAxis", axis);
            SetFloatProperty(so, "amplitude", amplitude);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureFakePlatform(FakePlatform platform, float unsafeThreshold, float flickerStartThreshold)
        {
            SerializedObject so = new SerializedObject(platform);
            SetFloatProperty(so, "intensityUnsafeThreshold", unsafeThreshold);
            SetFloatProperty(so, "flickerStartThreshold", flickerStartThreshold);
            SetEnumProperty(so, "behaviorMode", (int)FakePlatform.FakeBehaviorMode.IntensityAndFlicker);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static float Range(System.Random random, float min, float max)
        {
            return (float)(min + (max - min) * random.NextDouble());
        }

        private static bool TagExists(string tagName)
        {
            string[] tags = UnityEditorInternal.InternalEditorUtility.tags;
            for (int i = 0; i < tags.Length; i++)
            {
                if (string.Equals(tags[i], tagName, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static void SetObjectProperty(SerializedObject serializedObject, string propertyName, UnityEngine.Object value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
            {
                property.objectReferenceValue = value;
            }
        }

        private static void SetObjectListProperty<T>(SerializedObject serializedObject, string propertyName, IList<T> values) where T : UnityEngine.Object
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                return;
            }

            property.arraySize = values?.Count ?? 0;
            if (values == null)
            {
                return;
            }

            for (int i = 0; i < values.Count; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
        }

        private static void SetFloatProperty(SerializedObject serializedObject, string propertyName, float value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
            {
                property.floatValue = value;
            }
        }

        private static void SetIntProperty(SerializedObject serializedObject, string propertyName, int value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
            {
                property.intValue = value;
            }
        }

        private static void SetBoolProperty(SerializedObject serializedObject, string propertyName, bool value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
            {
                property.boolValue = value;
            }
        }

        private static void SetStringProperty(SerializedObject serializedObject, string propertyName, string value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
            {
                property.stringValue = value;
            }
        }

        private static void SetVector3Property(SerializedObject serializedObject, string propertyName, Vector3 value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
            {
                property.vector3Value = value;
            }
        }

        private static void SetEnumProperty(SerializedObject serializedObject, string propertyName, int enumValue)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
            {
                property.enumValueIndex = enumValue;
            }
        }
    }
}
#endif
