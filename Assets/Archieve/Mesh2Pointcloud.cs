using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Random = UnityEngine.Random;

public class Mesh2Pointcloud : MonoBehaviour
{
    [SerializeField]
    private ARMeshManager m_MeshManager;
    
    [SerializeField, Tooltip("粒子系统预制体，如果为空则自动创建")]
    private ParticleSystem particleSystemPrefab;
    
    [SerializeField, Tooltip("URP兼容的粒子材质")]
    private Material particleMaterial;
    
    [SerializeField]
    private float particleSize = 0.01f;
    
    [SerializeField]
    private float samplingDistance = 0.05f;
    
    [SerializeField, Tooltip("更新间隔(秒)，设置为0则等待外部触发")]
    private float updateInterval = 1f;
    
    [SerializeField]
    private float randomDistortion = 0.02f; // 随机扰动的最大幅度

    [SerializeField]
    private bool useRandomColors = true; // 是否使用随机颜色

    [SerializeField]
    private Color minColor = new Color(0.2f, 0.2f, 0.8f); // 颜色范围最小值

    [SerializeField]
    private Color maxColor = new Color(0.8f, 0.8f, 1.0f); // 颜色范围最大值
    
    [SerializeField]
    private Camera viewCamera; // 用于确定视角的相机

    [SerializeField, Tooltip("只生成相机周围这个距离内的点云")]
    public float visibleRadius = 5.0f; // 默认5米范围内的点才会被显示

    [SerializeField, Tooltip("是否只生成相机可见范围内的点")]
    private bool limitByDistance = true; // 是否根据距离限制点云生成
    
    [SerializeField, Tooltip("最大点云数量")]
    private int maxPointCount = 10000; // 最大点云数量限制
    
    [SerializeField, Tooltip("粒子生命周期")]
    private float particleLifetime = 10f;
    
    [SerializeField, Tooltip("强制为每个mesh创建独立的粒子系统以支持不同颜色")]
    private bool useMultipleParticleSystems = true; // 
    
    [SerializeField, Tooltip("是否使用颜色缓存，保持每个mesh的颜色一致")]
    private bool useMeshColorCache = true; // 是否使用颜色缓存
    
    [SerializeField]
    [Tooltip("双手距离(原始数据)")]
    private Text m_HandsDisText;
    
    [SerializeField]
    [Tooltip("显示通过双手距离计算出来的张开角度")]
    private Text m_HandsAngleText;

    [SerializeField] private MyHand myHand;
    
    private int currentPointCount = 0; // 当前生成的点云数量
    private float nextUpdateTime;
    
    // 粒子系统相关
    private ParticleSystem mainParticleSystem;
    private Dictionary<MeshFilter, ParticleSystem> meshParticleSystemMap = new Dictionary<MeshFilter, ParticleSystem>();
    private List<ParticleSystem.Particle> allParticles = new List<ParticleSystem.Particle>();
    
    // 使用字典存储每个mesh实例与其对应的颜色
    private Dictionary<MeshFilter, Color> meshColorMap = new Dictionary<MeshFilter, Color>();

    private void Start()
    {
        nextUpdateTime = Time.time;
        
        // 如果没有指定相机，则使用主相机
        if (viewCamera == null)
        {
            viewCamera = Camera.main;
            if (viewCamera == null)
            {
                Debug.LogWarning("找不到主相机，请手动指定一个相机用于确定点云可见范围");
            }
        }
        
        // 检查并创建URP兼容的材质
        CheckAndCreateURPMaterial();
        
        // 初始化粒子系统
        InitializeParticleSystem();
    }
    
    private void CheckAndCreateURPMaterial()
    {
        if (particleMaterial == null)
        {
            // 尝试找到URP的默认粒子材质
            particleMaterial = CreateURPParticleMaterial();
            if (particleMaterial != null)
            {
                Debug.Log("自动创建了URP兼容的粒子材质");
            }
            else
            {
                Debug.LogWarning("无法创建URP粒子材质，请手动指定一个使用URP Shader的材质");
            }
        }
    }
    
