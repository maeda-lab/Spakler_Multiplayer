# M2 LAN 网络模块与 A 交接

2026-10-19 补充：已进入 A+B 集成。NetworkPlayerSignals 提供 owner Ping/Pong、本机时钟 RTT/平滑/超时、姿态频率以及服务器权威颜色 NetworkVariable。桌面 NetworkPlayer 与独立产品 XRNetworkPlayer 使用同一组件；Shared/姿态包 v1 不变。NGO会话配置协议升至2，避免旧Behaviour布局的构建接入；双方需更新程序。MainScene 的绑定、VR 控件及本轮验证结果见 [m3-integration](m3-integration.md)。下文 2026-10-14 的“RTT/颜色待实现”是当时状态。

2026-10-14，feature/network，Unity 6000.3.15f1、Shared v1.0。用户本轮明确授权 B 代 A 安装并锁定 NGO/Transport。实际 UPM 解析：NGO 2.11.2、Transport 2.6.0（NGO 2.11.2 声明的依赖）；manifest/lock 已记录。既有 XR 包版本未改变。API 按本机实际 PackageCache 源码核对，使用统一 Rpc 与 InvokePermission，未使用旧 ServerRpc/ClientRpc 或默认 NetworkTransform。

## 拓扑与入口

两人 LAN IPv4 client-server；Host 包含本地玩家，监听 0.0.0.0，可配置 UDP 端口，默认 7777。Client 输入 Host IPv4；127.0.0.1 仅用于本机测试。LanSessionController 提供 StartHost、StartClient、Disconnect、连接状态、失败原因、Player ID 和日志开关。连接中禁止编辑配置，启动前检查 IPv4/端口/重复启动，10 秒超时；停止后可重启。断线/停用取消事件订阅，清理批准槽位及玩家状态。

服务器连接批准检查协议版本并限制总数为 2（含 Host）；第三人得到明确 Room full 原因。Player Prefab 由 NGO 每连接自动生成一次，并分配 OwnerClientId；断线由 NGO 清理，不再叠加手工 spawn。批准时分配静态出生位置 x=-0.65/+0.65 米，朝向 identity、单位缩放，服务器原始 spawn 信息复制根的位置/旋转；不接受客户端自行移动共享世界的根。

## 资源与模块

- Assets/Prefabs/Network/NetworkSession.prefab：NetworkManager、UnityTransport、LanSessionController，外加本地测试用 MockPoseSource/SandboxPoseControls/LanSmokeProbe。
- Assets/Prefabs/Network/NetworkPlayer.prefab：NetworkObject、NetworkPoseSync、SandboxNetworkAvatar。对象本身是单位缩放 AvatarRoot；纯视觉标记在运行时生成，无 XR Rig。
- Assets/Prefabs/Network/LanNetworkPrefabs.asset：明确注册 NetworkPlayer；NGO 另自动生成 Assets/DefaultNetworkPrefabs.asset，此默认列表保留，Session 实际使用自己的明确列表。
- Assets/NetworkingSandbox/Scenes/LanSandbox.unity：独立 LAN 场景，两个 Prefab 配置一致。旧 NetworkSandbox.unity/MockAvatar.prefab 保留为本地接口预览。
- Scene/Prefab/meta 由 Unity API 生成；NetworkPlayer 持久 hash 由 NGO OnValidate 从 Unity GlobalObjectId 计算、保存并检查非零，没有手工编 GUID/hash。

Networking 只引用 Shared、Unity/NGO；不读取 XR Origin/XR Hands。Simulation 的 MockPoseSource 代码只消费 Shared/Unity 类型；本轮 Simulation 程序集还包含网络测试组件，因此程序集引用 Networking/NGO。Shared、XR、Avatar 的实现未修改。SandboxNetworkAvatar 复用 A 的 AvatarPoseView 作为消费方，不修改它。

## 权限与传输

仅本机 owner 从 IAvatarPoseSource 读取数据；BindLocalSource 拒绝在已生成的远端玩家上绑定本机源。客户端提交使用 Rpc(SendTo.Server, InvokePermission.Owner)，服务器再次核对 SenderClientId == OwnerClientId，并验证数据及顺序。转发使用 Rpc(SendTo.NotOwner, InvokePermission.Server)，接收方也核对发送者为 server。Host 的本地数据经过同一路径，因此 Host 能看到 Client、Client 能看到 Host。没有客户端可写的玩家身份字段。

