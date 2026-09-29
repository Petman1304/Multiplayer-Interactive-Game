//using UnityEngine;
//using Unity.Netcode;
//using UnityEngine.InputSystem;

//public class PlayerMovement : NetworkBehaviour
//{

//    [SerializeField]
//    PlayerInput playerInput;

//    private InputAction move;
//    private InputAction jump;

//    public float speed = 5f;

//    // Start is called once before the first execution of Update after the MonoBehaviour is created
//    void Start()
//    {
//        if (IsOwner)
//        {
//            GetComponent<Renderer>().material.color = Color.red;

//            // Paksa Player Input untuk terhubung ke Keyboard secara manual jika belum pasang
//            if (playerInput != null && playerInput.playerIndex == 0)
//            {
//                // Untuk Host/Player 1
//                playerInput.SwitchCurrentControlScheme("Keyboard&Mouse", Keyboard.current);
//            }
//            else if (playerInput != null)
//            {
//                // Untuk Client/Player berikutnya
//                playerInput.SwitchCurrentControlScheme("Keyboard&Mouse", Keyboard.current);
//            }
//        }
//        else
//        {
//            // Jika bukan pemilik (player lain), kita bisa matikan komponen PlayerInput-nya
//            // agar tidak saling berebut input keyboard dari satu device yang sama
//            if (playerInput != null)
//                playerInput.enabled = false;
//            return;
//        }

//        // Ambil Action dari PlayerInput
//        if (playerInput != null && playerInput.actions != null)
//        {
//            move = playerInput.actions["Move"];
//            jump = playerInput.actions["Jump"];
//        }
//    }

//    // Update is called once per frame
//    void Update()
//    {
//        if (!IsOwner) return;

//        //float horizontal = Input.GetAxisRaw("Horizontal");
//        float horizontal = move.ReadValue<Vector2>().x;

//        //float vertical = Input.GetAxisRaw("Vertical");
//        float vertical = move.ReadValue<Vector2>().y;

//        Vector3 direction = new Vector3(horizontal, 0f, vertical).normalized;
//        transform.Translate(direction * speed * Time.deltaTime);
//    }
//}



using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;

public class PlayerMovement : NetworkBehaviour
{
    [SerializeField]
    private PlayerInput playerInput;

    private InputAction move;
    private InputAction jump;

    public float speed = 5f;

    void Awake()
    {
        if (playerInput == null)
        {
            playerInput = GetComponent<PlayerInput>();
        }
    }

    void Start()
    {
        if (IsOwner)
        {
            if (TryGetComponent(out Renderer playerRenderer))
            {
                playerRenderer.material.color = Color.red;
            }
        }
        else
        {
            if (playerInput != null)
                playerInput.enabled = false;
            return;
        }

        if (playerInput != null && playerInput.actions != null)
        {
            move = playerInput.actions["Move"];
            jump = playerInput.actions["Jump"];

            if (move != null) move.Enable();
            if (jump != null) jump.Enable();
        }
    }

    void OnDisable()
    {
        if (move != null) move.Disable();
        if (jump != null) jump.Disable();
    }

    void Update()
    {
        if (!IsOwner) return;
        if (move == null) return;

        Vector2 moveInput = move.ReadValue<Vector2>();
        float horizontal = moveInput.x;
        float vertical = moveInput.y;

        Vector3 direction = new Vector3(horizontal, 0f, vertical).normalized;
        transform.Translate(direction * speed * Time.deltaTime);
    }
}