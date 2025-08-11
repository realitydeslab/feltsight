using System.Transactions;
using UnityEngine;
using Sentry;
using Sentry.Unity;

public class MainCamera : MonoBehaviour
{
    public Texture2D _texture;
    EnterpriseCameraAccessManager ecam;
    
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        ecam = this.GetComponent<EnterpriseCameraAccessManager>();
    }

    // Update is called once per frame
    void Update()
    {
        if (ecam.CurrentTexture != null)
        {
            _texture = ecam.CurrentTexture;
            // 输出纹理分辨率信息
            string logMessage = $"VisionOS Camera Texture Resolution: {_texture.width}x{_texture.height}"; // 1920x1080
            Debug.Log(logMessage);
            // SentrySdk.CaptureMessage(logMessage);
        }
    }
}
