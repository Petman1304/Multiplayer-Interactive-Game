using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TruckHandler : MonoBehaviour
{
    [SerializeField]
    Rigidbody rb;

    [SerializeField]
    float truckAcceleration = 50;

    [SerializeField]
    float steerInputMultiplier = 100;

    Vector2 input = Vector2.zero;

    private void FixedUpdate()
    {
        accelerate();

        steer();
    }

    void accelerate()
    {
        rb.linearDamping = 0;
        rb.AddForce(rb.transform.forward * truckAcceleration);
    }

    void steer()
    {
        if(Mathf.Abs(input.x) > 0)
        {
            rb.AddForce(rb.transform.right * steerInputMultiplier * input.x);
        }
    }

    public void SetInput(Vector2 inputVector)
    {
        inputVector.Normalize();
        input = inputVector;
    }
}
