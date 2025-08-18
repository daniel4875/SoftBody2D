using System.Collections.Generic;
using UnityEngine;

public class SoftbodyPhysicsEngine : MonoBehaviour
{
    [Header("Gravity")]
    [SerializeField] bool useGravity;
    [SerializeField] float gravityAcceleration;

    [Header("Motion")]
    public bool useVerletIntegration;

    [Header("Bounding Box")]
    [SerializeField] float groundY;
    [SerializeField] float ceilingY;
    [SerializeField] float leftWallX;
    [SerializeField] float rightWallX;

    [Header("Simulation")]
    [Tooltip("The number of sub steps of the simulation to run per frame")]
    [SerializeField] int subSteps = 1;

    [Header("Softbody")]
    [SerializeField] bool handleInternalCollisions;

    List<Softbody> softbodies = new List<Softbody>();

    void FixedUpdate()
    {
        float deltaTime = Time.fixedDeltaTime / subSteps;

        for (int i = 0; i < subSteps; i++)
        {
            // Set the force on all points in all softbodies to zero
            ResetForces();

            // Apply gravity force to all points in all softbodies
            if (useGravity) Gravity();

            // Apply spring forces to all points in all softbodies
            foreach (Softbody softbody in softbodies)
            {
                softbody.SpringPhysics(deltaTime);
            }

            // Update all point velocities and positions in all softbodies based on the each point's total force calculated during this simulation step
            foreach (Softbody softbody in softbodies)
            {
                softbody.ApplyForces(deltaTime);
            }

            // For each softbody, allow points within to collide with each other
            if (handleInternalCollisions)
            {
                foreach (Softbody softbody in softbodies)
                {
                    softbody.HandleInternalCollisions();
                }
            }

            // Handle collision with outer bounds of simulation
            BoundsCollision();
        }
    }

    void Gravity()
    {
        foreach (Softbody softbody in softbodies)
        {
            Vector2 gravityForce = softbody.massOfPoint * gravityAcceleration * Vector2.down;
            softbody.AddForceToAllPoints(gravityForce);
        }
    }

    void ResetForces()
    {
        foreach (Softbody softbody in softbodies)
        {
            softbody.ResetForces();
        }
    }

    void BoundsCollision()
    {
        foreach (Softbody softbody in softbodies)
        {
            for (int i = 0; i < softbody.points.Length; i++)
            {
                Point point = softbody.points[i];

                // Check if collision with ground has occurred
                if (point.position.y < groundY)
                {
                    // Correct position by forcing point to be at ground level
                    point.position.y = groundY;

                    // Set vertical velocity to zero
                    point.velocity.y = 0f;
                }

                // Check if collision with ceiling has occurred
                if (point.position.y > ceilingY)
                {
                    // Correct position by forcing point to be at ceiling level
                    point.position.y = ceilingY;

                    // Set vertical velocity to zero
                    point.velocity.y = 0f;
                }

                // Check if collision with left wall has occurred
                if (point.position.x < leftWallX)
                {
                    // Correct position by forcing point to be at left wall x value
                    point.position.x = leftWallX;

                    // Set horizontal velocity to zero
                    point.velocity.x = 0f;
                }

                // Check if collision with right wall has occurred
                if (point.position.x > rightWallX)
                {
                    // Correct position by forcing point to be at right wall x value
                    point.position.x = rightWallX;

                    // Set horizontal velocity to zero
                    point.velocity.x = 0f;
                }

                softbody.points[i] = point;
            }
        }
    }

    public void RegisterSoftbody(Softbody softbody)
    {
        softbodies.Add(softbody);
    }

    private void OnDrawGizmos()
    {
        // Draw bounding box
        Gizmos.color = Color.green;
        Gizmos.DrawLine(new Vector2(leftWallX, groundY), new Vector2(rightWallX, groundY));
        Gizmos.DrawLine(new Vector2(leftWallX, ceilingY), new Vector2(rightWallX, ceilingY));
        Gizmos.DrawLine(new Vector2(leftWallX, groundY), new Vector2(leftWallX, ceilingY));
        Gizmos.DrawLine(new Vector2(rightWallX, groundY), new Vector2(rightWallX, ceilingY));
    }
}
