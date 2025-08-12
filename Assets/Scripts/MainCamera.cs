using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class MainCamera : MonoBehaviour
{
    public Texture2D _texture; // 当前使用的纹理
    EnterpriseCameraAccessManager ecam;
    
    private int height;
    private int width;
    
    [Tooltip("RawImage component which will be used to draw resuls.")]
    [SerializeField]
    protected RawImage ImageUI;

    void OnEnable()
    {
        
        // height = firstInput.shape[5];
        // width = firstInput.shape[6];
        Debug.Log($"Model input size: {width}x{height}");
    }

    [Header("Performance Settings")]
    [SerializeField] private float segmentationInterval = 0.5f; // 每0.5秒执行一次分割
    [SerializeField] private bool enableAsync = true; // 启用异步处理
    
    private bool isProcessing = false;
    private Coroutine segmentationCoroutine;
    
}
