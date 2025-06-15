using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Hands;

/// <summary>
///     这个组件用于打印手部根部姿态和腕部关节的位置旋转信息
///     发现双手合十的时候距离是0.9, 但是阿多给的时候距离会更短
///     我自己的手张开角度对应的双手距离是 0.4~0.8
/// </summary>
public class MyHand : MonoBehaviour
{
    private static readonly List<XRHandSubsystem> s_SubsystemsReuse = new();

    [Header("DEBUG LOG控制")]
    [SerializeField] [Tooltip("是否启用位置信息打印")]
    private bool m_EnableLogging = true;

    [SerializeField] [Tooltip("打印信息的间隔时间（秒）")]
    private float m_LogInterval = 0.5f;

    [SerializeField] [Tooltip("是否打印左手信息")] private bool m_LogLeftHand = true;

    [SerializeField] [Tooltip("是否打印右手信息")] private bool m_LogRightHand = true;

    [SerializeField] [Tooltip("是否打印手部根部姿态")]
    private bool m_LogRootPose = true;

    [SerializeField] [Tooltip("是否打印腕部关节姿态")]
    private bool m_LogWristJoint = true;
    
    [SerializeField] [Tooltip("是否打印手掌关节姿态")]
    private bool m_LogPalmJoint = true;
    
    [SerializeField] [Tooltip("是否打印指尖速度")]
    private bool ifLogFingerTipV = true;

    [Header("Palm替代设置")]
    [SerializeField] [Tooltip("是否使用四指Proximal重心作为Palm的备用")]
    private bool m_UseProximalCentroidAsPalmFallback = true;

    [SerializeField] [Tooltip("是否强制使用四指Proximal重心代替Palm")]
    private bool m_ForceUseProximalCentroidAsPalm = false;

    [Header("距离设置")]
    [SerializeField] [Tooltip("当无法获取距离时返回的极大值")]
    private float m_InvalidDistanceValue = float.MaxValue;

    [Header("UI组件")]

    [SerializeField] [Tooltip("用于显示左手位置的文本组件")]
    private TextMeshProUGUI m_LeftHandPositionText;

    [SerializeField] [Tooltip("用于显示右手位置的文本组件")]
    private TextMeshProUGUI m_RightHandPositionText;

    [SerializeField] [Tooltip("用于显示左手旋转的文本组件")]
    private TextMeshProUGUI m_LeftHandRotationText;

    [SerializeField] [Tooltip("用于显示右手旋转的文本组件")]
    private TextMeshProUGUI m_RightHandRotationText;

    [SerializeField] [Tooltip("用于显示两只手距离的文本组件")]
    private TextMeshProUGUI m_HandsDistanceText;

    [SerializeField] [Tooltip("用于显示两只手Palm距离的文本组件")]
    private TextMeshProUGUI m_PalmDistanceText;

    [SerializeField] [Tooltip("用于显示手掌开合角度的文本组件")]
    private TextMeshProUGUI m_PalmAngleText;

    [SerializeField] [Tooltip("用于显示所有可用关节名称的文本组件")]
    private TextMeshProUGUI m_AvailableJointsText;

    [SerializeField] [Tooltip("是否在文本组件中显示手部位置和旋转")]
    private bool m_ShowHandInfo = true;

    [SerializeField] [Tooltip("是否显示两只手的距离")]
    private bool m_ShowHandsDistance = true;

    [SerializeField] [Tooltip("是否显示两只手Palm的距离")]
    private bool m_ShowPalmDistance = true;

    [SerializeField] [Tooltip("是否显示手掌开合角度")]
    private bool m_ShowPalmAngle = true;

    [SerializeField] [Tooltip("是否显示所有可用关节名称")]
    private bool m_ShowAvailableJoints = true;

    [SerializeField] [Tooltip("关节列表更新间隔（秒）")]
    private float m_JointListUpdateInterval = 1.0f;

    [SerializeField] [Tooltip("是否显示关节的追踪状态")]
    private bool m_ShowJointTrackingState = false;

    [SerializeField] [Tooltip("选择显示哪只手的关节信息")]
    private Handedness m_JointDisplayHand = Handedness.Left;

    private float m_LastLogTime;
    private float m_LastJointListUpdateTime;

    private XRHandSubsystem m_Subsystem;

    // 存储关节位置历史记录用于计算速度
    private Dictionary<XRHandJointID, Vector3> m_LeftJointLastPositions = new Dictionary<XRHandJointID, Vector3>();
    private Dictionary<XRHandJointID, Vector3> m_RightJointLastPositions = new Dictionary<XRHandJointID, Vector3>();
    private Dictionary<XRHandJointID, Vector3> m_LeftJointVelocities = new Dictionary<XRHandJointID, Vector3>();
    private Dictionary<XRHandJointID, Vector3> m_RightJointVelocities = new Dictionary<XRHandJointID, Vector3>();
    private float m_LastVelocityUpdateTime;

    // 缓存所有关节ID
    private static XRHandJointID[] s_AllJointIds;

