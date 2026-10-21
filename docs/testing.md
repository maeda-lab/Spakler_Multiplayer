# 验证与实际结果

## 2026-10-19 M3 集成

本轮实现及验证见 [m3-integration.md](m3-integration.md)。合并后的基线在2026-10-16实际重跑 EditMode4/4、PlayMode13/13；本轮 EditMode7/7、完整PlayMode14/14通过，后者包含MainScene同实例Host重启及头部指向Color到服务器/材质的真实调用路径。Quest 2 / Link独立Host已接收到真实头/手帧；面板对齐、可见白点操作和实际变色最终获操作者“一切正常”确认。原生/真实LAN结果另记。

## 本轮 XR 专项验收
详见 [xr-acceptance.md](xr-acceptance.md)。Unity 隔离 PlayMode 自动测试 **7/7 通过**，覆盖独立手部丢失/恢复、缺失 Wrist/Dynamic 数据、暂停回调、subsystem 重启、五次组件启停/订阅和坐标重建。暂停回调为模拟触发，不等于 OS 暂停验收。

Quest 2 / Link 90 秒分阶段检查：左失效/右有效 81 次，左恢复 120 次；右失效/左有效 116 次，右恢复 126 次；操作者确认骨骼和腕标记隐藏/恢复正常。真实 Play 重入 30 秒、269 个采样，左右手各有效 267 次，操作者确认正常。原始日志留在忽略的 Logs 目录；实际 OS 暂停、设备 Floor/Device 切换和 Quest 3 原生仍待验收。

不得从静态检查推定 Unity 编译、从 Editor 测试推定 Quest 追踪或 LAN 已通过。

## 2026-10-14 当前记录
- 已检查：Unity 工程结构，6000.3.15f1 版本、Force Text、Visible Meta Files；原 Git main 干净且仅初始提交；经用户同意移动 Git 元数据，保留历史/origin。
- 已运行：固定版本 Unity UPM 顺序安装 XR Management 4.5.4、OpenXR 1.16.1、XR Hands 1.7.3、XRI 3.3.1，四项成功且 manifest/lock 有实际记录。日志 `Logs/QuestLAN-PackageInstall.log`（忽略、不提交）。
- 已运行：Unity API 生成 MainScene 并设置默认 Build Scene；通过官方 Sample.Import 导入 HandVisualizer 1.7.3；首次 M1 批处理 C# 编译成功、退出码 0。日志 `Logs/QuestLAN-M1Setup.log`。
- 首轮 EditMode 组件测试 0/3，通过失败详情发现 EditMode 不适合运行时组件生命周期及可附加测试 fixture；已改为 PlayMode 协程，使用真实 LateUpdate/OnEnable/OnDisable，待最终运行结果填写。保留首轮日志，不掩盖失败。
- 中间 PlayMode 编译在源修正前读取了位置错误的 using 指令，退出码 1；文件修正后重新执行，未以失败运行声称通过。
- **最终 PlayMode：Passed，total=3，passed=3，failed=0**；Test Runner 报告退出码 0。已验证导入官方样例后的项目 C# 编译。结果 `Logs/QuestLAN-PlayMode-Final.xml` / `QuestLAN-PlayMode-Final.log`；日志由 gitignore 排除。不是 Quest 设备测试。
- 静态检查：四个 XR manifest/lock 版本一致；Shared/Avatar 无 XR Hands/NGO 实现依赖；Assets 新增文件/目录均有 Unity 生成的 .meta；MainScene 的源/显示/Camera 输入引用已序列化；缓存/日志/生成 IDE 项目被忽略。
- 未验证：OpenXR loader/feature 配置、Project Validation、渲染画面、Link 手追踪、Quest 原生手追踪、Android Build、双机网络、Meta SDK。

