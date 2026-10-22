# M4a LAN 阶段收尾（2026-10-22）

本阶段功能验收结束。实现提交 `0d10e46` 已通过 [PR #9](https://github.com/maeda-lab/Spakler_Multiplayer/pull/9) 合入 main，合并提交 `083fbfd`。当前没有头显，不启动 M4b 硬件验收，也不提前实现 M5 手指协议。

## 已交付与验证

| 项目 | 实际结果 |
| --- | --- |
| 基础 LAN 与生命周期 | 两 PC Mock 同步、满房拒绝、退出/重入、Host 异常停止与重启后手动连接通过 |
| 可控链路故障 | 独立 UDP 代理有实际丢弃记录；5% 随机丢弃、双向 40±20ms 延迟、0.2/1.5 秒断流分别验证 |
| 跨 PC 长测 | Client 正常完成 1830.012 秒，实际连接 1829.595 秒、收到 32237 快照；接收超时/恢复各一次，69 次超时检查全部隐藏、姿态和显示违例 0 |
| 物理 Wi-Fi 断网 | Client/Host 分别 1770/1765 次超时检查全部隐藏、违例 0；约 30 秒连接清理，恢复后新 Client 实际连接 44.842 秒，Host 确认离开和重入 |
| 报告保护 | 独占输出路径；并发或已有报告时退出 2，旧 JSON/CSV 哈希不变；正常双进程仍通过 |
| Unity 自动验证 | EditMode 7/7、PlayMode 16/16，来自实际 Unity 6000.3.15f1 测试；不是静态检查结果 |
| 构建与工具 | 产品、MainScene 无头版、Sandbox 三种 Windows 构建均 Succeeded/errors=0；Python 测试 9/9 |
| 测试清理 | 测试 Host、故障代理已结束；临时防火墙规则已移除并复核，临时系统唤醒请求已释放 |

完整细节及失败/未执行历史见 [LAN 故障与长时验证](lan-resilience.md)。原始 Logs/Build 保留在本机忽略目录，不提交 Git。物理断网 Host 在完成客户端检查后被主动结束，不把其不完整终场报告当作自然跑满时长；重连通过新进程手动完成，当前没有自动重连功能。

## 交付包与重现

- 最新已验证的 Sandbox 包：`Build/PCVR-LAN-20261022-151701-643.zip`，SHA256 `3FE16DD338DC1561CAC929A8A00D2A3C807F572FB814C52468416CF6F9783985`；340 项清单哈希已核对。
- 此包在实现提交之前生成，manifest 的 `sourceDirty=true` 是真实历史，不改写清单或冒充从干净提交构建。它是无 XR 启动的网络测试包，不是正式 PCVR 交付程序。
- 有需要时，从明确记录的源码版本使用 `QuestLANIntegrationBuild.BuildWindowsBatch` 重新构建，再运行 `tools/Prepare-LanPackage.ps1` 冻结新包；无运行时代码变更时不要求为了文档更新重建。
- LAN 操作入口为 `tools/Run-LanCheck.ps1`；必须在 PowerShell 执行，使用唯一报告和日志名。先准备好 Client，再启动时长足够的 Host；人工网络操作使用本地终端提示，不能依据延迟到达聊天的旧 JSON 指挥实时断网。
- 既往测试 IP/网络类型不是永久配置。再次测试先确认两端 IPv4、端口和网络类型；当前没有保留临时放行规则，不修改持久防火墙或系统电源策略。

## 留待后续完成

| 项目 | 当前证据与限制 | 后续动作 |
| --- | --- | --- |
| Client 资源趋势 | 本机只有用户提供的 JSON，没有完整 Client CSV；不能推断整个 Client 运行期无内存泄漏，也不能补算逐秒收包率/RTT 分位数 | 若原文件仍在第二 PC，回收 `soak.json.metrics.csv` 及本次 `wifi-check-*-drop.json.metrics.csv`、`wifi-check-*-rejoin.json.metrics.csv`，保留与 JSON 的对应关系，再用分析器核对。缺失时标记不可回溯；仅在需要关闭资源稳定性结论时安排定向复测 |
| Host 资源观察 | 1759 行预热后活动窗口：private 首末 60 行中位数差 -106496 bytes；managed +2048、Unity allocated +274119 bytes | 仅记为该 Host 时间窗口观察，不替代 Client 趋势或全应用无泄漏结论 |
| 单端 Meta Link 生命周期 | 既往 Quest 2 / Link 基础头手及交互通过；真实 Link 中断、休眠唤醒、重新居中仍待验 | 有一套头显后，使用 Windows 产品构建，记录失效、恢复及是否需要重启应用 |
| 双 PCVR | 本阶段零头显，Mock 不能证明真实双端追踪或 PCVR 性能 | 两套 Meta Link 可用后验证双向真实头/腕、唯一硬件 Rig、远端显示、事件、退出重入及持续运行；交换 Host/Client |
| 手指同步 | Shared v1 仍只含头/手腕，协议和坐标语义未变 | M4b 稳定后协调独立 HandJointFrame、关节索引/坐标/validity/频率/版本迁移，再实现采集、传输与显示 |

CSV 未回收是资源结论的待办，不撤销已通过的连接/失效/重连功能结果；不要求为此立即重跑全部 30 分钟故障长测。RTT、插值等待和运动到显示的延迟分别描述，不跨设备直接相减本机时间戳。

## 恢复工作入口

暂时无头显时保留当前运行时代码，不新增包、修改场景或调整默认发送率/插值/超时。恢复时先处理仍可回收的 CSV，再按 [PCVR/LAN 验收](pcvr-lan-acceptance.md) 进入单端和双端 M4b。M5 的前置条件及任务状态见 [任务清单](tasks.md)。
