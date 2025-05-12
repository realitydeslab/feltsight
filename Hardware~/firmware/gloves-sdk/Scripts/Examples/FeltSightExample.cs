using UnityEngine;
using FeltSight;
using System.Threading.Tasks;

public class FeltSightExample : MonoBehaviour
{
    private FeltSightGlovesManager glovesManager;

    private void Start()
    {
        glovesManager = FeltSightGlovesManager.Instance;
        
        // Subscribe to events
        glovesManager.OnConnected += OnGlovesConnected;
        glovesManager.OnDisconnected += OnGlovesDisconnected;
        glovesManager.OnSensorDataReceived += OnSensorDataReceived;

        // Connect to gloves
        ConnectToGloves();
    }

    private async void ConnectToGloves()
    {
        bool success = await glovesManager.Connect();
        if (success)
        {
            Debug.Log("Successfully connected to gloves");
        }
        else
        {
            Debug.LogError("Failed to connect to gloves");
        }
    }

    private void OnGlovesConnected()
    {
        Debug.Log("Gloves connected event received");
    }

    private void OnGlovesDisconnected()
    {
        Debug.Log("Gloves disconnected event received");
    }

    private void OnSensorDataReceived(FeltSightGlovesManager.SensorData sensorData)
    {
        // Example: Print flex sensor values
        string flexValues = "Flex Values: ";
        for (int i = 0; i < 5; i++)
        {
            flexValues += $"{sensorData.flexValues[i]} ";
        }
        Debug.Log(flexValues);

        // Example: Print accelerometer data
        Debug.Log($"Acceleration: {sensorData.accel}");
    }

    private void Update()
    {
        if (!glovesManager) return;

        // Example: Set haptic feedback based on some condition
        // This is just an example - replace with your actual logic
        float time = Time.time;
        for (int i = 0; i < 5; i++)
        {
            // Create a simple wave pattern
            float intensity = Mathf.Abs(Mathf.Sin(time + i * 0.5f));
            glovesManager.SetFingerIntensity(i, intensity);
        }

        // Set pattern intensity based on some condition
        float patternIntensity = Mathf.Abs(Mathf.Sin(time * 2f));
        glovesManager.SetPatternIntensity(patternIntensity);
    }

    private void OnDestroy()
    {
        if (glovesManager)
        {
            // Unsubscribe from events
            glovesManager.OnConnected -= OnGlovesConnected;
            glovesManager.OnDisconnected -= OnGlovesDisconnected;
            glovesManager.OnSensorDataReceived -= OnSensorDataReceived;
        }
    }
} 