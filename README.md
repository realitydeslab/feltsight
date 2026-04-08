# FeltSight

中文文档：[`README.md`](README.md)  
English version: [`README_EN.md`](README_EN.md)

这是一个面向 Apple Vision Pro / visionOS 的 Unity 交互项目，核心目标是把**手部追踪**、**空间网格碰撞分类**与**BLE 触觉手套**连接起来，让用户在接触真实空间表面时获得对应的触觉反馈。

**把 Vision Pro 感知到的“手指接触空间表面”事件，实时转换成十路 BLE 触觉控制信号。**

当前仓库重点是 Unity 端实现：

- [`MyHand`](Assets/Scripts/MyHand.cs) 负责“感知手”. 统一读取双手追踪、关节位置、速度、Palm 替代姿态、双手距离与开合角度。
- [`HandRaycaster`](Assets/Scripts/HandRaycaster.cs) 负责“感知接触”. 从十个手指发射射线，检测手指是否命中空间网格，并读取 visionOS 的 mesh classification。
- 使用 [`VFXMan`](Assets/VFX/VFXMan.cs) 管理 AR Mesh 对应的 VFX 实例、材质参数以及与手部命中点相关的可视化效果。
- [`BLESendJointV`](Assets/Scripts/BLESendJointV.cs) 负责“生成并发送触觉控制”. 将十路手指状态编码为 32 字节 BLE 数据包，发送给外部 ESP32 / 触觉设备。
- [`SuperAdmin`](Assets/Scripts/SuperAdmin.cs) 负责“统一开关与调试展示”. 统一控制调试 UI、平台开关、手部功能和 BLE 功能启停。

---

## 项目定位

FeltSight 可以理解为一个“空间触觉映射”实验：

- Vision Pro 负责感知手和环境。
- Unity 负责把“手指碰到了什么、碰撞时速度多大”转换成可传输的控制信号。
- 外部 BLE 设备负责把这些信号变成触觉反馈。

---

## `Assets/Scripts` 脚本总览

### 核心脚本

1. [`MyHand`](Assets/Scripts/MyHand.cs)

   - 统一接入 `XRHandSubsystem`。
   - 提供左右手 root pose、wrist pose、palm pose 的读取接口。
   - 维护关节历史位置并计算速度，尤其是各指尖速度。
   - 提供双手距离、Palm 距离、Palm 开合角度等高层数据。
   - 当 Palm 关节不可用时，可退化为“四指 Proximal 重心”作为 Palm 替代。
   - 可把调试信息输出到 `TextMeshProUGUI`。

2. [`HandRaycaster`](Assets/Scripts/HandRaycaster.cs)

   - 对左右手五根手指分别做射线检测，共 10 路。
   - 射线方向由 `Distal -> Tip` 计算得到。
   - 支持 One Euro Filter，对关节位置做平滑，减少抖动。
   - 命中后记录 `RaycastHit`，并尝试读取 visionOS mesh face classification。
   - 将命中类别同步到 VFX 和调试 UI。
   - 对外提供 `TryGetFingerHit()`、`TryGetFingerHitClassification()` 等接口，供 BLE 层读取。

3. [`BLESendJointV`](Assets/Scripts/BLESendJointV.cs)

   - 负责 BLE 初始化、扫描、连接、发现服务/特征、发送数据、断线重连。
   - 从 [`MyHand`](Assets/Scripts/MyHand.cs) 读取手指速度，从 [`HandRaycaster`](Assets/Scripts/HandRaycaster.cs) 读取是否命中。
   - 为 10 个手指分别计算：
     - 是否触发音量
     - 当前速度映射值
     - 最终发送字节
   - 支持速度阈值、音量阈值、滤波、倍率、自动重连。
   - 周期性生成 32 字节数据包并写入 BLE RX characteristic。

4. [`SuperAdmin`](Assets/Scripts/SuperAdmin.cs)
   - 项目级总控脚本。
   - 管理调试 UI 是否显示。
   - 管理手部功能总开关、BLE 总开关。
   - 根据平台设置当前运行环境（Editor / VisionOS）。
   - 维护十个手指命中信息的 UI 文本。
   - 持有 [`BLESendJointV`](Assets/Scripts/BLESendJointV.cs) 引用并控制其启用状态。

