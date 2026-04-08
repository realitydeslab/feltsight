using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.VFX;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Hands;
using UnityEngine.XR.VisionOS;

/// <summary>
///     基于 Unity.Mathematics 的 OneEuro 滤波器，用于 2D 向量
/// </summary>
internal sealed class OneEuroFilter2
{
    #region Previous state variables as a tuple

    private (float t, float2 x, float2 dx) _prev;

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

    #region Public properties

    public float Beta { get; set; }
    public float MinCutoff { get; set; }

    #endregion

    #region Private class members

    private const float DCutOff = 1.0f;

    private static float Alpha(float t_e, float cutoff)
    {
        var r = 2 * math.PI * cutoff * t_e;
        return r / (r + 1);
    }

    #endregion
}

/// <summary>
///     扩展 OneEuroFilter2 以支持 3D 向量
/// </summary>
public sealed class OneEuroFilter3D
{
    private readonly OneEuroFilter2 _filterX;
    private readonly OneEuroFilter2 _filterY;
    private readonly OneEuroFilter2 _filterZ;

    /// <summary>
    ///     创建一个新的 OneEuroFilter3D 实例
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
    ///     应用滤波器到 Vector3
    /// </summary>
    /// <param name="time">当前时间</param>
    /// <param name="value">输入向量</param>
    /// <returns>滤波后的向量</returns>
    public Vector3 Step(float time, Vector3 value)
    {
        var xy = _filterX.Step(time, new float2(value.x, value.y));
        var zw = _filterY.Step(time, new float2(value.z, 0));

        return new Vector3(xy.x, xy.y, zw.x);
    }

    /// <summary>
    ///     更新滤波器参数
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
///     用于管理多个 OneEuroFilter3D 的容器类
/// </summary>
public class OneEuroFilter3DContainer
{
    private float _beta;
    private readonly Dictionary<string, OneEuroFilter3D> _filters = new();
    private float _minCutoff;

    /// <summary>
    ///     创建一个新的滤波器容器
    /// </summary>
    /// <param name="minCutoff">最小截止频率 (默认: 1.0)</param>
    /// <param name="beta">速度系数 (默认: 0.0)</param>
    public OneEuroFilter3DContainer(float minCutoff = 1.0f, float beta = 0.0f)
    {
        _minCutoff = minCutoff;
        _beta = beta;
    }

    /// <summary>
    ///     获取或创建指定键的滤波器
    /// </summary>
    public OneEuroFilter3D GetFilter(string key)
    {
        if (!_filters.TryGetValue(key, out var filter))
        {
            filter = new OneEuroFilter3D(_minCutoff, _beta);
            _filters[key] = filter;
        }

        return filter;
    }

    /// <summary>
    ///     应用滤波器到 Vector3
    /// </summary>
    public Vector3 FilterVector3(string key, Vector3 value, float timestamp = -1.0f)
    {
        if (timestamp < 0)
            timestamp = Time.time;

        return GetFilter(key).Step(timestamp, value);
    }

    /// <summary>
    ///     更新所有滤波器的参数
    /// </summary>
    public void UpdateAllParams(float minCutoff, float beta)
    {
        _minCutoff = minCutoff;
        _beta = beta;

        foreach (var filter in _filters.Values) filter.UpdateParams(minCutoff, beta);
    }
}

public class HandRaycaster : MonoBehaviour
{
    // 手指关节ID定义
    private static readonly XRHandJointID[] TipJointIds =
    {
        XRHandJointID.ThumbTip,
        XRHandJointID.IndexTip,
        XRHandJointID.MiddleTip,
        XRHandJointID.RingTip,
        XRHandJointID.LittleTip
    };

    private static readonly XRHandJointID[] DistalJointIds =
    {
        XRHandJointID.ThumbDistal,
        XRHandJointID.IndexDistal,
        XRHandJointID.MiddleDistal,
        XRHandJointID.RingDistal,
        XRHandJointID.LittleDistal
    };

    [Header("射线设置")] [SerializeField] private float rayDistance = 1.0f;

    [SerializeField] private LayerMask raycastMask = -1;

    [Header("射线Visualize")] [SerializeField]
    private LineRenderer[] lineRenderers; // From Left to Right, from damuzhi to xiaomuzhi

