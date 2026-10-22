# LAN 故障与长时验证（2026-10-21）

## 当前状态（2026-10-22）

跨PC 1830秒故障长测已通过功能与超时隐藏检查；Client完整CSV尚未回收，资源趋势结论仍有限。2026-10-22新版包的物理Wi-Fi断网、约30秒连接清理、恢复后新进程手动重连已完成两端核对，通过。首轮重复输出导致的无效报告及后续错过操作窗口的轮次保留为失败/未执行历史，不计通过。

已定位并修复诊断工具初始化缺陷：CSV分享冲突曾在连接启动前中断初始化，Update仍继续采样，引发空引用并覆盖JSON。现在以独占run.lock保留输出路径，拒绝已存在JSON/CSV；初始化失败停止采样并以退出码2结束，保留原报告。Unity在托管初始化之前打开logFile，因此直接命令仍须使用唯一日志名；推荐使用已有时间戳命名的Run-LanCheck.ps1。新增输出保留/并发冲突回归，Unity PlayMode16/16通过（Logs/Resilience-PlayMode-OutputGuard.log）。

上轮Host和代理均已结束，两条临时防火墙规则已移除并读回确认（2026-10-21 10:54:26 UTC），临时唤醒请求已释放。下方“Watching”“待最终报告”等文字为当时的阶段记录，以本节及后续完成记录为准。Windows构建、独立进程回归及以唯一文件名运行的物理断网补测现均已完成；不要求重跑已通过的30分钟长测。

2026-10-22实际进程回归：原Host正常运行时，第二个同报告进程退出2；原Host未中断，独立Client完成20秒、连接19.779秒，双人/运动/独立失效恢复/颜色及正数内存统计通过。原Host退出0后，再使用已有报告启动同样退出2，JSON和CSV的SHA256均保持不变，所有日志无空引用。证据在Logs/OutputGuard-20261022-151430-554/。Python9/9复测通过。

三种Windows构建最终均Succeeded/errors=0（Logs/Resilience-WindowsBuild-OutputGuard-Clean.log）。首次重建曾出现性能测试包删除旧自动生成meta失败，虽然构建result为Succeeded，但errors=1，未将其记为干净构建；已把未跟踪的生成文件归档至Logs后重建通过，未修改包源码或放宽构建标准。当前本机网络已变更，不复用旧IP或旧临时放行范围；物理补测等待当前两端地址确认。

补测包：Build/PCVR-LAN-20261022-151701-643.zip，SHA256为3FE16DD338DC1561CAC929A8A00D2A3C807F572FB814C52468416CF6F9783985；340项清单文件哈希逐项核对通过。基线仍为38a7ed1、sourceDirty=true；未自动提交或推送。包内文档是生成时快照，后续状态以工程文档为准。

补测准备更新：操作者确认第二PC可配合且IP不变；本机重新实测WLAN已恢复172.20.10.4且为Private。等待新版包解压确认后再启动Host，先验证双人连接，再通知操作者断开Wi-Fi。此次只需要此前授权范围内的Sandbox UDP17777临时规则（本机.4、来源.6、Private、指定程序），无需代理17778规则；以本次Host进程生命周期清理并核对移除。准备脚本存于忽略的Logs/Start-WifiRetryHost.ps1；准备阶段尚未启动Host或添加规则。

新版解压后已启动补测Host：2026-10-22 06:30:03 UTC，最长900秒，PID21964；输出Logs/WifiRetry-20261022-152957-284/。独立管理员脚本已创建本轮唯一规则QuestLAN-WifiRetry-20261022-Sandbox并检查存在，Host启动报告有效、内存计数为正。该脚本等待本次Host结束后在finally移除规则并检查不存在，状态输出Logs/WifiRetry-State.json；此时规则尚在使用。给Client的命令使用毫秒时间戳分别命名JSON/log，运行240秒；先等待20秒初步连接报告再执行物理断网。尚无本次Client结果，不计通过。

该轮Client初步报告已确认连接及全部基础断言：MSI，06:33:34 UTC启动，报告elapsed25.16秒、收到425快照、metricsError为空。但收到回复时Host已elapsed746秒，记录玩家在449.82秒离开，意味着Client已跑完240秒；这份较早报告不能用于指挥实时断网。已撤回断网指令，不计物理断网通过。旧Host被明确结束，协调器等待其临时规则清理完成后重新启动同范围Host，PID26204、06:43:12 UTC，输出Logs/WifiRetry-20261022-154309-890/。后续采用Client本地交互提示Wi-Fi操作及完成后新进程重连，避免异步回复延迟越过操作窗口。新Host的规则仍由其进程生命周期清理。