    // 四指Proximal关节ID
    private static readonly XRHandJointID[] s_ProximalJointIds = new[]
    {
        XRHandJointID.IndexProximal,
        XRHandJointID.MiddleProximal,
        XRHandJointID.RingProximal,
        XRHandJointID.LittleProximal
    };

    /// <summary>
    ///     是否启用位置信息打印
    /// </summary>
    public bool enableLogging
    {
        get => m_EnableLogging;
        set => m_EnableLogging = value;
    }

    /// <summary>
    ///     打印信息的间隔时间
    /// </summary>
    public float logInterval
    {
        get => m_LogInterval;
        set => m_LogInterval = Mathf.Max(0.1f, value);
    }

    /// <summary>
    ///     是否显示手部位置和旋转信息
    /// </summary>
    public bool showHandInfo
    {
        get => m_ShowHandInfo;
        set => m_ShowHandInfo = value;
    }

    /// <summary>
    ///     是否显示两只手的距离
    /// </summary>
    public bool showHandsDistance
    {
        get => m_ShowHandsDistance;
        set => m_ShowHandsDistance = value;
    }

    /// <summary>
    ///     是否显示两只手Palm的距离
    /// </summary>
    public bool showPalmDistance
    {
        get => m_ShowPalmDistance;
        set => m_ShowPalmDistance = value;
    }

    /// <summary>
    ///     是否显示手掌开合角度
    /// </summary>
    public bool showPalmAngle
    {
        get => m_ShowPalmAngle;
        set => m_ShowPalmAngle = value;
    }

    /// <summary>
    ///     是否显示所有可用关节名称
    /// </summary>
    public bool showAvailableJoints
    {
        get => m_ShowAvailableJoints;
        set => m_ShowAvailableJoints = value;
    }

    /// <summary>
    ///     获取当前双手之间的距离（米）
    ///     如果无法获取距离（手不可见），则返回极大值（默认float.MaxValue）
    /// </summary>
    public float handsDistance { get; private set; } = float.MaxValue;

    /// <summary>
    ///     获取当前双手Palm之间的距离（米）
    ///     如果无法获取距离（手不可见），则返回极大值（默认float.MaxValue）
    /// </summary>
    public float palmDistance { get; private set; } = float.MaxValue;

    /// <summary>
    ///     获取当前手掌开合角度（度）
    ///     角度是由左手Palm、两手腕中点、右手Palm组成的三角形在两手腕中点处的角度
    ///     如果无法获取角度（手不可见），则返回极大值（默认float.MaxValue）
    /// </summary>
    public float palmAngle { get; private set; } = float.MaxValue;

    /// <summary>
    ///     检查距离值是否有效（不是极大值）
    /// </summary>
    /// <param name="distance">要检查的距离值</param>
    /// <returns>如果距离有效返回true，否则返回false</returns>
    public bool IsDistanceValid(float distance)
    {
        return !float.IsInfinity(distance) && distance < m_InvalidDistanceValue * 0.9f;
    }

    /// <summary>
    ///     检查角度值是否有效（不是极大值）
    /// </summary>
    /// <param name="angle">要检查的角度值</param>
    /// <returns>如果角度有效返回true，否则返回false</returns>
    public bool IsAngleValid(float angle)
    {
        return !float.IsInfinity(angle) && angle < m_InvalidDistanceValue * 0.9f;
    }

    /// <summary>
    ///     静态构造函数，初始化所有关节ID
    /// </summary>
    static MyHand()
    {
        InitializeAllJointIds();
    }

    /// <summary>
    ///     初始化所有关节ID数组
    /// </summary>
    private static void InitializeAllJointIds()
    {
        var jointIdsList = new List<XRHandJointID>();
        
        // 遍历XRHandJointID枚举的所有值
        foreach (XRHandJointID jointId in System.Enum.GetValues(typeof(XRHandJointID)))
        {
            jointIdsList.Add(jointId);
        }
        
        s_AllJointIds = jointIdsList.ToArray();
    }

    /// <summary>
    ///     获取所有关节ID的数组
    /// </summary>
    /// <returns>包含所有关节ID的数组</returns>
    public static XRHandJointID[] GetAllJointIds()
    {
        if (s_AllJointIds == null)
            InitializeAllJointIds();
        return s_AllJointIds;
    }

    /// <summary>
    ///     计算四指Proximal关节的重心位置
    /// </summary>
    /// <param name="handedness">手的类型（左手或右手）</param>
    /// <param name="centroidPosition">输出的重心位置</param>
    /// <param name="availableJointsCount">可用关节数量</param>
    /// <returns>是否成功计算重心（至少需要2个关节可用）</returns>
    public bool TryGetProximalCentroid(Handedness handedness, out Vector3 centroidPosition, out int availableJointsCount)
    {
        centroidPosition = Vector3.zero;
        availableJointsCount = 0;

        if (m_Subsystem == null || !m_Subsystem.running)
            return false;

        var hand = handedness == Handedness.Left ? m_Subsystem.leftHand : m_Subsystem.rightHand;

        if (!hand.isTracked)
            return false;

        Vector3 positionSum = Vector3.zero;

        foreach (var jointId in s_ProximalJointIds)
        {
            var joint = hand.GetJoint(jointId);
            if (joint.TryGetPose(out var pose))
            {
                positionSum += pose.position;
                availableJointsCount++;
            }
        }

        // 至少需要2个关节可用才能计算有意义的重心
        if (availableJointsCount >= 2)
        {
            centroidPosition = positionSum / availableJointsCount;
            return true;
        }

        return false;
    }

