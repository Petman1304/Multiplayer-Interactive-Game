using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TruckHandler : MonoBehaviour
{
    [SerializeField]
    Rigidbody rb;

    [SerializeField]
    float truckAcceleration = 10;

    Vector2 input = Vector2.zero;

    private void FixedUpdate()
    {
        accelerate();
    }

    void accelerate()
    {
        rb.linearDamping = 0;
        rb.AddForce(rb.transform.forward * truckAcceleration);
    }
}
