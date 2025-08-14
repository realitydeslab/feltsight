using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Hands;

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
    
    [Header("Debug Settings")]
    [SerializeField] private bool showDebugInfo = true;
    [SerializeField] private bool createMultipleSpheres = false; // If false, reuses single sphere
    
    private GameObject currentSphere;
    
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
        ProcessRightIndexFingerHit();
    }
    
    /// <summary>
    /// Process the right hand index finger raycast hit and create sphere at projected position
    /// </summary>
    private void ProcessRightIndexFingerHit()
    {
        // Debug.Log(handRaycaster.TryGetFingerHit(Handedness.Right, 1, out RaycastHit hi11t));
        // Get right hand index finger hit
        if (handRaycaster.TryGetFingerHit(Handedness.Right, 1, out RaycastHit hit)) // Index finger = 1
        {
            // Project 3D hit point to screen space (0-1 coordinates)
            Vector2 screenPoint = ProjectToScreenSpace(hit.point);
            
            // 获取指向的物体类别
            string pointedObjectClass = GetPointedObjectClass(screenPoint);
             
            if (showDebugInfo)
            {
                Debug.Log($"Hit point: {hit.point}, Screen space: {screenPoint}, Pointed Object Class: {pointedObjectClass ?? "None"}");
            }
            
            // Map 2D coordinates to reference transform space
            Vector3 mappedPosition = MapToReferenceTransformSpace(screenPoint);
            
            // Create or update sphere at the mapped position
            CreateOrUpdateSphere(mappedPosition);
        }
        else
        {
            // Hide sphere if no hit
            if (currentSphere != null && !createMultipleSpheres)
            {
                currentSphere.SetActive(false);
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
    /// Create or update the sphere at the specified position
    /// </summary>
    private void CreateOrUpdateSphere(Vector3 position)
    {
        if (createMultipleSpheres || currentSphere == null)
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
            
            if (!createMultipleSpheres)
            {
                // Destroy old sphere if reusing
                if (currentSphere != null)
                {
                    DestroyImmediate(currentSphere);
                }
                currentSphere = newSphere;
            }
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
    /// Clear all created spheres
    /// </summary>
    public void ClearSpheres()
    {
        if (currentSphere != null)
        {
            DestroyImmediate(currentSphere);
            currentSphere = null;
        }
        
        // If creating multiple spheres, find and destroy them
        if (createMultipleSpheres)
        {
            GameObject[] spheres = GameObject.FindGameObjectsWithTag("ScreenProjectionSphere");
            foreach (GameObject sphere in spheres)
            {
                DestroyImmediate(sphere);
            }
        }
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