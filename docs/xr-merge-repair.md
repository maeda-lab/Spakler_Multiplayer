# 网络更新后的 XR / 场景配置修复

## 故障与原因
2026-10-14，在网络合并提交 `11653a4` 上报告 Play 仅见天空盒、没有进入 VR。MainScene 相比此前可用的 XR 提交未被改写；检查发现以下配置损坏：

- `ProjectSettings/ProjectSettings.asset` 的 m_BuildTargetBatching 下残留单独的 `feature/network` 行，不是合法配置项。故障 Editor 日志实际出现 `Invalid value of 'activeInputHandler' setting: -1`。
- `ProjectSettings/EditorBuildSettings.asset` 同样残留该文本，并把 `com.unity.xr.management.loader_settings` 指向 `Assets/XR/XRGeneralSettingsPerBuildTarget.asset`。这个新资源的 Keys/Values 为空，没有任何平台 manager/loader。
- `Assets/XR/Settings/OpenXR Editor Settings.asset.meta` 的文件头也残留该文本，破坏资源元数据格式。

这些合并残留和空设置绑定使 Unity 无法可靠恢复输入与 XR 启动配置。仅确认包已安装、或在 Inspector 中看到原有配置资源，并不能证明编辑器实际使用了该资源。

## 修复范围
- 清除三处无效合并残留；保留网络分支合法的 Standalone batching 设置。
- 将 XR 注册恢复为既有 `Assets/XR/Settings/QuestLAN XR General Settings.asset`，其 Standalone/Android 两平台均包含 OpenXR loader、启动配置和手追踪功能。
- 保留 OpenXR Editor Settings 当前真实 GUID，不生成替代 GUID；空的默认 XR 资源仍保留在仓库，注册入口不再使用它。
- `QuestLANXRConfiguration.ConfigureBatch` 优先绑定本工程的既有配置资源；`ValidateBatch` 显式检查该绑定，避免以后把空默认资源当作已配置工程。
- 保留 NGO、Transport、网络脚本和场景内容；本轮不重建场景、不删除网络实现、不升级包。

修复分支基于发生故障的网络合并提交，而不是退回无网络的 XR 分支。排查期间曾发生外部切分支，随后明确固定在 `codex/fix-network-xr-settings`。

## 回归验证
固定 Unity 6000.3.15f1，EditMode `QuestLAN.EditorTests` **4/4 Passed，failed=0**，覆盖：
1. 关键配置不包含本次合并残留或标准冲突标记。
2. PlayerSettings 可由 Unity 正常反序列化，Input System 配置启用。
3. 注册入口确为既有资源，双平台 OpenXR/Hands/Android Meta Quest Support 仍存在。
4. Unity API 实际加载 MainScene，存在唯一 XR Origin、TrackedPoseDriver、XRHandsPoseSource、地面 Renderer，且无缺失脚本。

首次新增测试因缺少 Meta Quest 功能程序集引用未通过编译，补齐官方包实际程序集引用后重新运行通过。日志 `Logs/QuestLAN-MergeFixTests-Final.log` / XML，均忽略不提交。

Quest 2 / Link 修复后实测：OpenXR 日志识别 Runtime `Oculus`、System `Oculus Quest2`；首次约 12.3 秒探测中 47 个样本、44 个有效头部样本，头部位置持续变化。该段在 Play 停止时结束，双手有效计数为 0，不把它作为自动手追踪通过证据。后续再次进入 Play 后，操作者对“进入 VR、地面/头部运动、双手骨骼/腕标记是否恢复”的反馈为“一切正常”。

本轮新日志未再出现 activeInputHandler=-1 或项目脚本编译错误。没有重新执行 Android 原生部署、双机 LAN 或长期手追踪验收；这些不能从本次 EditMode/Link 结果推定通过。

## 合并后检查
保留配置文件的合法 YAML 结构及 .meta 的 fileFormatVersion/guid；不要把分支名称当作冲突解决结果保留。确认 EditorBuildSettings 中 XR 注册指向既有已配置资源，然后运行上述四项测试。XR 原型使用 MainScene；B 的非 VR 沙盒应独立验证。