    protected void Update()
    {
        // 如果已经有运行中的子系统，持续更新双手距离
        if (m_Subsystem != null && m_Subsystem.running)
        {
            // 即使没有UI更新，也持续更新双手距离数据用于外部访问
            TryGetHandsDistance(out _);
            TryGetPalmDistance(out _);
            TryGetPalmAngle(out _);
            return;
        }

        // 查找运行中的手部追踪子系统
        SubsystemManager.GetSubsystems(s_SubsystemsReuse);
        var foundRunningHandSubsystem = false;
        for (var i = 0; i < s_SubsystemsReuse.Count; ++i)
        {
            var handSubsystem = s_SubsystemsReuse[i];
            if (handSubsystem.running)
            {
                UnsubscribeHandSubsystem();
                m_Subsystem = handSubsystem;
                foundRunningHandSubsystem = true;
                break;
            }
        }

        if (foundRunningHandSubsystem) SubscribeHandSubsystem();
    }

    /// <summary>
    ///     See <see cref="MonoBehaviour" />.
    /// </summary>
    protected void OnEnable()
    {
        m_LastLogTime = Time.time;
        m_LastVelocityUpdateTime = Time.time;
        m_LastJointListUpdateTime = Time.time;
        m_LeftJointLastPositions.Clear();
        m_RightJointLastPositions.Clear();
        m_LeftJointVelocities.Clear();
        m_RightJointVelocities.Clear();
        
        // 初始化距离和角度为极大值
        handsDistance = m_InvalidDistanceValue;
        palmDistance = m_InvalidDistanceValue;
        palmAngle = m_InvalidDistanceValue;
        
        // 初始化关节列表显示
        UpdateAvailableJointsText();
    }

    /// <summary>
    ///     See <see cref="MonoBehaviour" />.
    /// </summary>
    protected void OnDisable()
    {
        if (m_Subsystem != null)
        {
            m_Subsystem.updatedHands -= OnUpdatedHands;
            m_Subsystem = null;
        }
    }

    /// <summary>
    ///     获取双手距离的归一化值(0-1)，基于给定的最小和最大距离
    /// </summary>
    /// <param name="minDistance">最小距离参考值（默认0.4米）</param>
    /// <param name="maxDistance">最大距离参考值（默认0.8米）</param>
    /// <returns>归一化的距离值，范围0-1，如果双手不可见则返回0</returns>
    public float GetNormalizedHandsDistance(float minDistance = 0.4f, float maxDistance = 0.8f)
    {
        if (!IsDistanceValid(handsDistance))
            return 0;

        return Mathf.Clamp01((handsDistance - minDistance) / (maxDistance - minDistance));
    }

    /// <summary>
    ///     获取双手Palm距离的归一化值(0-1)，基于给定的最小和最大距离
    /// </summary>
    /// <param name="minDistance">最小距离参考值（默认0.4米）</param>
    /// <param name="maxDistance">最大距离参考值（默认0.8米）</param>
    /// <returns>归一化的距离值，范围0-1，如果双手不可见则返回0</returns>
    public float GetNormalizedPalmDistance(float minDistance = 0.4f, float maxDistance = 0.8f)
    {
        if (!IsDistanceValid(palmDistance))
            return 0;

        return Mathf.Clamp01((palmDistance - minDistance) / (maxDistance - minDistance));
    }

    /// <summary>
    ///     获取手掌开合角度的归一化值(0-1)，基于给定的最小和最大角度
    /// </summary>
    /// <param name="minAngle">最小角度参考值（默认0度）</param>
    /// <param name="maxAngle">最大角度参考值（默认180度）</param>
    /// <returns>归一化的角度值，范围0-1，如果双手不可见则返回0</returns>
    public float GetNormalizedPalmAngle(float minAngle = 0f, float maxAngle = 180f)
    {
        if (!IsAngleValid(palmAngle))
            return 0;

        return Mathf.Clamp01((palmAngle - minAngle) / (maxAngle - minAngle));
    }

    private void SubscribeHandSubsystem()
    {
        if (m_Subsystem == null)
            return;

        m_Subsystem.updatedHands += OnUpdatedHands;
    }

    private void UnsubscribeHandSubsystem()
    {
        if (m_Subsystem == null)
            return;

        m_Subsystem.updatedHands -= OnUpdatedHands;
    }

    // 更新关节速度计算
    private void UpdateJointVelocities()
    {
        if (m_Subsystem == null || !m_Subsystem.running)
            return;

        float deltaTime = Time.time - m_LastVelocityUpdateTime;
        if (deltaTime < 0.01f) // 确保时间间隔合理
            return;

        // 更新左手关节速度
        if (m_Subsystem.leftHand.isTracked)
        {
            UpdateHandJointVelocities(Handedness.Left, deltaTime);
        }

        // 更新右手关节速度
        if (m_Subsystem.rightHand.isTracked)
        {
            UpdateHandJointVelocities(Handedness.Right, deltaTime);
        }

        m_LastVelocityUpdateTime = Time.time;
    }

