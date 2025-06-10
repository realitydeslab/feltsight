using UnityEngine;
using UnityEngine.VFX;
using UnityEngine.XR.ARFoundation;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.XR.VisionOS;

public class VFXMan : MonoBehaviour
{
    [Header("VFX Settings")]
    public VisualEffect vfx;
    
    [Header("AR Mesh Settings")]
    public ARMeshManager meshManager;
    
    [Header("Transform Settings")]
    [SerializeField, Tooltip("合并后mesh的Transform，如果为空则使用当前GameObject")]
    public Transform pointCloudTransform;
    
    [Header("Update Settings")]
    public bool updateTransformEveryFrame = true;
    
    [Header("Mesh Merging Settings")]
    [SerializeField, Tooltip("合并mesh的最大顶点数")]
    private int maxVertices = 65000; // Unity mesh的顶点数限制
    
    [SerializeField, Tooltip("距离相机多远的mesh会被包含在合并中")]
    private float mergeRadius = 5.0f;
    
    [SerializeField, Tooltip("用于确定合并范围的相机")]
    private Camera viewCamera;
    
    [SerializeField, Tooltip("更新合并mesh的间隔(秒)")]
    private float mergeUpdateInterval = 0.5f;
    
    [Header("Debug Settings")]
    [SerializeField, Tooltip("是否在Scene视图中显示合并范围")]
    private bool showMergeRadius = true;
    
    private Matrix4x4 lastTransformMatrix;
    private Mesh combinedMesh;
    private float nextMergeUpdateTime;
    private GameObject combinedMeshObject; // 用于存放合并后mesh的GameObject
    private MeshFilter combinedMeshFilter;
    
    void Start()
    {
        // 创建合并mesh的GameObject
        CreateCombinedMeshObject();
        
        if (viewCamera == null)
        {
            viewCamera = Camera.main;
            if (viewCamera == null)
            {
                Debug.LogWarning("找不到相机，请手动指定viewCamera");
            }
        }
        
        if (meshManager == null)
        {
            meshManager = FindObjectOfType<ARMeshManager>();
            if (meshManager == null)
            {
                Debug.LogError("找不到ARMeshManager，请手动指定");
            }
        }
        #if UNITY_VISIONOS && !UNITY_EDITOR
            meshManager.subsystem.SetClassificationEnabled(true);
        #endif
        // 创建合并后的mesh
        combinedMesh = new Mesh();
        combinedMesh.name = "CombinedARMesh";
        
        // 初始更新
        UpdateCombinedMesh();
        UpdateVFXParameters();
    }
    
    private void CreateCombinedMeshObject()
    {
        // 创建一个专门用于存放合并mesh的GameObject
        combinedMeshObject = new GameObject("CombinedMeshObject");
        combinedMeshObject.transform.SetParent(this.transform);
        combinedMeshObject.transform.localPosition = Vector3.zero;
        combinedMeshObject.transform.localRotation = Quaternion.identity;
        combinedMeshObject.transform.localScale = Vector3.one;
        
        // 添加MeshFilter组件
        combinedMeshFilter = combinedMeshObject.AddComponent<MeshFilter>();
        
        // 设置pointCloudTransform为合并mesh的transform
        pointCloudTransform = combinedMeshObject.transform;
        
        Debug.Log("已创建合并mesh的GameObject，Transform已设置为合并mesh的Transform");
    }
    
    void Update()
    {
        // 检查是否需要更新合并的mesh
        if (Time.time >= nextMergeUpdateTime)
        {
            nextMergeUpdateTime = Time.time + mergeUpdateInterval;
            UpdateCombinedMesh();
        }
        
        // 检查Transform是否需要更新
        if (updateTransformEveryFrame || HasTransformChanged())
        {
            UpdateVFXParameters();
        }
    }
    
    private bool HasTransformChanged()
    {
        if (pointCloudTransform == null) return false;
        
        Matrix4x4 currentMatrix = pointCloudTransform.localToWorldMatrix;
        bool changed = currentMatrix != lastTransformMatrix;
        lastTransformMatrix = currentMatrix;
        return changed;
    }
    
