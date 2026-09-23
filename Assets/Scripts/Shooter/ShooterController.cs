using UnityEngine;
using UnityEngine.InputSystem;

public class ShooterController : MonoBehaviour
{
    [SerializeField]
    private float playerSpeed = 5.0f;
    
    [SerializeField]
    private float jumpHeight = 1.5f;

    [SerializeField]
    private float gravityValue = -9.81f;

    [SerializeField]
    Rigidbody rigidbody;

    public CharacterController controller;
    private PlayerInput playerInput;
    private Vector3 playerVelocity;
    private bool groundedPlayer;

    private InputAction moveAction;
    private InputAction jumpAction;

    [SerializeField]
    Transform truckTransform;

    private Vector3 lastTruckPosition;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        playerInput = GetComponent<PlayerInput>();

        moveAction = playerInput.actions["Move"];
        jumpAction = playerInput.actions["Jump"];
    }
    private void Start()
    {
        

        lastTruckPosition = truckTransform.position;

    }

    void Update()
    {
        groundedPlayer = controller.isGrounded;

        if (groundedPlayer)
        {
            // Slight downward velocity to keep grounded stable
            if (playerVelocity.y < -2f)
                playerVelocity.y = -2f;
        }

        Vector3 truckMovement = new Vector3((truckTransform.position.x - lastTruckPosition.x) * 0.6f, 0, truckTransform.position.z - lastTruckPosition.z);
        controller.Move(truckMovement);
        lastTruckPosition = truckTransform.position;


        // Read input
        Vector2 input = moveAction.ReadValue<Vector2>();
        Debug.Log($"Input : {input}");
        Vector3 move = new Vector3(input.x, 0, input.y);
        move = Vector3.ClampMagnitude(move, 1f);
        

        if (move != Vector3.zero)
            transform.forward = move;

        // Jump using WasPressedThisFrame()
        if (groundedPlayer && jumpAction.WasPressedThisFrame())
        {
            playerVelocity.y = Mathf.Sqrt(jumpHeight * -2f * gravityValue);
        }

        // Apply gravity
        playerVelocity.y += gravityValue * Time.deltaTime;

        // Move
        Vector3 finalMove = move * playerSpeed + Vector3.up * playerVelocity.y;
        controller.Move(finalMove * Time.deltaTime);
    }
}