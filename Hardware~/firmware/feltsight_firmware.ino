#include <bluefruit.h>
#include <Adafruit_DRV2605.h>
#include <SPI.h>
#include <Adafruit_FlashTransport.h>
#include <Adafruit_SPIFlash.h>

// BLE Service and Characteristic UUIDs
#define SERVICE_UUID "6E400001-B5A3-F393-E0A9-E50E24DCCA9E"
#define COMMAND_CHAR_UUID "6E400002-B5A3-F393-E0A9-E50E24DCCA9E"

// Pin Definitions
// Left Hand
#define LEFT_THUMB_MOTOR 2
#define LEFT_INDEX_MOTOR 3
#define LEFT_MIDDLE_MOTOR 4
#define LEFT_RING_MOTOR 5
#define LEFT_PINKY_MOTOR 6

// Right Hand
#define RIGHT_THUMB_MOTOR 7
#define RIGHT_INDEX_MOTOR 8
#define RIGHT_MIDDLE_MOTOR 9
#define RIGHT_RING_MOTOR 10
#define RIGHT_PINKY_MOTOR 11

// Data structures
struct HapticData {
    float leftFingerIntensities[5];
    float rightFingerIntensities[5];
    float patternIntensity;
};

// Global variables
Adafruit_DRV2605 drvLeft;
Adafruit_DRV2605 drvRight;
BLEService feltSightService;
BLECharacteristic commandChar;
HapticData currentHapticData;
bool isConnected = false;

void setup() {
    Serial.begin(115200);
    while (!Serial) delay(10);

    // Initialize haptic drivers
    if (!drvLeft.begin()) {
        Serial.println("Could not find DRV2605 for left hand");
    }
    if (!drvRight.begin()) {
        Serial.println("Could not find DRV2605 for right hand");
    }

    // Initialize BLE
    Bluefruit.begin();
    Bluefruit.setName("FeltSight");
    Bluefruit.setTxPower(4);

    // Setup BLE service and characteristics
    feltSightService = BLEService(SERVICE_UUID);
    commandChar = BLECharacteristic(COMMAND_CHAR_UUID);

    feltSightService.begin();
    commandChar.setProperties(CHR_PROPS_WRITE);
    commandChar.setPermission(SECMODE_OPEN, SECMODE_OPEN);
    commandChar.setWriteCallback(commandCallback);
    commandChar.begin();

    // Start advertising
    Bluefruit.Advertising.addFlags(BLE_GAP_ADV_FLAGS_LE_ONLY_GENERAL_DISC_MODE);
    Bluefruit.Advertising.addTxPower();
    Bluefruit.Advertising.addService(feltSightService);
    Bluefruit.Advertising.addName();
    Bluefruit.Advertising.restartOnDisconnect(true);
    Bluefruit.Advertising.setInterval(32, 244);
    Bluefruit.Advertising.setFastTimeout(30);
    Bluefruit.Advertising.start(0);

    // Initialize haptic data
    memset(&currentHapticData, 0, sizeof(HapticData));
}

void loop() {
    if (isConnected) {
        // Update haptic feedback
        updateHapticFeedback();
    }

    delay(16); // ~60Hz update rate
}

void commandCallback(uint16_t conn_hdl, BLECharacteristic* chr, uint8_t* data, uint16_t len) {
    if (len == sizeof(HapticData)) {
        memcpy(&currentHapticData, data, sizeof(HapticData));
    }
}

void updateHapticFeedback() {
    // Update left hand haptics
    for (int i = 0; i < 5; i++) {
        drvLeft.setWaveform(i, currentHapticData.leftFingerIntensities[i] * 255);
    }
    drvLeft.go();

    // Update right hand haptics
    for (int i = 0; i < 5; i++) {
        drvRight.setWaveform(i, currentHapticData.rightFingerIntensities[i] * 255);
    }
    drvRight.go();
} 