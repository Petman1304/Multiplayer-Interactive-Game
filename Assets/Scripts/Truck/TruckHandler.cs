using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TruckHandler : MonoBehaviour
{
    [SerializeField]
    Rigidbody rb;

    [SerializeField]
    Transform truckModel;

    [SerializeField]
    float truckAcceleration = 50;

    [SerializeField]
    float steerInputMultiplier = 100;

    [SerializeField]
    float maxSteerVelocity = 10;

    [SerializeField]
    float maxForwardVelocity = 100;

    Vector2 input = Vector2.zero;

    private void Update()
    {
        truckModel.transform.rotation = Quaternion.Euler(0, rb.linearVelocity.x * 0.25f, 0);
    }

    private void FixedUpdate()
    {
        accelerate();
        Debug.Log($"Velocity : {rb.linearVelocity.z}");

        steer();
    }

    void accelerate()
    {
        rb.linearDamping = 0;
        rb.AddForce(rb.transform.forward * truckAcceleration);

        float truckVelocity = Mathf.Clamp(rb.linearVelocity.z, 0f, maxForwardVelocity);

        rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0, truckVelocity);
    }

    void steer()
    {
        if(Mathf.Abs(input.x) > 0)
        {
            rb.AddForce(rb.transform.right * steerInputMultiplier * input.x);

            float normalizedX = rb.linearVelocity.x / maxSteerVelocity;

            normalizedX = Mathf.Clamp(normalizedX, -1.0f, 1.0f);

            rb.linearVelocity = new Vector3(normalizedX * maxSteerVelocity, 0, rb.linearVelocity.z);
        }
        else
        {
            rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, new Vector3(0, 0, rb.linearVelocity.z), Time.fixedDeltaTime * 3);
        }
    }

    public void SetInput(Vector2 inputVector)
    {
        inputVector.Normalize();
        input = inputVector;
    }
}
