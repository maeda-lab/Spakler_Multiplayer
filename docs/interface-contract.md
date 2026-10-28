# Avatar Pose / Hand Joints 契约 v1.2

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
未来独立 HandJointFrame：稳定的共享关节索引映射表（不能把 XRHandJointID 类型泄漏到 Shared）、左右手身份、每关节 validity、相对手腕/父骨骼坐标约定、独立更新频率、量化压缩与协议版本。v1.2 按下述独立契约实现，不扩充 AvatarPoseFrame。

字段改名/类型/坐标/有效性语义变化均为破坏性升级；双方先确认，更新此文档版本、迁移方案与 tasks，再通知 B。新增传输数据由 Networking 适配，不随意污染 Shared。v1.0：首次提供头与左右手腕 8 字段及采集接口。


## v1.1（2026-10-28）：控制器近似腕与连续移动，A/B 迁移通知

字段、类型、root-relative 坐标、单调采集时间戳和失效/去重语义保持 v1.0。产品现在支持本地 XR Hands 优先、真实控制器握持姿态回退；回退需对应侧 Controller 设备与位置/旋转追踪有效。默认在 grip 局部 Z=-0.04m 处定义近似腕锚点，可配置偏移。这表示该近似手腕 Pose 有效，不保证解剖学腕关节测量，也不代表任一手指关节有效。旧源默认不启用控制器回退；MainScene 显式启用。

连续移动平移本机 Rig，固定出生 AvatarRoot 不变；同一头/腕快照包含相对出生点的移动距离，不改变 root 语义，不额外同步 root。Networking 校验最大距离从5m调整为有限50m，产品Rig移动限制为出生点20m半径。网络包版本/字段仍为1，会话协议由2升为3以拒绝旧校验规则；Host/Client均需更新到同一版本。

给 A/B 的实施要求：消费者继续按左右独立 tracked 隐藏和恢复；不得把控制器回退腕有效性外推为手指有效性。M5仍应使用独立 HandJointFrame 与关节 validity。此次通知随工程文档交接，不自行发送外部消息。


## v1.2（2026-10-28）：独立双手关节，A/B 集成交接

用户本轮明确提前启用详细手指阶段。新增 Shared `HandJointFrame` / `IHandJointSource`，原 AvatarPoseFrame 接口保持不变。双方必须更新到会话协议 **4**（拒绝旧版玩家 Prefab/NetworkBehaviour 组合）；关节包自身版本为1。此次变更通过本文件与任务文档通知 A/B，不发送外部消息。

- Sequence/LocalCaptureTimeSeconds：与源本次头/腕帧同一采集序号/本机单调时间，网络使用独立 transport sequence。
- LeftWrist/RightWrist：相对于单位缩放 AvatarRoot；LeftJoints/RightJoints：每手26个 Pose，均相对该手腕（非相对父关节）。世界重建为 Root * Wrist * Joint。
- 每侧 uint ValidMask 低26位独立有效；0=该手无有效关节。任一关节有效必须同时有有效 Wrist（bit1）。只读取有效位对应的数组元素，失效位不延续旧显示。true 可包含双手 mask=0 的显式失效帧；false 的 out 不得消费。
- 固定顺序：0 Palm，1 Wrist；2–5 Thumb 的 Metacarpal/Proximal/Distal/Tip；6–10 Index、11–15 Middle、16–20 Ring、21–25 Little 各为 Metacarpal/Proximal/Intermediate/Distal/Tip。XR adapter 显式映射，Shared 无 XR/NGO 类型。
- 数组为只读借用，最新帧至少保持到下一次源采集；需要缓存必须 `Copy()` 深复制。主线程采集/消费。接收缓存拥有独立数组，插值结果数组下次采样可复用。
- XR Hands Dynamic 回调采集，LateUpdate 发布。控制器握持回退只适用于旧手腕接口，绝不生成手指有效位。停机、暂停、无当帧更新、失追踪均失效。
- 20Hz 独立 owner-only unreliable RPC，服务端验证后中继；源不更新超过0.2s发失效，接收超过0.5s失效。到达时钟插值0.1s，最新无效位立即隐藏，新恢复关节直接使用恢复数据；不跨设备相减采集时间。
- 根相对手腕最大50m；手腕相对关节最大1m。关节位置用有符号int16毫米（每轴最大误差0.5mm）、四元数四个int16归一化分量，收到后旋转插值。只序列化有效关节，完整双手payload为805字节（不含NGO/传输头）。版本、mask、尺寸、有限值和旋转模长必须校验。

可选 XR_META_simultaneous_hands_and_controllers 扩展每次会话显式 resume；扩展可用/启用与每帧关节有效性分别记录。Quest 2 + Link 的真实可用性以运行时返回为准，不将 controller pose 或固定五指模型冒充实时关节。
