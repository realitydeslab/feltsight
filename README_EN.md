# FeltSight

This is a Unity interaction project for Apple Vision Pro / visionOS. Its core goal is to connect **hand tracking**, **spatial mesh collision classification**, and **BLE haptic gloves**, so that when a user touches real-world surfaces, the system can generate corresponding haptic feedback.

**It converts Vision Pro's real-time perception of finger contact with spatial surfaces into ten-channel BLE haptic control signals.**

The current repository mainly focuses on the Unity-side implementation:

- [`MyHand`](Assets/Scripts/MyHand.cs) is responsible for hand sensing. It provides unified access to both-hand tracking, joint positions, velocities, palm fallback pose, hand distance, and palm opening angle.
- [`HandRaycaster`](Assets/Scripts/HandRaycaster.cs) is responsible for contact sensing. It casts rays from ten fingers, detects whether fingers hit the spatial mesh, and reads visionOS mesh classification.
- [`VFXMan`](Assets/VFX/VFXMan.cs) manages VFX instances for AR Mesh, material parameters, and hit-related visualization effects.
- [`BLESendJointV`](Assets/Scripts/BLESendJointV.cs) is responsible for generating and sending haptic control data. It encodes ten-channel finger states into a 32-byte BLE packet and sends it to an external ESP32 / haptic device.
- [`SuperAdmin`](Assets/Scripts/SuperAdmin.cs) is responsible for global toggles and debug display. It controls debug UI, platform switches, hand features, and BLE feature enable/disable states.

---

## Project Positioning

FeltSight can be understood as a spatial haptics mapping experiment:

- Vision Pro is responsible for sensing the hands and the environment.
- Unity is responsible for converting “which surface a finger touched” and “how fast the collision happened” into transmittable control signals.
- The external BLE device is responsible for turning those signals into haptic feedback.

---

## Overview of scripts in `Assets/Scripts`

### Core scripts

1. [`MyHand`](Assets/Scripts/MyHand.cs)

   - Integrates with `XRHandSubsystem`.
   - Provides interfaces for reading left/right root pose, wrist pose, and palm pose.
   - Maintains joint position history and computes velocity, especially fingertip velocity.
   - Provides higher-level data such as hand distance, palm distance, and palm opening angle.
   - Falls back to the centroid of four proximal joints when the palm joint is unavailable.
   - Can output debug information to `TextMeshProUGUI`.

2. [`HandRaycaster`](Assets/Scripts/HandRaycaster.cs)

   - Performs raycasts for five fingers on each hand, for a total of 10 channels.
   - Computes ray direction from `Distal -> Tip`.
   - Supports One Euro Filter style smoothing to reduce joint jitter.
   - Records `RaycastHit` after a hit and attempts to read visionOS mesh face classification.
   - Synchronizes hit classification to VFX and debug UI.
   - Exposes interfaces such as `TryGetFingerHit()` and `TryGetFingerHitClassification()` for the BLE layer.

3. [`BLESendJointV`](Assets/Scripts/BLESendJointV.cs)

   - Handles BLE initialization, scanning, connection, service/characteristic discovery, data sending, and reconnection.
   - Reads finger velocity from [`MyHand`](Assets/Scripts/MyHand.cs) and hit state from [`HandRaycaster`](Assets/Scripts/HandRaycaster.cs).
   - Computes the following for each of the 10 fingers:
     - whether volume should be triggered
     - current mapped velocity value
     - final byte to send
   - Supports velocity threshold, volume threshold, filtering, scaling, and auto reconnect.
   - Periodically generates a 32-byte packet and writes it to the BLE RX characteristic.

4. [`SuperAdmin`](Assets/Scripts/SuperAdmin.cs)
   - Project-level global controller.
   - Controls whether debug UI is shown.
   - Controls global hand and BLE feature switches.
   - Determines the current runtime environment by platform (`Editor` / `VisionOS`).
   - Maintains UI text for ten-finger hit information.
   - Holds a reference to [`BLESendJointV`](Assets/Scripts/BLESendJointV.cs) and controls whether it is enabled.

### Supporting scripts

5. [`OneDollarFilter`](Assets/Scripts/OneDollarFilter.cs)

   - A lightweight low-pass filter component.
   - Can smooth `Vector3` or `float` data.
   - Currently used mainly by [`BLESendJointV`](Assets/Scripts/BLESendJointV.cs) for velocity and velocity magnitude smoothing.
   - Although named One Dollar, the implementation is closer to exponential smoothing.

