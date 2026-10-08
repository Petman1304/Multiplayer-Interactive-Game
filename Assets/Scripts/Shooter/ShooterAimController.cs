using System;
using System.Collections;
using UnityEngine;
using Unity.Cinemachine;

public class ShooterAimController : MonoBehaviour
{
    public GameObject moveCamera;
    public GameObject aimCamera;
    public SerialGyroInputHandler serialGyroInputHandler;
    public CinemachineRotationComposer cinemachineRotation;

    private Vector3 startOffset;
    [SerializeField]
    float aimSensitivity = 1.0f;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        startOffset = cinemachineRotation.TargetOffset;
    }

    // Update is called once per frame
    void Update()
    {
        if(serialGyroInputHandler.resetAngle == 1)
        {
            moveCamera.SetActive(false);
            aimCamera.SetActive(true);

            //StartCoroutine(showReticle());
            Vector3 inputAngel = new Vector3(serialGyroInputHandler.rotAngle.z, serialGyroInputHandler.rotAngle.y, 0);

            cinemachineRotation.TargetOffset = startOffset + inputAngel*aimSensitivity;

        }
        else
        {
            moveCamera.SetActive(true);
            aimCamera.SetActive(false);
        }
    }

    IEnumerator showReticle()
    {
        throw new NotImplementedException();
    }
}
