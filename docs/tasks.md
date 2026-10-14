# 里程碑与交接

状态：完成 / 部分完成 / 待执行；“实现”与“硬件验证”分开记录。日期 2026-10-14。

| 任务 | 负责人 | 状态 | 完成条件 | 依赖 |
| --- | --- | --- | --- | --- |
| M0 工程检查、仓库布局 | A | 完成 | 精确 Editor、模板资产、Git 初态核对；仓库根与工程根对齐 | 用户授权仓库移动 |
| M0 模块目录与 Shared v1 | A | 完成 | 目录/meta、8 字段及接口、无 XR/NGO 耦合 | 现有 Unity 工程 |
| M0 协作文档与交接 | A | 完成 | AGENTS、README、architecture、contract、setup、tasks、testing | Shared v1 |
| M0 XR 官方包安装 | A | 完成 | 四包由 UPM 解析，manifest 和 lock 记录真实版本 | 6000.3.15f1 官方包元数据 |
| M1 头/手腕源与本地显示 | A | 部分完成 | 最小代码、MainScene、官方 Sample 已生成；Unity 编译及 PlayMode 3/3 通过，仍待设备上有效性和重入验证 | XR 包；后续 OpenXR 配置 |
| M1 OpenXR 平台配置、官方手骨架与追踪 | A | 待执行 | 双平台 validation、官方 Visualizer 骨骼、Quest 左右手独立丢失恢复测试 | Editor 人工设置、Quest 3 |
| M2 MockPoseSource 与非 VR LAN | B | 待执行，可立即开始 | 不依赖硬件实现 IAvatarPoseSource；两进程直连、Ownership、缓存/插值、连接/RTT、同步变色、离线隐藏 | Shared v1；协调 NGO/Transport 依赖 |
| M3 XR 与网络集成 | A+B | 待执行 | A 加入 B 网络 Prefab 到 MainScene，真实源替换 mock 无需改传输层 | M1 + M2 |
| M4 Quest 双机 LAN 测试 | A+B | 待执行 | 同 LAN 两头显加入/退出/断线、头/双手状态、RTT 和事件同步通过 | M3、两台 Quest 3 |
| M5 手指骨骼协议 | A+B | 待执行 | 独立 HandJointFrame、关节索引/validity/局部坐标/频率/压缩协商并实测 | M4 稳定 |

## B 可独立开始
使用 `Assets/Scripts/Shared/AvatarPoseFrame.cs` / `IAvatarPoseSource.cs` 与 [interface-contract](interface-contract.md)。创建 `QuestLAN.Simulation.MockPoseSource`（B 自选命名空间）实现接口，NGO serializer 放在 Networking；自己的 asmdef 引用 QuestLAN.Shared。测试目录中的 TestPoseSource 只是 PlayMode fixture，不是 B 的 runtime mock。

B 负责 NetworkManager、Host/Client IPv4 直连、NetworkObject 生命周期/Ownership、发送节奏与去重、接收缓存/插值、断线/超时失效、RTT 与同步变色测试物体。未来远端缓存也可以实现 IAvatarPoseSource，直接接 AvatarPoseView，设置 Show Head=true。连接重建清空 sequence 状态；全部 false 帧也是有效消息。

不要更改 Shared 字段或 root 相对坐标，不把 XR 类型或 NGO serializer 加进 Shared；不要修改 MainScene、Visual Prefab、manifest 或 A 的 ProjectSettings。依赖安装先与 A 协调。A 的本地 source 不应读取 NGO Ownership，由集成层绑定本机拥有的 Avatar。

## A 下一步
完成双平台 OpenXR 设置与官方 HandVisualizer 设备测试，再把骨骼可视化接入同一 Rig。明确验证 Camera Floor Offset 与 AvatarRoot 映射，记录头/左右手丢失与恢复、暂停/重入、Device/Floor 模式；不以无设备的测试声称 M1 完成。暂不接 Meta 抓取或网络。