### 辅助脚本

5. [`OneDollarFilter`](Assets/Scripts/OneDollarFilter.cs)

   - 一个简单低通滤波器组件。
   - 可对 `Vector3` 或 `float` 数据做平滑。
   - 当前主要被 [`BLESendJointV`](Assets/Scripts/BLESendJointV.cs) 用于速度与速度幅值平滑。
   - 名称叫 One Dollar，但实现更接近指数平滑滤波。

6. [`HandVisualizer`](Assets/Scripts/HandVisualizer.cs)

   - 用于可视化手部关节与骨架连线。
   - 依赖 `XR Hands` 与 visionOS 扩展宏。
   - 会为左右手创建关节可视化对象，并在追踪更新时刷新位置。
   - 更偏向调试 / 演示用途。

7. [`ChangeMaterialRandomColor`](Assets/Scripts/ChangeMaterialRandomColor.cs)
   - 启动时给物体材质随机赋色。
   - 是一个非常轻量的测试脚本。

### 测试脚本

8. [`TEST/BLETEST`](Assets/Scripts/TEST/BLETEST.cs)
   - 独立 BLE 测试脚本。
   - 连接到目标设备后，周期性发送模拟 32 字节测试数据。
   - 用于在不依赖手部追踪逻辑时验证 BLE 通路是否正常。

---

## 核心运行流程

### 1. 手部数据采集

[`MyHand`](Assets/Scripts/MyHand.cs) 在运行时查找 `XRHandSubsystem`，并持续更新：

- 左右手 root pose
- wrist pose
- palm pose / proximal centroid fallback
- 双手距离
- Palm 距离
- Palm 开合角度
- 关键关节速度缓存

其中对外最重要的接口是：

- `TryGetJointPositionAndVelocity(...)`
- `TryGetPalmPose(...)`
- `TryGetHandsDistance(...)`
- `TryGetPalmDistance(...)`
- `TryGetPalmAngle(...)`
- `GetAllFingertipsData(...)`

### 2. 手指射线与空间分类

[`HandRaycaster`](Assets/Scripts/HandRaycaster.cs) 每帧对左右手五指执行射线检测：

- 从指尖位置发射射线。
- 如果命中 collider，则记录命中点、法线、分类结果。
- 在 visionOS 下，会通过 `XRMeshSubsystem.GetFaceClassifications(...)` 读取三角面分类。
- 分类结果会同步到 VFX 和 UI。

这一步的意义是：**判断某根手指当前是否真的“碰到”了空间表面，以及碰到的是什么类型的表面。**

#### mesh classification 完整类别与映射说明

当前 [`HandRaycaster`](Assets/Scripts/HandRaycaster.cs:488) 在 visionOS 下会先读取原始 mesh classification，再映射成项目内部使用的触觉 / VFX 种类索引。

##### VisionOS 原始完整分类

以当前项目安装的 VisionOS 包 [`ARMeshClassification`](Library/PackageCache/com.unity.xr.visionos@c88aa5b2830f/Runtime/ARMeshClassification.cs:9) 为准，完整分类如下：

| 原始枚举值 | VisionOS 分类    |
| ---------- | ---------------- |
| `0`        | `None`           |
| `1`        | `Wall`           |
| `2`        | `Floor`          |
| `3`        | `Ceiling`        |
| `4`        | `Table`          |
| `5`        | `Seat`           |
| `6`        | `Window`         |
| `7`        | `Door`           |
| `8`        | `WallDecoration` |
| `9`        | `Blinds`         |
| `10`       | `Fireplace`      |
| `11`       | `Stairs`         |
| `12`       | `Bed`            |
| `13`       | `Counter`        |
| `14`       | `Cabinet`        |
| `15`       | `HomeAppliance`  |
| `16`       | `DoorFrame`      |
| `17`       | `TV`             |
| `18`       | `Whiteboard`     |
| `19`       | `Plant`          |

