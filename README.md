# PocketStriker

Unity 竖屏战斗游戏，通过 Git 子模块和 UPM 导入 MCombatShared。项目固定使用 **Unity 6000.5.1f1**，
默认目标平台为 **iOS**，屏幕方向为 **Portrait**。无需另一个 MCombat 项目目录即可打开。
共享包固定在 `5e962289405dd8a4078538350915e444fad1e4d4`（2026-09-14）；上游仍使用
`0.1.20` 版本号，因此以提交和文件哈希为准。

## 在编辑器中运行

1. 克隆后先运行 `git submodule update --init --recursive`，获取固定提交的共享包。
2. 安装 Unity 6000.5.1f1 和 iOS Build Support，在项目根目录运行 `Tools/open_unity.sh`。
   也可从 Unity Hub 打开：项目会在本次编辑器会话首次初始化时切换到 iOS。
3. Addressables 的 Play Mode Script 选择 **Use Asset Database**，使用本地资源。
4. 打开 `Assets/Scene/ABLoadScene/Scene1.unity`，Game 视图选择竖屏比例后进入 Play Mode。
5. 启动会加载配置并进入标题／演示战斗。登录后的在线功能使用项目现有的 PlayFab 配置。

默认移动键为 WASD；移动端使用项目已有的触控界面。
不要直接从 FightScene 启动，战斗依赖启动场景初始化的配置和资源索引。

Unity 的当前目标平台保存在本机缓存中，因此项目提供了启动脚本和首次会话默认平台逻辑。
显式传入 `-buildTarget`／`-activeBuildProfile`、批处理任务及会话中的手动平台切换都会保留。
需要桌面调试时使用 `Tools/open_unity.sh mac`；普通项目打开仍默认 iOS。

## iOS 和竖屏验证

关闭此项目的 Unity 编辑器后，在项目根目录执行：

```sh
Tools/validate_unity.sh check
Tools/validate_unity.sh startup
Tools/validate_unity.sh compile
Tools/validate_unity.sh build
python3 Tools/Validation/validate_cloudscript_contracts.py
python3 Tools/Validation/validate_dependency_upgrade.py
python3 Tools/Validation/validate_runtime_loading.py
python3 Tools/Validation/validate_shop_loading.py
python3 Tools/Validation/validate_upgrade_regressions.py
python3 Tools/Validation/test_shared_sync.py
```

以上 Unity 命令默认使用 iOS 目标和 `ProjectVersion.txt` 指定的编辑器版本。
需要打开并准备交互编辑器时，可在 Unity 启动参数中使用
`-buildTarget iOS -executeMethod PocketStrikerValidation.PrepareEditor`：打开启动场景并设定竖屏 Game
视图后保持停止状态，按 Play 开始运行。540 × 960 是请求的默认尺寸，编辑器布局和显示缩放
可能影响实际 Game 视口；验证报告记录实际尺寸并要求宽小于高。

- `check`：编译编辑器脚本，检查竖屏设置、资源版本同步、构建场景、Addressables 与所有预制体的缺失脚本。
- `startup`：用本地资源和请求为 **540 × 960** 的竖屏 Game 视图进入演示战斗，验证双队伍和标题 UI
  连续稳定运行 20 秒，再重新加载战斗并再次运行 20 秒；记录每次队伍人数和帧数，
  检查对象池重复归还／再次租用及实际视口仍为竖屏。测试不登录或购买，结束后恢复 Addressables
  的 Play Mode Script。Game 视图保留 `PocketStriker Portrait` 分辨率，便于继续调试。
- `compile`：编译 **iOS 玩家脚本**，检查编辑器以外及 iOS 条件编译代码。
- `build`：导出 `Builds/Revival/iOS` 中的 **未签名 Xcode 工程**，临时把远程 Addressables
  也打入本地验证资源，结束后恢复原发布配置。检查导出的 Info.plist 只支持 Portrait，并在
  导出工程中关闭代码签名；同时校验 Apple 登录 entitlement、原生框架，并将构建后处理
  产生的 Error／Exception／Assert 视为失败。此步骤不执行 Xcode 编译、归档或上传，
  也不修改项目签名配置。

导出及 CocoaPods 解析完成后，可继续用 Xcode 编译未签名的 iOS 原生包：

```sh
Tools/validate_ios_native.sh
python3 Tools/Validation/validate_ios_pods.py
```

该命令使用本机 Xcode 的 iPhoneOS SDK，日志为 `Logs/Revival/native-ios.log`，
产物位于 `Library/RevivalXcode`；不安装到设备或上传。

编辑器的启动验证可以检查 iOS 条件编译下的场景和资源；Xcode 导出验证并不等同于真机运行。
真机安装、Apple 登录、广告和内购仍需要签名、原生依赖、设备及相应服务环境。

## 独立 macOS 包辅助验证

macOS 仅作为显式选择的桌面运行验证目标，仍保留竖屏画面：

```sh
Tools/validate_unity.sh compile mac
Tools/validate_unity.sh build mac
Tools/validate_player.sh
```

这会生成并独立运行 `Builds/Revival/PocketStriker.app`，以 **540 × 960** 窗口验证包内资源、
双队伍战斗和标题界面。测试只在显式开关下启用，完成或失败后退出。独立包报告检查实际
Unity 版本及竖屏视口，旧版本编译的包不能替代当前版本的验证结果。
也可用 `Tools/validate_unity.sh startup mac` 检查桌面目标的编辑器启动。

## 日志与报告

日志写入 `Logs/Revival/check-ios.log`、`startup-ios.log`、`compile-ios.log`、`build-ios.log`；
显式 macOS 验证使用相应的 `-mac.log`。JSON 报告包含 Unity 实际／预期版本、当前构建平台、
屏幕方向、默认／实际尺寸、检查结果和错误，并保留各平台的报告副本。
独立包日志和报告为 `player.log`、`player-report.json`。

以当前版本生成的报告为验证依据。若 Unity 安装位置不同，可设置 `UNITY_EDITOR_PATH`
为编辑器可执行文件路径。交互模式下的验证会拒绝切换存在未保存修改的场景，避免丢失编辑内容。

## 更新共享代码

参见 [来源、迁移和更新流程](Tools/MCombatShared.md)。共享包保持上游原样，项目差异
放在 `Assets` 适配层中。使用 `Tools/sync_mcombat_shared.py` 预览固定提交的更新；
不要直接运行上游面向多个项目的批量 SourceSync 脚本。

更多依赖版本与回归检查见 [依赖升级记录](Tools/DependencyUpgrade.md)；
移植范围和最终验证结果见 [Unity 6000.5 升级记录](Tools/Unity6000.5Upgrade.md)。
