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
    # if UNITY_EDITOR
    bool isUseRealHandData =  false;
    #endif
    #if !UNITY_EDITOR && UNITY_VISIONOS
    bool isUseRealHandData = true ;
    #endif
    
    
    [SerializeField] private MyHand hand;
    [SerializeField] private HandRaycaster handRaycaster;
    public float ballRadius;
    
    [Header("Ballline Shader Settings")]
    [SerializeField, Tooltip("线条宽度")]
    private float lineWidth = 1.0f;
    [SerializeField, Tooltip("自发光强度")]
    private float emissionIntensity = 1.0f;
    [SerializeField, Tooltip("距离偏移量")]
    private float distanceOffset = 0f;
    
    [Header("Debug UI")]
    [SerializeField] private Text TextShowHandsBall;
    [SerializeField] private GameObject TransparentBallPrefab;

    // Shader属性ID缓存（性能优化）
    private static readonly int TargetDistanceID = Shader.PropertyToID("_TargetDistance");
    private static readonly int LineWidthID = Shader.PropertyToID("_LineWidth");
    private static readonly int EmissionIntensityID = Shader.PropertyToID("_EmissionIntensity");
    
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
            CreateVFX4Mesh(); // 这里会调用GetNearbyMeshes()，从而更新材质属性
        }
    }
    
    /// <summary>
    /// 更新单个mesh的材质属性
    /// </summary>
    private void UpdateMeshMaterialProperties(MeshFilter meshFilter)
    {
        // 获取mesh的Renderer组件
        Renderer meshRenderer = meshFilter.GetComponent<Renderer>();
        if (meshRenderer == null) return;
        
        // 直接使用ballRadius + offset作为目标距离
        float targetDistance = ballRadius + distanceOffset;
        
        // 使用MaterialPropertyBlock更新属性
        MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();
        meshRenderer.GetPropertyBlock(propertyBlock);
        
        propertyBlock.SetFloat(TargetDistanceID, targetDistance);
        propertyBlock.SetFloat(LineWidthID, lineWidth);
        propertyBlock.SetFloat(EmissionIntensityID, emissionIntensity);
        
        meshRenderer.SetPropertyBlock(propertyBlock);
    }
    
    /// <summary>
    /// 更新所有mesh材质的Target Distance（独立调用方法）
    /// </summary>
    private void UpdateAllMeshMaterialsTargetDistance()
    {
        if (meshManager == null) return;
        
        foreach (var meshFilter in meshManager.meshes)
        {
            if (meshFilter == null || meshFilter.mesh == null)
                continue;
                
            UpdateMeshMaterialProperties(meshFilter);
        }
    }
    
    /// <summary>
    /// 公共方法：手动设置ballRadius
    /// </summary>
    public void SetBallRadius(float newRadius)
    {
        ballRadius = newRadius;
    }
    
    /// <summary>
    /// 公共方法：设置线条宽度
    /// </summary>
    public void SetLineWidth(float width)
    {
        lineWidth = width;
    }
    
    /// <summary>
    /// 公共方法：设置自发光强度
    /// </summary>
    public void SetEmissionIntensity(float intensity)
    {
        emissionIntensity = intensity;
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
        
        // 获取相机周围的mesh（同时更新材质属性）
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
                var go = Instantiate(vfxMeshPrefab, this.transform);
                var ve = go.GetComponent<VisualEffect>();
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
                }
            }
            else
            {
                    ballRadius = 2.0f;
                
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
        
        foreach (var meshFilter in meshManager.meshes)
        {
            if (meshFilter == null || meshFilter.mesh == null)
                continue;
            
            // 更新当前mesh的材质属性
            UpdateMeshMaterialProperties(meshFilter);
            
            nearbyMeshes.Add(meshFilter);
        }
        
        return nearbyMeshes;
    }

    [ContextMenu("手动构建VFX")]
    public void ForceUpdateCombinedMesh()
    {
        CreateVFX4Mesh();
    }
    
    [ContextMenu("手动更新材质距离")]
    public void ForceUpdateMaterialDistance()
    {
        UpdateAllMeshMaterialsTargetDistance();
    }
}