##### 项目内部映射后的种类索引

当前项目不会直接使用上面的原始枚举值，而是通过 [`MapVisionOSClassificationToHitTypeIndex()`](Assets/Scripts/HandRaycaster.cs:520) 映射为内部 `typeIndex / HitColorIndex`。

映射规则如下：

| VisionOS 分类 | 映射后的种类 int |
| ------------- | ---------------- |
| `Seat`        | `3`              |
| `Table`       | `4`              |
| `Floor`       | `5`              |
| `Wall`        | `5`              |
| `Plant`       | `10`             |
| `TV`          | `11`             |
| 其他所有分类  | `0`              |

说明：

- 这里“第几种”就是映射后的整数值本身
- `0` 表示 `Unknown / 未定义种类`
- UI 和控制台会显示原始识别分类名
- VFX 的 `HitColorIndex` 与 BLE 协议中的 `typeIndex` 使用的是映射后的整数值

也就是说：

- 当控制台出现 [`[HandRaycastClass]`](Assets/Scripts/HandRaycaster.cs:334) 时，通常表示映射结果为 `0`
- 当控制台出现 [`[HandRaycastClassHit]`](Assets/Scripts/HandRaycaster.cs:334) 时，表示映射结果为非 `0` 的有效种类
- VFX 中的 `HitColorIndex` 应按映射后的种类索引配置，而不是直接按 VisionOS 原始枚举值配置

### 3. 触觉参数生成

[`BLESendJointV`](Assets/Scripts/BLESendJointV.cs) 每帧更新 10 路手指状态：

- 右手 5 指 + 左手 5 指。
- 读取每根手指的速度大小。
- 可选使用 [`OneDollarFilter`](Assets/Scripts/OneDollarFilter.cs) 做平滑。
- 如果该手指没有命中物体，或速度低于阈值，则音量置 0。
- 否则把速度映射到 `10~40`，对应 `1.0x~4.0x`。

### 4. BLE 数据发送

[`BLESendJointV`](Assets/Scripts/BLESendJointV.cs) 会周期性发送固定格式的 32 字节数据：

- `data[0] = 0xFE` 起始标记
- 中间 10 组通道数据，每组 3 字节
- `data[31] = 0xFF` 结束标记

每个通道的 3 字节含义：

1. 文件索引 / 材质索引
2. 音量
3. 速度

当前实现中，文件索引默认固定为 `5`，说明“按材质切换不同触觉素材”的逻辑还没有完全接回发送链路，现阶段重点是**每指独立音量 + 速度控制**。

---

## BLE 通信细节

[`BLESendJointV`](Assets/Scripts/BLESendJointV.cs) 与 [`TEST/BLETEST`](Assets/Scripts/TEST/BLETEST.cs) 中都使用了同一套 Nordic UART 风格 UUID：

- Service UUID: `6E400001-B5A3-F393-E0A9-E50E24DCCA9E`
- RX Characteristic: `6E400002-B5A3-F393-E0A9-E50E24DCCA9E`
- TX Characteristic: `6E400003-B5A3-F393-E0A9-E50E24DCCA9E`

默认目标设备名来自 [`BLESendJointV`](Assets/Scripts/BLESendJointV.cs)：

- `ESP32-BLE`
- `FeltSight BLE`

功能特性包括：

- 自动扫描
- 自动连接
- 连接状态 UI 更新
- 连续发送失败检测
- 断线自动重连
- 手动触发重连

---

## 关键数据设计

### 十指通道映射

[`BLESendJointV`](Assets/Scripts/BLESendJointV.cs) 中的 10 路通道约定为：

- 0~4：右手拇指到小指
- 5~9：左手拇指到小指

这与 [`HandRaycaster`](Assets/Scripts/HandRaycaster.cs) 的命中查询逻辑保持一致。

### 十指通道协议定义

为了便于 Unity 端与固件端协同开发，当前 BLE 数据包协议定义如下。

#### 数据包总长度

