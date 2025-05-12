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

## Project Structure
```
feltsight/
├── unity/                  # Unity Vision Pro project
├── gloves-sdk/            # Gloves Unity SDK
├── firmware/              # Gloves firmware
├── design/                # Gloves design files
└── docs/                  # Documentation
```

## Getting Started

1. Clone this repository
2. Follow the setup instructions in each component's README
3. Build and deploy the Unity application to Vision Pro
4. Flash the firmware to the gloves
5. Connect and test the system

## License
MIT License 


# FeltSight - Vision Pro Unity Application

This is the main Unity application that runs on Apple Vision Pro and handles the interaction with haptic gloves.

## Features
- Vision Pro hand tracking integration
- Haptic glove communication
- Art interaction system
- Real-time feedback

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
├── Assets/
│   ├── Scripts/           # C# scripts
│   ├── Prefabs/          # Unity prefabs
│   ├── Materials/        # Materials and shaders
│   ├── Scenes/           # Unity scenes
│   └── Plugins/          # Third-party plugins
├── Packages/             # Package dependencies
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