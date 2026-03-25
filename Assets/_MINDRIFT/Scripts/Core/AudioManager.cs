using UnityEngine;
using Mindrift.Effects;

namespace Mindrift.Core
{
    public sealed class AudioManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private AudioSource masterLoopSource;
        [SerializeField] private AudioIntensityDriver audioIntensityDriver;

        [Header("Startup Music")]
        [SerializeField] private AudioClip defaultLoopClip;
        [SerializeField] private bool autoFindClipByName = true;
        [SerializeField] private string autoFindClipName = "hardstyle";
        [SerializeField] private bool playOnStart = true;
        [SerializeField, Range(0f, 1f)] private float startupVolume = 0.35f;

        private void Awake()
        {
            if (masterLoopSource == null)
            {
                masterLoopSource = GetComponent<AudioSource>();
            }

            if (masterLoopSource == null)
            {
                masterLoopSource = gameObject.AddComponent<AudioSource>();
            }

            if (audioIntensityDriver == null)
            {
                audioIntensityDriver = GetComponent<AudioIntensityDriver>();
            }

            masterLoopSource.loop = true;
            masterLoopSource.spatialBlend = 0f;
            masterLoopSource.playOnAwake = false;
            masterLoopSource.volume = startupVolume;

            if (masterLoopSource.clip == null)
            {
                if (defaultLoopClip != null)
                {
                    masterLoopSource.clip = defaultLoopClip;
                }
                else if (autoFindClipByName)
                {
                    masterLoopSource.clip = FindAudioClipByName(autoFindClipName);
                }
            }
        }

        private void Start()
        {
            if (playOnStart)
            {
                EnsureLoopPlaying();
            }
        }

        public void EnsureLoopPlaying()
        {
            if (masterLoopSource == null)
            {
                return;
            }

            if (masterLoopSource.clip == null && autoFindClipByName)
            {
                masterLoopSource.clip = FindAudioClipByName(autoFindClipName);
            }

            if (masterLoopSource.clip != null && !masterLoopSource.isPlaying)
            {
                masterLoopSource.Play();
            }
        }

        private static AudioClip FindAudioClipByName(string needle)
        {
            if (string.IsNullOrWhiteSpace(needle))
            {
                return null;
            }

            AudioClip[] loadedClips = Resources.FindObjectsOfTypeAll<AudioClip>();
            for (int i = 0; i < loadedClips.Length; i++)
            {
                AudioClip clip = loadedClips[i];
                if (clip != null && clip.name.Contains(needle, System.StringComparison.OrdinalIgnoreCase))
                {
                    return clip;
                }
            }

            return null;
        }

        public AudioSource MasterLoopSource => masterLoopSource;
        public AudioIntensityDriver AudioIntensityDriver => audioIntensityDriver;
    }
}
