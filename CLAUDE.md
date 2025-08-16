# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

FeltSight is a Vision Pro haptics gloves art project that combines Apple Vision Pro with custom haptic gloves to create an immersive art experience. The project includes:

1. **Unity Vision Pro Application** - Main app running on Vision Pro with hand tracking and haptic feedback
2. **Haptic Gloves Hardware** - Custom gloves with Adafruit Feather nRF52840 microcontroller  
3. **Firmware** - Arduino-based firmware for glove control and BLE communication
4. **Hardware Design** - Circuit diagrams and component specifications

## Unity Project Structure

This is a Unity 6000.1.5f1 project configured for Apple Vision Pro development with the following key components:

### Core Systems
- **Hand Tracking**: Uses Unity XR Hands with custom `MyHand.cs` component for hand pose tracking and distance calculations
- **BLE Communication**: `BLESendJointV.cs` handles Bluetooth communication with haptic gloves using finger velocity mapping
- **Visual Effects**: VFX Graph-based visual effects in `/Assets/VFX/` with `VFXMan.cs` controller
- **AR Mesh Processing**: Handles Vision Pro mesh data for spatial interactions
- **YOLO Integration**: Computer vision pipeline using Barracuda for object detection/segmentation

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
- `DebugHandGestures.unity` - Hand tracking testing and debugging
- `DebugVFX.unity` - Visual effects testing
- `YoloScenes/Detection.unity` - Object detection testing
- `YoloScenes/Segmentation.unity` - Segmentation testing

## Development Commands

Unity projects don't typically use traditional build commands. Development workflow:

1. **Open Project**: Open in Unity 6000.1.5f1 or later
2. **Platform Setup**: Switch to visionOS platform in Build Settings  
3. **Build**: Use Unity's Build Settings → Build or Build And Run
4. **Testing**: Use Vision Pro Simulator or device deployment

## Hardware Integration

### Haptic Gloves Specs
- **Controller**: Adafruit Feather nRF52840 (single controller for both hands)
- **Motors**: 10x haptic feedback motors (5 per hand)
- **Communication**: BLE with custom protocol
- **Power**: 3.7V LiPo battery with power distribution to both hands
- **Wiring**: 7-pin connector per hand (power + 5 motor controls)

### BLE Protocol
Commands sent from Unity to gloves:
- `H<finger><intensity>` - Set haptic feedback (finger: 0-4, intensity: 0-255)
- `C` - Calibrate sensors  
- `B` - Get battery level

Data received as JSON:
```json
{
  "flex": [0, 0, 0, 0, 0],
  "imu": {"accel": {"x": 0, "y": 0, "z": 0}, "gyro": {"x": 0, "y": 0, "z": 0}},
  "battery": 100
}
```

## Key Technical Details

### Hand Tracking Features
- Dual hand tracking with palm distance calculations (0.4-0.8m typical range)
- Finger velocity mapping with OneEuro filtering for smooth data
- Volume control based on velocity thresholds (mute below 0.015 m/s)
- Remote configuration via web server for filter settings, velocity thresholds, and mapping parameters

### AR Integration  
- Vision Pro mesh management with ARMeshManager
- Spatial anchoring and mesh merging for performance
- Camera-relative coordinate transformations

### Performance Considerations
- VFX mesh merging runs at 0.5s intervals to optimize performance
- BLE data sending at configurable intervals (default 0.5s)
- YOLO inference optimized for real-time Vision Pro processing

### Remote Configuration
- Web server integration through `ReadSthFromServer.cs` for runtime parameter adjustments
- Configurable parameters include:
  - Velocity ratio multiplier for speed mapping sensitivity
  - Maximum velocity threshold for upper bound of mapping range (default 0.3 m/s)
  - Volume threshold for muting control (default 0.015 m/s)
  - Filter toggle switches for both velocity and magnitude filters
  - Filter strength parameters for fine-tuning response smoothness
- All configuration fetched at regular intervals (default 5.0s)

## Firmware Development

Located in `Hardware~/firmware/`:
- Arduino IDE project for nRF52840
- Required libraries: Adafruit nRF52 BSP, Bluefruit nRF52, MPU6050
- Pin configuration documented in hardware README files

## Common Development Tasks

- **Hand Tracking Debug**: Use `DebugHandGestures` scene with `MyHand` component logging
- **BLE Testing**: Monitor `BLESendJointV` velocity mapping and connection status
- **VFX Development**: Work in `DebugVFX` scene with `VFXMan` for spatial effects
- **YOLO Testing**: Use dedicated scenes in `YoloScenes/` folder

## Architecture Notes

The project uses a modular architecture with clear separation between:
1. **Input Systems** (hand tracking, camera)
2. **Processing** (YOLO, velocity filtering, VFX)  
3. **Output Systems** (BLE to gloves, visual effects)
4. **Hardware Integration** (native iOS CoreBluetooth bridge)

All major systems communicate through Unity's component system with public interfaces for cross-system data sharing.

## Recent Architecture Changes

The communication pattern between BLESendJointV and ReadSthFromServer has been updated:
- Previous logic: BLESendJointV would read values directly from ReadSthFromServer component
- Current logic: ReadSthFromServer now calls BLESendJointV's public API methods directly when values are received
- This creates a cleaner dependency direction and more maintainable architecture
- Parameters controlled include velocity ratio, filter states, filter strengths, and velocity thresholds