using UnityEngine;
using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;

namespace FeltSight
{
    public class FeltSightBLEBridge : MonoBehaviour
    {
        // Singleton instance
        public static FeltSightBLEBridge Instance { get; private set; }

        // Events
        public event Action<string> OnPeripheralDiscovered;
        public event Action OnConnected;
        public event Action OnDisconnected;
        public event Action<byte[]> OnSensorDataReceived;

        // Import Objective-C functions
        #if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void _startScanning();

        [DllImport("__Internal")]
        private static extern void _stopScanning();

        [DllImport("__Internal")]
        private static extern void _connectToPeripheral(string peripheralId);

        [DllImport("__Internal")]
        private static extern void _disconnect();

        [DllImport("__Internal")]
        private static extern void _sendHapticData(byte[] data, int length);
        #endif

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
        }

        public void StartScanning()
        {
            #if UNITY_IOS && !UNITY_EDITOR
            _startScanning();
            #endif
        }

        public void StopScanning()
        {
            #if UNITY_IOS && !UNITY_EDITOR
            _stopScanning();
            #endif
        }

        public void ConnectToPeripheral(string peripheralId)
        {
            #if UNITY_IOS && !UNITY_EDITOR
            _connectToPeripheral(peripheralId);
            #endif
        }

        public void Disconnect()
        {
            #if UNITY_IOS && !UNITY_EDITOR
            _disconnect();
            #endif
        }

        public void SendHapticData(byte[] data)
        {
            #if UNITY_IOS && !UNITY_EDITOR
            _sendHapticData(data, data.Length);
            #endif
        }

        // Called from Objective-C
        private void OnPeripheralDiscovered(string peripheralId)
        {
            OnPeripheralDiscovered?.Invoke(peripheralId);
        }

        // Called from Objective-C
        private void OnConnected()
        {
            OnConnected?.Invoke();
        }

        // Called from Objective-C
        private void OnDisconnected()
        {
            OnDisconnected?.Invoke();
        }

        // Called from Objective-C
        private void OnSensorDataReceived(string base64Data)
        {
            try
            {
                byte[] data = Convert.FromBase64String(base64Data);
                OnSensorDataReceived?.Invoke(data);
            }
            catch (Exception e)
            {
                Debug.LogError($"Error decoding sensor data: {e.Message}");
            }
        }
    }
} 