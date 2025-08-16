using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using Unity.InferenceEngine;

public class MainCamera : MonoBehaviour
{
    public Texture2D _texture; // 当前使用的纹理
    EnterpriseCameraAccessManager ecam;
    
    private int height;
    private int width;
    
    [Tooltip("RawImage component which will be used to draw resuls.")]
    [SerializeField]
    protected RawImage ImageUI;
    
    [Header("YOLO Configuration")]
    [Tooltip("Drag the classes.txt here")]
    public TextAsset classesAsset;
    
    [Tooltip("Select the task type: Detection or Segmentation")]
    public TaskType taskType = TaskType.Detection;
    
    [Header("⚠️ 配置已迁移")]
    [Tooltip("YOLO模型配置(ModelAsset、输入尺寸、IOU/Score阈值、推理间隔)现在统一在SuperAdmin.cs中设置")]
    public string configNote = "👉 请到 SuperAdmin.cs 的 'YOLO模型配置' 区域设置模型参数";
    
    // YOLO配置将从SuperAdmin获取
    private ModelAsset modelAsset;
    private float iouThreshold;
    private float scoreThreshold;
    private float inferenceInterval;
    private bool enableYoloDebugLogs;
    
    [Tooltip("Drag a border box texture here")]
    public Texture2D borderTexture;
    
    [Tooltip("Select an appropriate font for the labels")]
    public Font font;
    
    public enum TaskType
    {
        Detection,
        Segmentation
    }
    
    // YOLO推理相关变量
    const BackendType backend = BackendType.GPUCompute;
    // const BackendType backend = BackendType.CPU;
    private Worker worker;
    private string[] labels;
    private RenderTexture targetRT;
    private RenderTexture tempRT; // Double buffering
    private Tensor<float> centersToCorners;
    
    // 推理状态控制
    private float lastInferenceTime;
    private bool isInferenceRunning = false;
    private Coroutine inferenceCoroutine;
    
    // 模型输入尺寸 (从SuperAdmin获取)
    private int imageWidth;
    private int imageHeight;
    
    // 当前推理结果存储
    private List<ClassificationResult> currentResults = new List<ClassificationResult>();
    private readonly object resultsLock = new object();
    
    // UI绘制相关
    private Transform displayLocation;
    private Sprite borderSprite;
    private List<GameObject> boxPool = new List<GameObject>();
    
    // 分类结果数据结构
    [System.Serializable]
    public struct ClassificationResult
    {
        public float CenterX;
        public float CenterY;
        public float Width;
        public float Height;
        public string Label;
        public float Confidence;
        public int ClassID;
    }

    [Header("Performance Settings")]
    [Tooltip("Maximum number of boxes to process per frame")]
    [SerializeField] private int maxBoxesPerFrame = 5;
    
    [Tooltip("Maximum number of results to keep in memory")]
    [SerializeField] private int maxResultsInMemory = 100;
    
    private Coroutine segmentationCoroutine;
    private Coroutine cameraProcessingCoroutine;
    
    void Start()
    {
        Application.targetFrameRate = 60;
        
        // 从SuperAdmin获取配置
        LoadConfigFromSuperAdmin();
        
        // 初始化EnterpriseCameraAccessManager
        ecam = FindFirstObjectByType<EnterpriseCameraAccessManager>();
        if (ecam == null)
        {
            Debug.LogError("EnterpriseCameraAccessManager not found in scene!");
            return;
        }
        
        // 解析神经网络标签
        if (classesAsset != null)
        {
            labels = classesAsset.text.Split('\n');
        }
        
        // 加载模型
        LoadModel();
        
        // 创建渲染纹理
        if (imageWidth > 0 && imageHeight > 0)
        {
            targetRT = new RenderTexture(imageWidth, imageHeight, 0);
            tempRT = new RenderTexture(imageWidth, imageHeight, 0);
        }
        
        Debug.Log($"Model input size: {imageWidth}x{imageHeight}");
        
        // 初始化UI绘制
        InitializeUI();
        
        // 启动相机处理协程
        StartCameraProcessing();
    }
    