    [SerializeField] private VisualEffect[] vfx;
    [SerializeField] private VFXMan vv;

    [Header("手部追踪")] [SerializeField] private MyHand handTracker;

    [Header("测试功能")] [SerializeField] private bool useTestColors; // 是否使用测试颜色

    [Header("OneEuro 滤波器设置")] [SerializeField]
    private bool useFiltering = true;

    [SerializeField] private float minCutoff = 1.0f;
    [SerializeField] private float beta;

    [Header("Debug Settings")] [SerializeField]
    private bool isShowHitInfo;

    [SerializeField] private bool showDebugRays = true;
    [SerializeField] private float rayDuration = 0.1f;

    // 颜色平滑插值速度
    [Header("颜色平滑设置")] [SerializeField] private float colorSmoothSpeed = 5.0f;

    // OneEuro 滤波器容器
    private OneEuroFilter3DContainer filterContainer;

    // 存储每个手指的当前颜色 (用于平滑过渡)
    private readonly Dictionary<string, Vector3> fingerColors = new();

    // 存储射线命中信息和对应的球体
    public Dictionary<string, RaycastHit> lastHits = new();

    // 材质类型到HDR颜色的映射
    private readonly Dictionary<string, int> materialColorMap = new();

    private readonly Dictionary<string, int> lastHitColorIndices = new();
    private readonly Dictionary<string, string> lastHitClassNames = new();


    // SuperAdmin引用
    private SuperAdmin superAdmin;

    private void Start()
    {
        // 如果没有指定handTracker，尝试自动查找
        if (handTracker == null)
        {
            handTracker = FindFirstObjectByType<MyHand>();
            if (handTracker == null) Debug.LogError("HandRaycaster: 找不到MyHand组件！");
        }

        // 初始化滤波器容器
        filterContainer = new OneEuroFilter3DContainer(minCutoff, beta);

        // 获取SuperAdmin引用
        superAdmin = FindFirstObjectByType<SuperAdmin>();
    }

    private void Update()
    {
        if (handTracker == null) return;

        // 更新滤波器参数（如果在Inspector中修改了参数）
        if (useFiltering) filterContainer.UpdateAllParams(minCutoff, beta);

        // 对左右手分别进行射线检测
        PerformHandRaycast(Handedness.Left);
        PerformHandRaycast(Handedness.Right);
    }


