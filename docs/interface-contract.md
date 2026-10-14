# Avatar Pose 契约 v1.0

2026-10-14 首次建立。命名空间 `QuestLAN.Shared`，程序集 `QuestLAN.Shared`，位于 `Assets/Scripts/Shared`。只引用 UnityEngine；无 XR Hands、NGO 或序列化传输依赖。字段为 public struct 字段，可按值复制；网络序列化由 B 在 Networking 实现，不假设结构的内存布局或 padding。

| AvatarPoseFrame 字段 | 类型 | 语义 |
| --- | --- | --- |
| Sequence | uint | 每次采集递增，允许 unchecked 回绕；不是每次接口读数递增 |
| LocalCaptureTimeSeconds | double | 发送端本机单调时钟秒；当前源使用 Time.realtimeSinceStartupAsDouble |
| Head | UnityEngine.Pose | 头部相对于当前玩家 AvatarRoot |
| LeftHand / RightHand | UnityEngine.Pose | 左/右**手腕**相对于同一 AvatarRoot；不是 rootPose 或全部关节 |
| HeadTracked / LeftHandTracked / RightHandTracked | bool | 独立位置与旋转有效性；false 时对应 Pose 不保证有效 |

`bool IAvatarPoseSource.TryGetLatestPose(out AvatarPoseFrame frame)`：true 代表已有本次源生命周期内采集帧或显式失效帧，允许所有 Tracked=false；false 代表尚未采集或必需映射引用无效。false 的 out 值不得消费。重复调用可以返回同一 Sequence；消费者自行去重。源失去硬件仍可发布全部 false 的新帧，不能继续返回全 true 的陈旧帧。停用/暂停时立即发布失效帧（此时仍返回 true）；重新启用清空可读帧，恢复后必须重新采集。

## 坐标与映射
Unity 米制、Unity Quaternion、左右手身份来自 provider。每个玩家拥有由共享世界出生点决定的 AvatarRoot；根节点及祖先单位缩放，尽量无缩放父节点。每设备只有一个 Local XR Rig；AvatarRoot 是可视化/身份根，不是另一个硬件 Rig。

本地 `trackingOrigin` 指向 XR Origin 的 **Camera Floor Offset Object**（相机父对象），包含设备到世界的映射和 Device 模式高度偏移；不要误接到相机本身。头部取 XR Camera 世界位姿；XR Hands 手腕关节原始 Pose 由 trackingOrigin 映射到世界，再变换至 AvatarRoot。

```
worldPosition = trackingOrigin.TransformPoint(wrist.position)
worldRotation = trackingOrigin.rotation * wrist.rotation
relativePosition = avatarRoot.InverseTransformPoint(worldPosition)
relativeRotation = Quaternion.Inverse(avatarRoot.rotation) * worldRotation
```

远端重建：`worldPosition = remoteRoot.TransformPoint(framePose.position)`，`worldRotation = remoteRoot.rotation * framePose.rotation`。远端渲染对象可为 root 下直接子节点，也可经世界位姿设置；不能误将相对 Pose 直接应用到任意中间父节点。推荐静态出生点，Rig 与 AvatarRoot 对齐；双方独立房间不会自动物理共定位。移动/重定位时 A 更新同一映射，B 必须协调 root 状态；不在 v1 偷加物理空间校准。

HeadTracked 要求 HMD 的 isTracked 且 trackingState 同时含 Position/Rotation；手腕要求对应 XRHand.isTracked 以及 Wrist.TryGetPose 成功。无 subsystem、未运行、暂停、失败/未更新关节都不得冒充有效。相机 Transform 的存在不能证明 HMD 已追踪。

## 顺序与时间
允许初始 Sequence 任意（当前源第一帧为 1）。可用 `unchecked((int)(candidate - previous)) > 0` 比较新旧，前提序号间隔小于 2^31；连接重建/源重建应重置接收缓存，不把新连接与旧序号比较。暂停失效帧也递增。时间戳不能跨设备直接相减计算单向延迟；RTT 由网络独立测量。不同设备和重启后的时钟无共同原点。接口仅支持 Unity 主线程采集/读取；后台传输须先复制快照并保证线程交接。

## 失追踪显示与恢复
只应用 Tracked=true 的 Pose；false 时隐藏对应对象，不维持可见的旧有效模型。重新有效后重新显示并更新。源之外的接收超时、断线、丢包、缓存插值由 B 负责；应主动隐藏/失效，不能仅依赖最后收到的 true。本地头模型默认隐藏，避免遮挡视野。

## 手指扩展与变更
未来独立 HandJointFrame：稳定的共享关节索引映射表（不能把 XRHandJointID 类型泄漏到 Shared）、左右手身份、每关节 validity、相对手腕/父骨骼坐标约定、独立更新频率、量化压缩与协议版本。当前不实现、不扩充 AvatarPoseFrame。

字段改名/类型/坐标/有效性语义变化均为破坏性升级；双方先确认，更新此文档版本、迁移方案与 tasks，再通知 B。新增传输数据由 Networking 适配，不随意污染 Shared。v1.0：首次提供头与左右手腕 8 字段及采集接口。
