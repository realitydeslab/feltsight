using UnityEngine;
using UnityEngine.VFX;
using UnityEngine.XR.ARFoundation;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine.UI;
using UnityEngine.XR.VisionOS;

public class VFXMan : MonoBehaviour
{
    private Dictionary<MeshFilter, VisualEffect> vfxMap = new Dictionary<MeshFilter, VisualEffect>();
    public GameObject vfxMeshPrefab; 
    [Header("AR Mesh Settings")]
    public ARMeshManager meshManager;
    
    
    [SerializeField, Tooltip("用于确定合并范围的相机")]
    private Camera viewCamera;
    
    [SerializeField, Tooltip("更新合并mesh的间隔(秒)")]
    private float mergeUpdateInterval = 0.5f;

    [Header("Hand Data")] [SerializeField] private MyHand hand;
    
    [Header("Debug UI")]
[SerializeField] private Text TextShowHandsBall;

    
    private Matrix4x4 lastTransformMatrix;
    private float nextMergeUpdateTime;
    private MeshFilter combinedMeshFilter;
    
    void Start()
    {

        
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
            meshManager = FindFirstObjectByType<ARMeshManager>();
            if (meshManager == null)
            {
                Debug.LogError("找不到ARMeshManager，请手动指定");
            }
        }
        #if UNITY_VISIONOS && !UNITY_EDITOR
            meshManager.subsystem.SetClassificationEnabled(true);
        #endif
        
        // 初始更新
        CreateVFX4Mesh();

    }
    
    
    void Update()
    {
        
        // 检查是否需要更新合并的mesh
        if (Time.time >= nextMergeUpdateTime)
        {
            nextMergeUpdateTime = Time.time + mergeUpdateInterval;
            CreateVFX4Mesh();
        }
        
    }
    
    
    private void CreateVFX4Mesh()
    {
        if (meshManager == null || viewCamera == null)
        {
            return;
        }
        
        // 获取相机周围的mesh
        List<MeshFilter> nearbyMeshes = GetNearbyMeshes();
        
        if (nearbyMeshes.Count == 0)
        {
            Debug.Log("No nearby meshes found");
            return;
        }
        
        // 1) 销毁不再需要的 VFX
        foreach (var kv in vfxMap.ToList())
        {
            if (!nearbyMeshes.Contains(kv.Key))
            {
                Destroy(kv.Value.gameObject);
                vfxMap.Remove(kv.Key);
            }
        }
        
        // 2) 为新 Mesh 创建 VFX
        foreach (var mf in nearbyMeshes)
        {
            if (!vfxMap.ContainsKey(mf))
            {
                // var go = Instantiate(vfxMeshPrefab, Vector3.zero, Quaternion.identity, this.transform);
                var go = Instantiate(vfxMeshPrefab, this.transform);
                var ve = go.GetComponent<VisualEffect>();
                vfxMap[mf] = ve;
            }

            // 3) 更新已有 VFX 参数
            var vfxInst = vfxMap[mf];
            vfxInst.SetMesh("PointCloudMesh", mf.mesh);
            vfxInst.SetVector3("PointCloudTransform", mf.transform.localPosition);
            vfxInst.SetVector3("PointCloudRotation", mf.transform.localEulerAngles);
            // 半径 1 - 0.5 m 实测我的手20度到80度
            float ballRadius;
            if (hand.handsDistance < 0.1)
            {
                // 两只手根部贴在一起
            ballRadius = (float)((hand.palmAngle - 20) / 40.0 * 0.5 + 0.5);
                
            }
            else
            {
                ballRadius = 0.0f;
            }
            vfxInst.SetFloat("MaskBall", ballRadius);
            if (TextShowHandsBall)
            {
            TextShowHandsBall.text = $"Hands control ball radius: {ballRadius} m";
                
            }
            
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
            
            nearbyMeshes.Add(meshFilter);
        }
        //
        // // 按距离排序，优先处理近的mesh
        // nearbyMeshes.Sort((a, b) => 
        // {
        //     // float distA = Vector3.Distance(cameraPosition, CalculateMeshCenter(a));
        //     // float distB = Vector3.Distance(cameraPosition, CalculateMeshCenter(b));
        //
        //
        //     // ;
        //     return a.mesh.vertexCount.CompareTo(b.mesh.vertexCount);
        // });
        
        return nearbyMeshes;
    }
    

    [ContextMenu("手动构建VFX")]
    public void ForceUpdateCombinedMesh()
    {
        CreateVFX4Mesh();
    }
    

}