第二个900秒窗口同样在等待操作者期间剩余不足整轮时长，且未收到Client连接，不计测试结果。现改用最长3600秒的本轮Host，旧进程与规则清理后于06:56:00 UTC启动PID22012，输出Logs/WifiRetry-20261022-155558-490/；仍只放行同一Private/程序/IP/UDP17777范围，进程结束或时长安全上限后清理。此窗口时长不作为实际活动连接时长的证据。

基线main `38a7ed1`，工作分支`codex/lan-resilience-soak`。当前两PC：Host `172.20.10.4`，Client `172.20.10.6`；零头显。不部署Android，也不修改Shared/网络包格式或产品默认超时。测试阶段保留未提交源码的包清单；用户现已授权将本轮改动提交并推送到该功能分支。

## 物理断网最终结果（2026-10-22）

- 用户提供完整汇总JSON，原文保存为Logs/Resilience-FromMSI/wifi-retry-results.json；拆分两份报告及核对摘要保留于同目录。用户按本地终端操作后确认关闭/恢复时刻为06:59:13.1842386Z、07:00:09.5839500Z，确认间隔56.400秒（不是精确网卡状态切换测量；关闭操作先于按Enter）。恢复后的IPv4未单独提供，但新进程已成功连上同一Host，不把地址未确认作为失败。
- Client wifiDrop正常完成240.008秒，曾连接56.699秒，收到465/发送946快照。事件27.50秒姿态接收超时，57.06秒玩家2→0，最终ProtocolTimeout。1770次超时检查全部隐藏，姿态/显示违例均0，metricsError为空，内存计数有效。maxReceiveAge约30.031秒、超时隐藏持续约29.531秒，符合0.5秒姿态超时和约30秒连接清理两个阈值；按同一Client接收年龄解释，不跨机减时间戳。timeoutRecoveries=0符合连接已销毁，不能要求旧对象自动恢复。
- 新进程wifiRejoin正常完成45.014秒、实际连接44.842秒，收到795/发送743快照，最终Connected；双人/运动/独立失效恢复/权限/坐标重建/颜色及晚加入颜色均通过，无接收超时及姿态/显示违例、无metricsError。这是新进程手动重连，不是自动重连功能。
- Host同一进程记录159.54秒玩家1→2、186.77秒姿态超时、216.30秒玩家2→1、401.19秒玩家1→2、446.10秒玩家2→1；connectionCount=2且sawRemoteLeave/sawRemoteRejoin均true。1765次超时检查全部隐藏，违例0、metricsError为空；无第三个或残留玩家。Host只比较自身elapsed事件，不与Client UTC相减。
- 验收完成后明确停止测试Host，故它的completedNormally=false、进程退出-1为测试清理，不冒充自然跑满3600秒或产品崩溃。管理员finally清理后另行确认本轮唯一临时规则不存在（07:06:26.682 UTC），Logs/WifiRetry-State.json为stopped/temporaryRuleAbsent=true，Logs/WifiRetry-Cleanup.txt存复核结果。当前未留测试Host或临时规则。
- 此次完整Client CSV和上轮soak CSV尚未回收；JSON足以核对本次连接、断网、隐藏、清理和重连，资源趋势及逐秒RTT/收包率尚不能补记。零头显，不新增PCVR硬件验收结果。下一步回收CSV完成资源趋势分析，再在两套Meta Link可用时执行M4b真实设备生命周期及双人追踪；M5手指协议仍在后续。

## 实现范围