    private void UpdateCombinedMesh()
    {
        if (meshManager == null || viewCamera == null)
        {
            return;
        }
        
        // 获取相机周围的mesh
        List<MeshFilter> nearbyMeshes = GetNearbyMeshes();
        
        if (nearbyMeshes.Count == 0)
        {
            Debug.Log("没有找到附近的mesh进行合并");
            return;
        }
        
        // 合并mesh
        CombineMeshes(nearbyMeshes);
        
        // 将合并后的mesh赋给MeshFilter
        if (combinedMeshFilter != null)
        {
            combinedMeshFilter.mesh = combinedMesh;
        }
        
        // 更新合并mesh对象的位置（设置为所有mesh的中心）
        UpdateCombinedMeshTransform(nearbyMeshes);
        
        // 更新VFX参数
        UpdateVFXParameters();
    }
    
    private void UpdateCombinedMeshTransform(List<MeshFilter> meshFilters)
    {
        if (meshFilters.Count == 0 || combinedMeshObject == null)
            return;
        
        // 计算所有mesh的中心点
        Vector3 centerPosition = Vector3.zero;
        int validMeshCount = 0;
        
        foreach (var meshFilter in meshFilters)
        {
            Vector3 meshCenter = CalculateMeshCenter(meshFilter);
            centerPosition += meshCenter;
            validMeshCount++;
        }
        
        if (validMeshCount > 0)
        {
            centerPosition /= validMeshCount;
            
            // 设置合并mesh对象的位置为所有mesh的中心
            combinedMeshObject.transform.position = centerPosition;
            
            // 保持旋转和缩放为默认值
            combinedMeshObject.transform.rotation = Quaternion.identity;
            combinedMeshObject.transform.localScale = Vector3.one;
        }
    }
    
    private List<MeshFilter> GetNearbyMeshes()
    {
        List<MeshFilter> nearbyMeshes = new List<MeshFilter>();
        Vector3 cameraPosition = viewCamera.transform.position;
        
        foreach (var meshFilter in meshManager.meshes)
        {
            if (meshFilter == null || meshFilter.mesh == null)
                continue;
                
            // 计算mesh的中心点
            Vector3 meshCenter = CalculateMeshCenter(meshFilter);
            float distance = Vector3.Distance(cameraPosition, meshCenter);
            
            // 如果在合并范围内，添加到列表
            if (distance <= mergeRadius)
            {
                nearbyMeshes.Add(meshFilter);
            }
        }
        
        // 按距离排序，优先处理近的mesh
        nearbyMeshes.Sort((a, b) => 
        {
            float distA = Vector3.Distance(cameraPosition, CalculateMeshCenter(a));
            float distB = Vector3.Distance(cameraPosition, CalculateMeshCenter(b));
            return distA.CompareTo(distB);
        });
        
        return nearbyMeshes;
    }
    
    private Vector3 CalculateMeshCenter(MeshFilter meshFilter)
    {
        Mesh mesh = meshFilter.mesh;
        if (mesh == null || mesh.vertices.Length == 0)
        {
            return meshFilter.transform.position;
        }
        
        Vector3 center = Vector3.zero;
        Vector3[] vertices = mesh.vertices;
        
        foreach (Vector3 vertex in vertices)
        {
            center += vertex;
        }
        center /= vertices.Length;
        
        return meshFilter.transform.TransformPoint(center);
    }
    
