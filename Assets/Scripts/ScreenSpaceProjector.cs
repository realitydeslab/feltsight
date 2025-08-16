using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Hands;
using TMPro;

public class ScreenSpaceProjector : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private HandRaycaster handRaycaster;
    [SerializeField] private Camera mainCamera;
    [SerializeField] private MainCamera yoloMainCamera; // 添加对MainCamera的引用以获取YOLO检测结果
    
    [Header("Reference Transform Setup")]
    [SerializeField] private Transform originTransform;
    [SerializeField] private Transform xAxisTransform; // x=1 position
    [SerializeField] private Transform yAxisTransform; // y=1 position
    
    [Header("Sphere Generation")]
    [SerializeField] private GameObject spherePrefab;
    [SerializeField] private float sphereSize = 0.05f;
    [SerializeField] private Material sphereMaterial;
    
    
    private GameObject currentSphere;
    
    [Header("Finger Classification")]
    [SerializeField] public string[] fingerClasses = new string[10]; // 存储10个手指的类别信息 [左手拇指到小指, 右手拇指到小指]
    
    [Header("UI Display")]
    [SerializeField] private TextMeshProUGUI[] fingerClassTexts = new TextMeshProUGUI[10]; // UI文本控件数组，对应10个手指
    // 手指名称数组，用于显示
    private readonly string[] fingerNames = new string[]
    {
        "L-Thumb", "L-Index", "L-Middle", "L-Ring", "L-Pinky",
        "R-Thumb", "R-Index", "R-Middle", "R-Ring", "R-Pinky"
    };
    
    void Start()
    {
        if (mainCamera == null)
        {
            Debug.LogError("ScreenSpaceProjector: No camera found!");
            return;
        }
        
        // 自动查找场景中的MainCamera组件
        if (yoloMainCamera == null)
        {
            yoloMainCamera = FindFirstObjectByType<MainCamera>();
            if (yoloMainCamera == null)
            {
                Debug.LogError("ScreenSpaceProjector: No YOLO MainCamera found in scene!");
            }
        }
        
        // Setup reference transforms if not assigned
        SetupReferenceTransforms();
        
    }
    
    void Update()
    {
        ProcessAllFingerClasses();
    }
    
    /// <summary>
    /// 处理所有手指的类别检测，更新fingerClasses数组
    /// fingerClasses数组索引: 0-4左手(拇指到小指), 5-9右手(拇指到小指)
    /// </summary>
    private void ProcessAllFingerClasses()
    {
        // 处理左手 (索引0-4)
        for (int fingerIndex = 0; fingerIndex < 5; fingerIndex++)
        {
            if (handRaycaster.TryGetFingerHit(Handedness.Left, fingerIndex, out RaycastHit hit))
            {
                Vector2 screenPoint = ProjectToScreenSpace(hit.point);
                string classification = GetPointedObjectClass(screenPoint);
                fingerClasses[fingerIndex] = classification;
            }
            else
            {
                fingerClasses[fingerIndex] = null;
            }
        }
        
        // 处理右手 (索引5-9)
        for (int fingerIndex = 0; fingerIndex < 5; fingerIndex++)
        {
            if (handRaycaster.TryGetFingerHit(Handedness.Right, fingerIndex, out RaycastHit hit))
            {
                Vector2 screenPoint = ProjectToScreenSpace(hit.point);
                string classification = GetPointedObjectClass(screenPoint);
                fingerClasses[fingerIndex + 5] = classification;
            }
            else
            {
                fingerClasses[fingerIndex + 5] = null;
            }
        }
        
        // 为右手食指创建可视化球体 (保持原有功能)
        ProcessRightIndexFingerVisualization();
        
        // 更新UI显示
        UpdateFingerClassUI();
    }
    
    /// <summary>
    /// 为右手食指创建可视化球体 (从原ProcessRightIndexFingerHit方法分离出来)
    /// </summary>
    private void ProcessRightIndexFingerVisualization()
    {
        if (handRaycaster.TryGetFingerHit(Handedness.Right, 1, out RaycastHit hit)) // Index finger = 1
        {
            Vector2 screenPoint = ProjectToScreenSpace(hit.point);
            Vector3 mappedPosition = MapToReferenceTransformSpace(screenPoint);
            CreateOrUpdateSphere(mappedPosition);
            
            if (SuperAdmin.superAdmin != null && SuperAdmin.superAdmin.showDebugInfo)
            {
                Debug.Log($"Right Index Hit: {hit.point}, Screen: {screenPoint}, Class: {GetFingerClass(6) ?? "None"}");
            }
        }
        else
        {
            // Hide sphere if no hit (always reuse single sphere)
            if (currentSphere != null)
            {
                currentSphere.SetActive(false);
            }
        }
    }
    
    /// <summary>
    /// 更新所有手指类别信息到UI文本控件
    /// </summary>
    private void UpdateFingerClassUI()
    {
        // 直接从SuperAdmin读取UI显示状态
        if (SuperAdmin.superAdmin == null || 
            !(SuperAdmin.superAdmin.isShowHandRayHitClass && SuperAdmin.superAdmin.isShowYoloResult)) 
            return;
        
        for (int i = 0; i < 10; i++)
        {
            if (fingerClassTexts[i] != null)
            {
                string fingerName = fingerNames[i];
                string classification = fingerClasses[i];
                string displayText = classification != null ? 
                    $"{fingerName}: {classification}" : 
                    $"{fingerName}: --";
                    
                fingerClassTexts[i].text = displayText;
            }
        }
    }
    
    /// <summary>
    /// 根据屏幕坐标获取指向的物体类别
    /// </summary>
    /// <param name="screenPoint">屏幕坐标 (0-1范围)</param>
    /// <returns>物体类别名称，如果没有指向任何检测物体则返回null</returns>
    private string GetPointedObjectClass(Vector2 screenPoint)
    {
        if (yoloMainCamera == null) return null;
        
        // 使用MainCamera的GetClassificationAtPixel方法获取类别
        return yoloMainCamera.GetClassificationAtPixel(screenPoint);
    }
    
    /// <summary>
    /// Calculates screen-space position a world space object. Useful for showing something on screen that is not visible in VR.
    /// For example, it can be used to update the position of a marker that highlights the gaze of the player, using eye tracking.
    /// </summary>
    /// <param name="camera">The camera used for VR rendering.</param>
    /// <param name="worldPos">World position of a point.</param>
    /// <returns>Screen position of a point.</returns>
    static Vector2 WorldToScreenVR(Camera camera, Vector3 worldPos)
    {
        Vector3 screenPoint = camera.WorldToViewportPoint(worldPos);
        float w = XRSettings.eyeTextureWidth;
        float h = XRSettings.eyeTextureHeight;
        float ar = w / h;

        screenPoint.x = (screenPoint.x - 0.15f * XRSettings.eyeTextureWidth) / 0.7f;
        screenPoint.y = (screenPoint.y - 0.15f * XRSettings.eyeTextureHeight) / 0.7f;

        return screenPoint;
    }
    
    /// <summary>
    /// Project 3D world position to screen space coordinates (0-1)
    /// </summary>
    private Vector2 ProjectToScreenSpace(Vector3 worldPosition)
    {
        Vector3 screenPoint = mainCamera.WorldToScreenPoint(worldPosition);
        
        // Convert to 0-1 coordinates
        float normalizedX = screenPoint.x / Screen.width;
        float normalizedY = screenPoint.y / Screen.height;
        
        // Clamp to 0-1 range
        normalizedX = Mathf.Clamp01(normalizedX);
        normalizedY = Mathf.Clamp01(normalizedY);
        
        return new Vector2(normalizedX, normalizedY);
    }
    

    /// <summary>
    /// Map 2D texture coordinates (in pixels) to the reference transform coordinate system
    /// </summary>
    private Vector3 MapToReferenceTransformSpace(Vector2 textureCoords)
    {
        if (originTransform == null || xAxisTransform == null || yAxisTransform == null)
        {
            Debug.LogWarning("Reference transforms not properly set up!");
            return Vector3.zero;
        }
        // Get the reference vectors
        Vector3 originPos = originTransform.position;
        Vector3 xAxisVector = xAxisTransform.position - originPos; // Vector from origin to x=1
        Vector3 yAxisVector = yAxisTransform.position - originPos; // Vector from origin to y=1
        
        // Map the 2D coordinates to 3D space using the reference transforms
        Vector3 mappedPosition = originPos + (textureCoords.x * xAxisVector) + (textureCoords.y * yAxisVector);
        
        return mappedPosition;
    }
    
    /// <summary>
    /// Create or update the sphere at the specified position (always reuse single sphere)
    /// </summary>
    private void CreateOrUpdateSphere(Vector3 position)
    {
        if (currentSphere == null)
        {
            // Create new sphere
            GameObject newSphere = Instantiate(spherePrefab, position, Quaternion.identity);
            newSphere.transform.localScale = Vector3.one * sphereSize;
            
            if (sphereMaterial != null)
            {
                Renderer renderer = newSphere.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material = sphereMaterial;
                }
            }
            
            currentSphere = newSphere;
        }
        else
        {
            // Update existing sphere position
            currentSphere.transform.position = position;
            currentSphere.SetActive(true);
        }
    }
    
    /// <summary>
    /// Setup reference transforms automatically if not assigned
    /// </summary>
    private void SetupReferenceTransforms()
    {
        
        // Make reference transforms children of this object for organization
        originTransform.SetParent(transform);
        xAxisTransform.SetParent(transform);
        yAxisTransform.SetParent(transform);
    }
    

    
    /// <summary>
    /// Public method to manually set reference transforms
    /// </summary>
    public void SetReferenceTransforms(Transform origin, Transform xAxis, Transform yAxis)
    {
        originTransform = origin;
        xAxisTransform = xAxis;
        yAxisTransform = yAxis;
    }
    
    /// <summary>
    /// Clear all created spheres (only single sphere since we always reuse)
    /// </summary>
    public void ClearSpheres()
    {
        if (currentSphere != null)
        {
            DestroyImmediate(currentSphere);
            currentSphere = null;
        }
    }
    
    // ========== 公共API方法 ==========
    
    /// <summary>
    /// 根据手指索引获取对应的类别信息
    /// </summary>
    /// <param name="fingerIndex">手指索引 (0-4: 左手拇指到小指, 5-9: 右手拇指到小指)</param>
    /// <returns>物体类别名称，如果索引无效或没有指向任何物体则返回null</returns>
    public string GetFingerClass(int fingerIndex)
    {
        if (fingerIndex < 0 || fingerIndex >= 10)
        {
            Debug.LogWarning($"ScreenSpaceProjector: Invalid finger index {fingerIndex}. Valid range is 0-9.");
            return null;
        }
        
        return fingerClasses[fingerIndex];
    }
    
    /// <summary>
    /// 获取所有手指的类别信息
    /// </summary>
    /// <returns>包含10个手指类别信息的数组副本</returns>
    public string[] GetAllFingerClasses()
    {
        string[] result = new string[10];
        System.Array.Copy(fingerClasses, result, 10);
        return result;
    }
    
    /// <summary>
    /// 获取左手所有手指的类别信息
    /// </summary>
    /// <returns>包含5个左手手指类别信息的数组 (拇指到小指)</returns>
    public string[] GetLeftHandClasses()
    {
        string[] result = new string[5];
        System.Array.Copy(fingerClasses, 0, result, 0, 5);
        return result;
    }
    
    /// <summary>
    /// 获取右手所有手指的类别信息
    /// </summary>
    /// <returns>包含5个右手手指类别信息的数组 (拇指到小指)</returns>
    public string[] GetRightHandClasses()
    {
        string[] result = new string[5];
        System.Array.Copy(fingerClasses, 5, result, 0, 5);
        return result;
    }
    
    /// <summary>
    /// 获取指定手的指定手指类别信息
    /// </summary>
    /// <param name="handedness">手的类型 (Left/Right)</param>
    /// <param name="fingerIndex">手指索引 (0: 拇指, 1: 食指, 2: 中指, 3: 无名指, 4: 小指)</param>
    /// <returns>物体类别名称，如果索引无效或没有指向任何物体则返回null</returns>
    public string GetFingerClass(Handedness handedness, int fingerIndex)
    {
        if (fingerIndex < 0 || fingerIndex >= 5)
        {
            Debug.LogWarning($"ScreenSpaceProjector: Invalid finger index {fingerIndex} for hand. Valid range is 0-4.");
            return null;
        }
        
        int arrayIndex = handedness == Handedness.Left ? fingerIndex : fingerIndex + 5;
        return GetFingerClass(arrayIndex);
    }
    
    // ========== UI管理方法 ==========
    
    /// <summary>
    /// 设置UI文本控件数组
    /// </summary>
    /// <param name="textComponents">TextMeshProUGUI控件数组，必须包含10个元素，对应10个手指</param>
    public void SetFingerClassTextComponents(TextMeshProUGUI[] textComponents)
    {
        if (textComponents == null)
        {
            Debug.LogWarning("ScreenSpaceProjector: Text components array is null.");
            return;
        }
        
        if (textComponents.Length != 10)
        {
            Debug.LogWarning($"ScreenSpaceProjector: Text components array must contain exactly 10 elements, got {textComponents.Length}.");
            return;
        }
        
        fingerClassTexts = textComponents;
        Debug.Log("ScreenSpaceProjector: Finger class text components updated successfully.");
    }
    
    /// <summary>
    /// 设置是否启用UI显示 (已弃用 - 现在由SuperAdmin统一控制)
    /// </summary>
    /// <param name="enable">是否启用UI显示</param>
    [System.Obsolete("UI显示现在由SuperAdmin.cs中的 isShowHandRayHitClass && isShowYoloResult 控制")]
    public void SetUIDisplayEnabled(bool enable)
    {
        Debug.LogWarning("SetUIDisplayEnabled is obsolete. UI display is now controlled by SuperAdmin.isShowHandRayHitClass && SuperAdmin.isShowYoloResult");
    }
    
    /// <summary>
    /// 手动强制更新UI显示（通常由Update自动调用）
    /// </summary>
    public void ForceUpdateUI()
    {
        UpdateFingerClassUI();
    }
    
    void OnDrawGizmos()
    {
        if (originTransform != null && xAxisTransform != null && yAxisTransform != null)
        {
            // Draw reference coordinate system
            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(originTransform.position, 0.02f);
            
            Gizmos.color = Color.red;
            Gizmos.DrawLine(originTransform.position, xAxisTransform.position);
            Gizmos.DrawWireSphere(xAxisTransform.position, 0.015f);
            
            Gizmos.color = Color.green;
            Gizmos.DrawLine(originTransform.position, yAxisTransform.position);
            Gizmos.DrawWireSphere(yAxisTransform.position, 0.015f);
            
            // Draw coordinate system plane
            Gizmos.color = new Color(1f, 1f, 1f, 0.1f);
            Vector3[] corners = new Vector3[]
            {
                originTransform.position,
                xAxisTransform.position,
                xAxisTransform.position + (yAxisTransform.position - originTransform.position),
                yAxisTransform.position
            };
            
            // Draw plane outline
            Gizmos.color = Color.yellow;
            for (int i = 0; i < corners.Length; i++)
            {
                Gizmos.DrawLine(corners[i], corners[(i + 1) % corners.Length]);
            }
        }
    }
}