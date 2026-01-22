using System;
using System.Collections.Generic;
using UnityEngine;

public class SoftbodyPhysicsEngine : MonoBehaviour
{
    [Header("Gravity")]
    [SerializeField] bool useGravity;
    [SerializeField] float gravityAcceleration;

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
            
            // Handle collisions between softbodies
            HandleSoftbodyCollisions();

            // // For each softbody, allow points within to collide with each other
            // if (handleInternalCollisions)
            // {
            //     foreach (Softbody softbody in softbodies)
            //     {
            //         softbody.HandleInternalCollisions(deltaTime);
            //     }
            //
            //     Debug.Log("=================");
            // }

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

    void HandleSoftbodyCollisions()
    {
        for (int i = 0; i < softbodies.Count; i++)
        {
            Softbody softbody1 = softbodies[i];
            for (int j = 0; j < softbodies.Count; j++)
            {
                // TODO: Handle self collision
                if (i == j) continue;
                    
                Softbody softbody2 = softbodies[j];
                    
                // TODO: Check if bounding boxes intersect before continuing collision check
                // ...
                    
                // For each point in first softbody, check if it has crossed spring in other softbody
                for (int k = 0; k < softbody1.points.Length; k++)
                {
                    Point point = softbody1.points[k];
                    
                    // TODO: Check if point intersects bounding box before continuing collision check
                    // ...
                        
                    foreach (Spring spring in softbody2.springs)
                    {
                        Point springPoint1 = softbody2.points[spring.point1];
                        Point springPoint2 = softbody2.points[spring.point2];
                        
                        // Get displacement of spring line segment by taking average of both spring point displacements
                        Vector2 springPoint1Displacement = springPoint1.position - springPoint1.previousPosition;
                        Vector2 springPoint2Displacement = springPoint2.position - springPoint2.previousPosition;
                        Vector2 springDisplacement = (springPoint1Displacement + springPoint2Displacement) * 0.5f;

                        // Calculate previous point position relative to new spring line segment position (i.e. get prev point pos shifted by same amount as spring line)
                        // This is so that we account for spring line segment movement in addition to point movement (this allows us to handle case where point is stationary and only spring line is moving)
                        Vector2 relativePrevPointPosition = point.previousPosition + springDisplacement;
                        
                        // Check if point displacement vector intersects spring line segment, and get intersection point
                        bool intersects = GetLineIntersection(relativePrevPointPosition, point.position, springPoint1.position, springPoint2.position, out Vector2 intersection);
                        
                        // If no line segment intersection then there is no collision to handle
                        if (!intersects) continue;

                        // TODO: Push point back to intersection point
                        if (!point.isPinned)
                        {
                            point.position -= (point.position - intersection) * 1.01f;
                            point.previousPosition = point.position;
                        }
                        
                        // Update point
                        softbody1.points[k] = point;
                    }
                }
            }
        }
    }

    // Get intersection point between line segments AB and CD, returns false if intersection does not exist
    bool GetLineIntersection(Vector2 a, Vector2 b, Vector2 c, Vector2 d, out Vector2 intersection)
    {
        intersection = Vector2.zero;
        
        Vector2 r = b - a;
        Vector2 s = d - c;

        float rxs = Cross(r, s);
        float qpxr = Cross(c - a, r);

        // If rxs is 0 then the lines are parallel or collinear, so there is no single intersection
        if (Mathf.Approximately(rxs, 0))
            return false;

        float t = Cross(c - a, s) / rxs;
        float u = qpxr / rxs;

        // Intersection exists only if both t and u are between 0 and 1 inclusive
        if (t >= 0 && t <= 1 && u >= 0 && u <= 1)
        {
            intersection = a + t * r;
            return true;
        }

        return false;
    }

    // Calculate the 2D cross product
    float Cross(Vector2 v1, Vector2 v2)
    {
        return v1.x * v2.y - v1.y * v2.x;
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