    /// <summary>
    /// 从SuperAdmin加载YOLO配置
    /// </summary>
    private void LoadConfigFromSuperAdmin()
    {
        if (SuperAdmin.superAdmin != null)
        {
            modelAsset = SuperAdmin.superAdmin.yoloModelAsset;
            imageWidth = SuperAdmin.superAdmin.modelInputWidth;
            imageHeight = SuperAdmin.superAdmin.modelInputHeight;
            iouThreshold = SuperAdmin.superAdmin.iouThreshold;
            scoreThreshold = SuperAdmin.superAdmin.scoreThreshold;
            inferenceInterval = SuperAdmin.superAdmin.inferenceInterval;
            enableYoloDebugLogs = SuperAdmin.superAdmin.enableYoloDebugLogs;
            
            Debug.Log($"MainCamera: Loaded config from SuperAdmin - Model: {modelAsset?.name}, Size: {imageWidth}x{imageHeight}, IOU: {iouThreshold}, Score: {scoreThreshold}, Interval: {inferenceInterval}, DebugLogs: {enableYoloDebugLogs}");
        }
        else
        {
            Debug.LogWarning("MainCamera: SuperAdmin not found! Using default values.");
            // 使用默认值
            imageWidth = imageWidth == 0 ? 640 : imageWidth;
            imageHeight = imageHeight == 0 ? 640 : imageHeight;
            iouThreshold = 0.5f;
            scoreThreshold = 0.5f;
            inferenceInterval = 0.1f;
            enableYoloDebugLogs = false;
        }
    }
    
    void OnEnable()
    {
        if (ecam != null)
        {
            StartCameraProcessing();
        }
    }
    
    void OnDisable()
    {
        StopCameraProcessing();
    }
    
    void InitializeUI()
    {
        // 初始化绘制位置
        if (ImageUI != null)
        {
            displayLocation = ImageUI.transform;
        }
        
        // 创建边框精灵
        if (borderTexture != null)
        {
            borderSprite = Sprite.Create(borderTexture, new Rect(0, 0, borderTexture.width, borderTexture.height), 
                new Vector2(borderTexture.width / 2, borderTexture.height / 2));
        }
    }
    
    void LoadModel()
    {
        if (modelAsset == null)
        {
            Debug.LogError("Model asset is not assigned!");
            return;
        }
        
        // 加载模型
        var model = ModelLoader.Load(modelAsset);
        
        // 使用手动设置的输入维度信息
        var inputShape = model.inputs[0].shape;
        // imageWidth 和 imageHeight 由用户在Inspector中设置
        
        Debug.Log($"Model input shape: {inputShape}");
        Debug.Log($"Model outputs count: {model.outputs.Count}");
        
        // 初始化中心点到角点转换矩阵
        centersToCorners = new Tensor<float>(new TensorShape(4, 4),
        new float[]
        {
            1,      0,      1,      0,
            0,      1,      0,      1,
            -0.5f,  0,      0.5f,   0,
            0,      -0.5f,  0,      0.5f
        });
        
        if (taskType == TaskType.Detection)
        {
            LoadDetectionModel(model);
        }
        else
        {
            LoadSegmentationModel(model);
        }
    }
    
    void LoadDetectionModel(Model model)
    {
        // 使用NMS处理模型输出
        var graph = new FunctionalGraph();
        var inputs = graph.AddInputs(model);
        var modelOutput = Functional.Forward(model, inputs)[0];
        var boxCoords = modelOutput[0, 0..4, ..].Transpose(0, 1);
        var allScores = modelOutput[0, 4.., ..];
        var scores = Functional.ReduceMax(allScores, 0);
        var classIDs = Functional.ArgMax(allScores, 0);
        var boxCorners = Functional.MatMul(boxCoords, Functional.Constant(centersToCorners));
        var indices = Functional.NMS(boxCorners, scores, iouThreshold, scoreThreshold);
        var coords = Functional.IndexSelect(boxCoords, 0, indices);
        var labelIDs = Functional.IndexSelect(classIDs, 0, indices);
        var finalScores = Functional.IndexSelect(scores, 0, indices);
        
        // 创建工作线程
        worker = new Worker(graph.Compile(coords, labelIDs, finalScores), backend);
    }
    
    void LoadSegmentationModel(Model model)
    {
        // 分割模型处理，同时输出检测和遮罩
        var graph = new FunctionalGraph();
        var inputs = graph.AddInputs(model);
        var outputs = Functional.Forward(model, inputs);
        var detectionOutput = outputs[0];
        var maskOutput = outputs[1];
        
        var boxCoords = detectionOutput[0, 0..4, ..].Transpose(0, 1);
        var allScores = detectionOutput[0, 4..84, ..];
        var maskCoefs = detectionOutput[0, 84.., ..];
        
        var scores = Functional.ReduceMax(allScores, 0);
        var classIDs = Functional.ArgMax(allScores, 0);
        var boxCorners = Functional.MatMul(boxCoords, Functional.Constant(centersToCorners));
        var indices = Functional.NMS(boxCorners, scores, iouThreshold, scoreThreshold);
        var coords = Functional.IndexSelect(boxCoords, 0, indices);
        var labelIDs = Functional.IndexSelect(classIDs, 0, indices);
        var finalScores = Functional.IndexSelect(scores, 0, indices);
        var selectedMaskCoefs = Functional.IndexSelect(maskCoefs.Transpose(0, 1), 0, indices);
        
        worker = new Worker(graph.Compile(coords, labelIDs, finalScores, selectedMaskCoefs, maskOutput), backend);
    }
    
