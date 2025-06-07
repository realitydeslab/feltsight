using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Management;
using TMPro;


/// <summary>
/// 这个组件用于打印手部根部姿态和腕部关节的位置旋转信息
/// 发现双手合十的时候距离是0.9, 但是阿多给的时候距离会更短
/// 我自己的手张开角度对应的双手距离是 0.4~0.8
/// </summary>
public class MyHand : MonoBehaviour
{
    [SerializeField]
    [Tooltip("是否启用位置信息打印")]
    bool m_EnableLogging = true;

    [SerializeField]
    [Tooltip("打印信息的间隔时间（秒）")]
    float m_LogInterval = 0.5f;

    [SerializeField]
    [Tooltip("是否打印左手信息")]
    bool m_LogLeftHand = true;

    [SerializeField]
    [Tooltip("是否打印右手信息")]
    bool m_LogRightHand = true;

    [SerializeField]
    [Tooltip("是否打印关节半径信息")]
    bool m_LogJointRadius = true;

    [SerializeField]
    [Tooltip("是否打印手部根部姿态")]
    bool m_LogRootPose = true;

    [SerializeField]
    [Tooltip("是否打印腕部关节姿态")]
    bool m_LogWristJoint = true;
    
    /// <summary>
    /// 是否启用位置信息打印
    /// </summary>
    public bool enableLogging
    {
        get => m_EnableLogging;
        set => m_EnableLogging = value;
    }

    /// <summary>
    /// 打印信息的间隔时间
    /// </summary>
    public float logInterval
    {
        get => m_LogInterval;
        set => m_LogInterval = Mathf.Max(0.1f, value);
    }

    [SerializeField]
    [Tooltip("用于显示左手位置的文本组件")]
    private TextMeshProUGUI m_LeftHandPositionText;

    [SerializeField]
    [Tooltip("用于显示右手位置的文本组件")]
    private TextMeshProUGUI m_RightHandPositionText;

    [SerializeField]
    [Tooltip("用于显示左手旋转的文本组件")]
    private TextMeshProUGUI m_LeftHandRotationText;

    [SerializeField]
    [Tooltip("用于显示右手旋转的文本组件")]
    private TextMeshProUGUI m_RightHandRotationText;

    [SerializeField]
    [Tooltip("用于显示两只手距离的文本组件")]
    private TextMeshProUGUI m_HandsDistanceText;

    [SerializeField]
    [Tooltip("是否在文本组件中显示手部位置和旋转")]
    private bool m_ShowHandInfo = true;

    [SerializeField]
    [Tooltip("是否显示两只手的距离")]
    private bool m_ShowHandsDistance = true;

    /// <summary>
    /// 是否显示手部位置和旋转信息
    /// </summary>
    public bool showHandInfo
    {
        get => m_ShowHandInfo;
        set => m_ShowHandInfo = value;
    }

    /// <summary>
    /// 是否显示两只手的距离
    /// </summary>
    public bool showHandsDistance
    {
        get => m_ShowHandsDistance;
        set => m_ShowHandsDistance = value;
    }

    /// <summary>
    /// 获取当前双手之间的距离（米）
    /// 如果无法获取距离（手不可见），则返回0
    /// </summary>
    public float handsDistance
    {
        get => m_CurrentHandsDistance;
    }

    /// <summary>
    /// 获取双手距离的归一化值(0-1)，基于给定的最小和最大距离
    /// </summary>
    /// <param name="minDistance">最小距离参考值（默认0.4米）</param>
    /// <param name="maxDistance">最大距离参考值（默认0.8米）</param>
    /// <returns>归一化的距离值，范围0-1，如果双手不可见则返回0</returns>
    public float GetNormalizedHandsDistance(float minDistance = 0.4f, float maxDistance = 0.8f)
    {
        if (m_CurrentHandsDistance <= 0)
            return 0;

        return Mathf.Clamp01((m_CurrentHandsDistance - minDistance) / (maxDistance - minDistance));
    }
    
    XRHandSubsystem m_Subsystem;
    float m_LastLogTime;
    private float m_CurrentHandsDistance = 0f;

    static readonly List<XRHandSubsystem> s_SubsystemsReuse = new List<XRHandSubsystem>();

    /// <summary>
    /// See <see cref="MonoBehaviour"/>.
    /// </summary>
    protected void OnEnable()
    {
        m_LastLogTime = Time.time;
    }

    /// <summary>
    /// See <see cref="MonoBehaviour"/>.
    /// </summary>
    protected void OnDisable()
    {
        if (m_Subsystem != null)
        {
            m_Subsystem.updatedHands -= OnUpdatedHands;
            m_Subsystem = null;
        }
    }

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

