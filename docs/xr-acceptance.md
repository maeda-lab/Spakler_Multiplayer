# M1 本地 XR 验收

本轮只推进 A1/A2；Quest 2 / Link 与 Quest 3 Android 原生分别记录。Shared v1 无变更。网络实现留在 B 的独立目录/分支，不在本轮修改或合并。

## 自动检查
使用 Unity 菜单 `QuestLAN/Run isolated XR acceptance tests`，先停止当前 Play。工具暂时仅在内存关闭 Standalone 的 Initialize XR on Startup，再运行 QuestLAN.Tests，结束后恢复原值；这样真实 Quest 数据不会混入测试。测试源码位于 Assets/Tests/PlayMode，ControlledHandProvider 使用 XR Hands 官方 Provider API，仅在测试程序集存在，不是生产输入源或 B 的 MockPoseSource。

新增四组行为检查（加上原有三组，预期共七组）：
- 两手有效后独立失去左/右手，只隐藏对应腕标记；恢复时使用新的手腕 Pose。
- Hand.isTracked 但 Wrist Pose 无效、缺少 Dynamic 更新或仅有 BeforeRender 更新时，不沿用陈旧有效姿态。
- 已有效时模拟 OnApplicationPause(true/false)、subsystem Stop/Start、组件连续五次 disable/enable；失效立即发布，恢复需新采集，订阅数恢复到一个。
- 不同旋转/平移的 Tracking Origin 与 AvatarRoot，以及 0/1.6 米 Camera Floor Offset 下，手腕相对 Pose 重建后保持世界位置/旋转；专门令 provider.rootPose 与 Wrist 不同，防止误用手根锚点。

这验证的是生命周期处理和坐标数学。SendMessage 触发的暂停回调不等于 Android OS 实际暂停，0/1.6 米偏移不等于设备实际切换 Floor/Device 已通过。

结果输出 `Logs/QuestLAN-XRAcceptance.xml` 和 `QuestLAN-Acceptance-State.txt`。若测试被中断，可菜单 `QuestLAN/Restore XR startup after interrupted tests` 恢复本轮内存设置；重新打开编辑器后也应检查磁盘上的 Initialize XR on Startup。工具没有将临时关闭值保存到工程资源。

编辑器自动化入口仅接受本地 `Logs/QuestLAN-Acceptance-Request.txt` 中的 `automated`、`hardware` 或 `reentry`，在空闲且不编译时消费；不是通用命令执行器。第一次添加工具或外部切换分支后需聚焦 Unity，让它刷新/编译。请求状态改为 accepted，避免 Domain Reload 后重复触发。

## Quest 2 / Link 分阶段遮挡
菜单 `QuestLAN/Run 90-second Link hardware probe`，保持头显佩戴、放下控制器，让手部进入摄像头视野。Play 中相机前方临时显示提示，不保存到 MainScene。

| 时间 | 操作 | 观察 |
| --- | --- | --- |
| 0–15 秒 | 双手可见 | 双手骨骼和左方块/右球腕标记均出现 |
| 15–30 秒 | 仅将左手移出追踪范围或遮挡，右手保持可见 | 左手失效/隐藏，右手继续运动 |
| 30–45 秒 | 恢复左手，两手可见 | 左手以当前姿态恢复，无可见旧位置停留 |
| 45–60 秒 | 仅遮挡右手，左手保持可见 | 右手失效/隐藏，左手继续运动 |
| 60–75 秒 | 恢复右手，两手可见 | 右手以当前姿态恢复 |
| 75–90 秒 | 双手可见，缓慢移动/旋转头部 | 画面与追踪响应正常，无本地头模型遮挡 |

约每 0.1 秒记录一次 flags、帧序号、头/左右手腕位置与旋转；失效 Pose 写 NaN，不能消费为真实值。报告只在相应遮挡/恢复阶段各观察到至少三个样本时记录独立丢失/恢复证据；不是对画面或动作意图的自动判定，仍需操作者反馈。

每次输出保存到 `Logs/QuestLAN-LinkRuns/<run-id>/frames.csv` 与 summary.txt；固定名称的 LinkProbe.csv/Summary 指向最近一次。若某手整个阶段都无有效基线或没有恢复证据，记录未完成，不把两手同时失效当成单手独立失效。

## 重入与真实暂停
真实 Link 验收完成后，再次进入/退出 Play，检查双手仍能出现和事件不重复；自动测试中的五次组件启停不能单独替代这一项。暂停/恢复的 OS 回调需要在相应平台实际触发；Unity 编辑器 Pause 按钮、摘下头显引起的 focus/session 变化与 OnApplicationPause 不是同一事件。

提供 `QuestLAN/Run 30-second Link reentry probe` 菜单：前一轮 Play 完整结束后重进 MainScene，保持双手可见 30 秒；该模式不要求遮挡，报告中 Staged occlusion test=false，独立遮挡 evidence=false 不代表重入失败。

## 本轮结果
- Unity 编译及隔离 PlayMode 自动测试已实际执行：**7/7 Passed，failed=0，skipped=0**。结果文件 `Logs/QuestLAN-XRAcceptance.xml`。真实 Link 采样随后成功启动，头和双手均出现有效基线，证明隔离测试后硬件 XR 已恢复。
- **Quest 2 / Link 分阶段遮挡通过**：90 秒，807 个可用采样；头部有效 798 次，左手 703 次，右手 674 次。左手阶段记录 81 个“左失效/右有效”样本，随后 120 个双手恢复样本；右手阶段记录 116 个“右失效/左有效”样本，随后 126 个双手恢复样本。操作者确认提示、对应骨骼与腕标记隐藏、另一手更新及恢复位置“一切正常”。证据归档 `Logs/QuestLAN-LinkRuns/deacb4c488c94c1784afd57680254eb9/`。
- **Quest 2 / Link 真实 Play 重入通过**：前一轮 Play 结束后重新进入 MainScene，30 秒、269 个可用采样，头部有效 261 次，左右手各有效 267 次；操作者确认显示“一切正常”。该次不要求遮挡，不用遮挡 evidence 字段评价重入。归档 `Logs/QuestLAN-LinkRuns/041dfe8756a34c6cbb2dc4be3f2f2756/`。
- 工作期间目录被外部切到 feature/network；按用户确认已切回 codex/xr-hands-m0，B 使用另一目录。本轮未改写 B 的文件或依赖。
- Quest 3 不在当前可用设备中，Android 原生 A3 仍待验收。
- 实际 OS 暂停恢复和设备端 Floor/Device 切换仍待设备验证。本轮没有修改生产 XR/Avatar 采集显示逻辑、Shared 契约、场景或包依赖；增加的是测试和 Editor 验收工具。
