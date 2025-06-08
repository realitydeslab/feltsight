using UnityEngine;
using UnityCoreBluetooth;
using System.Collections;
using TMPro;
using UnityEngine.UI;
using UnityEngine.XR.Hands;

/// <summary>
/// 使用食指尖速度来控制BLE发送的速度参数
/// 速度0~0.3 m/s线性映射到1.0x~4.0x速度(10-40)
/// </summary>
public class BLESendJointV : MonoBehaviour
{
    [SerializeField] [Tooltip("要使用的手部追踪组件")]
    private MyHand m_HandTracker;

    [SerializeField] [Tooltip("要使用的手（左手或右手）")]
    private Handedness m_HandToUse = Handedness.Right;

    [SerializeField] [Tooltip("用于显示手指速度的文本组件")]
    private TextMeshProUGUI m_VelocityText;

    [SerializeField] [Tooltip("用于调整速度倍率的Slider")]
    private UnityEngine.UI.Slider m_VelocitySlider;

    [SerializeField] [Tooltip("发送数据的间隔时间（秒）")]
    private float m_SendInterval = 0.5f;

    [SerializeField] [Tooltip("速度映射的最小阈值（米/秒）")]
    private float m_MinVelocityThreshold = 0.0f;

    [SerializeField] [Tooltip("速度映射的最大阈值（米/秒）")]
    private float m_MaxVelocityThreshold = 0.3f;

    [SerializeField] [Tooltip("是否在控制台显示调试信息")]
    private bool m_ShowDebugInfo = true;

    [SerializeField] [Tooltip("在编辑器中模拟速度值（仅供测试）")]
    private float m_SimulatedVelocity = 0.15f;

    private CoreBluetoothManager m_Manager;
    private CoreBluetoothCharacteristic m_Characteristic;
    private bool m_IsConnectedAndReady = false;
    private bool m_IsScanStopped = false;
    private Coroutine m_DataSendCoroutine;
    private float m_CurrentSpeed = 0f;
    private byte m_CurrentSpeedByte = 10; // 默认值1.0x速度
    private float m_VelocityMultiplier = 1.0f; // 速度倍率

    // 最小速度和最大速度的字节值
    private const byte MIN_SPEED_BYTE = 10; // 1.0x速度
    private const byte MAX_SPEED_BYTE = 40; // 4.0x速度

    void Start()
    {
        // 确保有手部追踪组件
        if (m_HandTracker == null)
        {
            m_HandTracker = FindObjectOfType<MyHand>();
            if (m_HandTracker == null)
            {
                Debug.LogError("未找到MyHand组件，请手动分配");
                enabled = false;
                return;
            }
        }

        // 注册Slider事件（如果已分配）
        if (m_VelocitySlider != null)
        {
            m_VelocitySlider.onValueChanged.AddListener(OnVelocitySliderChanged);
        }

        // 初始化BLE
        InitializeBLE();
    }

    void Update()
    {
        // 更新食指尖速度并映射到速度值
        UpdateFingerVelocityAndSpeed();
    }

    void OnDestroy()
    {
        // 移除Slider监听器
        if (m_VelocitySlider != null)
        {
            m_VelocitySlider.onValueChanged.RemoveListener(OnVelocitySliderChanged);
        }

        StopDataTransmission();
        if (m_Manager != null)
        {
            m_Manager.Stop();
        }
    }

