# FeltSight Hardware Documentation

This folder contains the hardware specifications and circuit diagrams for the FeltSight haptic gloves.

## Components

### Main Components
1. Adafruit nRF52840 Feather (Single Controller)
2. Haptic Feedback Motors (10x - 5 per hand)
3. Battery Management System
4. Haptic Driver Circuit (2x - 1 per hand)
5. Flash Memory for Texture Storage
6. Wired Connection System

## Circuit Diagrams

### Power Management
- Battery: 3.7V LiPo (2000mAh)
- Charging Circuit: MCP73831
- Voltage Regulation: 3.3V LDO
- Power Path Management
- Power Distribution to Both Hands

### Haptic Driver
- DRV2605L Haptic Driver IC (2x)
- PWM Control for each motor (10 channels)
- Current Limiting
- Overvoltage Protection
- Hand-Specific Control

### Communication
- BLE Antenna Design
- RF Matching Network
- ESD Protection
- Wired Hand Connection System

## Pin Configuration

### Adafruit nRF52840 (Main Controller)
```
Power:
- VBAT: Battery Input
- VUSB: USB Power
- 3V3: Regulated 3.3V Output

Haptic Control (Left Hand):
- D2: Thumb Motor
- D3: Index Motor
- D4: Middle Motor
- D5: Ring Motor
- D6: Pinky Motor

Haptic Control (Right Hand):
- D7: Thumb Motor
- D8: Index Motor
- D9: Middle Motor
- D10: Ring Motor
- D11: Pinky Motor

Flash Memory:
- SCK: Flash Clock
- MISO: Flash Data Out
- MOSI: Flash Data In
- CS: Flash Chip Select
```

## Power Requirements
- Operating Voltage: 3.3V
- Peak Current: 1000mA (both hands)
- Standby Current: < 1mA
- Battery Life: ~4 hours continuous use

## Safety Features
- Overcurrent Protection
- Overvoltage Protection
- Thermal Protection
- ESD Protection
- Reverse Polarity Protection
- Short Circuit Protection

## Wired Connection System
- 7-pin connector per hand
- Pinout:
  1. +3.3V Power
  2. Ground
  3. Thumb Motor
  4. Index Motor
  5. Middle Motor
  6. Ring Motor
  7. Pinky Motor

## Hand Synchronization
- Independent hand control
- Shared haptic pattern storage
- Coordinated haptic feedback 