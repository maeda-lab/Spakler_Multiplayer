# 桌面大厅、头向移动与双端手模型（2026-10-28）

分支 codex/desktop-lobby-locomotion，基于上一轮头部/UI 提交 8cac40f；开发开始时 main 尚未包含 PR #11；本轮提交前 fetch 确认 PR #11 已合入 origin/main（8990c2d）。本轮继续本地 NGO LAN，不引入云服务。用户本轮已明确授权提交、推送并创建PR；不自动合并。

## 启动和退出

- Windows 构建首先打开 Assets/Scenes/DesktopLobby.unity。它只有桌面流程组件；鼠标按钮、可键盘输入的 Host IPv4/UDP Port 和英文状态在运行时创建，使用 Input System UI 输入模块。
- 在 Unity 直接对 MainScene 按 Play 也会先显示同一桌面菜单。Windows 的 XR 自动启动故意设为 false，由 DesktopRoomFlow 在连接成功后手动初始化 OpenXR 和启动子系统；Android 历史配置保持不变。
- Create Host 创建房间；Join 使用输入的 Host IPv4。默认端口7777，两端一致；同 PC 测试可以用127.0.0.1，跨 PC 必须使用 Host 实际 LAN IPv4。
- 选择后加载 MainScene 并等待网络连接，成功后才启动 VR。输入无效或连接失败保留桌面状态。Meta Link/XR 不可用时保持菜单，支持 Retry VR 或 Disconnect，不以相机存在伪装已进入 VR。
- Client Disconnect 离开房间；Host Disconnect 关闭房间。停止网络、清理本机映射和 XR 后返回 DesktopLobby。进入 VR 后仍可使用桌面 Disconnect 工具条或世界面板退出。

## 左摇杆与头部朝向

HeadRelativeLocomotion 读取左控制器 primary2DAxis，仅在本机已绑定网络玩家、已进入房间、头/控制器真实追踪有效且应用有焦点时移动。前后方向取头朝向的水平投影，左右为对应侧移；抬头/低头不产生垂直运动，不额外把头旋转叠加到 Rig。

默认速度1.5m/s、应用死区0.15，输入向量限长避免斜向加速，单次位移的时间步长上限0.05秒以避免卡顿后突跳。保留当前地面高度，没有增加重力、爬坡或碰撞角色控制器；适用于当前平坦原型。Inspector 可调参数，默认在出生点20m半径内移动，地面扩展到50m。

移动只平移本机 XR Rig，AvatarRoot/网络出生点保持固定。头/腕的 root-relative Pose 自然包含移动距离，远端沿原有快照流重建，不增加第二条 root transform 同步流。原5m包校验会错误拒绝移动后的姿态，现使用有限50m上限并增加越界测试。包字段和 Pose 协议版本1不变；会话协议升为3，双方需同版本，拒绝旧会话规则。

## 先前简化手模型（已被下述详细关节视图替代）

- LeftHandAvatar / RightHandAvatar Prefab 各有手掌和五个固定手指外形，左右拇指镜像，无碰撞体。本机与远端使用同一模型，并分别根据左右腕有效性显示/隐藏；对端头部继续使用球体双眼模型，本机头从第一人称相机排除。
- 本机 XR Hands 手腕有效时优先使用手部追踪；不可用时，MainScene 明确启用真实左/右控制器握持姿态回退。握持姿态在本地 Z=-0.04m 处生成近似手腕锚点，位置偏移可在 XRHandsPoseSource 上调整。Controller 输入通过已启用的 Input Actions 在手动 XR 初始化前注册。
- 控制器回退要求对应设备已连接、isTracked 且位置/旋转状态有效，不由 GameObject Transform 冒充追踪；暂停/停用继续失效并释放输入/事件订阅。
- 这是手腕驱动的简化手模型，不提供手指关节动画。控制器手腕有效不代表手指有效；未来 M5 必须独立采集与判断关节有效性。契约补充及给 A/B 的迁移通知见 interface-contract.md v1.1。
- 官方骨架样例在 MainScene 中停用以避免重复叠加；真实手部采集仍由 XRHandsPoseSource 完成。