6. [`HandVisualizer`](Assets/Scripts/HandVisualizer.cs)

   - Used to visualize hand joints and skeleton lines.
   - Depends on `XR Hands` and visionOS extension macros.
   - Creates visualization objects for left and right hand joints and updates them during tracking.
   - Mainly intended for debugging / demo purposes.

7. [`ChangeMaterialRandomColor`](Assets/Scripts/ChangeMaterialRandomColor.cs)
   - Randomizes object material color on startup.
   - A very lightweight test script.

### Test scripts

8. [`TEST/BLETEST`](Assets/Scripts/TEST/BLETEST.cs)
   - A standalone BLE test script.
   - Periodically sends simulated 32-byte test data after connecting to the target device.
   - Used to verify the BLE path without depending on hand tracking logic.

---

## Core runtime flow

### 1. Hand data acquisition

[`MyHand`](Assets/Scripts/MyHand.cs) locates `XRHandSubsystem` at runtime and continuously updates:

- left/right root pose
- wrist pose
- palm pose / proximal centroid fallback
- hand distance
- palm distance
- palm opening angle
- key joint velocity cache

The most important public interfaces are:

- `TryGetJointPositionAndVelocity(...)`
- `TryGetPalmPose(...)`
- `TryGetHandsDistance(...)`
- `TryGetPalmDistance(...)`
- `TryGetPalmAngle(...)`
- `GetAllFingertipsData(...)`

### 2. Finger raycasts and spatial classification

[`HandRaycaster`](Assets/Scripts/HandRaycaster.cs) performs raycasts for five fingers on each hand every frame:

- Casts rays from fingertip positions.
- If a collider is hit, records hit point, normal, and classification result.
- On visionOS, reads triangle face classification through `XRMeshSubsystem.GetFaceClassifications(...)`.
- Synchronizes classification results to VFX and UI.

The purpose of this step is to determine whether a finger is actually touching a spatial surface, and what type of surface it is.

#### Full mesh classification categories and mapping

Currently, [`HandRaycaster`](Assets/Scripts/HandRaycaster.cs:488) first reads the raw mesh classification on visionOS, then maps it into the internal haptic / VFX type index used by the project.

##### Raw VisionOS classifications

Based on the currently installed VisionOS package [`ARMeshClassification`](Library/PackageCache/com.unity.xr.visionos@c88aa5b2830f/Runtime/ARMeshClassification.cs:9), the full classification list is:

| Raw enum value | VisionOS classification |
| -------------- | ----------------------- |
| `0`            | `None`                  |
| `1`            | `Wall`                  |
| `2`            | `Floor`                 |
| `3`            | `Ceiling`               |
| `4`            | `Table`                 |
| `5`            | `Seat`                  |
| `6`            | `Window`                |
| `7`            | `Door`                  |
| `8`            | `WallDecoration`        |
| `9`            | `Blinds`                |
| `10`           | `Fireplace`             |
| `11`           | `Stairs`                |
| `12`           | `Bed`                   |
| `13`           | `Counter`               |
| `14`           | `Cabinet`               |
| `15`           | `HomeAppliance`         |
| `16`           | `DoorFrame`             |
| `17`           | `TV`                    |
| `18`           | `Whiteboard`            |
| `19`           | `Plant`                 |

##### Internal mapped type index

The project does not directly use the raw enum values above. Instead, it maps them through [`MapVisionOSClassificationToHitTypeIndex()`](Assets/Scripts/HandRaycaster.cs:520) into the internal `typeIndex / HitColorIndex`.

The mapping rules are:

| VisionOS classification | Mapped type int |
| ----------------------- | --------------- |
| `Seat`                  | `3`             |
| `Table`                 | `4`             |
| `Floor`                 | `5`             |
| `Wall`                  | `5`             |
| `Plant`                 | `10`            |
| `TV`                    | `11`            |
| all others              | `0`             |

Notes:

- The integer itself is the mapped type index.
- `0` means `Unknown / undefined type`.
- UI and console logs display the raw recognized classification name.
- `HitColorIndex` in VFX and `typeIndex` in the BLE protocol use the mapped integer value.

That means:

- When the console shows [`[HandRaycastClass]`](Assets/Scripts/HandRaycaster.cs:334), the mapped result is usually `0`.
- When the console shows [`[HandRaycastClassHit]`](Assets/Scripts/HandRaycaster.cs:334), the mapped result is a valid non-zero type.
- `HitColorIndex` in VFX should be configured according to the mapped type index, not the raw VisionOS enum value.

### 3. Haptic parameter generation

