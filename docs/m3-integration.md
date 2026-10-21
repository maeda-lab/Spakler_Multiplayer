# M3 XR / LAN 集成

2026-10-19，功能分支 `codex/xr-lan-integration`，基于 main `fc910a3`。本轮用户授权推进 A+B 集成；B 当前没有并行开发。Shared v1、包版本和双平台 XR 注册不变，不自动提交或推送。

## 实现

- MainScene 保留唯一 Local XR Rig、官方本机手骨骼、本地腕标记及地面，加入 B NetworkSession 实例；移除该实例的 MockPoseSource、SandboxPoseControls、LanSmokeProbe。
- XRNetworkBinding 是独立集成层。会话初始 source=null；本机 PlayerObject 生成后，先对齐 Rig 与 Local AvatarRoot 到网络出生点，保留 tracking offset，再配置真实源的 network AvatarRoot 并绑定 owner。Configure 清空旧帧，防止发送重映射前的有效帧。断线/停用解除绑定并恢复离线映射。
- 新建 XRNetworkPlayer / XRNetworkPrefabs 资源；Unity API 创建并让 NGO 生成持久 hash。本机网络视觉隐藏，避免重复腕标记和遮挡视野；远端只有头/左右腕视觉，没有相机或 XR Rig。远端骨骼尚未传输。
- NetworkPlayerSignals 的 RTT 为 owner 发起、服务器回送，回复只携带 token，耗时由请求端本机单调时钟计算。默认 1.5 秒 ping，1 秒 timeout，EMA 权重 0.2；拒绝重复、旧 token 和超时回复，暂停/重入清理状态。Host 是本机 server，界面不把本机 RTT 显示成远程链路性能。
- 每个玩家有一个同步颜色测试方块。owner 发出请求，服务器检查身份并以不高于 4Hz 修改 byte NetworkVariable；所有观察者和晚加入者取得当前颜色。颜色不是逐帧 RPC 事件，不需要客户端补播历史。重连创建新的玩家状态。
- 显示发送/接收姿态频率。RTT、100ms 接收插值和头显运动到画面的端到端延迟含义不同。
- 会话配置协议版本升级为2，以拒绝仍使用旧NetworkBehaviour布局的程序；姿态包Version=1和Shared v1不变。联机双方须使用本轮构建。

## 操作

打开 MainScene。未连接时保留离线 XR 功能。桌面 Game 面板可用原 LAN IPv4/端口/Host/Client/Stop 控件及新增颜色/RTT 信息。

头显内前方有 World Space Canvas 面板：移动头部，把可见白点放到按钮上，保持1.2秒执行一次；移开再指向可再次执行。Host/Client/Stop/Color分别用于建房、加入、停止、改变本机的同步色块；本机色块位于面板右侧，面板显示其颜色序号，其他玩家看到的是同一状态。IP / Port切换编辑项，Clear清空，数字键/点号/退格编辑地址或端口。默认127.0.0.1:7777仅供本机测试，跨机器填写Host LAN IPv4。Host监听0.0.0.0；两台设备不会自动物理共定位。Windows启用后台运行以便切换窗口测试；Android显式请求INTERNET权限。

## 自动验证与实测