- `NetworkPoseSync.ReceiveAgeSeconds/ReceiveTimedOut`与buffer最后接收时刻为只读诊断。旧序号/重复帧不能刷新最后接收时刻；不修改采集时钟语义。
- opt-in `LanSmokeProbe` JSON schemaVersion=2；保留原断言，新增实际时长、累计快照、接收超时/恢复、LateUpdate后远端几何体隐藏与姿态有效性断言。1Hz CSV包含收发累计、接收年龄、RTT及其timeout、working set/private/managed/Unity分配、CPU累计时间、最大Update步长；JSON每5秒更新，完成时`completedNormally=true`。事件最多256条，超出计数；CSV流式写入。
- 无头帧步长不是PCVR帧率；CPU字段为累计CPU秒，不是瞬时百分比；working set会受OS分页/缓存影响，不能只凭一次首尾差认定泄漏。诊断本身有采样开销。无头probe显式限制60fps、关闭vSync（仅进程内，销毁恢复），避免无限帧率放大诊断开销；不改变正常产品帧率配置。
- 实测Windows Mono的Process内存属性会无异常返回0；现改用Windows `GetProcessMemoryInfo`读取working set/private bytes，必须为正，否则报告采样失败。对应原生计数增加实际回归；其他平台使用系统Process属性并同样拒绝0，本轮只验证Windows。
- `tools/lan_fault_proxy.py`是独立Python3标准库UDP代理，不改NGO包。现有NGO 2.11.2源代码已将`SetDebugSimulatorParameters`标为无效，因此未使用该旧接口。代理只转发到本机loopback目标，只接受指定Client IP及本次第一个Client端口；不是多Client服务，也不支持同进程切换Client端口。退出/重入专项使用直连或重启代理。
- 双方向分别使用固定种子，模拟datagram丢弃及延迟40±20ms等配置，可发生重排；影响包括握手、可靠消息、姿态、RTT在内的全部UDP数据。相同输入次序可重现决策，真实网络到达次序和OS调度不保证逐包重放。
- 代理报告分别记录收包、实际转发、随机/断流/队列溢出/退出丢弃、发送错误及队列峰值、决策摘要；不保存载荷。队列限制4096个datagram且不超过8MiB。每个包恰归入转发、丢弃、待发或发送错误之一；不能将UDP丢弃百分比等同于姿态快照损失百分比。

## 测试矩阵

| 场景 | 注入与时长 | 检查 |
| --- | --- | --- |
| 本机代理基线 | 零丢包/延迟 | 真实NGO连接、双向Mock/颜色，代理双向有转发 |
| 本机故障回归 | 5% datagram丢弃、每向40±20ms；首次Client包后20秒断流0.2秒、30秒断流1.5秒；60秒Client | 记录两段实际丢弃；长断流接收超时、姿态失效/几何体隐藏、流量恢复、保持连接。短断流单独按CSV检查，不能被长断流的通过覆盖 |
| 跨PC故障 | 同上；第二PC无须Python，通过Host代理17778加入 | 两端JSON/CSV及代理报告相互核对，配置与种子留档；仅Proxy测试仍不是物理Wi-Fi中断 |
| 物理断网 | 第二PC直连17777，客户端运行期间手动关闭Wi-Fi约40秒后恢复 | 记录操作者实际断开/恢复时刻，0.5秒姿态失效及30秒连接清理分别核对；新进程手动重连无旧玩家。不修改整个系统的防火墙/网卡配置代替操作者操作 |
| 长时 | 两PC同一包，Client至少1800秒；优先稳定链路，再用可控注入 | 持续收发、最终连接、超时恢复/错误、CSV资源趋势；不能只凭进程未退出判通过。Header总时长包含等待Client，必须另查connectedSeconds |

## 命令与产物

固定Unity实际编译及测试后，用现有`QuestLANIntegrationBuild.BuildWindowsBatch`构建。`Run-LanCheck.ps1`要求schema2完成报告，可加`-ExpectReceiveTimeout`、`-RequireFinalConnected`、`-MinimumConnectedSeconds 1800`；若要求实际连接1800秒，Client运行设1830秒，为连接建立留余量。包仍由`Prepare-LanPackage.ps1`生成。第二PC只需要完整Sandbox输出；代理仅在Host PC使用Python运行。

`analyze_lan_run.py <report.json> --expect-timeout --require-connected --minimum-connected <秒>`读取配套`.json.metrics.csv`并检查完整报告、真实连接流量、时长、内存计数、超时隐藏/恢复。输出采样收包率中位数/P05、有效的平滑RTT采样中位数/P95和资源窗口差；不跨设备减时间戳，不把平滑RTT采样分位数当作每个Ping原始RTT分位数，不自动断言泄漏或无泄漏。

示例（在工程根目录；端口与IP按场景明确选择）：

