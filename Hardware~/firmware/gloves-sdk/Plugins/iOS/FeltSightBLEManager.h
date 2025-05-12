#import <Foundation/Foundation.h>
#import <CoreBluetooth/CoreBluetooth.h>

@interface FeltSightBLEManager : NSObject <CBCentralManagerDelegate, CBPeripheralDelegate>

+ (instancetype)sharedInstance;

- (void)startScanning;
- (void)stopScanning;
- (void)connectToPeripheral:(NSString *)peripheralId;
- (void)disconnect;
- (void)sendHapticData:(NSData *)data;

@property (nonatomic, strong) CBCentralManager *centralManager;
@property (nonatomic, strong) CBPeripheral *connectedPeripheral;
@property (nonatomic, strong) CBCharacteristic *commandCharacteristic;
@property (nonatomic, strong) CBCharacteristic *sensorCharacteristic;

@end 