using UnityEngine;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine.XR;

namespace FeltSight
{
    public class FeltSightGlovesManager : MonoBehaviour
    {
        // Singleton instance
        public static FeltSightGlovesManager Instance { get; private set; }

        // Data structures matching firmware
        [Serializable]
        public struct HapticData
        {
            public float[] leftFingerIntensities;  // 0-1 values for each finger
            public float[] rightFingerIntensities; // 0-1 values for each finger
            public float patternIntensity;         // 0-1 value for pattern
        }

        // Current state
        private HapticData currentHapticData;
        private bool isConnected = false;
        private float updateInterval = 1f / 60f; // 60Hz
        private float lastUpdateTime;
        private FeltSightBLEBridge bleBridge;

        // Events
        public event Action OnConnected;
        public event Action OnDisconnected;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }

            // Initialize haptic data
            currentHapticData = new HapticData
            {
                leftFingerIntensities = new float[5],
                rightFingerIntensities = new float[5],
                patternIntensity = 0f
            };

            // Initialize BLE bridge
            bleBridge = gameObject.AddComponent<FeltSightBLEBridge>();
            bleBridge.OnConnected += OnBLEConnected;
            bleBridge.OnDisconnected += OnBLEDisconnected;
        }

        private void OnDestroy()
        {
            if (bleBridge != null)
            {
                bleBridge.OnConnected -= OnBLEConnected;
                bleBridge.OnDisconnected -= OnBLEDisconnected;
            }
        }

        private void Update()
        {
            if (!isConnected) return;

            float currentTime = Time.time;
            if (currentTime - lastUpdateTime >= updateInterval)
            {
                UpdateHapticFeedback();
                lastUpdateTime = currentTime;
            }
        }

        public async Task<bool> Connect()
        {
            try
            {
                bleBridge.StartScanning();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to connect: {e.Message}");
                return false;
            }
        }

        public void Disconnect()
        {
            bleBridge.Disconnect();
        }

        public void SetLeftFingerIntensity(int fingerIndex, float intensity)
        {
            if (fingerIndex < 0 || fingerIndex >= 5)
            {
                Debug.LogError("Invalid finger index");
                return;
            }

            currentHapticData.leftFingerIntensities[fingerIndex] = Mathf.Clamp01(intensity);
        }

        public void SetRightFingerIntensity(int fingerIndex, float intensity)
        {
            if (fingerIndex < 0 || fingerIndex >= 5)
            {
                Debug.LogError("Invalid finger index");
                return;
            }

            currentHapticData.rightFingerIntensities[fingerIndex] = Mathf.Clamp01(intensity);
        }

        public void SetPatternIntensity(float intensity)
        {
            currentHapticData.patternIntensity = Mathf.Clamp01(intensity);
        }

        private void UpdateHapticFeedback()
        {
            if (!isConnected) return;

            // Convert HapticData to byte array
            byte[] data = new byte[44]; // Size of HapticData struct
            Buffer.BlockCopy(BitConverter.GetBytes(currentHapticData.patternIntensity), 0, data, 0, 4);
            
            // Left hand intensities
            for (int i = 0; i < 5; i++)
            {
                Buffer.BlockCopy(BitConverter.GetBytes(currentHapticData.leftFingerIntensities[i]), 0, data, (i + 1) * 4, 4);
            }
            
            // Right hand intensities
            for (int i = 0; i < 5; i++)
            {
                Buffer.BlockCopy(BitConverter.GetBytes(currentHapticData.rightFingerIntensities[i]), 0, data, (i + 6) * 4, 4);
            }

            bleBridge.SendHapticData(data);
        }

        private void OnBLEConnected()
        {
            isConnected = true;
            OnConnected?.Invoke();
        }

        private void OnBLEDisconnected()
        {
            isConnected = false;
            OnDisconnected?.Invoke();
        }
    }
} 