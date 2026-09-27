# PocketStriker

Unity 战斗游戏，使用内嵌的 MCombatShared 包。项目编辑器版本为 **6000.4.0f1**，
无需另一个 MCombat 项目目录即可打开。共享包固定在
`5e962289405dd8a4078538350915e444fad1e4d4`（2026-09-14）；上游仍使用
`0.1.20` 版本号，因此以提交和文件哈希为准。

## 在编辑器中运行

1. 使用 `ProjectSettings/ProjectVersion.txt` 指定的 Unity 版本打开项目。
2. Addressables 的 Play Mode Script 选择 **Use Asset Database**，使用本地资源。
3. 打开 `Assets/Scene/ABLoadScene/Scene1.unity` 并进入 Play Mode。
4. 启动会加载配置并进入标题／演示战斗。登录后的在线功能使用项目现有的 PlayFab 配置。

默认移动键为 WASD；移动端使用项目已有的触控界面。
不要直接从 FightScene 启动，战斗依赖启动场景初始化的配置和资源索引。

## 验证

关闭此项目的 Unity 编辑器后，在项目根目录执行：

```sh
Tools/validate_unity.sh check
Tools/validate_unity.sh startup
Tools/validate_unity.sh compile
Tools/validate_unity.sh build
Tools/validate_player.sh
python3 Tools/Validation/validate_cloudscript_contracts.py
python3 Tools/Validation/test_shared_sync.py
```

- `check`：编译编辑器脚本，检查构建场景、Addressables 与所有预制体的缺失脚本。
- `startup`：用本地资源进入演示战斗，验证双队伍和标题 UI 连续稳定运行 20 秒。
  测试不执行登录或购买，结束后恢复 Addressables 的 Play Mode Script。
- `compile`：单独编译 macOS 玩家脚本，发现误用 UnityEditor 等编辑器之外的问题。
- `build`：生成 `Builds/Revival/PocketStriker.app`，临时把远程 Addressables 也打入
  本地开发包，结束后恢复原发布配置，不上传资源。
- `validate_player.sh`：独立运行开发包，验证包内资源、双队伍战斗和标题界面。
  仅测试开关会允许后台运行，并在完成或失败后退出。

日志和 JSON 检查结果写入 `Logs/Revival/`。如 Unity 未安装在默认的 macOS Hub
目录，可设置 `UNITY_EDITOR_PATH` 为编辑器可执行文件路径。

macOS 开发包用于本地验证。iOS/Android 发布需要对应 Unity 构建模块、签名和真机测试；
真实登录、广告、内购与线上 CloudScript 需在相应服务环境中验证。

2026-09-27 的恢复验证已通过：3 张构建场景、493 个 Addressables 条目与预制体脚本检查；
编辑器和独立 macOS 包的双队伍演示战斗各连续运行 20 秒；玩家脚本编译及完整本地打包；
14 项 CloudScript 契约检查和 3 项共享包同步保护测试。
已修复移动端广告初始化误入桌面平台的问题，并移除 Addressables 已不支持的旧虚拟构建器。

## 更新共享代码

参见 [来源、迁移和更新流程](Tools/MCombatShared.md)。共享包保持上游原样，项目差异
放在 `Assets` 适配层中。使用 `Tools/sync_mcombat_shared.py` 预览固定提交的更新；
不要直接运行上游面向多个项目的批量 SourceSync 脚本。
