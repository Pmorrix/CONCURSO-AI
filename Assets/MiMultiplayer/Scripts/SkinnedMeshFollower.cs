using UnityEngine;

public class SkinnedMeshFollower : MonoBehaviour
{
    [Tooltip("The original animated TurretTop object that contains the SkinnedMeshRenderer")]
    [SerializeField] private SkinnedMeshRenderer targetSkinnedMesh;

    [Tooltip("The offset rotation to fix your orientation problem (e.g., Y = 90 or 180)")]
    [SerializeField] private Vector3 rotationOffset;

    private Transform m_TargetBone;

    void Start()
    {
        if (targetSkinnedMesh != null && targetSkinnedMesh.bones.Length > 0)
        {
            // Most skinned meshes use the first bone (index 0) or rootBone for their primary movement.
            m_TargetBone = targetSkinnedMesh.rootBone != null ? targetSkinnedMesh.rootBone : targetSkinnedMesh.bones[0];
        }
        else
        {
            Debug.LogError($"[TurretFix] No bones found on the assigned SkinnedMeshRenderer on {gameObject.name}!", this);
        }
    }

    void LateUpdate()
    {
        if (m_TargetBone == null) return;

        // Perfect position matching
        transform.position = m_TargetBone.position;

        // Match rotation and seamlessly apply your custom correction offset
        transform.rotation = m_TargetBone.rotation * Quaternion.Euler(rotationOffset);
    }
}