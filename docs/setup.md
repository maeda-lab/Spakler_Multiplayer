# 环境配置（2026-10-14）

## 工程检查
工程已由 Unity Hub 创建，URP 模板，不需要重建。`ProjectSettings/ProjectVersion.txt` = `6000.3.15f1 (c1aa84e375f6)`。本机 Editor 位于 `D:/Program Files/UnityHub/Editor/6000.3.15f1-x86_64/Editor/Unity.exe`；不要使用旁边的 6000.3.16f1 或 2022.3。AndroidPlayer 模块目录存在，但未运行 Android Build。序列化已是 Force Text，版本控制已是 Visible Meta Files。

原内层 `Spakler_Multiplayer/.git` 已经用户同意移至工程根目录，保留 main 历史与 origin：`https://github.com/maeda-lab/Spakler_Multiplayer.git`。原仓库初始提交 `03e153a`。本次工作分支 `codex/xr-hands-m0`；未 push、merge 或提交。本环境 Git 对目录所有权有疑义；检查使用仅命令级 `git -c safe.directory=D:/Unity/Spakler_Multiplayer status`，未修改全局 Git 配置。若你的常用账户也出现此错误，先核对目录所有权，再为确切项目目录设置信任，不要信任整块 D: 盘。

## 包版本
以下来自实际 manifest / packages-lock / UPM 日志；四个 XR 版本来自**此精确编辑器随附 Package Manager 元数据**，不是猜测或按旧教程选择。最终编译/测试结果另见 testing。

| Package | 实际直接依赖版本 | 状态 |
| --- | --- | --- |
| com.unity.render-pipelines.universal | 17.3.0 | 原模板 |
| com.unity.inputsystem | 1.19.0 | 原模板 |
| com.unity.test-framework | 1.6.0 | 原模板 |
| com.unity.xr.management | 4.5.4 | UPM 实际解析 |
| com.unity.xr.openxr | 1.16.1 | UPM 实际解析 |
| com.unity.xr.hands | 1.7.3 | UPM 实际解析 |
| com.unity.xr.interaction.toolkit | 3.3.1 | UPM 实际解析 |

其他全部解析版本以 `Packages/packages-lock.json` 为准；保留模板现有包，不擅自删除。NGO / Unity Transport 未安装，由 B 与 A 协调 manifest 后安装。Multiplayer Center 是模板包，不代表已实现 NGO。

Meta 目标清单暂未安装或验证：meta-openxr 2.5.0、Meta SDK all 203.0.2 / core、interaction、interaction.ovr 203.0.0、AR Foundation 6.5.0、Composition Layers 2.4.0。这些是用户期望，**不是本次实际解析版本**。最小头/手腕追踪无需抓取或 Passthrough；后续按 Meta 官方发布说明、UPM 解析及编译结果核实，尤其 OVR backend 与 OpenXR/XR Hands 共存不能推定可用。不要同时启用竞争的 Oculus/OpenXR loader 或创建 OVR 与 XR Origin 双 Rig。