        if (foundRunningHandSubsystem)
        {
            SubscribeHandSubsystem();
        }
    }
    
    void SubscribeHandSubsystem()
    {
        if (m_Subsystem == null)
            return;

        m_Subsystem.updatedHands += OnUpdatedHands;
    }

    void UnsubscribeHandSubsystem()
    {
        if (m_Subsystem == null)
            return;

        m_Subsystem.updatedHands -= OnUpdatedHands;
    }
    
    void OnUpdatedHands(XRHandSubsystem subsystem, XRHandSubsystem.UpdateSuccessFlags updateSuccessFlags, XRHandSubsystem.UpdateType updateType)
    {
        // 只在启用日志记录且达到间隔时间时打印
        if (!m_EnableLogging || Time.time - m_LastLogTime < m_LogInterval)
        {
            // 即使不打印日志，也更新文本
            UpdateHandInfoTexts();
            return;
        }

        // 检查是否有手部数据更新成功
        bool leftHandUpdated = (updateSuccessFlags & XRHandSubsystem.UpdateSuccessFlags.LeftHandJoints) != 0;
        bool rightHandUpdated = (updateSuccessFlags & XRHandSubsystem.UpdateSuccessFlags.RightHandJoints) != 0;

        // 更新所有手部信息文本
        UpdateHandInfoTexts();

        // 打印左手信息
        if (m_LogLeftHand && leftHandUpdated && subsystem.leftHand.isTracked)
        {
            LogHandInfo("Left Hand", subsystem.leftHand);
        }

        // 打印右手信息
        if (m_LogRightHand && rightHandUpdated && subsystem.rightHand.isTracked)
        {
            LogHandInfo("Right Hand", subsystem.rightHand);
        }

        m_LastLogTime = Time.time;
    }
    
    
    void LogHandInfo(string handName, XRHand hand)
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
            if (wristJoint.TryGetPose(out Pose wristPose))
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

        // 打印关节半径信息
        if (m_LogJointRadius)
        {
            // 遍历一些主要关节以获取半径
            Debug.Log($"{handName} Joint Radius Information:");
            
            LogJointRadius(handName, hand.GetJoint(XRHandJointID.Wrist));
            // LogJointRadius(handName, hand, XRHandJointID.ThumbTip, "拇指尖");
            // LogJointRadius(handName, hand, XRHandJointID.IndexTip, "食指尖");
            // LogJointRadius(handName, hand, XRHandJointID.MiddleTip, "中指尖");
            // LogJointRadius(handName, hand, XRHandJointID.RingTip, "无名指尖");
            // LogJointRadius(handName, hand, XRHandJointID.LittleTip, "小指尖");
        }

        Debug.Log($"=== End {handName} Info ===\n");
    }
    
    /// <summary>
    /// 手动触发一次信息打印（用于调试）
    /// </summary>
    [ContextMenu("Log Hand Info Now")]
    public void LogHandInfoNow()
    {
        if (m_Subsystem == null || !m_Subsystem.running)
        {
            Debug.LogWarning("Hand subsystem is not running");
            return;
        }

        if (m_LogLeftHand && m_Subsystem.leftHand.isTracked)
        {
            LogHandInfo("Left Hand", m_Subsystem.leftHand);
        }

        if (m_LogRightHand && m_Subsystem.rightHand.isTracked)
        {
            LogHandInfo("Right Hand", m_Subsystem.rightHand);
        }

        // 同时更新UI文本
        UpdateHandInfoTexts();
    }

    /// <summary>
    /// 手动更新所有手部信息文本（可从外部调用）
    /// </summary>
    [ContextMenu("Update Hand Info Texts")]
    public void UpdateHandInfoTextsPublic()
    {
        UpdateHandInfoTexts();
    }

    /// <summary>
    /// 手动触发打印所有关节半径信息（用于调试）
    /// </summary>
    [ContextMenu("Print All Joint Radius")]
    public void PrintAllJointRadius()
    {
        if (m_Subsystem == null || !m_Subsystem.running)
        {
            Debug.LogWarning("手部子系统未运行");
            return;
        }

        // 定义要检查的关节ID列表
        XRHandJointID[] jointIds = new XRHandJointID[]
        {
            XRHandJointID.Wrist,
            XRHandJointID.Palm,
            XRHandJointID.ThumbMetacarpal,
            XRHandJointID.ThumbProximal,
            XRHandJointID.ThumbDistal,
            XRHandJointID.ThumbTip,
            XRHandJointID.IndexMetacarpal,
            XRHandJointID.IndexProximal,
            XRHandJointID.IndexIntermediate,
            XRHandJointID.IndexDistal,
            XRHandJointID.IndexTip,
            XRHandJointID.MiddleMetacarpal,
            XRHandJointID.MiddleProximal,
            XRHandJointID.MiddleIntermediate,
            XRHandJointID.MiddleDistal,
            XRHandJointID.MiddleTip,
            XRHandJointID.RingMetacarpal,
            XRHandJointID.RingProximal,
            XRHandJointID.RingIntermediate,
            XRHandJointID.RingDistal,
            XRHandJointID.RingTip,
            XRHandJointID.LittleMetacarpal,
            XRHandJointID.LittleProximal,
            XRHandJointID.LittleIntermediate,
            XRHandJointID.LittleDistal,
            XRHandJointID.LittleTip
        };

        // 中文关节名称对应表
        Dictionary<XRHandJointID, string> jointNames = new Dictionary<XRHandJointID, string>()
        {
            { XRHandJointID.Wrist, "腕部" },
            { XRHandJointID.Palm, "掌心" },
            { XRHandJointID.ThumbMetacarpal, "拇指掌骨" },
            { XRHandJointID.ThumbProximal, "拇指近节" },
            { XRHandJointID.ThumbDistal, "拇指远节" },
            { XRHandJointID.ThumbTip, "拇指尖" },
            { XRHandJointID.IndexMetacarpal, "食指掌骨" },
            { XRHandJointID.IndexProximal, "食指近节" },
            { XRHandJointID.IndexIntermediate, "食指中节" },
            { XRHandJointID.IndexDistal, "食指远节" },
            { XRHandJointID.IndexTip, "食指尖" },
            { XRHandJointID.MiddleMetacarpal, "中指掌骨" },
            { XRHandJointID.MiddleProximal, "中指近节" },
            { XRHandJointID.MiddleIntermediate, "中指中节" },
            { XRHandJointID.MiddleDistal, "中指远节" },
            { XRHandJointID.MiddleTip, "中指尖" },
            { XRHandJointID.RingMetacarpal, "无名指掌骨" },
            { XRHandJointID.RingProximal, "无名指近节" },
            { XRHandJointID.RingIntermediate, "无名指中节" },
            { XRHandJointID.RingDistal, "无名指远节" },
            { XRHandJointID.RingTip, "无名指尖" },
            { XRHandJointID.LittleMetacarpal, "小指掌骨" },
            { XRHandJointID.LittleProximal, "小指近节" },
            { XRHandJointID.LittleIntermediate, "小指中节" },
            { XRHandJointID.LittleDistal, "小指远节" },
            { XRHandJointID.LittleTip, "小指尖" }
        };

        // 打印左手关节半径
        if (m_LogLeftHand && m_Subsystem.leftHand.isTracked)
        {
            Debug.Log("=== 左手关节半径信息 ===");
            foreach (var jointId in jointIds)
            {
                string jointName = jointNames.ContainsKey(jointId) ? jointNames[jointId] : jointId.ToString();
                if (TryGetJointRadius(Handedness.Left, jointId, out float radius))
                {
                    Debug.Log($"{jointName}: {radius:F4}米");
                }
                else
                {
                    Debug.Log($"{jointName}: 数据不可用");
                }
            }
            Debug.Log("=== 左手关节半径信息结束 ===\n");
        }

        // 打印右手关节半径
        if (m_LogRightHand && m_Subsystem.rightHand.isTracked)
        {
            Debug.Log("=== 右手关节半径信息 ===");
            foreach (var jointId in jointIds)
            {
                string jointName = jointNames.ContainsKey(jointId) ? jointNames[jointId] : jointId.ToString();
                if (TryGetJointRadius(Handedness.Right, jointId, out float radius))
                {
                    Debug.Log($"{jointName}: {radius:F4}米");
                }
                else
                {
                    Debug.Log($"{jointName}: 数据不可用");
                }
            }
            Debug.Log("=== 右手关节半径信息结束 ===\n");
        }
    }
    /// <summary>
        /// 获取指定手的根部姿态
        /// </summary>
        /// <param name="handedness">手的类型（左手或右手）</param>
        /// <param name="rootPose">输出的根部姿态</param>
        /// <returns>是否成功获取姿态</returns>
        public bool TryGetHandRootPose(Handedness handedness, out Pose rootPose)
        {
            rootPose = Pose.identity;

            if (m_Subsystem == null || !m_Subsystem.running)
                return false;

            XRHand hand = handedness == Handedness.Left ? m_Subsystem.leftHand : m_Subsystem.rightHand;
            
            if (!hand.isTracked)
                return false;

            rootPose = hand.rootPose;
            return true;
        }

        /// <summary>
        /// 获取指定手的腕部关节姿态
        /// </summary>
        /// <param name="handedness">手的类型（左手或右手）</param>
        /// <param name="wristPose">输出的腕部姿态</param>
        /// <returns>是否成功获取姿态</returns>
        public bool TryGetWristPose(Handedness handedness, out Pose wristPose)
        {
            wristPose = Pose.identity;

            if (m_Subsystem == null || !m_Subsystem.running)
                return false;

            XRHand hand = handedness == Handedness.Left ? m_Subsystem.leftHand : m_Subsystem.rightHand;
            
            if (!hand.isTracked)
                return false;

            var wristJoint = hand.GetJoint(XRHandJointID.Wrist);
            return wristJoint.TryGetPose(out wristPose);
        }

        /// <summary>
        /// 获取两只手的腕部位置
        /// </summary>
        /// <param name="leftWristPosition">左手腕部位置</param>
        /// <param name="rightWristPosition">右手腕部位置</param>
        /// <returns>成功获取的手的数量（0, 1, 或 2）</returns>
        public int GetBothWristPositions(out Vector3 leftWristPosition, out Vector3 rightWristPosition)
        {
            leftWristPosition = Vector3.zero;
            rightWristPosition = Vector3.zero;
            int successCount = 0;

            if (TryGetWristPose(Handedness.Left, out Pose leftWristPose))
            {
                leftWristPosition = leftWristPose.position;
                successCount++;
            }

            if (TryGetWristPose(Handedness.Right, out Pose rightWristPose))
            {
                rightWristPosition = rightWristPose.position;
                successCount++;
            }

            return successCount;
        }

        /// <summary>
        /// 计算两只手之间的距离
        /// </summary>
        /// <param name="distance">输出的距离值</param>
        /// <returns>是否成功计算距离（两只手都被跟踪）</returns>
        public bool TryGetHandsDistance(out float distance)
        {
            distance = 0f;

            if (TryGetHandRootPose(Handedness.Left, out Pose leftHandPose) && 
                TryGetHandRootPose(Handedness.Right, out Pose rightHandPose))
            {
                distance = Vector3.Distance(leftHandPose.position, rightHandPose.position);
                m_CurrentHandsDistance = distance; // 更新当前距离字段
                return true;
            }

            return false;
        }
    
    /// <summary>
    /// 打印指定关节的半径信息
    /// </summary>
    /// <param name="handName">手的名称</param>
    /// <param name="hand">手的引用</param>
    /// <param name="jointId">要查询的关节ID</param>
    /// <param name="jointName">关节的中文名称</param>
    private void LogJointRadius(string handName, XRHandJoint joint)
    {
        if (joint.TryGetRadius(out float radius))
        {
            Debug.Log($"  {joint}关节半径: {radius:F4}米");
        }
        else
        {
            Debug.Log($"  {joint}关节半径数据不可用");
        }
    }

    /// <summary>
    /// 尝试获取指定手部关节的半径
    /// </summary>
    /// <param name="handedness">手的类型（左手或右手）</param>
    /// <param name="jointId">要查询的关节ID</param>
    /// <param name="radius">输出的关节半径</param>
    /// <returns>是否成功获取半径</returns>
    public bool TryGetJointRadius(Handedness handedness, XRHandJointID jointId, out float radius)
    {
        radius = 0f;

        if (m_Subsystem == null || !m_Subsystem.running)
            return false;

        XRHand hand = handedness == Handedness.Left ? m_Subsystem.leftHand : m_Subsystem.rightHand;

        if (!hand.isTracked)
            return false;

        var joint = hand.GetJoint(jointId);
        return joint.TryGetRadius(out radius);
    }

    /// <summary>
    /// 更新所有手部信息文本
    /// </summary>
    private void UpdateHandInfoTexts()
    {
        if ((!m_ShowHandInfo && !m_ShowHandsDistance) || m_Subsystem == null || !m_Subsystem.running)
            return;

        // 更新左手位置
        if (m_ShowHandInfo && m_LeftHandPositionText != null)
        {
            if (TryGetHandRootPose(Handedness.Left, out Pose leftHandPose))
            {
                Vector3 position = leftHandPose.position;
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
            if (TryGetHandRootPose(Handedness.Right, out Pose rightHandPose))
            {
                Vector3 position = rightHandPose.position;
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
            if (TryGetHandRootPose(Handedness.Left, out Pose leftHandPose))
            {
                Vector3 eulerAngles = leftHandPose.rotation.eulerAngles;
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
            if (TryGetHandRootPose(Handedness.Right, out Pose rightHandPose))
            {
                Vector3 eulerAngles = rightHandPose.rotation.eulerAngles;
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
            if (TryGetHandsDistance(out float distance))
            {
                m_HandsDistanceText.text = $"{distance:F3}m";
            }
            else
            {
                m_HandsDistanceText.text = "N/A";
            }
        }
    }
    
    
}