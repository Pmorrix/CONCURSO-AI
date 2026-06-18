using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

namespace Unity.FPS.Core
{
    public class MenuEffectsManager : MonoBehaviour
    {
        public static MenuEffectsManager Instance { get; private set; }

        [Header("Audio Settings")]
        [SerializeField] private AudioMixer audioMixer;
        [SerializeField] private AudioSource musicSource;
        [SerializeField] private string musicVolumeParameterName = "MusicVolume";

        [Header("Fade Screen Settings")]
        [SerializeField] private CanvasGroup fadeCanvasGroup;
        [SerializeField] private float fadeDuration = 1.0f;

        [Header("Loading Icon Settings")]
        [SerializeField] private RectTransform loadingIconRect;

        [Tooltip("Time in seconds between each 30-degree snap rotation step")]
        [SerializeField] private float rotationInterval = 0.1f;

        [Tooltip("True for clockwise snaps (-30°), False for counter-clockwise (+30°)")]
        [SerializeField] private bool clockwise = true;

        private float m_RotationTimer;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            // Clean initialization setup
            if (fadeCanvasGroup != null)
            {
                fadeCanvasGroup.alpha = 0f;
                fadeCanvasGroup.blocksRaycasts = false;
                fadeCanvasGroup.interactable = false;
                // Leave the GameObject active, but invisible and uninteractable
                fadeCanvasGroup.gameObject.SetActive(true);
            }
        }

        private void Start()
        {
            if (musicSource != null && !musicSource.isPlaying)
            {
                musicSource.loop = true;
                musicSource.Play();
            }
        }

        private void Update()
        {
            // Only step-rotate the icon if it is configured and the fader is actively visible
            if (loadingIconRect != null && fadeCanvasGroup != null && fadeCanvasGroup.alpha > 0f)
            {
                // unscaledDeltaTime protects the ticking speed if the scene load spikes CPU frames
                m_RotationTimer += Time.unscaledDeltaTime;

                if (m_RotationTimer >= rotationInterval)
                {
                    // Calculate exactly how many 30-degree steps to take (handles lag spikes cleanly)
                    int stepsToTake = Mathf.FloorToInt(m_RotationTimer / rotationInterval);
                    m_RotationTimer %= rotationInterval; // Keep the remainder for tracking accuracy

                    float stepDegrees = clockwise ? -30f : 30f;
                    float totalDegreesThisFrame = stepDegrees * stepsToTake;

                    loadingIconRect.Rotate(0f, 0f, totalDegreesThisFrame);
                }
            }
            else
            {
                // Reset the timer when the screen goes invisible so it doesn't instantly snap on open
                m_RotationTimer = 0f;
            }
        }

        public void StartMatchTransitions(System.Action onTransitionComplete)
        {
            StartCoroutine(ExecuteTransitionSequence(onTransitionComplete));
        }

        private IEnumerator ExecuteTransitionSequence(System.Action onTransitionComplete)
        {
            if (fadeCanvasGroup != null)
            {
                // FORCE the gameobject alive right here just in case it got turned off
                fadeCanvasGroup.gameObject.SetActive(true);
                fadeCanvasGroup.blocksRaycasts = true;
                fadeCanvasGroup.interactable = true;
                fadeCanvasGroup.alpha = 0f;
            }

            float currentTime = 0;
            float startVolumeDb;
            audioMixer.GetFloat(musicVolumeParameterName, out startVolumeDb);
            float targetVolumeDb = -80f;

            while (currentTime < fadeDuration)
            {
                currentTime += Time.deltaTime;
                float progressNormalized = currentTime / fadeDuration;

                // 1. Linearly blend the visual transparency to solid black
                if (fadeCanvasGroup != null)
                {
                    fadeCanvasGroup.alpha = Mathf.Lerp(0f, 1f, progressNormalized);
                }

                // 2. Adjust Audio Mixer attenuation levels
                float currentVol = Mathf.Lerp(startVolumeDb, targetVolumeDb, progressNormalized);
                audioMixer.SetFloat(musicVolumeParameterName, currentVol);

                yield return null;
            }

            // Fast-forward directly to absolute precision targets at conclusion
            if (fadeCanvasGroup != null) fadeCanvasGroup.alpha = 1f;
            audioMixer.SetFloat(musicVolumeParameterName, targetVolumeDb);

            if (musicSource != null) musicSource.Stop();

            // Transition yields are complete. Pass control back to initiate network actions.
            onTransitionComplete?.Invoke();
        }
    }
}