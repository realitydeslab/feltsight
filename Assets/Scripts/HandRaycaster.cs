using UnityEngine;
using UnityEngine.XR.Hands;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine.VFX;

/// <summary>
/// 基于 Unity.Mathematics 的 OneEuro 滤波器，用于 2D 向量
/// </summary>
sealed class OneEuroFilter2
{
    #region Public properties

    public float Beta { get; set; }
    public float MinCutoff { get; set; }

    #endregion

    #region Public step function

    public float2 Step(float t, float2 x)
    {
        var t_e = t - _prev.t;

        // Do nothing if the time difference is too small.
        if (t_e < 1e-5f) return _prev.x;

        var dx = (x - _prev.x) / t_e;
        var dx_res = math.lerp(_prev.dx, dx, Alpha(t_e, DCutOff));

        var cutoff = MinCutoff + Beta * math.length(dx_res);
        var x_res = math.lerp(_prev.x, x, Alpha(t_e, cutoff));

        _prev = (t, x_res, dx_res);

        return x_res;
    }

    #endregion

    #region Private class members

    const float DCutOff = 1.0f;

    static float Alpha(float t_e, float cutoff)
    {
        var r = 2 * math.PI * cutoff * t_e;
        return r / (r + 1);
    }

    #endregion

    #region Previous state variables as a tuple

    (float t, float2 x, float2 dx) _prev;

    #endregion
}

/// <summary>
/// 扩展 OneEuroFilter2 以支持 3D 向量
/// </summary>
public sealed class OneEuroFilter3D
{
    private OneEuroFilter2 _filterX;
    private OneEuroFilter2 _filterY;
    private OneEuroFilter2 _filterZ;

    /// <summary>
    /// 创建一个新的 OneEuroFilter3D 实例
    /// </summary>
    /// <param name="minCutoff">最小截止频率 (默认: 1.0)</param>
    /// <param name="beta">速度系数 (默认： 0.0)</param>
    public OneEuroFilter3D(float minCutoff = 1.0f, float beta = 0.0f)
    {
        _filterX = new OneEuroFilter2 { MinCutoff = minCutoff, Beta = beta };
        _filterY = new OneEuroFilter2 { MinCutoff = minCutoff, Beta = beta };
        _filterZ = new OneEuroFilter2 { MinCutoff = minCutoff, Beta = beta };
    }

    /// <summary>
    /// 应用滤波器到 Vector3
    /// </summary>
    /// <param name="time">当前时间</param>
    /// <param name="value">输入向量</param>
    /// <returns>滤波后的向量</returns>
    public Vector3 Step(float time, Vector3 value)
    {
        float2 xy = _filterX.Step(time, new float2(value.x, value.y));
        float2 zw = _filterY.Step(time, new float2(value.z, 0));
        
        return new Vector3(xy.x, xy.y, zw.x);
    }

    /// <summary>
    /// 更新滤波器参数
    /// </summary>
    public void UpdateParams(float minCutoff, float beta)
    {
        _filterX.MinCutoff = minCutoff;
        _filterX.Beta = beta;
        
        _filterY.MinCutoff = minCutoff;
        _filterY.Beta = beta;
        
        _filterZ.MinCutoff = minCutoff;
        _filterZ.Beta = beta;
    }
}

/// <summary>
/// 用于管理多个 OneEuroFilter3D 的容器类
/// </summary>
public class OneEuroFilter3DContainer
{
    private Dictionary<string, OneEuroFilter3D> _filters = new Dictionary<string, OneEuroFilter3D>();
    private float _minCutoff;
    private float _beta;

    /// <summary>
    /// 创建一个新的滤波器容器
    /// </summary>
    /// <param name="minCutoff">最小截止频率 (默认: 1.0)</param>
    /// <param name="beta">速度系数 (默认: 0.0)</param>
    public OneEuroFilter3DContainer(float minCutoff = 1.0f, float beta = 0.0f)
    {
        _minCutoff = minCutoff;
        _beta = beta;
    }

    /// <summary>
    /// 获取或创建指定键的滤波器
    /// </summary>
    public OneEuroFilter3D GetFilter(string key)
    {
        if (!_filters.TryGetValue(key, out OneEuroFilter3D filter))
        {
            filter = new OneEuroFilter3D(_minCutoff, _beta);
            _filters[key] = filter;
        }
        return filter;
    }

    /// <summary>
    /// 应用滤波器到 Vector3
    /// </summary>
    public Vector3 FilterVector3(string key, Vector3 value, float timestamp = -1.0f)
    {
        if (timestamp < 0)
            timestamp = Time.time;
            
        return GetFilter(key).Step(timestamp, value);
    }

    /// <summary>
    /// 更新所有滤波器的参数
    /// </summary>
    public void UpdateAllParams(float minCutoff, float beta)
    {
        _minCutoff = minCutoff;
        _beta = beta;

        foreach (var filter in _filters.Values)
        {
            filter.UpdateParams(minCutoff, beta);
        }
    }
}
public class HandRaycaster : MonoBehaviour
{
    [Header("射线设置")]
    [SerializeField] private float rayDistance = 1.0f;
    [SerializeField] private LayerMask raycastMask = -1;

