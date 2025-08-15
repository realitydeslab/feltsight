using UnityEngine;
using System.Collections;
using UnityEngine.Networking;

public class ReadSthFromServer : MonoBehaviour
{
    [Header("API Settings")]
    public string baseUrl = "https://httpbin.org"; // API的基础URL
    public float updateInterval = 5.0f; // 更新间隔（秒）
    
    [Header("从服务器上获取的动态值")]
    public float VelocityRatio = 1.0f; // 保存从API获取的float值
    public byte NormalVolume = 75; // 保存从API获取的音量值(0-100)
    public float VolumeThreshold = 0.015f; // 音量静音的速度阈值（米/秒）
    public float MaxVelocityThreshold = 0.3f; // 速度映射的最大阈值（米/秒）
    public bool EnableMagnitudeFilter = true; // 是否启用速度大小滤波
    public bool EnableVelocityFilter = true; // 是否启用速度滤波
    public float VelocityFilterStrength = 0.1f; // 速度滤波强度 (0.01-1.0)，值越小滤波效果越强
    public float MagnitudeFilterStrength = 0.15f; // 速度大小滤波强度 (0.01-1.0)，值越小滤波效果越强
    
    [Header("控制器配置")]
    [Tooltip("要控制的BLESendJointV组件")]
    public BLESendJointV bleController;

    private float lastUpdateTime = 0f;
    private bool isRequesting = false;