    /// <summary>
    /// 初始化蓝牙连接
    /// </summary>
    private void InitializeBLE()
    {
        Debug.Log("BLESendJointV 开始初始化BLE连接");
        try
        {
            m_Manager = CoreBluetoothManager.Shared;

        m_Manager.OnUpdateState((string state) =>
        {
            Debug.Log("BLE状态: " + state);
            if (state != "poweredOn") return;
            m_Manager.StartScan();
        });

        m_Manager.OnDiscoverPeripheral((CoreBluetoothPeripheral peripheral) =>
        {
            if (peripheral.name != "" && peripheral.name != null && peripheral.name != "(null-name)" && peripheral.name != "null-name")
            {
                Debug.Log("发现设备: " + peripheral.name);
            }

            if (peripheral.name != "ESP32-BLE" && peripheral.name != "FeltSight BLE") return;

            m_Manager.StopScan();
            m_IsScanStopped = true;
            Debug.Log("已停止扫描，准备连接设备");
            m_Manager.ConnectToPeripheral(peripheral);
        });

        m_Manager.OnConnectPeripheral((CoreBluetoothPeripheral peripheral) =>
        {
            Debug.Log("已连接设备: " + peripheral.name);
            peripheral.discoverServices();
        });

        m_Manager.OnDiscoverService((CoreBluetoothService service) =>
        {
            Debug.Log("发现服务UUID: " + service.uuid);
            // ESP32服务UUID
            if (service.uuid.ToUpper() != "6E400001-B5A3-F393-E0A9-E50E24DCCA9E") return;
            service.discoverCharacteristics();
        });

        m_Manager.OnDiscoverCharacteristic((CoreBluetoothCharacteristic characteristic) =>
        {
            string uuid = characteristic.Uuid.ToUpper();
            string[] usage = characteristic.Propertis;
            Debug.Log("发现特征UUID: " + uuid + ", 用途: " + string.Join(",", usage));

            // 查找RX特征（用于写入数据到ESP32）
            if (uuid == "6E400002-B5A3-F393-E0A9-E50E24DCCA9E")
            {
                m_Characteristic = characteristic;
                Debug.Log("找到RX特征，可以发送数据");

                // 确保扫描已停止后才设置连接就绪状态
                m_IsConnectedAndReady = true;

                // 确保不会重复启动数据发送
                if (m_DataSendCoroutine == null)
                {
                    Debug.Log("扫描已停止，开始发送数据");
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
            Debug.LogError($"BLE初始化失败，但不影响主进程: {e.Message}");
        }
    }

    /// <summary>
    /// 更新食指尖速度并映射到速度值
    /// </summary>
    private void UpdateFingerVelocityAndSpeed()
    {
        float velocityMagnitude = 0f;

        #if UNITY_EDITOR
        // 在编辑器中使用模拟值进行测试
        velocityMagnitude = m_SimulatedVelocity;
        #else
        // 获取食指尖速度
        if (m_HandTracker.TryGetJointPositionAndVelocity(
            m_HandToUse, XRHandJointID.IndexTip, out Vector3 position, out Vector3 velocity))
        {
            // 如果有滑块，获取当前倍率值
            if (m_VelocitySlider != null)
            {
                m_VelocityMultiplier = m_VelocitySlider.value;
            }

            // 应用倍率到速度
            velocityMagnitude = velocity.magnitude * m_VelocityMultiplier;

            // 确保倍率不为负数或零
            m_VelocityMultiplier = Mathf.Max(0.1f, m_VelocityMultiplier);
        }
        #endif

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
            Debug.Log("开始周期性发送数据");
            m_DataSendCoroutine = StartCoroutine(SendDataPeriodically());
        }
        else
        {
            Debug.LogWarning("扫描尚未停止，不能发送数据");
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

        while (m_IsConnectedAndReady)
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
                    Debug.LogWarning("BLE特征不可用，等待下一次尝试");
                }
            }
            catch (System.Exception e)
            {
                // 捕获所有异常，确保协程不会因任何错误而中断
                Debug.LogWarning($"发送周期中发生错误，但继续运行: {e.Message}");
            }

            yield return new WaitForSeconds(m_SendInterval);
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

            // 音量：使用固定值75%
            data[offset + 1] = 75;

            // 速度：使用应用了倍率后的速度值
            data[offset + 2] = finalSpeedByte;
        }

        // 结束标记
        data[31] = 0xFF;

        return data;
    }

    /// <summary>
    /// 发送数据到ESP32
    /// </summary>
    private void SendDataToESP32(byte[] data)
    {
        if (m_Characteristic == null || !m_IsConnectedAndReady)
        {
            Debug.LogWarning("特征未准备好或连接已断开");
            return;
        }

        try
        {
            m_Characteristic.Write(data);

            if (m_ShowDebugInfo)
            {
                // 打印发送的数据用于调试
                string hexString = System.BitConverter.ToString(data).Replace("-", " ");
                Debug.Log($"发送数据: {hexString}");

                // 打印解析后的数据
                LogDataContent(data);

                // 打印当前食指速度和映射值，与发送同步
                Debug.Log($"食指速度: {m_CurrentSpeed:F3} m/s, 映射速度: {m_CurrentSpeedByte / 10f:F1}x, 值: {m_CurrentSpeedByte}");
            }
        }
        catch (System.Exception e)
        {
            // 只记录错误但不抛出异常，确保不影响主进程
            Debug.LogWarning($"发送数据失败，但继续运行: {e.Message}");
        }
    }

    /// <summary>
    /// 打印数据内容（用于调试）
    /// </summary>
    private void LogDataContent(byte[] data)
    {
        if (data.Length != 32 || data[0] != 0xFE || data[31] != 0xFF)
        {
            Debug.LogError("数据格式错误");
            return;
        }

        Debug.Log("=== 数据内容 ===");
        for (int i = 0; i < 10; i++)
        {
            int offset = 1 + i * 3;
            byte fileIndex = data[offset];
            byte volume = data[offset + 1];
            byte speed = data[offset + 2];

            Debug.Log($"通道 {i + 1}: 文件={fileIndex}, 音量={volume}%, 速度={speed / 10f:F1}x");
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
            if (m_IsConnectedAndReady && m_Characteristic != null && m_IsScanStopped)
            {
                UpdateFingerVelocityAndSpeed(); // 更新当前速度
                byte[] data = GenerateData(0);
                SendDataToESP32(data);
            }
            else
            {
                if (!m_IsScanStopped)
                {
                    Debug.LogWarning("扫描尚未停止，不能发送数据");
                }
                else
                {
                    Debug.LogWarning("BLE未连接或特征未准备好");
                }
            }
        }
        catch (System.Exception e)
        {
            // 捕获任何异常，确保不影响调用方
            Debug.LogWarning($"发送单次数据时发生错误，但继续运行: {e.Message}");
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
                // 显示原始速度和映射后的速度
                m_VelocityText.text = $"V: {velocityMagnitude:F3} m/s Play V: {m_CurrentSpeedByte / 10f:F1}x\nFactor is: {m_VelocitySlider.GetComponent<Slider>().value:F1}";

                // 根据速度变化颜色
                float normalizedSpeed = Mathf.InverseLerp(m_MinVelocityThreshold, m_MaxVelocityThreshold, velocityMagnitude);
                m_VelocityText.color = Color.Lerp(Color.green, Color.red, normalizedSpeed);
            }
            catch (System.Exception e)
            {
                // 防止UI更新异常影响主流程
                Debug.LogWarning($"更新速度UI时发生错误: {e.Message}");
            }
        }
    }

