using UnityEngine;
using UnityCoreBluetooth;
using System.Collections;
using TMPro;
using UnityEngine.UI;
using UnityEngine.XR.Hands;

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

    [SerializeField] [Tooltip("要使用的手（左手或右手）")]
    private Handedness m_HandToUse = Handedness.Right;

    [SerializeField] [Tooltip("用于显示手指速度的文本组件")]
    private Text m_VelocityText;

    [SerializeField] [Tooltip("用于调整速度倍率的Slider")]
    private UnityEngine.UI.Slider m_VelocitySlider;

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

    [SerializeField] [Tooltip("是否在控制台显示调试信息")]
    private bool m_ShowDebugInfo = true;

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

    [SerializeField] [Tooltip("用于调整速度滤波强度的Slider")]
    private UnityEngine.UI.Slider m_FilterStrengthSlider;

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

        // 注册Slider事件（如果已分配）
        if (m_VelocitySlider != null)
        {
            m_VelocitySlider.onValueChanged.AddListener(OnVelocitySliderChanged);
        }

        if (m_FilterStrengthSlider != null)
        {
            m_FilterStrengthSlider.value = m_VelocityFilterStrength;
            m_FilterStrengthSlider.onValueChanged.AddListener(OnFilterStrengthSliderChanged);
        }

        // 初始化BLE
        InitializeBLE();
        
        // 更新连接状态UI
        UpdateConnectionStatusUI("Initializing...");
    }

    void Update()
    {
        // 更新食指尖速度并映射到速度值
        UpdateFingerVelocityAndSpeed();
        
        // 检查连接状态
        CheckConnectionStatus();
    }

    void OnDestroy()
    {
        // 移除Slider监听器
        if (m_VelocitySlider != null)
        {
            m_VelocitySlider.onValueChanged.RemoveListener(OnVelocitySliderChanged);
        }

        if (m_FilterStrengthSlider != null)
        {
            m_FilterStrengthSlider.onValueChanged.RemoveListener(OnFilterStrengthSliderChanged);
        }

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
                    Debug.Log("Device discovered: " + peripheral.name);
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
            Debug.Log($"Connection appears to be lost: {m_ConsecutiveFailures} consecutive failures, " +
                     $"{secondsSinceLastSuccess:F1}s since last successful send");
            
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
            Debug.Log("Starting BLE scan...");
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
                Debug.Log($"Maximum reconnect attempts ({m_MaxReconnectAttempts}) reached, stopping reconnect process");
                UpdateConnectionStatusUI($"Reconnect Failed: Max attempts reached ({m_MaxReconnectAttempts})");
                m_IsReconnecting = false;
                yield break;
            }

            m_ReconnectAttempts++;
            Debug.Log($"Attempting to reconnect (attempt {m_ReconnectAttempts})...");
            UpdateConnectionStatusUI($"Reconnecting (Attempt {m_ReconnectAttempts})...");

            // 如果有之前连接的设备，尝试直接连接
            if (m_ConnectedPeripheral != null)
            {
                
                {
                    Debug.Log($"Trying to reconnect to last device: {m_ConnectedPeripheral.name}");
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
                        Debug.Log("Reconnection successful");
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
                    Debug.Log("Reconnection successful after scan");
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
    /// 更新食指尖速度并映射到速度值
    /// </summary>
    private void UpdateFingerVelocityAndSpeed()
    {
        Vector3 rawVelocity = Vector3.zero;
        float velocityMagnitude = 0f;

        #if UNITY_EDITOR
        // 在编辑器中使用模拟值进行测试
        rawVelocity = new Vector3(m_SimulatedVelocity, 0, 0);
        #else
        // 获取食指尖速度
        if (m_HandTracker.TryGetJointPositionAndVelocity(
            m_HandToUse, XRHandJointID.IndexTip, out Vector3 position, out Vector3 velocity))
        {
            rawVelocity = velocity;
        }
        #endif

        // 保存原始数据用于显示
        m_RawVelocity = rawVelocity;
        m_RawMagnitude = rawVelocity.magnitude;

        // 根据原始速度决定音量
        if (m_RawMagnitude < m_VolumeThreshold)
        {
            m_CurrentVolume = 0; // 原始速度低于阈值时静音
        }
        else
        {
            m_CurrentVolume = m_NormalVolume; // 使用正常音量
        }

        // 应用OneDollar滤波
        Vector3 filteredVelocity = rawVelocity;
        if (m_EnableVelocityFilter && m_VelocityFilter != null)
        {
            filteredVelocity = m_VelocityFilter.Filter(rawVelocity);
        }

        // 计算滤波后的速度大小
        float filteredMagnitude = filteredVelocity.magnitude;
        
        // 对速度大小再次应用滤波（可选）
        if (m_EnableMagnitudeFilter && m_MagnitudeFilter != null)
        {
            filteredMagnitude = m_MagnitudeFilter.Filter(filteredMagnitude);
        }

        // 保存滤波后的数据用于显示
        m_FilteredVelocity = filteredVelocity;
        m_FilteredMagnitude = filteredMagnitude;

        // 如果有滑块，获取当前倍率值
        if (m_VelocitySlider != null)
        {
            m_VelocityMultiplier = m_VelocitySlider.value;
        }

        // 确保倍率不为负数或零
        m_VelocityMultiplier = Mathf.Max(0.1f, m_VelocityMultiplier);

        // 应用倍率到滤波后的速度
        velocityMagnitude = filteredMagnitude * m_VelocityMultiplier;

        // 将速度映射到1.0x-4.0x范围 (10-40)
        m_CurrentSpeed = Mathf.Clamp(velocityMagnitude, m_MinVelocityThreshold, m_MaxVelocityThreshold);
        float normalizedSpeed = Mathf.InverseLerp(m_MinVelocityThreshold, m_MaxVelocityThreshold, m_CurrentSpeed);

        // 将归一化的速度值(0-1)转换为整数值(10-40)
        int speedValue = Mathf.RoundToInt(Mathf.Lerp(MIN_SPEED_BYTE, MAX_SPEED_BYTE, normalizedSpeed));
        m_CurrentSpeedByte = (byte)speedValue;

        // 更新UI显示
        UpdateVelocityText(velocityMagnitude);
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
            Debug.Log("Starting periodic data transmission");
            m_DataSendCoroutine = StartCoroutine(SendDataPeriodically());
        }
        else
        {
            Debug.LogWarning("Scan not stopped yet, cannot send data");
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
                // 每次发送前更新速度
                UpdateFingerVelocityAndSpeed();

                if (m_Characteristic != null)
                {
                    byte[] data = GenerateData(counter);
                    SendDataToESP32(data);
                    counter++;
                }
                else
                {
                    Debug.LogWarning("BLE characteristic not available, waiting for next attempt");
                    m_ConsecutiveFailures++;
                }
            }
            catch (System.Exception e)
            {
                // 捕获所有异常，确保协程不会因任何错误而中断
                Debug.LogWarning($"Error occurred during transmission cycle, but continuing: {e.Message}");
                m_ConsecutiveFailures++;
            }

            yield return new WaitForSeconds(m_SendInterval);
        }
        
        Debug.Log("Data transmission stopped due to connection loss or state change");
    }

    /// <summary>
    /// 生成要发送的数据
    /// </summary>
    private byte[] GenerateData(int counter)
    {
        byte[] data = new byte[32];

        // 起始标记
        data[0] = 0xFE;

        // 应用倍率影响到最终速度值
        float basePlaybackRate = m_CurrentSpeedByte / 10f; // 原始映射速率（1.0x-4.0x）
        float adjustedRate = basePlaybackRate;

        // 如果有滑块，直接使用滑块的值作为倍率
        if (m_VelocitySlider != null)
        {
            m_VelocityMultiplier = m_VelocitySlider.value;
        }

        // 应用倍率
        adjustedRate = adjustedRate * m_VelocityMultiplier;

        // 确保在有效范围内
        adjustedRate = Mathf.Clamp(adjustedRate, 1.0f, 4.0f);

        // 转换回字节值
        byte finalSpeedByte = (byte)Mathf.RoundToInt(adjustedRate * 10);

        // 生成10个通道的数据
        for (int channel = 0; channel < 10; channel++)
        {
            int offset = 1 + channel * 3;

            // 文件索引：循环使用0x01-0x0A
            // data[offset] = (byte)((counter + channel) % 10 + 1);
            data[offset] = (byte)(1);

            // 音量：根据原始速度决定是否静音
            data[offset + 1] = m_CurrentVolume;

            // 速度：使用应用了倍率后的速度值
            data[offset + 2] = finalSpeedByte;
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
            Debug.LogWarning("Characteristic not ready or connection lost");
            m_ConsecutiveFailures++;
            
            // 如果连续失败次数超过阈值，触发重连
            if (m_ConsecutiveFailures >= m_FailureThreshold && !m_ConnectionLost)
            {
                m_ConnectionLost = true;
                m_IsConnectedAndReady = false;
                
                if (m_AutoReconnect && !m_IsReconnecting)
                {
                    Debug.Log($"Connection appears to be lost after {m_ConsecutiveFailures} consecutive failures");
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

            if (m_ShowDebugInfo)
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
            Debug.LogWarning($"Failed to send data: {e.Message}");
            m_ConsecutiveFailures++;
            
            // 如果连续失败次数超过阈值，触发重连
            if (m_ConsecutiveFailures >= m_FailureThreshold && !m_ConnectionLost)
            {
                m_ConnectionLost = true;
                m_IsConnectedAndReady = false;
                
                if (m_AutoReconnect && !m_IsReconnecting)
                {
                    Debug.Log($"Connection appears to be lost after {m_ConsecutiveFailures} consecutive failures");
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
    /// 手动发送单次数据（可以通过UI按钮调用）
    /// </summary>
    public void SendSingleData()
    {
        try
        {
            if (m_IsConnectedAndReady && m_Characteristic != null && m_IsScanStopped && !m_ConnectionLost)
            {
                UpdateFingerVelocityAndSpeed(); // 更新当前速度
                byte[] data = GenerateData(0);
                SendDataToESP32(data);
            }
            else
            {
                if (!m_IsScanStopped)
                {
                    Debug.LogWarning("Scan not stopped yet, cannot send data");
                }
                else if (m_ConnectionLost)
                {
                    Debug.LogWarning("Connection lost, cannot send data");
                }
                else
                {
                    Debug.LogWarning("BLE not connected or characteristic not ready");
                }
            }
        }
        catch (System.Exception e)
        {
            // 捕获任何异常，确保不影响调用方
            Debug.LogWarning($"Error occurred while sending single data, but continuing: {e.Message}");
            m_ConsecutiveFailures++;
        }
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
                m_VelocityText.text = $"Ori V: {m_RawMagnitude:F3} m/s\nFiltered V: {m_FilteredMagnitude:F3} m/s {filterInfo}\nPlay V: {m_CurrentSpeedByte / 10f:F1}x\nFactor: {(m_VelocitySlider != null ? m_VelocitySlider.value : m_VelocityMultiplier):F1}\n{volumeStatus}";

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
                m_ConnectionStatusText.text = status;
                
                // 根据状态设置颜色
                if (status.Contains("Connected and Ready"))
                {
                    m_ConnectionStatusText.color = Color.green;
                }
                else if (status.Contains("正在连接") || status.Contains("正在扫描") || status.Contains("正在尝试重连"))
                {
                    m_ConnectionStatusText.color = Color.yellow;
                }
                else if (status.Contains("连接断开") || status.Contains("失败") || status.Contains("错误"))
                {
                    m_ConnectionStatusText.color = Color.red;
                }
                else
                {
                    m_ConnectionStatusText.color = Color.white;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Error updating connection status UI: {e.Message}");
            }
        }
    }
    
    /// <summary>
    /// 设置速度倍率滑块
    /// </summary>
    /// <param name="slider">用于控制速度倍率的滑块</param>
    public void SetVelocitySlider(UnityEngine.UI.Slider slider)
    {
        // 移除之前的监听器（如果有）
        if (m_VelocitySlider != null)
        {
            m_VelocitySlider.onValueChanged.RemoveListener(OnVelocitySliderChanged);
        }

        // 设置新的滑块并添加监听器
        m_VelocitySlider = slider;

        if (m_VelocitySlider != null)
        {
            // 设置滑块初始值为当前倍率
            m_VelocitySlider.value = m_VelocityMultiplier;

            // 添加值变化监听器
            m_VelocitySlider.onValueChanged.AddListener(OnVelocitySliderChanged);

            if (m_ShowDebugInfo)
            {
                Debug.Log($"Velocity multiplier slider set, current value: {m_VelocitySlider.value:F1}");
            }
        }
    }

    /// <summary>
    /// 设置滤波强度滑块
    /// </summary>
    /// <param name="slider">用于控制滤波强度的滑块</param>
    public void SetFilterStrengthSlider(UnityEngine.UI.Slider slider)
    {
        // 移除之前的监听器（如果有）
        if (m_FilterStrengthSlider != null)
        {
            m_FilterStrengthSlider.onValueChanged.RemoveListener(OnFilterStrengthSliderChanged);
        }

        // 设置新的滑块并添加监听器
        m_FilterStrengthSlider = slider;

        if (m_FilterStrengthSlider != null)
        {
            // 设置滑块初始值为当前滤波强度
            m_FilterStrengthSlider.value = m_VelocityFilterStrength;

            // 添加值变化监听器
            m_FilterStrengthSlider.onValueChanged.AddListener(OnFilterStrengthSliderChanged);

            if (m_ShowDebugInfo)
            {
                Debug.Log($"Filter strength slider set, current value: {m_FilterStrengthSlider.value:F2}");
            }
        }
    }

    /// <summary>
    /// 获取当前实际播放速率（已应用倍率）
    /// </summary>
    /// <returns>实际播放速率(1.0x-4.0x)</returns>
    public float GetActualPlaybackRate()
    {
        float basePlaybackRate = m_CurrentSpeedByte / 10f; // 原始映射速率（1.0x-4.0x）
        return Mathf.Clamp(basePlaybackRate * m_VelocityMultiplier, 1.0f, 4.0f); // 应用倍率后的实际速率
    }

    /// <summary>
    /// 响应速度倍率滑块值变化
    /// </summary>
    /// <param name="value">滑块的值</param>
    private void OnVelocitySliderChanged(float value)
    {
        SetVelocityMultiplier(value);
    }

    /// <summary>
    /// 响应滤波强度滑块值变化
    /// </summary>
    /// <param name="value">滑块的值</param>
    private void OnFilterStrengthSliderChanged(float value)
    {
        SetFilterStrength(value);
    }

    /// <summary>
    /// 设置速度倍率（用于注册到Slider的valueChanged事件）
    /// </summary>
    /// <param name="multiplier">速度倍率，建议范围0.1-5.0</param>
    public void SetVelocityMultiplier(float multiplier)
    {
        // 确保倍率在合理范围内
        m_VelocityMultiplier = Mathf.Clamp(multiplier, 0.1f, 10.0f);

        // 立即更新速度显示
        UpdateFingerVelocityAndSpeed();

        // 计算实际会传输的速度值
        float basePlaybackRate = m_CurrentSpeedByte / 10f; // 原始映射速率（1.0x-4.0x）
        float actualPlaybackRate = Mathf.Clamp(basePlaybackRate * m_VelocityMultiplier, 1.0f, 4.0f); // 应用倍率后的实际速率
        byte finalSpeedByte = (byte)Mathf.RoundToInt(actualPlaybackRate * 10); // 最终传输的字节值

        if (m_ShowDebugInfo)
        {
            Debug.Log($"Velocity multiplier set to: {m_VelocityMultiplier:F1}, Original speed: {basePlaybackRate:F1}x, Actual transmission speed: {actualPlaybackRate:F1}x (Value: {finalSpeedByte})");
        }

        // 如果正在发送数据，可以考虑立即发送一次最新速度的数据
        if (m_DataSendCoroutine != null && m_IsConnectedAndReady && m_Characteristic != null && !m_ConnectionLost)
        {
            try
            {
                byte[] data = GenerateData(0);
                SendDataToESP32(data);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Error occurred while sending data after multiplier change: {e.Message}");
                m_ConsecutiveFailures++;
            }
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

        if (m_ShowDebugInfo)
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
        
        if (m_ShowDebugInfo)
        {
            Debug.Log($"Volume threshold set to: {m_VolumeThreshold:F3} m/s");
        }
    }

    /// <summary>
    /// 设置正常播放音量
    /// </summary>
    /// <param name="volume">音量值（0-100）</param>
    public void SetNormalVolume(byte volume)
    {
        m_NormalVolume = (byte)Mathf.Clamp(volume, 0, 100);
        
        if (m_ShowDebugInfo)
        {
            Debug.Log($"Normal playback volume set to: {m_NormalVolume}%");
        }
    }

    /// <summary>
    /// 获取当前音量状态
    /// </summary>
    /// <returns>当前音量值</returns>
    public byte GetCurrentVolume()
    {
        return m_CurrentVolume;
    }

    /// <summary>
    /// 是否处于静音状态
    /// </summary>
    /// <returns>true表示静音，false表示正常播放</returns>
    public bool IsMuted()
    {
        return m_CurrentVolume == 0;
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

        if (m_ShowDebugInfo)
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
        m_EnableMagnitudeFilter = enable;

        if (!enable && m_MagnitudeFilter != null)
        {
            m_MagnitudeFilter.Reset();
        }

        if (m_ShowDebugInfo)
        {
            Debug.Log($"Magnitude filter {(enable ? "enabled" : "disabled")}");
        }
    }

    /// <summary>
    /// 重置所有滤波器
    /// </summary>
    public void ResetFilters()
    {
        if (m_VelocityFilter != null)
        {
            m_VelocityFilter.Reset();
        }

        if (m_MagnitudeFilter != null)
        {
            m_MagnitudeFilter.Reset();
        }

        if (m_ShowDebugInfo)
        {
            Debug.Log("All filters reset");
        }
    }

    /// <summary>
    /// 获取滤波器状态信息
    /// </summary>
    /// <returns>滤波器状态字符串</returns>
    public string GetFilterStatus()
    {
        string volumeStatus = m_CurrentVolume == 0 ? "Muted" : $"Volume {m_CurrentVolume}%";
        return $"Velocity filter: {(m_EnableVelocityFilter ? "Enabled" : "Disabled")} (Strength: {m_VelocityFilterStrength:F2})\n" +
               $"Magnitude filter: {(m_EnableMagnitudeFilter ? "Enabled" : "Disabled")} (Strength: {m_MagnitudeFilterStrength:F2})\n" +
               $"Raw velocity: {m_RawMagnitude:F3} m/s\n" +
               $"Filtered velocity: {m_FilteredMagnitude:F3} m/s\n" +
               $"Volume threshold: {m_VolumeThreshold:F3} m/s\n" +
               $"Current status: {volumeStatus}";
    }

    /// <summary>
    /// 切换数据发送状态
    /// </summary>
    public void ToggleDataTransmission()
    {
        if (m_DataSendCoroutine != null)
        {
            StopDataTransmission();
            Debug.Log("Data transmission stopped");
        }
        else if (m_IsConnectedAndReady && m_IsScanStopped && !m_ConnectionLost)
        {
            StartDataTransmission();
            Debug.Log("Data transmission started");
        }
        else if (!m_IsScanStopped)
        {
            Debug.LogWarning("Scan not stopped yet, cannot send data");
        }
        else if (m_ConnectionLost)
        {
            Debug.LogWarning("Connection lost, cannot start transmission");
            
            // 如果Connection Lost但未在重连，可以尝试重连
            if (m_AutoReconnect && !m_IsReconnecting)
            {
                Debug.Log("Attempting to reconnect before starting transmission");
                StartReconnectProcess();
            }
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

    /// <summary>
    /// 设置自动重连
    /// </summary>
    public void SetAutoReconnect(bool enable)
    {
        m_AutoReconnect = enable;
        Debug.Log($"Auto reconnect {(enable ? "enabled" : "disabled")}");
    }

    /// <summary>
    /// 获取连接状态信息
    /// </summary>
    public string GetConnectionStatus()
    {
        if (m_IsConnectedAndReady && !m_ConnectionLost)
        {
            return $"Connected: {(m_ConnectedPeripheral != null ? m_ConnectedPeripheral.name : "Unknown")}";
        }
        else if (m_IsReconnecting)
        {
            return $"Reconnecting (Attempt {m_ReconnectAttempts})...";
        }
        else if (m_IsConnecting)
        {
            return "Connecting...";
        }
        else if (!m_IsScanStopped)
        {
            return "Scanning Devices...";
        }
        else if (m_ConnectionLost)
        {
            return "Connection Lost";
        }
        else
        {
            return "Not Connected";
        }
    }
    
    /// <summary>
    /// 设置连续失败阈值
    /// </summary>
    public void SetFailureThreshold(int threshold)
    {
        m_FailureThreshold = Mathf.Max(1, threshold);
        Debug.Log($"Failure threshold set to: {m_FailureThreshold}");
    }
}
