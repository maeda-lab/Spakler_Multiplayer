# QuestLANPrototype 协作约定

## 目标与环境
Windows PC + Meta Link 双人 LAN VR 原型：先本地头部/手腕追踪，再 NGO LAN 同步，最后手指关节。固定 **Unity 6000.3.15f1**，URP、Input System、XR Plugin Management、OpenXR、XRI、XR Hands。最终交付 Windows x64 程序，通过 Meta Link 运行；无需 Quest Android 原生打包或部署。已有 Android 配置和历史结果保留，不作为交付门槛。未来 Meta Interaction SDK 抓取；不复制旧线香花火工程，不增加 Relay/Lobby、全身 IK、传感器或语音。

## 分工与所有权
- A：`Assets/Scenes`（包括未来 MainScene）、`Scripts/XR`、`Scripts/Avatar`、`Prefabs/Visual`、XR/初始化 Editor 工具、Packages、ProjectSettings、相关 docs。
- B：`Scripts/Networking`、`Scripts/Diagnostics/Network`、`Scripts/Simulation`、`Prefabs/Network`；以独立 Prefab 交给 A 集成。未进入集成阶段不得改写 B 文件。
- Shared 首次由 A 建立，此后为双方稳定契约。修改需更新 `docs/interface-contract.md` 的版本/迁移记录并通知 B；不得擅自更名、改坐标或改有效性语义。
- 同时只允许一人修改同一个 Scene/Prefab、manifest 或同一个 ProjectSettings 文件。包与场景变更先协调。不要自行发送外部消息。

## 架构约束
Shared 只依赖 UnityEngine 基础类型，禁止 XRHand/XRHandJoint/XRHandSubsystem 或任何 NGO 类型。LocalXR 只采集并实现 IAvatarPoseSource，不发送网络；Networking 消费接口，不读取 XR Hands；Avatar 只消费姿态，与传输无关。每设备一个 Local XR Rig，远端绝不创建硬件 Rig。头/左右手腕 Pose 均相对单位缩放 AvatarRoot，左右手身份来自 XR Hands。缺失、停机、暂停、失追踪必须失效；取消事件订阅，支持 Play Mode 重入。时间戳是本机单调时钟，不能当跨设备单向延迟。RTT 不等于运动显示端到端延迟。

## 工程与 Git
工程根目录就是仓库根目录；Visible Meta Files、Force Text。提交 Assets（含 Unity 生成的 .meta）、Packages/manifest.json、packages-lock.json、ProjectSettings、docs；不提交 Library/Temp/Obj/Logs/Build/UserSettings。保留非本任务改动，不伪造 Scene/Prefab/GUID 或 SDK 引用。
使用功能分支（建议 `feature/xr-hands` / `feature/network`；自动创建分支默认 `codex/`），PR 合并；main 必须记录真实验证状态。未经明确许可不 push/merge/reset --hard/rebase、删仓库文件或改远端分支。不自动提交。

每次结束检查 git status，列修改文件、目的/影响、实际测试、未验证项、建议 commit。禁止以静态检查代替 Unity 编译通过，禁止声称连接过未测试的 Quest。执行范围以用户当次任务为准。
