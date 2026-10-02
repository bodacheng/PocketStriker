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

冒险关卡混合团战、轮换和进化模式，前两关保留单人教学；首页提供随机 Boss，
原「混沌」和固定 Boss 挑战入口已停用。规则和关卡配置见 [战斗模式说明](Tools/BattleModes.md)。
可运行 `python3 Tools/Validation/validate_battle_modes.py` 检查模式、队伍与奖励请求规则；
Unity 停止播放时，使用 **PocketStriker → Validation → Battle Modes** 验证全部冒险资源和随机 Boss 技能生成。

**PocketStriker → Validation → Tutorial** 检查六页战斗教程的点击拦截、翻页、自动战斗引导和关闭后恢复输入，
以及第 1、2 关跳过 AI 故事的规则。报告位于 `Logs/Tutorial/report.json`；此检查使用隔离的战斗 UI，
不登录账号或请求 AI 服务，不代替完整战斗和真机触控验证。

**Tutorial Layout** 另检查教程全部说明页在四种竖屏尺寸和中／英／日文下的完整文字、
安全区、箭头目标与重复排版；逐页预览和报告位于 `Logs/Tutorial/Layout`。

**Pre-battle Tutorial** 检查首次技能编辑的普通／EX 拖拽目标、自动装备和确认指示、
非法技能组的修复提示、加载顺序及按账号保存的待同步教程进度；报告位于
`Logs/Tutorial/prebattle.json`。**Tutorial Difficulty Playmode Smoke** 使用两套合法等级 1
技能组，对实际发布的前两关各运行三次自然战斗，报告位于 `Logs/Tutorial/Balance/report.json`。
它使用本地测试角色和自动战斗，不代替服务器初始库存及真机操作验证。
`python3 Tools/Validation/validate_combat_balance.py` 沿 Addressables GUID 检查实际关卡来源及数值。

UI 修改后，在停止播放的编辑器中运行 **PocketStriker → Validation → UI Layout** 和
**Check Stage Cards**：前者检查所有注册画面及常用导航组合在手机、刘海屏、iPad 下的区域边界，
后者检查三种语言、锁定状态和四位关卡编号的卡片排版。报告和预览图位于 `Logs/UILayout`。
**Arena Awards** 用真实奖励行检查列表间距和滚动首尾；**Check Fight Preparation**
用实际奖励、领取标记和多语言标题检查战斗准备页。设置页另检查六个页签内容的垂直居中。
**Startup Smoke** 可继续检查启动、标题与演示战斗的实际运行。覆盖范围见 [UI 布局验证](Tools/UILayout.md)。

Unity 的当前目标平台保存在本机缓存中，因此项目提供了启动脚本和首次会话默认平台逻辑。
显式传入 `-buildTarget`／`-activeBuildProfile`、批处理任务及会话中的手动平台切换都会保留。
需要桌面调试时使用 `Tools/open_unity.sh mac`；普通项目打开仍默认 iOS。

## iOS 和竖屏验证

关闭此项目的 Unity 编辑器后，在项目根目录执行：

```sh
Tools/validate_unity.sh check
Tools/validate_unity.sh startup
Tools/validate_unity.sh compile
python3 Tools/Validation/validate_ai_condition_bindings.py
Tools/validate_unity.sh build
python3 Tools/Validation/validate_cloudscript_contracts.py
python3 Tools/Validation/validate_dependency_upgrade.py
python3 Tools/Validation/validate_runtime_loading.py
python3 Tools/Validation/validate_downloads.py
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

标题战斗的 AI 条件使用显式委托绑定，避免只有字符串反射引用的方法在 IL2CPP 中被裁剪。
`validate_ai_condition_bindings.py` 检查已编译 iOS 玩家程序集里的直接引用是否覆盖共享包的 AI 规则；
它检查裁剪前代码，不能代替真机运行。开发模式的日志窗口按竖屏宽度缩放并避开安全区，
点击错误行可展开消息和调用堆栈，拖动列表可查看后续内容；展开行的 **Copy** 按钮复制完整日志。
控制台通过 Package Manager 固定到官方 v1.9.0，使用支持中／日／英文的 TMP 字体；迁移说明见
[依赖升级记录](Tools/DependencyUpgrade.md#debug-console-migration-2026-09-30)。
**PocketStriker → Validation → Debug Console** 检查超长堆栈的展开、复制和竖屏显示，
`python3 Tools/Validation/validate_model_loading.py` 检查角色加载失败的具体诊断与取消行为。

**Download Policy** 检查四个并发请求、30 秒无数据超时及自动重试；
`python3 Tools/Validation/validate_downloads.py` 验证共享资源合并计数、失败后的缓存复用与进度回调。
**Check Bundled UI Fonts** 检查包内字体、中文／日文备用字体、全部 Resources 界面的翻译字符
和战斗准备标题；**Check Character Rendering** 用实际角色调色板和六个画质档位检查颜色、
实时光照与阴影模型，报告与预览位于 `Logs/Fonts` 和 `Logs/Rendering`。
战斗相机按实际角色及地面范围调整 URP 阴影距离，保留各画质的图集预算；渲染检查另覆盖
六档画质的每队 12／24／48 人与最远镜头，开关对比图位于 `Logs/Rendering/ShadowCoverage`。
iOS 默认画质显式设为支持阴影的 Ultra；Startup Smoke 还检查真实战斗的材质、光源和阴影，
并保存 `Logs/Revival/startup-run-1.png`、`startup-run-2.png` 供视觉核对。
这些修复需要重新构建客户端及同次 Addressables 资源；已发布版本按资源配对流程使用新版本发布。

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

### 程序与资源版本

在 `MCombat/Version Sync` 设置一个版本号。程序、资源 URL、catalog 名称共用此版本；Jenkins 构建号自动生成。先由独立资源任务构建并发布 Dev/Release 资源，再运行对应环境的程序任务。程序构建复用已编译的本地 bundle 并随包携带，远端 bundle 在运行时从对应地址下载；程序任务不再编译或发布 asset。详细流程和 Jenkins 工具仓库位置见 [Tools/JenkinsPipelines.md](Tools/JenkinsPipelines.md)。
