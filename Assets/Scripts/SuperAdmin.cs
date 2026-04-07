using System;
using UnityEngine;
using Sentry.Unity;
using UnityEngine.UI; // 添加这行来支持RawImage
using Unity.InferenceEngine; // 添加这行来支持ModelAsset
using UnityEngine.XR.ARFoundation; // ARMeshManager

public class SuperAdmin : MonoBehaviour
{
    [Header("设置区")]
    [Tooltip("控制测试UI界面是否会显示")]
    public bool isDebug=true;


    [Header("Hand相关配置")] 
    [Tooltip("如果关闭, 所有涉及Hand相关的功能都将关闭")]
    public bool isEnableHandDetection = true;
    public GameObject handFuncAll;
    [Tooltip("是否在UI上显示每个手指指向的物体类别")]
    public bool isShowHandRayHitClass = true;
    public GameObject[] handRayHitClassTexts;
    public GameObject righthandShiZhiRayHitIndicator;
    [Tooltip("是否显示手指投影调试信息")]
    public bool showDebugInfo = true;
    
    
    [Header("蓝牙BLE相关配置")] 
    public bool isEnableBLE=true;
    
    [Tooltip("是否在控制台显示BLE调试信息")]
    public bool showBLEDebugInfo = true;
    
    
    [Header("平台信息")]
    [Tooltip("指示当前运行平台")]
    [SerializeField] private PlatformType currentPlatform;
    
    [Header("Leave me alone")]
    public BLESendJointV Ble;

    [Tooltip("所有UI元素的父类")]
    public GameObject AllUI;
    
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
        
        AllUI.SetActive(isDebug);

#if UNITY_VISIONOS && !UNITY_EDITOR
        currentPlatform = PlatformType.VisionOS;
        isEnableBLE = true;
#else
        currentPlatform = PlatformType.UnityEditor;
#endif

        isEnableHandDetection=isEnableHandDetection || currentPlatform==PlatformType.VisionOS;
        handFuncAll.SetActive(isEnableHandDetection);
        isShowHandRayHitClass=isShowHandRayHitClass&&isEnableHandDetection;
        righthandShiZhiRayHitIndicator.SetActive(isShowHandRayHitClass);
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

    }
    

    public void QuitApplication()
    {
        Application.Quit();
    }
    
}