- 固定 `32` 字节
- 起始字节：`0xFE`
- 结束字节：`0xFF`

对应实现见 [`GenerateDataPacket()`](Assets/Scripts/BLESendJointV.cs:710)。

#### 数据包结构

| 字节范围             | 含义                               |
| -------------------- | ---------------------------------- |
| `data[0]`            | 包头，固定为 `0xFE`                |
| `data[1] ~ data[30]` | 10 个手指通道数据，每个通道 3 字节 |
| `data[31]`           | 包尾，固定为 `0xFF`                |

#### 单通道结构

每个手指通道占 3 字节，格式如下：

| 偏移         | 字段        | 说明                                     |
| ------------ | ----------- | ---------------------------------------- |
| `offset + 0` | `typeIndex` | 触觉种类索引 / 文件索引                  |
| `offset + 1` | `volume`    | 音量，范围 `0~100`                       |
| `offset + 2` | `speed`     | 速度字节，范围 `10~40`，对应 `1.0x~4.0x` |

其中：

- `offset = 1 + channel * 3`
- `channel` 范围为 `0~9`

#### 十指通道编号

| 通道号 | 手    | 手指   |
| ------ | ----- | ------ |
| `0`    | Right | Thumb  |
| `1`    | Right | Index  |
| `2`    | Right | Middle |
| `3`    | Right | Ring   |
| `4`    | Right | Little |
| `5`    | Left  | Thumb  |
| `6`    | Left  | Index  |
| `7`    | Left  | Middle |
| `8`    | Left  | Ring   |
| `9`    | Left  | Little |

#### `typeIndex` 当前语义

当前 Unity 端约定：[`HandRaycaster`](Assets/Scripts/HandRaycaster.cs) 会先识别 VisionOS 的原始 mesh classification，再映射为项目内部触觉种类索引；[`BLESendJointV`](Assets/Scripts/BLESendJointV.cs) 在发送时会基于该结果再做一次 BLE 协议侧修正。

##### 项目内部映射（`HandRaycaster` / VFX / UI）

| VisionOS 分类 | 内部 `typeIndex` / `HitColorIndex` |
| ------------- | ---------------------------------- |
| `Seat`        | `3`                                |
| `Table`       | `4`                                |
| `Floor`       | `5`                                |
| `Wall`        | `5`                                |
| `Plant`       | `10`                               |
| `TV`          | `11`                               |
| 其他所有分类  | `0`                                |

##### BLE 实际发送映射（`BLESendJointV`）

[`BLESendJointV`](Assets/Scripts/BLESendJointV.cs) 在 [`GetFingerTypeIndex()`](Assets/Scripts/BLESendJointV.cs:1128) 中读取 [`HandRaycaster.TryGetFingerHitClassification()`](Assets/Scripts/HandRaycaster.cs:475) 的结果，并通过 [`RemapBleTypeIndex()`](Assets/Scripts/BLESendJointV.cs:1141) 做发送侧修正。

最终 BLE 包里发送的 `typeIndex` 为：

| VisionOS 分类 | BLE 发送 `typeIndex` |
| ------------- | -------------------- |
| `Seat`        | `3`                  |
| `Table`       | `4`                  |
| `Floor`       | `5`                  |
| `Wall`        | `6`                  |
| `Plant`       | `10`                 |
| `TV`          | `11`                 |
| 其他所有分类  | `0`                  |

说明：

- 这里的“第几种”就是 `typeIndex` 的整数值本身
- `0` 表示 Unknown / 未定义种类
- VFX / UI 使用的是内部映射值
- BLE 发送使用的是发送侧修正后的值
- 固件端应按 BLE 实际发送的 `typeIndex` 选择对应触觉素材

#### `volume` 当前语义

- 范围 `0~100`
- 当手指未命中物体，或速度低于阈值时，音量会被置为 `0`
- 否则使用当前配置的正常音量值

#### `speed` 当前语义

- 范围 `10~40`
- 对应播放倍率 `1.0x~4.0x`
- 由手指速度大小线性映射得到

#### 协议示例

假设：

