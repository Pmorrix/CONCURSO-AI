using Unity.FPS.Game;
using UnityEngine;

namespace Unity.FPS.UI
{
    public class CompassElement : MonoBehaviour
    {
        [Tooltip("The marker on the compass for this element")]
        public CompassMarker CompassMarkerPrefab;

        [Tooltip("Text override for the marker, if it's a direction")]
        public string TextDirection;

        Compass m_Compass;
        CompassMarker m_MarkerInstance;

        void Start()
        {
            TryRegisterWithCompass();
        }

        void Update()
        {
            if (m_Compass == null)
            {
                TryRegisterWithCompass();
            }
        }

        void TryRegisterWithCompass()
        {
            m_Compass = FindFirstObjectByType<Compass>();

            if (m_Compass != null)
            {
                m_MarkerInstance = Instantiate(CompassMarkerPrefab);
                m_MarkerInstance.Initialize(this, TextDirection);
                m_Compass.RegisterCompassElement(transform, m_MarkerInstance);
            }
        }
        void OnDestroy()
        {
            if (m_Compass != null)
            {
                m_Compass.UnregisterCompassElement(transform);
            }

            if (m_MarkerInstance != null)
            {
                Destroy(m_MarkerInstance.gameObject);
            }
        }
    }
}