using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Hands;
using System.Collections.Generic;

public class HandRaycaster : MonoBehaviour
{
    [Header("射线设置")]
    [SerializeField] private float rayDistance = 1.0f;
    [SerializeField] private LayerMask raycastMask = -1;
    [SerializeField] private bool showDebugRays = true;
    [SerializeField] private Color rayColor = Color.red;
    [SerializeField] private float rayDuration = 0.1f;
    [Header("射线Visualize")]
    [SerializeField] LineRenderer[] lineRenderers; // From Left to Right, from damuzhi to xiaomuzhi
    
    [Header("球体生成设置")]
    [SerializeField] private GameObject spherePrefab;
    [SerializeField] private bool spawnSphereOnHit = true;
    [SerializeField] private bool hideWhenNotHit = true; // 没有命中时是否隐藏球体
    
    [Header("手部追踪")]
    [SerializeField] private MyHand handTracker;
    
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
    private Dictionary<string, RaycastHit> lastHits = new Dictionary<string, RaycastHit>();
    private Dictionary<string, GameObject> fingerSpheres = new Dictionary<string, GameObject>();
    
    void Start()
    {
        // 如果没有指定handTracker，尝试自动查找
        if (handTracker == null)
        {
            handTracker = FindObjectOfType<MyHand>();
            if (handTracker == null)
            {
                Debug.LogError("HandRaycaster: 找不到MyHand组件！");
            }
        }
        
        // 预创建所有手指的球体
        if (spawnSphereOnHit && spherePrefab != null)
        {
            CreateAllFingerSpheres();
        }
    }
    
    void Update()
    {
        if (handTracker == null) return;
        
        // 对左右手分别进行射线检测
        PerformHandRaycast(Handedness.Left);
        PerformHandRaycast(Handedness.Right);
    }
    