- 右手食指（通道 `1`）命中 `Wall`
- 当前音量为 `75`
- 当前速度字节为 `24`

则该通道 3 字节为：

- `typeIndex = 5`
- `volume = 75`
- `speed = 24`

即：

- `data[4] = 5`
- `data[5] = 75`
- `data[6] = 24`

如果左手中指（通道 `7`）未命中有效分类，则：

- `typeIndex = 0`
- `volume` 可能为 `0`
- `speed` 仍为当前速度映射值或默认值

#### 固件端建议

固件端解析时建议：

- 先校验 [`data[0]`](Assets/Scripts/BLESendJointV.cs:711) 是否为 `0xFE`
- 再校验 [`data[31]`](Assets/Scripts/BLESendJointV.cs:734) 是否为 `0xFF`
- 按 `channel = 0~9` 逐通道读取 3 字节
- 使用 `typeIndex` 选择素材，使用 `volume` 控制强度，使用 `speed` 控制播放倍率

### 速度到播放倍率映射

在 [`BLESendJointV`](Assets/Scripts/BLESendJointV.cs) 中：

- 速度范围默认 `0.0 ~ 0.3 m/s`
- 映射到字节 `10 ~ 40`
- 对应播放倍率 `1.0x ~ 4.0x`

### 静音条件

单根手指会被静音的条件：

- 没有命中任何物体
- 或速度低于 `m_VolumeThreshold`

这意味着当前触觉反馈不是“只要手在动就震”，而是“**碰到东西并且有足够运动速度时才触发**”。

---

## 依赖与平台说明

从脚本可见，项目依赖主要包括：

- Unity XR Hands
- visionOS / Vision Pro 相关扩展
- AR Foundation / XR Mesh 分类能力
- `UnityCoreBluetooth` 插件
- TextMeshPro

其中：

- [`HandVisualizer`](Assets/Scripts/HandVisualizer.cs) 受编译宏控制，只在满足 XR Hands 与 visionOS / Editor 条件时编译。
- [`HandRaycaster`](Assets/Scripts/HandRaycaster.cs) 中 mesh classification 的读取在 `UNITY_VISIONOS` 下才会真正生效。
- [`SuperAdmin`](Assets/Scripts/SuperAdmin.cs) 会在运行时区分 Editor 与 VisionOS。

### Xcode / Info.plist 蓝牙权限注意事项

在 visionOS / iOS 真机上，如果应用可以正常运行，但蓝牙逻辑不执行、不报错、甚至几乎没有相关 log，需要优先检查 Xcode 工程的 `Info` / `Info.plist` 是否补充了蓝牙隐私描述。

必须添加两个以 `Privacy` 开头的蓝牙 Description：

- `Privacy - Bluetooth Always Usage Description`
- `Privacy - Bluetooth Peripheral Usage Description`

建议说明文案示例：

- `This app uses Bluetooth to connect to external haptic devices.`
- `This app uses Bluetooth to communicate with external haptic peripherals.`

如果缺少这两项，常见现象是：

- 应用本身可以启动
- BLE 扫描 / 连接逻辑没有真正执行
- 控制台没有明显报错
- 看起来像蓝牙代码没有进入，或者进入后没有任何反馈

因此，这两个 `Privacy` 蓝牙描述项应视为 Xcode 导出工程中的 BLE 必检配置。

### Xcode 构建报错：`CoreBluetooth.framework did not contain an Info.plist`

如果在 Xcode 编译 visionOS 工程时出现类似下面的错误：

```text
Framework .../MeshClassification.app/Frameworks/CoreBluetooth.framework did not contain an Info.plist
```

通常不是因为 [`mcUnityCoreBluetooth.bundle`](Assets/Plugins/UnityCoreBluetooth/Plugins/macOS/mcUnityCoreBluetooth.bundle) 没有被排除，而是因为 `CoreBluetooth.framework` 被错误地作为 **Embedded Framework** 复制进了应用包。

对于 visionOS，`CoreBluetooth.framework` 属于系统框架，应当 **Link**，但不应 **Embed**。

