using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;

public class PlayerMovement : NetworkBehaviour
{

    [SerializeField]
    PlayerInput playerInput;

    private InputAction move;
    private InputAction jump;

    public float speed = 5f;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (IsOwner)
        {
            GetComponent<Renderer>().material.color = Color.red;
        }
        else
        {
            // Jika bukan pemilik (player lain), kita bisa matikan komponen PlayerInput-nya
            // agar tidak saling berebut input keyboard dari satu device yang sama
            if (playerInput != null)
                playerInput.enabled = false;
            return;
        }

        // Ambil Action dari PlayerInput
        if (playerInput != null && playerInput.actions != null)
        {
            move = playerInput.actions["Move"];
            jump = playerInput.actions["Jump"];
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (!IsOwner) return;

        //float horizontal = Input.GetAxisRaw("Horizontal");
        float horizontal = move.ReadValue<Vector2>().x;

        //float vertical = Input.GetAxisRaw("Vertical");
        float vertical = move.ReadValue<Vector2>().y;

        Vector3 direction = new Vector3(horizontal, 0f, vertical).normalized;
        transform.Translate(direction * speed * Time.deltaTime);
    }
}