    void StartCameraProcessing()
    {
        if (cameraProcessingCoroutine == null && ecam != null)
        {
            cameraProcessingCoroutine = StartCoroutine(CameraProcessingLoop());
        }
    }
    
    void StopCameraProcessing()
    {
        if (cameraProcessingCoroutine != null)
        {
            StopCoroutine(cameraProcessingCoroutine);
            cameraProcessingCoroutine = null;
        }
        
        if (inferenceCoroutine != null)
        {
            StopCoroutine(inferenceCoroutine);
            inferenceCoroutine = null;
        }
    }
    
    IEnumerator CameraProcessingLoop()
    {
        while (enabled)
        {
            // 检查是否需要进行新的推理
            if (Time.time - lastInferenceTime >= inferenceInterval && !isInferenceRunning)
            {
                // 获取相机纹理
                if (ecam != null && ecam.CurrentTexture != null)
                {
                    _texture = ecam.CurrentTexture;
                    
                    // 停止上一次推理（如果有）
                    if (inferenceCoroutine != null)
                    {
                        StopCoroutine(inferenceCoroutine);
                    }
                    
                    // 启动新的推理
                    inferenceCoroutine = StartCoroutine(ExecuteInferenceAsync());
                }
            }
            
            yield return new WaitForSeconds(0.02f); // 50 FPS 检查频率
        }
    }
    