    /// <summary>
    ///     对指定手进行射线检测
    /// </summary>
    /// <param name="handedness">手的类型</param>
    private void PerformHandRaycast(Handedness handedness)
    {
        var handName = handedness == Handedness.Left ? "Left" : "Right";

        // 遍历五个手指
        for (var fingerIndex = 0; fingerIndex < 5; fingerIndex++)
        {
            var fingerName = GetFingerName(fingerIndex);
            var rayKey = $"{handName}_{fingerName}";

            // 获取Tip和Distal关节位置
            if (TryGetFingerJointPositions(handedness, fingerIndex, out var tipPos, out var distalPos))
            {
                // 计算射线方向（从Distal指向Tip）
                var rayDirection = (tipPos - distalPos).normalized;

                // 从Tip位置发射射线
                var ray = new Ray(tipPos, rayDirection);

                LineRenderer lineRenderer;
                int index;
                if (handedness == Handedness.Left)
                    index = 0;
                else
                    index = 5;

                index += fingerIndex;

                lineRenderer = lineRenderers[index];

                // 执行射线检测
                var offset = Vector3.zero;
                lineRenderer.SetPosition(0, ray.origin + offset);
                var currentRayDistance = superAdmin != null && superAdmin.isDebug ? rayDistance : vv.ballRadius + 0.2f;
                if (Physics.Raycast(ray, out var hit, currentRayDistance, raycastMask))
                {
                    // 存储命中信息
                    lastHits[rayKey] = hit;

                    // Vector3 offset = Vector3.one * (99 * -1);
                    lineRenderer.SetPosition(1, hit.point + offset);

                    // Debug.Log("Send to Hit VFX: "+hit.point);
                    vfx[index].SetVector3("HitPosiiton", hit.point + offset);
                    vfx[index].SetVector3("HitNormal", hit.normal);
                    vfx[index].SetFloat("BallRaidus", vv.ballRadius);
                    vfx[index].SetBool("isHit", true);


                    // 计算手指索引 (0-9: 左手拇指到小指, 右手拇指到小指)
                    var arrayIndex = handedness == Handedness.Left ? fingerIndex : fingerIndex + 5;
                    var hitClassName = useTestColors ? $"TestColor_{arrayIndex}" : XRMeshClassification.Unknown.ToString();
                    var hitColorIndex = useTestColors ? arrayIndex : GetHitMeshClassificationIndex(hit, out hitClassName);

                    vfx[index].SetInt("HitColorIndex", hitColorIndex);
                    lastHitColorIndices[rayKey] = hitColorIndex;
                    lastHitClassNames[rayKey] = hitClassName;
                    UpdateFingerHitInfo(handedness, fingerIndex, hit.collider.name, hitClassName, hitColorIndex);

                    if (isShowHitInfo)
                    {
                        var hn = handedness == Handedness.Left ? "Left" : "Right";
                        var debugTag = hitColorIndex != (int)XRMeshClassification.Unknown ? "[HandRaycastClassHit]" : "[HandRaycastClass]";
                        Debug.Log($"{debugTag} {hn} {GetFingerName(fingerIndex)} -> {hitClassName} ({hitColorIndex}) [{hit.collider.name}] at {hit.point}");
                    }
                }
                else
                {
                    lineRenderer.SetPosition(1, ray.origin + offset + rayDirection * vv.ballRadius);
                    vfx[index].SetBool("isHit", false);
                    lastHitColorIndices.Remove(rayKey);
                    lastHitClassNames.Remove(rayKey);
                    ResetFingerHitInfo(handedness, fingerIndex);
                    // 移除之前的命中记录
                    if (lastHits.ContainsKey(rayKey)) lastHits.Remove(rayKey);
                }

                // 绘制调试射线
                if (showDebugRays)
                {
                    var debugColor = lastHits.ContainsKey(rayKey) ? Color.green : Color.red;
                    Debug.DrawRay(tipPos, rayDirection * rayDistance, debugColor, rayDuration);
                }
            }
        }
    }


    /// <summary>
    ///     获取手指的Tip和Distal关节位置，应用OneEuro滤波
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

        var handName = handedness == Handedness.Left ? "Left" : "Right";
        var fingerName = GetFingerName(fingerIndex);

        // 获取Tip关节位置
        var hasTip = handTracker.TryGetJointPositionAndVelocity(
            handedness, TipJointIds[fingerIndex], out var rawTipPosition, out _);

        // 获取Distal关节位置
        var hasDistal = handTracker.TryGetJointPositionAndVelocity(
            handedness, DistalJointIds[fingerIndex], out var rawDistalPosition, out _);

        if (hasTip && hasDistal)
        {
            // 应用OneEuro滤波（如果启用）
            if (useFiltering)
            {
                var tipKey = $"{handName}_{fingerName}_Tip";
                var distalKey = $"{handName}_{fingerName}_Distal";

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
    ///     获取手指名称
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
            _ => -1 // Return -1 for unknown finger names
        };
    }

    /// <summary>
    ///     启用或禁用滤波
    /// </summary>
    /// <param name="enable">是否启用</param>
    public void SetFilteringEnabled(bool enable)
    {
        useFiltering = enable;
    }

    /// <summary>
    ///     更新滤波器参数
    /// </summary>
    public void UpdateFilterParams(float newMinCutoff, float newBeta)
    {
        minCutoff = newMinCutoff;
        beta = newBeta;

        filterContainer.UpdateAllParams(minCutoff, beta);
    }


    /// <summary>
    ///     获取指定手指的最后命中信息
    /// </summary>
    public bool TryGetFingerHit(Handedness handedness, int fingerIndex, out RaycastHit hit)
    {
        var handName = handedness == Handedness.Left ? "Left" : "Right";
        var fingerName = GetFingerName(fingerIndex);
        var rayKey = $"{handName}_{fingerName}";
 
        return lastHits.TryGetValue(rayKey, out hit);
    }

