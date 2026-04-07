using UnityEngine;
using UnityCoreBluetooth;
using System.Collections;
using TMPro;
using UnityEngine.UI;
using UnityEngine.XR.Hands;
using System;

/// <summary>
/// 使用食指尖速度来控制BLE发送的速度参数
/// 速度0~0.3 m/s线性映射到1.0x~4.0x速度(10-40)
/// 添加了OneDollar滤波器来平滑速度数据
/// 当原始速度小于0.015时，音量设置为0
/// 添加了自动重连功能，当蓝牙连接中断时自动尝试重新连接
/// </summary>
public class BLESendJointV : MonoBehaviour
{
    [SerializeField] [Tooltip("要使用的手部追踪组件")]
    private MyHand m_HandTracker;

    [SerializeField] [Tooltip("HandRaycaster组件引用")]
    private HandRaycaster m_HandRaycaster;

    [SerializeField] [Tooltip("用于显示手指速度的文本组件")]
    private Text m_VelocityText;
    

    [SerializeField] [Tooltip("发送数据的间隔时间（秒）")]
    private float m_SendInterval = 0.5f;

    [SerializeField] [Tooltip("速度映射的最小阈值（米/秒）")]
    private float m_MinVelocityThreshold = 0.0f;

    [SerializeField] [Tooltip("速度映射的最大阈值（米/秒）")]
    private float m_MaxVelocityThreshold = 0.3f;

    [SerializeField] [Tooltip("音量静音的速度阈值（米/秒）- 原始速度低于此值时音量为0")]
    private float m_VolumeThreshold = 0.015f;

    [SerializeField] [Tooltip("正常播放时的音量（0-100）")]
    private byte m_NormalVolume = 75;
    
    [SerializeField] [Tooltip("在编辑器中模拟速度值（仅供测试）")]
    private float m_SimulatedVelocity = 0.15f;

    [Header("OneDollar滤波器设置")]
    [SerializeField] [Tooltip("是否启用速度滤波")]
    private bool m_EnableVelocityFilter = true;
    
    // OneDollar滤波器
    public OneDollarFilter m_VelocityFilter;
    public OneDollarFilter m_MagnitudeFilter;

    [SerializeField] [Tooltip("速度滤波强度 (0.01-1.0)，值越小滤波效果越强")]
    [Range(0.01f, 1.0f)]
    private float m_VelocityFilterStrength = 0.1f;

    [SerializeField] [Tooltip("是否启用速度大小滤波")]
    private bool m_EnableMagnitudeFilter = true;

    [SerializeField] [Tooltip("速度大小滤波强度 (0.01-1.0)，值越小滤波效果越强")]
    [Range(0.01f, 1.0f)]
    private float m_MagnitudeFilterStrength = 0.15f;

    [Header("蓝牙连接设置")]
    [SerializeField] [Tooltip("蓝牙设备名称，多个名称用逗号分隔")]
    private string m_DeviceName = "ESP32-BLE,FeltSight BLE";

    [SerializeField] [Tooltip("断开连接后自动重连")]
    private bool m_AutoReconnect = true;

    [SerializeField] [Tooltip("重连尝试间隔（秒）")]
    private float m_ReconnectInterval = 3.0f;

    [SerializeField] [Tooltip("最大重连尝试次数，0表示无限次")]
    private int m_MaxReconnectAttempts = 0;

    [SerializeField] [Tooltip("显示连接状态的文本组件")]
    private Text m_ConnectionStatusText;

    [SerializeField] [Tooltip("连续发送失败次数阈值，超过此值触发重连")]
    private int m_FailureThreshold = 3;

    private CoreBluetoothManager m_Manager;
    private CoreBluetoothCharacteristic m_Characteristic;
    private bool m_IsConnectedAndReady = false;
    private bool m_IsScanStopped = false;
    private Coroutine m_DataSendCoroutine;
    private float m_CurrentSpeed = 0f;
    private byte m_CurrentSpeedByte = 10; // 默认值1.0x速度
    private byte m_CurrentVolume = 75; // 当前音量
    private float m_VelocityMultiplier = 1.0f; // 速度倍率
    
    // 每个手指的音量和速度控制
    private byte[] m_FingerVolumes = new byte[10]; // 每个手指的音量(0-100)
    private byte[] m_FingerSpeeds = new byte[10];  // 每个手指的速度(10-40)
    private float[] m_FingerVelocityMagnitudes = new float[10]; // 每个手指的速度大小
    
    // 连接状态管理
    private bool m_IsConnecting = false;
    private bool m_IsReconnecting = false;
    private int m_ReconnectAttempts = 0;
    private Coroutine m_ReconnectCoroutine = null;
    private CoreBluetoothPeripheral m_ConnectedPeripheral = null;
    private string[] m_TargetDeviceNames;
    private int m_ConsecutiveFailures = 0;
    private System.DateTime m_LastSuccessfulSend = System.DateTime.Now;
    private bool m_ConnectionLost = false;

    // 用于显示原始数据和滤波后数据的对比
    private Vector3 m_RawVelocity = Vector3.zero;
    private float m_RawMagnitude = 0f;
    private Vector3 m_FilteredVelocity = Vector3.zero;
    private float m_FilteredMagnitude = 0f;
    