    // API路径常量
    private const string VELOCITY_RATIO_PATH = "/velocity"; // 速度倍率API路径
    private const string NORMAL_VOLUME_PATH = "/volume"; // 音量API路径
    private const string VOLUME_THRESHOLD_PATH = "/threshold"; // 音量阈值API路径
    private const string MAX_VELOCITY_THRESHOLD_PATH = "/max-threshold"; // 速度映射最大阈值API路径
    private const string MAGNITUDE_FILTER_PATH = "/filter"; // 速度大小滤波开关API路径
    private const string VELOCITY_FILTER_PATH = "/vfilter"; // 速度滤波开关API路径
    private const string VELOCITY_FILTER_STRENGTH_PATH = "/vfilter-strength"; // 速度滤波强度API路径
    private const string MAGNITUDE_FILTER_STRENGTH_PATH = "/filter-strength"; // 速度大小滤波强度API路径
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // 初始请求一次
        StartCoroutine(GetVelocityRatioFromServer());
        StartCoroutine(GetNormalVolumeFromServer());
        StartCoroutine(GetVolumeThresholdFromServer());
        StartCoroutine(GetMaxVelocityThresholdFromServer());
        StartCoroutine(GetMagnitudeFilterFromServer());
        StartCoroutine(GetVelocityFilterFromServer());
        StartCoroutine(GetVelocityFilterStrengthFromServer());
        StartCoroutine(GetMagnitudeFilterStrengthFromServer());
    }

    // Update is called once per frame
    void Update()
    {
        // 检查是否到了更新时间且当前没有正在发送的请求
        if (Time.time - lastUpdateTime >= updateInterval && !isRequesting)
        {
            StartCoroutine(GetVelocityRatioFromServer());
            StartCoroutine(GetNormalVolumeFromServer());
            StartCoroutine(GetVolumeThresholdFromServer());
            StartCoroutine(GetMaxVelocityThresholdFromServer());
            StartCoroutine(GetMagnitudeFilterFromServer());
            StartCoroutine(GetVelocityFilterFromServer());
            StartCoroutine(GetVelocityFilterStrengthFromServer());
            StartCoroutine(GetMagnitudeFilterStrengthFromServer());
        }
    }

    IEnumerator GetVelocityRatioFromServer()
    {
        isRequesting = true;
        string fullUrl = baseUrl + VELOCITY_RATIO_PATH;
        
        using (UnityWebRequest www = UnityWebRequest.Get(fullUrl))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                string text = www.downloadHandler.text;
                // Debug.Log("从服务器获取的速度倍率文本内容:\n" + text);
                
                // 尝试解析返回的文本为float
                if (float.TryParse(text, out float result))
                {
                    VelocityRatio = result;
                    // Debug.Log("成功获取VelocityRatio值: " + VelocityRatio);
                    
                    // 直接将速度倍率应用到BLE控制器
                    if (bleController != null)
                    {
                        bleController.SetVelocityMultiplier(VelocityRatio);
                        Debug.Log($"已将速度倍率{VelocityRatio}直接应用到BLE控制器");
                    }
                }
                else
                {
                    // Debug.LogWarning("无法将返回的文本解析为float: " + text);
                }
            }
            else
            {
                // Debug.LogError("请求速度倍率失败: " + www.error);
            }
            
                    // 更新最后请求时间
            lastUpdateTime = Time.time;
            isRequesting = false;
        }
    }

    
    /// <summary>
    /// 从服务器获取正常播放音量值
    /// </summary>
    IEnumerator GetNormalVolumeFromServer()
    {
        // 使用一个单独的标志跟踪NormalVolume请求
        bool isVolumeRequesting = true;
        string fullUrl = baseUrl + NORMAL_VOLUME_PATH;
        
        using (UnityWebRequest www = UnityWebRequest.Get(fullUrl))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                string text = www.downloadHandler.text;
                // Debug.Log("从服务器获取的音量文本内容:\n" + text);
                
                // 尝试解析返回的文本为整数
                if (int.TryParse(text, out int result))
                {
                    // 确保值在0-100范围内
                    result = Mathf.Clamp(result, 0, 100);
                    NormalVolume = (byte)result;
                    // Debug.Log("成功获取NormalVolume值: " + NormalVolume);
                    
                    // 直接调用BLESendJointV组件
                    // 注意：BLESendJointV没有设置音量的直接API，但它会从这个组件中获取音量值
                }
                else
                {
                    // Debug.LogWarning("无法将返回的文本解析为整数: " + text);
                }
            }
            else
            {
                // Debug.LogError("请求音量失败: " + www.error);
            }
            
            isVolumeRequesting = false;
        }
    }
    
    /// <summary>
    /// 从服务器获取音量静音阈值(米/秒)
    /// </summary>
    IEnumerator GetVolumeThresholdFromServer()
    {
        // 使用一个单独的标志跟踪阈值请求
        bool isThresholdRequesting = true;
        string fullUrl = baseUrl + VOLUME_THRESHOLD_PATH;
        
        using (UnityWebRequest www = UnityWebRequest.Get(fullUrl))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                string text = www.downloadHandler.text;
                // Debug.Log("从服务器获取的阈值文本内容:\n" + text);
                
                // 尝试解析返回的文本为浮点数
                if (float.TryParse(text, out float result))
                {
                    // 确保值在合理范围内 (0-1)
                    result = Mathf.Clamp(result, 0f, 1f);
                    VolumeThreshold = result;
                    // Debug.Log("成功获取VolumeThreshold值: " + VolumeThreshold);
                    
                    // 直接将阈值应用到BLE控制器
                    if (bleController != null)
                    {
                        bleController.SetVolumeThreshold(VolumeThreshold);
                        // Debug.Log($"已将音量阈值{VolumeThreshold:F3}直接应用到BLE控制器");
                    }
                }
                else
                {
                    // Debug.LogWarning("无法将返回的文本解析为浮点数: " + text);
                }
            }
            else
            {
                // Debug.LogError("请求音量阈值失败: " + www.error);
            }
            
            isThresholdRequesting = false;
        }
    }
    
    /// <summary>
    /// 从服务器获取速度大小滤波器开关状态
    /// </summary>
    IEnumerator GetMagnitudeFilterFromServer()
    {
        string fullUrl = baseUrl + MAGNITUDE_FILTER_PATH;
        
        using (UnityWebRequest www = UnityWebRequest.Get(fullUrl))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                string text = www.downloadHandler.text;
                // Debug.Log("从服务器获取的滤波器状态文本内容:\n" + text);
                
                // 解析返回的文本为布尔值 (1/0, true/false, on/off 等)
                bool filterEnabled = false;
                
                // 尝试解析为数字 (1/0)
                if (int.TryParse(text, out int result))
                {
                    filterEnabled = result != 0;
                }
                // 尝试解析为布尔文本
                else if (bool.TryParse(text, out bool boolResult))
                {
                    filterEnabled = boolResult;
                }
                // 尝试解析为文本形式 (on/off, yes/no)
                else
                {
                    string lowerText = text.ToLower().Trim();
                    filterEnabled = lowerText == "on" || lowerText == "yes" || lowerText == "true" || lowerText == "1";
                }
                
                EnableMagnitudeFilter = filterEnabled;
                // Debug.Log("成功获取速度大小滤波器状态: " + (EnableMagnitudeFilter ? "开启" : "关闭"));
                
                // 直接将滤波器状态应用到BLE控制器
                if (bleController != null)
                {
                    bleController.SetMagnitudeFilterEnabled(EnableMagnitudeFilter);
                    // Debug.Log($"已将速度大小滤波器状态{(EnableMagnitudeFilter ? "开启" : "关闭")}直接应用到BLE控制器");
                }
            }
            else
            {
                // Debug.LogError("请求速度大小滤波器状态失败: " + www.error);
            }
            
        }
    }
    
    /// <summary>
    /// 从服务器获取速度滤波器开关状态
    /// </summary>
    IEnumerator GetVelocityFilterFromServer()
    {
        string fullUrl = baseUrl + VELOCITY_FILTER_PATH;
        
        using (UnityWebRequest www = UnityWebRequest.Get(fullUrl))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                string text = www.downloadHandler.text;
                // Debug.Log("从服务器获取的速度滤波器状态文本内容:\n" + text);
                
                // 解析返回的文本为布尔值 (1/0, true/false, on/off 等)
                bool filterEnabled = false;
                
                // 尝试解析为数字 (1/0)
                if (int.TryParse(text, out int result))
                {
                    filterEnabled = result != 0;
                }
                // 尝试解析为布尔文本
                else if (bool.TryParse(text, out bool boolResult))
                {
                    filterEnabled = boolResult;
                }
                // 尝试解析为文本形式 (on/off, yes/no)
                else
                {
                    string lowerText = text.ToLower().Trim();
                    filterEnabled = lowerText == "on" || lowerText == "yes" || lowerText == "true" || lowerText == "1";
                }
                
                EnableVelocityFilter = filterEnabled;
                // Debug.Log("成功获取速度滤波器状态: " + (EnableVelocityFilter ? "开启" : "关闭"));
                
                // 直接将滤波器状态应用到BLE控制器
                if (bleController != null)
                {
                    bleController.SetVelocityFilterEnabled(EnableVelocityFilter);
                    // Debug.Log($"已将速度滤波器状态{(EnableVelocityFilter ? "开启" : "关闭")}直接应用到BLE控制器");
                }
            }
            else
            {
                // Debug.LogError("请求速度滤波器状态失败: " + www.error);
            }
        }
    }
    
    /// <summary>
    /// 从服务器获取速度滤波强度
    /// </summary>
    IEnumerator GetVelocityFilterStrengthFromServer()
    {
        string fullUrl = baseUrl + VELOCITY_FILTER_STRENGTH_PATH;
        
        using (UnityWebRequest www = UnityWebRequest.Get(fullUrl))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                string text = www.downloadHandler.text;
                // Debug.Log("从服务器获取的速度滤波强度文本内容:\n" + text);
                
                // 尝试解析返回的文本为浮点数
                if (float.TryParse(text, out float result))
                {
                    // 确保值在合理范围内 (0.01-1.0)
                    result = Mathf.Clamp(result, 0.01f, 1.0f);
                    VelocityFilterStrength = result;
                    // Debug.Log("成功获取VelocityFilterStrength值: " + VelocityFilterStrength);
                    
                    // 如果同时获取了速度大小滤波强度，一起应用
                    if (bleController != null)
                    {
                        bleController.SetFilterStrength(VelocityFilterStrength);
                    }
                }
                else
                {
                    // Debug.LogWarning("无法将返回的文本解析为浮点数: " + text);
                }
            }
            else
            {
                // Debug.LogError("请求速度滤波强度失败: " + www.error);
            }
        }
    }
    
    /// <summary>
    /// 从服务器获取速度大小滤波强度
    /// </summary>
    IEnumerator GetMagnitudeFilterStrengthFromServer()
    {
        string fullUrl = baseUrl + MAGNITUDE_FILTER_STRENGTH_PATH;
        
        using (UnityWebRequest www = UnityWebRequest.Get(fullUrl))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                string text = www.downloadHandler.text;
                // Debug.Log("从服务器获取的速度大小滤波强度文本内容:\n" + text);
                
                // 尝试解析返回的文本为浮点数
                if (float.TryParse(text, out float result))
                {
                    // 确保值在合理范围内 (0.01-1.0)
                    result = Mathf.Clamp(result, 0.01f, 1.0f);
                    MagnitudeFilterStrength = result;
                    // Debug.Log("成功获取MagnitudeFilterStrength值: " + MagnitudeFilterStrength);
                    
                    // 如果同时获取了速度滤波强度，一起应用
                    if (bleController != null)
                    {
                        bleController.SetFilterStrength(VelocityFilterStrength);
                    }
                }
                else
                {
                    // Debug.LogWarning("无法将返回的文本解析为浮点数: " + text);
                }
            }
            else
            {
                // Debug.LogError("请求速度大小滤波强度失败: " + www.error);
            }
        }
    }
    
    /// <summary>
    /// 从服务器获取速度映射的最大阈值(米/秒)
    /// </summary>
    IEnumerator GetMaxVelocityThresholdFromServer()
    {
        string fullUrl = baseUrl + MAX_VELOCITY_THRESHOLD_PATH;
        
        using (UnityWebRequest www = UnityWebRequest.Get(fullUrl))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                string text = www.downloadHandler.text;
                // Debug.Log("从服务器获取的最大阈值文本内容:\n" + text);
                
                // 尝试解析返回的文本为浮点数
                if (float.TryParse(text, out float result))
                {
                    // 确保值为正数
                    result = Mathf.Max(0f, result);
                    MaxVelocityThreshold = result;
                    // Debug.Log("成功获取MaxVelocityThreshold值: " + MaxVelocityThreshold);
                    
                    // 直接将最大阈值应用到BLE控制器
                    if (bleController != null)
                    {
                        bleController.SetMaxVelocityThreshold(MaxVelocityThreshold);
                        Debug.Log($"已将速度映射最大阈值{MaxVelocityThreshold:F3}直接应用到BLE控制器");
                    }
                }
                else
                {
                    // Debug.LogWarning("无法将返回的文本解析为浮点数: " + text);
                }
            }
            else
            {
                // Debug.LogError("请求最大阈值失败: " + www.error);
            }
        }
    }
}