    [Header("射线Visualize")]
    [SerializeField] LineRenderer[] lineRenderers; // From Left to Right, from damuzhi to xiaomuzhi
    [SerializeField] Camera cam;
    [SerializeField] private VisualEffect[] vfx;
    [SerializeField] private VFXMan vv;
    
    [Header("手部追踪")]
    [SerializeField] private MyHand handTracker;
    
    [Header("OneEuro 滤波器设置")]
    [SerializeField] private bool useFiltering = true;
    [SerializeField] private float minCutoff = 1.0f;
    [SerializeField] private float beta;

    [Header("Debug Settings")] [SerializeField]
    private bool isShowHitInfo;
    [SerializeField] private bool showDebugRays = true;
    [SerializeField] private float rayDuration = 0.1f;
    
    // OneEuro 滤波器容器
    private OneEuroFilter3DContainer filterContainer;
    
    // 手指关节ID定义
    private static readonly XRHandJointID[] TipJointIds = new[]
    {
        XRHandJointID.ThumbTip,
        XRHandJointID.IndexTip,
        XRHandJointID.MiddleTip,
        XRHandJointID.RingTip,
        XRHandJointID.LittleTip
    };
    
    private static readonly XRHandJointID[] DistalJointIds = new[]
    {
        XRHandJointID.ThumbDistal,
        XRHandJointID.IndexDistal,
        XRHandJointID.MiddleDistal,
        XRHandJointID.RingDistal,
        XRHandJointID.LittleDistal
    };
    
    // 存储射线命中信息和对应的球体
    public Dictionary<string, RaycastHit> lastHits = new Dictionary<string, RaycastHit>();
    
    void Start()
    {
        // 如果没有指定handTracker，尝试自动查找
        if (handTracker == null)
        {
            handTracker = FindFirstObjectByType<MyHand>();
            if (handTracker == null)
            {
                Debug.LogError("HandRaycaster: 找不到MyHand组件！");
            }
        }
        
        // 初始化滤波器容器
        filterContainer = new OneEuroFilter3DContainer(minCutoff, beta);
        
    }
    
