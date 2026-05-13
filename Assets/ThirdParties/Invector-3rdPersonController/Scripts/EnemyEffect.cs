using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public class EnemyEffect : MonoBehaviour
{
    [Header("Hit Effects")]
    [SerializeField] GameObject[] hitParticles;
    [SerializeField] AudioClip[] hitSounds;
    [SerializeField] AudioSource audioSource;

    [Header("Ragdoll")]
    [SerializeField] float ragdollImpulse = 4f;
    [SerializeField] bool disableMainColliderOnRagdoll = true;

    Animator animator;
    NavMeshAgent navMeshAgent;
    Rigidbody rootRigidbody;
    Collider rootCollider;
    Rigidbody[] ragdollBodies;
    Collider[] ragdollColliders;
    bool ragdollActive;

    void Awake()
    {
        animator = GetComponent<Animator>();
        navMeshAgent = GetComponent<NavMeshAgent>();
        rootRigidbody = GetComponent<Rigidbody>();
        rootCollider = GetComponent<Collider>();

        if (rootRigidbody != null)
        {
            rootRigidbody.isKinematic = true;
            rootRigidbody.useGravity = false;
        }

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        ragdollBodies = GetComponentsInChildren<Rigidbody>(true);
        ragdollColliders = GetComponentsInChildren<Collider>(true);

        for (int index = 0; index < ragdollBodies.Length; index++)
        {
            Rigidbody body = ragdollBodies[index];
            if (body == rootRigidbody)
            {
                continue;
            }

            body.isKinematic = true;
            body.useGravity = false;
        }
    }

    public void PlayHitEffects(Vector3 hitPoint, Vector3 hitDirection)
    {
        if (hitParticles != null && hitParticles.Length > 0)
        {
            GameObject particlePrefab = hitParticles[Random.Range(0, hitParticles.Length)];
            if (particlePrefab != null)
            {
                Quaternion rotation = hitDirection.sqrMagnitude > 0.001f
                    ? Quaternion.LookRotation(hitDirection.normalized)
                    : transform.rotation;
                Instantiate(particlePrefab, hitPoint, rotation);
            }
        }

        if (audioSource != null && hitSounds != null && hitSounds.Length > 0)
        {
            AudioClip clip = hitSounds[Random.Range(0, hitSounds.Length)];
            if (clip != null)
            {
                audioSource.PlayOneShot(clip);
            }
        }
    }

    public void ActivateRagdoll()
    {
        ActivateRagdoll(Vector3.zero);
    }

    public void ActivateRagdoll(Vector3 hitDirection)
    {
        if (ragdollActive)
        {
            return;
        }

        ragdollActive = true;

        if (animator != null)
        {
            animator.enabled = false;
        }

        if (navMeshAgent != null)
        {
            navMeshAgent.enabled = false;
        }

        if (rootRigidbody != null)
        {
            rootRigidbody.isKinematic = true;
            rootRigidbody.useGravity = false;
        }

        if (rootCollider != null && disableMainColliderOnRagdoll)
        {
            rootCollider.enabled = false;
        }

        for (int index = 0; index < ragdollBodies.Length; index++)
        {
            Rigidbody body = ragdollBodies[index];
            if (body == null || body == rootRigidbody)
            {
                continue;
            }

            body.isKinematic = false;
            body.useGravity = true;
        }

        for (int index = 0; index < ragdollColliders.Length; index++)
        {
            Collider col = ragdollColliders[index];
            if (col == null || col == rootCollider)
            {
                continue;
            }

            col.enabled = true;
        }

        if (hitDirection.sqrMagnitude > 0.001f)
        {
            Rigidbody impulseBody = FindClosestRagdollBody();
            if (impulseBody != null)
            {
                impulseBody.AddForce(hitDirection.normalized * ragdollImpulse, ForceMode.Impulse);
            }
        }
    }

    Rigidbody FindClosestRagdollBody()
    {
        Rigidbody closest = null;
        float minDistance = float.MaxValue;

        for (int index = 0; index < ragdollBodies.Length; index++)
        {
            Rigidbody body = ragdollBodies[index];
            if (body == null || body == rootRigidbody)
            {
                continue;
            }

            float distance = Vector3.Distance(body.worldCenterOfMass, transform.position);
            if (distance < minDistance)
            {
                minDistance = distance;
                closest = body;
            }
        }

        return closest;
    }
}
