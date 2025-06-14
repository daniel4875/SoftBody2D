using UnityEngine;

public class Softbody : MonoBehaviour
{
    public Point[] points = { };
    [HideInInspector] public Spring[] springs = { };

    [Header("Point Settings")]
    [Tooltip("Mass of each point in the softbody in kg")]
    public float pointMass;

    [Header("Spring Settings")]
    [Tooltip("Stiffness (spring constant) of each spring in the softbody")]
    public float springStiffness;
    [Tooltip("Damping coefficient of each spring in the softbody")]
    public float springDamping;

    SoftbodyPhysicsEngine engine;

    void Start()
    {
        engine = FindFirstObjectByType<SoftbodyPhysicsEngine>();
        engine.RegisterSoftbody(this);

        InitialisePoints();
        SetSpringRestingLengths();
    }

    void InitialisePoints()
    {
        for (int i = 0; i < points.Length; i++)
        {
            Point point = points[i];
            point.previousPosition = point.position;
            point.force = Vector2.zero;
            points[i] = point;
        }
    }

    void SetSpringRestingLengths()
    {
        for (int i = 0; i < springs.Length; i++)
        {
            Spring spring = springs[i];
            Vector2 pos1 = points[spring.point1].position;
            Vector2 pos2 = points[spring.point2].position;
            spring.restingLength = (pos1 - pos2).magnitude;
            springs[i] = spring;
        }
    }

    // For each spring in softbody, calculate and apply forces on each connected point
    public void SpringPhysics(float deltaTime)
    {
        foreach (Spring spring in springs)
        {
            // Get points either side of spring
            Point point1 = points[spring.point1];
            Point point2 = points[spring.point2];

            Vector2 diff = point1.position - point2.position;
            Vector2 direction = diff.normalized;
            float distance = diff.magnitude;

            // Calculate spring extension relative to resting length
            float extension = distance - spring.restingLength;

            // Calculate the restoring force of the spring (it is proportional to extension of spring, but acts in opposite direction)
            Vector2 springDisplacement = extension * direction;
            Vector2 springForce = -springStiffness * springDisplacement; // F = -ke

            // With Verlet integration, we don't store velocity for the points, so we need to calculate it using current and previous positions
            // With Euler integration, we can just use the stored velocity
            Vector2 point1Velocity = engine.useVerletIntegration ? ((point1.position - point1.previousPosition) / deltaTime) : point1.velocity;
            Vector2 point2Velocity = engine.useVerletIntegration ? ((point2.position - point2.previousPosition) / deltaTime) : point2.velocity;

            // Calculate the damping force (it resists the relative velocity along the spring)
            Vector2 relativeVelocity = point1Velocity - point2Velocity;
            Vector2 velocityAlongSpring = Vector2.Dot(direction, relativeVelocity) * direction;
            Vector2 dampingForce = -springDamping * velocityAlongSpring;

            // Calculate the total force
            Vector2 totalForce = springForce + dampingForce;

            // Update forces for points (we have been calculating the force for point1, so flip direction for point2)
            point1.force += totalForce;
            point2.force -= totalForce;

            // Write updates to points array
            points[spring.point1] = point1;
            points[spring.point2] = point2;
        }
    }

    // For each point in softbody, update velocity using the total force accumulated during the current simulation step, then use the new velocity to update the position
    public void ApplyForces(float deltaTime)
    {
        if (engine.useVerletIntegration)
            VerletIntegration(deltaTime);
        else
            EulerIntegration(deltaTime);
    }

    void EulerIntegration(float deltaTime)
    {
        for (int i = 0; i < points.Length; i++)
        {
            Point point = points[i];

            // Pinned points don't move, therefore can't have forces applied to them
            if (point.isPinned) continue;

            // Calculate and apply velocity change
            Vector2 acceleration = point.force / pointMass; // a = F/m (rearranged form of F = ma)
            Vector2 velocityChange = acceleration * deltaTime; // v = at
            point.velocity += velocityChange;

            // Calculate and apply displacement to get updated position
            Vector2 displacement = point.velocity * deltaTime; // s = vt
            point.position += displacement;

            points[i] = point;
        }
    }

    void VerletIntegration(float deltaTime)
    {
        for (int i = 0; i < points.Length; i++)
        {
            Point point = points[i];

            // Pinned points don't move, therefore can't have forces applied to them
            if (point.isPinned) continue;

            // Get acceleration using accumulated force
            Vector2 acceleration = point.force / pointMass; // a = F/m (rearranged form of F = ma)

            // Use verlet integration formula to calculate new position using current position, previous position and acceleration
            Vector2 newPosition = 2 * point.position - point.previousPosition + acceleration * deltaTime * deltaTime;

            // Update previous position and set current position to new one
            point.previousPosition = point.position;
            point.position = newPosition;

            points[i] = point;
        }
    }

    // For each point in softbody, add the given force to its current accumulated force
    public void AddForceToAllPoints(Vector2 force)
    {
        for (int i = 0; i < points.Length; i++)
        {
            Point point = points[i];
            point.force += force;
            points[i] = point;
        }
    }

    // Set the force on all points in softbody to zero
    public void ResetForces()
    {
        for (int i = 0; i < points.Length; i++)
        {
            Point point = points[i];
            point.force = Vector2.zero;
            points[i] = point;
        }
    }

    // Visualise softbody in scene view
    private void OnDrawGizmos()
    {
        // Draw points
        foreach (Point point in points)
        {
            Gizmos.color = point.isPinned ? Color.cyan : Color.red;
            Gizmos.DrawSphere(point.position, 0.2f);
        }

        // Draw springs (point connections)
        foreach (Spring spring in springs)
        {
            Point point1 = points[spring.point1];
            Point point2 = points[spring.point2];

            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(point1.position, point2.position);
        }
    }
}

[System.Serializable]
public struct Point
{
    public Vector2 position;
    [HideInInspector] public Vector2 previousPosition;
    public Vector2 velocity;
    [HideInInspector] public Vector2 force;
    public bool isPinned;
}

[System.Serializable]
public struct Spring
{
    public int point1;
    public int point2;
    [HideInInspector] public float restingLength;
}