## 验证入口与状态

- Unity 6000.3.15f1：EditMode9/9、完整PlayMode19/19通过。覆盖大厅配置、头向平面位移/死区/斜向速度/卡顿步长、握持到腕偏移、有限包边界及既有追踪/会话回归。
- 首次编辑器检查发现 Scene 中旧桌面诊断 GUI 的 Prefab 覆盖未保存，已通过 Unity Prefab API 补记并复核。首次新探针编译缺少 XR Management 程序集引用，已补齐后实际编译通过。
- 四种 Windows 构建（产品/MainScene无头版/大厅无头版/Sandbox）均 Succeeded/errors=0；修复后 PlayMode再次19/19。大厅双进程及连接失败检查通过，双方本地手/远端头手、独立失效/恢复、超过5m姿态接收、退出大厅/清理通过（Logs/Lobby-Processes-20261028-175848-153）。同一应用连续两次建房/离房、每次清理旧NetworkManager再加载新Rig的回归通过（Logs/Lobby-Reentry-*）。
- 首轮真实用户确认鼠标点击和键盘数字输入正常，但在DX12编辑器 Create Host 后VR不可用；报告未进入房间且interrupted=true，不计硬件通过。随后修复未等待显示子系统就绪即判断失败的时序，并清理跨场景驻留的旧NetworkManager，避免重试复用已销毁Rig；给映射更新增加退出保护。Windows改用明确Direct3D11。后续单头显通过结果见下文，不把模拟对端或单头显测试记为双头显通过。
- 旧 MainScene 批处理探针/隔离测试默认跳过交互大厅；新的显式批处理 `--lobby-probe 1 --lobby-no-xr` 从大厅进行真实 LAN 按钮操作，使用标注的合成姿态验证模型和远端移动范围，绝不作为真实设备样本。
- QuestLAN/Run desktop lobby and locomotion Link check 打开大厅开始实机观察，不注入本机追踪或输入。进入房间后记录90秒：XR显示、真实头/腕、模型可见性、Rig位移和左摇杆移动样本；结束时断开返回大厅并保存独立 Logs/Lobby-Link-*.json。

Build/Logs 与原始报告不提交。XR启动策略、默认场景、输入和通信规则发生了有意改变，后续不能用“Standalone自动XR启动必须为true”的旧断言覆盖本流程。


## 单头显观察与运动参照调整

Direct3D11 的 Join 检查已实际进入 VR：Logs/Lobby-Link-20261028-180006-047.json 记录86.205秒房间活动、6137采样，其中真实头部有效6075帧、左右腕有效5540/2467帧，本地模型对应显示；模拟对端头和双手显示6137帧。摇杆移动样本1750、Rig最大位移2.514m；最终断开返回大厅，未在连接前启动XR。操作者确认成功进入VR和双方模型可见，但反馈感觉在移动对端而非自己，因此这轮不能记为移动体验最终通过。

已增加固定世界地面网格和四个定位标记，并把世界面板固定在入房位置（保留在Rig层级以兼容现有查找，但每帧保持世界位置/旋转）。面板显示Your rig X/Z，实机观察器新增相机与远端头部各自的最大世界位移，用于区分本机视点运动和对端运动。模拟对端在本轮near-peer模式中固定世界位置；它不是第二套真实设备。

网格复测的首个观察器报告在进入VR前被中断（Logs/Lobby-Link-20261028-180858-467.json），不计通过；随后操作者确认网格下移动正常，并已停止Play；此为人工结果，未补造被中断观察器的位移数据。未声称截图验证成功，Windows窗口捕获通道本轮超时。


## 用户追加要求：自动 IP 与详细关节