    private Material CreateURPParticleMaterial()
    {
        // 尝试创建URP兼容的材质
        Shader urpShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (urpShader == null)
        {
            urpShader = Shader.Find("Universal Render Pipeline/Particles/Lit");
        }
        if (urpShader == null)
        {
            urpShader = Shader.Find("Sprites/Default");
        }
        
        if (urpShader != null)
        {
            Material mat = new Material(urpShader);
            mat.name = "Auto_URP_Particle_Material";
            
            // 设置材质属性以支持顶点颜色
            if (mat.HasProperty("_BaseColor"))
            {
                mat.SetColor("_BaseColor", Color.white);
            }
            if (mat.HasProperty("_Color"))
            {
                mat.SetColor("_Color", Color.white);
            }
            
            // 设置混合模式为透明
            if (mat.HasProperty("_Surface"))
            {
                mat.SetFloat("_Surface", 1); // Transparent
            }
            if (mat.HasProperty("_Blend"))
            {
                mat.SetFloat("_Blend", 0); // Alpha
            }
            
            return mat;
        }
        
        return null;
    }
    
    private void InitializeParticleSystem()
    {
        if (!useMultipleParticleSystems)
        {
            // 创建单个主粒子系统
            if (particleSystemPrefab != null)
            {
                mainParticleSystem = Instantiate(particleSystemPrefab, transform);
            }
            else
            {
                // 创建默认粒子系统
                GameObject particleGO = new GameObject("PointCloud_ParticleSystem");
                particleGO.transform.SetParent(transform);
                mainParticleSystem = particleGO.AddComponent<ParticleSystem>();
                
                // 配置粒子系统
                ConfigureParticleSystem(mainParticleSystem, Color.white);
            }
        }
    }
    
    private void ConfigureParticleSystem(ParticleSystem ps, Color baseColor)
    {
        var main = ps.main;
        main.startLifetime = particleLifetime;
        main.startSpeed = 0f;
        main.startSize = particleSize;
        main.startColor = baseColor;
        main.maxParticles = maxPointCount;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        
        var emission = ps.emission;
        emission.enabled = false; // 我们手动控制粒子发射
        
        var shape = ps.shape;
        shape.enabled = false;
        
        var velocityOverLifetime = ps.velocityOverLifetime;
        velocityOverLifetime.enabled = false;
        
        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = false;
        
        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = false;
        
        // 设置渲染器材质
        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        if (renderer != null && particleMaterial != null)
        {
            renderer.material = particleMaterial;
            
            // 确保启用顶点流以支持颜色
            renderer.enableGPUInstancing = false; // 禁用GPU实例化以确保颜色正确
            
            // 设置顶点流
            List<ParticleSystemVertexStream> streams = new List<ParticleSystemVertexStream>();
            streams.Add(ParticleSystemVertexStream.Position);
            streams.Add(ParticleSystemVertexStream.Color);
            streams.Add(ParticleSystemVertexStream.UV);
            renderer.SetActiveVertexStreams(streams);
        }
    }
    
    private ParticleSystem GetOrCreateParticleSystemForMesh(MeshFilter meshFilter, Color meshColor)
    {
        if (!useMultipleParticleSystems)
        {
            return mainParticleSystem;
        }
        
        if (meshParticleSystemMap.TryGetValue(meshFilter, out ParticleSystem existingPS))
        {
            return existingPS;
        }
        
        // 为这个mesh创建新的粒子系统
        GameObject particleGO = new GameObject($"ParticleSystem_{meshFilter.name}");
        particleGO.transform.SetParent(transform);
        ParticleSystem newPS = particleGO.AddComponent<ParticleSystem>();
        
        ConfigureParticleSystem(newPS, meshColor);
        meshParticleSystemMap[meshFilter] = newPS;
        
        return newPS;
    }
    
    private void FixedUpdate()
    {
        // 获取手的距离
        float handdis = myHand.handsDistance;
        m_HandsDisText.text = "HandsDis: " + handdis.ToString();
        float normalizedDistance = Mathf.InverseLerp(0.02f, 0.10f, handdis); // 0.4~0.8 映射到 0~1
        normalizedDistance = 1 - normalizedDistance;
        m_HandsAngleText.text = "HandsAngNormalize: " +normalizedDistance.ToString();
        
        visibleRadius= normalizedDistance * 3.8f + 0.3f;
        
        if (handdis > 0.1f)
        {
            return;
        }
        
        // 如果updateInterval为0，不自动更新，等待外部调用
        if (updateInterval <= 0)
        {
            return;
        }

        // 时间未到，不更新
        if (Time.time < nextUpdateTime)
        {
            return;
        }

        // 更新下一次刷新时间
        nextUpdateTime = Time.time + updateInterval;
        

        

        // 生成点云
        GeneratePointCloudFromMeshes();
    }

