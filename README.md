# QuestLANPrototype

Meta Quest 3 双人局域网 VR 架构验证工程（工作目录名 Spakler_Multiplayer）。第一阶段同步头部、左右手腕、追踪有效性，再扩展手指；提供连接状态、RTT 与同步变色测试物体。

固定 Unity **6000.3.15f1**，Universal 3D / URP、OpenXR、XR Hands、Input System、XRI。最终 Android；Quest Link/Air Link 开发调试。NGO 2.11.2 / Unity Transport 2.6.0 与模拟输入已由 B 提供，桌面双进程原型已有验收记录。

## 当前状态
M0 工程与 Shared v1 已完成；MainScene 已接入真实源/网络出生点映射、远端视觉、RTT、权威变色及头部指向面板，独立 LanSandbox 提供无硬件验证。2026-10-19 M3 的桌面loopback、Host重启与Quest 2 / Link实际变色已通过；Quest 3原生与双头显LAN仍待验收。实现、构建和证据见 [M3 集成记录](docs/m3-integration.md)。Meta SDK / Passthrough / 抓取尚未安装配置。

后续已完成双平台 OpenXR 设置与 MainScene 官方骨骼接入，Android Development APK 构建成功。[XR 专项验收](docs/xr-acceptance.md)的 7/7 自动测试通过；Quest 2 / Link 的双手骨骼/腕标记、左右手独立遮挡恢复及真实 Play 重入均通过数据与操作者确认。实际 OS 暂停恢复、设备端 Floor/Device 切换、Quest 3 原生和双机 LAN 仍待验收。

## 启动
Unity Hub 使用上述精确版本打开仓库根目录（Assets、Packages、ProjectSettings 同级），等待包解析和脚本编译。首次搭建按 [setup](docs/setup.md) 顺序进行；无需云服务账户。Android 模块已在本机 Editor 路径发现，实际 SDK/JDK/设备授权仍须验证。

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
