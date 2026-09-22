using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class InputHandler : MonoBehaviour
{

    [SerializeField]
    TruckHandler truckHandler;

    InputAction moveAction;
    InputAction resetAction;

    private void Start()
    {
        moveAction = InputSystem.actions.FindAction("Move");
        resetAction = InputSystem.actions.FindAction("Reset");
    }

    // Update is called once per frame
    void Update()
    {
        Vector2 input = Vector2.zero;

        input.x = moveAction.ReadValue<Vector2>().x;

        truckHandler.SetInput(input);

        if(resetAction.IsPressed())
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);

        //if (Input.GetKeyDown(KeyCode.R))
        //    SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
