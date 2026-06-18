using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

namespace Unity.FPS.UI
{
    public class MainMenuAudioManager : MonoBehaviour
    {
        [Header("Audio Mixer Reference")]
        public AudioMixer MainMixer;

        [Header("UI Slider Components")]
        public Slider MasterVolumeSlider;
        public Slider MusicVolumeSlider;
        public Slider SFXVolumeSlider;

        // Permanent storage keys matching parameter strings
        private const string KEY_MASTER = "MasterVolume";
        private const string KEY_MUSIC = "MusicVolume";
        private const string KEY_SFX = "SFXVolume";

        private void Start()
        {
            // Load values from PlayerPrefs. If they don't exist yet, default them to max (1.0f)
            float savedMaster = PlayerPrefs.GetFloat(KEY_MASTER, 1.0f);
            float savedMusic = PlayerPrefs.GetFloat(KEY_MUSIC, 1.0f);
            float savedSFX = PlayerPrefs.GetFloat(KEY_SFX, 1.0f);

            // Assign slider values without triggering the listeners destructively during loading
            MasterVolumeSlider.value = savedMaster;
            MusicVolumeSlider.value = savedMusic;
            SFXVolumeSlider.value = savedSFX;

            // Push those values directly into the active AudioMixer
            ApplyVolume(KEY_MASTER, savedMaster);
            ApplyVolume(KEY_MUSIC, savedMusic);
            ApplyVolume(KEY_SFX, savedSFX);

            // Hook up operational state runtime changes
            MasterVolumeSlider.onValueChanged.AddListener((val) => OnVolumeSliderChanged(KEY_MASTER, val));
            MusicVolumeSlider.onValueChanged.AddListener((val) => OnVolumeSliderChanged(KEY_MUSIC, val));
            SFXVolumeSlider.onValueChanged.AddListener((val) => OnVolumeSliderChanged(KEY_SFX, val));
        }

        private void OnVolumeSliderChanged(string parameterKey, float value)
        {
            // 1. Save to system disk instantly
            PlayerPrefs.SetFloat(parameterKey, value);
            PlayerPrefs.Save();

            // 2. Drive the Mixer changes
            ApplyVolume(parameterKey, value);
        }

        private void ApplyVolume(string parameterName, float linearValue)
        {
            // Converts linear slider value (0 to 1) to logarithmic decibels (-80 to 0)
            float dB = Mathf.Log10(Mathf.Max(0.0001f, linearValue)) * 20f;
            MainMixer.SetFloat(parameterName, dB);
        }
    }
}