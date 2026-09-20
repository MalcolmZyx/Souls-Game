using UnityEngine;

public class Boid
{
    public Vector3 position;
    public Vector3 velocity;
    public Vector3 acceleration;
    public Transform transform; // the visual we move around

    public float maxSpeed = 5f;
    public float maxForce = 0.5f;

    public Boid(Transform t, Vector3 startPos, Vector3 startVel)
    {
        transform = t;
        position = startPos;
        velocity = startVel;
        acceleration = Vector3.zero;
        transform.position = position;
    }

    public void ApplyForce(Vector3 force)
    {
        acceleration += force;
    }

    public void UpdateBoid(float dt)
    {
        velocity += acceleration * dt;
        if (velocity.magnitude > maxSpeed)
            velocity = velocity.normalized * maxSpeed;

        position += velocity * dt;
        acceleration = Vector3.zero;

        transform.position = position;
        if (velocity.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(velocity);
    }
}