    // 检查是否应该显示调试信息
    private bool ShouldShowDebugInfo()
    {
        return SuperAdmin.superAdmin != null ? SuperAdmin.superAdmin.showBLEDebugInfo : false;
    }

    // 最小速度和最大速度的字节值
    private const byte MIN_SPEED_BYTE = 10; // 1.0x
    private const byte MAX_SPEED_BYTE = 40; // 4.0x速度

    void Start()
    {
        // 处理设备名称列表
        if (!string.IsNullOrEmpty(m_DeviceName))
        {
            m_TargetDeviceNames = m_DeviceName.Split(',');
            for (int i = 0; i < m_TargetDeviceNames.Length; i++)
            {
                m_TargetDeviceNames[i] = m_TargetDeviceNames[i].Trim();
            }
        }
        else
        {
            m_TargetDeviceNames = new string[] { "ESP32-BLE", "FeltSight BLE" };
        }

        // 初始化每个手指的数组
        for (int i = 0; i < 10; i++)
        {
            m_FingerVolumes[i] = m_NormalVolume;
            m_FingerSpeeds[i] = m_CurrentSpeedByte;
            m_FingerVelocityMagnitudes[i] = 0f;
        }

        // 初始化OneDollar滤波器
        InitializeFilters();

        // 确保有手部追踪组件
        if (m_HandTracker == null)
        {
            m_HandTracker = FindFirstObjectByType<MyHand>();
            if (m_HandTracker == null)
            {
                Debug.LogError("MyHand component not found, please assign manually");
                enabled = false;
                return;
            }
        }
        
        // 初始化HandRaycaster引用
        if (m_HandRaycaster == null)
        {
            m_HandRaycaster = FindFirstObjectByType<HandRaycaster>();
            if (m_HandRaycaster == null)
            {
                Debug.LogWarning("HandRaycaster component not found, hit detection will not be available");
            }
        }
        

        // 初始化BLE
        InitializeBLE();
        
        // 更新连接状态UI
        UpdateConnectionStatusUI("Initializing...");
    }

    void Update()
    {
        // 更新所有手指的速度和音量
        UpdateAllFingersVelocityAndVolume();
        
        // 更新食指尖速度信息用于UI显示
        UpdateVelocityText(m_FilteredMagnitude);
        
        // 检查连接状态
        CheckConnectionStatus();
    }

    void OnDestroy()
    {
        

        StopDataTransmission();
        StopReconnectProcess();
        
        if (m_Manager != null)
        {
            m_Manager.Stop();
        }
    }

    /// <summary>
    /// 初始化OneDollar滤波器
    /// </summary>
    private void InitializeFilters()
    {
        Debug.Log($"OneDollar filters initialized - Velocity filter strength: {m_VelocityFilterStrength}, Magnitude filter strength: {m_MagnitudeFilterStrength}");
    }

