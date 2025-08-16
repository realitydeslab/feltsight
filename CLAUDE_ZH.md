# CLAUDE.md (中文版)

该文件为 Claude Code (claude.ai/code) 在此代码库中工作时提供指导。

## 项目概览

FeltSight 是一个结合了 Apple Vision Pro 和定制触觉手套的艺术项目，创造沉浸式艺术体验。项目包括：

1. **Unity Vision Pro 应用程序** - 运行在 Vision Pro 上的主应用，具有手部追踪和触觉反馈功能
2. **触觉手套硬件** - 基于 Adafruit Feather nRF52840 微控制器的定制手套  
3. **固件** - 基于 Arduino 的手套控制和 BLE 通信固件
4. **硬件设计** - 电路图和元器件规格说明

## Unity 项目结构

这是一个为 Apple Vision Pro 开发配置的 Unity 6000.1.5f1 项目，包含以下关键组件：

### 核心系统
- **手部追踪**：使用 Unity XR Hands 和自定义 `MyHand.cs` 组件进行手部姿态追踪和距离计算
- **BLE 通信**：`BLESendJointV.cs` 处理与触觉手套的蓝牙通信，使用手指速度映射
- **视觉特效**：基于 VFX Graph 的视觉特效位于 `/Assets/VFX/`，由 `VFXMan.cs` 控制器管理
- **AR 网格处理**：处理 Vision Pro 网格数据进行空间交互
- **YOLO 集成**：使用 Barracuda 进行目标检测/分割的计算机视觉管道

### 关键脚本架构

#### 手部追踪 (`/Assets/Scripts/`)
- `MyHand.cs` - 核心手部追踪，包含姿态数据、手掌距离计算和手指速度追踪
- `HandRaycaster.cs` - 基于手部的射线投射用于空间交互  
- `HandVisualizer.cs` - 手部数据的视觉表现

#### BLE 通信
- `BLESendJointV.cs` - 将手指速度 (0-0.3 m/s) 映射到 BLE 速度参数 (1.0x-4.0x)，带有 OneDollar 滤波
- `ReadSthFromServer.cs` - 从网络服务器获取配置参数并应用到 BLESendJointV
- `/Assets/Scripts/Library/CoreBluetooth/` - Unity-iOS 通信的核心蓝牙封装
- `/Assets/Scripts/Library/NativeInterface/` - 原生 iOS 接口层

#### 计算机视觉 (`/Assets/Scripts/Library/Yolo/`)
- `Detector.cs` - 使用 Barracuda 的主 YOLO 检测控制器
- `YOLOv8.cs` 和 `YOLOv8Segmentation.cs` - YOLO 模型实现
- `/Assets/Scripts/Library/Yolo/TextureProviders/` - 摄像头和视频输入提供器

#### VFX 系统 (`/Assets/VFX/`)
- `VFXMan.cs` - 控制 Visual Effect Graph 资产与 AR 网格集成
- 用于空间特效和网格交互的自定义 VFX 操作器

### Unity 包
`manifest.json` 中的关键依赖项：
- `com.unity.xr.visionos`: "2.2.4" - Vision Pro 平台支持
- `com.unity.xr.hands`: "1.5.1" - 手部追踪
- `com.unity.xr.interaction.toolkit`: "3.1.2" - XR 交互  
- `com.unity.visualeffectgraph`: "17.1.0" - VFX 系统
- `com.unity.barracuda` - 机器学习推理
- `com.unity.render-pipelines.universal`: "17.1.0" - URP 渲染

### 场景
- `DebugHandGestures.unity` - 手部追踪测试和调试
- `DebugVFX.unity` - 视觉特效测试
- `YoloScenes/Detection.unity` - 目标检测测试
- `YoloScenes/Segmentation.unity` - 分割测试

## 开发命令

Unity 项目通常不使用传统的构建命令。开发工作流程：

