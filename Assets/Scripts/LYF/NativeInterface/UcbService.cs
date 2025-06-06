using System;
using System.Runtime.InteropServices;

#if UNITY_EDITOR_OSX || UNITY_IOS || UNITY_VISIONOS
namespace UnityCoreBluetooth.NativeInterface
{
    public class UcbService
    {
        [DllImport(ImportConfig.TargetName)]
        public static extern string ucb_service_getUuid(IntPtr service);

        [DllImport(ImportConfig.TargetName)]
        public static extern void ucb_service_discoverCharacteristic(IntPtr service);

    }
}
#endif