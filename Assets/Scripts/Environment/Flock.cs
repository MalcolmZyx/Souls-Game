using System.Collections.Generic;
using UnityEngine;

public class Flock : MonoBehaviour
{
    [Header("Spawn")]
    public GameObject boidPrefab;        // drag your visual prefab here
    public int boidCount = 100;
    public float spawnRadius = 10f;

    [Header("Perception")]
    public float separationRadius = 1.5f;
    public float neighborRadius = 4f;    // used for alignment & cohesion

    [Header("Weights")]
    public float separationWeight = 1.5f;
    public float alignmentWeight = 1.0f;
    public float cohesionWeight = 1.0f;
    public float boundsWeight = 1.0f;

    [Header("Tuning")]
    public float maxSpeed = 5f;
    public float maxForce = 0.5f;
    public float boundsSize = 20f;       // soft cube to keep flock contained

    [Header("Gizmos")]
    public bool drawGizmos = true;
    public bool drawRadiiOnAllBoids = false;     // expensive — only for debugging
    public bool drawVelocityVectors = false;
    public Color spawnColor = new Color(0.4f, 0.8f, 1f, 0.25f);
    public Color boundsColor = new Color(1f, 0.6f, 0.2f, 0.6f);
    public Color separationColor = new Color(1f, 0.3f, 0.3f, 0.8f);
    public Color neighborColor = new Color(0.3f, 1f, 0.5f, 0.6f);
    public Color velocityColor = Color.yellow;

    private List<Boid> aliveBoids = new List<Boid>();

    void Start()
    {
        if (boidPrefab == null)
        {
            Debug.LogError("Flock: assign a boidPrefab in the inspector.");
            return;
        }

        for (int i = 0; i < boidCount; i++)
        {
            Vector3 pos = transform.position + Random.insideUnitSphere * spawnRadius;
            Vector3 vel = Random.insideUnitSphere * maxSpeed;

            GameObject go = Instantiate(boidPrefab, pos, Quaternion.identity, transform);
            Boid b = new Boid(go.transform, pos, vel);
            b.maxSpeed = maxSpeed;
            b.maxForce = maxForce;
            aliveBoids.Add(b);
        }
    }

    void Update()
    {
        float dt = Time.deltaTime;

        // O(n^2) — fine up to a few hundred boids. Use a spatial grid if you scale up.
        for (int i = 0; i < aliveBoids.Count; i++)
        {
            Boid b = aliveBoids[i];

            Vector3 sep = Vector3.zero;
            Vector3 ali = Vector3.zero;
            Vector3 coh = Vector3.zero;
            int sepCount = 0, neiCount = 0;

            for (int j = 0; j < aliveBoids.Count; j++)
            {
                if (i == j) continue;
                Boid other = aliveBoids[j];
                Vector3 offset = b.position - other.position;
                float dist = offset.magnitude;

                if (dist < separationRadius && dist > 0.0001f)
                {
                    sep += offset.normalized / dist; // stronger when closer
                    sepCount++;
                }
                if (dist < neighborRadius)
                {
                    ali += other.velocity;
                    coh += other.position;
                    neiCount++;
                }
            }

            if (sepCount > 0)
            {
                sep /= sepCount;
                sep = Steer(sep, b);
            }
            if (neiCount > 0)
            {
                ali /= neiCount;
                ali = Steer(ali, b);

                coh /= neiCount;
                coh = Steer(coh - b.position, b); // desired direction toward centroid
            }

            // Soft bounds: nudge back toward origin if outside the cube
            Vector3 bounds = Vector3.zero;
            Vector3 center = transform.position;
            Vector3 d = b.position - center;
            if (Mathf.Abs(d.x) > boundsSize) bounds.x = -Mathf.Sign(d.x);
            if (Mathf.Abs(d.y) > boundsSize) bounds.y = -Mathf.Sign(d.y);
            if (Mathf.Abs(d.z) > boundsSize) bounds.z = -Mathf.Sign(d.z);
            if (bounds != Vector3.zero) bounds = Steer(bounds, b);

            b.ApplyForce(sep * separationWeight);
            b.ApplyForce(ali * alignmentWeight);
            b.ApplyForce(coh * cohesionWeight);
            b.ApplyForce(bounds * boundsWeight);

            b.UpdateBoid(dt);
        }
    }

    // Reynolds-style steering: desired direction at maxSpeed, minus current velocity, clamped to maxForce.
    Vector3 Steer(Vector3 desired, Boid b)
    {
        if (desired.sqrMagnitude < 0.0001f) return Vector3.zero;
        desired = desired.normalized * b.maxSpeed;
        Vector3 force = desired - b.velocity;
        if (force.magnitude > b.maxForce)
            force = force.normalized * b.maxForce;
        return force;
    }

    void OnDrawGizmosSelected()
    {
        if (!drawGizmos) return;

        Vector3 center = transform.position;

        // Spawn sphere
        Gizmos.color = spawnColor;
        Gizmos.DrawWireSphere(center, spawnRadius);

        // Soft bounds cube
        Gizmos.color = boundsColor;
        Gizmos.DrawWireCube(center, Vector3.one * boundsSize * 2f);

        if (Application.isPlaying && aliveBoids.Count > 0)
        {
            // Show radii on first boid as a sample, plus optional all-boids debug
            if (drawRadiiOnAllBoids)
            {
                for (int i = 0; i < aliveBoids.Count; i++)
                    DrawBoidGizmos(aliveBoids[i]);
            }
            else
            {
                DrawBoidGizmos(aliveBoids[0]);
            }

            if (drawVelocityVectors)
            {
                Gizmos.color = velocityColor;
                for (int i = 0; i < aliveBoids.Count; i++)
                {
                    Boid b = aliveBoids[i];
                    Gizmos.DrawLine(b.position, b.position + b.velocity * 0.3f);
                }
            }
        }
        else
        {
            // Edit mode: preview the radii at the flock origin so you can tune visually
            Gizmos.color = separationColor;
            Gizmos.DrawWireSphere(center, separationRadius);
            Gizmos.color = neighborColor;
            Gizmos.DrawWireSphere(center, neighborRadius);
        }
    }

    void DrawBoidGizmos(Boid b)
    {
        Gizmos.color = separationColor;
        Gizmos.DrawWireSphere(b.position, separationRadius);
        Gizmos.color = neighborColor;
        Gizmos.DrawWireSphere(b.position, neighborRadius);
    }
}