- 新增 EditMode 测试及原有回归：**7/7 Passed**，`Logs/M3-EditMode.xml`。覆盖 RTT 相关性/超时/恢复、旋转出生点及 Device 高度 offset 的坐标保持、单位缩放限制，以及 MainScene 单 Rig、真实源延迟绑定、产品网络资源无 Mock/相机。
- 最终 EditMode **7/7 Passed**（`Logs/M3-EditMode-Final.xml`），增加迟到回复在 Update 尚未 tick 时的拒绝断言，检查 Android 强制 INTERNET 权限。
- 隔离硬件的完整 PlayMode **13/13 Passed，failed=0，skipped=0**（`Logs/M3-PlayMode-Final.xml`）：XR/Avatar 7、协议4、Mock2。RunAllAutomatedBatch 在内存关闭启动并在完成后恢复，不保存到 XR 配置。
- 后续增加 MainScene 的真实 NGO Host 生命周期回归后，完整 PlayMode **14/14 Passed**（同一XML更新，日志`M3-PlayMode-Lifecycle.log`）。同一场景连续两次Start Host/Stop，检查仅一个玩家、网络出生点对齐、停止后玩家全部销毁、source解绑以及Rig恢复原位置/旋转。
- Windows 产品、专用无头集成版、Sandbox 构建均 Succeeded/errors=0。首次直接在产品 VR 程序中使用 -nographics 时产生大量 OpenXR handle 错误，虽完成进程报告，也不作为干净验收结果。随后提供独立测试构建：仅这些构建序列化关闭 XR 启动；工程和产品仍启用。原始失败日志保留，不改 SDK。
- 专用 MainScene 双进程：双方 connected/mapped/mappingCorrect/oneHardwareRig/sourceBound/remoteHasNoRig/colorReplicated/mappingCleared/stoppedCleanly 均 true；Host/Client 收到392/394个快照。无硬件时真实头/手有效计数均0，Client 平滑 RTT约0.9ms，为本机loopback数据。`Logs/M3-main-host.json`、`M3-main-client.json`。
- Sandbox 实际进程：Host/Client均观察远端运动、单手失效、全失效与恢复、拒绝远端源绑定及正确root重建；Host收到最多324帧，Client282帧。第三进程明确 Room full。Host人数1→2→1→2→1；重加入Client收到355帧并在加入后2秒内读到Host此前变更的颜色（lateJoinColor=true）。Client/重加入Client分别取得11/12个RTT样本，平滑约2.4/1.5ms，仅为本机loopback。`Logs/M3-sandbox-*.json`。
- Quest 2 / Link 首轮图形产品Client连接专用Host：OpenXR会话进入FOCUSED；Host收到1598个快照，其中头1205、左腕357、右腕397个新序号有效样本；观察独立左失效42、右失效2个样本。Client RTT60个样本，平滑约11.3ms。双方单Rig/映射/清理检查通过。这证明真实XR源经过网络抵达Host，不代表双头显或端到端运动延迟验收。
- 首轮操作者确认手部追踪与凝视变色可用，同时报告背景与文字分离。将原TextMesh+立方体面板改为同一World Space Canvas，背景Image与文字Text共用按钮RectTransform；实际双眼对齐需复测确认。右手专项失效/恢复不以首轮2个失效样本冒充完整专项通过。
- Canvas复测后操作者表示仅剩实际方块不变色，面板对齐/凝视和手部无其他问题。Host收到1069帧，头859、左156、右271个有效新序号样本，独立左失效120、右失效5；Client40个RTT样本，平滑约12.3ms。该轮网络颜色状态报告true并不等于实际材质变色已通过。
- 为实际颜色显示配置显式URP Unlit材质与_BaseColor，产品和Sandbox均使用Unity生成并序列化的材质引用；本机色块移到面板右侧，面板显示本机颜色序号。增加owner请求计数、服务器颜色状态日志、报告中的同步/渲染序号与shader名称，以区分输入、同步和显示。颜色修正后的实机复测、当前Android构建结果待执行后补充。
- 后续诊断显示当时只有自动请求、没有手动请求，不能将问题仅归因于材质。修正凝视命中使用实际按钮RectTransform，显式居中锚点，并加上可见头部指向光标。自动回归实际指向Color按钮，验证owner请求、Host颜色变化、材质AppliedColorIndex，并在Host重启后再次验证；完整14/14通过，日志`M3-GazeRegression.log`。
- 最终120秒Quest 2 / Link复测，用户明确反馈“一切正常”。客户端记录5个颜色请求（1次启动探针、4次手动）；客户端及Host日志均有手动`1→2→3→0→1`变化，材质shader为URP/Unlit，最终同步与渲染序号一致。Client收到2367帧、80个RTT样本，平滑约11.4ms；Host收到2101帧，头1986、左132、右1437个新序号有效样本。双方映射/单Rig/无远端Rig及停止清理均通过。`Logs/M3-Pointer-Link-*.json/log`。不将最后一轮主要操作面板时的左右手计数当作完整遮挡专项。
- 原 Quest 2 / Link 历史追踪验收、原 Android APK 记录不自动覆盖本次集成版本。

## 复现入口

Unity `QuestLAN/Integrate XR LAN into MainScene` 创建缺失资源，向B桌面玩家补入信号组件并刷新明确材质引用/NGO持久hash；保留已有MainScene布局，不重建场景。常规使用直接打开现有MainScene。Simulation程序集因显式集成探针而引用Integration/XR；MockPoseSource实现本身仍仅依赖Shared/Unity，Networking没有读入XR类型。

`QuestLAN.Editor.QuestLANIntegrationBuild.BuildWindowsBatch` 构建 `Build/M3` 产品、`Build/M3Headless` 专用无硬件集成程序及 `Build/M3Sandbox` 独立沙盒，不改变默认 Build Scene。无头测试使用后两个输出；普通VR使用M3。MainScene 程序显式传入 `--m3-role host/client --m3-report <json> --m3-port 17779 --m3-duration 18` 才启用真实源集成探针；它不伪造硬件帧。沙盒继续使用 `--lan-role` 探针，在原报告基础上记录 RTT 和变色/晚加入状态。无参数时都不自动联网。

## 后续硬件门槛

Quest 2 / Link 可验证本轮 PCVR，但不代替 Quest 3 Android。两 PC 真实 LAN、Wi-Fi/AP 隔离、强制断网/抖动丢包、真实系统暂停、Floor/Device 切换、双 Quest 同时运行及原生性能仍需分别记录。手指协议待基础联机稳定后再设计。