    private void CombineMeshes(List<MeshFilter> meshFilters)
    {
        List<Vector3> combinedVertices = new List<Vector3>();
        List<int> combinedTriangles = new List<int>();
        List<Vector3> combinedNormals = new List<Vector3>();
        List<Vector2> combinedUVs = new List<Vector2>();
        
        int vertexOffset = 0;
        Vector3 mergeCenter = combinedMeshObject.transform.position;
        
        foreach (var meshFilter in meshFilters)
        {
            Mesh mesh = meshFilter.mesh;
            if (mesh == null) continue;
            
            // 检查是否会超过顶点限制
            if (combinedVertices.Count + mesh.vertexCount > maxVertices)
            {
                Debug.LogWarning($"达到最大顶点数限制 {maxVertices}，停止合并更多mesh");
                break;
            }
            
            // 获取mesh数据
            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            Vector2[] uvs = mesh.uv;
            int[] triangles = mesh.triangles;
            
            // 转换顶点到世界坐标，然后转换为相对于合并mesh中心的本地坐标
            for (int i = 0; i < vertices.Length; i++)
            {
                // 先转换到世界坐标
                Vector3 worldVertex = meshFilter.transform.TransformPoint(vertices[i]);
                // 再转换为相对于合并mesh中心的坐标
                Vector3 localVertex = worldVertex - mergeCenter;
                combinedVertices.Add(localVertex);
                
                // 转换法线到世界坐标
                if (normals.Length > i)
                {
                    combinedNormals.Add(meshFilter.transform.TransformDirection(normals[i]).normalized);
                }
                else
                {
                    combinedNormals.Add(Vector3.up); // 默认法线
                }
                
                // UV坐标
                if (uvs.Length > i)
                {
                    combinedUVs.Add(uvs[i]);
                }
                else
                {
                    combinedUVs.Add(Vector2.zero); // 默认UV
                }
            }
            
            // 调整三角形索引
            for (int i = 0; i < triangles.Length; i++)
            {
                combinedTriangles.Add(triangles[i] + vertexOffset);
            }
            
            vertexOffset += vertices.Length;
        }
        
        // 更新合并后的mesh
        combinedMesh.Clear();
        
        if (combinedVertices.Count > 0)
        {
            combinedMesh.SetVertices(combinedVertices);
            combinedMesh.SetTriangles(combinedTriangles, 0);
            combinedMesh.SetNormals(combinedNormals);
            combinedMesh.SetUVs(0, combinedUVs);
            
            // 重新计算边界
            combinedMesh.RecalculateBounds();
            
            Debug.Log($"合并完成：{meshFilters.Count} 个mesh，总顶点数：{combinedVertices.Count}，总三角形数：{combinedTriangles.Count / 3}");
        }
        else
        {
            Debug.LogWarning("合并后的mesh没有顶点数据");
        }
    }

    private void UpdateVFXParameters()
    {
        if (vfx == null) return;
        

        // 设置合并后的Mesh参数
        if (combinedMesh != null && combinedMesh.vertexCount > 0)
        {
            vfx.SetMesh("PointCloudMesh", combinedMesh);
            
            
            // 可选：设置顶点数量
            vfx.SetInt("VertexCount", combinedMesh.vertexCount);
            
            // 可选：设置三角形数量
            vfx.SetInt("TriangleCount", combinedMesh.triangles.Length / 3);
        }
        
        // 设置Transform参数 - 使用合并后mesh的transform
        if (pointCloudTransform != null)
        {
            // 方法1：传递位置（如您当前的代码）
            vfx.SetVector3("PointCloudTransform", pointCloudTransform.position);
            
        }
        
        
        
    }
    
    // 公共方法：手动触发mesh合并更新
    public void ForceUpdateCombinedMesh()
    {
        UpdateCombinedMesh();
    }
    
    // 公共方法：设置合并半径
    public void SetMergeRadius(float radius)
    {
        mergeRadius = radius;
        ForceUpdateCombinedMesh();
    }
    
    // 公共方法：获取当前合并mesh的信息
    public void GetCombinedMeshInfo(out int vertexCount, out int triangleCount)
    {
        if (combinedMesh != null)
        {
            vertexCount = combinedMesh.vertexCount;
            triangleCount = combinedMesh.triangles.Length / 3;
        }
        else
        {
            vertexCount = 0;
            triangleCount = 0;
        }
    }
    
    // 公共方法：获取合并mesh的Transform
    public Transform GetCombinedMeshTransform()
    {
        return pointCloudTransform;
    }
    
    // 公共方法：获取合并后的mesh
    public Mesh GetCombinedMesh()
    {
        return combinedMesh;
    }
    
    private void OnDestroy()
    {
        // 清理资源
        if (combinedMesh != null)
        {
            DestroyImmediate(combinedMesh);
        }
        
        if (combinedMeshObject != null)
        {
            DestroyImmediate(combinedMeshObject);
        }
    }
    
    // 在Scene视图中绘制合并范围和合并mesh的位置
    private void OnDrawGizmosSelected()
    {
        if (showMergeRadius && viewCamera != null)
        {
            // 绘制合并范围
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(viewCamera.transform.position, mergeRadius);
        }
        
        // 绘制合并mesh的位置
        if (pointCloudTransform != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(pointCloudTransform.position, Vector3.one * 0.1f);
            
            // 绘制坐标轴
            Gizmos.color = Color.red;
            Gizmos.DrawLine(pointCloudTransform.position, pointCloudTransform.position + pointCloudTransform.right * 0.2f);
            Gizmos.color = Color.green;
            Gizmos.DrawLine(pointCloudTransform.position, pointCloudTransform.position + pointCloudTransform.up * 0.2f);
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(pointCloudTransform.position, pointCloudTransform.position + pointCloudTransform.forward * 0.2f);
        }
    }
}
