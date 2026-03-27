using System;
using Mindrift.UI;
using UnityEngine;

namespace Mindrift.Online.Models
{
    [Serializable]
    public sealed class MindriftSettingsDto
    {
        public float master_volume;
        public float music_volume;
        public float sfx_volume;
        public float controller_sensitivity;
        public bool invert_y;
        public float controller_deadzone;
        public bool fullscreen;
        public int quality_level;
        public int resolution_index;

        public void Sanitize()
        {
            master_volume = Mathf.Clamp01(master_volume);
            music_volume = Mathf.Clamp01(music_volume);
            sfx_volume = Mathf.Clamp01(sfx_volume);
            controller_sensitivity = Mathf.Clamp(controller_sensitivity, 0.4f, 2f);
            controller_deadzone = Mathf.Clamp(controller_deadzone, 0f, 0.4f);
            quality_level = Mathf.Max(0, quality_level);
            resolution_index = Mathf.Max(0, resolution_index);
        }

        public static MindriftSettingsDto FromLocalSettings()
        {
            return new MindriftSettingsDto
            {
                master_volume = SettingsManager.MasterVolume,
                music_volume = SettingsManager.MusicVolume,
                sfx_volume = SettingsManager.SfxVolume,
                controller_sensitivity = SettingsManager.ControllerSensitivity,
                invert_y = SettingsManager.InvertY,
                controller_deadzone = SettingsManager.ControllerDeadzone,
                fullscreen = SettingsManager.Fullscreen,
                quality_level = SettingsManager.QualityIndex,
                resolution_index = SettingsManager.ResolutionIndex
            };
        }

        public void ApplyToLocalSettings()
        {
            Sanitize();
            SettingsManager.SetMasterVolume(master_volume);
            SettingsManager.SetMusicVolume(music_volume);
            SettingsManager.SetSfxVolume(sfx_volume);
            SettingsManager.SetControllerSensitivity(controller_sensitivity);
            SettingsManager.SetInvertY(invert_y);
            SettingsManager.SetControllerDeadzone(controller_deadzone);
            SettingsManager.SetFullscreen(fullscreen);
            SettingsManager.SetQualityIndex(quality_level);
            SettingsManager.SetResolutionIndex(resolution_index);
        }
    }
}
