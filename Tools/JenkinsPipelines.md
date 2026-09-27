# PocketStriker Jenkins 管线维护记录

检查日期：2026-09-27。

## 生效配置

- [CustomIOSBuild_V](http://localhost:8080/job/CustomIOSBuild_V/)：`MCombat_tool` master 的 `pipeline_script/PocketStriker/ios.groovy`。
- [AssetDev_V](http://localhost:8080/job/AssetDev_V/)：`MCombat_tool` master 的 `pipeline_script/PocketStriker/assets.groovy`。
- 两个任务的 `UNITY_VERSION` 使用 Jenkins 原生单选参数，仅包含 `6000.5.1f1`（Unity 6.5）；启动时还会检查项目版本、编辑器和目标平台模块。
- `AssetDev_V` 默认只构建 iOS 资源（`IOS=true`、`ANDROID=false`）；Android 保留为手动可选项，不再自动参与验证或构建。
- 两个任务各用自己的默认工作目录，禁止同一任务并发运行。资源任务已移除会导致共用目录／额外 `@2` 分配的 `CUSTOM_WORKSPACE` 参数。
- 原来其他项目使用的通用脚本保持不变。

## 修复内容

- 禁用工具仓库在 Unity 项目根目录的默认检出，直接检出所选项目分支，并保持递归子模块的固定版本。
- 缓存清理明确只处理工作目录中的 `Library`；创建日志目录，检查必要配置和输出，保留失败日志。
- Addressables 构建检查实际返回结果；缺失配置、无效 profile 或构建错误会使批处理失败。
- 客户端正确解析带空格的命令行参数，明确接受 `-assetProfile`。管线同时传入机器名以选择相应 iOS 签名配置。
- Xcode 配置使用实际的 `Debug`／`Release`；统一归档路径；IPA 导出不再把 workspace 当作 project；自动定位生成的 IPA。
- 安装 Xcode 27 官方 Metal Toolchain 27A266a；管线增加 `xcrun metal --version` 启动检查。
- CocoaPods 使用 UTF-8 环境。资源上传读取 `AWS_PROFILE`，验证 catalog/hash/bundle 完整性，并先上传 bundle、再上传 catalog。
- macOS Bash 3.2 的可选缓存参数不再展开空数组，避免默认 `AWS_CACHE=false` 时失败。

## 不发布的验证方式

在 Build with Parameters 中勾选 `VALIDATE_ONLY`：

- iOS：执行资源构建、Unity Xcode 导出、CocoaPods 和未签名原生编译，跳过钥匙串、IPA 导出和 App Store 上传。
- 资源：执行所选 iOS／Android 资源构建并检查输出，跳过 S3 上传。

该参数默认为 false，保留原有正常构建／上传行为。正常 Release iOS 构建仍会上传 App Store；正常资源构建仍会上传目标 S3 路径。

## 验证结果

- Unity 6000.5.1f1 iOS 编译通过：`Logs/Revival/compile-iOS-report.json`，`passed=true`，`errors=[]`。
- Jenkins 实际安装版本的 Declarative Pipeline 校验通过；Groovy 2.4.21 编译、11 段 Bash 语法、输出检查／参数传递／缓存开关行为验证通过。
- C# 构建入口的 17 项行为检查和 13 项版本回归检查通过。
- AWS 只读检查通过：`mcombatDev` 身份有效，`mcombat` 桶可访问，Dev／Release 路径与项目配置一致。未写入对象，未验证 PutObject 权限。
- [`CustomIOSBuild_V #14`](http://localhost:8080/job/CustomIOSBuild_V/14/) 验证构建成功：Unity iOS 资源、Xcode 工程导出、CocoaPods 和未签名原生编译均通过，未执行签名、IPA 导出或上传。
- [`AssetDev_V #4`](http://localhost:8080/job/AssetDev_V/4/) 的 iOS 资源阶段已通过；按项目不上线 Android 的要求，中止其正在进行的 Android 阶段。
- [`AssetDev_V #5`](http://localhost:8080/job/AssetDev_V/5/) 以 `IOS=true`、`ANDROID=false`、`VALIDATE_ONLY=true` 完整通过，生成 399 个 iOS bundle 文件及 catalog/hash；Android 阶段和 S3 上传均跳过。

## 正式签名的剩余前提

机器 `rudel` 的旧 Dev profile `test_v` 已于 2025-05-05 过期。本机另有有效的 Ad Hoc 和 App Store profile，但两者均不含 Unity 工程要求的 `com.apple.developer.applesignin`；Apple Developer 中 `com.PocketStriker.BO` 的 Sign In with Apple 当前也未启用。因此正式签名、IPA 导出和上传尚未验证，预计会受该配置影响。

根据本次暂缓上线相关改动的要求，Apple Developer 账号、profile 和项目中的 `ExportOptions_{Dev,Release}.plist` 均保持原状。后续若要正式发布，需先处理 App ID 能力与 profile，再单独验证签名及上传。

## 回退与工作区

原始 Jenkins 配置备份位于 `/Users/daisei/.jenkins/backups/pocketstriker-20260927/`；该目录权限为 0700。使用配置备份回退时需通过 Jenkins 正常配置接口重新加载对应任务。

验证使用独立 Jenkins checkout 和独立的缓存副本。开发目录中原有材质和布局修改保持不变。