    // 更新指定手的所有关节速度
    private void UpdateHandJointVelocities(Handedness handedness, float deltaTime)
    {
        var hand = handedness == Handedness.Left ? m_Subsystem.leftHand : m_Subsystem.rightHand;
        var positionsDict = handedness == Handedness.Left ? m_LeftJointLastPositions : m_RightJointLastPositions;
        var velocitiesDict = handedness == Handedness.Left ? m_LeftJointVelocities : m_RightJointVelocities;

        // 定义要计算速度的关节ID数组
        var jointIds = new List<XRHandJointID>
        {
            XRHandJointID.Wrist,
            XRHandJointID.Palm,
            XRHandJointID.ThumbTip,
            XRHandJointID.IndexTip,
            XRHandJointID.MiddleTip,
            XRHandJointID.RingTip,
            XRHandJointID.LittleTip
        };

        // 添加四指Proximal关节
        jointIds.AddRange(s_ProximalJointIds);

        foreach (var jointId in jointIds)
        {
            var joint = hand.GetJoint(jointId);
            if (joint.TryGetPose(out var pose))
            {
                Vector3 currentPosition = pose.position;

                // 如果有上一帧位置记录，计算速度
                if (positionsDict.TryGetValue(jointId, out Vector3 lastPosition))
                {
                    Vector3 velocity = (currentPosition - lastPosition) / deltaTime;
                    velocitiesDict[jointId] = velocity;
                }

                // 更新位置记录
                positionsDict[jointId] = currentPosition;
            }
        }
    }

    /// <summary>
    ///     更新可用关节列表文本
    /// </summary>
    private void UpdateAvailableJointsText()
    {
        if (!m_ShowAvailableJoints || m_AvailableJointsText == null)
            return;

        // 检查是否需要更新（基于更新间隔）
        if (Time.time - m_LastJointListUpdateTime < m_JointListUpdateInterval)
            return;

        var stringBuilder = new StringBuilder();
        stringBuilder.AppendLine($"=== {m_JointDisplayHand} Hand Joints ===");

        if (m_Subsystem != null && m_Subsystem.running)
        {
            var hand = m_JointDisplayHand == Handedness.Left ? m_Subsystem.leftHand : m_Subsystem.rightHand;
            
            if (hand.isTracked)
            {
                var availableCount = 0;
                var totalCount = 0;

                foreach (var jointId in GetAllJointIds())
                {
                    totalCount++;
                    if (jointId==XRHandJointID.Invalid ||  jointId==XRHandJointID.EndMarker)
                        continue;
                    var joint = hand.GetJoint(jointId);
                    
                    if (joint.TryGetPose(out var pose))
                    {
                        availableCount++;
                        
                        if (m_ShowJointTrackingState)
                        {
                            stringBuilder.AppendLine($"✓ {jointId} ({joint.trackingState})");
                        }
                        else
                        {
                            stringBuilder.AppendLine($"✓ {jointId}");
                        }
                    }
                    else
                    {
                        if (m_ShowJointTrackingState)
                        {
                            stringBuilder.AppendLine($"✗ {jointId} ({joint.trackingState})");
                        }
                        else
                        {
                            stringBuilder.AppendLine($"✗ {jointId}");
                        }
                    }
                }

                stringBuilder.AppendLine($"\nAvailable: {availableCount}/{totalCount}");
                
                // 添加Palm替代信息
                if (m_ForceUseProximalCentroidAsPalm)
                {
                    if (TryGetProximalCentroid(m_JointDisplayHand, out _, out int centroidJointsCount))
                    {
                        stringBuilder.AppendLine($"Palm: Using Proximal Centroid (Forced, {centroidJointsCount}/4 joints)");
                    }
                    else
                    {
                        stringBuilder.AppendLine("Palm: Proximal Centroid Failed (Forced)");
                    }
                }
                else if (m_UseProximalCentroidAsPalmFallback)
                {
                    var palmAvailable = hand.GetJoint(XRHandJointID.Palm).TryGetPose(out _);
                    
                    if (palmAvailable)
                    {
                        stringBuilder.AppendLine("Palm: Using Palm (Primary)");
                    }
                    else if (TryGetProximalCentroid(m_JointDisplayHand, out _, out int centroidJointsCount))
                    {
                        stringBuilder.AppendLine($"Palm: Using Proximal Centroid (Fallback, {centroidJointsCount}/4 joints)");
                    }
                    else
                    {
                        stringBuilder.AppendLine("Palm: Not Available");
                    }
                }
            }
            else
            {
                stringBuilder.AppendLine("Hand not tracked");
                stringBuilder.AppendLine("\nAll Joint IDs:");
                foreach (var jointId in GetAllJointIds())
                {
                    stringBuilder.AppendLine($"- {jointId}");
                }
            }
        }
        else
        {
            stringBuilder.AppendLine("Hand tracking not available");
            stringBuilder.AppendLine("\nAll Joint IDs:");
            foreach (var jointId in GetAllJointIds())
            {
                stringBuilder.AppendLine($"- {jointId}");
            }
        }

        m_AvailableJointsText.text = stringBuilder.ToString();
        m_LastJointListUpdateTime = Time.time;
    }

