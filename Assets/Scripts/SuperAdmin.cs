using System;
using UnityEngine;
using Sentry.Unity;
using UnityEngine.UI; // 添加这行来支持RawImage
using Unity.InferenceEngine; // 添加这行来支持ModelAsset

public class SuperAdmin : MonoBehaviour
{
    [Header("设置区")]
    public bool isDebug=true;
    public bool isEnableBLE=true;
    public bool isEnableVFX=true;
    
    [Header("Leave me alone")]
    public BLESendJointV Ble;

    [Header("视觉相关Debug")] 
    [Tooltip("是否显示实时的摄像头画面")]
    public bool isShowCameraImage = true;
    
    [Tooltip("要控制显示/隐藏的RawImage组件")]
    public GameObject cameraRawImage;
    
    public MainCamera MainCam;
    public RunYOLO Yolo;
    [Tooltip("是否plot yolo识别的结果")]
    public bool isShowYoloResult = true;
    [Tooltip("yolo识别的结果的显示控件")]
    public GameObject yoloResultImg;

    [Header("Hand相关Debug")] 
    [Tooltip("如果关闭, 所有涉及Hand相关的功能都将关闭")]
    public bool isEnableHandDetection = true;
    public GameObject handFuncAll;
    [Tooltip("是否在UI上显示每个手指指向的物体类别")]
    public bool isShowHandRayHitClass = true;
    public GameObject[] handRayHitClassTexts;
    public GameObject righthandShiZhiRayHitIndicator;
    
    [Header("YOLO模型配置")]
    [Tooltip("YOLO模型文件(.onnx)")]
    public ModelAsset yoloModelAsset;
    
    [Tooltip("模型输入宽度")]
    public int modelInputWidth = 640;
    
    [Tooltip("模型输入高度")]
    public int modelInputHeight = 640;
    
    [Tooltip("IOU阈值，用于非最大抑制")]
    [Range(0f, 1f)]
    public float iouThreshold = 0.5f;
    
    [Tooltip("置信度阈值，用于非最大抑制")]
    [Range(0f, 1f)]
    public float scoreThreshold = 0.5f;
    
    [Tooltip("YOLO推理间隔时间(秒)")]
    [Range(0.05f, 2f)]
    public float inferenceInterval = 0.1f;
    
    [Tooltip("是否输出YOLO推理的详细日志信息(包括检测框数量、坐标等)")]
    public bool enableYoloDebugLogs = false;
    
    [Header("平台信息")]
    [Tooltip("指示当前运行平台")]
    [SerializeField] private PlatformType currentPlatform;
    public enum PlatformType
    {
        UnityEditor,
        VisionOS
    }
    
    // 只读公共属性，外部可以访问但不能修改
    public PlatformType CurrentPlatform { get { return currentPlatform; } }
    
    // 单例实例
    public static SuperAdmin superAdmin;

    void Start()
    {
        // 初始化时根据isShowCameraImage设置RawImage的显示状态
        UpdateCameraImageVisibility();
        
        // 输出当前平台信息
        Debug.Log("Current platform: " + currentPlatform);

#if UNITY_VISIONOS && !UNITY_EDITOR
        
        SentrySdk.CaptureMessage("Felsight Start on VP");
        #endif
#if UNITY_EDITOR
        SentrySdk.CaptureMessage("Felsight Start on Editor");
        #endif
        
    }

    private void Awake()
    {
        // 确保单例唯一性
        if (superAdmin != null && superAdmin != this)
        {
            Destroy(gameObject);
            return;
        }
        
        superAdmin = this;
        DontDestroyOnLoad(gameObject);

#if UNITY_VISIONOS && !UNITY_EDITOR
        currentPlatform = PlatformType.VisionOS;
        isEnableBLE = true;
#else
        currentPlatform = PlatformType.UnityEditor;
#endif
        isShowCameraImage=currentPlatform==PlatformType.VisionOS;
        MainCam.enabled = currentPlatform==PlatformType.VisionOS;
        Yolo.enabled = currentPlatform==PlatformType.UnityEditor;
        yoloResultImg.SetActive(isShowYoloResult);
        isEnableHandDetection=isEnableHandDetection || currentPlatform==PlatformType.VisionOS;
        handFuncAll.SetActive(isEnableHandDetection);
        isShowHandRayHitClass=isShowHandRayHitClass&&isEnableHandDetection;
        righthandShiZhiRayHitIndicator.SetActive(isShowHandRayHitClass&&isShowYoloResult);
        foreach (var text in handRayHitClassTexts)
        {
            text.SetActive(isShowHandRayHitClass);
        }
        
        
        if (!Ble)
        {
            Debug.LogError("BLE component is not assigned in SuperAdmin script!");
        }
        else
        {
            Ble.enabled = isEnableBLE;
            Debug.Log("BLE component is assigned and enabled: " + isEnableBLE);
        }
    }

    // Update is called once per frame
    void Update()
    {
        // 每帧检查是否需要更新摄像头图像的可见性
        UpdateCameraImageVisibility();
    }
    
    /// <summary>
    /// 根据isShowCameraImage变量更新RawImage的可见性
    /// </summary>
    private void UpdateCameraImageVisibility()
    {
        
        // 如果找到了RawImage组件，则根据isShowCameraImage变量控制其可见性
        if (cameraRawImage != null)
        {
            cameraRawImage.gameObject.SetActive(isShowCameraImage);
        }
        else
        {
            Debug.LogWarning("No RawImage component found for camera display control.");
        }
    }

    public void QuitApplication()
    {
        Application.Quit();
    }
    
}