## 2026-10-14 后续配置记录
- 开始时分支 `codex/xr-hands-m0` 干净，用户已提交并推送 `641e6f5`；本次继续修改未提交、不 push。
- Windows 注册表活动 OpenXR runtime 为 Meta Quest Link。ADB 初次设备列表为空，未声称头显已连接或有真实追踪结果。
- 双平台 OpenXR/Hand Tracking Subsystem 与 Android Meta Quest Support 通过公开 Unity API 配置；MainScene 复用官方 HandVisualizer 的资产引用接入同一 Rig。
- 配置工具首次编译因遗漏验证 API 编辑器命名空间失败，已修正。第二次运行遇到安装包 Meta 规则跨活动平台空引用，已改为各活动平台独立检查，不修改包、不使用反射。第三次 Standalone 配置/验证退出码 0。
- StandaloneWindows64 Project Validation：**0 errors，2 optional warnings**，关于 InputSystem.XR.PoseControl 迁移和 StickControl thumbstick 迁移。当前项目不使用这些复合 pose/thumbstick 输入，保留并记录；不是阻止构建/Play 的错误。报告 `Logs/QuestLAN-XRValidation-StandaloneWindows64.txt`。
- Android 活动目标 Project Validation：**0 errors，4 warnings**。除上述输入迁移之外：SSAO 警告来自仍保留的 PC_Renderer（Android 默认 quality=Mobile，Mobile_Renderer 的 rendererFeatures 为空）；另外推荐 Prioritize Input Polling，当前未启用该可选优化，需后续设备上比较。报告 `Logs/QuestLAN-XRValidation-Android.txt`。
- Android Development APK：**BuildResult.Succeeded，errors=0，Unity 退出码 0**。产物 `Builds/Android/QuestLANPrototype.apk`，实际 APK 大小 50,242,977 bytes；构建摘要 totalSize 还包含调试符号等，不等于 APK 大小。日志 `Logs/QuestLAN-AndroidBuild.log`。首次包含 Vulkan 着色器、Burst、IL2CPP/ARM64 和 Gradle 打包。
- 已用 Editor SDK 的 aapt 检查最终 APK：包名 `com.maedalab.questlanprototype`，native-code `arm64-v8a`，minSdk=25、targetSdk=36；包含 `com.oculus.permission.HAND_TRACKING`、可选 `oculus.software.handtracking` 及必需 VR headtracking 声明。生成的 Meta supportedDevices 列表含 Quest 3 标识 eureka。包声明不等于实机运行通过。
- 用户目前不能连接 Quest 3，仅可提供 Quest 2 / Quest Link；已请求进入有效 PCVR 会话。该路径只能记录 Quest 2 / PCVR 的结果，不能代替 Quest 3 原生、Android 安装、双机或 Quest 3 手追踪验收。
- 新增并实际运行 `QuestLANLinkProbe`：Windows x64 下菜单 `QuestLAN/Run 90-second Link hardware probe`，打开 MainScene 并采样真实源，日志 `Logs/QuestLAN-LinkProbe.csv` 和 Summary。只有真实源 flags，不生成模拟有效追踪。
- Quest 实机、视觉效果、左右手丢失恢复、暂停重入仍须设备执行。
- Quest 2 Link **首轮真实 90 秒采样已运行**：342 次采样，HeadTracked=true 338 次，LeftHandTracked=true 15 次、RightHandTracked=true 15 次。头部 root 相对位置范围 (0.360, 0.174, 0.264) 米。有效手帧出现在约 1.078–5.952 秒，之后双手失效；未把这次短暂有效视为持续手追踪或丢失恢复验收通过。
- OpenXR 诊断日志明确识别 `System Properties: Oculus Quest2`、Runtime Name `Oculus`，启用了 `XR_EXT_hand_tracking` 和 Hand Tracking Subsystem。该环境确实提供过手帧，后续还需排除控制器/双手切换与追踪条件；画面正确性等待操作者反馈。
- 本轮实时 Editor 日志包含 Android Extension 的 CancellationTokenSource Dispose 提示及 Unity CoreBusinessMetrics 缓存 SQLite 锁消息，未观察到 QuestLAN 脚本异常。它们不能被解释为网络或手追踪已通过。
- **操作者另行进入 Play 后确认正常**：Quest 2 / Link 中可见双手骨骼，左手腕为长方体、右手腕为球体。此为真实画面反馈，独立于首轮自动采样结果；该次手动 Play 未运行 probe，不给它虚构采样计数。首次短暂手帧可能与加载/追踪条件有关，但根因尚未确定。已确认基本 PCVR 头/手显示及再次进入 Play；单手遮挡/恢复、应用暂停/恢复和 Quest 3 原生仍未完成专项验收。
- 构建/打开双平台使 Unity 自动迁移了 Mobile_RPAsset、DefaultVolumeProfile、URP Global Settings，并写入构建着色器过滤/运行时设置；保留引擎生成结果。已还原本次引擎自动启用的 UnityConnect 开关至原值，未引入云服务。Android GameActivity 构建临时目录 `.utmp/` 已加入 gitignore，不提交 APK/符号/原始姿态日志。