    private void OnUpdatedHands(XRHandSubsystem subsystem, XRHandSubsystem.UpdateSuccessFlags updateSuccessFlags,
        XRHandSubsystem.UpdateType updateType)
    {
        // 更新关节速度
        UpdateJointVelocities();

        // 更新关节列表
        UpdateAvailableJointsText();

        // 只在启用日志记录且达到间隔时间时打印
        if (!m_EnableLogging || Time.time - m_LastLogTime < m_LogInterval)
        {
            // 即使不打印日志，也更新文本
            UpdateHandInfoTexts();
            return;
        }

        // 检查是否有手部数据更新成功
        var leftHandUpdated = (updateSuccessFlags & XRHandSubsystem.UpdateSuccessFlags.LeftHandJoints) != 0;
        var rightHandUpdated = (updateSuccessFlags & XRHandSubsystem.UpdateSuccessFlags.RightHandJoints) != 0;

        // 更新所有手部信息文本
        UpdateHandInfoTexts();

        // 打印左手信息
        if (m_LogLeftHand && leftHandUpdated && subsystem.leftHand.isTracked)
            LogHandInfo("Left Hand", subsystem.leftHand);

        // 打印右手信息
        if (m_LogRightHand && rightHandUpdated && subsystem.rightHand.isTracked)
            LogHandInfo("Right Hand", subsystem.rightHand);

        m_LastLogTime = Time.time;
    }

    private void LogHandInfo(string handName, XRHand hand)
    {
        Debug.Log($"=== {handName} Info ===");

        // 打印手部根部姿态 (rootPose) 
        if (m_LogRootPose)
        {
            var rootPose = hand.rootPose;
            Debug.Log($"{handName} Root Pose:");
            Debug.Log($"  Position: {rootPose.position:F3}");
            Debug.Log($"  Rotation: {rootPose.rotation:F3} (Euler: {rootPose.rotation.eulerAngles:F1})");
        }

        // 打印腕部关节姿态， 其实和手部根部是完全一样的
        if (m_LogWristJoint)
        {
            var wristJoint = hand.GetJoint(XRHandJointID.Wrist);
            if (wristJoint.TryGetPose(out var wristPose))
            {
                Debug.Log($"{handName} Wrist Joint:");
                Debug.Log($"  Position: {wristPose.position:F3}");
                Debug.Log($"  Rotation: {wristPose.rotation:F3} (Euler: {wristPose.rotation.eulerAngles:F1})");
                Debug.Log($"  Tracking State: {wristJoint.trackingState}");
            }
            else
            {
                Debug.LogWarning($"{handName} Wrist Joint pose not available");
            }
        }

        if (m_LogPalmJoint)
        {
            // 尝试获取Palm关节，如果失败则尝试Proximal重心
            if (TryGetPalmPose(handName == "Left Hand" ? Handedness.Left : Handedness.Right, out var palmPose))
            {
                Debug.Log($"{handName} Palm Joint (or Proximal Centroid fallback):");
                Debug.Log($"  Position: {palmPose.position:F3}");
                Debug.Log($"  Rotation: {palmPose.rotation:F3} (Euler: {palmPose.rotation.eulerAngles:F1})");
            }
            else
            {
                Debug.LogWarning($"{handName} Palm Joint and Proximal Centroid fallback not available");
            }
        }

        if (ifLogFingerTipV)
        {
            // 打印手指尖关节位置和速度
            Debug.Log($"{handName} finger info:");

            if (TryGetJointPositionAndVelocity(
                    handName == "Left Hand" ? Handedness.Left : Handedness.Right, 
                    XRHandJointID.ThumbTip, out Vector3 thumbPos, out Vector3 thumbVel))
            {
                Debug.Log($"  Thumb tip: Position {thumbPos:F3}, Velocity {thumbVel.magnitude:F3} m/s");
            }

            if (TryGetJointPositionAndVelocity(
                    handName == "Left Hand" ? Handedness.Left : Handedness.Right, 
                    XRHandJointID.IndexTip, out Vector3 indexPos, out Vector3 indexVel))
            {
                Debug.Log($"  Index tip: Position {indexPos:F3}, Velocity {indexVel.magnitude:F3} m/s");
            }
        }

        Debug.Log($"=== End {handName} Info ===\n");
    }

    /// <summary>
    ///     手动更新所有手部信息文本（可从外部调用）
    /// </summary>
    [ContextMenu("Update Hand Info Texts")]
    public void UpdateHandInfoTextsPublic()
    {
        UpdateHandInfoTexts();
    }

    /// <summary>
    /// 手动更新关节速度计算（供外部调用）
    /// </summary>
    [ContextMenu("Update Joint Velocities")]
    public void UpdateJointVelocitiesPublic()
    {
        UpdateJointVelocities();
    }

