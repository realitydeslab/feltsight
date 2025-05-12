# Haptic Gloves Design

This document outlines the physical design and construction of the haptic gloves.

## Components List

### Electronics
- Seeed Studio XIAO nRF52840 (1x)
- Haptic feedback motors (10x)
- Flex sensors (10x)
- 3.7V LiPo battery (1x)
- Battery charger (1x)
- Wires and connectors

### Materials
- Stretchable fabric (gloves)
- Conductive thread
- Velcro straps
- Foam padding
- Heat shrink tubing
- Adhesive

## Design Specifications

### Glove Structure
1. Base glove made of stretchable fabric
2. Electronics pockets for each component
3. Wire routing channels
4. Battery compartment on wrist
5. Adjustable straps for fit

### Component Placement
- Microcontroller: Back of hand
- Haptic motors: Finger tips
- Flex sensors: Finger joints
- IMU: Back of hand
- Battery: Wrist
- Charging port: Wrist

## Assembly Instructions

1. Prepare the base glove
   - Cut and sew the stretchable fabric
   - Create pockets for components
   - Add wire routing channels

2. Install electronics
   - Solder components to microcontroller
   - Route wires through channels
   - Secure components in pockets
   - Connect battery and charging circuit

3. Final assembly
   - Test all connections
   - Secure components with adhesive
   - Add padding for comfort
   - Install adjustable straps

## Wiring Diagram

```
[Adafruit Feather nRF52840]
        |
        ├── Haptic Motors
        │   ├── Thumb (D2)
        │   ├── Index (D3)
        │   ├── Middle (D4)
        │   ├── Ring (D5)
        │   └── Pinky (D6)
        │
        ├── Flex Sensors
        │   ├── Thumb (A0)
        │   ├── Index (A1)
        │   ├── Middle (A2)
        │   ├── Ring (A3)
        │   └── Pinky (A4)
        │
        ├── IMU (MPU6050)
        │   ├── SCL
        │   └── SDA
        │
        └── Battery
            ├── Positive
            └── Negative
```

## Safety Considerations

1. Electrical Safety
   - Insulate all connections
   - Use heat shrink tubing
   - Secure battery properly
   - Add fuse protection

2. Comfort and Fit
   - Ensure proper ventilation
   - Add padding at pressure points
   - Allow for finger movement
   - Use adjustable straps

3. Durability
   - Reinforce stress points
   - Protect sensitive components
   - Use flexible wiring
   - Add strain relief

## Maintenance

1. Regular Checks
   - Inspect wiring
   - Check battery health
   - Test haptic feedback
   - Verify sensor readings

2. Cleaning
   - Remove battery before cleaning
   - Use mild detergent
   - Air dry only
   - Avoid water on electronics

3. Storage
   - Remove battery
   - Store in dry place
   - Protect from dust
   - Avoid extreme temperatures 