    /// <summary>
    /// 预创建所有手指的球体
    /// </summary>
    private void CreateAllFingerSpheres()
    {
        string[] handNames = { "Left", "Right" };
        
        foreach (string handName in handNames)
        {
            for (int fingerIndex = 0; fingerIndex < 5; fingerIndex++)
            {
                string fingerName = GetFingerName(fingerIndex);
                string sphereKey = $"{handName}_{fingerName}";
                
                // 创建球体
                GameObject sphere = Instantiate(spherePrefab);
                sphere.name = $"FingerSphere_{sphereKey}";
                sphere.transform.SetParent(transform); // 设置为当前对象的子物体
                
                // 初始时隐藏球体
                sphere.SetActive(false);
                
                // 存储到字典中
                fingerSpheres[sphereKey] = sphere;
            }
        }
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
                
                // 执行射线检测
                if (Physics.Raycast(ray, out RaycastHit hit, rayDistance, raycastMask))
                {
                    // 存储命中信息
                    lastHits[rayKey] = hit;
                    
                    // 更新球体位置（如果启用）
                    if (spawnSphereOnHit)
                    {
                        UpdateFingerSphere(rayKey, hit.point, true);
                    }
                    
                    // 调用命中事件
                    OnFingerRayHit(handedness, fingerIndex, hit);
                }
                else
                {
                    // 移除之前的命中记录
                    if (lastHits.ContainsKey(rayKey))
                    {
                        lastHits.Remove(rayKey);
                    }
                    
                    // 隐藏球体（如果启用隐藏选项）
                    if (spawnSphereOnHit && hideWhenNotHit)
                    {
                        UpdateFingerSphere(rayKey, Vector3.zero, false);
                    }
                }
                
                // 绘制调试射线
                if (showDebugRays)
                {
                    Color debugColor = lastHits.ContainsKey(rayKey) ? Color.green : rayColor;
                    Debug.DrawRay(tipPos, rayDirection * rayDistance, debugColor, rayDuration);
                }
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
                // lineRenderer.gameObject.SetActive(true); useless
                Vector3 offset = Vector3.one*99*-1;
                lineRenderer.SetPosition(0, ray.origin+offset);
                lineRenderer.SetPosition(1, hit.point+offset);
            }
            else
            {
                // 如果无法获取关节位置，隐藏对应的球体
                if (spawnSphereOnHit && hideWhenNotHit)
                {
                    UpdateFingerSphere(rayKey, Vector3.zero, false);
                }
            }
        }
    }
    
    /// <summary>
    /// 更新手指球体的位置和显示状态
    /// </summary>
    /// <param name="sphereKey">球体标识符</param>
    /// <param name="position">新位置</param>
    /// <param name="show">是否显示</param>
    private void UpdateFingerSphere(string sphereKey, Vector3 position, bool show)
    {
        if (fingerSpheres.TryGetValue(sphereKey, out GameObject sphere) && sphere != null)
        {
            if (show)
            {
                sphere.transform.position = position;
                sphere.SetActive(true);
            }
            else
            {
                sphere.SetActive(false);
            }
        }
    }
    
    /// <summary>
    /// 获取手指的Tip和Distal关节位置
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
        
        // 获取Tip关节位置
        bool hasTip = handTracker.TryGetJointPositionAndVelocity(
            handedness, TipJointIds[fingerIndex], out tipPosition, out _);
        
        // 获取Distal关节位置
        bool hasDistal = handTracker.TryGetJointPositionAndVelocity(
            handedness, DistalJointIds[fingerIndex], out distalPosition, out _);
        
        return hasTip && hasDistal;
    }
    
    /// <summary>
    /// 手指射线命中事件
    /// </summary>
    /// <param name="handedness">手的类型</param>
    /// <param name="fingerIndex">手指索引</param>
    /// <param name="hit">命中信息</param>
    protected virtual void OnFingerRayHit(Handedness handedness, int fingerIndex, RaycastHit hit)
    {
        string handName = handedness == Handedness.Left ? "Left" : "Right";
        string fingerName = GetFingerName(fingerIndex);
        
        Debug.Log($"{handName} {fingerName} hit: {hit.collider.name} at {hit.point}");
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
    
    /// <summary>
    /// 获取指定手指的球体GameObject
    /// </summary>
    /// <param name="handedness">手的类型</param>
    /// <param name="fingerIndex">手指索引</param>
    /// <returns>球体GameObject，如果不存在返回null</returns>
    public GameObject GetFingerSphere(Handedness handedness, int fingerIndex)
    {
        string handName = handedness == Handedness.Left ? "Left" : "Right";
        string fingerName = GetFingerName(fingerIndex);
        string sphereKey = $"{handName}_{fingerName}";
        
        fingerSpheres.TryGetValue(sphereKey, out GameObject sphere);
        return sphere;
    }
    
    /// <summary>
    /// 获取所有手指球体
    /// </summary>
    /// <returns>球体字典</returns>
    public Dictionary<string, GameObject> GetAllFingerSpheres()
    {
        return new Dictionary<string, GameObject>(fingerSpheres);
    }
    
    /// <summary>
    /// 手动显示/隐藏指定手指的球体
    /// </summary>
    /// <param name="handedness">手的类型</param>
    /// <param name="fingerIndex">手指索引</param>
    /// <param name="show">是否显示</param>
    public void SetFingerSphereVisibility(Handedness handedness, int fingerIndex, bool show)
    {
        string handName = handedness == Handedness.Left ? "Left" : "Right";
        string fingerName = GetFingerName(fingerIndex);
        string sphereKey = $"{handName}_{fingerName}";
        
        if (fingerSpheres.TryGetValue(sphereKey, out GameObject sphere) && sphere != null)
        {
            sphere.SetActive(show);
        }
    }
    
    /// <summary>
    /// 显示/隐藏所有球体
    /// </summary>
    /// <param name="show">是否显示</param>
    public void SetAllSpheresVisibility(bool show)
    {
        foreach (var sphere in fingerSpheres.Values)
        {
            if (sphere != null)
            {
                sphere.SetActive(show);
            }
        }
    }
    
    /// <summary>
    /// 重新创建所有球体（当spherePrefab改变时使用）
    /// </summary>
    [ContextMenu("Recreate All Spheres")]
    public void RecreateAllSpheres()
    {
        // 销毁现有球体
        foreach (var sphere in fingerSpheres.Values)
        {
            if (sphere != null)
            {
                DestroyImmediate(sphere);
            }
        }
        
        fingerSpheres.Clear();
        
        // 重新创建
        if (spherePrefab != null)
        {
            CreateAllFingerSpheres();
        }
    }
    
    /// <summary>
    /// 获取指定手指的最后命中信息
    /// </summary>
    /// <param name="handedness">手的类型</param>
    /// <param name="fingerIndex">手指索引</param>
    /// <param name="hit">输出命中信息</param>
    /// <returns>是否有命中信息</returns>
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
    /// <returns>命中信息字典</returns>
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
    /// <param name="distance">新的射线距离</param>
    public void SetRayDistance(float distance)
    {
        rayDistance = Mathf.Max(0.1f, distance);
    }
    
    /// <summary>
    /// 设置射线遮罩
    /// </summary>
    /// <param name="mask">新的遮罩</param>
    public void SetRaycastMask(LayerMask mask)
    {
        raycastMask = mask;
    }
    
    /// <summary>
    /// 切换调试射线显示
    /// </summary>
    /// <param name="show">是否显示</param>
    public void SetShowDebugRays(bool show)
    {
        showDebugRays = show;
    }
    
    /// <summary>
    /// 设置没有命中时是否隐藏球体
    /// </summary>
    /// <param name="hide">是否隐藏</param>
    public void SetHideWhenNotHit(bool hide)
    {
        hideWhenNotHit = hide;
    }
    
    void OnDestroy()
    {
        // 清理所有球体
        foreach (var sphere in fingerSpheres.Values)
        {
            if (sphere != null)
            {
                DestroyImmediate(sphere);
            }
        }
        fingerSpheres.Clear();
    }
}