```powershell
# Host后端（真实程序，不直接对第二PC暴露代理目标）
.\tools\Run-LanCheck.ps1 -Role host -Port 17777 -Duration 180
# 独立终端，代理；断流时间从首次合法Client datagram开始
python .\tools\lan_fault_proxy.py --listen 172.20.10.4:17778 --target 127.0.0.1:17777 --allow-client 172.20.10.6 --duration 180 --seed 20261021 --loss-percent 5 --delay-ms 40 --jitter-ms 20 --blackout 20:0.2 --blackout 30:1.5 --report Logs/proxy.json
# 第二PC解压目录
.\Run-LanCheck.ps1 -Role client -Address 172.20.10.4 -Port 17778 -Duration 60 -ExpectReceiveTimeout -RequireFinalConnected
```

防火墙：此前临时规则已移除。本轮若添加，需用户授权后仅Private、来源172.20.10.6、本机172.20.10.4：Sandbox UDP17777、Python UDP17778；结束移除，不改原Public规则。即使应用收到的是代理loopback地址，也要记录真实Client来源及代理拓扑，不能冒充直接路径RTT。

## 实测记录

- EditMode7/7（`Logs/Resilience-EditMode.xml`），初次完整PlayMode14/14；修复Windows内存计数后新增真实计数回归，最终PlayMode15/15（`Logs/M3-PlayMode-Final.xml`、`Logs/Resilience-PlayMode-Memory.log`）。
- Python代理/分析器9/9通过：种子独立性、丢弃/延迟与断流边界、真实UDP往返与第二端口拒绝、队列收支/上限、已排队包在断流中丢弃、禁止远端目标、完成/连接与超时状态、零内存计数拒绝。这些不是跨PC证据。
- 首版实际NGO代理：独立0.2秒断流，代理c2s/s2c实际丢弃4/6个datagram，Client保持连接、接收超时0。另一个50秒Client经5%随机丢包、40±20ms、1.5秒断流，收到890个快照，接收超时/恢复各1，最长超时约1.095秒、最大接收年龄约1.595秒；8856次超时视觉检查全隐藏、有效姿态违例0，最终Connected。路径全部为本机loopback。
- 首版统计发现Windows Mono `Process`的working set/private属性无异常返回0，旧工具当时未拒绝，故旧脚本退出0不代表内存统计通过。这些首版数据只作为传输验证，不用于资源稳定性结论。已修复为Windows原生计数、零值拒绝及回归；同时限制无头probe60fps。早期日志和旧包`PCVR-LAN-20261021-190057-134.zip`保留为历史，未向用户交付。
- 最终三种Windows构建Succeeded/errors=0（`Logs/Resilience-WindowsBuild-Memory.log`）；最终限帧Standalone实际代理回归通过：45秒、连接44.59秒、有效内存计数（working set约197MB、private约337MB）、采样45行、错误为空；超时/恢复各1、最长约1.041秒、超时姿态/视觉违例均0、最终Connected。收包率采样中位数约17.87Hz，平滑RTT采样中位数约127.92ms/P95约149.32ms，包含双向代理延迟与进程限帧等待，不当成物理LAN基线。
- 最终代理随机丢弃c2s/s2c=41/32，1.5秒断流实际丢弃30/37，队列峰值6个datagram/1101bytes，溢出和发送错误0，完成时无待发；`Logs/Resilience-FinalLocal/proxy.json`。`rejected=1`为对端结束后UDP ICMP重置类计数，不是姿态权限违规；该计数也包括不匹配来源，不能仅凭汇总分辨来源。
- 最终包`Build/PCVR-LAN-20261021-190540-323.zip`，SHA256 `7FA58D40101883BF5A2A2DBFE50111D38BEE822869DE014C89C87AED45F91053`；已提供用户转发。源代码未提交，manifest明确标记dirty。初始包保留但未交付。本轮Unity自动迁移/构建缓存与UnityConnect改写已恢复，产品Standalone/Android XR startup仍为true。
- 本轮跨PC故障/断网/长时待执行。用户已授权两条受限临时规则，已以管理员权限添加并核对Private/程序/IP/UDP端口；本轮完成后需全部移除。
- 两PC长测已启动：Host UDP17777（最长2700秒），代理`172.20.10.4:17778 → 127.0.0.1:17777`（最长2400秒），seed20261021，5%随机丢弃、每向40±20ms、首次Client包后20秒0.2秒断流和30秒1.5秒断流。要求用户Client运行1830秒且至少连接1800秒；刚启动/周期报告的completedNormally=false不能当作满时通过。
- 已启动管理员清理守护，核对Host PID19684的程序路径后等待该进程退出（安全上限48分钟），只移除本轮两条规则并读回确认；它是本轮进程生命周期清理，不修改现有规则。当前`Logs/Resilience-FirewallWatch.txt`为Watching，**尚未移除**。测试提前结束时可提前执行清理；无论是否继续对话都有安全上限。
- 代理已收到真实来源`172.20.10.6`，首包约10:10:59 UTC；首轮跨PC注入已执行：0.2秒窗口丢弃c2s/s2c=5/4，1.5秒窗口27/33。Host早期报告约108秒连接、1676个累计快照，接收超时/恢复各1，姿态/视觉违例0，资源计数有效、无metricsError。此时长测仍运行、未达到1800秒，不算满时通过；Client早期和最终报告尚待核对。
- 用户提供Client早期报告（`MSI`，10:10:59.936 UTC启动）：135.19秒运行、134.77秒连接、2326个累计快照、RTT79样本、平滑约110.57ms；超时/恢复各1、最长1.150秒、69次超时视觉检查全隐藏、无有效姿态/视觉违例、资源计数有效、无metricsError，最终状态当时Connected。事件30.64秒超时、31.79秒恢复，与长断流阶段吻合，未观察短断流阶段超时。`completedNormally=false`符合仍在运行；最终1830秒报告待用户提供，不能提前记为30分钟通过。
- Host进程绑定临时系统唤醒请求，防止长测因当前PC自动睡眠中断；不改持久电源设置，Host退出/48分钟安全上限时释放。第二PC需操作者保持唤醒。预计Client完成约日本时间19:41:30，后续再做直连物理Wi-Fi中断/恢复。
- **跨PC 30分钟故障长测功能/超时验收通过**：用户最终`MSI/soak.json`正常完成1830.012秒、连接1829.595秒、接收32237/发送31308快照、RTT1092样本，最终Connected。超时/恢复仍各1，仅发生于30秒长断流；69次超时视觉检查全部隐藏、姿态/视觉违例均0、metricsError为空，CSV采样计数1827。最后平滑RTT约126.54ms为经故障代理的数据，不是直连基线或显示延迟。完整用户JSON存于忽略的`Logs/Resilience-FromMSI/soak.json`，原始Client CSV仍需复制回本机分析。
- Host对应长测阶段连接约1829.765秒、接收29419快照，63次超时视觉检查全隐藏、违例0，Client正常退出后玩家2→1。该Host继续留作物理断网专项，故当时完整进程报告completedNormally=false，不冒充Host已退出；保存阶段快照/CSV至`Logs/Resilience-SoakHost/`。
- 阶段代理收支：c2s收到34025、转发32242、随机丢弃1751、断流丢弃32；s2c收到36873、转发35050、随机丢弃1786、断流丢弃37；均无队列溢出/发送错误/待发。每向收支相等，配置5%为随机目标，不保证实测恰好5%。代理尚等待完整2400秒自然结束，阶段结果不篡改其completedNormally状态。
- Client资源最终working set约209.94MB、private约346.42MB，峰值209.97/346.66MB，未超过早期135秒private峰值；计数有效。仅端点/峰值不足以断言无泄漏，完整Client CSV趋势待核对。Host可先按自身时间窗口分析，不跨设备减时间戳；无头测试不代表PCVR性能。
- Host长测CSV已独立分析：按Host自身elapsed157.35–1927.12秒筛选两玩家活动，排除首次连接后60秒预热；1759行。Private约339410944–339533824 bytes，末60行中位数较首60行减少106496 bytes；working set中位数差-4247552 bytes；managed约+2048 bytes，Unity allocated约+274119 bytes。此窗口排除了初始故障注入，因此窗口内timeout行0不意味着整轮没有超时；它是资源趋势观察，不是泄漏证明。摘要在`Logs/Resilience-SoakHost/resource-window-summary.json`。
- 物理断网专项已启动另一Client直连UDP17777：Host记录新玩家出现后，elapsed2182.04秒出现第二次接收超时，2211.54秒玩家2→1；有效姿态/视觉违例仍0。暂等待操作者实际Wi-Fi开关时间、Client终场报告与恢复后手动重连，不只凭Host数据提前判完整通过。
- 代理最终自然运行满2400秒、退出0、completedNormally=true，数据包总数与阶段快照一致；queued=0、rejected=0、溢出/发送错误0，队列峰值7个datagram/864 bytes，两个断流窗口已记录实际开始/结束与分向丢弃。该进程40分钟包含等待/Client结束后的空闲，不冒充40分钟活动连接；实际Client活动仍是上述约30.5分钟。
