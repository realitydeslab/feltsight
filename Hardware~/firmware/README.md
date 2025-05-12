# Haptic Gloves Firmware

This firmware runs on the Adafruit Feather nRF52840 microcontroller and controls the haptic feedback and sensor data collection.

## Hardware Requirements
- Adafruit Feather nRF52840
- Haptic feedback motors (5x)
- Flex sensors (5x)
- IMU sensor (MPU6050)
- Power supply (3.7V LiPo battery)

## Pin Configuration

### Adafruit Feather nRF52840
```
Motor Control:
- Thumb: D2
- Index: D3
- Middle: D4
- Ring: D5
- Pinky: D6

Flex Sensors:
- Thumb: A0
- Index: A1
- Middle: A2
- Ring: A3
- Pinky: A4

IMU:
- SCL: SCL
- SDA: SDA
```

## Features
- BLE communication
- Haptic feedback control
- Flex sensor reading
- IMU data collection
- Battery monitoring

## Setup

1. Install Arduino IDE
2. Install required libraries:
   - Adafruit nRF52 BSP
   - Adafruit Bluefruit nRF52
   - Adafruit MPU6050
   - Adafruit PWM Servo Driver

3. Configure the firmware:
   - Set BLE device name
   - Configure motor pins
   - Calibrate sensors

## Building and Flashing

1. Connect the Adafruit Feather to your computer
2. Select the correct board in Arduino IDE
3. Compile and upload the firmware

## Communication Protocol

The firmware implements a custom BLE protocol:

### Commands
- `H<finger><intensity>`: Set haptic feedback
  - finger: 0-4 (thumb to pinky)
  - intensity: 0-255
- `C`: Calibrate sensors
- `B`: Get battery level

### Data Format
- Sensor data is sent as JSON:
```json
{
  "flex": [0, 0, 0, 0, 0],
  "imu": {
    "accel": {"x": 0, "y": 0, "z": 0},
    "gyro": {"x": 0, "y": 0, "z": 0}
  },
  "battery": 100
}
```

## Dependencies
- Arduino IDE
- Adafruit nRF52 BSP
- Adafruit Bluefruit nRF52
- Adafruit MPU6050
- Adafruit PWM Servo Driver 