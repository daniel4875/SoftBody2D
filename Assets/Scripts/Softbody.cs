using UnityEngine;

public class Softbody : MonoBehaviour
{
    //[HideInInspector] public Point[] points = { };
    public Point[] points = { };
    [HideInInspector] public Spring[] springs = { };

    [Header("Point Mass Settings")]
    [Tooltip("Mass of each point in the softbody in kg")]
    public float massOfPoint;
    public float collisionRadiusOfPoint;

    [Header("Spring Settings")]
    [Tooltip("Stiffness (spring constant) of each spring in the softbody")]
    public float springStiffness;
    [Tooltip("Damping coefficient of each spring in the softbody")]
    public float springDamping;

    private SoftbodyPhysicsEngine engine;

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
            Vector2 point1Velocity = (point1.position - point1.previousPosition) / deltaTime;
            Vector2 point2Velocity = (point2.position - point2.previousPosition) / deltaTime;

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

    // For each point in softbody, use the total force accumulated during the current simulation step to update position using Verlet integration
    public void ApplyForces(float deltaTime)
    {
        for (int i = 0; i < points.Length; i++)
        {
            Point point = points[i];

            // Pinned points don't move, therefore can't have forces applied to them
            if (point.isPinned) continue;

            // Get acceleration using accumulated force
            Vector2 acceleration = point.force / massOfPoint; // a = F/m (rearranged form of F = ma)

            // Use verlet integration formula to calculate new position using current position, previous position and acceleration
            Vector2 newPosition = 2 * point.position - point.previousPosition + acceleration * deltaTime * deltaTime;

            // Update previous position and set current position to new one
            point.previousPosition = point.position;
            point.position = newPosition;

            points[i] = point;
        }
    }

    // Allow points within softbody to collide with each other
    public void HandleInternalCollisions(float deltaTime)
    {
        for (int i = 0; i < points.Length; i++)
        {
            Point point1 = points[i];

            for (int j = i + 1; j < points.Length; j++)
            {
                Point point2 = points[j];

                float minDistRequired = 2 * collisionRadiusOfPoint;

                Vector2 diff = point2.position - point1.position;
                float dist = diff.magnitude;

                // If both points are within each other's collision radius, then we have a collision to resolve
                if (dist < minDistRequired)
                {
                    // Calculate offset to move both points by such that they no longer overlap
                    float amountToSeparate = (minDistRequired - dist) / 2;
                    Vector2 dir = diff.normalized;
                    Vector2 offset = amountToSeparate * dir;

                    // Adjust position of both points (if not pinned)
                    if (!point1.isPinned)
                        point1.position -= offset;
                    if (!point2.isPinned)
                        point2.position += offset;

                    // With Verlet integration, previous position needs to be adjusted so that the implicit velocity has no velocity along collision direction
                    
                    // Calculate velocity from current and previous positions
                    Vector2 point1Velocity = (point1.position - point1.previousPosition) / deltaTime;
                    Vector2 point2Velocity = (point2.position - point2.previousPosition) / deltaTime;

                    // Correct point1 velocity
                    Vector2 velocityAlongCollision = Vector2.Dot(point1Velocity, dir) * dir;
                    point1Velocity -= velocityAlongCollision;

                    // Correct point2 velocity
                    velocityAlongCollision = Vector2.Dot(point2Velocity, dir) * dir;
                    point2Velocity -= velocityAlongCollision;

                    // Translate velocity into previous position
                    point1.previousPosition = point1.position - (point1Velocity * deltaTime);
                    point2.previousPosition = point2.position - (point2Velocity * deltaTime);

                    // Update both points in array
                    points[i] = point1;
                    points[j] = point2;
                }
            }
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