    /// <summary>
    /// 初始化蓝牙连接
    /// </summary>
    private void InitializeBLE()
    {
        Debug.Log("BLESendJointV starting BLE connection initialization");
        try
        {
            m_Manager = CoreBluetoothManager.Shared;

            m_Manager.OnUpdateState((string state) =>
            {
                Debug.Log("BLE state: " + state);
                UpdateConnectionStatusUI("BLE Status: " + state);
                
                if (state != "poweredOn") return;
                
                // 只有在非重连状态下才自动开始扫描
                if (!m_IsReconnecting)
                {
                    StartScan();
                }
            });

            m_Manager.OnDiscoverPeripheral((CoreBluetoothPeripheral peripheral) =>
            {
                if (peripheral.name != "" && peripheral.name != null && peripheral.name != "(null-name)" && peripheral.name != "null-name")
                {
                    if (ShouldShowDebugInfo())
                    {
                        Debug.Log("Device discovered: " + peripheral.name);
                    }
                }

                // 检查设备名称是否在目标列表中
                bool isTargetDevice = false;
                foreach (string deviceName in m_TargetDeviceNames)
                {
                    if (peripheral.name == deviceName)
                    {
                        isTargetDevice = true;
                        break;
                    }
                }

                if (!isTargetDevice) return;

                m_Manager.StopScan();
                m_IsScanStopped = true;
                m_IsConnecting = true;
                
                Debug.Log("Scan stopped, preparing to connect to device: " + peripheral.name);
                UpdateConnectionStatusUI("Connecting to: " + peripheral.name);
                
                m_Manager.ConnectToPeripheral(peripheral);
            });

            m_Manager.OnConnectPeripheral((CoreBluetoothPeripheral peripheral) =>
            {
                m_ConnectedPeripheral = peripheral;
                m_IsConnecting = false;
                m_IsReconnecting = false;
                m_ReconnectAttempts = 0;
                m_ConsecutiveFailures = 0;
                m_ConnectionLost = false;
                
                Debug.Log("Connected to device: " + peripheral.name);
                UpdateConnectionStatusUI("Connected: " + peripheral.name);
                
                peripheral.discoverServices();
            });

            m_Manager.OnDiscoverService((CoreBluetoothService service) =>
            {
                Debug.Log("Service UUID discovered: " + service.uuid);
                // ESP32服务UUID
                if (service.uuid.ToUpper() != "6E400001-B5A3-F393-E0A9-E50E24DCCA9E") return;
                service.discoverCharacteristics();
            });

            m_Manager.OnDiscoverCharacteristic((CoreBluetoothCharacteristic characteristic) =>
            {
                string uuid = characteristic.Uuid.ToUpper();
                string[] usage = characteristic.Propertis;
                Debug.Log("Characteristic UUID discovered: " + uuid + ", Usage: " + string.Join(",", usage));

                // 查找RX特征（用于写入数据到ESP32）
                if (uuid == "6E400002-B5A3-F393-E0A9-E50E24DCCA9E")
                {
                    m_Characteristic = characteristic;
                    Debug.Log("RX characteristic found, ready to send data");
                    UpdateConnectionStatusUI("Connected and Ready");

                    // 确保扫描已停止后才设置连接就绪状态
                    m_IsConnectedAndReady = true;
                    m_LastSuccessfulSend = System.DateTime.Now;

                    // 确保不会重复启动数据发送
                    if (m_DataSendCoroutine == null)
                    {
                        Debug.Log("Scan stopped, starting data transmission");
                        StartDataTransmission();
                    }
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

            m_Manager.Start();
        }
        catch (System.Exception e)
        {
            // 记录初始化错误但允许程序继续运行
            Debug.LogError($"BLE initialization failed, but main process continues: {e.Message}");
            UpdateConnectionStatusUI("BLE Init Failed: " + e.Message);
        }
    }

    /// <summary>
    /// 检查连接状态，如果长时间没有成功发送数据，认为Connection Lost
    /// </summary>
    private void CheckConnectionStatus()
    {
        // 如果已标记为连接丢失或正在重连，则跳过检查
        if (m_ConnectionLost || m_IsReconnecting || !m_IsConnectedAndReady)
            return;
            
        // 检查距离上次成功发送数据的时间
        double secondsSinceLastSuccess = (System.DateTime.Now - m_LastSuccessfulSend).TotalSeconds;
        
        // 如果超过发送间隔的5倍，且连续失败次数超过阈值，认为Connection Lost
        if (secondsSinceLastSuccess > m_SendInterval * 5 && m_ConsecutiveFailures >= m_FailureThreshold)
        {
            if (ShouldShowDebugInfo())
            {
                Debug.Log($"Connection appears to be lost: {m_ConsecutiveFailures} consecutive failures, " +
                         $"{secondsSinceLastSuccess:F1}s since last successful send");
            }
            
            m_ConnectionLost = true;
            m_IsConnectedAndReady = false;
            
            // 如果启用了自动重连，开始重连过程
            if (m_AutoReconnect)
            {
                UpdateConnectionStatusUI("Connection Lost, Reconnecting...");
                StartReconnectProcess();
            }
            else
            {
                UpdateConnectionStatusUI("Connection Lost");
            }
        }
    }

    /// <summary>
    /// 开始扫描蓝牙设备
    /// </summary>
    private void StartScan()
    {
        if (m_Manager == null) return;
        
        try
        {
            m_IsScanStopped = false;
            if (ShouldShowDebugInfo())
            {
                Debug.Log("Starting BLE scan...");
            }
            UpdateConnectionStatusUI("Scanning Devices...");
            m_Manager.StartScan();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to start scan: {e.Message}");
            UpdateConnectionStatusUI("Scan Failed: " + e.Message);
        }
    }

    /// <summary>
    /// 开始重连过程
    /// </summary>
    private void StartReconnectProcess()
    {
        if (m_ReconnectCoroutine != null)
        {
            StopCoroutine(m_ReconnectCoroutine);
        }
        
        m_IsReconnecting = true;
        m_ReconnectAttempts = 0;
        m_ReconnectCoroutine = StartCoroutine(ReconnectCoroutine());
    }

    /// <summary>
    /// 停止重连过程
    /// </summary>
    private void StopReconnectProcess()
    {
        if (m_ReconnectCoroutine != null)
        {
            StopCoroutine(m_ReconnectCoroutine);
            m_ReconnectCoroutine = null;
        }
        
        m_IsReconnecting = false;
    }

    /// <summary>
    /// 重连协程
    /// </summary>
    private IEnumerator ReconnectCoroutine()
    {
        while (true)
        {
            // 检查最大重连次数
            if (m_MaxReconnectAttempts > 0 && m_ReconnectAttempts >= m_MaxReconnectAttempts)
            {
                if (ShouldShowDebugInfo())
                {
                    Debug.Log($"Maximum reconnect attempts ({m_MaxReconnectAttempts}) reached, stopping reconnect process");
                }
                UpdateConnectionStatusUI($"Reconnect Failed: Max attempts reached ({m_MaxReconnectAttempts})");
                m_IsReconnecting = false;
                yield break;
            }

            m_ReconnectAttempts++;
            if (ShouldShowDebugInfo())
            {
                Debug.Log($"Attempting to reconnect (attempt {m_ReconnectAttempts})...");
            }
            UpdateConnectionStatusUI($"Reconnecting (Attempt {m_ReconnectAttempts})...");

            // 如果有之前连接的设备，尝试直接连接
            if (m_ConnectedPeripheral != null)
            {
                
                {
                    if (ShouldShowDebugInfo())
                    {
                        Debug.Log($"Trying to reconnect to last device: {m_ConnectedPeripheral.name}");
                    }
                    m_IsConnecting = true;
                    m_Manager.ConnectToPeripheral(m_ConnectedPeripheral);
                    
                    // 等待一段时间看是否连接成功
                    float waitTime = 0;
                    while (waitTime < m_ReconnectInterval && m_IsConnecting)
                    {
                        yield return new WaitForSeconds(0.1f);
                        waitTime += 0.1f;
                    }
                    
                    // 如果连接成功，退出重连循环
                    if (m_IsConnectedAndReady)
                    {
                        if (ShouldShowDebugInfo())
                        {
                            Debug.Log("Reconnection successful");
                        }
                        m_IsReconnecting = false;
                        m_ConnectionLost = false;
                        yield break;
                    }
                }

            }

            // 如果直接连接失败，尝试重新扫描
            
            {
                // 确保之前的扫描已停止
                if (!m_IsScanStopped)
                {
                    m_Manager.StopScan();
                    yield return new WaitForSeconds(0.5f);
                }
                
                // 开始新的扫描
                StartScan();
                
                // 等待扫描和连接过程
                yield return new WaitForSeconds(m_ReconnectInterval);
                
                // 如果连接成功，退出重连循环
                if (m_IsConnectedAndReady)
                {
                    if (ShouldShowDebugInfo())
                    {
                        Debug.Log("Reconnection successful after scan");
                    }
                    m_IsReconnecting = false;
                    m_ConnectionLost = false;
                    yield break;
                }
            }
            
            // 等待下一次重连尝试
            yield return new WaitForSeconds(0.5f);
        }
    }
    
    /// <summary>
    /// 更新所有手指的速度和音量
    /// </summary>
    private void UpdateAllFingersVelocityAndVolume()
    {
       

        // 手指关节ID数组
        XRHandJointID[] fingerJoints = {
            XRHandJointID.ThumbTip,
            XRHandJointID.IndexTip,
            XRHandJointID.MiddleTip,
            XRHandJointID.RingTip,
            XRHandJointID.LittleTip
        };

        // 为每个手指计算速度和音量（这里简化处理，实际可能需要更复杂的逻辑）
        for (int i = 0; i < 10; i++)
        {
            // 对于前5个通道使用右手，后5个通道使用左手
            Handedness handToUse = (i < 5) ? Handedness.Right : Handedness.Left;
            int jointIndex = i % 5;
            
            if (jointIndex < fingerJoints.Length)
            {
                Vector3 fingerRawVelocity = Vector3.zero;
                
                #if UNITY_EDITOR
                // 在编辑器中使用模拟值进行测试
                fingerRawVelocity = new Vector3(m_SimulatedVelocity, 0, 0);
                #else
                // 获取手指尖速度
                if (m_HandTracker.TryGetJointPositionAndVelocity(
                    handToUse, fingerJoints[jointIndex], out Vector3 position, out Vector3 velocity))
                {
                    fingerRawVelocity = velocity;
                }
                #endif

                float fingerVelocityMagnitude = fingerRawVelocity.magnitude;
                m_FingerVelocityMagnitudes[i] = fingerVelocityMagnitude;
                
                // 应用滤波
                if (m_EnableVelocityFilter && m_VelocityFilter != null)
                {
                    fingerRawVelocity = m_VelocityFilter.Filter(fingerRawVelocity);
                    fingerVelocityMagnitude = fingerRawVelocity.magnitude;
                }
                
                // 应用速度大小滤波
                if (m_EnableMagnitudeFilter && m_MagnitudeFilter != null)
                {
                    fingerVelocityMagnitude = m_MagnitudeFilter.Filter(fingerVelocityMagnitude);
                }

                // 应用倍率
                fingerVelocityMagnitude = fingerVelocityMagnitude * m_VelocityMultiplier;

                // 计算音量
                byte volume = m_NormalVolume;
                
                // 检查手指是否击中物体，如果没有击中则静音
                bool isFingerHit = false;
                if (m_HandRaycaster != null)
                {
                    Handedness handToUse2 = (i < 5) ? Handedness.Right : Handedness.Left;
                    int fingerIndex = i % 5;
                    RaycastHit hit;
                    isFingerHit = m_HandRaycaster.TryGetFingerHit(handToUse2, fingerIndex, out hit);
                }
                
                // 如果手指没有击中物体，或者速度低于阈值，则静音
                if (!isFingerHit || fingerVelocityMagnitude < m_VolumeThreshold)
                {
                    volume = 0; // 没有击中或速度低于阈值时静音
                }
                
                // 保存每个手指的音量
                m_FingerVolumes[i] = volume;

                // 将速度映射到1.0x-4.0x范围 (10-40)
                float clampedSpeed = Mathf.Clamp(fingerVelocityMagnitude, m_MinVelocityThreshold, m_MaxVelocityThreshold);
                float normalizedSpeedFinger = Mathf.InverseLerp(m_MinVelocityThreshold, m_MaxVelocityThreshold, clampedSpeed);
                int speedByteValue = Mathf.RoundToInt(Mathf.Lerp(MIN_SPEED_BYTE, MAX_SPEED_BYTE, normalizedSpeedFinger));
                
                // 保存每个手指的速度
                m_FingerSpeeds[i] = (byte)speedByteValue;
            }
        }
    }

    /// <summary>
    /// 开始发送数据
    /// </summary>
    private void StartDataTransmission()
    {
        if (m_DataSendCoroutine != null)
        {
            StopCoroutine(m_DataSendCoroutine);
        }

        // 确保扫描已停止后才开始发送数据
        if (m_IsScanStopped)
        {
            if (ShouldShowDebugInfo())
            {
                Debug.Log("Starting periodic data transmission");
            }
            m_DataSendCoroutine = StartCoroutine(SendDataPeriodically());
        }
        else
        {
            if (ShouldShowDebugInfo())
            {
                Debug.LogWarning("Scan not stopped yet, cannot send data");
            }
        }
    }

    /// <summary>
    /// 停止发送数据
    /// </summary>
    private void StopDataTransmission()
    {
        if (m_DataSendCoroutine != null)
        {
            StopCoroutine(m_DataSendCoroutine);
            m_DataSendCoroutine = null;
        }
    }

    /// <summary>
    /// 定期发送数据的协程
    /// </summary>
    private IEnumerator SendDataPeriodically()
    {
        int counter = 0;

        while (m_IsConnectedAndReady && !m_ConnectionLost)
        {
            try
            {
                if (m_Characteristic != null)
                {
                    byte[] data = GenerateData(counter);
                    SendDataToESP32(data);
                    counter++;
                }
                else
                {
                    if (ShouldShowDebugInfo())
                    {
                        Debug.LogWarning("BLE characteristic not available, waiting for next attempt");
                    }
                    m_ConsecutiveFailures++;
                }
            }
            catch (System.Exception e)
            {
                // 捕获所有异常，确保协程不会因任何错误而中断
                if (ShouldShowDebugInfo())
                {
                    Debug.LogWarning($"Error occurred during transmission cycle, but continuing: {e.Message}");
                }
                m_ConsecutiveFailures++;
            }

            yield return new WaitForSeconds(m_SendInterval);
        }
        
        if (ShouldShowDebugInfo())
        {
            Debug.Log("Data transmission stopped due to connection loss or state change");
        }
    }

    /// <summary>
    /// 生成要发送的数据
    /// </summary>
    private byte[] GenerateData(int counter)
    {
        byte[] data = new byte[32];

        // 起始标记
        data[0] = 0xFE;

        // 生成10个通道的数据
        for (int channel = 0; channel < 10; channel++)
        {
            int offset = 1 + channel * 3;

            // 文件索引：从ScreenSpaceProjector的fingerMats获取材质类型
            byte fileIndex = 1; // 默认值
            if (m_ScreenSpaceProjector != null)
            {
                string materialType = m_ScreenSpaceProjector.fingerMats[channel];
                fileIndex = MapMaterialToIndex(materialType);
            }
            // TODO 这里由于相机有问题所以文件全部都用第五个
            data[offset] = 5;
            // data[offset] = fileIndex;

            // 获取当前手指的速度和音量
            byte fingerVolume = GetFingerVolume(channel);
            byte fingerSpeed = GetFingerSpeed(channel);

            // 音量：使用每个手指单独的音量值
            data[offset + 1] = fingerVolume;

            // 速度：使用每个手指单独的速度值
            data[offset + 2] = fingerSpeed;
        }

        // 结束标记
        data[31] = 0xFF;

        return data;
    }

    /// <summary>
    /// 发送数据到ESP32，添加了连接状态检查和错误处理
    /// </summary>
    private void SendDataToESP32(byte[] data)
    {
        if (m_Characteristic == null || !m_IsConnectedAndReady || m_ConnectionLost)
        {
            if (ShouldShowDebugInfo())
            {
                Debug.LogWarning("Characteristic not ready or connection lost");
            }
            m_ConsecutiveFailures++;
            
            // 如果连续失败次数超过阈值，触发重连
            if (m_ConsecutiveFailures >= m_FailureThreshold && !m_ConnectionLost)
            {
                m_ConnectionLost = true;
                m_IsConnectedAndReady = false;
                
                if (m_AutoReconnect && !m_IsReconnecting)
                {
                    if (ShouldShowDebugInfo())
                    {
                        Debug.Log($"Connection appears to be lost after {m_ConsecutiveFailures} consecutive failures");
                    }
                    UpdateConnectionStatusUI("Connection Lost, Reconnecting...");
                    StartReconnectProcess();
                }
                else
                {
                    UpdateConnectionStatusUI("Connection Lost");
                }
            }
            
            return;
        }

        try
        {
            m_Characteristic.Write(data);
            
            // 重置连续失败计数并更新最后成功发送时间
            m_ConsecutiveFailures = 0;
            m_LastSuccessfulSend = System.DateTime.Now;

            if (ShouldShowDebugInfo())
            {
                // 打印发送的数据用于调试
                string hexString = System.BitConverter.ToString(data).Replace("-", " ");
                Debug.Log($"Data sent: {hexString}");

                // 打印解析后的数据
                LogDataContent(data);

                // 打印当前食指速度和映射值，包含滤波信息和音量状态
                string volumeStatus = m_CurrentVolume == 0 ? "Muted" : $"Volume {m_CurrentVolume}%";
                Debug.Log($"Raw velocity: {m_RawMagnitude:F3} m/s, Filtered velocity: {m_FilteredMagnitude:F3} m/s, Mapped speed: {m_CurrentSpeedByte / 10f:F1}x, Value: {m_CurrentSpeedByte}, {volumeStatus}");
            }
        }
        catch (System.Exception e)
        {
            // 记录错误并增加连续失败计数
            if (ShouldShowDebugInfo())
            {
                Debug.LogWarning($"Failed to send data: {e.Message}");
            }
            m_ConsecutiveFailures++;
            
            // 如果连续失败次数超过阈值，触发重连
            if (m_ConsecutiveFailures >= m_FailureThreshold && !m_ConnectionLost)
            {
                m_ConnectionLost = true;
                m_IsConnectedAndReady = false;
                
                if (m_AutoReconnect && !m_IsReconnecting)
                {
                    if (ShouldShowDebugInfo())
                    {
                        Debug.Log($"Connection appears to be lost after {m_ConsecutiveFailures} consecutive failures");
                    }
                    UpdateConnectionStatusUI("Connection Lost, Reconnecting...");
                    StartReconnectProcess();
                }
                else
                {
                    UpdateConnectionStatusUI("Connection Lost");
                }
            }
        }
    }

    /// <summary>
    /// 打印数据内容（用于调试）
    /// </summary>
    private void LogDataContent(byte[] data)
    {
        if (data.Length != 32 || data[0] != 0xFE || data[31] != 0xFF)
        {
            Debug.LogError("Data format error");
            return;
        }

        // LogDataContent is only called from within a ShouldShowDebugInfo() block,
        // so we don't need to check again here
        Debug.Log("=== Data Content ===");
        for (int i = 0; i < 10; i++)
        {
            int offset = 1 + i * 3;
            byte fileIndex = data[offset];
            byte volume = data[offset + 1];
            byte speed = data[offset + 2];

            string volumeInfo = volume == 0 ? "Muted" : $"{volume}%";
            Debug.Log($"Channel {i + 1}: File={fileIndex}, Volume={volumeInfo}, Speed={speed / 10f:F1}x");
        }
        Debug.Log("==================");
    }
    
    /// <summary>
    /// 更新速度显示文本
    /// </summary>
    /// <param name="velocityMagnitude">手指速度大小</param>
    private void UpdateVelocityText(float velocityMagnitude)
    {
        if (m_VelocityText != null)
        {
            try
            {
                // 显示原始速度、滤波后速度、映射后的速度和音量状态
                string filterInfo = m_EnableVelocityFilter ? $"(filter strength: {m_VelocityFilterStrength:F2})" : "(no filter)";
                string volumeStatus = m_CurrentVolume == 0 ? "Mute" : $"Volume {m_CurrentVolume}%";
                
                // 使用当前倍率值(由外部通过SetVelocityMultiplier方法设置)
                m_VelocityText.text = $"Ori V: {m_RawMagnitude:F3} m/s\nFiltered V: {m_FilteredMagnitude:F3} m/s {filterInfo}\nPlay V: {m_CurrentSpeedByte / 10f:F1}x\nFactor: {m_VelocityMultiplier:F1}\nMin V: {m_MinVelocityThreshold:F3} m/s\nMax V: {m_MaxVelocityThreshold:F3} m/s\n{volumeStatus}\n"+GetFilterStatus();
                // 根据滤波后的速度变化颜色，静音时显示灰色
                if (m_CurrentVolume == 0)
                {
                    m_VelocityText.color = Color.gray;
                }
                else
                {
                    float normalizedSpeed = Mathf.InverseLerp(m_MinVelocityThreshold, m_MaxVelocityThreshold, m_FilteredMagnitude);
                    m_VelocityText.color = Color.Lerp(Color.green, Color.red, normalizedSpeed);
                }
            }
            catch (System.Exception e)
            {
                // 防止UI更新异常影响主流程
                Debug.LogWarning($"Error occurred while updating velocity UI: {e.Message}");
            }
        }
        
        // 同时更新连接状态文本中的手指信息
        UpdateFingerInfoText();
    }
    
    /// <summary>
    /// 更新连接状态UI
    /// </summary>
    private void UpdateConnectionStatusUI(string status)
    {
        if (m_ConnectionStatusText != null)
        {
            try
            {
                m_ConnectionStatusText.text = "BLE status: " + status;
                
                // // 根据状态设置颜色
                // if (status.Contains("Connected and Ready"))
                // {
                //     m_ConnectionStatusText.color = Color.green;
                // }
                // else if (status.Contains("Connecting") || status.Contains("Scanning") || status.Contains("Reconnecting"))
                // {
                //     m_ConnectionStatusText.color = Color.yellow;
                // }
                // else if (status.Contains("Lost") || status.Contains("Failed") || status.Contains("Error"))
                // {
                //     m_ConnectionStatusText.color = Color.red;
                // }
                // else
                // {
                //     m_ConnectionStatusText.color = Color.white;
                // }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Error updating connection status UI: {e.Message}");
            }
        }
    }
    
    /// <summary>
    /// 更新手指信息文本
    /// </summary>
    private void UpdateFingerInfoText()
    {
        if (m_ConnectionStatusText != null)
        {
            try
            {
                // 构建每个手指的信息
                string fingerInfo = "\nFingers Status:";
                string[] fingerNames = {"Thumb", "Index", "Middle", "Ring", "Little"};
                
                for (int i = 0; i < 10; i++)
                {
                    int handIndex = i / 5;  // 0 for right hand, 1 for left hand
                    int fingerIndex = i % 5;
                    string handName = handIndex == 0 ? "R" : "L";
                    
                    fingerInfo += $"\n{handName}-{fingerNames[fingerIndex]}: V={m_FingerVelocityMagnitudes[i]:F3}m/s, Vol={m_FingerVolumes[i]}%, Speed={m_FingerSpeeds[i] / 10f:F1}x";
                }
                
                // 添加到现有文本后面
                m_ConnectionStatusText.text = fingerInfo;
                // m_ConnectionStatusText.color = new Color(4,145,255,255);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Error updating finger info text: {e.Message}");
            }
        }
    }
    


    /// <summary>
    /// 设置速度倍率
    /// </summary>
    /// <param name="multiplier">速度倍率，建议范围0.1-5.0</param>
    public void SetVelocityMultiplier(float multiplier)
    {
        // 确保倍率在合理范围内
        m_VelocityMultiplier = Mathf.Clamp(multiplier, 0.1f, 10.0f);
        
        // 计算实际会传输的速度值
        float basePlaybackRate = m_CurrentSpeedByte / 10f; // 原始映射速率（1.0x-4.0x）
        float actualPlaybackRate = Mathf.Clamp(basePlaybackRate * m_VelocityMultiplier, 1.0f, 4.0f); // 应用倍率后的实际速率
        byte finalSpeedByte = (byte)Mathf.RoundToInt(actualPlaybackRate * 10); // 最终传输的字节值

        if (ShouldShowDebugInfo())
        {
            Debug.Log($"Velocity multiplier set to: {m_VelocityMultiplier:F1}, Original speed: {basePlaybackRate:F1}x, Actual transmission speed: {actualPlaybackRate:F1}x (Value: {finalSpeedByte})");
        }
    }

    /// <summary>
    /// 设置滤波强度
    /// </summary>
    /// <param name="strength">滤波强度 (0.01-1.0)，值越小滤波效果越强</param>
    public void SetFilterStrength(float strength)
    {
        m_VelocityFilterStrength = Mathf.Clamp(strength, 0.01f, 1.0f);
        m_MagnitudeFilterStrength = m_VelocityFilterStrength; // 同步设置

        // 更新滤波器强度
        if (m_VelocityFilter != null)
        {
            m_VelocityFilter.SetFilterStrength(m_VelocityFilterStrength);
        }

        if (m_MagnitudeFilter != null)
        {
            m_MagnitudeFilter.SetFilterStrength(m_MagnitudeFilterStrength);
        }

        if (ShouldShowDebugInfo())
        {
            Debug.Log($"Filter strength set to: {m_VelocityFilterStrength:F2}");
        }
    }

    /// <summary>
    /// 设置音量阈值
    /// </summary>
    /// <param name="threshold">音量阈值（米/秒），原始速度低于此值时音量为0</param>
    public void SetVolumeThreshold(float threshold)
    {
        m_VolumeThreshold = Mathf.Max(0f, threshold);
        
        if (ShouldShowDebugInfo())
        {
            Debug.Log($"Local volume threshold set to: {m_VolumeThreshold:F3} m/s");
        }
    }
    
    /// <summary>
    /// 设置速度映射的最大阈值
    /// </summary>
    /// <param name="threshold">速度映射的最大阈值（米/秒）</param>
    public void SetMaxVelocityThreshold(float threshold)
    {
        m_MaxVelocityThreshold = Mathf.Max(m_MinVelocityThreshold + 0.01f, threshold);
        
        if (ShouldShowDebugInfo())
        {
            Debug.Log($"Max velocity threshold set to: {m_MaxVelocityThreshold:F3} m/s");
        }
    }

    /// <summary>
    /// 启用或禁用速度滤波
    /// </summary>
    /// <param name="enable">是否启用滤波</param>
    public void SetVelocityFilterEnabled(bool enable)
    {
        m_EnableVelocityFilter = enable;

        if (!enable && m_VelocityFilter != null)
        {
            m_VelocityFilter.Reset();
        }

        if (ShouldShowDebugInfo())
        {
            Debug.Log($"Velocity filter {(enable ? "enabled" : "disabled")}");
        }
    }

    /// <summary>
    /// 启用或禁用速度大小滤波
    /// </summary>
    /// <param name="enable">是否启用滤波</param>
    public void SetMagnitudeFilterEnabled(bool enable)
    {
        // 直接应用从服务器接收到的设置
        m_EnableMagnitudeFilter = enable;

        if (!enable && m_MagnitudeFilter != null)
        {
            m_MagnitudeFilter.Reset();
        }

        if (ShouldShowDebugInfo())
        {
            Debug.Log($"Magnitude filter set to {(enable ? "enabled" : "disabled")} by server");
        }
    }
    

    /// <summary>
    /// 获取滤波器状态信息
    /// </summary>
    /// <returns>滤波器状态字符串</returns>
    public string GetFilterStatus()
    {
        string volumeStatus = m_CurrentVolume == 0 ? "Muted" : $"Volume {m_CurrentVolume}%";
        
        // 所有设置都由外部通过相应的方法设置
        float currentThreshold = m_VolumeThreshold;
        bool currentMagnitudeFilterEnabled = m_EnableMagnitudeFilter;
        
        return $"Velocity filter: {(m_EnableVelocityFilter ? "Enabled" : "Disabled")} (Strength: {m_VelocityFilterStrength:F2})\n" +
               $"Magnitude filter: {(currentMagnitudeFilterEnabled ? "Enabled" : "Disabled")} (Strength: {m_MagnitudeFilterStrength:F2})\n" +
               $"Raw velocity: {m_RawMagnitude:F3} m/s\n" +
               $"Filtered velocity: {m_FilteredMagnitude:F3} m/s\n" +
               $"Volume threshold: {currentThreshold:F3} m/s\n" +
               $"Current status: {volumeStatus}";
    }
    
    
    /// <summary>
    /// 将材质类型映射到文件索引
    /// </summary>
    /// <param name="materialType">材质类型字符串</param>
    /// <returns>对应的文件索引(1-12)，如果未找到匹配则返回1</returns>
    private byte MapMaterialToIndex(string materialType)
    {
        if (string.IsNullOrEmpty(materialType))
            return 1; // 默认值

        switch (materialType.ToUpper())
        {
            case "M1": return 1;   // 金属
            case "M2": return 2;   // 玻璃/陶瓷
            case "M3": return 3;   // 硬塑
            case "M4": return 4;   // 木材
            case "M5": return 5;   // 石材/水泥
            case "M6": return 6;   // 织物/毛皮
            case "M7": return 7;   // 皮革/橡胶
            case "M8": return 8;   // 纸/纸板
            case "M9": return 9;   // 食物软组织
            case "M10": return 10; // 植被/土壤
            case "M11": return 11; // 电子玻璃面板
            case "M12": return 12; // 泡沫/海绵/复合
            default: return 1;     // 默认值
        }
    }

    /// <summary>
    /// 获取指定手指的音量
    /// </summary>
    /// <param name="fingerIndex">手指索引(0-9)</param>
    /// <returns>音量值(0-100)</returns>
    private byte GetFingerVolume(int fingerIndex)
    {
        if (fingerIndex < 0 || fingerIndex >= 10)
            return 0;
        
        // 如果还没有初始化，则使用全局音量
        if (m_FingerVolumes[fingerIndex] == 0 && m_CurrentVolume > 0)
            return m_CurrentVolume;
            
        return m_FingerVolumes[fingerIndex];
    }

    /// <summary>
    /// 获取指定手指的速度
    /// </summary>
    /// <param name="fingerIndex">手指索引(0-9)</param>
    /// <returns>速度值(10-40)</returns>
    private byte GetFingerSpeed(int fingerIndex)
    {
        if (fingerIndex < 0 || fingerIndex >= 10)
            return 10; // 默认最小速度
        
        // 如果还没有初始化，则使用全局速度
        if (m_FingerSpeeds[fingerIndex] == 0 && m_CurrentSpeedByte > 0)
            return m_CurrentSpeedByte;
            
        // 确保速度在有效范围内
        return (byte)Mathf.Clamp(m_FingerSpeeds[fingerIndex], MIN_SPEED_BYTE, MAX_SPEED_BYTE);
    }

    /// <summary>
    /// 设置指定手指的音量
    /// </summary>
    /// <param name="fingerIndex">手指索引(0-9)</param>
    /// <param name="volume">音量值(0-100)</param>
    public void SetFingerVolume(int fingerIndex, byte volume)
    {
        if (fingerIndex < 0 || fingerIndex >= 10)
            return;
            
        m_FingerVolumes[fingerIndex] = (byte)Mathf.Clamp(volume, 0, 100);
        
        if (ShouldShowDebugInfo())
        {
            Debug.Log($"Finger {fingerIndex} volume set to: {m_FingerVolumes[fingerIndex]}");
        }
    }

    /// <summary>
    /// 设置指定手指的速度
    /// </summary>
    /// <param name="fingerIndex">手指索引(0-9)</param>
    /// <param name="speed">速度值(10-40)</param>
    public void SetFingerSpeed(int fingerIndex, byte speed)
    {
        if (fingerIndex < 0 || fingerIndex >= 10)
            return;
            
        m_FingerSpeeds[fingerIndex] = (byte)Mathf.Clamp(speed, MIN_SPEED_BYTE, MAX_SPEED_BYTE);
        
        if (ShouldShowDebugInfo())
        {
            Debug.Log($"Finger {fingerIndex} speed set to: {m_FingerSpeeds[fingerIndex]} ({m_FingerSpeeds[fingerIndex] / 10f:F1}x)");
        }
    }

    /// <summary>
    /// 手动触发重连（可以通过UI按钮调用）
    /// </summary>
    public void ManualReconnect()
    {
        if (m_IsConnectedAndReady && !m_ConnectionLost)
        {
            Debug.Log("Already connected, no need to reconnect");
            return;
        }
        
        if (m_IsReconnecting)
        {
            StopReconnectProcess();
        }
        
        // 重置连接状态
        m_IsConnectedAndReady = false;
        m_ConnectionLost = true;
        
        // 开始重连
        UpdateConnectionStatusUI("Manual Reconnect Triggered...");
        StartReconnectProcess();
    }

}
