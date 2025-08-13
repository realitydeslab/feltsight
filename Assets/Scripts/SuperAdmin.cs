using UnityEngine;
using Sentry.Unity;


public class SuperAdmin : MonoBehaviour
{
    public bool isEnableBLE=true;

    public BLESendJointV Ble;
    
    
    void Start()
    {
        if (!Ble)
        {
            Debug.LogError("BLE component is not assigned in SuperAdmin script!");
        }
        else
        {
            Ble.enabled = isEnableBLE;
        }
#if UNITY_VISIONOS && !UNITY_EDITOR
        
        SentrySdk.CaptureMessage("Felsight Start on VP");
        #endif
#if UNITY_EDITOR
        SentrySdk.CaptureMessage("Felsight Start on Editor");
        #endif
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void QuitApplication()
    {
        Application.Quit();
    }
    
}
