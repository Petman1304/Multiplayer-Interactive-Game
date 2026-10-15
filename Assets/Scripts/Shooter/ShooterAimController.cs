using System;
using System.Collections;
using UnityEngine;
using Unity.Cinemachine;

public class ShooterAimController : MonoBehaviour
{
    public CinemachineCamera moveCamera;
    public CinemachineCamera aimCamera;
    public SerialGyroInputHandler serialGyroInputHandler;
    public CinemachineRotationComposer cinemachineRotation;

    private WaitForSeconds wait = new WaitForSeconds(0.1f);
    
    [SerializeField]
    public Canvas reticle;

    private Vector3 startOffset;
    [SerializeField]
    float aimSensitivity = 1.0f;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        startOffset = cinemachineRotation.TargetOffset;

        StartCoroutine(showReticle());
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        if(serialGyroInputHandler.aimDown == 1)
        {
            //moveCamera.SetActive(false);
            //aimCamera.SetActive(true);

            moveCamera.Priority = 0;
            aimCamera.Priority = 1;

            //StartCoroutine(showReticle());
            Vector3 inputAngel = new Vector3(serialGyroInputHandler.rotAngle.z, serialGyroInputHandler.rotAngle.y, 0);

            cinemachineRotation.TargetOffset = startOffset + inputAngel*aimSensitivity;

        }
        else
        {
            //moveCamera.SetActive(true);
            //aimCamera.SetActive(false);

            moveCamera.Priority = 1;
            aimCamera.Priority = 0;
        }
    }

    IEnumerator showReticle()
    {
        while (true)
        {
            if (aimCamera.Priority == 1)
            {
                reticle.enabled = true;
            }
            else
            {
                reticle.enabled = false;
            }

            yield return wait;
        }
        
    }
}