    // 计算mesh的重心
    private Vector3 CalculateMeshCenter(MeshFilter meshFilter)
    {
        Mesh mesh = meshFilter.mesh;
        if (mesh == null || mesh.vertices.Length == 0)
        {
            return meshFilter.transform.position;
        }

        Vector3 center = Vector3.zero;
        Vector3[] vertices = mesh.vertices;
        
        // 计算所有顶点的平均位置
        foreach (Vector3 vertex in vertices)
        {
            center += vertex;
        }
        center /= vertices.Length;
        
        // 转换为世界坐标
        return meshFilter.transform.TransformPoint(center);
    }

    // 提取的点云生成方法，供FixedUpdate和外部调用使用
    private void GeneratePointCloudFromMeshes()
    {
        if (m_MeshManager == null)
        {
            Debug.LogError("Mesh Manager is null. Please set it in the inspector.");
            return;
        }

        if (viewCamera == null && limitByDistance)
        {
            viewCamera = Camera.main;
            if (viewCamera == null)
            {
                Debug.LogWarning("找不到相机，将生成所有点云而不考虑距离限制");
                limitByDistance = false;
            }
        }

        string distanceInfo = limitByDistance ? $"（仅相机周围{visibleRadius}米范围内）" : "（无距离限制）";
        Debug.Log($"开始生成点云。..{distanceInfo}");
        System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
        stopwatch.Start();

        ClearExistingParticles();
        Debug.Log("已清除现有点云，准备重新生成");

        // 重置当前点数计数器
        currentPointCount = 0;
        allParticles.Clear();

        // 定期清理不再使用的mesh颜色映射
        CleanupUnusedMeshColorMappings();

        // 计算所有mesh的重心并按距离排序
        var meshesWithDistance = new List<(MeshFilter meshFilter, float distance)>();
        Vector3 cameraPosition = viewCamera != null ? viewCamera.transform.position : Vector3.zero;

        foreach (var meshFilter in m_MeshManager.meshes)
        {
            Vector3 meshCenter = CalculateMeshCenter(meshFilter);
            float distance = Vector3.Distance(cameraPosition, meshCenter);
            meshesWithDistance.Add((meshFilter, distance));
        }

        // 按距离排序（从近到远）
        meshesWithDistance.Sort((a, b) => a.distance.CompareTo(b.distance));

        Debug.Log($"共有 {meshesWithDistance.Count} 个mesh，已按距离排序");

        int processedMeshCount = 0;

        // 按排序后的顺序处理mesh
        foreach (var (meshFilter, distance) in meshesWithDistance)
        {
            // 检查是否已达到点数上限
            if (currentPointCount >= maxPointCount)
            {
                Debug.Log($"已达到最大点数限制 {maxPointCount}，停止生成。已处理 {processedMeshCount}/{meshesWithDistance.Count} 个mesh");
                break;
            }

            // 为每个mesh获取或生成颜色
            Color meshColor = GetOrCreateMeshColor(meshFilter);
            
            // 获取或创建该mesh对应的粒子系统
            ParticleSystem targetPS = GetOrCreateParticleSystemForMesh(meshFilter, meshColor);
            List<ParticleSystem.Particle> meshParticles = new List<ParticleSystem.Particle>();

            Mesh mesh = meshFilter.mesh;
            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;

            // 处理每个三角形面
            for (int i = 0; i < triangles.Length; i += 3)
            {
                // 检查是否已达到点数上限
                if (currentPointCount >= maxPointCount)
                {
                    Debug.Log($"在处理三角形时达到最大点数限制 {maxPointCount}，停止生成");
                    break;
                }

                Vector3 v1 = vertices[triangles[i]];
                Vector3 v2 = vertices[triangles[i + 1]];
                Vector3 v3 = vertices[triangles[i + 2]];

                // 将顶点转换为世界坐标
                v1 = meshFilter.transform.TransformPoint(v1);
                v2 = meshFilter.transform.TransformPoint(v2);
                v3 = meshFilter.transform.TransformPoint(v3);

                // 在顶点位置创建粒子（如果没有超过限制）
                if (currentPointCount < maxPointCount) AddParticleAtPosition(v1, meshColor, meshParticles);
                if (currentPointCount < maxPointCount) AddParticleAtPosition(v2, meshColor, meshParticles);
                if (currentPointCount < maxPointCount) AddParticleAtPosition(v3, meshColor, meshParticles);

                // 如果已经达到上限，跳出循环
                if (currentPointCount >= maxPointCount)
                {
                    break;
                }

                // 对三角形面进行采样
                List<Vector3> sampledPoints = SampleTriangle(v1, v2, v3, samplingDistance);

                // 在采样点位置创建粒子
                foreach (Vector3 point in sampledPoints)
                {
                    if (currentPointCount >= maxPointCount)
                    {
                        Debug.Log($"在处理采样点时达到最大点数限制 {maxPointCount}，停止生成");
                        break;
                    }
                    AddParticleAtPosition(point, meshColor, meshParticles);
                }

                // 如果已经达到上限，跳出三角形循环
                if (currentPointCount >= maxPointCount)
                {
                    break;
                }
            }

            // 将该mesh的粒子添加到粒子系统中
            if (meshParticles.Count > 0)
            {
                if (useMultipleParticleSystems)
                {
                    targetPS.SetParticles(meshParticles.ToArray(), meshParticles.Count);
                }
                else
                {
                    allParticles.AddRange(meshParticles);
                }
            }

            processedMeshCount++;

            // 如果已经达到上限，跳出mesh循环
            if (currentPointCount >= maxPointCount)
            {
                break;
            }
        }

        // 如果使用单个粒子系统，一次性设置所有粒子
        if (!useMultipleParticleSystems && allParticles.Count > 0)
        {
            mainParticleSystem.SetParticles(allParticles.ToArray(), allParticles.Count);
        }

        stopwatch.Stop();
        Debug.Log($"点云生成完成，共创建 {currentPointCount} 个粒子（上限：{maxPointCount}），处理了 {processedMeshCount}/{meshesWithDistance.Count} 个mesh，耗时 {stopwatch.ElapsedMilliseconds}毫秒");
    }

