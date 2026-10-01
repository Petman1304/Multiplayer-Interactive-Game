using System.Globalization;
using UnityEngine;
using System.IO.Ports;

public class SerialGyroInputHandler : MonoBehaviour
{
    [SerializeField]
    string portName;

    public Vector3 value;

    private SerialPort serial;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        serial = new SerialPort(portName, 115200);
        serial.ReadTimeout = 10;

        try
        {
            serial.Open();
        }
        catch (System.Exception e) 
        {
            Debug.LogError($"Failed to open serial port {portName} : {e.Message}");
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (serial == null || !serial.IsOpen)
            return;
        
        try
        {
            string data = serial.ReadLine().Trim();

            string[] values = data.Split(',');

            if (values.Length != 3)
                return;

            if (
                float.TryParse(values[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float rotX) &&
                float.TryParse(values[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float rotY) &&
                float.TryParse(values[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float rotZ)
        ) {
                value = new Vector3(rotX, rotY, rotZ);
            } 
        }
        catch (System.TimeoutException)
        {

        }
    }

    private void OnDestroy()
    {
        if (serial != null && serial.IsOpen)
            serial.Close();
    }
}
