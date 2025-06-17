using UnityEngine;
using UnityEngine.VFX;
using UnityEngine.XR.ARFoundation;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.Mathematics;
using Unity.Mathematics.Geometry;
using UnityEngine.UI;
using UnityEngine.XR.VisionOS;
using Random = UnityEngine.Random;

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

    [Header("Hand Data")] 
    public bool isUseRealHandData = true;
    [SerializeField] private MyHand hand;
    [SerializeField] private HandRaycaster handRaycaster;
    
    [Header("Debug UI")]
[SerializeField] private Text TextShowHandsBall;

    [SerializeField] private GameObject TransparentBallPrefab;

    
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
    /// <summary>
    /// 将十六进制颜色字符串转换为Unity Color
    /// </summary>
    /// <param name="hex">十六进制颜色字符串 (例如: "#FF0000", "FF0000", "#RGB", "RGB")</param>
    /// <returns>Unity Color对象</returns>
    public static Color HexToColor(string hex)
    {
        // 移除#号
        hex = hex.Replace("#", "");
        
        // 处理3位十六进制颜色 (例如: "F0A" -> "FF00AA")
        if (hex.Length == 3)
        {
            hex = hex[0].ToString() + hex[0].ToString() + 
                  hex[1].ToString() + hex[1].ToString() + 
                  hex[2].ToString() + hex[2].ToString();
        }
        
        // 处理6位十六进制颜色
        if (hex.Length == 6)
        {
            hex += "FF"; // 添加Alpha通道
        }
        
        // 解析RGBA
        if (hex.Length == 8)
        {
            byte r = System.Convert.ToByte(hex.Substring(0, 2), 16);
            byte g = System.Convert.ToByte(hex.Substring(2, 2), 16);
            byte b = System.Convert.ToByte(hex.Substring(4, 2), 16);
            byte a = System.Convert.ToByte(hex.Substring(6, 2), 16);
            
            return new Color(r / 255f, g / 255f, b / 255f, a / 255f);
        }
        
        Debug.LogError($"Invalid hex color format: {hex}");
        return Color.white;
    }
    private Color[] getRandomColors()
    {
        Color[] colorset = new Color[4];
        
        switch (Random.Range(0, 6))
        {
            case 0:
                colorset[0] = HexToColor("F7E4B0");
                colorset[1] = HexToColor("F5B3B3");
                colorset[2] = HexToColor("F77D54");
                colorset[3] = HexToColor("F76D7E");
                break;
            case 1:
                colorset[0] = HexToColor("F2C337");
                colorset[1] = HexToColor("EDB6FF");
                colorset[2] = HexToColor("FF8DCB");
                colorset[3] = HexToColor("A558F0");
                break;
            case 2:
                colorset[0] = HexToColor("F5F56E");
                colorset[1] = HexToColor("FFE1C9");
                colorset[2] = HexToColor("FFDC1E");
                colorset[3] = HexToColor("FF902E");
                break;
            case 3:
                colorset[0] = HexToColor("FFD54D");
                colorset[1] = HexToColor("67E397");
                colorset[2] = HexToColor("31B1CC");
                colorset[3] = HexToColor("2777D9");
                break;
            case 4:
                colorset[0] = HexToColor("D9F7C8");
                colorset[1] = HexToColor("E0F071");
                colorset[2] = HexToColor("7AE3F2");
                colorset[3] = HexToColor("00E373");
                break;
            case 5:
                colorset[0] = HexToColor("FCD8AB");
                colorset[1] = HexToColor("97B3FF");
                colorset[2] = HexToColor("CB84FA");
                colorset[3] = HexToColor("753FFF");
                break;
        }
        
        return colorset;


        
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
                // ve.SetVector3();
                vfxMap[mf] = ve;
            }

            // 3) 更新已有 VFX 参数
            var vfxInst = vfxMap[mf];
            vfxInst.SetMesh("PointCloudMesh", mf.mesh);
            vfxInst.SetVector3("PointCloudTransform", mf.transform.localPosition);
            vfxInst.SetVector3("PointCloudRotation", mf.transform.localEulerAngles);
            

            if (isUseRealHandData)
            {
                // 半径 1 - 0.5 m 实测我的手20度到80度
                float ballRadius;
                if (hand.handsDistance < 0.1)
                {
                    // 两只手根部贴在一起
                    ballRadius = (float)((hand.palmAngle - 20) / 40.0 * 0.5 + 0.5);
                    ballRadius = (float)math.max(ballRadius, 0.5);
                    ballRadius = (float)math.min(ballRadius, 1.0);

                }
                else
                {
                    ballRadius = 0.0f;
                }

                vfxInst.SetFloat("MaskBall", ballRadius);
                if (TextShowHandsBall)
                {
                    TextShowHandsBall.text = $"Hands control ball radius: {ballRadius} m";
                    // GameObject.Instantiate(TransparentBallPrefab)

                }
            }

            foreach (var hitinfo in handRaycaster.lastHits)
            {
                int vfxIndexInside = 0;
                var handName= hitinfo.Key.Split("_")[0];
                var fingerName= hitinfo.Key.Split("_")[1];
                
                vfxIndexInside += handName == "Left" ? 0 : 5;
                vfxIndexInside += HandRaycaster.FingerName2index(fingerName);
                vfxInst.SetVector3($"finger {vfxIndexInside+1}", hitinfo.Value.point);
                vfxInst.SetVector3($"fingerNormal {vfxIndexInside+1}", hitinfo.Value.normal);

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