官方参考：[Unity 6000.3 XR 包](https://docs.unity.com/en-us/engine/6000.3/manual/xr/support/support-packages)、[XR Hands 1.7](https://docs.unity3d.com/Packages/com.unity.xr.hands@1.7/manual/index.html)、[OpenXR 1.16](https://docs.unity3d.com/Packages/com.unity.xr.openxr@1.16/manual/index.html)、[XRI 3.3](https://docs.unity3d.com/Packages/com.unity.xr.interaction.toolkit@3.3/manual/index.html)。优先读当前包 Documentation 和 Samples。2023 年[旧路线文章](https://donabenabe.hatenablog.com/entry/TryXRHands)只作背景，不作为当前 API 来源。

## Unity Editor 配置顺序
2026-10-14 后续配置已通过 Unity 编辑器 API 完成：Standalone/Android 各自注册 OpenXR loader、Initialize XR on Startup、Hand Tracking Subsystem、Oculus Touch Controller Profile；Android 启用 Meta Quest Support，设置 IL2CPP/ARM64、Vulkan、包名 `com.maedalab.questlanprototype`。MainScene 的 Camera Floor Offset 下已接入官方 HandVisualizer，复用样例资产引用，只有一个 XR Origin；左右手腕标记继续保留作为接口诊断。下列步骤现在主要用于检查和实机验收。

可复现工具：`Assets/Editor/QuestLANXRConfiguration.cs`；菜单 `QuestLAN/Configure OpenXR platforms`。仅缺失时创建 XR 管理设置，不覆盖既有管理器；发现竞争 loader 会停止并报错。Project Validation 必须在相应平台活动时执行：OpenXR 1.16.1 的 Meta 规则在跨平台检查时会空引用，所以未通过反射或修改包代码绕过。独立报告位于忽略的 Logs 目录。

1. Hub 打开工程根目录，确认 6000.3.15f1，等待 UPM/编译完成，查看 Console；不要把包安装成功当作设备验证成功。
2. Edit > Project Settings > Player：Active Input Handling 使用 Input System Package (New)，若弹出重启按提示执行；确认 URP 管线仍有效。
3. Edit > Project Settings > XR Plug-in Management，在 Standalone 与 Android 页分别选择 **OpenXR**，启用 Initialize XR on Startup。每个目标分别配置，不用一个平台设置代替另一个。
4. XR Plug-in Management > OpenXR：Android 的 Feature Groups/Features 开启当前插件提供的 **Meta Quest Support**，启用 **Hand Tracking**；Standalone 也开启 Hand Tracking。若 Project Validation 要求交互 profile，添加当前 Meta Quest 控制器 profile（如 Oculus Touch Controller Profile）；控制器 profile 本身不启用手指追踪。按安装包实际名称查找，不启用仅针对其他厂商的功能。
5. XR Plug-in Management > Project Validation，分别选 Android / Standalone，处理每项 Errors；逐条查看 Fix，不盲目启用额外 SDK。把仍存在的 warnings 及理由记录到 testing。
6. Package Manager > XR Hands > Samples > HandVisualizer：本次已通过官方 Sample.Import 导入；若未导入或新机器状态不同，点 Import。先打开导入目录下 HandVisualizer.unity 运行官方样例确认 provider 与双手；这一步不需要复制其代码。样例不是网络场景。
7. 打开 `Assets/Scenes/MainScene.unity`：Local XR Rig 含 XROrigin、Camera Floor Offset、XR Camera（Input System TrackedPoseDriver）；XRHandsPoseSource 的三个引用已由 Unity API 配置。Local AvatarRoot 含显示组件与独立头/左右手腕几何体；本地头隐藏，左手方块、右手球，便于区分。不要把相机或 Rig 赋给 visual 引用。场景默认 Floor 模式，仍须设备验证；如切换 Device，检查 XROrigin 的高度偏移及 Camera Floor Offset 引用。
8. 本次已把官方 HandVisualizer 的当前组件配置复制到 MainScene 的同一 Camera Floor Offset，使用官方手网格与调试资产；未复制样例第二个 Rig。设备上核对骨骼显示与手腕标记一致，勿以资源接入推定实际手追踪已通过。B 不修改此场景。

## Link / Air Link 验证
1. Quest 3 开启开发者模式及系统手部追踪，完成设备账户/开发组织要求；按 Meta 当前界面启用，不假设已完成。
2. Windows 安装/运行 Meta Quest Link，连接 Link 或 Air Link 并进入 PCVR 会话，将 Meta Quest Link 设为活动 OpenXR runtime。
3. Unity 使用 Standalone 目标，先运行官方 HandVisualizer，再 MainScene。观察头/左/右标记及 flags；Link runtime 是否暴露手追踪需实测，缺少手追踪时不得用控制器或假手数据冒充。
4. 若 Link 没有提供 XR Hands，记录 subsystem/validity，转 Android 原生路径验证，不能据此认定代码追踪成功。

## Android / Quest 原生
1. Hub 为 6000.3.15f1 安装 Android Build Support、SDK/NDK Tools、OpenJDK；本机模块目录已存在，检查 Preferences > External Tools 的实际路径。
2. File > Build Profiles > Android，创建/选择 Android Profile 并 Switch Platform；在 Player 设置唯一 package identifier、IL2CPP、ARM64。最低/目标 API 与图形 API 按当前 OpenXR/Quest Project Validation 要求设置并记录，避免猜测 SDK 数字。确认 MainScene 在该 Profile 的 Scene List；本次默认 EditorBuildSettings 已切到 MainScene，自定义 Profile 仍需检查。
3. 重新验证 Android OpenXR 与 Hand Tracking 功能。在 Quest 开发者模式允许 USB debugging，设备出现在 Build And Run 的设备列表。
4. Development Build 部署；Console/Android Logcat 检查 loader 与 XRHandSubsystem，执行 testing 硬件清单。保存设备固件、runtime、包版本、测试日期及结果。
5. APK、Build 缓存和设备日志不提交；只提交必要配置与验证摘要。

## 连接设备后的执行
本机 Windows 活动 OpenXR runtime 已读取为 Meta Quest Link：`D:/Meta Horizon/Support/oculus-runtime/oculus_openxr_64.json`；仍须头显进入有效 Link 会话。2026-10-14 本次 ADB 初次检查设备列表为空。

Android 构建工具：固定版本 Editor 以 `-buildTarget Android -executeMethod QuestLAN.Editor.QuestLANXRConfiguration.BuildAndroidBatch` 运行；生成 `Builds/Android/QuestLANPrototype.apk`（Development，默认忽略）。它先调用安装包正式 validation，错误未解决会拒绝构建。设置是否支持真实 Quest 与构建结果分别记录。

本次已成功构建上述 APK，aapt 验证包名/ARM64/手追踪声明；minSdk=25、targetSdk=36 是最终包实测值，不是对 Meta 商店发布要求的承诺。未安装到真实设备。

USB 接入并在头显确认允许调试后，用 Editor 自带 `SDK/platform-tools/adb.exe devices -l` 检查状态；必须为 `device`，`unauthorized` 需在头显授权。APK 成功后可 `adb install -r <apk>`，再在 Quest 中启动应用，按 testing 表逐项验证。若开发者手动切换到官方 HandVisualizer 场景构建，须先检查 Build Profile Scene List，不能误以为默认 MainScene APK 就是官方样例 APK。

## Quest 2 Link 辅助 PCVR 验证
可先用现有 Quest 2 的 Link 会话检查 PCVR 启动与头部追踪，结果单独标为 Quest 2 / PCVR。Link 手追踪是否暴露给 XR Hands 要实测；没有有效手帧不能直接推断为代码错误，也不能宣称原生 Quest 3 已通过。

头显进入 PCVR 后，Unity 切 Windows x64，菜单 `QuestLAN/Run 90-second Link hardware probe`。工具通过 Unity API 进入 MainScene Play，约每 0.25 秒记录一次真实源帧，90 秒后停止 Play 并输出 CSV/摘要；可手动提前停止。依次观察环境随头移动、左右手骨骼和腕标记、单手遮挡恢复；期间让头显保持佩戴并适当移动。设备型号和画面正确性由操作者确认，计数只能证明收到了相应有效帧。Logs 被忽略，不提交原始本地姿态日志。
