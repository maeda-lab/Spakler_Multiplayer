# M2 网络验证记录

日期 2026-10-14（JST），feature/network，固定 Unity 6000.3.15f1。

## 实际完成

B0–B7 的最小 LAN 原型已经提供：Host/Client/Disconnect、IPv4/UDP 配置、双人限制/身份/生命周期、Mock 三 Pose/单手失效、owner 权限与服务器验证、双向快照同步、插值/超时隐藏、独立 LAN 场景和 Windows 测试程序。NGO 2.11.2、Transport 2.6.0 经本轮用户授权安装并锁定，Shared v1 未改变。

## 已运行并通过

- Unity 实际编译；Scene/Prefab/meta 由 Unity 生成。NGO Prefab hash 由真实 Unity GlobalObjectId 生成，检查非零。
- PlayMode：6/6，通过，failed=0、skipped=0。Logs/QuestLAN-M2-PlayMode.xml，最终复测 2026-10-14 19:18 JST。2 项 Mock 生命周期/世界重建测试与 4 项协议测试覆盖 uint 回绕/拒旧/去重、独立 validity、超时与恢复、到达时钟插值、有限数值/版本/四元数检查。
- Windows Standalone Development Build：Succeeded，errors=0；Logs/QuestLAN-M2-Build.txt。输出 Build/M2/QuestLANSandbox.exe，仅包含 LanSandbox，未更改主场景 Build Scene 列表。
- 两个真实 Windows 程序进程，127.0.0.1、UDP 17777，headless：Host/Client 都看到两个玩家并收到对方持续运动；各自收到至少 324 个远端快照，观察单手失效、全失效、恢复；远端源绑定被拒绝，root 相对姿态正确重建。Logs/M2-host.json 与 M2-client.json。
- 第三个真实进程拒绝，原因 Room full: maximum two players including Host，未生成玩家；Logs/M2-overflow.json。
- 第一个 Client 退出后 Host 玩家数 2→1，重新加入 1→2，新连接独立序号/缓存；Host connectionCount=2、sawRemoteLeave/sawRemoteRejoin=true。重加入 Client 在 Host 停止后玩家数 2→0，报告 Disconnected due to host shutting down；Logs/M2-rejoin.json。所有该轮测试进程已自动退出。
- 此前本地 NetworkSandbox 实际画面验证：左腕失效只隐藏 cube，输出中断全部隐藏，恢复有效对象显示。
- 用户处理防火墙窗口后，Editor Host + Standalone Client（UDP 7777）实际连接；最大化 Game 面板观察到两组头球/左腕 cube/右腕 capsule，且远端运动、失效和恢复可见。Editor Stop 后 Idle、无 Avatar，再次 Start Host 正常生成本机玩家，并接纳新的 Client。Logs/M2-editor-client-final.json 接收 587 个 Host 快照，Logs/M2-editor-restart-client.json 接收 349 个且确认运动/正确重建。已停止网络和 Play，保留 LanSandbox 打开。

## 限制与历史失败

首轮 Unity 无许可证导致退出 198；许可证恢复后已编译/测试成功。首次 NGO UPM 解包重命名遇到 EPERM；从 Unity 官方 dist.tarball 恢复同版本缓存，SHA1 与官方 shasum beeaefb722f735ce40752c4dda2c83480d08c97a 完全相同，之后 UPM 识别并导入。未修改 SDK 源码。首次代码编译曾缺 Simulation 对 NGO 程序集引用，修正后编译成功。新网络 Prefab 初次 hash=0，调用 NGO 的真实 OnValidate 保存持久标识后重建并完成实际联机测试。所有早期失败仍保留在忽略的 Logs/Editor 日志。

初次图形 LAN 检查遇到 Windows 防火墙权限窗口；用户已自行处理该安全请求，助手未操作安全设置。随后 Editor Host 首次启动出现 NGO 消息注册表为空（Allowed Count: 0），补充 recompile-lan 入口，通过 Unity 的 CleanBuildCache 全量重编译后，Editor 正常启动/连接/停止/重启；未改 SDK 源码或调用内部注册表修补。早期 GUI Client 无 Host 导致连接失败的日志保留，不覆盖。已验证 Editor GPU 画面 + headless Client；没有声称两个图形窗口均检查过。

未验证：两台 PC 的真实 LAN、Wi-Fi/AP 隔离、强制断网/高抖动/大规模丢包、恶意伪造 RPC（权限由 NGO/server 代码核对，测试仅验证远端绑定拒绝）、Quest、Android、XR 接入。RTT/变色/手指未实现。桌面成功不证明 Quest 联机成功。

