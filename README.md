# QuestLANPrototype

Meta Quest 3 双人局域网 VR 架构验证工程（工作目录名 Spakler_Multiplayer）。第一阶段同步头部、左右手腕、追踪有效性，再扩展手指；提供连接状态、RTT 与同步变色测试物体。

固定 Unity **6000.3.15f1**，Universal 3D / URP、OpenXR、XR Hands、Input System、XRI。最终 Android；Quest Link/Air Link 开发调试。NGO/Unity Transport 与模拟输入由 B 开发，当前未实现联机。

## 当前状态
已有 URP 模板 SampleScene；M0 结构、Shared v1 与协作文档已建立。M1 以本地手腕采集与独立 Avatar 显示为边界；依赖解析、编译和测试的实测记录见 [testing](docs/testing.md)。硬件追踪、Android APK 与双机 LAN 均不能从编译结果推定通过。Meta SDK / Passthrough / 抓取尚未安装配置。

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