    private Color GetOrCreateMeshColor(MeshFilter meshFilter)
    {
        // 如果不使用颜色缓存，每次都生成新颜色
        if (!useMeshColorCache)
        {
            // 生成一个新的随机颜色
            return useRandomColors
                ? new Color(
                    UnityEngine.Random.Range(minColor.r, maxColor.r),
                    UnityEngine.Random.Range(minColor.g, maxColor.g),
                    UnityEngine.Random.Range(minColor.b, maxColor.b),
                    1.0f
                )
                : Color.white;
        }

        // 使用缓存模式，检查这个mesh是否已经有对应的颜色
        if (!meshColorMap.TryGetValue(meshFilter, out Color meshColor))
        {
            // 如果没有，生成一个新的随机颜色
            meshColor = useRandomColors
                ? new Color(
                    UnityEngine.Random.Range(minColor.r, maxColor.r),
                    UnityEngine.Random.Range(minColor.g, maxColor.g),
                    UnityEngine.Random.Range(minColor.b, maxColor.b),
                    1.0f
                )
                : Color.white;

            // 将新颜色存储在字典中
            meshColorMap[meshFilter] = meshColor;
        }

        return meshColor;
    }

    private void AddParticleAtPosition(Vector3 position, Color color, List<ParticleSystem.Particle> particleList)
    {
        // 首先检查是否已达到点数上限
        if (currentPointCount >= maxPointCount)
        {
            return;
        }

        // 首先检查点是否在可见范围内
        if (!IsPointVisible(position))
        {
            return; // 不在可见范围内，不创建点
        }

        // 创建粒子
        ParticleSystem.Particle particle = new ParticleSystem.Particle();
        particle.position = position;
        particle.startColor = color;
        particle.startSize = particleSize;
        particle.startLifetime = particleLifetime;
        particle.remainingLifetime = particleLifetime;
        particle.velocity = Vector3.zero;

        particleList.Add(particle);

        // 增加点数计数器
        currentPointCount++;
    }

    private List<Vector3> SampleTriangle(Vector3 v1, Vector3 v2, Vector3 v3, float distance)
    {
        List<Vector3> sampledPoints = new List<Vector3>();
        
        // 计算三角形的边长
        float a = Vector3.Distance(v2, v3);
        float b = Vector3.Distance(v1, v3);
        float c = Vector3.Distance(v1, v2);
        
        // 计算三角形面积
        float s = (a + b + c) / 2;
        float area = Mathf.Sqrt(s * (s - a) * (s - b) * (s - c));
        
        // 估算需要的采样点数量 (简化为面积除以采样间距的平方)
        int numSamples = Mathf.Max(1, Mathf.CeilToInt(area / (distance * distance)));
        
        // 简单采样
        for (int i = 0; i < numSamples; i++)
        {
            // 生成随机重心坐标
            Vector3 barycentric = GetRandomBarycentricCoordinate();
            
            // 使用重心坐标计算采样点
            Vector3 point = barycentric.x * v1 + barycentric.y * v2 + barycentric.z * v3;
            
            // 添加随机扰动
            point += new Vector3(
                UnityEngine.Random.Range(-randomDistortion, randomDistortion),
                UnityEngine.Random.Range(-randomDistortion, randomDistortion),
                UnityEngine.Random.Range(-randomDistortion, randomDistortion)
            );
            
            // 直接添加点，不检查是否太近
            sampledPoints.Add(point);
        }
        
        return sampledPoints;
    }
    
