#if UNITY_EDITOR_OSX || UNITY_IOS || UNITY_VISIONOS || UNITY_VISIONOS
namespace UnityCoreBluetooth.NativeInterface
{
    public class ImportConfig
    {
#if UNITY_EDITOR_OSX || OSXEditor
        public const string TargetName = "mcUnityCoreBluetooth";
#elif UNITY_IOS || UNITY_VISIONOS
        public const string TargetName = "__Internal";
#endif
    }
}
#endif