## 最终构建与回归

- Android ARM64 Development构建：**Succeeded，errors=0**，`Logs/M3-AndroidBuild.log`。OpenXR当前Android目标validation为**0 errors、4 warnings**，为两条既有输入迁移提示、PC Renderer SSAO提示及Prioritize Input Polling建议；不据此声称原生性能已验证。
- APK：`Builds/Android/QuestLANPrototype.apk`，实际**73,606,431 bytes**。aapt确认包名`com.maedalab.questlanprototype`、ARM64、minSdk25/targetSdk36、INTERNET及HAND_TRACKING权限。未部署Quest 3。构建摘要约1.25GB包含符号等，并非APK大小。
- APK SHA256：`6005EFFCCDC824AB7D505B3C62C34972720EA2FEA8303C1C4B9325712465A241`。
- Android构建后恢复Windows x64，最终EditMode **7/7 Passed**（`Logs/M3-Final-EditMode.xml`），完整PlayMode **14/14 Passed**（`Logs/M3-PlayMode-Final.xml`、`Logs/M3-Final-PlayMode-Cleanup.log`），failed/skipped均0。
- 批处理工具最初过早在RunFinished后关闭Editor，留下本轮生成的4个InitTestScene。已移到忽略的`Logs/M3-test-bootstrap-backup`备份，改为等待正常退出Play及清理后再退出；最终复测没有遗留InitTestScene。未删除用户源文件。
- 完成后Standalone/Android的InitManagerOnStart都为true，默认Build Scene仍为MainScene。UnityConnect恢复原值0；PC管线构建过滤缓存及无语义的图形/XR重写恢复原文件，原有Mobile_RPAsset版本迁移改动保留。Packages和Shared v1内容未改。

## 修改文件与影响

新增Assets资源/脚本及目录均带Unity生成的`.meta`，未手工编造GUID/hash。以下文件另包含相应元数据：

| 文件 | 目的/影响 |
| --- | --- |
| `Assets/Scenes/MainScene.unity` | 单Rig真实XR、去除实例Mock、接入会话/绑定/VR面板 |
| `Assets/Scripts/Integration/QuestLAN.Integration.asmdef`、`XRNetworkBinding.cs`、`NetworkAvatarPresentation.cs`、`VRSessionPanel.cs` | 隔离集成层、出生点映射与断线恢复、远端显示、实际RectTransform命中与可见头部指向光标 |
| `Assets/Scripts/Networking/LanSessionController.cs`、`NetworkPlayerSignals.cs`、`RttTracker.cs` | 会话协议2、RTT/超时/频率、权威颜色与事件诊断；姿态传输层/Shared不耦合XR |
| `Assets/Scripts/Simulation/LanSmokeProbe.cs`、`SandboxNetworkAvatar.cs`、`IntegrationSmokeProbe.cs`、`QuestLAN.Simulation.asmdef` | 扩展进程报告与显式MainScene探针、使用明确变色材质 |
| `Assets/Editor/QuestLANIntegrationSetup.cs`、`QuestLANIntegrationBuild.cs`、`QuestLANAcceptanceRunner.cs` | 通过Unity API配置资源，构建产品/专用无头/Sandbox，隔离硬件测试并等待清理后退出 |
| `Assets/Prefabs/Visual/XRNetworkPlayer.prefab`、`XRNetworkPrefabs.asset`、`NetworkPulse.mat` | 产品网络玩家、明确注册列表、URP Unlit颜色材质 |
| `Assets/Prefabs/Network/NetworkPlayer.prefab`、`Assets/DefaultNetworkPrefabs.asset` | 桌面玩家补入信号/材质；保留NGO自动生成的真实注册记录 |
| `Assets/Tests/Editor/IntegrationRegressionTests.cs`、`QuestLAN.EditorTests.asmdef`；`Assets/Tests/PlayMode/NetworkIntegrationLifecycleTests.cs`、`QuestLAN.Tests.asmdef` | RTT/配置/坐标回归、真实Host两次重启与实际Color凝视调用链 |
| `ProjectSettings/ProjectSettings.asset` | 联网权限、后台运行、Android合法batching配置 |
| `Assets/Settings/Mobile_RPAsset.asset` | 任务开始前已存在的`k_AssetPreviousVersion: 13`改动保留，不冒充本轮实现 |
| `README.md`、`docs/architecture.md`、`setup.md`、`tasks.md`、`testing.md`、`networking.md`、`network-testing.md`、`m3-integration.md` | 更新真实进度、操作、协议与验收证据；保留历史失败与未验证项 |

提交说明：`feat: integrate XR LAN avatars and diagnostics`。2026-10-21 用户明确授权提交并推送本阶段改动；沿用上述已完成的验证记录，不将提交当天的 Git 检查算作重新运行 Unity 测试。原有 Mobile_RPAsset 改动不纳入本次提交；本次不合并 main。
