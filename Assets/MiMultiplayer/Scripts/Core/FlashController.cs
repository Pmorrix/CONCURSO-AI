using UnityEngine;

namespace Unity.FPS.Core
{
    public interface IFlashable
    {
        void FlashWhite();
    }

    public class FlashController : MonoBehaviour, IFlashable
    {
        [Header("Mesh Settings")]
        [Tooltip("The parent container object holding all the mesh parts to flash.")]
        [SerializeField] private GameObject meshContainer;

        [Header("Flash Configuration")]
        [SerializeField] private Material damageFlashMaterial;
        [SerializeField] private float flashDuration = 0.15f;

        private Coroutine m_FlashCoroutine;
        private System.Action m_RestoreCallback;

        private struct RendererMaterialBackup
        {
            public SkinnedMeshRenderer skinnedRenderer;
            public MeshRenderer meshRenderer; // Supports both types of renderers
            public Material[] originalSharedMaterials;
        }
        private RendererMaterialBackup[] m_RendererBackups;

        private void Awake()
        {
            CacheOriginalMaterials();
        }

        private void CacheOriginalMaterials()
        {
            if (meshContainer == null) return;

            SkinnedMeshRenderer[] skinnedRenderers = meshContainer.GetComponentsInChildren<SkinnedMeshRenderer>();
            MeshRenderer[] meshRenderers = meshContainer.GetComponentsInChildren<MeshRenderer>();

            m_RendererBackups = new RendererMaterialBackup[skinnedRenderers.Length + meshRenderers.Length];
            int index = 0;

            for (int i = 0; i < skinnedRenderers.Length; i++)
            {
                m_RendererBackups[index++] = new RendererMaterialBackup
                {
                    skinnedRenderer = skinnedRenderers[i],
                    originalSharedMaterials = skinnedRenderers[i].sharedMaterials
                };
            }

            for (int i = 0; i < meshRenderers.Length; i++)
            {
                m_RendererBackups[index++] = new RendererMaterialBackup
                {
                    meshRenderer = meshRenderers[i],
                    originalSharedMaterials = meshRenderers[i].sharedMaterials
                };
            }
        }

        public void Initialize(System.Action restoreColorsCallback)
        {
            m_RestoreCallback = restoreColorsCallback;
        }

        public void FlashWhite()
        {
            if (m_FlashCoroutine != null) StopCoroutine(m_FlashCoroutine);
            m_FlashCoroutine = StartCoroutine(FlashWhiteCoroutine());
        }

        private System.Collections.IEnumerator FlashWhiteCoroutine()
        {
            if (m_RendererBackups != null && damageFlashMaterial != null)
            {
                // 1. Blanket absolutely everything into pure unlit white
                foreach (var backup in m_RendererBackups)
                {
                    int slotCount = backup.originalSharedMaterials.Length;
                    Material[] flashMaterials = new Material[slotCount];

                    for (int i = 0; i < slotCount; i++)
                    {
                        flashMaterials[i] = damageFlashMaterial;
                    }

                    if (backup.skinnedRenderer != null) backup.skinnedRenderer.materials = flashMaterials;
                    if (backup.meshRenderer != null) backup.meshRenderer.materials = flashMaterials;
                }

                yield return new WaitForSeconds(flashDuration);

                // 2. Execute restoration callback back to standard colors
                if (m_RestoreCallback != null)
                {
                    m_RestoreCallback.Invoke();
                }
                else
                {
                    // Fallback baseline restoration if no team matrix callback exists
                    foreach (var backup in m_RendererBackups)
                    {
                        if (backup.skinnedRenderer != null) backup.skinnedRenderer.materials = backup.originalSharedMaterials;
                        if (backup.meshRenderer != null) backup.meshRenderer.materials = backup.originalSharedMaterials;
                    }
                }
            }

            m_FlashCoroutine = null;
        }
    }
}