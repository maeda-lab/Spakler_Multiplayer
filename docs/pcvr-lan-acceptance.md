# PC + Meta Link 验收（2026-10-21）

最终产品为 Windows x64 程序，通过 Meta Link 获取头/手追踪。Quest Android 构建、ADB、原生 Quest 部署不属于交付门槛。历史 Android 结果不删除、不算作 PCVR 证据。

## 两台 PC、零头显的本轮范围

先以独立 Sandbox 的 Mock 数据验证真实 LAN。2026-10-21 已完成跨 PC 基础、满房、退出重入、Host 异常停机与重启后手动连接，证据见本页末尾；Link 恢复和双头显仍待硬件验收。Mock 的通过不能证明真实 XR 通过。默认使用 UDP 17777；主产品 UI 默认 7777，两端必须使用相同端口。

## 构建与交付

使用 Unity 6000.3.15f1、Windows x64 活动目标，执行 `QuestLAN.Editor.QuestLANIntegrationBuild.BuildWindowsBatch`。

- `Build/M3/QuestLANPrototype.exe`：正式 PCVR 路径；先进入 Meta Link 会话，运行程序；无参数不自动联网。
- `Build/M3Sandbox/QuestLANSandbox.exe`：不启动 XR 的独立模拟网络程序，本轮两 PC 使用此目录。
- `Build/M3Headless/QuestLANPrototype.exe`：无硬件 MainScene 生命周期探针，不作为 VR 运行包。

跨 PC 复制完整 Sandbox 输出目录（包括 exe、Data、DLL、UnityPlayer 等），不能只复制 exe。交付包将其放在 `Sandbox/`，并在同级放置 `tools/Run-LanCheck.ps1`。解压到本地可写目录；启动报告保存在脚本旁 `Reports/`。两 PC 使用同一个包，保存包 SHA256、机器名、Host LAN IPv4、端口、日期、网络类型及防火墙处理结果。不要把 127.0.0.1 填作另一台 PC 的地址。

PowerShell 中运行下列命令；若组织策略阻止脚本，按组织规则处理，也可直接使用后述 exe 参数。不自动修改执行策略或防火墙。

```powershell
# PC A，先启动 Host（保留 3 分钟给 Client 和重连检查）
.\Run-LanCheck.ps1 -Role host -Duration 180
# PC B，Host 启动后尽快执行，替换为 PC A 的实际 LAN IPv4
.\Run-LanCheck.ps1 -Role client -Address 192.168.1.10 -Duration 35
```

脚本不接受终端输入，开始后等待指定时长并检查 JSON；双方看见两玩家后才开始模拟时序，单手丢失发生在此后的 4–7 秒，全失效在 9–11 秒；断开后下一次连接重新开始时序。Client 至少在 Host 结束前 20 秒进入，留足恢复和报告时间。Host 不要求 RTT，Client 应记录 RTT。

满房检查：当两人仍在线时，再开一个终端启动 `-Role overflow -Address <Host IPv4> -Duration 15`，预期 Room full 且无本地玩家。Client 正常结束后运行 `-Role rejoin -Address <Host IPv4> -Duration 30`；Host 报告检查 `sawRemoteLeave/sawRemoteRejoin`，重入 Client 检查 `lateJoinColor`。脚本通用通过不自动代表上述三个专项全部通过，需人工核对各报告。

可直接运行参数（路径包含空格时加引号）：

```text
QuestLANSandbox.exe -batchmode -nographics --lan-role client --lan-address <Host IPv4> --lan-port 17777 --lan-duration 35 --lan-report <绝对报告路径> -logFile <绝对日志路径>
```

MainScene 探针同样支持 `--m3-address <Host IPv4>`；不提供地址时两类探针仍默认 127.0.0.1，保留原有 loopback 用法。

打包脚本：在工程内执行 `tools/Prepare-LanPackage.ps1`，生成忽略目录 `Build/PCVR-LAN-<时间>.zip`、SHA256 和逐文件 manifest；manifest 明确记录源码 commit 及是否存在未提交修改。包中仅包含 Sandbox、启动脚本与说明，正常 PCVR 产品另用完整 `Build/M3` 输出。

## 验收顺序与门槛

| 阶段 | 实际操作 | 通过条件 |
| --- | --- | --- |
| LAN 基础 | A Host、B Client，双向 Mock | 两玩家、运动/左右手状态、失效恢复、root 重建、颜色、Client RTT，保存两端报告 |
| 生命周期 | 满房、Client 退出/重入、Host 停止/重启后 Client 手动重连 | 无第三玩家、旧对象清理、重连新状态；不要求自动重连 |
| 故障恢复 | 操作者断开网络后恢复，单独保留本轮报告/日志 | 超时隐藏、明确状态；重新连接后无旧姿态/旧玩家。固定时序探针不能替代故障专项 |
| PCVR 单端 | 有头显后，产品进入 Meta Link | 头/双手/骨骼、凝视面板、颜色、Link 中断重连、头显休眠和重新居中；记录是否需重启应用 |
| 双 PCVR | 两台 PC 各自连接头显，同一 LAN | 远端只有视觉，无第二硬件 Rig；双向头/腕追踪、独立失效、恢复和同步事件 |
| 稳定性 | 长时间运行及可控抖动/丢包 | 记录时长、帧率、收发率、RTT、异常和退出清理；RTT 不等于运动显示延迟 |

