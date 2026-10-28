# 里程碑与交接

状态：完成 / 部分完成 / 待执行；“实现”与“硬件验证”分开记录。更新日期 2026-10-28。用户明确最终为 Windows PC + Meta Link，取消 Android 原生门槛。当前集成证据以 [m3-integration](m3-integration.md) 为准，下一阶段见 [PCVR/LAN验收](pcvr-lan-acceptance.md)；下面的 2026-10-14 段落保留为历史，不代表当前必做任务。

| 任务 | 负责人 | 状态 | 完成条件 | 依赖 |
| --- | --- | --- | --- | --- |
| M0 工程检查、仓库布局 | A | 完成 | 精确 Editor、模板资产、Git 初态核对；仓库根与工程根对齐 | 用户授权仓库移动 |
| M0 模块目录与 Shared v1 | A | 完成 | 目录/meta、8 字段及接口、无 XR/NGO 耦合 | 现有 Unity 工程 |
| M0 协作文档与交接 | A | 完成 | AGENTS、README、architecture、contract、setup、tasks、testing | Shared v1 |
| M0 XR 官方包安装 | A | 完成 | 四包由 UPM 解析，manifest 和 lock 记录真实版本 | 6000.3.15f1 官方包元数据 |
| M1 头/手腕源与本地显示 | A | PCVR 基础验收完成 | MainScene、官方 Sample；Unity 编译及隔离 PlayMode 7/7；Quest 2 Link 骨骼/腕标记、独立丢失恢复和重入通过 | XR 包；Meta Link |
| M1 OpenXR 平台配置、官方手骨架与追踪 | A | PCVR 基础通过；扩展生命周期待验 | Standalone OpenXR、单 Rig 官方 HandVisualizer；真实系统暂停、Link 中断重连与重新居中另验 | Windows、Meta Link 头显 |
| M2 MockPoseSource 与非 VR LAN | B | 桌面loopback与诊断扩展通过 | RTT/频率/权威变色、双向Mock、人数限制、离开重入和晚加入颜色有本轮实测；真实双PC LAN另验 | Shared v1；NGO/Transport 已锁定 |
| M3 XR 与网络集成 | A+B | 单端 PCVR 集成验收通过；双头显待验 | MainScene真实源/出生点映射、远端视觉、无Mock；EditMode7/7、PlayMode14/14；Host接收到Quest 2 / Link真实头/手；面板/手动变色最终确认正常 | M1 + M2 |
| M4a 两 PC LAN 基础 | A+B | 跨PC基础及Host生命周期通过；故障/长时结果见扩展项 | 同包Mock同步、满房、退出重入、Host异常停止与重启后手动连接通过；Client超时清理约30.6秒。Private防火墙规则不匹配已定位，授权临时放行后通过，现已移除 | 两台 PC；本轮零头显 |
| M4b 双 PC + Meta Link | A+B | 待头显 | 每 PC 一个头显，真实头/双腕、独立失效恢复、事件同步、长时间稳定性；RTT 与显示延迟分别记录 | M4a、两套 Meta Link |
| M4a 扩展：故障/长时 | A+B | 功能验收收尾；跨PC30分钟故障长测及物理断网/重连通过；Client CSV趋势待核对 | 独立UDP代理有实际丢弃计数；Client正常完成1830秒、连接1829.6秒、32237快照；超时/恢复各1、无姿态/视觉违例；Host资源窗口稳定。最终PlayMode16/16；报告冲突保护实际进程验证通过 | 两PC；补测Client/Host超时隐藏1770/1765次全通过，重连44.84秒；临时规则已清理；见lan-resilience |
| 头部 Avatar 与英文会话 UI | A+B 集成 | 实现与无硬件回归完成，真实 Link 待验 | 球体/双眼头部、相机排除本机头部、远端显示；Host/Join/Disconnect/Show Status、共享 IP/Port、人数与状态；EditMode7/7、PlayMode18/18 | 用户新增需求；见head-avatar-session-ui |
| 桌面大厅、头向移动与IP自动发现 | A+B 集成 | 已实现；大厅与网格下移动用户确认正常；新增IP待用户实测 | 鼠标/数字输入Host/Join后手动启动XR；左摇杆头向平移；本机IPv4填入/可手改 | 见desktop-lobby-locomotion |
| M5 手指骨骼协议 | A+B | 独立关节契约/骨架/同步及自动验证完成；实机由用户自行执行 | Shared v1.2、会话4；双手26关节、独立有效性、压缩/插值/超时；实际双头显与长测待验 | 见desktop-lobby-locomotion |

## M4a 阶段收尾（2026-10-22历史状态）

LAN 功能阶段已收尾，实现经 PR #9 合入 main（083fbfd）。当前零头显，M4b 保留待设备，不提前实施 M5。Client 完整 CSV 未回收，资源趋势单独保留待办；不补记无泄漏结论，不影响已通过功能结果。交付包、实际验证、测试清理及恢复顺序见 [M4a 收尾](m4a-closeout.md)。

## B 初始交接（历史，已实现）
使用 `Assets/Scripts/Shared/AvatarPoseFrame.cs` / `IAvatarPoseSource.cs` 与 [interface-contract](interface-contract.md)。创建 `QuestLAN.Simulation.MockPoseSource`（B 自选命名空间）实现接口，NGO serializer 放在 Networking；自己的 asmdef 引用 QuestLAN.Shared。测试目录中的 TestPoseSource 只是 PlayMode fixture，不是 B 的 runtime mock。

B 负责 NetworkManager、Host/Client IPv4 直连、NetworkObject 生命周期/Ownership、发送节奏与去重、接收缓存/插值、断线/超时失效、RTT 与同步变色测试物体。未来远端缓存也可以实现 IAvatarPoseSource，直接接 AvatarPoseView，设置 Show Head=true。连接重建清空 sequence 状态；全部 false 帧也是有效消息。

不要更改 Shared 字段或 root 相对坐标，不把 XR 类型或 NGO serializer 加进 Shared；不要修改 MainScene、Visual Prefab、manifest 或 A 的 ProjectSettings。依赖安装先与 A 协调。A 的本地 source 不应读取 NGO Ownership，由集成层绑定本机拥有的 Avatar。

## 2026-10-14 下一步与完成记录（历史）
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


## 2026-10-28 追加：IP发现与详细双手骨架

用户已确认Quest 2 + Link下桌面鼠标/数字输入、连接后进入VR及网格下头向摇杆移动正常。当前基于8cac40f的功能分支继续实现IP自动填入与独立关节同步，非main合并结论。

Shared v1.2 / 会话协议4已为A/B集成更新：双方必须同版；实时26关节、压缩独立通道、丢失隐藏/恢复、接收到达时钟插值已实现。EditMode10/10、PlayMode24/24、同机双进程关节同步/手指变化通过，详见desktop-lobby-locomotion.md。下一步为Quest 2持握与空手状态确认；再补两PC/两套Link真实关节、长时资源与网络劣化验收。无有效关节时绝不以固定手模型冒充实时手指。此次通过文档通知B，不发送外部消息。