    /// <summary>
    /// 设置用于显示速度的文本组件
    /// </summary>
    /// <param name="text">TextMeshProUGUI组件</param>
    public void SetVelocityText(TextMeshProUGUI text)
    {
        m_VelocityText = text;
        if (m_VelocityText != null)
        {
            m_VelocityText.text = "V: 0.000 m/s Play V: 1.0x";
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
                Debug.Log($"已设置速度倍率滑块，当前值: {m_VelocitySlider.value:F1}");
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

        if (m_VelocityText != null)
        {
            // 更新倍率显示
            m_VelocityText.text = $"V: {m_CurrentSpeed:F3} m/s (x{m_VelocityMultiplier:F1}) Play V: {actualPlaybackRate:F1}x\nFactor is: {m_VelocitySlider.GetComponent<Slider>().value:F1}";
        }

        if (m_ShowDebugInfo)
        {
            Debug.Log($"速度倍率已设置为: {m_VelocityMultiplier:F1}，原始速度:{basePlaybackRate:F1}x，实际传输速度:{actualPlaybackRate:F1}x (值:{finalSpeedByte})");
        }

        // 如果正在发送数据，可以考虑立即发送一次最新速度的数据
        if (m_DataSendCoroutine != null && m_IsConnectedAndReady && m_Characteristic != null)
        {
            try
            {
                byte[] data = GenerateData(0);
                SendDataToESP32(data);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"倍率更改后发送数据时发生错误: {e.Message}");
            }
        }
    }

    /// <summary>
    /// 切换数据发送状态
    /// </summary>
    public void ToggleDataTransmission()
    {
        if (m_DataSendCoroutine != null)
        {
            StopDataTransmission();
            Debug.Log("停止发送数据");
        }
        else if (m_IsConnectedAndReady && m_IsScanStopped)
        {
            StartDataTransmission();
            Debug.Log("开始发送数据");
        }
        else if (!m_IsScanStopped)
        {
            Debug.LogWarning("扫描尚未停止，不能发送数据");
        }
    }
}
