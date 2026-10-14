# 里程碑与交接

状态：完成 / 部分完成 / 待执行；“实现”与“硬件验证”分开记录。日期 2026-10-14。

| 任务 | 负责人 | 状态 | 完成条件 | 依赖 |
| --- | --- | --- | --- | --- |
| M0 工程检查、仓库布局 | A | 完成 | 精确 Editor、模板资产、Git 初态核对；仓库根与工程根对齐 | 用户授权仓库移动 |
| M0 模块目录与 Shared v1 | A | 完成 | 目录/meta、8 字段及接口、无 XR/NGO 耦合 | 现有 Unity 工程 |
| M0 协作文档与交接 | A | 完成 | AGENTS、README、architecture、contract、setup、tasks、testing | Shared v1 |
| M0 XR 官方包安装 | A | 完成 | 四包由 UPM 解析，manifest 和 lock 记录真实版本 | 6000.3.15f1 官方包元数据 |
| M1 头/手腕源与本地显示 | A | 部分完成 | 最小代码、MainScene、官方 Sample；Unity 编译及隔离 PlayMode 7/7 通过；Quest 2 Link 骨骼/腕标记、独立丢失恢复和重入通过；Quest 3 原生仍待验证 | XR 包；Quest 3 |
| M1 OpenXR 平台配置、官方手骨架与追踪 | A | 部分完成 | 双平台 validation 0 errors；单 Rig 官方 HandVisualizer；Android APK 成功；Quest 2 专项验收通过；OS 实际暂停、设备模式切换和 Quest 3 原生仍待验收 | 活动平台 validation、Quest 3 |
| M2 MockPoseSource 与非 VR LAN | B | B 分支已有提交，待交接验收 | 不依赖硬件实现 IAvatarPoseSource；两进程直连、Ownership、缓存/插值、连接/RTT、同步变色、离线隐藏；不由 A 的 XR 测试推定通过 | Shared v1；协调 NGO/Transport 依赖 |
| M3 XR 与网络集成 | A+B | 待执行 | A 加入 B 网络 Prefab 到 MainScene，真实源替换 mock 无需改传输层 | M1 + M2 |
| M4 Quest 双机 LAN 测试 | A+B | 待执行 | 同 LAN 两头显加入/退出/断线、头/双手状态、RTT 和事件同步通过 | M3、两台 Quest 3 |
| M5 手指骨骼协议 | A+B | 待执行 | 独立 HandJointFrame、关节索引/validity/局部坐标/频率/压缩协商并实测 | M4 稳定 |

## B 可独立开始
使用 `Assets/Scripts/Shared/AvatarPoseFrame.cs` / `IAvatarPoseSource.cs` 与 [interface-contract](interface-contract.md)。创建 `QuestLAN.Simulation.MockPoseSource`（B 自选命名空间）实现接口，NGO serializer 放在 Networking；自己的 asmdef 引用 QuestLAN.Shared。测试目录中的 TestPoseSource 只是 PlayMode fixture，不是 B 的 runtime mock。

B 负责 NetworkManager、Host/Client IPv4 直连、NetworkObject 生命周期/Ownership、发送节奏与去重、接收缓存/插值、断线/超时失效、RTT 与同步变色测试物体。未来远端缓存也可以实现 IAvatarPoseSource，直接接 AvatarPoseView，设置 Show Head=true。连接重建清空 sequence 状态；全部 false 帧也是有效消息。

不要更改 Shared 字段或 root 相对坐标，不把 XR 类型或 NGO serializer 加进 Shared；不要修改 MainScene、Visual Prefab、manifest 或 A 的 ProjectSettings。依赖安装先与 A 协调。A 的本地 source 不应读取 NGO Ownership，由集成层绑定本机拥有的 Avatar。

## A 下一步
双平台 OpenXR 配置和同一 Rig 的官方骨骼接入已完成；下一阶段先补齐 M1 验收，同时 B 可独立启动 M2。Quest 2 / Link 的成功结果不替代 Quest 3 原生测试。

| 顺序 | 工作 | 负责人 | 完成条件与依赖 |
| --- | --- | --- | --- |
| A1 | 单手失追踪及恢复 | A | **Quest 2 / Link 已通过**：左右独立失效/恢复有分阶段 flags 证据，操作者确认骨骼/腕标记显示；Quest 3 原生需另验 |
| A2 | 暂停、重入与坐标检查 | A | **自动测试与真实重入通过**：暂停回调、subsystem 重启、五次组件启停/订阅、坐标与高度偏移数学；真实重入 30 秒左右手各有效 267 次，操作者确认正常；OS 暂停和设备模式切换仍待验 |
| A3 | Quest 3 Android 原生验收 | A | 有设备后安装已构建 APK，验证头/双手骨骼/腕标记及 A1/A2；记录设备、runtime、包版本和结果 |
| B1 | 模拟姿态源与 LAN 基础 | B | 协调 NGO/Transport 包修改，MockPoseSource 接 Shared v1；两个非 VR 进程完成 Host/Client IPv4 连接及 Ownership |
| B2 | 同步及诊断 | B | 头/双手腕同步、序号处理、缓存插值、追踪 flags、断线超时隐藏、连接状态/RTT 和变色物体通过 |
| AB1 | M3 集成 | A+B | B 提供独立网络 Prefab，A 接入 MainScene；替换模拟源为真实源，无需改变网络传输层，远端不创建 XR Rig |

A3 受 Quest 3 可用性限制，可在等待设备时推进 B1/B2。M4 双 Quest LAN 和 M5 手指网络协议依次在基础集成稳定后开展；本阶段不提前接 Meta 抓取或传感器。

本轮复现步骤及分层证据见 [xr-acceptance.md](xr-acceptance.md)。工作期间曾切至 B 的 feature/network，按用户确认回到 codex/xr-hands-m0，B 使用另一目录；本轮不合并或修改 B 分支。B 分支提交 b52d8ea 的独立验收结果由 B 交接后更新。
