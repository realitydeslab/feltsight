# FeltSight - Haptic Gloves Unity SDK

This SDK provides integration between Unity and the custom haptic gloves hardware.

## Features
- Bluetooth Low Energy (BLE) communication
- Haptic feedback control
- Sensor data processing
- Hand tracking synchronization

## Setup

1. Import the SDK into your Unity 6.0 project
2. Add the HapticGlovesManager prefab to your scene
3. Configure the communication settings
4. Set up the haptic feedback profiles

## API Reference

### HapticGlovesManager
```csharp
public class HapticGlovesManager : MonoBehaviour
{
    public void ConnectGloves();
    public void DisconnectGloves();
    public void SetHapticFeedback(int finger, float intensity);
    public void SetVibrationPattern(int pattern);
    public void CalibrateSensors();
}
```

### HapticFeedbackProfile
```csharp
public class HapticFeedbackProfile
{
    public float[] fingerIntensities;
    public float duration;
    public AnimationCurve intensityCurve;
}
```

## Communication Protocol

The SDK communicates with the gloves using a custom BLE protocol:

- Service UUID: `6E400001-B5A3-F393-E0A9-E50E24DCCA9E`
- Characteristic UUIDs:
  - Command: `6E400002-B5A3-F393-E0A9-E50E24DCCA9E`
  - Feedback: `6E400003-B5A3-F393-E0A9-E50E24DCCA9E`
  - Sensor Data: `6E400004-B5A3-F393-E0A9-E50E24DCCA9E`

## Example Usage

```csharp
// Connect to gloves
hapticGlovesManager.ConnectGloves();

// Set haptic feedback for thumb
hapticGlovesManager.SetHapticFeedback(0, 0.8f);

// Set vibration pattern
hapticGlovesManager.SetVibrationPattern(1);

// Calibrate sensors
hapticGlovesManager.CalibrateSensors();
```

## Dependencies
- Unity 6.0 or later
- BLE plugin for Unity
- Newtonsoft.Json 