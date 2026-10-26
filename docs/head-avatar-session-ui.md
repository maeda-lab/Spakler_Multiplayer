# 头部 Avatar 与英文连接面板（2026-10-26）

作用于 MainScene / Windows PC + Meta Link。实现分支 codex/head-avatar-session-ui；本轮没有头显，不补记真实 PCVR 验收结果，不改变 Shared v1 或网络包格式。

## 头部模型

- Assets/Prefabs/Visual/HeadAvatar.prefab：直径 0.18m 的蓝色球体，以及面向局部 +Z 的两颗深色小球眼睛。HeadBlue / EyesDark 材质使用 URP Unlit，Prefab 没有碰撞体。
- MainScene 的 Local AvatarRoot/Local Head Avatar 通过现有 AvatarPoseView 消费真实头部姿态，位置/旋转来自 XR 相机并按 AvatarRoot 重建；头部失追踪或源不可用时整个头部及眼睛隐藏。
- 本地模型使用 LocalAvatarHead 用户层（本工程 Unity 分配为 31），XR Camera 的 Culling Mask 排除该层，防止自己的头遮挡第一人称；Scene 视图或其他包含该层的相机可以查看模型。不要将远端模型放到此层。
- XRNetworkPlayer 引用同一 HeadAvatar Prefab；远端头和眼睛跟随收到的头部姿态，不创建硬件 Rig。本机拥有的网络视觉仍隐藏，避免与本地显示重复。
- 无头显时真实源没有有效头部样本，Play 中模型隐藏是正确行为；可在编辑状态查看 HeadAvatar Prefab，不能用无硬件的相机 Transform 存在冒充有效追踪。

## 使用连接 UI

桌面 Game 窗口左上角支持鼠标/键盘；VR 世界面板沿用头部白点凝视 1.2 秒激活，并提供数字键盘。所有产品 UI 标签为英文，使用内置 Latin 字体，不需要中文字体。

1. 在 Host PC 点击 Start Host（桌面）或 Host（VR）。Host 监听所有网卡，默认 UDP 7777。
2. 在 Client PC 输入 Host 的实际 LAN IPv4，并保持两端端口一致，点击 Join。跨 PC 不使用 127.0.0.1；既有测试脚本常用 17777，与产品默认 7777 要明确区分。
3. VR 内用 IP / Port 切换编辑字段，Clear 清空，数字/小数点及 < 键输入或退格。桌面输入与 VR 键盘共享同一目标字段。
4. Show Status 显示状态详情；面板提供当前连接状态、Host/Client/Offline 身份及 Players: N/2。VR 基础状态持续显示，详情包括 RTT 和本机颜色序号。桌面可 Hide Status 收起详情。
5. Client 点击 Disconnect 退出房间，应用保持打开以便再次 Join；Host 点击 Disconnect 关闭房间，并使 Client 退出连接。旧玩家清理后人数归零。连接中也可取消，停止完成前不能再次 Host/Join。

无效 IPv4 由会话层显示英文失败状态；无效端口在 VR 面板给出英文提示。Host 创建房间不依赖 Client 目标 IP 输入框的内容。诊断和 GUI 共用已有 LanSessionController 会话生命周期。

## 验证与限制

- 固定 Unity 6000.3.15f1 实际编译；EditMode 7/7、完整 PlayMode 18/18。初次新增测试在面板 Start 执行前读取 Canvas 失败，已修正测试等待时序并避免重复加载 MainScene，最终完整回归通过。
- PlayMode 验证头部位置/旋转及眼睛朝向、失追踪隐藏与恢复、英文标签、Join 无效输入、取消等待连接、Host 断开/重启、人数清零、真实凝视 Color 路径；Editor 验证模型引用、眼睛、无碰撞体及本机相机排除层。
- 三种 Windows 构建（产品/MainScene 无头版/Sandbox）均 Succeeded/errors=0，最终日志 Logs/HeadUI-WindowsBuild-Final.log。新增 opt-in `--m3-ui 1` 的进程探针，实际调用 Host/Join/Disconnect 按钮：Client 先离房及 Host 先关闭两种场景均通过，两端显示过 Players: 2/2，最终人数0、映射和旧对象清理、远端模型双眼及无额外Rig均通过；证据 Logs/HeadUI-Processes-20261026-131805-036/verification.json。没有硬件的真实源有效样本仍为0，不注入假追踪。
- 没有真实 Meta Link 画面/交互验证；测试夹具的合成姿态仅用于隔离回归，不注入产品场景运行源。
- Scene/Prefab/材质及其 .meta 均由 Unity API 实际生成；ProjectSettings/TagManager 的层配置由 Unity 序列化。没有更换包或调整追踪/网络默认超时。

复用的升级工具入口为 QuestLAN/Update head avatar and session controls。已有 HeadAvatar 或材质会保留编辑结果，不应将它当作重置工具。修改或调试后仍需保持本机相机排除层与远端默认层的区别。

## 修改文件与影响

| 文件 | 目的 |
| --- | --- |
| Assets/Scenes/MainScene.unity、Assets/Prefabs/Visual/XRNetworkPlayer.prefab | 接入本地头与远端头 Prefab，保持现有 Rig/腕部映射 |
| Assets/Prefabs/Visual/HeadAvatar.prefab、HeadBlue.mat、EyesDark.mat 及 Unity 生成的 .meta | 可直接在 Unity 编辑的球体/双眼模型和材质 |
| Assets/Scripts/Integration/NetworkAvatarPresentation.cs、VRSessionPanel.cs | 远端模型、英文世界面板、输入共享及按钮可用状态 |
| Assets/Scripts/Networking/LanSessionController.cs | 桌面英文操作、共享目标字段、按实际生成玩家统计人数，复用原会话生命周期 |
| Assets/Editor/QuestLANIntegrationSetup.cs、ProjectSettings/TagManager.asset | Unity API 升级入口及本机头部相机排除层；TagManager 由当前 Unity 版本序列化迁移 |
| Assets/Scripts/Simulation/IntegrationSmokeProbe.cs、QuestLAN.Simulation.asmdef | opt-in 实际按钮双进程探针及 UI 程序集引用 |
| Assets/Tests/Editor/IntegrationRegressionTests.cs、Assets/Tests/PlayMode/NetworkIntegrationLifecycleTests.cs、QuestLAN.Tests.asmdef | Prefab/相机排除及头部、输入、断开/重启回归和测试依赖 |
| README.md、docs/tasks.md、docs/testing.md、本文件 | 使用入口、验证结果与硬件限制 |

提交说明：`feat: add head avatars and English session controls`。