## 轻量自动验证
PlayMode Test Runner 的 `QuestLAN.Tests` 验证：旋转/平移 AvatarRoot 下重建世界 Pose；左右手 flags 独立；本地头隐藏；无帧时隐藏；无设备不得输出 true；重复读取不增 Sequence；停用失效/重启无帧；缺引用和缩放祖先拒绝采集。TestPoseSource 仅测试 fixture，不提供 B 的网络/模拟实现；测试程序集只在 Test Runner 测试构建中启用，不是正常产品依赖。

复现：Window > General > Test Runner > PlayMode > QuestLAN.Tests > Run。批处理使用此精确 Unity：`-batchmode -nographics -projectPath <root> -runTests -testPlatform PlayMode -testFilter QuestLAN.Tests -testResults <root>/Logs/QuestLAN-PlayMode-Final.xml -logFile <root>/Logs/QuestLAN-PlayMode-Final.log`；不加 -quit，Test Runner 自行退出。检查 XML 的 failed/passed，不只看进程存在与否。测试无硬件前提：无启动 XR loader 和连接 HMD；硬件存在时改在无设备测试环境运行，不能用此用例覆盖真实有效追踪。

## Unity / 本地 XR 人工验收
| 检查 | 步骤 | 通过标准 |
| --- | --- | --- |
| 编译和加载 | 6000.3.15f1 打开，UPM resolve，Console | 无项目 C# 错误；记录包/样例 warnings |
| 官方追踪 | 先 HandVisualizer，查看 subsystem 是否 running | Quest 上双手骨骼独立显示；记录 runtime 与目标平台 |
| 头追踪 | MainScene 移动/旋转 HMD，观察调试器最新 frame | HeadTracked=true，root 相对位置/旋转正确；本地头隐藏 |
| 左右手身份 | 单独举左/右手，不依赖相机镜像 | 左手 cube、右手 sphere；Frame 相应 wrist 独立变化 |
| 单手丢失 | 一只手移出摄像头范围或遮挡 | 仅该手 flag=false、模型隐藏；另一手持续更新 |
| 恢复 | 手回到追踪范围 | 新帧恢复 true，无旧位置冒充实时姿态 |
| 停机/暂停 | 暂停应用、停止 loader、再恢复 | 手/头失效，恢复重采集，无重复事件回调 |
| Play 重入 | 多次进/出 Play，默认 domain reload；可另测禁用 reload | 无残留订阅、错误或重复采集 |
| 坐标映射 | Rig/root 同步平移旋转；检查 Floor/Device 高度 | 头/手重建位置一致，所有映射祖先单位缩放 |
| 原生部署 | Android ARM64 Development Build And Run | 正确 loader/hand provider 启动，完成以上设备清单 |

Frame 没有每帧 Console 输出；可用 IDE 断点/Inspector Debug 检查源私有 latest，避免日志影响帧率。官方骨骼样例和简化 wrist 标记分别验收。

## B 模拟接口与未来 LAN 验收
MockPoseSource 应无 XR/Quest 依赖；驱动头/左右手位置旋转、独立 flags、重复 Sequence、uint 回绕、源重启。接收端应过滤旧帧、处理超时、按 root 重建并隐藏失效手，全部 false 帧不能当成无消息丢弃。

双设备同一 LAN IPv4：Host/Client 连接、第二人加入、退出/重连/断网；确认每机只有一个 Local XR Rig、只有 owner 采集本机。核对头/左右手与变色物体同步、RTT、发送/接收频率；记录两设备版本、构建、IP/网络环境（公开文档勿泄漏敏感网络信息）。不得把 RTT 或未同步的本机时间差当作手运动端到端延迟。M5 再做关节索引、握拳/张手、带宽及关节失效测试。