    public bool TryGetFingerHitClassification(Handedness handedness, int fingerIndex, out int hitColorIndex, out string hitClassName)
    {
        var handName = handedness == Handedness.Left ? "Left" : "Right";
        var fingerName = GetFingerName(fingerIndex);
        var rayKey = $"{handName}_{fingerName}";

        var hasColorIndex = lastHitColorIndices.TryGetValue(rayKey, out hitColorIndex);
        var hasClassName = lastHitClassNames.TryGetValue(rayKey, out hitClassName);
        return hasColorIndex && hasClassName;
    }

    private int GetHitMeshClassificationIndex(RaycastHit hit, out string className)
    {
        className = "Unknown";

#if UNITY_VISIONOS
        var meshCollider = hit.collider as MeshCollider;
        var sharedMesh = meshCollider != null ? meshCollider.sharedMesh : null;
        if (sharedMesh == null)
            return 0;

        var triangleIndex = hit.triangleIndex;
        if (triangleIndex < 0)
            return 0;

        var trackableId = TryExtractTrackableId(hit.collider.transform);
        if (!trackableId.HasValue)
            return 0;

        var meshSubsystem = UnityEngine.XR.Management.XRGeneralSettings.Instance?.Manager?.activeLoader?.GetLoadedSubsystem<UnityEngine.XR.XRMeshSubsystem>();
        if (meshSubsystem == null)
            return 0;

        using var faceClassifications = meshSubsystem.GetFaceClassifications(trackableId.Value, Allocator.Temp);
        if (!faceClassifications.IsCreated || triangleIndex >= faceClassifications.Length)
            return 0;

        var classification = faceClassifications[triangleIndex];
        className = classification.ToString();
        return MapVisionOSClassificationToHitTypeIndex(classification);
#else
        return 0;
#endif
    }

    private int MapVisionOSClassificationToHitTypeIndex(ARMeshClassification classification)
    {
        return classification switch
        {
            ARMeshClassification.Seat => 3,
            ARMeshClassification.Table => 4,
            ARMeshClassification.Floor => 5,
            ARMeshClassification.Wall => 5,
            ARMeshClassification.Plant => 10,
            ARMeshClassification.TV => 11,
            _ => 0
        };
    }

    private TrackableId? TryExtractTrackableId(Transform current)
    {
        while (current != null)
        {
            var parts = current.name.Split(' ');
            if (parts.Length > 1)
            {
                try
                {
                    return new TrackableId(parts[1]);
                }
                catch
                {
                    // Ignore invalid name format and continue searching parent transforms.
                }
            }

            current = current.parent;
        }

        return null;
    }

    private void UpdateFingerHitInfo(Handedness handedness, int fingerIndex, string colliderName, string className, int hitColorIndex)
    {
        if (superAdmin == null || !superAdmin.isShowHandRayHitClass)
            return;

        var arrayIndex = handedness == Handedness.Left ? fingerIndex : fingerIndex + 5;
        var handShortName = handedness == Handedness.Left ? "L" : "R";
        var fingerName = GetFingerName(fingerIndex);
        superAdmin.SetFingerHitInfo(arrayIndex, $"{handShortName} {fingerName}: {className} ({hitColorIndex}) [{colliderName}]");
    }

    private void ResetFingerHitInfo(Handedness handedness, int fingerIndex)
    {
        if (superAdmin == null || !superAdmin.isShowHandRayHitClass)
            return;

        var arrayIndex = handedness == Handedness.Left ? fingerIndex : fingerIndex + 5;
        superAdmin.ResetFingerHitInfo(arrayIndex);
    }
 
    /// <summary>
    ///     平滑过渡颜色，减少抖动(弃用,平滑算法放到了VFX里)
    /// </summary>
/// <param name="key">手指唯一标识</param>
    /// <param name="targetColor">目标颜色</param>
    /// <returns>平滑过渡后的颜色</returns>
    private Vector3 SmoothColor(string key, Vector3 targetColor)
    {
        if (!fingerColors.TryGetValue(key, out var currentColor))
        {
            // 如果是第一次设置颜色，直接使用目标颜色
            fingerColors[key] = targetColor;
            return targetColor;
        }

        // 计算平滑过渡的颜色 (Vector3.Lerp)
        var smoothedColor = Vector3.Lerp(
            currentColor,
            targetColor,
            colorSmoothSpeed * Time.deltaTime
        );

        // 更新存储的颜色
        fingerColors[key] = smoothedColor;

        return smoothedColor;
    }
    
}