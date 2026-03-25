using System;
using System.Collections.Generic;
using Mindrift.Effects;
using Mindrift.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mindrift.UI
{
    public static class SettingsManager
    {
        private const string MasterVolumeKey = "Mindrift.Settings.MasterVolume";
        private const string MusicVolumeKey = "Mindrift.Settings.MusicVolume";
        private const string SfxVolumeKey = "Mindrift.Settings.SfxVolume";
        private const string FullscreenKey = "Mindrift.Settings.Fullscreen";
        private const string ResolutionIndexKey = "Mindrift.Settings.ResolutionIndex";
        private const string QualityIndexKey = "Mindrift.Settings.QualityIndex";
        private const string ControllerSensitivityKey = "Mindrift.Settings.ControllerSensitivity";
        private const string InvertYKey = "Mindrift.Settings.InvertY";
        private const string ControllerDeadzoneKey = "Mindrift.Settings.ControllerDeadzone";

        private static readonly List<Resolution> AvailableResolutions = new List<Resolution>();
        private static bool initialized;

        public static event Action OnSettingsApplied;

        public static float MasterVolume { get; private set; } = 1f;
        public static float MusicVolume { get; private set; } = 1f;
        public static float SfxVolume { get; private set; } = 1f;
        public static bool Fullscreen { get; private set; } = true;
        public static int ResolutionIndex { get; private set; }
        public static int QualityIndex { get; private set; }
        public static float ControllerSensitivity { get; private set; } = 1f;
        public static bool InvertY { get; private set; }
        public static float ControllerDeadzone { get; private set; } = 0.08f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            EnsureInitialized();
        }

        public static void EnsureInitialized()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;
            CacheResolutions();
            LoadFromPrefs();
            SceneManager.sceneLoaded += HandleSceneLoaded;
            ApplyAll();
        }

        public static IReadOnlyList<Resolution> GetAvailableResolutions()
        {
            EnsureInitialized();
            return AvailableResolutions;
        }

        public static string GetResolutionLabel(int index)
        {
            EnsureInitialized();
            if (index < 0 || index >= AvailableResolutions.Count)
            {
                return "CURRENT";
            }

            Resolution res = AvailableResolutions[index];
            return $"{res.width} x {res.height}";
        }

        public static void SetMasterVolume(float value)
        {
            EnsureInitialized();
            MasterVolume = Mathf.Clamp01(value);
            SaveFloat(MasterVolumeKey, MasterVolume);
            ApplyAudio();
            NotifyApplied();
        }

        public static void SetMusicVolume(float value)
        {
            EnsureInitialized();
            MusicVolume = Mathf.Clamp01(value);
            SaveFloat(MusicVolumeKey, MusicVolume);
            ApplyAudio();
            NotifyApplied();
        }

        public static void SetSfxVolume(float value)
        {
            EnsureInitialized();
            SfxVolume = Mathf.Clamp01(value);
            SaveFloat(SfxVolumeKey, SfxVolume);
            ApplyAudio();
            NotifyApplied();
        }

        public static void SetFullscreen(bool enabled)
        {
            EnsureInitialized();
            Fullscreen = enabled;
            SaveInt(FullscreenKey, enabled ? 1 : 0);
            ApplyDisplay();
            NotifyApplied();
        }

        public static void SetResolutionIndex(int index)
        {
            EnsureInitialized();
            ResolutionIndex = Mathf.Clamp(index, 0, Mathf.Max(AvailableResolutions.Count - 1, 0));
            SaveInt(ResolutionIndexKey, ResolutionIndex);
            ApplyDisplay();
            NotifyApplied();
        }

        public static void SetQualityIndex(int index)
        {
            EnsureInitialized();
            int max = Mathf.Max(QualitySettings.names.Length - 1, 0);
            QualityIndex = Mathf.Clamp(index, 0, max);
            SaveInt(QualityIndexKey, QualityIndex);
            ApplyDisplay();
            NotifyApplied();
        }

        public static void SetControllerSensitivity(float value)
        {
            EnsureInitialized();
            ControllerSensitivity = Mathf.Clamp(value, 0.4f, 2f);
            SaveFloat(ControllerSensitivityKey, ControllerSensitivity);
            ApplyControllerSettings();
            NotifyApplied();
        }

        public static void SetInvertY(bool enabled)
        {
            EnsureInitialized();
            InvertY = enabled;
            SaveInt(InvertYKey, enabled ? 1 : 0);
            ApplyControllerSettings();
            NotifyApplied();
        }

        public static void SetControllerDeadzone(float value)
        {
            EnsureInitialized();
            ControllerDeadzone = Mathf.Clamp(value, 0f, 0.4f);
            SaveFloat(ControllerDeadzoneKey, ControllerDeadzone);
            ApplyControllerSettings();
            NotifyApplied();
        }

        public static void ApplyAll()
        {
            EnsureInitialized();
            ApplyDisplay();
            ApplyAudio();
            ApplyControllerSettings();
            NotifyApplied();
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ApplyAll();
        }

        private static void CacheResolutions()
        {
            AvailableResolutions.Clear();
            Resolution[] resolutions = Screen.resolutions;

            for (int i = 0; i < resolutions.Length; i++)
            {
                Resolution candidate = resolutions[i];
                bool duplicate = false;
                for (int j = 0; j < AvailableResolutions.Count; j++)
                {
                    Resolution existing = AvailableResolutions[j];
                    if (existing.width == candidate.width && existing.height == candidate.height)
                    {
                        duplicate = true;
                        break;
                    }
                }

                if (!duplicate)
                {
                    AvailableResolutions.Add(candidate);
                }
            }

            if (AvailableResolutions.Count == 0)
            {
                AvailableResolutions.Add(Screen.currentResolution);
            }
        }

        private static void LoadFromPrefs()
        {
            MasterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MasterVolumeKey, 1f));
            MusicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MusicVolumeKey, 1f));
            SfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(SfxVolumeKey, 1f));
            Fullscreen = PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) != 0;

            int defaultResolution = FindCurrentResolutionIndex();
            int loadedResolution = PlayerPrefs.GetInt(ResolutionIndexKey, defaultResolution);
            ResolutionIndex = Mathf.Clamp(loadedResolution, 0, Mathf.Max(AvailableResolutions.Count - 1, 0));

            int defaultQuality = Mathf.Clamp(QualitySettings.GetQualityLevel(), 0, Mathf.Max(QualitySettings.names.Length - 1, 0));
            QualityIndex = Mathf.Clamp(PlayerPrefs.GetInt(QualityIndexKey, defaultQuality), 0, Mathf.Max(QualitySettings.names.Length - 1, 0));

            ControllerSensitivity = Mathf.Clamp(PlayerPrefs.GetFloat(ControllerSensitivityKey, 1f), 0.4f, 2f);
            InvertY = PlayerPrefs.GetInt(InvertYKey, 0) != 0;
            ControllerDeadzone = Mathf.Clamp(PlayerPrefs.GetFloat(ControllerDeadzoneKey, 0.08f), 0f, 0.4f);
        }

        private static int FindCurrentResolutionIndex()
        {
            int width = Screen.currentResolution.width;
            int height = Screen.currentResolution.height;

            for (int i = 0; i < AvailableResolutions.Count; i++)
            {
                Resolution res = AvailableResolutions[i];
                if (res.width == width && res.height == height)
                {
                    return i;
                }
            }

            return Mathf.Max(AvailableResolutions.Count - 1, 0);
        }

        private static void ApplyDisplay()
        {
            Screen.fullScreen = Fullscreen;

            if (AvailableResolutions.Count > 0)
            {
                int clampedIndex = Mathf.Clamp(ResolutionIndex, 0, AvailableResolutions.Count - 1);
                Resolution res = AvailableResolutions[clampedIndex];
                Screen.SetResolution(res.width, res.height, Fullscreen);
            }

            int safeQuality = Mathf.Clamp(QualityIndex, 0, Mathf.Max(QualitySettings.names.Length - 1, 0));
            if (safeQuality != QualitySettings.GetQualityLevel())
            {
                QualitySettings.SetQualityLevel(safeQuality, true);
            }
        }

        private static void ApplyAudio()
        {
            AudioListener.volume = MasterVolume;

            AudioIntensityDriver[] drivers = UnityEngine.Object.FindObjectsByType<AudioIntensityDriver>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < drivers.Length; i++)
            {
                AudioIntensityDriver driver = drivers[i];
                if (driver != null)
                {
                    driver.ApplyMenuVolumeSettings(MusicVolume, SfxVolume);
                }
            }
        }

        private static void ApplyControllerSettings()
        {
            FirstPersonLook[] lookComponents = UnityEngine.Object.FindObjectsByType<FirstPersonLook>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < lookComponents.Length; i++)
            {
                FirstPersonLook look = lookComponents[i];
                if (look != null)
                {
                    look.ApplyControllerOptions(ControllerSensitivity, InvertY, ControllerDeadzone);
                }
            }
        }

        private static void SaveFloat(string key, float value)
        {
            PlayerPrefs.SetFloat(key, value);
            PlayerPrefs.Save();
        }

        private static void SaveInt(string key, int value)
        {
            PlayerPrefs.SetInt(key, value);
            PlayerPrefs.Save();
        }

        private static void NotifyApplied()
        {
            OnSettingsApplied?.Invoke();
        }
    }
}
