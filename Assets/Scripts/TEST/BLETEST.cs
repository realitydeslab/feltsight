using UnityEngine;
using UnityCoreBluetooth;
using System.Collections;

public class BLETEST : MonoBehaviour
{
    private CoreBluetoothManager manager;
    private CoreBluetoothCharacteristic characteristic;

    public GameObject cube;
    
    // 测试数据相关
    private bool isConnectedAndReady = false;
    private Coroutine testDataCoroutine;
    
    

    void Start()
    {
        Debug.Log("BLETEST script has started.");
        Debug.Log($"Current platform: {Application.platform}");
        Debug.Log($"Unity version: {Application.unityVersion}");
        manager = CoreBluetoothManager.Shared;

        manager.OnUpdateState((string state) =>
        {
            Debug.Log("state: " + state);
            if (state != "poweredOn") return;
            manager.StartScan();
        });

        manager.OnDiscoverPeripheral((CoreBluetoothPeripheral peripheral) =>
        {
            if (peripheral.name != "" && peripheral.name != null && peripheral.name != "(null-name)" && peripheral.name != "null-name")
            {
                Debug.Log("discover peripheral name: " + peripheral.name);
            }

            if (peripheral.name != "ESP32-BLE" && peripheral.name != "FeltSigh BLE") return;

            manager.StopScan();
            manager.ConnectToPeripheral(peripheral);
        });

        manager.OnConnectPeripheral((CoreBluetoothPeripheral peripheral) =>
        {
            Debug.Log("connected peripheral name: " + peripheral.name);
            peripheral.discoverServices();
            // cube.SetActive(false);
        });

        manager.OnDiscoverService((CoreBluetoothService service) =>
        {
            Debug.Log("discover service uuid: " + service.uuid);
            // ESP32服务UUID
            if (service.uuid.ToUpper() != "6E400001-B5A3-F393-E0A9-E50E24DCCA9E") return;
            service.discoverCharacteristics();
        });

        manager.OnDiscoverCharacteristic((CoreBluetoothCharacteristic characteristic) =>
        {
            string uuid = characteristic.Uuid.ToUpper();
            string[] usage = characteristic.Propertis;
            Debug.Log("discover characteristic uuid: " + uuid + ", usage: " + string.Join(",", usage));
            
            // 查找RX特征（用于写入数据到ESP32）
            if (uuid == "6E400002-B5A3-F393-E0A9-E50E24DCCA9E")
            {
                this.characteristic = characteristic;
                Debug.Log("找到RX特征，可以发送数据");
                
                // 连接完成，开始发送测试数据
                isConnectedAndReady = true;
                StartTestDataTransmission();

            }
            
            
            // 处理TX特征（用于接收ESP32的数据）
            if (uuid == "6E400003-B5A3-F393-E0A9-E50E24DCCA9E")
            {

                for (int i = 0; i < usage.Length; i++)
                {
                    if (usage[i] == "notify")
                        characteristic.SetNotifyValue(true);
                }
            }
        });

        manager.Start();
    }

    void Update()
    {
        // 可以在这里添加其他逻辑
    }

    void OnDestroy()
    {
        StopTestDataTransmission();
        if (manager != null)
        {
            manager.Stop();
        }
    }

    // 开始发送测试数据
    private void StartTestDataTransmission()
    {
        if (testDataCoroutine != null)
        {
            StopCoroutine(testDataCoroutine);
        }
        testDataCoroutine = StartCoroutine(SendTestDataPeriodically());
    }

    // 停止发送测试数据
    private void StopTestDataTransmission()
    {
        if (testDataCoroutine != null)
        {
            StopCoroutine(testDataCoroutine);
            testDataCoroutine = null;
        }
    }

    // 每隔1秒发送测试数据的协程
    private IEnumerator SendTestDataPeriodically()
    {
        int testCounter = 0;
        
        while (isConnectedAndReady && characteristic != null)
        {
            byte[] testData = GenerateTestData(testCounter);
            SendDataToESP32(testData);
            
            testCounter++;
            yield return new WaitForSeconds(8.0f); // 等待1秒
        }
    }

    // 生成测试数据
    private byte[] GenerateTestData(int counter)
    {
        byte[] data = new byte[32];
        
        // 起始标记
        data[0] = 0xFE;
        
        // 生成10个通道的测试数据
        for (int channel = 0; channel < 10; channel++)
        {
            int offset = 1 + channel * 3;
            
            // 文件索引：循环使用0x01-0x0A
            data[offset] = (byte)((counter + channel) % 10 + 1);
            
            // 音量：循环使用50-100
            data[offset + 1] = (byte)(50 + (counter + channel) % 51);
            
            // 速度：循环使用0x0A-0x28 (1.0x-4.0x)
            data[offset + 2] = (byte)(0x0A + (counter + channel) % 31);
        }
        
        // 结束标记
        data[31] = 0xFF;
        
        return data;
    }

    // 发送数据到ESP32
    private void SendDataToESP32(byte[] data)
    {
        if (characteristic == null || !isConnectedAndReady)
        {
            Debug.LogWarning("特征未准备好或连接已断开");
            return;
        }

        try
        {
            characteristic.Write(data);
            
            // 打印发送的数据用于调试
            string hexString = System.BitConverter.ToString(data).Replace("-", " ");
            Debug.Log($"发送测试数据: {hexString}");
            
            // 打印解析后的数据
            LogTestDataContent(data);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"发送数据失败: {e.Message}");
        }
    }

    // 打印测试数据内容（用于调试）
    private void LogTestDataContent(byte[] data)
    {
        if (data.Length != 32 || data[0] != 0xFE || data[31] != 0xFF)
        {
            Debug.LogError("测试数据格式错误");
            return;
        }

        Debug.Log("=== 测试数据内容 ===");
        for (int i = 0; i < 10; i++)
        {
            int offset = 1 + i * 3;
            byte fileIndex = data[offset];
            byte volume = data[offset + 1];
            byte speed = data[offset + 2];
            
            Debug.Log($"通道 {i + 1}: 文件={fileIndex:X2}, 音量={volume}%, 速度={speed * 0.1f:F1}x");
        }
        Debug.Log("==================");
    }

    // 手动发送单次测试数据（可以通过UI按钮调用）
    public void SendSingleTestData()
    {
        if (isConnectedAndReady && characteristic != null)
        {
            byte[] testData = GenerateTestData(0);
            SendDataToESP32(testData);
        }
        else
        {
            Debug.LogWarning("BLE未连接或特征未准备好");
        }
    }

    // 切换测试数据发送状态
    public void ToggleTestDataTransmission()
    {
        if (testDataCoroutine != null)
        {
            StopTestDataTransmission();
            Debug.Log("停止发送测试数据");
        }
        else if (isConnectedAndReady)
        {
            StartTestDataTransmission();
            Debug.Log("开始发送测试数据");
        }
    }
}
