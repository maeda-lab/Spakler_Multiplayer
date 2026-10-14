# 验证与实际结果

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