    /// <summary>
    /// 手动更新关节列表显示（供外部调用）
    /// </summary>
    [ContextMenu("Update Available Joints")]
    public void UpdateAvailableJointsTextPublic()
    {
        m_LastJointListUpdateTime = 0; // 强制更新
        UpdateAvailableJointsText();
    }

    /// <summary>
    ///     获取指定手的根部姿态
    /// </summary>
    /// <param name="handedness">手的类型（左手或右手）</param>
    /// <param name="rootPose">输出的根部姿态</param>
    /// <returns>是否成功获取姿态</returns>
    public bool TryGetHandRootPose(Handedness handedness, out Pose rootPose)
    {
        rootPose = Pose.identity;

        if (m_Subsystem == null || !m_Subsystem.running)
            return false;

        var hand = handedness == Handedness.Left ? m_Subsystem.leftHand : m_Subsystem.rightHand;

        if (!hand.isTracked)
            return false;

        rootPose = hand.rootPose;
        return true;
    }

    /// <summary>
    ///     获取指定手的腕部关节姿态
    /// </summary>
    /// <param name="handedness">手的类型（左手或右手）</param>
    /// <param name="wristPose">输出的腕部姿态</param>
    /// <returns>是否成功获取姿态</returns>
    public bool TryGetWristPose(Handedness handedness, out Pose wristPose)
    {
        wristPose = Pose.identity;

        if (m_Subsystem == null || !m_Subsystem.running)
            return false;

        var hand = handedness == Handedness.Left ? m_Subsystem.leftHand : m_Subsystem.rightHand;

        if (!hand.isTracked)
            return false;

        var wristJoint = hand.GetJoint(XRHandJointID.Wrist);
        return wristJoint.TryGetPose(out wristPose);
    }

