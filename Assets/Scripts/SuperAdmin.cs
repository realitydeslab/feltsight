using System;
using UnityEngine;
using Sentry.Unity;


public class SuperAdmin : MonoBehaviour
{
    [Header("设置区")]
    public bool isDebug=true;
    public bool isEnableBLE=true;
    public bool isEnableVFX=true;
    
    [Header("Leave me alone")]
    public BLESendJointV Ble;

    private void Awake()
    {
        #if UNITY_VISIONOS && !UNITY_EDITOR
        isEnableBLE = true;
        #endif
        if (!Ble)
        {
            Debug.LogError("BLE component is not assigned in SuperAdmin script!");
        }
        else
        {
            Ble.enabled = isEnableBLE;
            Debug.Log("BLE component is assigned and enabled: " + isEnableBLE);
        }
    }

    void Start()
    {

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