当前姿态默认发送 20 Hz、插值 100 ms，先记录实际观感再决定调参。手指网络同步在上述基础链路稳定后设计独立 HandJointFrame；本地官方骨骼显示不是手指同步。

## 本轮验证记录

- 2026-10-21，分支 `codex/pcvr-lan-validation`，基线 main `0531db0`；验收结束后用户明确授权提交并推送。提交时复核既有测试记录与文件范围，不声称重新运行Unity或硬件测试。
- 固定 Unity 实际编译及 EditMode **7/7 Passed**（`Logs/PCVR-EditMode.xml`），完整隔离硬件 PlayMode **14/14 Passed**（`Logs/M3-PlayMode-Final.xml`、`Logs/PCVR-PlayMode.log`），failed/skipped 均 0；XR 启动恢复。
- Windows 产品、MainScene 无头版、Sandbox 全部 **Succeeded/errors=0**，`Logs/PCVR-WindowsBuild.log` 和 `Logs/M3-*-Build` 对应摘要（实际摘要文件名为 `M3-WindowsBuild.txt`、`M3-HeadlessBuild.txt`、`M3-SandboxBuild.txt`）。最终新增 LAN 时序在此构建中实际编译，随后以进程测试验证。
- 使用本机网卡 IPv4 `172.20.10.4`，Host 先运行约 24 秒后 Client 才加入。Host/Client/Overflow/Rejoin 脚本均退出 0；Client 收到565个快照、重入466个；RTT平滑约0.72/0.78ms。Host观察离开与重入，重入Client取得先前颜色，满房进程未生成玩家。报告在 `Logs/PCVR-LAN/`。这是**同 PC 使用网卡地址**，不算跨 PC。
- 无头 MainScene 双进程也使用新增 `--m3-address 172.20.10.4`：Host/Client 收到352/353个快照，映射、唯一Rig、源绑定、远端无Rig、颜色及停止清理均通过，进程退出0（`Logs/PCVR-main-*.json/log`）。头/左右手有效样本均为0，符合没有头显的实际条件。
- 本轮 Unity 自动迁移的 Mobile RP 版本字段、PC 构建过滤缓存、URP 全局缓存与 UnityConnect 改写已恢复；未改包版本、Shared、场景或产品 XR 启动配置。
- 冻结的跨 PC 测试包：`Build/PCVR-LAN-20261021-181333-687.zip`，73,430,660 bytes；SHA256 `EB39671CA98B926BEB4318C3C2C4C2A647787E387D4D83A8D3641ADB81F682E2`。内含 Sandbox、脚本、说明及逐文件校验 manifest；源码含本轮未提交改动，manifest 标记 `sourceDirty=true`。包内说明是打包时快照，后续验收结果以本页为准。
- 当前 PC `172.20.10.4`，用户提供第二台 PC `172.20.10.6`，用户手动运行 Client。初始Ping无响应，后续游戏UDP实际联通；不能将ICMP无响应当作UDP不通。
- 临时仅共享测试包的 HTTP 下载服务在本机 HEAD 检查为200，但第二台 PC 用户反馈页面打不开；按用户要求改为直接转发 ZIP，临时服务已停止。不因此声称游戏 UDP 连接失败。
- 第二台 PC（报告机器名 `MSI`）于09:24:51 UTC启动Client，报告 `connected=false`、快照0、`Failed: connection timed out`。该时段Host正常监听`0.0.0.0:17777`，未记录远端连接。保留首次失败。
- 排查发现Host WLAN实际是Private，已有Sandbox入站Allow规则仅为Public；`netsh advfirewall show currentprofile`显示BlockInbound/AllowOutbound。经用户明确授权，以管理员权限新增临时规则`QuestLAN-PCVR-LAN-20261021-Temp17777`：仅Private、Sandbox程序、来源172.20.10.6、本机172.20.10.4、UDP17777；已读回核对。该不匹配是已确认的阻断因素候选，最终是否解决以跨机重试为准。测试后必须移除此临时规则，不修改原Public规则。
- 调整后跨 PC 重试**基础通过**：用户提供 `MSI` 的 `client-retry.json`，09:29:54 UTC启动，连接约0.32秒，863个快照、30个RTT样本，平滑约6.06ms。Host对应连接/退出事件、收到862个快照。双方运动、独立单手失效、全失效与恢复、ownership拒绝、root重建、颜色均true。实际通过证明本次Private防火墙规则修复解决了连接阻断，不需要改NGO同步代码；不将RTT当成显示端到端延迟。
- 两PC玩家在线时，本机另开第三个进程，报告`connected=false`、0快照、`Room full: maximum two players including Host`，满房拒绝通过。脚本退出0，原始本机报告见`Logs/PCVR-CrossPC/`。客户端原始内容由用户粘贴确认。
- 跨PC重入通过：用户提供`MSI/rejoin.json`，09:31:35 UTC启动，约0.24秒连接，收到566个快照、20个RTT样本、平滑约9.44ms；全部追踪/运动/有效性/重建/权限断言及`lateJoinColor=true`。Host事件为1→2→1→2→1，确认退出与重新生成；新Client的`sawRemoteRejoin=false`符合其新进程生命周期，不能据此否定Host重入记录。Host本地自测RTT不作为跨PC RTT。
- 首个600秒Host正常结束、脚本退出0。原计划让下一Client跨过定时结束，因留给操作者时间过短未观察到新连接，不以此声称Host停机专项通过。改为重启Host，观察Client连接及完整追踪恢复后，于09:35:25.555 UTC仅终止核对过路径的Host进程PID5320（`Logs/PCVR-HostRestart/intentional-host-stop.json`）。该故障注入导致Host退出-1和脚本报错，为主动触发而非产品自发崩溃；被终止进程未生成完整终场报告，不用其初始JSON当完整统计。
- Transport序列化断连超时为30000ms；故障注入后等待超过此时长再启动新Host。用户提供`MSI/host-stop.json`：09:34:51.995 UTC启动，先连接并收到649个快照，23个RTT样本；`events`为0.28秒players=2、64.18秒players=0，最终`ProtocolTimeout`。结合停机时刻，玩家清理约发生于Host结束后30.6秒，异常断连清理通过。RTT约7.89ms为此前连接期间的平滑样本，不表示断开后仍正常。此报告不能测出姿态视觉具体何时隐藏；0.5秒接收超时由既有代码/自动测试覆盖，不能冒充本轮显示计时。
- 新Host收到第二台PC新连接，接收862个快照，各运动/失效恢复/权限/重建/颜色断言true，退出后人数2→1（`Logs/PCVR-PostRestart/`）。用户提供`MSI/after-restart.json`：09:37:50 UTC启动、约0.29秒连接、865快照、30个RTT样本、平滑约5.95ms；全部基础断言及`lateJoinColor=true`，最终Connected。Host重启后手动重连通过；没有自动重连功能，也不将本轮当成Wi-Fi丢包/抖动测试。
- 重启后Client退出，于本轮收尾以管理员权限移除唯一临时规则；清理脚本退出0并记录`Removed`，`netsh`读回`No rules match the specified criteria`（该查询退出1表示规则不存在，不是移除失败）。原有Public放行规则未改。若后续仍在Private网络做跨PC联机，需要重新获得授权配置对应规则，不能假定本轮临时放行永久存在。
- 零头显条件下不执行或声称本轮 PCVR 硬件验收。物理链路断网恢复、可控抖动/丢包和长时稳定性仍待验；Host异常停机与重启专项结果见上文。