[`BLESendJointV`](Assets/Scripts/BLESendJointV.cs) updates the 10 finger channels every frame:

- 5 fingers on the right hand + 5 fingers on the left hand.
- Reads the velocity magnitude of each finger.
- Optionally smooths the data using [`OneDollarFilter`](Assets/Scripts/OneDollarFilter.cs).
- If the finger does not hit an object, or the velocity is below the threshold, volume is set to 0.
- Otherwise, velocity is mapped to `10~40`, corresponding to `1.0x~4.0x`.

### 4. BLE data transmission

[`BLESendJointV`](Assets/Scripts/BLESendJointV.cs) periodically sends a fixed-format 32-byte packet:

- `data[0] = 0xFE` start marker
- 10 groups of channel data in the middle, 3 bytes per group
- `data[31] = 0xFF` end marker

Meaning of the 3 bytes for each channel:

1. file index / material index
2. volume
3. speed

In the current implementation, the file index defaults to `5`, which means the logic for switching different haptic assets by material has not yet been fully connected back into the sending pipeline. The current focus is on per-finger volume + speed control.

---

## BLE communication details

Both [`BLESendJointV`](Assets/Scripts/BLESendJointV.cs) and [`TEST/BLETEST`](Assets/Scripts/TEST/BLETEST.cs) use the same Nordic UART style UUIDs:

- Service UUID: `6E400001-B5A3-F393-E0A9-E50E24DCCA9E`
- RX Characteristic: `6E400002-B5A3-F393-E0A9-E50E24DCCA9E`
- TX Characteristic: `6E400003-B5A3-F393-E0A9-E50E24DCCA9E`

The default target device names come from [`BLESendJointV`](Assets/Scripts/BLESendJointV.cs):

- `ESP32-BLE`
- `FeltSight BLE`

Features include:

- automatic scanning
- automatic connection
- connection state UI updates
- continuous send failure detection
- automatic reconnection after disconnect
- manual reconnect trigger

---

## Key data design

### Ten-finger channel mapping

The 10 channels in [`BLESendJointV`](Assets/Scripts/BLESendJointV.cs) are defined as:

- `0~4`: right thumb to little finger
- `5~9`: left thumb to little finger

This is consistent with the hit query logic in [`HandRaycaster`](Assets/Scripts/HandRaycaster.cs).

### Ten-finger channel protocol definition

To make Unity-side and firmware-side collaboration easier, the current BLE packet protocol is defined as follows.

#### Total packet length

- fixed `32` bytes
- start byte: `0xFE`
- end byte: `0xFF`

See [`GenerateDataPacket()`](Assets/Scripts/BLESendJointV.cs:710).

#### Packet structure

| Byte range            | Meaning                                      |
| --------------------- | -------------------------------------------- |
| `data[0]`             | packet header, fixed `0xFE`                  |
| `data[1] ~ data[30]`  | 10 finger channels, 3 bytes per channel      |
| `data[31]`            | packet tail, fixed `0xFF`                    |

#### Single-channel structure

Each finger channel occupies 3 bytes in the following format:

| Offset       | Field       | Description                                  |
| ------------ | ----------- | -------------------------------------------- |
| `offset + 0` | `typeIndex` | haptic type index / file index               |
| `offset + 1` | `volume`    | volume, range `0~100`                        |
| `offset + 2` | `speed`     | speed byte, range `10~40`, means `1.0x~4.0x` |

Where:

- `offset = 1 + channel * 3`
- `channel` ranges from `0~9`

#### Ten-finger channel numbering

| Channel | Hand    | Finger |
| ------- | ------- | ------ |
| `0`     | Right   | Thumb  |
| `1`     | Right   | Index  |
| `2`     | Right   | Middle |
| `3`     | Right   | Ring   |
| `4`     | Right   | Little |
| `5`     | Left    | Thumb  |
| `6`     | Left    | Index  |
| `7`     | Left    | Middle |
| `8`     | Left    | Ring   |
| `9`     | Left    | Little |

#### Current meaning of `typeIndex`

The current Unity-side convention is that [`HandRaycaster`](Assets/Scripts/HandRaycaster.cs) first recognizes the raw VisionOS mesh classification and maps it into the project's internal haptic type index. Then [`BLESendJointV`](Assets/Scripts/BLESendJointV.cs) applies an additional BLE-side remapping before sending.

##### Internal mapping (`HandRaycaster` / VFX / UI)

