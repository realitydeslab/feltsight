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

    private float lastUpdateTime = 0f;
    private bool isRequesting = false;

    // API路径常量
    private const string VELOCITY_RATIO_PATH = "/velocity"; // 速度倍率API路径
    private const string NORMAL_VOLUME_PATH = "/volume"; // 音量API路径
    private const string VOLUME_THRESHOLD_PATH = "/threshold"; // 音量阈值API路径
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // 初始请求一次
        StartCoroutine(GetVelocityRatioFromServer());
        StartCoroutine(GetNormalVolumeFromServer());
        StartCoroutine(GetVolumeThresholdFromServer());
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
    /// 获取当前正常音量
    /// </summary>
    /// <param name="defaultVolume">如果没有从服务器获取到音量，则使用此默认值</param>
    /// <returns>当前音量值(0-100)</returns>
    public byte GetCurrentNormalVolume(byte defaultVolume = 75)
    {
        // 如果NormalVolume已被初始化且不为默认值，则返回它
        // 否则返回传入的默认值
        return NormalVolume;
    }
    
    /// <summary>
    /// 获取当前音量阈值
    /// </summary>
    /// <param name="defaultThreshold">如果没有从服务器获取到阈值，则使用此默认值</param>
    /// <returns>当前音量阈值(米/秒)</returns>
    public float GetCurrentVolumeThreshold(float defaultThreshold = 0.015f)
    {
        // 返回从服务器获取的阈值
        return VolumeThreshold;
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
}