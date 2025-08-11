using UnityEngine;
using Sentry;
using Sentry.Unity; // On the top of the script

public class SuperAdmin : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
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
