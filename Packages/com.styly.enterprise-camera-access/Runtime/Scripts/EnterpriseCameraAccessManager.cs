using System.Collections;
using AOT;
using UnityEngine;
using System.Runtime.InteropServices;
using UnityEngine.UI;
using System;

public class EnterpriseCameraAccessManager : MonoBehaviour
{
    public static EnterpriseCameraAccessManager Instance { get; private set; }
    public Material PreviewMaterial;

    [Tooltip("WebCam will be used for Editor mode or Smartphone. Default camera is used if this field is empty.")]
    public string WebCamDeviceName = "";

    private WebCamTexture webCamTexture;
    private Texture2D tmpTexture = null;
    private string tempBase64String = null;
    private float skipSeconds = 0.1f;
#if UNITY_VISIONOS && !UNITY_EDITOR
    private bool _hasSetTexture = false;
    private Texture2D _texture;
    private RenderTexture _renderTexture;
    private IntPtr _texturePtr;
    private int _width = 1920;
    private int _height = 1080;
#endif

    /// <summary>
    /// Public property to access the current camera texture
    /// </summary>
    public Texture2D CurrentTexture 
    { 
        get 
        { 
#if UNITY_VISIONOS && !UNITY_EDITOR
            return _texture;
#else
            return tmpTexture;
#endif
        } 
    }


