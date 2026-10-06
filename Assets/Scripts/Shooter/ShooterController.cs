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
    Transform truckTransform;

    private Vector3 lastTruckPosition;
    private float rotAngle;
    private void Start()
    {
        lastTruckPosition = truckTransform.position;
    }

    void Update()
    {

        //Vector3 truckMovement = new Vector3((truckTransform.position.x - lastTruckPosition.x) * 0.6f, 0, truckTransform.position.z - lastTruckPosition.z);
        //controller.Move(truckMovement);
        //lastTruckPosition = truckTransform.position;


        // Read input
        rotAngle = serialGyroInputHandler.rotAngle.y;
        Vector3 move = new Vector3(-rotAngle, 0, 0);
        move = Vector3.ClampMagnitude(move, 1f);
        

        if(move != Vector3.zero)
        {
            rb.AddForce(transform.right * move.x * playerSpeed);
        }
    }
}