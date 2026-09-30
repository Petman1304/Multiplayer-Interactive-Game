using UnityEngine;
using System.IO.Ports;
using System;

public class ArduinoDriverInputHandler : MonoBehaviour
{
    [SerializeField]
    string portName;

    SerialPort serial;
    public float value;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        serial = new SerialPort(portName, 9600);
        serial.ReadTimeout = 100;

        try
        {
            serial.Open();
            Debug.Log($"Serial port {portName} opened succesfully");
        }
        catch (Exception e)
        {
            Debug.LogError($"Failde to open serial port {portName} : {e.Message}");
        }

    }

    // Update is called once per frame
    void Update()
    {
        if (serial == null || !serial.IsOpen)
            return;

        try
        {
            string data = serial.ReadLine();
            
            if(float.TryParse(data, out float result))
            {
                value = result;
            }


            
        }
        catch (TimeoutException)
        {
            return;
        }

    }

    private void OnDestroy()
    {
        if(serial != null && serial.IsOpen)
        {
            serial.Close();
        }
    }
}
