#import "FeltSightBLEManager.h"

// Service and Characteristic UUIDs
static NSString *const kFeltSightServiceUUID = @"6E400001-B5A3-F393-E0A9-E50E24DCCA9E";
static NSString *const kCommandCharUUID = @"6E400002-B5A3-F393-E0A9-E50E24DCCA9E";
static NSString *const kSensorCharUUID = @"6E400004-B5A3-F393-E0A9-E50E24DCCA9E";

@implementation FeltSightBLEManager

+ (instancetype)sharedInstance {
    static FeltSightBLEManager *sharedInstance = nil;
    static dispatch_once_t onceToken;
    dispatch_once(&onceToken, ^{
        sharedInstance = [[self alloc] init];
    });
    return sharedInstance;
}

- (instancetype)init {
    self = [super init];
    if (self) {
        _centralManager = [[CBCentralManager alloc] initWithDelegate:self queue:nil];
    }
    return self;
}

- (void)startScanning {
    if (self.centralManager.state == CBManagerStatePoweredOn) {
        NSArray *services = @[[CBUUID UUIDWithString:kFeltSightServiceUUID]];
        [self.centralManager scanForPeripheralsWithServices:services options:nil];
        NSLog(@"Started scanning for FeltSight devices");
    }
}

- (void)stopScanning {
    [self.centralManager stopScan];
    NSLog(@"Stopped scanning");
}

- (void)connectToPeripheral:(NSString *)peripheralId {
    NSUUID *uuid = [[NSUUID alloc] initWithUUIDString:peripheralId];
    NSArray *peripherals = [self.centralManager retrievePeripheralsWithIdentifiers:@[uuid]];
    
    if (peripherals.count > 0) {
        self.connectedPeripheral = peripherals[0];
        self.connectedPeripheral.delegate = self;
        [self.centralManager connectPeripheral:self.connectedPeripheral options:nil];
    }
}

- (void)disconnect {
    if (self.connectedPeripheral) {
        [self.centralManager cancelPeripheralConnection:self.connectedPeripheral];
    }
}

- (void)sendHapticData:(NSData *)data {
    if (self.connectedPeripheral && self.commandCharacteristic) {
        [self.connectedPeripheral writeValue:data forCharacteristic:self.commandCharacteristic type:CBCharacteristicWriteWithoutResponse];
    }
}

#pragma mark - CBCentralManagerDelegate

- (void)centralManagerDidUpdateState:(CBCentralManager *)central {
    switch (central.state) {
        case CBManagerStatePoweredOn:
            NSLog(@"Bluetooth is powered on");
            break;
        case CBManagerStatePoweredOff:
            NSLog(@"Bluetooth is powered off");
            break;
        case CBManagerStateUnauthorized:
            NSLog(@"Bluetooth is unauthorized");
            break;
        default:
            NSLog(@"Bluetooth state changed: %ld", (long)central.state);
            break;
    }
}

- (void)centralManager:(CBCentralManager *)central didDiscoverPeripheral:(CBPeripheral *)peripheral advertisementData:(NSDictionary<NSString *,id> *)advertisementData RSSI:(NSNumber *)RSSI {
    NSLog(@"Discovered peripheral: %@", peripheral.name);
    // Notify Unity about discovered peripheral
    UnitySendMessage("FeltSightBLEBridge", "OnPeripheralDiscovered", [peripheral.identifier.UUIDString UTF8String]);
}

- (void)centralManager:(CBCentralManager *)central didConnectPeripheral:(CBPeripheral *)peripheral {
    NSLog(@"Connected to peripheral: %@", peripheral.name);
    [peripheral discoverServices:@[[CBUUID UUIDWithString:kFeltSightServiceUUID]]];
    UnitySendMessage("FeltSightBLEBridge", "OnConnected", "");
}

- (void)centralManager:(CBCentralManager *)central didDisconnectPeripheral:(CBPeripheral *)peripheral error:(NSError *)error {
    NSLog(@"Disconnected from peripheral: %@", peripheral.name);
    self.connectedPeripheral = nil;
    self.commandCharacteristic = nil;
    self.sensorCharacteristic = nil;
    UnitySendMessage("FeltSightBLEBridge", "OnDisconnected", "");
}

#pragma mark - CBPeripheralDelegate

- (void)peripheral:(CBPeripheral *)peripheral didDiscoverServices:(NSError *)error {
    if (error) {
        NSLog(@"Error discovering services: %@", error);
        return;
    }
    
    for (CBService *service in peripheral.services) {
        if ([service.UUID.UUIDString isEqualToString:kFeltSightServiceUUID]) {
            [peripheral discoverCharacteristics:@[[CBUUID UUIDWithString:kCommandCharUUID],
                                               [CBUUID UUIDWithString:kSensorCharUUID]] forService:service];
        }
    }
}

- (void)peripheral:(CBPeripheral *)peripheral didDiscoverCharacteristicsForService:(CBService *)service error:(NSError *)error {
    if (error) {
        NSLog(@"Error discovering characteristics: %@", error);
        return;
    }
    
    for (CBCharacteristic *characteristic in service.characteristics) {
        if ([characteristic.UUID.UUIDString isEqualToString:kCommandCharUUID]) {
            self.commandCharacteristic = characteristic;
        } else if ([characteristic.UUID.UUIDString isEqualToString:kSensorCharUUID]) {
            self.sensorCharacteristic = characteristic;
            [peripheral setNotifyValue:YES forCharacteristic:characteristic];
        }
    }
}

- (void)peripheral:(CBPeripheral *)peripheral didUpdateValueForCharacteristic:(CBCharacteristic *)characteristic error:(NSError *)error {
    if (error) {
        NSLog(@"Error receiving notification: %@", error);
        return;
    }
    
    if ([characteristic.UUID.UUIDString isEqualToString:kSensorCharUUID]) {
        NSData *sensorData = characteristic.value;
        // Convert NSData to base64 string for Unity
        NSString *base64String = [sensorData base64EncodedStringWithOptions:0];
        UnitySendMessage("FeltSightBLEBridge", "OnSensorDataReceived", [base64String UTF8String]);
    }
}

@end 