## 改动与下一阶段

本轮修改AGENTS、README及setup/architecture/tasks/testing等文档以统一Windows+Meta Link目标；两个Simulation探针增加显式地址、机器名和UTC开始时间，Sandbox失追踪时序改为双方出现后开始；构建工具确保Logs存在。新增`tools/Run-LanCheck.ps1`、`Prepare-LanPackage.ps1`与本页，提供完整包、参数、报告检查和校验信息。未改Shared/NGO协议、场景或包依赖；日志、测试输出和构建包不提交。

下一阶段：先安排真实链路中断/恢复与可控丢包抖动、长时间运行；有头显后继续单端Link中断/休眠/重新居中和双PCVR真实头腕验收。单独评估30秒连接清理等待与0.5秒姿态失效的实际观感，再决定是否调整；手指协议依赖上述基础链路稳定。建议commit：`feat: prepare PCVR LAN acceptance workflow`。

| 优先级 | 工作与资源 | 验收产物/门槛 |
| --- | --- | --- |
| P1 | 两PC物理链路中断/恢复，操作者断开其中一端Wi-Fi后恢复 | 分别记录姿态接收超时失效、连接清理及手动重连；旧玩家/旧姿态不得残留。防火墙放行需明确授权并限制程序、来源和端口 |
| P1 | 增加可复现的丢包/抖动注入与报告，再做两PC 30–60分钟运行 | 记录实际注入配置、收发率/快照间隔、RTT/超时、断线与资源变化；低于已有0.5秒接收超时的短断流和超过阈值的长断流分别检查，不能仅以进程未崩溃判通过 |
| P2 | 有头显后单端Windows产品+Meta Link | 真实头/双手、凝视面板、Link中断重连、休眠恢复、重新居中与Floor/Device坐标；记录是否需要重启应用 |
| P2 | 两PC各一套Meta Link | 双向真实头/腕及有效性、唯一硬件Rig、远端视觉、颜色、实际流畅度；区分RTT、插值等待和运动显示延迟 |
| P3 | 基础联机及双PCVR稳定后设计手指契约 | 独立HandJointFrame、索引/坐标/validity/频率/压缩与版本迁移；Shared不依赖XR/NGO，协调A+B后再实现 |
