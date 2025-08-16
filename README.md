# FeltSight - Vision Pro Haptics Gloves Art Project

This project combines Apple Vision Pro with custom haptic gloves to create an immersive art experience. The project consists of several components:

## Project Components

1. **Unity Vision Pro Application**
   - Main application running on Vision Pro
   - Hand tracking and haptic feedback integration
   - Art interaction system

2. **Haptic Gloves Unity SDK**
   - Custom SDK for glove integration
   - Communication protocols
   - Haptic feedback control

3. **Gloves Firmware**
   - Based on Adafruit hardware
   - Controls haptic feedback
   - Handles sensor data

4. **Gloves Design**
   - Physical glove design
   - Component placement
   - Wiring schematics

## Setup Instructions

### Prerequisites
- Apple Vision Pro
- Unity 6.0 or later
- Adafruit hardware components
- Arduino IDE
- Xcode (for Vision Pro development)

### Hardware Requirements
- Adafruit Feather nRF52840
- Haptic feedback motors
- Flex sensors
- IMU sensors
- Power supply components

### Software Requirements
- Unity 6.0 or later
- Apple Vision Pro SDK
- Arduino IDE
- Visual Studio Code (optional)


## Getting Started

1. Clone this repository
3. Build and deploy the Unity application to Vision Pro
4. Flash the firmware to the gloves
5. Connect and test the system

## License
MIT License 


# FeltSight - Vision Pro Unity Application

This is a Unity 6000.1.5f1 project configured for Apple Vision Pro development with the following key components:

## Core Systems
- **Hand Tracking**: Uses Unity XR Hands with custom `MyHand.cs` component for hand pose tracking and distance calculations
- **BLE Communication**: `BLESendJointV.cs` handles Bluetooth communication with haptic gloves using finger velocity mapping
- **Visual Effects**: VFX Graph-based visual effects in `/Assets/VFX/` with `VFXMan.cs` controller
- **AR Mesh Processing**: Handles Vision Pro mesh data for spatial interactions
- **YOLO Integration**: Computer vision pipeline using Barracuda for object detection/segmentation

## Features
- Vision Pro hand tracking integration
- Haptic glove communication
- Art interaction system
- Real-time feedback


### Key Scripts Architecture

#### Hand Tracking (`/Assets/Scripts/`)
- `MyHand.cs` - Core hand tracking with pose data, palm distance calculations, and finger velocity tracking
- `HandRaycaster.cs` - Hand-based raycasting for spatial interactions  
- `HandVisualizer.cs` - Visual representation of hand data

#### BLE Communication
- `BLESendJointV.cs` - Maps finger velocity (0-0.3 m/s) to BLE speed parameters (1.0x-4.0x) with OneDollar filtering
- `ReadSthFromServer.cs` - Fetches configuration parameters from a web server and applies them to BLESendJointV
- `/Assets/Scripts/Library/CoreBluetooth/` - Core Bluetooth wrapper for Unity-iOS communication
- `/Assets/Scripts/Library/NativeInterface/` - Native iOS interface layer

#### Computer Vision (`/Assets/Scripts/Library/Yolo/`)
- `Detector.cs` - Main YOLO detection controller using Barracuda
- `YOLOv8.cs` and `YOLOv8Segmentation.cs` - YOLO model implementations
- `/Assets/Scripts/Library/Yolo/TextureProviders/` - Camera and video input providers

#### VFX System (`/Assets/VFX/`)
- `VFXMan.cs` - Controls Visual Effect Graph assets with AR mesh integration
- Custom VFX operators for spatial effects and mesh interactions

### Unity Packages
Key dependencies from `manifest.json`:
- `com.unity.xr.visionos`: "2.2.4" - Vision Pro platform support
- `com.unity.xr.hands`: "1.5.1" - Hand tracking
- `com.unity.xr.interaction.toolkit`: "3.1.2" - XR interactions  
- `com.unity.visualeffectgraph`: "17.1.0" - VFX system
- `com.unity.barracuda` - Machine learning inference
- `com.unity.render-pipelines.universal`: "17.1.0" - URP rendering

### Scenes
- `DebugVFX.unity` - Main Sceen
- `DebugHandGestures.unity` - Hand tracking testing and debugging
- `YoloScenes/Detection.unity` - Object detection testing
- `YoloScenes/Segmentation.unity` - Segmentation testing


## Setup

1. Open the project in Unity 6.0 or later
2. Install required packages:
   - Apple Vision Pro SDK
   - Haptic Gloves SDK (included in this project)
   - XR Interaction Toolkit

3. Configure build settings:
   - Set platform to Vision Pro
   - Configure signing and provisioning
   - Set up entitlements

## Project Structure
```
unity/
├── Assets/ # Unity Project
├── Hardware/            # Glove design and program
├── Packages/             # Unity Project
└── ProjectSettings/      # Unity project settings
```

## Development

1. Open the main scene in `Assets/Scenes/Main.unity`
2. Configure the HapticGlovesManager in the scene
3. Test with the Vision Pro simulator
4. Build and deploy to device

## Dependencies
- Unity 6.0 or later
- Apple Vision Pro SDK
- XR Interaction Toolkit
- Haptic Gloves SDK 