using System.Linq;
using Unity.FPS.Game;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

namespace Unity.FPS.AI
{
    public class DetectionModule : NetworkBehaviour
    {
        [Tooltip("The point representing the source of target-detection raycasts for the enemy AI")]
        public Transform DetectionSourcePoint;

        [Tooltip("The max distance at which the enemy can see targets")]
        public float DetectionRange = 20f;

        [Tooltip("The max distance at which the enemy can attack its target")]
        public float AttackRange = 10f;

        [Tooltip("Time before an enemy abandons a known target that it can't see anymore")]
        public float KnownTargetTimeout = 4f;

        [Tooltip("Optional animator for OnShoot animations")]
        public Animator Animator;

        public UnityAction onDetectedTarget;
        public UnityAction onLostTarget;

        public GameObject KnownDetectedTarget { get; set; }
        public bool IsTargetInAttackRange { get; private set; }
        public bool IsSeeingTarget { get; private set; }
        public bool HadKnownTarget { get; private set; }

        protected float TimeLastSeenTarget = Mathf.NegativeInfinity;

        ActorsManager m_ActorsManager;

        const string k_AnimAttackParameter = "Attack";
        const string k_AnimOnDamagedParameter = "OnDamaged";

        protected virtual void Start()
        {
            if (Unity.FPS.Game.ActorsManager.Instance != null)
            {
                m_ActorsManager = FindFirstObjectByType<ActorsManager>();
                DebugUtility.HandleErrorIfNullFindObject<ActorsManager, DetectionModule>(m_ActorsManager, this);
            }
            else
            {
                Debug.LogError("Couldn't find ActorsManager");
            }
        }

        // --- NEW SAFETY METHOD TO SAFELY BREAK TARGET LOCKS MANUALLY ---
        public void ClearTargetManually()
        {
            KnownDetectedTarget = null;
            IsSeeingTarget = false;
            IsTargetInAttackRange = false;
            HadKnownTarget = false;
            TimeLastSeenTarget = Mathf.NegativeInfinity;
        }

        public virtual void HandleTargetDetection(Actor actor, Collider[] selfColliders)
        {
            if (!IsServer) return;

            if (Unity.FPS.Game.ActorsManager.Instance == null) return;

            if (KnownDetectedTarget)
            {
                Health targetHealth = KnownDetectedTarget.GetComponentInParent<Health>();
                if (targetHealth != null && targetHealth.CurrentHealth.Value <= 0)
                {
                    KnownDetectedTarget = null;
                    Debug.Log("Server Enemy: Target is Dead!");
                }
            }

            if (KnownDetectedTarget && !IsSeeingTarget && (Time.time - TimeLastSeenTarget) > KnownTargetTimeout)
            {
                KnownDetectedTarget = null;
            }

            float sqrDetectionRange = DetectionRange * DetectionRange;
            IsSeeingTarget = false;
            float closestSqrDistance = Mathf.Infinity;
            foreach (Actor otherActor in m_ActorsManager.Actors)
            {
                if (otherActor.Affiliation != actor.Affiliation)
                {
                    Health targetHealth = otherActor.GetComponentInParent<Health>();
                    if (targetHealth != null && targetHealth.CurrentHealth.Value <= 0) continue;

                    float sqrDistance = (otherActor.transform.position - DetectionSourcePoint.position).sqrMagnitude;
                    if (sqrDistance < sqrDetectionRange && sqrDistance < closestSqrDistance)
                    {
                        RaycastHit[] hits = Physics.RaycastAll(DetectionSourcePoint.position,
                            (otherActor.AimPoint.position - DetectionSourcePoint.position).normalized, DetectionRange,
                            -1, QueryTriggerInteraction.Ignore);
                        RaycastHit closestValidHit = new RaycastHit();
                        closestValidHit.distance = Mathf.Infinity;
                        bool foundValidHit = false;
                        foreach (var hit in hits)
                        {
                            if (!selfColliders.Contains(hit.collider) && hit.distance < closestValidHit.distance)
                            {
                                closestValidHit = hit;
                                foundValidHit = true;
                            }
                        }

                        if (foundValidHit)
                        {
                            Actor hitActor = closestValidHit.collider.GetComponentInParent<Actor>();
                            if (hitActor == otherActor)
                            {
                                IsSeeingTarget = true;
                                closestSqrDistance = sqrDistance;

                                TimeLastSeenTarget = Time.time;
                                KnownDetectedTarget = otherActor.AimPoint.gameObject;
                            }
                        }
                    }
                }
            }

            IsTargetInAttackRange = KnownDetectedTarget != null &&
                                    Vector3.Distance(transform.position, KnownDetectedTarget.transform.position) <=
                                    AttackRange;

            if (!HadKnownTarget && KnownDetectedTarget != null)
            {
                OnDetect();
            }

            if (HadKnownTarget && KnownDetectedTarget == null)
            {
                OnLostTarget();
            }

            HadKnownTarget = KnownDetectedTarget != null;
        }

        public virtual void OnLostTarget() => onLostTarget?.Invoke();

        public virtual void OnDetect() => onDetectedTarget?.Invoke();

        public virtual void OnDamaged(GameObject damageSource)
        {
            TimeLastSeenTarget = Time.time;

            Actor sourceActor = damageSource.GetComponentInParent<Actor>();
            if (sourceActor != null && sourceActor.AimPoint != null)
            {
                KnownDetectedTarget = sourceActor.AimPoint.gameObject;
            }
            else
            {
                KnownDetectedTarget = damageSource;
            }

            if (Animator)
            {
                Animator.SetTrigger(k_AnimOnDamagedParameter);
            }
        }

        public virtual void OnAttack()
        {
            if (Animator)
            {
                Animator.SetTrigger(k_AnimAttackParameter);
            }
        }
    }
}