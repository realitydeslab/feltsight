using UnityEngine;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using System.IO;

public class VisionOSBuildPostProcessor
{
    [PostProcessBuild(1)]
    public static void OnPostprocessBuild(BuildTarget buildTarget, string pathToBuiltProject)
    {
        // VisionOS使用iOS构建目标
        if (buildTarget != BuildTarget.iOS)
            return;

        Debug.Log("开始VisionOS构建后处理...");

        // 修改Info.plist
        ModifyInfoPlist(pathToBuiltProject);
        
        // 修改Xcode项目设置
        ModifyXcodeProject(pathToBuiltProject);
        
        Debug.Log("VisionOS构建后处理完成");
    }

    private static void ModifyInfoPlist(string pathToBuiltProject)
    {
        string plistPath = Path.Combine(pathToBuiltProject, "Info.plist");
        PlistDocument plist = new PlistDocument();
        plist.ReadFromString(File.ReadAllText(plistPath));
        PlistElementDict rootDict = plist.root;

        // 添加蓝牙权限声明
        // Privacy - Bluetooth Always Usage Description
        rootDict.SetString("NSBluetoothAlwaysUsageDescription", 
            "此应用需要使用蓝牙连接手套");
        
        // Privacy - Bluetooth Peripheral Usage Description  
        rootDict.SetString("NSBluetoothPeripheralUsageDescription", 
            "此应用需要使用蓝牙连接手套");

        // 后台模式（如果需要）
        // PlistElementArray backgroundModes;
        // if (rootDict.values.ContainsKey("UIBackgroundModes"))
        // {
        //     backgroundModes = rootDict["UIBackgroundModes"].AsArray();
        // }
        // else
        // {
        //     backgroundModes = rootDict.CreateArray("UIBackgroundModes");
        // }
        //
        // // 添加蓝牙后台模式（如果还没有的话）
        // bool hasCentral = false;
        // bool hasPeripheral = false;
        //
        // for (int i = 0; i < backgroundModes.values.Count; i++)
        // {
        //     string mode = backgroundModes.values[i].AsString();
        //     if (mode == "bluetooth-central") hasCentral = true;
        //     if (mode == "bluetooth-peripheral") hasPeripheral = true;
        // }
        //
        // if (!hasCentral)
        //     backgroundModes.AddString("bluetooth-central");
        // if (!hasPeripheral)
        //     backgroundModes.AddString("bluetooth-peripheral");

        // 保存文件
        File.WriteAllText(plistPath, plist.WriteToString());
        Debug.Log("Info.plist 蓝牙权限添加完成");
        Debug.Log("已添加: NSBluetoothAlwaysUsageDescription");
        Debug.Log("已添加: NSBluetoothPeripheralUsageDescription");
    }

    private static void ModifyXcodeProject(string pathToBuiltProject)
    {
        string projectPath = PBXProject.GetPBXProjectPath(pathToBuiltProject);
        PBXProject pbxProject = new PBXProject();
        pbxProject.ReadFromFile(projectPath);

        // 获取target GUID
        string targetGuid = pbxProject.GetUnityMainTargetGuid();
        string frameworkTargetGuid = pbxProject.GetUnityFrameworkTargetGuid();

        // 添加CoreBluetooth框架
        pbxProject.AddFrameworkToProject(targetGuid, "CoreBluetooth.framework", false);
        
        // 如果使用了UnityFramework
        if (!string.IsNullOrEmpty(frameworkTargetGuid))
        {
            pbxProject.AddFrameworkToProject(frameworkTargetGuid, "CoreBluetooth.framework", false);
        }

        // 保存项目文件
        pbxProject.WriteToFile(projectPath);
        Debug.Log("Xcode项目设置修改完成 - 已添加CoreBluetooth.framework");
    }
}