    /// <summary>
    /// Get Vision Pro main camera image as texture2D.
    /// </summary>
    /// <returns></returns>
    public Texture2D GetMainCameraTexture2D()
    {
#if UNITY_VISIONOS && !UNITY_EDITOR
        Base64ToTexture2D(tmpTexture, tempBase64String);
#endif
        return tmpTexture;
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this.gameObject); }
        else { Instance = this; DontDestroyOnLoad(this.gameObject); }
    }

    void OnEnable()
    {
#if UNITY_VISIONOS && !UNITY_EDITOR
        // No setup required for native texture capture
#endif
    }

    void Start()
    {


#if UNITY_VISIONOS && !UNITY_EDITOR
        _renderTexture = new RenderTexture(_width, _height, 1, RenderTextureFormat.ARGB32);
        _renderTexture.enableRandomWrite = true;
        _renderTexture.Create();
        PreviewMaterial.mainTexture = _renderTexture;
        startCapture();
        return;
#endif

#if UNITY_EDITOR
        StartWebCam(WebCamDeviceName);
#elif UNITY_IOS
        StartCoroutine(RequestCameraPermission_iOS());
#else
        StartWebCam(WebCamDeviceName);
#endif
    }

    void OnDisable()
    {

#if UNITY_VISIONOS && !UNITY_EDITOR
        stopCapture();
        return;
#endif
        if (webCamTexture != null) { webCamTexture.Stop(); }
    }

    IEnumerator RequestCameraPermission_iOS()
    {
        yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
        if (Application.HasUserAuthorization(UserAuthorization.WebCam))
        {
            StartWebCam(WebCamDeviceName);
        }
        else
        {
            Debug.Log("Permission denied.");
        }
    }

    IEnumerator RequestCameraPermission_Android()
    {
        if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
        {
            Application.RequestUserAuthorization(UserAuthorization.WebCam);
            yield return new WaitForSeconds(1); // Wait for the result of the authorization request
        }

        if (Application.HasUserAuthorization(UserAuthorization.WebCam))
        {
            StartWebCam(WebCamDeviceName);
        }
        else
        {
            Debug.Log("Permission denied");
        }
    }

    void Update()
    {
#if UNITY_VISIONOS && !UNITY_EDITOR
        if (_hasSetTexture)
        {
            UpdateTexture();
        }
        else
        {
            TryGetTexture();
        }
#else
        // Apply WebCamTexture to material
        ApplyWebcamTextureToMaterial(PreviewMaterial, webCamTexture);

#endif
    }

    void StartWebCam(string deviceName)
    {
        webCamTexture = new WebCamTexture(deviceName);
        webCamTexture.Play();
    }

    // Call function continuously
    IEnumerator ApplyVisionProCameraCaptureToMaterialContinuously()
    {
        while (true)
        {
            yield return new WaitForSeconds(skipSeconds);
            ApplyBase64StringToMaterial(PreviewMaterial, tempBase64String);
        }
    }

    void ApplyWebcamTextureToMaterial(Material material, WebCamTexture webCamTexture)
    {
        if (webCamTexture == null) { return; }
        if (material == null) { return; }
        if (webCamTexture.width <= 16) { return; }
        if (webCamTexture.isPlaying == false) { return; }
        if (tmpTexture == null) { tmpTexture = new Texture2D(webCamTexture.width, webCamTexture.height); }
        tmpTexture.SetPixels(webCamTexture.GetPixels());
        tmpTexture.Apply();
        material.mainTexture = tmpTexture;
    }

    void ApplyBase64StringToMaterial(Material material, string base64String)
    {
        if (base64String == null) { return; }

        // Overwrite the tmpTexture (material.mainTexture) with the base64String
        Base64ToTexture2D(tmpTexture, base64String);
    }

    // Convert Base64String to Texture2D
    void Base64ToTexture2D(Texture2D tex, string base64)
    {
        try
        {
            byte[] imageBytes = System.Convert.FromBase64String(base64);

            // tmpTexture に画像を読み込む
            bool loadSuccess = tex.LoadImage(imageBytes);

            if (!loadSuccess)
            {
                Debug.LogError("Failed to load image from byte array.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to convert base64 string to texture2D: {ex.Message}");
        }
    }

    bool IsVisionOs()
    {
#if UNITY_VISIONOS && !UNITY_EDITOR
        return true;
#endif
        return false;
    }

    delegate void CallbackDelegate(string command);
    [MonoPInvokeCallback(typeof(CallbackDelegate))]
    static void CallbackFromNative(string command)
    {
        Instance.tempBase64String = command;
    }

#if UNITY_VISIONOS && !UNITY_EDITOR
    private void TryGetTexture()
    {
        IntPtr texturePtr = getTexture();
        if (texturePtr == IntPtr.Zero) return;

        _texturePtr = texturePtr;

        if (_texture != null)
        {
            UnityEngine.Object.Destroy(_texture);
        }

        _texture = Texture2D.CreateExternalTexture(_width, _height, TextureFormat.BGRA32, false, false, _texturePtr);
        _texture.UpdateExternalTexture(_texturePtr);
        // 输出纹理分辨率信息
        Debug.Log($"VisionOS Camera Texture Resolution: {_texture.width}x{_texture.height}");

        // スケールとオフセットを使用して上下反転を行う
        // scale.y を -1 にすることで上下反転、offset.y を 1 にすることで位置を調整
        Vector2 scale = new Vector2(1, -1);
        Vector2 offset = new Vector2(0, 1);
        
        Graphics.Blit(_texture, _renderTexture, scale, offset);
        PreviewMaterial.mainTexture = _renderTexture;

        _hasSetTexture = true;
    }

    private void UpdateTexture()
    {
        // スケールとオフセットを使用して上下反転を行う
        Vector2 scale = new Vector2(1, -1);
        Vector2 offset = new Vector2(0, 1);
        
        Graphics.Blit(_texture, _renderTexture, scale, offset);
        // Unity.PolySpatial.PolySpatialObjectUtils.MarkDirty(_renderTexture);
    }
#endif

#if UNITY_VISIONOS && !UNITY_EDITOR
    [DllImport("__Internal")]
    static extern void SetNativeCallbackOfCameraAccess(CallbackDelegate callback);
    [DllImport("__Internal")]
    static extern void StartVisionProMainCameraCapture();
    [DllImport("__Internal")]
    static extern void startCapture();
    [DllImport("__Internal")]
    static extern void stopCapture();
    [DllImport("__Internal")]
    static extern IntPtr getTexture();
#else
    static void SetNativeCallbackOfCameraAccess(CallbackDelegate callback) { }
    static void StartVisionProMainCameraCapture() { }
#endif


}