#### 手动修复步骤（Xcode）

1. 打开 Unity 导出的 Xcode 工程。
2. 选中主 Target。
3. 进入 `General` 标签页。
4. 找到 `Frameworks, Libraries, and Embedded Content`。
5. 找到 `CoreBluetooth.framework`。
6. 将其 `Embed` 方式改为 `Do Not Embed`。

#### 说明

- [`Assets/Plugins/UnityCoreBluetooth/Plugins/macOS/mcUnityCoreBluetooth.bundle`](Assets/Plugins/UnityCoreBluetooth/Plugins/macOS/mcUnityCoreBluetooth.bundle) 对 visionOS 保持排除是正确的。
- 真正需要避免的是把系统框架 `CoreBluetooth.framework` 打进 `.app/Frameworks/` 目录。
- 如果项目里有自定义 Xcode 后处理脚本，例如 [`VisionOSBuildPostProcessor`](Assets/Editor/VisionOSBuildPostProcessor.cs)，需要确保其中对 `CoreBluetooth.framework` 的处理是 **Link Only / Do Not Embed**。

---

## 脚本之间的依赖关系

可以把当前脚本关系概括为：

```text
XRHandSubsystem
   ↓
MyHand
   ├─ 提供关节位置 / 速度 / Palm / 距离
   ↓
HandRaycaster
   ├─ 判断十指是否命中空间网格
   ├─ 读取 mesh classification
   ↓
BLESendJointV
   ├─ 结合“是否命中 + 速度大小”生成十路触觉参数
   └─ 通过 BLE 发送到 ESP32 / 触觉设备

SuperAdmin
   ├─ 控制 UI
   ├─ 控制功能开关
   └─ 显示每个手指的命中信息
```

---

## 运行与调试建议

### Unity 侧

- 使用支持 visionOS / XR Hands 的 Unity 版本打开项目。
- 检查场景中 [`MyHand`](Assets/Scripts/MyHand.cs)、[`HandRaycaster`](Assets/Scripts/HandRaycaster.cs)、[`BLESendJointV`](Assets/Scripts/BLESendJointV.cs)、[`SuperAdmin`](Assets/Scripts/SuperAdmin.cs) 的引用是否完整。
- 如果只想先验证手部与 UI，可先关闭 [`SuperAdmin`](Assets/Scripts/SuperAdmin.cs) 中的 BLE 开关。
- 如果只想验证 BLE，可直接使用 [`TEST/BLETEST`](Assets/Scripts/TEST/BLETEST.cs)。

### 设备侧

- 确保外设广播名与 [`BLESendJointV`](Assets/Scripts/BLESendJointV.cs) 中配置一致。
- 确保服务 UUID / 特征 UUID 与 Unity 端一致。
- 如果频繁断连，优先检查供电、广播稳定性和写入特征权限。

### 参考项目与来源

