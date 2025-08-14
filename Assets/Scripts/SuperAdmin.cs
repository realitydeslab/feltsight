using System;
using UnityEngine;
using Sentry.Unity;


public class SuperAdmin : MonoBehaviour
{
    [Header("设置区")]
    public bool isEnableBLE=true;
    
    [Header("Leave me alone")]
    public BLESendJointV Ble;

    private void Awake()
    {
        #if UNITY_VISIONOS
        isEnableBLE = true;
        #endif
        if (!Ble)
        {
            Debug.LogError("BLE component is not assigned in SuperAdmin script!");
        }
        else
        {
            Ble.enabled = isEnableBLE;
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
