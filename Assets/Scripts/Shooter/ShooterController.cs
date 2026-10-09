using UnityEngine;
using UnityEngine.InputSystem;

public class ShooterController : MonoBehaviour
{
    [SerializeField]
    private float playerSpeed = 5.0f;
    
    [SerializeField]
    Rigidbody rb;

    [SerializeField]
    SerialGyroInputHandler serialGyroInputHandler;

    [SerializeField]
    Rigidbody truckRigidBody;

    private Vector3 lastTruckPosition;
    private float rotAngle;
    private void Start()
    {
        lastTruckPosition = truckRigidBody.position;
    }

    void FixedUpdate()
    {
        // Read input
        rotAngle = serialGyroInputHandler.rotAngle.x;
        if(Mathf.Abs(rotAngle) > 0.1)
        {
            Vector3 playerMovement = Vector3.ClampMagnitude(new Vector3(-rotAngle, 0, 0), 1f);
            playerMovement = transform.right * playerMovement.x * playerSpeed * Time.fixedDeltaTime;
            rb.MovePosition(rb.position + playerMovement);
        }
    }
}