    IEnumerator ExecuteInferenceAsync()
    {
        if (worker == null || _texture == null)
        {
            yield break;
        }
        
        isInferenceRunning = true;
        lastInferenceTime = Time.time;
        
        // 准备输入纹理 - 先进行变换 (使用双缓冲)
        Vector2 scale = new Vector2(1, -1);
        Vector2 offset = new Vector2(0, 1);
        Graphics.Blit(_texture, tempRT, scale, offset);
        
        Graphics.Blit(tempRT, targetRT);
        
        // 显示在UI上（可选）
        if (ImageUI != null)
        {
            ImageUI.texture = targetRT;
        }
        
        yield return null; // 让渲染继续
        
        // 创建输入张量并调度推理 - 使用 using 保护
        bool inferenceSuccess = false;
        try
        {
            using (Tensor<float> inputTensor = new Tensor<float>(new TensorShape(1, 3, imageHeight, imageWidth)))
            {
                TextureConverter.ToTensor(targetRT, inputTensor);
                worker.Schedule(inputTensor);
                inferenceSuccess = true;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Inference error: {e.Message}");
            yield break;
        }
        
        if (inferenceSuccess)
        {
            yield return null; // 等待一帧让工作线程处理
            
            // 处理结果
            if (taskType == TaskType.Detection)
            {
                yield return StartCoroutine(ProcessDetectionResultsAsync());
            }
            else
            {
                yield return StartCoroutine(ProcessSegmentationResultsAsync());
            }
        }
        
        isInferenceRunning = false;
    }
    
    IEnumerator ProcessDetectionResultsAsync()
    {
        using var coords = (worker.PeekOutput("output_0") as Tensor<float>)?.ReadbackAndClone();
        using var labelIDs = (worker.PeekOutput("output_1") as Tensor<int>)?.ReadbackAndClone();
        using var scores = (worker.PeekOutput("output_2") as Tensor<float>)?.ReadbackAndClone();
        
        if (coords == null || labelIDs == null || scores == null)
        {
            Debug.LogError("Failed to get YOLO detection outputs");
            yield break;
        }
        
        int boxesFound = coords.shape[0];
        if (enableYoloDebugLogs)
        {
            Debug.Log($"YOLO Detection found {boxesFound} objects");
        }
        
        // 批处理更新结果以避免一帧内处理过多数据
        yield return StartCoroutine(UpdateResultsInBatches(coords, labelIDs, scores));
        
        yield return null;
    }
    
    IEnumerator UpdateResultsInBatches(Tensor<float> coords, Tensor<int> labelIDs, Tensor<float> scores)
    {
        int boxesFound = coords.shape[0];
        int maxBoxesToProcess = Mathf.Min(boxesFound, maxResultsInMemory);
        
        lock (resultsLock)
        {
            currentResults.Clear();
            
            int processedCount = 0;
            
            for (int n = 0; n < maxBoxesToProcess; n++)
            {
                if (labelIDs[n] >= labels.Length) continue;
                
                var result = new ClassificationResult
                {
                    CenterX = coords[n, 0],
                    CenterY = coords[n, 1],
                    Width = coords[n, 2],
                    Height = coords[n, 3],
                    Label = labels[labelIDs[n]],
                    Confidence = scores[n],
                    ClassID = labelIDs[n]
                };
                
                currentResults.Add(result);
                processedCount++;
                
                // 批处理：每处理一定数量后让出控制权
                if (processedCount >= maxBoxesPerFrame)
                {
                    processedCount = 0;
                    yield return null; // 让出一帧
                }
            }
            
            // 绘制检测框
            StartCoroutine(DrawDetectionBoxesAsync());
        }
    }
    
    IEnumerator DrawDetectionBoxesAsync()
    {
        // 检查是否应该显示YOLO结果
        if (SuperAdmin.superAdmin != null && !SuperAdmin.superAdmin.isShowYoloResult)
        {
            // 不显示结果，清理旧的框并直接返回
            ClearAnnotations();
            yield break;
        }
        
        if (displayLocation == null || currentResults == null)
            yield break;
            
        // 清理旧的框
        ClearAnnotations();
        
        if (ImageUI == null)
            yield break;
            
        float displayWidth = ImageUI.rectTransform.rect.width;
        float displayHeight = ImageUI.rectTransform.rect.height;
        
        float scaleX = displayWidth / imageWidth;
        float scaleY = displayHeight / imageHeight;
        
        int processedCount = 0;
        
        lock (resultsLock)
        {
            for (int i = 0; i < currentResults.Count; i++)
            {
                var result = currentResults[i];
                
                // 转换坐标系统
                var box = new BoundingBox
                {
                    CenterX = (result.CenterX < 1 ? result.CenterX * imageWidth : result.CenterX) * scaleX - displayWidth / 2,
                    CenterY = (result.CenterY < 1 ? result.CenterY * imageHeight : result.CenterY) * scaleY - displayHeight / 2,
                    Width = (result.Width < 1 ? result.Width * imageWidth : result.Width) * scaleX,
                    Height = (result.Height < 1 ? result.Height * imageHeight : result.Height) * scaleY,
                    Label = result.Label
                };
                
                DrawBox(box, i, displayHeight * 0.05f);
                processedCount++;
                
                // 批处理绘制
                if (processedCount >= maxBoxesPerFrame)
                {
                    processedCount = 0;
                    yield return null;
                }
            }
        }
    }
    
    public struct BoundingBox
    {
        public float CenterX;
        public float CenterY;
        public float Width;
        public float Height;
        public string Label;
    }
    
    public void DrawBox(BoundingBox box, int id, float fontSize)
    {
        // 创建或获取边界框
        GameObject panel;
        if (id < boxPool.Count)
        {
            panel = boxPool[id];
            panel.SetActive(true);
        }
        else
        {
            panel = CreateNewBox(Color.yellow);
        }
        
        // 设置框位置
        panel.transform.localPosition = new Vector3(box.CenterX, -box.CenterY);
        
        // 设置框大小
        RectTransform rt = panel.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(box.Width, box.Height);
        
        // 设置标签文本
        var label = panel.GetComponentInChildren<Text>();
        if (label != null)
        {
            label.text = box.Label;
            label.fontSize = (int)fontSize;
        }
    }
    
    public GameObject CreateNewBox(Color color)
    {
        if (displayLocation == null)
            return null;
            
        // 创建框
        var panel = new GameObject("ObjectBox");
        panel.AddComponent<CanvasRenderer>();
        Image img = panel.AddComponent<Image>();
        img.color = color;
        
        if (borderSprite != null)
        {
            img.sprite = borderSprite;
            img.type = Image.Type.Sliced;
        }
        
        panel.transform.SetParent(displayLocation, false);
        
        // 创建标签
        if (font != null)
        {
            var text = new GameObject("ObjectLabel");
            text.AddComponent<CanvasRenderer>();
            text.transform.SetParent(panel.transform, false);
            Text txt = text.AddComponent<Text>();
            txt.font = font;
            txt.color = color;
            txt.fontSize = 40;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            
            RectTransform rt2 = text.GetComponent<RectTransform>();
            rt2.offsetMin = new Vector2(20, rt2.offsetMin.y);
            rt2.offsetMax = new Vector2(0, rt2.offsetMax.y);
            rt2.offsetMin = new Vector2(rt2.offsetMin.x, 0);
            rt2.offsetMax = new Vector2(rt2.offsetMax.x, 30);
            rt2.anchorMin = new Vector2(0, 0);
            rt2.anchorMax = new Vector2(1, 1);
        }
        
        boxPool.Add(panel);
        return panel;
    }
    
    public void ClearAnnotations()
    {
        for (int i = 0; i < boxPool.Count; i++)
        {
            if (boxPool[i] != null)
            {
                boxPool[i].SetActive(false);
            }
        }
    }
    
    IEnumerator ProcessSegmentationResultsAsync()
    {
        using var coords = (worker.PeekOutput("output_0") as Tensor<float>)?.ReadbackAndClone();
        using var labelIDs = (worker.PeekOutput("output_1") as Tensor<int>)?.ReadbackAndClone();
        using var scores = (worker.PeekOutput("output_2") as Tensor<float>)?.ReadbackAndClone();
        
        if (coords == null || labelIDs == null || scores == null)
        {
            Debug.LogError("Failed to get YOLO segmentation outputs");
            yield break;
        }
        
        int instancesFound = coords.shape[0];
        if (enableYoloDebugLogs)
        {
            Debug.Log($"YOLO Segmentation found {instancesFound} instances");
        }
        
        // 批处理更新结果以避免一帧内处理过多数据
        yield return StartCoroutine(UpdateResultsInBatches(coords, labelIDs, scores));
        
        yield return null;
    }
    
    // 公共方法：根据像素坐标获取类别
    public string GetClassificationAtPixel(Vector2 pixelCoord)
    {
        return GetClassificationAtPixel(pixelCoord.x, pixelCoord.y);
    }
    
    public string GetClassificationAtPixel(float x, float y)
    {
        lock (resultsLock)
        {
            x*= imageWidth;
            y*= imageHeight;
            foreach (var result in currentResults)
            {
                if (enableYoloDebugLogs)
                {
                    Debug.Log($"Checking pixel ({x}, {y}) against box: Center({result.CenterX}, {result.CenterY}), Size({result.Width}x{result.Height}), Label: {result.Label}");
                }
                // 检查点是否在边界框内
                float left = result.CenterX - result.Width / 2;
                float right = result.CenterX + result.Width / 2;
                float top = result.CenterY - result.Height / 2;
                float bottom = result.CenterY + result.Height / 2;
                
                if (x >= left && x <= right &&
                    y >= top && y <= bottom)
                {
                    return result.Label;
                }
            }
        }
        
        return null; // 未找到类别
    }
    
    // 获取所有当前的分类结果
    public List<ClassificationResult> GetCurrentResults()
    {
        lock (resultsLock)
        {
            return new List<ClassificationResult>(currentResults);
        }
    }
    
    void OnDestroy()
    {
        StopCameraProcessing();
        
        // 停止所有协程
        if (inferenceCoroutine != null)
        {
            StopCoroutine(inferenceCoroutine);
            inferenceCoroutine = null;
        }
        
        if (segmentationCoroutine != null)
        {
            StopCoroutine(segmentationCoroutine);
            segmentationCoroutine = null;
        }
        
        // 清理 Tensor 资源
        try 
        {
            centersToCorners?.Dispose();
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Error disposing centersToCorners: {e.Message}");
        }
        
        try
        {
            worker?.Dispose();
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Error disposing worker: {e.Message}");
        }
        
        // 清理 RenderTexture
        if (targetRT != null)
        {
            try
            {
                targetRT.Release();
                Destroy(targetRT);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Error releasing targetRT: {e.Message}");
            }
            finally
            {
                targetRT = null;
            }
        }
        
        if (tempRT != null)
        {
            try
            {
                tempRT.Release();
                Destroy(tempRT);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Error releasing tempRT: {e.Message}");
            }
            finally
            {
                tempRT = null;
            }
        }
        
        // 清理 UI 对象池
        try
        {
            foreach (var box in boxPool)
            {
                if (box != null)
                {
                    Destroy(box);
                }
            }
            boxPool.Clear();
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Error clearing box pool: {e.Message}");
        }
        
        // 清理结果列表
        lock (resultsLock)
        {
            currentResults?.Clear();
        }
    }
    
    // 添加OnApplicationPause方法处理应用暂停/恢复
    void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            StopCameraProcessing();
        }
        else if(enabled)
        {
            StartCameraProcessing();
        }
    }
}