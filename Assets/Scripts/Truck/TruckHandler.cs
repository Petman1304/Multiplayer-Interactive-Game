using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

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

    bool isCrashed = false;

    public event Action<TruckHandler> OnPlayerCrashed;

    //Stats
    float startPositionZ;
    float distanceTravelledZ = 0;
    public float DistanceTravelledZ => distanceTravelledZ;

    private void Start()
    {
        startPositionZ = transform.position.z;
    }

    private void Update()
    {

        if (isCrashed)
            return;

        truckModel.transform.rotation = Quaternion.Euler(0, rb.linearVelocity.x * 0.25f, 0);

        distanceTravelledZ = (transform.position.z - startPositionZ)/100;
    }

    private void FixedUpdate()
    {
        if (!isCrashed)
        {
            accelerate();
            //Debug.Log($"Velocity : {rb.linearVelocity.z}");

            steer();
        }
        else
        {
            rb.linearVelocity = new Vector3(0, 0, 0);
        }
        //TODO : game over if crashed
        
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

    public void SetMaxVelocity(float newMaxVelocity)
    {
        maxForwardVelocity = newMaxVelocity;
    }

    private void OnCollisionEnter(Collision collision)
    {
        Debug.Log($"Hit: {collision.collider.name}");

        isCrashed = true;

        OnPlayerCrashed?.Invoke(this);
    }
}