1. **打开项目**：在 Unity 6000.1.5f1 或更高版本中打开
2. **平台设置**：在 Build Settings 中切换到 visionOS 平台  
3. **构建**：使用 Unity 的 Build Settings → Build 或 Build And Run
4. **测试**：使用 Vision Pro 模拟器或设备部署

## 硬件集成

### 触觉手套规格
- **控制器**：Adafruit Feather nRF52840（单个控制器控制双手）
- **马达**：10x 触觉反馈马达（每只手5个）
- **通信**：自定义协议的 BLE
- **电源**：3.7V LiPo 电池，向双手供电
- **接线**：每只手7针连接器（电源 + 5个马达控制）

### BLE 协议
从 Unity 发送到手套的命令：
- `H<finger><intensity>` - 设置触觉反馈（finger: 0-4，intensity: 0-255）
- `C` - 传感器校准  
- `B` - 获取电池电量

接收的 JSON 数据：
```json
{
  "flex": [0, 0, 0, 0, 0],
  "imu": {"accel": {"x": 0, "y": 0, "z": 0}, "gyro": {"x": 0, "y": 0, "z": 0}},
  "battery": 100
}
```

## 关键技术细节

### 手部追踪功能
- 双手追踪，手掌距离计算（典型范围 0.4-0.8m）
- 带有 OneEuro 滤波的手指速度映射，平滑数据处理
- 基于速度阈值的音量控制（低于 0.015 m/s 时静音）
- 通过网络服务器远程配置滤波设置、速度阈值和映射参数

### AR 集成  
- 使用 ARMeshManager 进行 Vision Pro 网格管理
- 空间锚定和网格合并以优化性能
- 相机相对坐标转换

### 性能考虑
- VFX 网格合并以 0.5 秒间隔运行以优化性能
- BLE 数据发送可配置间隔（默认 0.5 秒）
- YOLO 推理针对 Vision Pro 实时处理进行优化

### 远程配置
- 通过 `ReadSthFromServer.cs` 进行网络服务器集成，实现运行时参数调整
- 可配置参数包括：
  - 速度比率乘数，用于调整速度映射敏感度
  - 最大速度阈值，用于映射范围的上限（默认 0.3 m/s）
  - 音量阈值，用于静音控制（默认 0.015 m/s）
  - 速度滤波和大小滤波的开关
  - 滤波强度参数，用于微调响应平滑度
- 所有配置按固定时间间隔获取（默认 5.0 秒）

## 固件开发

位于 `Hardware~/firmware/`：
- nRF52840 的 Arduino IDE 项目
- 所需库：Adafruit nRF52 BSP、Bluefruit nRF52、MPU6050
- 引脚配置在硬件 README 文件中有记录

## 常见开发任务

- **手部追踪调试**：使用 `DebugHandGestures` 场景和 `MyHand` 组件日志记录
- **BLE 测试**：监控 `BLESendJointV` 速度映射和连接状态
- **VFX 开发**：在 `DebugVFX` 场景中使用 `VFXMan` 进行空间特效开发
- **YOLO 测试**：使用 `YoloScenes/` 文件夹中的专用场景

## 架构说明

项目采用模块化架构，清晰分离以下部分：
1. **输入系统**（手部追踪、摄像头）
2. **处理**（YOLO、速度滤波、VFX）  
3. **输出系统**（BLE 到手套、视觉特效）
4. **硬件集成**（原生 iOS CoreBluetooth 桥接）

所有主要系统通过 Unity 的组件系统进行通信，具有用于跨系统数据共享的公共接口。

## 最近的架构变更

BLESendJointV 和 ReadSthFromServer 之间的通信模式已更新：
- 之前的逻辑：BLESendJointV 直接从 ReadSthFromServer 组件读取值
- 当前逻辑：ReadSthFromServer 在接收到值时直接调用 BLESendJointV 的公共 API 方法
- 这创建了更清晰的依赖方向和更易维护的架构
- 控制的参数包括速度比率、滤波器状态、滤波器强度和速度阈值