本轮入口已存在 XR Package Settings、EditorBuildSettings、ProjectSettings 三项未提交改动，保留并排除 B 提交。Unity 包导入/Build 自动生成 XRGeneralSettingsPerBuildTarget、OpenXR Editor Settings（A 范围），保留供 A 审核，不纳入 B 提交。Unity 自动保存的此前干净 URP/Graphics/ShaderGraph/UnityConnect 改动已核对后撤回；后续打开/Build 可能再次自动生成。NGO 自动生成根目录 DefaultNetworkPrefabs.asset 是包的默认列表，保留其 Unity meta；Session 使用 LanNetworkPrefabs 明确列表。曾移动生成的冗余列表已确认无引用，移至忽略的 Logs/M2-known-generated 备份，不删用户文件。

## 人工运行

1. 6000.3.15f1 打开现有工程、等待 UPM 与编译。打开 Assets/NetworkingSandbox/Scenes/LanSandbox.unity；现在已经存在，无需再次 Create。
2. Editor Play，Start Host（默认 UDP 7777，监听 0.0.0.0）。运行 Build/M2/QuestLANSandbox.exe，输入 127.0.0.1 与相同端口，Start Client。同机也可用两个 Standalone；两 PC 则输入 Host 实际 LAN IPv4。
3. 两边各有 Local Owner/Remote ID；观察头球、左腕 cube、右腕 capsule。另一端显示应受本端左右追踪、Animate、Output available 开关影响，不修改对方玩家输入。
4. Client Disconnect/重新 Start Client；Host Stop 后重新 Start Host；检查人数/对象/旧状态清理。已测退出/新进程加入、Host 停止和图形 Editor 同实例重复启动。小 Game 面板可能与诊断 UI 重叠，双击 Game 标签最大化后观察完整双人画面。
5. 勾选 Diagnostic logs，Console 或 player log 输出连接、spawn/despawn、tracking 状态转换，不输出每帧日志。失败记录具体原因，并检查 LAN/Host/地址/端口/网卡/防火墙/AP 隔离。
6. PlayMode Test Runner 运行 QuestLAN.Simulation.Tests 与 QuestLAN.Networking.Tests。Build 菜单工具或验证入口用明确 scenes 参数构建 Windows，不改变 A 的默认 Build Scene。

B 的已打开 Editor 验证桥读取 Logs/QuestLAN-M2-Validation.request，支持 setup-lan、validate-lan、build-lan、preview-lan、recompile-lan（及旧 validate-mock/preview-mock）。有未保存场景时拒绝动作；先刷新并等待工具编译完成，再投递请求，避免旧程序集消费新请求。recompile-lan 调用 Unity 的 CleanBuildCache 重新编译，恢复安装过程中可能不完整的 ILPP；不要在 Play 中编辑脚本。setup-lan 不覆盖已有资产。结果/日志/Build 全部忽略、不提交。

LanSmokeProbe 仅在独立程序传入 --lan-role 时启用，无参数时不自动联网。测试参数：--lan-role host/client/overflow/rejoin，--lan-duration 秒数，--lan-port 17777，--lan-report 本地 JSON 路径，配合 -batchmode -nographics -logFile。探针按本机启动后 4–7s 模拟单手失效、9–11s 中断输出，结束保存报告、Disconnect 后退出；测试开关不用于产品场景。

## 修改范围与提交建议

B 文件：Assets/Scripts/Networking（连接/同步/包/缓冲/程序集/测试）；Assets/Scripts/Simulation（Mock、几何显示、UI、真实进程探针及测试）；Assets/NetworkingSandbox（Editor 工具、两个场景）；Assets/Prefabs/Network（本地预览和网络玩家/Session/注册列表）；Assets/DefaultNetworkPrefabs.asset（NGO 自动生成）；全部 meta；docs/networking.md 与本文件。授权依赖修改：Packages/manifest.json、packages-lock.json。

建议 commit：feat(networking): add two-player LAN mock pose prototype。

PR 摘要：增加固定版本 NGO/Transport 的双人 LAN Host/Client 与网络玩家；owner 快照经服务器校验转发，保留 Shared v1 根相对坐标及追踪失效语义；提供 Mock/独立 Sandbox 与双进程验证。Unity 6/6、Windows Build 与 loopback 双向同步/人数限制/离开重入已通过；真实 LAN/Quest、RTT 与颜色仍待后续。

人工审核包版本、源绑定/根映射、权限/序列号、有效性/缓存、生成资源 hash/meta、实测日志，以及与 A 的原有 XR 设置变更分开提交。不自动 commit/push/merge。

## 给 A 与下一次任务

NetworkSession/NetworkPlayer 已提供。A 集成唯一 XRHandsPoseSource，确保它相对本机网络 AvatarRoot 采样；远端只创建视觉，AvatarPoseView 消费 NetworkPoseSync。停用 Sandbox 的 Mock/控件/探针，按本地头隐藏/远端头显示策略替换测试几何。Shared v1 不需升级。

下一次具体指令：在当前 LAN 原型上实现 Client 单调时钟 Ping/Pong RTT（current/smoothed/timeout）与 server 权威 NetworkColorPulse（晚加入取得当前颜色），在双进程验证后补双 PC LAN 记录；不修改 Shared、XR、MainScene，不把 RTT 当成运动端到端延迟。