    void Update()
    {
        if (handTracker == null) return;
        
        // 更新滤波器参数（如果在Inspector中修改了参数）
        if (useFiltering)
        {
            filterContainer.UpdateAllParams(minCutoff, beta);
        }
        
        // 对左右手分别进行射线检测
        PerformHandRaycast(Handedness.Left);
        PerformHandRaycast(Handedness.Right);
    }

    
    /// <summary>
    /// 对指定手进行射线检测
    /// </summary>
    /// <param name="handedness">手的类型</param>
    private void PerformHandRaycast(Handedness handedness)
    {
        string handName = handedness == Handedness.Left ? "Left" : "Right";
        
        // 遍历五个手指
        for (int fingerIndex = 0; fingerIndex < 5; fingerIndex++)
        {
            string fingerName = GetFingerName(fingerIndex);
            string rayKey = $"{handName}_{fingerName}";
            
            // 获取Tip和Distal关节位置
            if (TryGetFingerJointPositions(handedness, fingerIndex, out Vector3 tipPos, out Vector3 distalPos))
            {
                // 计算射线方向（从Distal指向Tip）
                Vector3 rayDirection = (tipPos - distalPos).normalized;
                
                // 从Tip位置发射射线
                Ray ray = new Ray(tipPos, rayDirection);
                
                LineRenderer lineRenderer;
                int index;
                if (handedness == Handedness.Left)
                {
                    index = 0;
                }
                else
                {
                    index = 5;
                }

                index += fingerIndex;

                lineRenderer = lineRenderers[index];
                
                // 执行射线检测
                    Vector3 offset = Vector3.zero;
                lineRenderer.SetPosition(0, ray.origin+offset);
                if (Physics.Raycast(ray, out RaycastHit hit, vv.ballRadius, raycastMask))
                {
                    // 存储命中信息
                    lastHits[rayKey] = hit;
                    
                    // Vector3 offset = Vector3.one * (99 * -1);
                        lineRenderer.SetPosition(1, hit.point+offset);

                    Debug.Log("Send to Hit VFX: "+hit.point);
                    vfx[index].SetVector3("HitPosiiton",hit.point+offset );
                    vfx[index].SetVector3("HitNormal",hit.point+hit.normal);
                    vfx[index].SetBool("isHit",true);
                    

                    if (isShowHitInfo)
                    {
                        string hn=handedness == Handedness.Left ? "Left" : "Right";
                        Debug.Log($"{hn} {GetFingerName(fingerIndex)} hit: {hit.collider.name} at {hit.point}");
                    }
                }
                else
                {
                    lineRenderer.SetPosition(1, ray.origin+offset+rayDirection*vv.ballRadius);
                    vfx[index].SetBool("isHit",false);
                    // 移除之前的命中记录
                    if (lastHits.ContainsKey(rayKey))
                    {
                        lastHits.Remove(rayKey);
                    }
                }
                
                // 绘制调试射线
                if (showDebugRays)
                {
                    Color debugColor = lastHits.ContainsKey(rayKey) ? Color.green : Color.red;
                    Debug.DrawRay(tipPos, rayDirection * rayDistance, debugColor, rayDuration);
                }
                
                

                
            }

        }
    }
    
    
    /// <summary>
    /// 获取手指的Tip和Distal关节位置，应用OneEuro滤波
    /// </summary>
    /// <param name="handedness">手的类型</param>
    /// <param name="fingerIndex">手指索引（0-4）</param>
    /// <param name="tipPosition">输出Tip关节位置</param>
    /// <param name="distalPosition">输出Distal关节位置</param>
    /// <returns>是否成功获取位置</returns>
    private bool TryGetFingerJointPositions(Handedness handedness, int fingerIndex, 
        out Vector3 tipPosition, out Vector3 distalPosition)
    {
        tipPosition = Vector3.zero;
        distalPosition = Vector3.zero;
        
        if (fingerIndex < 0 || fingerIndex >= TipJointIds.Length)
            return false;
        
        string handName = handedness == Handedness.Left ? "Left" : "Right";
        string fingerName = GetFingerName(fingerIndex);
        
        // 获取Tip关节位置
        bool hasTip = handTracker.TryGetJointPositionAndVelocity(
            handedness, TipJointIds[fingerIndex], out Vector3 rawTipPosition, out _);
        
        // 获取Distal关节位置
        bool hasDistal = handTracker.TryGetJointPositionAndVelocity(
            handedness, DistalJointIds[fingerIndex], out Vector3 rawDistalPosition, out _);
        
        if (hasTip && hasDistal)
        {
            // 应用OneEuro滤波（如果启用）
            if (useFiltering)
            {
                string tipKey = $"{handName}_{fingerName}_Tip";
                string distalKey = $"{handName}_{fingerName}_Distal";
                
                tipPosition = filterContainer.FilterVector3(tipKey, rawTipPosition);
                distalPosition = filterContainer.FilterVector3(distalKey, rawDistalPosition);
            }
            else
            {
                tipPosition = rawTipPosition;
                distalPosition = rawDistalPosition;
            }
            
            return true;
        }
        
        return false;
    }
    
    
    /// <summary>
    /// 获取手指名称
    /// </summary>
    /// <param name="fingerIndex">手指索引</param>
    /// <returns>手指名称</returns>
    private string GetFingerName(int fingerIndex)
    {
        return fingerIndex switch
        {
            0 => "Thumb",
            1 => "Index",
            2 => "Middle",
            3 => "Ring",
            4 => "Little",
            _ => "Unknown"
        };
    }

    public static int FingerName2index(string fingerName)
    {
        return fingerName?.ToLower() switch
        {
            "thumb" => 0,
            "index" => 1,
            "middle" => 2,
            "ring" => 3,
            "little" => 4,
            _ => -1  // Return -1 for unknown finger names
        };
    }
    
    /// <summary>
    /// 启用或禁用滤波
    /// </summary>
    /// <param name="enable">是否启用</param>
    public void SetFilteringEnabled(bool enable)
    {
        useFiltering = enable;
    }
    
    /// <summary>
    /// 更新滤波器参数
    /// </summary>
    public void UpdateFilterParams(float newMinCutoff, float newBeta)
    {
        minCutoff = newMinCutoff;
        beta = newBeta;
        
        filterContainer.UpdateAllParams(minCutoff, beta);
    }

    
    /// <summary>
    /// 获取指定手指的最后命中信息
    /// </summary>
    public bool TryGetFingerHit(Handedness handedness, int fingerIndex, out RaycastHit hit)
    {
        string handName = handedness == Handedness.Left ? "Left" : "Right";
        string fingerName = GetFingerName(fingerIndex);
        string rayKey = $"{handName}_{fingerName}";
        
        return lastHits.TryGetValue(rayKey, out hit);
    }
    
    /// <summary>
    /// 获取所有当前命中信息
    /// </summary>
    public Dictionary<string, RaycastHit> GetAllHits()
    {
        return new Dictionary<string, RaycastHit>(lastHits);
    }
    
    /// <summary>
    /// 清除所有命中记录
    /// </summary>
    public void ClearAllHits()
    {
        lastHits.Clear();
    }
    
    /// <summary>
    /// 设置射线距离
    /// </summary>
    public void SetRayDistance(float distance)
    {
        rayDistance = Mathf.Max(0.1f, distance);
    }
    
    /// <summary>
    /// 设置射线遮罩
    /// </summary>
    public void SetRaycastMask(LayerMask mask)
    {
        raycastMask = mask;
    }
    
    /// <summary>
    /// 切换调试射线显示
    /// </summary>
    public void SetShowDebugRays(bool show)
    {
        showDebugRays = show;
    }
    
}
