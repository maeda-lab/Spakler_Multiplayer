# QuestLANPrototype

Windows PC + Meta Link 双人局域网 VR 架构验证工程（工作目录名 Spakler_Multiplayer）。第一阶段同步头部、左右手腕、追踪有效性，再扩展手指；提供连接状态、RTT 与同步变色测试物体。

固定 Unity **6000.3.15f1**，Universal 3D / URP、OpenXR、XR Hands、Input System、XRI。最终交付 Windows x64 程序，通过 Meta Link 运行，不要求 Quest Android 原生打包。NGO 2.11.2 / Unity Transport 2.6.0 与模拟输入已由 B 提供，桌面双进程原型已有验收记录。

## 当前状态

2026-10-28 当前功能分支新增桌面大厅（本机IPv4自动填入/手工Host地址）、连接后手动启动VR、左摇杆头向平移、地面参照和独立双手26关节骨架同步。会话协议4，Host/Client需同版。具体实现与本轮真实/合成验证边界见 [桌面大厅与详细双手](docs/desktop-lobby-locomotion.md)。尚未提交合并的功能不视为main已具备。

2026-10-22，M4a LAN 功能阶段已收尾，代码经 [PR #9](https://github.com/maeda-lab/Spakler_Multiplayer/pull/9) 合入 main：跨 PC 30 分钟故障长测、物理 Wi-Fi 断网清理和手动重连通过，测试进程及临时规则已清理。Client 完整 CSV 资源趋势尚未核对，双头显 M4b 待设备；此为2026-10-22阶段记录；2026-10-28用户追加要求后，当前功能分支已进入详细手关节同步。验收边界、交付包和恢复工作入口见 [M4a 阶段收尾](docs/m4a-closeout.md)。

以下为既往集成与设备结果。
M0 工程与 Shared v1 已完成；MainScene 已接入真实源/网络出生点映射、远端视觉、RTT、权威变色及头部指向面板，独立 LanSandbox 提供无硬件验证。2026-10-19 M3 的桌面loopback、Host重启与Quest 2 / Link实际变色已通过；2026-10-21真实双PC Sandbox的双向同步、满房、退出重入、Host异常停机清理与重启后手动连接通过，双头显PCVR仍待验收。实现与历史证据见 [M3 集成记录](docs/m3-integration.md)，最新跨PC结果见 [PCVR/LAN验收](docs/pcvr-lan-acceptance.md)。Meta SDK / Passthrough / 抓取尚未安装配置。

后续已完成双平台 OpenXR 设置与 MainScene 官方骨骼接入，历史 Android Development APK 构建成功（不再属于交付要求）。[XR 专项验收](docs/xr-acceptance.md)的 7/7 自动测试通过；Quest 2 / Link 的双手骨骼/腕标记、左右手独立遮挡恢复及真实 Play 重入均通过数据与操作者确认。实际系统暂停恢复、Link 中断重连、重新居中和双头显 PCVR 仍待验收；双 PC Mock LAN 结果见上述收尾记录。下一阶段操作以 [PCVR/LAN 验收](docs/pcvr-lan-acceptance.md) 为准；本轮可用两台 PC、零头显。

## 头部 Avatar 与连接按钮

MainScene 新增蓝色球体头部与两颗朝向眼睛，并提供英文 Host / Join / Disconnect / Show Status。桌面支持鼠标键盘，VR 使用白点凝视和数字键盘；Client 输入 Host IPv4 加入，Host 断开关闭房间。实现及无头显验证边界见 [头部与连接 UI](docs/head-avatar-session-ui.md)。本机头部从第一人称相机排除，远端显示头部；失追踪仍隐藏。

## 桌面大厅与移动

先用鼠标/键盘在 DesktopLobby 选择 Create Host 或输入 Host IPv4/Port 后 Join，连接成功后才启动 Meta Link/OpenXR 并进入 MainScene。左摇杆按头部水平朝向前后移动/侧移，本机和远端显示简化手模型；无手部追踪时支持真实控制器近似手腕。使用方式、边界和同版本要求见 [桌面大厅与移动](docs/desktop-lobby-locomotion.md)。

## 启动
Unity Hub 使用上述精确版本打开仓库根目录（Assets、Packages、ProjectSettings 同级），等待包解析和脚本编译。活动目标使用 Windows x64；先进入 Meta Link PCVR 会话，再打开 DesktopLobby 按 Play 或运行 Windows 产品，通过桌面按钮建立/加入房间后进入 VR。直接对 MainScene 按 Play 也先显示桌面菜单。首次搭建按 [setup](docs/setup.md) 顺序进行；无需云服务账户或 Android 工具链。无头显时使用独立 Sandbox。

## 文档
- [协作规则](AGENTS.md)
- [架构](docs/architecture.md)
- [共享接口 v1](docs/interface-contract.md) — B 开发依据
- [环境与依赖](docs/setup.md)
- [任务与交接](docs/tasks.md)
- [验证步骤与实测记录](docs/testing.md)
- [网络实现与交接](docs/networking.md)
- [网络历史验收](docs/network-testing.md)
- [M3 集成与验收](docs/m3-integration.md)
- [PCVR/LAN 验收与跨机命令](docs/pcvr-lan-acceptance.md)
- [LAN 故障注入与长时统计](docs/lan-resilience.md)
- [M4a 阶段收尾与待办](docs/m4a-closeout.md)