本项目中的 AR Mesh 分类接入思路，参考了 Unity 官方 AR Foundation 示例仓库 [`Unity-Technologies/arfoundation-samples`](https://github.com/Unity-Technologies/arfoundation-samples)。

同时也参考了 Unity 官方文档 [`AR Foundation 6.4`](https://docs.unity3d.com/Packages/com.unity.xr.arfoundation@6.4/manual/index.html) 与 [`Apple ARKit XR Plug-in 6.4`](https://docs.unity3d.com/Packages/com.unity.xr.arkit@6.4/manual/index.html)。

参考说明：

- [`Unity-Technologies/arfoundation-samples`](https://github.com/Unity-Technologies/arfoundation-samples) 是 Unity 官方提供的 AR Foundation 示例项目
- 该仓库包含可直接运行和修改的 AR Foundation 示例场景与代码
- 本项目中 visionOS mesh classification 的接入思路，尤其参考了其中的 meshing / classification 示例结构
- [`AR Foundation 6.4`](https://docs.unity3d.com/Packages/com.unity.xr.arfoundation@6.4/manual/index.html) 文档用于理解 AR Foundation 的整体架构、subsystems / managers / extensions 关系，以及 meshing、mesh classification、raycasts、anchors、XR Simulation 等通用能力
- 该文档也明确说明：AR Foundation 本身只提供跨平台接口，要在目标平台真正工作，还必须安装对应 provider plug-in，例如 iOS 的 [`Apple ARKit XR Plug-in 6.4`](https://docs.unity3d.com/Packages/com.unity.xr.arkit@6.4/manual/index.html) 与 visionOS provider
- [`Apple ARKit XR Plug-in 6.4`](https://docs.unity3d.com/Packages/com.unity.xr.arkit@6.4/manual/index.html) 文档可用于理解 AR Foundation 在 Apple 平台上的 meshing、raycasts、anchors、occlusion 等能力边界与工程配置方式
- 该文档也说明了 ARKit XR Plug-in 主要通过 AR Foundation 暴露能力，本身通常不直接提供额外公共脚本接口
- [`XRMeshSubsystem`](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/XR.XRMeshSubsystem.html) Scripting API 文档可用于理解底层动态 mesh 子系统的职责，例如 `TryGetMeshInfos`、`GenerateMeshAsync`、`SetBoundingVolume` 等基础能力
- [`AR Foundation Mesh classification`](https://docs.unity3d.com/Packages/com.unity.xr.arfoundation@6.4/manual/features/meshing/classification.html) 文档说明了 AR Foundation 6.4+ 中 mesh classification 的启用方式，包括在 [`ARMeshManager`](Assets/VFX/VFXMan.cs:18) Inspector 中启用 Classification，或通过脚本启用 `submeshClassificationEnabled`
- [`AR Foundation Meshing sample scenes`](https://docs.unity3d.com/Packages/com.unity.xr.arfoundation@6.4/manual/samples/features/meshing.html#classification) 文档说明了官方 `Classification Meshes` 示例场景的用途：将不同 mesh classification 分裂为子 mesh 并用不同颜色渲染
- [`Unity Discussions: MR Example Mesh Classification not from ARFoundation Subsystems?`](https://discussions.unity.com/t/mr-example-mesh-classification-not-from-arfoundation-subsystems/332759/5) 这条讨论说明了 visionOS 上需要把 AR Foundation sample 中的分类脚本适配到 [`ARMeshClassification`](Library/PackageCache/com.unity.xr.visionos@c88aa5b2830f/Runtime/ARMeshClassification.cs:9)，并注意 visionOS 的分类集合与 ARKit 不同，且可能出现“mesh 暂时没有 classification 数据”的情况
- [`visionOS ARMeshClassification`](https://docs.unity3d.com/Packages/com.unity.xr.visionos@0.1/api/UnityEngine.XR.VisionOS.ARMeshClassification.html?ampDeviceId=2f412fcc-cd60-4ed0-93a3-7c411c6d1a1a&ampSessionId=1775591360726&ampTimestamp=1775677853090) 文档可用于核对 visionOS 插件提供的完整分类枚举集合
- [`Unity Discussions: Using ARKit meshing with AR Foundation 4.0`](https://discussions.unity.com/t/using-arkit-meshing-with-ar-foundation-4-0/789330) 这条官方讨论对 [`ARMeshManager`](Assets/VFX/VFXMan.cs:18) 的 mesh prefab、[`MeshCollider`](Assets/Scripts/HandRaycaster.cs:491)、法线、并发队列、LiDAR meshing 行为等工程细节有很好的说明
- [`VisionOSMeshSubsystemExtensions`](https://docs.unity3d.com/Packages/com.unity.xr.visionos@2.0/api/UnityEngine.XR.VisionOS.VisionOSMeshSubsystemExtensions.html) 文档明确了 visionOS 对 [`XRMeshSubsystem`](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/XR.XRMeshSubsystem.html) 的扩展入口，包括 `SetClassificationEnabled`、`GetClassificationEnabled`、`GetFaceClassifications`
- 这些官方资料与社区讨论的版本跨度较大，实际接入时仍需结合当前项目所使用的 VisionOS / AR Foundation / Unity 包版本进行适配