    private Vector3 GetRandomBarycentricCoordinate()
    {
        // 生成随机重心坐标
        float r1 = Mathf.Sqrt(UnityEngine.Random.Range(0f, 1f));
        float r2 = UnityEngine.Random.Range(0f, 1f);
        
        float a = 1 - r1;
        float b = r1 * (1 - r2);
        float c = r1 * r2;
        
        return new Vector3(a, b, c);
    }
    
    private void ClearExistingParticles()
    {
        // 清除主粒子系统
        if (mainParticleSystem != null)
        {
            mainParticleSystem.Clear();
        }
        
        // 清除所有mesh对应的粒子系统
        foreach (var ps in meshParticleSystemMap.Values)
        {
            if (ps != null)
            {
                ps.Clear();
            }
        }
        
        // 重置点数计数器
        currentPointCount = 0;
        allParticles.Clear();
    }

    // 清理不再使用的mesh颜色映射和粒子系统
    private void CleanupUnusedMeshColorMappings()
    {
        // 如果不使用颜色缓存，直接清空颜色映射字典
        if (!useMeshColorCache)
        {
            meshColorMap.Clear();
            Debug.Log("未使用颜色缓存，已清空所有颜色映射");
        }
        else
        {
            // 创建当前活跃mesh的列表
            List<MeshFilter> activeMeshes = new List<MeshFilter>();
            int totalMeshes = m_MeshManager.meshes.Count;

            Debug.Log($"开始处理 {totalMeshes} 个Mesh");

            foreach (var meshFilter in m_MeshManager.meshes)
            {
                activeMeshes.Add(meshFilter);
            }

            // 移除不再使用的mesh映射
            List<MeshFilter> meshesToRemove = new List<MeshFilter>();
            foreach (var meshEntry in meshColorMap)
            {
                if (!activeMeshes.Contains(meshEntry.Key))
                {
                    meshesToRemove.Add(meshEntry.Key);
                }
            }

            // 从字典中删除颜色映射
            foreach (var mesh in meshesToRemove)
            {
                meshColorMap.Remove(mesh);
            }
        }
        
        // 清理不再使用的粒子系统
        if (useMultipleParticleSystems)
        {
            List<MeshFilter> particleSystemsToRemove = new List<MeshFilter>();
            // foreach (var psEntry in meshParticleSystemMap)
            // {
            //     if (!activeMeshes.Contains(psEntry.Key))
            //     {
            //         particleSystemsToRemove.Add(psEntry.Key);
            //         // 销毁粒子系统GameObject
            //         if (psEntry.Value != null)
            //         {
            //             DestroyImmediate(psEntry.Value.gameObject);
            //         }
            //     }
            // }
            
            foreach (var mesh in particleSystemsToRemove)
            {
                meshParticleSystemMap.Remove(mesh);
            }
        }
    }

    // 公共方法，供外部调用来触发点云生成
    public void GeneratePointCloud()
    {
        // 直接调用生成点云的代码，不管计时器
        GeneratePointCloudFromMeshes();
    }

    private void OnDisable()
    {
        // 清理资源
        ClearExistingParticles();
    }

    // 检查点是否在相机可见范围内
    private bool IsPointVisible(Vector3 point)
    {
        // 如果不启用距离限制，所有点都可见
        if (!limitByDistance || viewCamera == null)
        {
            return true;
        }

        // 计算点到相机的距离
        float distance = Vector3.Distance(viewCamera.transform.position, point);

        // 如果距离小于等于可见半径，则点可见
        return distance <= visibleRadius;
    }
    
    // 公共方法：设置粒子大小
    public void SetParticleSize(float size)
    {
        particleSize = size;
        
        if (mainParticleSystem != null)
        {
            var main = mainParticleSystem.main;
            main.startSize = size;
        }
        
        foreach (var ps in meshParticleSystemMap.Values)
        {
            if (ps != null)
            {
                var main = ps.main;
                main.startSize = size;
            }
        }
    }
    
    // 公共方法：设置粒子生命周期
    public void SetParticleLifetime(float lifetime)
    {
        particleLifetime = lifetime;
        
        if (mainParticleSystem != null)
        {
            var main = mainParticleSystem.main;
            main.startLifetime = lifetime;
        }
        
        foreach (var ps in meshParticleSystemMap.Values)
        {
            if (ps != null)
            {
                var main = ps.main;
                main.startLifetime = lifetime;
            }
        }
    }
}