默认发送频率 20Hz，可配置 1–60Hz。发送层使用连接内独立 uint 传输序号，保留原采样时间；读源的重复 Sequence 不视为新采样。源缺失/停止采样时 0.2 秒失效 heartbeat 不再冒充有效追踪；恢复采样重新发送。源重绑清理采样去重，但不倒退传输序号。全部 false 帧仍是有效状态消息。连接重建/despawn 清空接收缓存和序号状态。

NetworkAvatarPosePacket 按字段 NGO 序列化：byte Version=1、uint Sequence、double LocalCaptureTimeSeconds、3 个 bool tracking、3 个 Vector3+Quaternion。字段预计 100 字节，未测量线上 RPC/UDP 总开销；不依赖 struct 内存布局。序号为传输层序号，来源帧序号用于发送端去重。Shared 无 NGO 序列化类型。

服务器拒绝未知版本、非有限/负采样时间、有效部位的非有限位置/旋转、超过 5 米的局部位置及异常四元数；有效旋转归一化，无效部位规范化为 Pose.identity。半范围序号比较 unchecked((int)(candidate-previous)) > 0，拒绝旧帧/重复帧并支持 uint 回绕（间隔小于 2^31）。快照 RPC 为 unreliable；失效 heartbeat 与接收超时覆盖丢包后的隐藏，暂无预测/回滚或带宽压缩。

## 远端应用与坐标

NetworkPoseSync 同时实现远端 IAvatarPoseSource。PoseSnapshotBuffer 保留最多 16 个到达快照，以接收端本机单调时钟安排默认 100ms 插值，Vector3.Lerp/Quaternion.Slerp；不比较不同设备时间。最新 flag=false 立即隐藏相应部位，恢复跨越无效段时采用新有效样本，不对无效 Pose 插值。默认 0.5 秒接收超时全失效；断线/despawn/暂停清空，超过 0.5 秒到达间隔重新建立缓存。无无限外推。

头和左右手腕保持 Shared v1 的同一 AvatarRoot 相对语义，Unity 米制与 Quaternion。视觉通过 root.TransformPoint/local rotation 重建；头球、左腕 cube、右腕 capsule，左右身份不按画面左右推断。根及祖先必须单位缩放。两个 Quest 的物理 tracking origin 不自动共定位；动态根移动/重定位不在此版范围。

## A 集成方式

先在独立 LAN 场景验收，再由 A 集成到 MainScene。NetworkSession 的 Configure 接收本机唯一 XRHandsPoseSource；连接后根据 NetworkManager.LocalClient.PlayerObject 的根配置 A 的 tracking-origin 到 AvatarRoot 映射。仅 owner 将该源绑定到发送组件；开始提供有效帧前确保映射已建立。不要把硬件 Rig 放入 NetworkPlayer。

最终场景停用 MockPoseSource、SandboxPoseControls 与测试探针；远端 A 视觉的 AvatarPoseView 绑定 NetworkPoseSync，Show Head=true。本地头按 A 策略隐藏。SandboxNetworkAvatar 是临时几何视觉，可在集成时由 A 的视觉替换。Shared v1 无需改字段或有效性语义；包依赖和 Prefab 均已由 B 提供，MainScene 集成仍归 A。

## 验证与后续

真实结果见 network-testing.md。已完成 Unity 编译、6/6 PlayMode、Windows Build 与两个独立 Windows 进程的双向 Mock 同步、第三人拒绝、退出/重新加入、Host 停止后的 Client 清理，以及 Editor + Standalone 的真实画面与同实例停止/重启。Editor 安装期间消息注册表异常经 Unity CleanBuildCache 全量重编译后恢复，未修改 SDK。尚未进行双 PC LAN、Quest、Android、RTT 或变色物体测试。RTT 与变色属于下一步；RTT 不等于运动显示端到端延迟，采样时钟不可跨设备直接相减。

连接异常逐项检查 Host 是否启动、目标 IPv4/端口、同 LAN、网卡绑定、防火墙、AP Client Isolation；不把所有错误归因于防火墙。不自动修改 Windows 安全权限。
