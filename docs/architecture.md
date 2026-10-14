# 模块与控制权

```
本机 Quest -> Local XR Rig / XRHandsPoseSource -> Shared AvatarPoseFrame
                                               | -> AvatarPoseView（本地，隐藏头）
                                               | -> B: Networking -> LAN
远端 LAN -> B: 缓存/插值/超时 -> IAvatarPoseSource -> AvatarPoseView（远端）
B: MockPoseSource -----------> 同一接口（无 Quest 网络验证）
```

Shared 无具体 XR/网络依赖。LocalXR 读取 HMD 与左右手腕；只发布采集结果，绝不调用 NGO。Avatar 显示层依赖接口，任意源都可替换。Networking 负责身份、Ownership、玩家生命周期、序列化、发送节奏、接收缓存/插值和失联处理。Diagnostics/Network 负责连接、RTT、频率；Simulation 由 B 生成模拟帧。

每台设备只有一个真正获取硬件数据的 Local XR Rig。Network Avatar 有独立身份与 AvatarRoot，不包含硬件输入系统。只有本机有权更新本机 tracking；接收方的远端 Avatar 只从网络快照更新。

AvatarRoot 定义共享虚拟世界中的玩家出生点与朝向，保持单位缩放。头和双手都发送 root 相对位姿，不把两台 Quest 各自原始 tracking 空间当成同一个世界。XR Origin 的 Camera Floor Offset Object 将手腕 tracking Pose 映射到世界，再转换为 root 相对 Pose。详见 [契约](interface-contract.md)。v1 不实现物理空间共定位。

MainScene 最终由 A 建立；当前保留原 SampleScene。B 独立提供网络 Prefab，不修改 A 场景。M1 用官方 XR Hands HandVisualizer 确认骨骼，然后验证我们的手腕接口；Visualizer 的骨骼展示不等于已实现手指网络协议。XRI 与未来 Meta 抓取职责需后续协调，避免两套硬件 Rig 或手驱动同时运行。

未来线香花火、IMU、心率、振动作为独立模块消费已建立的边界；此次不提前设计传感器协议。未来 HandJointFrame 独立于头/手腕基础同步。