地面网格复测后用户确认移动正常；仅一套 Quest 2 + Link/控制器，远端仍为显式合成对端，不代表两套硬件验证。

大厅枚举处于 Up 状态的 IPv4 网卡，优先默认网关/物理LAN网卡，排除loopback/link-local；无候选时回退127.0.0.1。打开大厅时填入，Refresh IP可重新检测，手工输入的目标IP不会逐帧覆盖。Create Host永远建立本机房间（监听所有接口），Join连接输入的Host地址；多个网络/VPN环境仍可手工选择目标IP。

新增 HandJointFrame 契约 v1.2，独立双手26关节/骨架，服务端校验与20Hz同步，替代产品场景中的静态六块手模型。旧模型资产保留兼容历史测试，但其渲染关闭。只呈现运行时有效关节；控制器只有腕姿态时不展示假手指。会话协议4要求双方同步升级。

新增可选原生OpenXR simultaneous-hands feature，每次会话调用resume并记录结果；不安装Meta Interaction SDK。设备能否持续提供手指仍需要持控制器/放下控制器两种状态实测。新的验证结果完成后附于下文。


## 详细关节自动验证（2026-10-28）

- Unity EditMode 10/10（Logs/Hands-EditMode.xml）；最终 PlayMode 24/24（Logs/Hands-PlayMode-Final.xml）。覆盖完整双手805字节往返与量化精度、坏包/mask/NaN、接收数组隔离、序号回绕、超时/独立关节丢失/恢复、到达时钟插值、Root*Wrist*Joint重建、关闭视图立即隐藏。
- 两个实际Windows进程经桌面按钮建立loopback连接，使用明确合成的关节源。Logs/Lobby-Processes-20261028-184029-058：Host/Client分别接收307/304个关节包，双方详细骨架与手指动作变化、独立单手丢失和恢复、超过5m位置、返回大厅/零玩家/单Rig通过。连接失败仍停留桌面并清理；不能据此声称双头显或跨PC新版关节验收。
- 首轮四种Windows构建均Succeeded/errors=0；随后补充骨架视图停用隐藏，最终四种Windows构建再次均Succeeded/errors=0，记录Hands-WindowsBuild-Final.log（18:42–18:43）。
- Quest 2持握/放下控制器时真实关节可用性、实际视觉表现与新协议双头显LAN仍待验；M4a旧版30分钟记录不代表关节通道已长测。


## 用户自行实机验证步骤

用户2026-10-28选择自行完成本轮Quest 2验证；未启动新一轮硬件测试。建议使用同版Windows程序或Unity DesktopLobby场景，Link处于PCVR会话：

1. 大厅确认Local IPv4与本机LAN地址一致、输入框已填入；可手动修改为另一台PC的Host地址。Create Host在本机建房，Join加入输入地址；连接成功后进入VR。
2. 持握控制器观察Joints L/R计数、骨架和Simultaneous状态。缓慢动指，再放下控制器切换空手追踪，确认左右关节真实变化。若计数为0或缺失，记录状态；扩展Active不等于当前帧有光学关节。
3. 分别遮挡/恢复左右手；仅对应关节隐藏，另一手持续更新。摇杆按头部水平方向平移，网格/定位标记维持世界固定。
4. 两台PC同版建房/加入后，各自检查自己的详细手与对方头/详细手。当前只有一套头显时不能完成双设备真实关节验收；不要将合成对端当成真实头显结果。
5. Disconnect应返回桌面大厅，Host退出关闭房间；再建房/加入确认重入。需要重新测试时保留原报告，避免同名文件冲突。

平台边界参考：Meta Unity multimodal文档 https://developers.meta.com/vr/documentation/unity/unity-multimodal/ ；每会话resume规范 https://registry.khronos.org/OpenXR/specs/1.1/man/html/xrResumeSimultaneousHandsAndControllersTrackingMETA.html 。实现以可选扩展和每关节有效性为准，不承诺Quest 2所有控制器组合在Link下始终有光学手指。
