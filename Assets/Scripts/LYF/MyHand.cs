using System.Collections.Generic;
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

    [SerializeField] [Tooltip("是否在文本组件中显示手部位置和旋转")]
    private bool m_ShowHandInfo = true;

    [SerializeField] [Tooltip("是否显示两只手的距离")]
    private bool m_ShowHandsDistance = true;

    private float m_LastLogTime;

    private XRHandSubsystem m_Subsystem;

    // 存储关节位置历史记录用于计算速度
    private Dictionary<XRHandJointID, Vector3> m_LeftJointLastPositions = new Dictionary<XRHandJointID, Vector3>();
    private Dictionary<XRHandJointID, Vector3> m_RightJointLastPositions = new Dictionary<XRHandJointID, Vector3>();
    private Dictionary<XRHandJointID, Vector3> m_LeftJointVelocities = new Dictionary<XRHandJointID, Vector3>();
    private Dictionary<XRHandJointID, Vector3> m_RightJointVelocities = new Dictionary<XRHandJointID, Vector3>();
    private float m_LastVelocityUpdateTime;

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
    ///     获取当前双手之间的距离（米）
    ///     如果无法获取距离（手不可见），则返回0
    /// </summary>
    public float handsDistance { get; private set; }

    protected void Update()
    {
        // 如果已经有运行中的子系统，持续更新双手距离
        if (m_Subsystem != null && m_Subsystem.running)
        {
            // 即使没有UI更新，也持续更新双手距离数据用于外部访问
            TryGetHandsDistance(out _);
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
        m_LeftJointLastPositions.Clear();
        m_RightJointLastPositions.Clear();
        m_LeftJointVelocities.Clear();
        m_RightJointVelocities.Clear();
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
        if (handsDistance <= 0)
            return 0;

        return Mathf.Clamp01((handsDistance - minDistance) / (maxDistance - minDistance));
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
        var jointIds = new[]
        {
            XRHandJointID.Wrist,
            XRHandJointID.ThumbTip,
            XRHandJointID.IndexTip,
            XRHandJointID.MiddleTip,
            XRHandJointID.RingTip,
            XRHandJointID.LittleTip
        };

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

    private void OnUpdatedHands(XRHandSubsystem subsystem, XRHandSubsystem.UpdateSuccessFlags updateSuccessFlags,
        XRHandSubsystem.UpdateType updateType)
    {
        // 更新关节速度
        UpdateJointVelocities();

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

        // 打印腕部关节姿态
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
    ///     计算两只手之间的距离
    /// </summary>
    /// <param name="distance">输出的距离值</param>
    /// <returns>是否成功计算距离（两只手都被跟踪）</returns>
    public bool TryGetHandsDistance(out float distance)
    {
        distance = 0f;

        if (TryGetHandRootPose(Handedness.Left, out var leftHandPose) &&
            TryGetHandRootPose(Handedness.Right, out var rightHandPose))
        {
            distance = Vector3.Distance(leftHandPose.position, rightHandPose.position);
            handsDistance = distance; // 更新当前距离字段
            return true;
        }

        return false;
    }



    /// <summary>
    ///     更新所有手部信息文本
    /// </summary>
    private void UpdateHandInfoTexts()
    {
        if ((!m_ShowHandInfo && !m_ShowHandsDistance) || m_Subsystem == null || !m_Subsystem.running)
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
            if (TryGetHandsDistance(out var distance))
                m_HandsDistanceText.text = $"{distance:F3}m";
            else
                m_HandsDistanceText.text = "N/A";
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