| VisionOS classification | Internal `typeIndex` / `HitColorIndex` |
| ----------------------- | -------------------------------------- |
| `Seat`                  | `3`                                    |
| `Table`                 | `4`                                    |
| `Floor`                 | `5`                                    |
| `Wall`                  | `5`                                    |
| `Plant`                 | `10`                                   |
| `TV`                    | `11`                                   |
| all others              | `0`                                    |

##### Actual BLE send mapping (`BLESendJointV`)

[`BLESendJointV`](Assets/Scripts/BLESendJointV.cs) reads the result of [`HandRaycaster.TryGetFingerHitClassification()`](Assets/Scripts/HandRaycaster.cs:475) in [`GetFingerTypeIndex()`](Assets/Scripts/BLESendJointV.cs:1128), then applies send-side remapping through [`RemapBleTypeIndex()`](Assets/Scripts/BLESendJointV.cs:1141).

The final `typeIndex` sent in the BLE packet is:

| VisionOS classification | BLE sent `typeIndex` |
| ----------------------- | -------------------- |
| `Seat`                  | `3`                  |
| `Table`                 | `4`                  |
| `Floor`                 | `5`                  |
| `Wall`                  | `6`                  |
| `Plant`                 | `10`                 |
| `TV`                    | `11`                 |
| all others              | `0`                  |

Notes:

- The integer itself is the `typeIndex`.
- `0` means Unknown / undefined type.
- VFX / UI use the internal mapped value.
- BLE transmission uses the send-side remapped value.
- Firmware should select the corresponding haptic asset according to the actual BLE `typeIndex`.

#### Current meaning of `volume`

- range `0~100`
- volume is set to `0` when the finger does not hit an object, or when velocity is below the threshold
- otherwise the configured normal volume value is used

#### Current meaning of `speed`

- range `10~40`
- corresponds to playback rate `1.0x~4.0x`
- linearly mapped from finger velocity magnitude

#### Protocol example

Assume:

- the right index finger (channel `1`) hits `Wall`
- current volume is `75`
- current speed byte is `24`

Then the 3 bytes for that channel are:

- `typeIndex = 5`
- `volume = 75`
- `speed = 24`

That is:

- `data[4] = 5`
- `data[5] = 75`
- `data[6] = 24`

If the left middle finger (channel `7`) does not hit a valid classification, then:

- `typeIndex = 0`
- `volume` may be `0`
- `speed` may still be the current mapped speed value or a default value

#### Firmware-side recommendation

When parsing on the firmware side, it is recommended to:

- first validate whether [`data[0]`](Assets/Scripts/BLESendJointV.cs:711) is `0xFE`
- then validate whether [`data[31]`](Assets/Scripts/BLESendJointV.cs:734) is `0xFF`
- read 3 bytes per channel for `channel = 0~9`
- use `typeIndex` to select the asset, `volume` to control intensity, and `speed` to control playback rate

### Velocity to playback-rate mapping

In [`BLESendJointV`](Assets/Scripts/BLESendJointV.cs):

- default velocity range is `0.0 ~ 0.3 m/s`
- mapped to byte range `10 ~ 40`
- corresponding playback rate is `1.0x ~ 4.0x`

### Mute conditions

A single finger channel will be muted when:

- it does not hit any object
- or its velocity is below `m_VolumeThreshold`

This means the current haptic feedback behavior is not “vibrate whenever the hand moves”, but rather “trigger only when the finger touches something and moves fast enough”.

---

## Dependencies and platform notes

From the scripts, the main project dependencies include:

- Unity XR Hands
- visionOS / Vision Pro related extensions
- AR Foundation / XR Mesh classification capability
- `UnityCoreBluetooth` plugin
- TextMeshPro

Among them:

- [`HandVisualizer`](Assets/Scripts/HandVisualizer.cs) is controlled by compile-time macros and is only compiled when XR Hands and visionOS / Editor conditions are met.
- mesh classification reading in [`HandRaycaster`](Assets/Scripts/HandRaycaster.cs) only actually works under `UNITY_VISIONOS`.
- [`SuperAdmin`](Assets/Scripts/SuperAdmin.cs) distinguishes between Editor and VisionOS at runtime.

### Xcode / Info.plist Bluetooth permission notes

On visionOS / iOS devices, if the app launches normally but BLE logic does not run, does not report errors, or barely produces any related logs, the first thing to check is whether the Xcode project's `Info` / `Info.plist` includes the required Bluetooth privacy descriptions.

You must add the following two Bluetooth Description entries:

- `Privacy - Bluetooth Always Usage Description`
- `Privacy - Bluetooth Peripheral Usage Description`

Suggested text examples:

- `This app uses Bluetooth to connect to external haptic devices.`
- `This app uses Bluetooth to communicate with external haptic peripherals.`

If these entries are missing, common symptoms include:

- the app itself launches normally
- BLE scanning / connection logic does not actually execute
- there is no obvious error in the console
- it looks like the Bluetooth code never runs, or runs without any visible feedback

Therefore, these two Bluetooth privacy description entries should be treated as mandatory BLE checks in the exported Xcode project.

### Xcode build error: `CoreBluetooth.framework did not contain an Info.plist`

If you see an error like the following when compiling the visionOS project in Xcode:

```text
Framework .../MeshClassification.app/Frameworks/CoreBluetooth.framework did not contain an Info.plist
```

this is usually not caused by [`mcUnityCoreBluetooth.bundle`](Assets/Plugins/UnityCoreBluetooth/Plugins/macOS/mcUnityCoreBluetooth.bundle) failing to be excluded. The more likely cause is that `CoreBluetooth.framework` was incorrectly copied into the app bundle as an **Embedded Framework**.

For visionOS, `CoreBluetooth.framework` is a system framework. It should be **linked**, but it should **not** be **embedded**.

#### Manual fix steps in Xcode

1. Open the Xcode project exported by Unity.
2. Select the main target.
3. Open the `General` tab.
4. Find `Frameworks, Libraries, and Embedded Content`.
5. Locate `CoreBluetooth.framework`.
6. Change its `Embed` setting to `Do Not Embed`.

#### Notes

- Keeping [`Assets/Plugins/UnityCoreBluetooth/Plugins/macOS/mcUnityCoreBluetooth.bundle`](Assets/Plugins/UnityCoreBluetooth/Plugins/macOS/mcUnityCoreBluetooth.bundle) excluded from visionOS is correct.
- What must be avoided is copying the system framework `CoreBluetooth.framework` into the `.app/Frameworks/` directory.
- If the project contains a custom Xcode post-process script such as [`VisionOSBuildPostProcessor`](Assets/Editor/VisionOSBuildPostProcessor.cs), make sure its handling of `CoreBluetooth.framework` is **Link Only / Do Not Embed**.

---

## Dependency relationships between scripts

The current script relationships can be summarized as:

```text
XRHandSubsystem
   ↓
MyHand
   ├─ provides joint position / velocity / palm / distance
   ↓
HandRaycaster
   ├─ determines whether ten fingers hit the spatial mesh
   ├─ reads mesh classification
   ↓
BLESendJointV
   ├─ combines “hit state + velocity magnitude” to generate ten-channel haptic parameters
   └─ sends them to ESP32 / haptic device through BLE

SuperAdmin
   ├─ controls UI
   ├─ controls feature switches
   └─ displays hit information for each finger
```

---

## Runtime and debugging suggestions

### Unity side

- Open the project with a Unity version that supports visionOS / XR Hands.
- Check whether references to [`MyHand`](Assets/Scripts/MyHand.cs), [`HandRaycaster`](Assets/Scripts/HandRaycaster.cs), [`BLESendJointV`](Assets/Scripts/BLESendJointV.cs), and [`SuperAdmin`](Assets/Scripts/SuperAdmin.cs) are complete in the scene.
- If you only want to verify hand tracking and UI first, you can disable the BLE switch in [`SuperAdmin`](Assets/Scripts/SuperAdmin.cs).
- If you only want to verify BLE, you can directly use [`TEST/BLETEST`](Assets/Scripts/TEST/BLETEST.cs).

### Device side

- Make sure the peripheral advertising name matches the configuration in [`BLESendJointV`](Assets/Scripts/BLESendJointV.cs).
- Make sure the service UUID / characteristic UUID match the Unity side.
- If disconnections happen frequently, first check power supply, advertising stability, and write characteristic permissions.

### Reference projects and sources

The AR Mesh classification integration approach in this project references the Unity official AR Foundation sample repository [`Unity-Technologies/arfoundation-samples`](https://github.com/Unity-Technologies/arfoundation-samples).

It also references the Unity official documentation for [`AR Foundation 6.4`](https://docs.unity3d.com/Packages/com.unity.xr.arfoundation@6.4/manual/index.html) and [`Apple ARKit XR Plug-in 6.4`](https://docs.unity3d.com/Packages/com.unity.xr.arkit@6.4/manual/index.html).

Reference notes:

- [`Unity-Technologies/arfoundation-samples`](https://github.com/Unity-Technologies/arfoundation-samples) is the official AR Foundation sample project provided by Unity.