    /// <summary>
    ///     获取指定手的Palm关节姿态，如果Palm不可用则使用四指Proximal重心作为备用
    /// </summary>
    /// <param name="handedness">手的类型（左手或右手）</param>
    /// <param name="palmPose">输出的Palm姿态</param>
    /// <returns>是否成功获取姿态</returns>
    public bool TryGetPalmPose(Handedness handedness, out Pose palmPose)
    {
        palmPose = Pose.identity;

        if (m_Subsystem == null || !m_Subsystem.running)
            return false;

        var hand = handedness == Handedness.Left ? m_Subsystem.leftHand : m_Subsystem.rightHand;

        if (!hand.isTracked)
            return false;

        // 如果强制使用Proximal重心
        if (m_ForceUseProximalCentroidAsPalm)
        {
            if (TryGetProximalCentroid(handedness, out var centroidPosition, out _))
            {
                palmPose = new Pose(centroidPosition, Quaternion.identity);
                return true;
            }
            return false;
        }

        // 首先尝试Palm关节
        var palmJoint = hand.GetJoint(XRHandJointID.Palm);
        if (palmJoint.TryGetPose(out palmPose))
        {
            return true;
        }

        // 如果Palm不可用且启用了备用选项，则使用Proximal重心
        if (m_UseProximalCentroidAsPalmFallback)
        {
            if (TryGetProximalCentroid(handedness, out var centroidPosition, out _))
            {
                palmPose = new Pose(centroidPosition, Quaternion.identity);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     获取两只手的腕部位置
    /// </summary>
    /// <param name="leftWristPosition">左手腕部位置</param>
    /// <param name="rightWristPosition">右手腕部位置</param>
    /// <returns>成功获取的手的数量（0, 1, 或 2）</returns>
    public int GetBothWristPositions(out Vector3 leftWristPosition, out Vector3 rightWristPosition)
    {
        leftWristPosition = Vector3.zero;
        rightWristPosition = Vector3.zero;
        var successCount = 0;

        if (TryGetWristPose(Handedness.Left, out var leftWristPose))
        {
            leftWristPosition = leftWristPose.position;
            successCount++;
        }

        if (TryGetWristPose(Handedness.Right, out var rightWristPose))
        {
            rightWristPosition = rightWristPose.position;
            successCount++;
        }

        return successCount;
    }

    /// <summary>
    ///     获取两只手的Palm位置（使用备用机制）
    /// </summary>
    /// <param name="leftPalmPosition">左手Palm位置</param>
    /// <param name="rightPalmPosition">右手Palm位置</param>
    /// <returns>成功获取的手的数量（0, 1, 或 2）</returns>
    public int GetBothPalmPositions(out Vector3 leftPalmPosition, out Vector3 rightPalmPosition)
    {
        leftPalmPosition = Vector3.zero;
        rightPalmPosition = Vector3.zero;
        var successCount = 0;

        if (TryGetPalmPose(Handedness.Left, out var leftPalmPose))
        {
            leftPalmPosition = leftPalmPose.position;
            successCount++;
        }

        if (TryGetPalmPose(Handedness.Right, out var rightPalmPose))
        {
            rightPalmPosition = rightPalmPose.position;
            successCount++;
        }

        return successCount;
    }

    /// <summary>
    ///     计算两只手之间的距离
    /// </summary>
    /// <param name="distance">输出的距离值</param>
    /// <returns>是否成功计算距离（两只手都被跟踪）</returns>
    public bool TryGetHandsDistance(out float distance)
    {
        distance = m_InvalidDistanceValue;

        if (TryGetHandRootPose(Handedness.Left, out var leftHandPose) &&
            TryGetHandRootPose(Handedness.Right, out var rightHandPose))
        {
            distance = Vector3.Distance(leftHandPose.position, rightHandPose.position);
            handsDistance = distance; // 更新当前距离字段
            return true;
        }

        handsDistance = m_InvalidDistanceValue; // 更新为无效值
        return false;
    }

    /// <summary>
    ///     计算两只手Palm之间的距离（使用备用机制）
    /// </summary>
    /// <param name="distance">输出的距离值</param>
    /// <returns>是否成功计算距离（两只手都被跟踪）</returns>
    public bool TryGetPalmDistance(out float distance)
    {
        distance = m_InvalidDistanceValue;

        if (TryGetPalmPose(Handedness.Left, out var leftPalmPose) &&
            TryGetPalmPose(Handedness.Right, out var rightPalmPose))
        {
            distance = Vector3.Distance(leftPalmPose.position, rightPalmPose.position);
            palmDistance = distance; // 更新当前Palm距离字段
            return true;
        }

        palmDistance = m_InvalidDistanceValue; // 更新为无效值
        return false;
    }

    /// <summary>
    ///     计算手掌开合角度（使用备用机制）
    ///     计算由左手Palm、两手腕中点、右手Palm组成的三角形在两手腕中点处的角度
    /// </summary>
    /// <param name="angle">输出的角度值（度）</param>
    /// <returns>是否成功计算角度（两只手都被跟踪）</returns>
    public bool TryGetPalmAngle(out float angle)
    {
        angle = m_InvalidDistanceValue;

        // 获取两只手的腕部位置和Palm位置（使用备用机制）
        if (GetBothWristPositions(out var leftWristPos, out var rightWristPos) == 2 &&
            GetBothPalmPositions(out var leftPalmPos, out var rightPalmPos) == 2)
        {
            // 计算两手腕的中点
            Vector3 wristMiddle = (leftWristPos + rightWristPos) * 0.5f;

            // 计算从中点到左Palm和右Palm的向量
            Vector3 vectorToLeftPalm = leftPalmPos - wristMiddle;
            Vector3 vectorToRightPalm = rightPalmPos - wristMiddle;

            // 计算两个向量之间的角度
            float dotProduct = Vector3.Dot(vectorToLeftPalm.normalized, vectorToRightPalm.normalized);
            
            // 防止浮点精度问题导致的NaN
            dotProduct = Mathf.Clamp(dotProduct, -1f, 1f);
            
            angle = Mathf.Acos(dotProduct) * Mathf.Rad2Deg;
            palmAngle = angle; // 更新当前角度字段
            return true;
        }

        palmAngle = m_InvalidDistanceValue; // 更新为无效值
        return false;
    }

    /// <summary>
    ///     更新所有手部信息文本
    /// </summary>
    private void UpdateHandInfoTexts()
    {
        if ((!m_ShowHandInfo && !m_ShowHandsDistance && !m_ShowPalmDistance && !m_ShowPalmAngle) || 
            m_Subsystem == null || !m_Subsystem.running)
            return;

        // 更新左手位置
        if (m_ShowHandInfo && m_LeftHandPositionText != null)
        {
            if (TryGetHandRootPose(Handedness.Left, out var leftHandPose))
            {
                var position = leftHandPose.position;
                m_LeftHandPositionText.text = $"Pos: {position.x:F2}, {position.y:F2}, {position.z:F2}";
            }
            else
            {
                m_LeftHandPositionText.text = "Untrack";
            }
        }

        // 更新右手位置
        if (m_ShowHandInfo && m_RightHandPositionText != null)
        {
            if (TryGetHandRootPose(Handedness.Right, out var rightHandPose))
            {
                var position = rightHandPose.position;
                m_RightHandPositionText.text = $"Pos: {position.x:F2}, {position.y:F2}, {position.z:F2}";
            }
            else
            {
                m_RightHandPositionText.text = "Untrack";
            }
        }

        // 更新左手旋转
        if (m_ShowHandInfo && m_LeftHandRotationText != null)
        {
            if (TryGetHandRootPose(Handedness.Left, out var leftHandPose))
            {
                var eulerAngles = leftHandPose.rotation.eulerAngles;
                m_LeftHandRotationText.text = $"Rot: {eulerAngles.x:F1}, {eulerAngles.y:F1}, {eulerAngles.z:F1}";
            }
            else
            {
                m_LeftHandRotationText.text = "Untrack";
            }
        }

        // 更新右手旋转
        if (m_ShowHandInfo && m_RightHandRotationText != null)
        {
            if (TryGetHandRootPose(Handedness.Right, out var rightHandPose))
            {
                var eulerAngles = rightHandPose.rotation.eulerAngles;
                m_RightHandRotationText.text = $"Rot: {eulerAngles.x:F1}, {eulerAngles.y:F1}, {eulerAngles.z:F1}";
            }
            else
            {
                m_RightHandRotationText.text = "Untrack";
            }
        }

        // 更新两只手之间的距离
        if (m_ShowHandsDistance && m_HandsDistanceText != null)
        {
            if (IsDistanceValid(handsDistance))
                m_HandsDistanceText.text = $"Hands: {handsDistance:F3}m";
            else
                m_HandsDistanceText.text = "Hands: N/A";
        }

        // 更新两只手Palm之间的距离（使用备用机制）
        if (m_ShowPalmDistance && m_PalmDistanceText != null)
        {
            if (IsDistanceValid(palmDistance))
            {
                string suffix = "";
                if (m_ForceUseProximalCentroidAsPalm)
                    suffix = " (Centroid)";
                else if (m_UseProximalCentroidAsPalmFallback)
                    suffix = " (Auto)";
                
                m_PalmDistanceText.text = $"Palm: {palmDistance:F3}m{suffix}";
            }
            else
                m_PalmDistanceText.text = "Palm: N/A";
        }

        // 更新手掌开合角度（使用备用机制）
        if (m_ShowPalmAngle && m_PalmAngleText != null)
        {
            if (IsAngleValid(palmAngle))
            {
                string suffix = "";
                if (m_ForceUseProximalCentroidAsPalm)
                    suffix = " (Centroid)";
                else if (m_UseProximalCentroidAsPalmFallback)
                    suffix = " (Auto)";
                
                m_PalmAngleText.text = $"Angle: {palmAngle:F1}°{suffix}";
            }
            else
                m_PalmAngleText.text = "Angle: N/A";
        }
    }

    /// <summary>
    ///     获取指定手指尖关节的位置和速度
    /// </summary>
    /// <param name="handedness">手的类型（左手或右手）</param>
    /// <param name="jointId">指定的关节ID（例如 XRHandJointID.ThumbTip）</param>
    /// <param name="position">输出的关节位置</param>
    /// <param name="velocity">输出的关节速度</param>
    /// <returns>是否成功获取数据</returns>
    public bool TryGetJointPositionAndVelocity(Handedness handedness, XRHandJointID jointId,
        out Vector3 position, out Vector3 velocity)
    {
        position = Vector3.zero;
        velocity = Vector3.zero;

        if (m_Subsystem == null || !m_Subsystem.running)
            return false;

        var hand = handedness == Handedness.Left ? m_Subsystem.leftHand : m_Subsystem.rightHand;

        if (!hand.isTracked)
            return false;

        var joint = hand.GetJoint(jointId);
        var hasPosition = joint.TryGetPose(out var pose);

        if (hasPosition) 
        {
            position = pose.position;

            // 从缓存中获取速度
            var velocitiesDict = handedness == Handedness.Left ? m_LeftJointVelocities : m_RightJointVelocities;
            if (velocitiesDict.TryGetValue(jointId, out Vector3 cachedVelocity))
            {
                velocity = cachedVelocity;
                return true;
            }
        }

        return hasPosition;
    }

    /// <summary>
    ///     获取指定手的所有手指尖关节数据
    /// </summary>
    /// <param name="handedness">手的类型（左手或右手）</param>
    /// <param name="positions">输出的各指尖位置数组（按拇指、食指、中指、无名指、小指顺序）</param>
    /// <param name="velocities">输出的各指尖速度数组（按拇指、食指、中指、无名指、小指顺序）</param>
    /// <returns>成功获取的手指尖数量（0-5）</returns>
    public int GetAllFingertipsData(Handedness handedness, Vector3[] positions, Vector3[] velocities)
    {
        if (positions == null || velocities == null || positions.Length < 5 || velocities.Length < 5)
        {
            Debug.LogError("Array length must be at least 5");
            return 0;
        }

        if (m_Subsystem == null || !m_Subsystem.running)
            return 0;

        var hand = handedness == Handedness.Left ? m_Subsystem.leftHand : m_Subsystem.rightHand;

        if (!hand.isTracked)
            return 0;

        // 定义指尖关节ID数组
        var tipJointIds = new[]
        {
            XRHandJointID.ThumbTip, // 拇指尖
            XRHandJointID.IndexTip, // 食指尖
            XRHandJointID.MiddleTip, // 中指尖
            XRHandJointID.RingTip, // 无名指尖
            XRHandJointID.LittleTip // 小指尖
        };

        var successCount = 0;

        // 获取每个指尖的数据
        for (var i = 0; i < tipJointIds.Length; i++)
        {
            var joint = hand.GetJoint(tipJointIds[i]);
            var jointId = tipJointIds[i];

            var hasPosition = joint.TryGetPose(out var pose);

            if (hasPosition)
            {
                positions[i] = pose.position;

                // 从缓存中获取速度
                var velocitiesDict = handedness == Handedness.Left ? m_LeftJointVelocities : m_RightJointVelocities;
                if (velocitiesDict.TryGetValue(jointId, out Vector3 cachedVelocity))
                {
                    velocities[i] = cachedVelocity;
                    successCount++;
                }
                else
                {
                    velocities[i] = Vector3.zero;
                }
            }
        }

        return successCount;
    }
}
